namespace Bash.Lexer;

/// <summary>
/// Converts a raw input string into a flat token stream.
/// Phase 4 additions: $'...' ANSI-C quoting, heredoc body capture.
///
/// Heredoc protocol:
///   1. When &lt;&lt; or &lt;&lt;- is seen, a sentinel HeredocPending record is queued.
///   2. The delimiter word that follows on the same line is lexed normally as
///      a Word/SingleQuoted/DoubleQuoted token.
///   3. After the Newline that ends that command line, Tokenize reads pending
///      heredocs, resolves each delimiter from the tokens already emitted, and
///      inserts a HeredocBody token whose value is the raw body text.
/// </summary>
public sealed class Lexer
	{
	/// <summary>Precedes a backslash-escaped `* ? [ { } , ~` inside a word token, so the parser can
	/// emit it as a QUOTED part. U+FDD0 is a Unicode noncharacter: it never occurs in text.</summary>
	public const char EscapeMark = '﷐';

	private readonly string _src;
	private int _pos;
	private int _line;
	private int _col;
	private bool _hadSpace; // set by Tokenize before each NextToken call

	// Each entry: (stripTabs, noExpand).  noExpand = delimiter was quoted.
	// Delimiter word resolved post-hoc from emitted token stream.
	private readonly Queue<(bool stripTabs, int tokenIndexAfterOp)> _heredocQueue = new();

	public Lexer(string src)
		{
		_src = src;
		_pos = 0;
		_line = 1;
		_col  = 1;
		}

	/// <summary>The source text being tokenised (the parser slices function definitions from it).</summary>
	public string Source => _src;

	// ── public API ───────────────────────────────────────────────────────────

	public List<Token> Tokenize()
		{
		var tokens = new List<Token>();

		while (true)
			{
			_hadSpace = _pos < _src.Length && _src[_pos] is ' ' or '\t' or '\r';
			bool hadSpace = _hadSpace;
			SkipWhitespace();

			if (_pos >= _src.Length)
				{
				if (_heredocQueue.Count > 0) FlushHeredocs(tokens);   // heredoc delimited by EOF
				tokens.Add(Token.Eof(_line, _col));
				break;
				}

			var tok = NextToken(tokens.Count);
			if (tok is null) continue;

			// Stamp leading-space flag: re-create the record with HasLeadingSpace set.
			// Newlines and the first token never carry the flag (newline is its own token).
			if (hadSpace && tok.Type != TokenType.Newline)
				tok = tok with { HasLeadingSpace = true };

			tokens.Add(tok);

			if (tok.Type == TokenType.Newline)
				FlushHeredocs(tokens);
			}

		return tokens;
		}

	private void FlushHeredocs(List<Token> tokens)
		{
		while (_heredocQueue.Count > 0)
			{
			var (stripTabs, delimIdx) = _heredocQueue.Dequeue();

			// The delimiter token is the Word/SingleQuoted/DoubleQuoted token
			// immediately after the << operator token.
			string rawDelim = delimIdx < tokens.Count ? tokens[delimIdx].Value : "";
			// If it was single-quoted, expansions are suppressed (noExpand),
			// and we strip the quoting for matching purposes.
			bool noExpand = delimIdx < tokens.Count &&
				tokens[delimIdx].Type is TokenType.SingleQuoted;
			string delim = rawDelim; // already unquoted by lexer

			tokens.Add(ConsumeHeredocBody(delim, stripTabs, delimIdx < tokens.Count ? tokens[delimIdx].Line : _line));
			}
		}

	// ── core dispatch ─────────────────────────────────────────────────────────

	private Token? NextToken(int currentTokenCount)
		{
		char c = Peek();

		if (c == '\n') return ConsumeNewline();
		if (c == '#')
			{
			// bash: `#` starts a comment only at the START of a word. Inside one it is an
			// ordinary character: `$((10#0010))`, `a#b`, `$x#y`. Until 2026-09-08 the rest of the
			// line vanished after `10` — `echo $((10#0010))` printed 10 by coincidence,
			// `x=$((10#0010)); echo $x` printed nothing, `( echo $((10#0010)) )` never closed
			// (installer-79's report).
			bool wordStart = _pos == 0 || _src[_pos - 1] is ' ' or '\t' or '\n' or '\r' or ';' or '|' or '&' or '(' or ')' or '<' or '>';
			if (wordStart) { SkipComment(); return null; }
			return ConsumeWord(hashStart: true);
			}
		if (c == '\'') return ConsumeSingleQuoted();
		if (c == '"')  return ConsumeDoubleQuoted();
		if (c == '`')  return Consume1(TokenType.Backtick);
		if (c == '$')  return ConsumeDollar();
		if (IsOperatorStart(c)) return ConsumeOperator(currentTokenCount);

		// { and } are brace-group operators only when they appear with leading space
		// (i.e. at command/word start). When adjacent to other word chars they are
		// part of a brace expression like {a..e} or pre{x,y}suf.
		if (c == '{')
			{
			// '{' is a brace-group operator only when the character immediately
			// following it is whitespace or end-of-input (i.e. it stands alone).
			// '{a..e}' has a non-space char after '{', so it's a word.
			bool nextIsSpace = _pos + 1 >= _src.Length || _src[_pos + 1] is ' ' or '\t' or '\n' or '\r' or ';';
			if (nextIsSpace) { int l = _line, col = _col; Advance(); return Make(TokenType.LBrace, "{", l, col); }
			return ConsumeWord();
			}
		if (c == '}')
			{
			// '}' is a brace-group terminator in command position: when it has leading
			// space, OR begins a line (preceded by a newline), OR follows a ';'. This
			// covers the common multi-line function/group form where '}' sits alone at
			// column 1. A '}' adjacent to word chars ({a..e}, pre{x,y}) stays a word.
			char prev = _pos > 0 ? _src[_pos - 1] : '\n';
			if (_hadSpace || prev is '\n' or ';')
				{ int l = _line, col = _col; Advance(); return Make(TokenType.RBrace, "}", l, col); }
			return ConsumeWord();
			}

		return ConsumeWord();
		}

	// ── operators ─────────────────────────────────────────────────────────────

	private static bool IsOperatorStart(char c) =>
		c is '|' or '&' or ';' or '<' or '>' or '(' or ')';

	private Token ConsumeOperator(int currentTokenCount)
		{
		int startLine = _line, startCol = _col;
		char c = Advance();

		switch (c)
			{
			case '|':
				if (Peek() == '|') { Advance(); return Make(TokenType.PipePipe,            "||",  startLine, startCol); }
				if (Peek() == '&') { Advance(); return Make(TokenType.PipeAmpersand,        "|&",  startLine, startCol); }
				return Make(TokenType.Pipe, "|", startLine, startCol);

			case '&':
				if (Peek() == '&') { Advance(); return Make(TokenType.AmpersandAmpersand,  "&&",  startLine, startCol); }
				if (Peek() == '>')
					{
					Advance();
					if (Peek() == '>') { Advance(); return Make(TokenType.AmpersandGreaterGreater, "&>>", startLine, startCol); }
					return Make(TokenType.AmpersandGreater, "&>", startLine, startCol);
					}
				return Make(TokenType.Ampersand, "&", startLine, startCol);

			case ';':
				if (Peek() == ';') { Advance(); return Make(TokenType.SemicolonSemicolon,  ";;",  startLine, startCol); }
				return Make(TokenType.Semicolon, ";", startLine, startCol);

			case '<':
				if (Peek() == '<')
					{
					Advance();
					if (Peek() == '<') { Advance(); return Make(TokenType.LessLessLess, "<<<", startLine, startCol); }
					bool strip = Peek() == '-';
					if (strip) Advance();
					// Queue: delimiter token will be at currentTokenCount + 1
					// (currentTokenCount + 0 = this << token itself, not yet added;
					//  the delimiter word will be the next token emitted)
					_heredocQueue.Enqueue((strip, currentTokenCount + 1));
					return Make(strip ? TokenType.LessLessDash : TokenType.LessLess,
						strip ? "<<-" : "<<", startLine, startCol);
					}
				if (Peek() == '&') { Advance(); return Make(TokenType.LessAmpersand,  "<&", startLine, startCol); }
				if (Peek() == '>') { Advance(); return Make(TokenType.LessGreater,     "<>", startLine, startCol); }
				if (Peek() == '(') { Advance(); return Make(TokenType.LessLParen,      "<(", startLine, startCol); }
				return Make(TokenType.Less, "<", startLine, startCol);

			case '>':
				if (Peek() == '>') { Advance(); return Make(TokenType.GreaterGreater,  ">>", startLine, startCol); }
				if (Peek() == '&') { Advance(); return Make(TokenType.GreaterAmpersand,">&", startLine, startCol); }
				if (Peek() == '|') { Advance(); return Make(TokenType.GreaterPipe,     ">|", startLine, startCol); }
				if (Peek() == '(') { Advance(); return Make(TokenType.GreaterLParen,   ">(", startLine, startCol); }
				return Make(TokenType.Greater, ">", startLine, startCol);

			case '(':
				// "((" opens an arithmetic command: capture the raw interior up to the
				// matching "))" (balancing parentheses, skipping quoted text).
				if (Peek() == '(')
					{
					int save = _pos, saveLine = _line, saveCol = _col;
					Advance();
					var sb = new System.Text.StringBuilder();
					int depth = 0;
					bool closed = false;
					while (_pos < _src.Length)
						{
						char ch = Peek();
						if (ch == '\'' || ch == '"')
							{
							sb.Append(Advance());
							while (_pos < _src.Length && Peek() != ch) sb.Append(Advance());
							if (_pos < _src.Length) sb.Append(Advance());
							continue;
							}
						if (ch == '(') { depth++; sb.Append(Advance()); continue; }
						if (ch == ')')
							{
							if (depth == 0 && _pos + 1 < _src.Length && _src[_pos + 1] == ')')
								{ Advance(); Advance(); closed = true; break; }
							if (depth > 0) depth--;
							sb.Append(Advance()); continue;
							}
						sb.Append(Advance());
						}
					if (closed) return Make(TokenType.ArithCommand, sb.ToString(), startLine, startCol);
					// no matching "))" — treat as an ordinary "(" (nested subshell)
					_pos = save; _line = saveLine; _col = saveCol;
					}
				return Make(TokenType.LParen, "(", startLine, startCol);
			case ')':  return Make(TokenType.RParen, ")", startLine, startCol);
			case '{':  return Make(TokenType.LBrace, "{", startLine, startCol);
			case '}':  return Make(TokenType.RBrace, "}", startLine, startCol);

			default:
				throw new LexException($"Unexpected operator char '{c}'", startLine, startCol);
			}
		}

	// ── heredoc body ──────────────────────────────────────────────────────────

	/// <summary>Warnings bash prints while reading the text, before running it: (line, text).</summary>
	public List<(int Line, string Text)> Warnings { get; } = [];

	private Token ConsumeHeredocBody(string delimiter, bool stripTabs, int opLine)
		{
		int startLine = _line, startCol = _col;
		var sb = new System.Text.StringBuilder();
		bool delimited = false;

		while (_pos < _src.Length)
			{
			var lineSb = new System.Text.StringBuilder();
			while (_pos < _src.Length && Peek() != '\n')
				lineSb.Append(Advance());
			if (_pos < _src.Length) Advance(); // consume '\n'

			var line = lineSb.ToString();
			var check = stripTabs ? line.TrimStart('\t') : line;

			if (check == delimiter) { delimited = true; break; }

			sb.Append(check);
			sb.Append('\n');
			}

		// the end of the text closed it: bash runs it anyway, with this warning at the last line read
		// (C#Bash was silent until 2026-10-10)
		if (!delimited)
			Warnings.Add((_src.EndsWith('\n') ? _line - 1 : _line,
			              $"warning: here-document at line {opLine} delimited by end-of-file (wanted `{delimiter}')"));

		return Make(TokenType.HeredocBody, sb.ToString(), startLine, startCol);
		}

	// ── dollar / expansions ───────────────────────────────────────────────────

	private Token ConsumeDollar()
		{
		int startLine = _line, startCol = _col;
		Advance(); // consume '$'

		if (_pos >= _src.Length)
			return Make(TokenType.Dollar, "$", startLine, startCol);

		char next = Peek();

		if (next == '(')
			{
			Advance();
			if (_pos < _src.Length && Peek() == '(')
				{ Advance(); return Make(TokenType.DollarDollarLParen, "$((", startLine, startCol); }
			return Make(TokenType.DollarLParen, "$(", startLine, startCol);
			}

		if (next == '{')
			{
			// The whole ${...} body as one raw string (so `#` is no comment and operators inside are
			// not tokens); the parser's ParseBraceExpansion handles the content. Where it ends is
			// ConstructEnd's rule. An unclosed ${ is an error (bash: unexpected EOF looking for `}'),
			// never the rest of the file, which a nested ${ counted twice used to swallow silently.
			int end = ConstructEnd(_src, _pos - 1);
			if (end < 0)
				throw new LexException("Unterminated parameter expansion ${", startLine, startCol) { Incomplete = true, Missing = '}' };
			Advance(); // {
			var sb = new System.Text.StringBuilder();
			while (_pos < end - 1) sb.Append(Advance());
			Advance(); // }
			return Make(TokenType.DollarLBrace, sb.ToString(), startLine, startCol);
			}

		// $'...' ANSI-C quoting
		if (next == '\'')
			{
			Advance(); // consume opening '
			var sb = new System.Text.StringBuilder();
			while (_pos < _src.Length && Peek() != '\'')
				{
				if (Peek() == '\\' && _pos + 1 < _src.Length)
					{ Advance(); sb.Append(ConsumeAnsiEscape()); }
				else
					sb.Append(Advance());
				}
			if (_pos < _src.Length) Advance(); // consume closing '
			return Make(TokenType.DollarSingleQuote, sb.ToString(), startLine, startCol);
			}

		// plain $VAR, $$, $?, $#, $@, $*, $!, $-, $0..$9.
		// Consume the parameter name here (so $# doesn't start a comment, $@/$* aren't
		// mis-tokenised) and route it through the DollarLBrace path — the parser turns
		// that into a BraceExpansionPart, the same handling as ${name}.
		if (next is '@' or '*' or '#' or '?' or '$' or '!' or '-')
			{
			Advance();
			return Make(TokenType.DollarLBrace, next.ToString(), startLine, startCol);
			}
		if (char.IsDigit(next))
			{
			// Unbraced positional is a single digit ($10 == ${1}0), per bash.
			Advance();
			return Make(TokenType.DollarLBrace, next.ToString(), startLine, startCol);
			}
		if (char.IsLetter(next) || next == '_')
			{
			var sb = new System.Text.StringBuilder();
			while (_pos < _src.Length && (char.IsLetterOrDigit(Peek()) || Peek() == '_'))
				sb.Append(Advance());
			return Make(TokenType.DollarLBrace, sb.ToString(), startLine, startCol);
			}

		// Lone $ (not a parameter introducer).
		return Make(TokenType.Dollar, "$", startLine, startCol);
		}

	private char ConsumeAnsiEscape()
		{
		char c = Advance();
		return c switch
			{
			'a'        => '\a',
			'b'        => '\b',
			'e' or 'E' => '\x1b',
			'f'        => '\f',
			'n'        => '\n',
			'r'        => '\r',
			't'        => '\t',
			'v'        => '\v',
			'\\'       => '\\',
			'\''       => '\'',
			'"'        => '"',
			'?'        => '?',
			// `\xNN` and `\NNN` are BYTE escapes: $'\xff' is the byte 0xFF, not U+00FF (which would
			// encode as two bytes). `\u`/`\U` are code-point escapes and stay characters.
			'x'        => Bash.Evaluator.ShellEncoding.ByteChar(ConsumeHexEscape(2)),
			'u'        => (char)ConsumeHexEscape(4),
			'U'        => (char)ConsumeHexEscape(8),
			>= '0' and <= '7' => Bash.Evaluator.ShellEncoding.ByteChar(ConsumeOctalEscape(c)),
			_          => c
			};
		}

	private int ConsumeHexEscape(int maxDigits)
		{
		int val = 0;
		for (int i = 0; i < maxDigits && _pos < _src.Length && IsHexDigit(Peek()); i++)
			val = val * 16 + HexVal(Advance());
		return val;
		}

	/// <summary>`\NNN` — up to three octal digits, the first already consumed.</summary>
	private int ConsumeOctalEscape(char first)
		{
		int val = first - '0';
		for (int i = 1; i < 3 && _pos < _src.Length && Peek() is >= '0' and <= '7'; i++)
			val = val * 8 + (Advance() - '0');
		return val;
		}

	private static bool IsHexDigit(char c) =>
		(c >= '0' && c <= '9') || (c >= 'a' && c <= 'f') || (c >= 'A' && c <= 'F');

	private static int HexVal(char c) =>
		c >= '0' && c <= '9' ? c - '0' :
		c >= 'a' && c <= 'f' ? c - 'a' + 10 :
		                       c - 'A' + 10;

	// ── quoting ───────────────────────────────────────────────────────────────

	private Token ConsumeSingleQuoted()
		{
		int startLine = _line, startCol = _col;
		Advance(); // opening '
		var sb = new System.Text.StringBuilder();
		while (_pos < _src.Length && Peek() != '\'')
			sb.Append(Advance());
		if (_pos >= _src.Length)
			throw new LexException("Unterminated single-quoted string", startLine, startCol) { Incomplete = true, Missing = '\'' };
		Advance(); // closing '
		return Make(TokenType.SingleQuoted, sb.ToString(), startLine, startCol);
		}

	private Token ConsumeDoubleQuoted()
		{
		int startLine = _line, startCol = _col;
		Advance(); // opening "
		// The interior is kept RAW (backslashes preserved): the parser's
		// ParseDoubleQuotedInterior applies bash's double-quote escape rules, where only
		// \$ \` \" \\ and \<newline> are escapes and every other backslash is literal.
		var sb = new System.Text.StringBuilder();
		while (_pos < _src.Length && Peek() != '"')
			{
			if (Peek() == '\\' && _pos + 1 < _src.Length)
				{ sb.Append(Advance()); sb.Append(Advance()); continue; }
			// A nested $( … ), $(( … )), ${ … } or ` … ` may itself contain double quotes, braces
			// and parentheses: copy it through as one unit (ConstructEnd's rule) so they don't end
			// this string. Unclosed, the string is unterminated.
			if (Peek() == '`' || (Peek() == '$' && _pos + 1 < _src.Length && (_src[_pos + 1] == '(' || _src[_pos + 1] == '{')))
				{
				int end = ConstructEnd(_src, _pos);
				if (end < 0)
					throw new LexException("Unterminated double-quoted string", startLine, startCol) { Incomplete = true, Missing = '"' };
				while (_pos < end) sb.Append(Advance());
				continue;
				}
			sb.Append(Advance());
			}
		if (_pos >= _src.Length)
			throw new LexException("Unterminated double-quoted string", startLine, startCol) { Incomplete = true, Missing = '"' };
		Advance(); // closing "
		return Make(TokenType.DoubleQuoted, sb.ToString(), startLine, startCol);
		}

	// ── the one construct matcher ─────────────────────────────────────────────

	/// <summary>
	/// The index just past the construct that opens at <paramref name="i"/> in <paramref name="s"/>,
	/// or -1 if it never closes: `'…'`, `"…"`, `` `…` ``, `$'…'`, `${…}`, `$(…)`, `$((…))`, `$name`,
	/// `$1`. Bash's single rule for where such a construct ends (parse.y, parse_matched_pair): a
	/// backslash skips the next character; quotes, backquotes, `$( )` and `${ }` inside are matched
	/// as units, so a `}` or `)` inside them closes nothing; within `${…}` only a `${` opens a brace
	/// level (a bare `{` is literal), within `$( )` a bare `(` does. Every scanner of these
	/// constructs uses this (2026-10-10): there were seven hand-written copies, each with its own
	/// counting, and `${…}` broke in several (a nested `${` counted twice swallowed the rest of a
	/// script; `\}`, `"}"`, `$(echo })` and a bare `{` closed or failed to close the wrong brace).
	/// </summary>
	public static int ConstructEnd(string s, int i)
		{
		if (i >= s.Length) return -1;
		char c = s[i];
		if (c == '\'') { int e = s.IndexOf('\'', i + 1); return e < 0 ? -1 : e + 1; }
		if (c == '`')
			{
			for (int j = i + 1; j < s.Length; j++)
				{
				if (s[j] == '\\') { j++; continue; }
				if (s[j] == '`') return j + 1;
				}
			return -1;
			}
		if (c == '"')
			{
			for (int j = i + 1; j < s.Length; )
				{
				char ch = s[j];
				if (ch == '\\') { j += 2; continue; }
				if (ch == '"') return j + 1;
				if (ch == '`' || (ch == '$' && j + 1 < s.Length && s[j + 1] is '{' or '('))
					{
					j = ConstructEnd(s, j);
					if (j < 0) return -1;
					continue;
					}
				j++;
				}
			return -1;
			}
		if (c != '$' || i + 1 >= s.Length) return i + 1;
		char n = s[i + 1];
		if (n == '\'')
			{
			for (int j = i + 2; j < s.Length; j++)
				{
				if (s[j] == '\\') { j++; continue; }
				if (s[j] == '\'') return j + 1;
				}
			return -1;
			}
		if (n is '{' or '(')
			{
			char close = n == '{' ? '}' : ')';
			int depth = 1;
			for (int j = i + 2; j < s.Length; )
				{
				char ch = s[j];
				if (ch == '\\') { j += 2; continue; }
				if (ch is '\'' or '"' or '`' || (ch == '$' && j + 1 < s.Length && s[j + 1] is '{' or '(' or '\''))
					{
					j = ConstructEnd(s, j);
					if (j < 0) return -1;
					continue;
					}
				if (n == '(' && ch == '(') depth++;
				else if (ch == close && --depth == 0) return j + 1;
				j++;
				}
			return -1;
			}
		if (char.IsLetter(n) || n == '_')
			{
			int j = i + 1;
			while (j < s.Length && (char.IsLetterOrDigit(s[j]) || s[j] == '_')) j++;
			return j;
			}
		return i + 2;   // $1 $@ $* $# $? $$ $! $- $0
		}

	// ── words ─────────────────────────────────────────────────────────────────

	private static bool IsWordChar(char c) =>
		c is not (' ' or '\t' or '\r' or '\n' or '|' or '&' or ';' or '<' or '>' or
				  '(' or ')' or '\'' or '"' or '`' or '$' or '#');

	/// <param name="hashStart">The word begins with a `#` that is not a comment (it follows
	/// a word character); a `#` after the first character is always part of the word.</param>
	private Token? ConsumeWord(bool hashStart = false)
		{
		int startLine = _line, startCol = _col;
		var sb = new System.Text.StringBuilder();
		bool continued = false;
		while (_pos < _src.Length && (IsWordChar(Peek()) || (Peek() == '#' && (sb.Length > 0 || hashStart))))
			{
			if (Peek() == '\\' && _pos + 1 < _src.Length)
				{
				Advance();
				// line continuation (LF or CRLF): the backslash-newline pair vanishes entirely
				if (Peek() == '\n') { Advance(); continued = true; continue; }
				if (Peek() == '\r' && _pos + 1 < _src.Length && _src[_pos + 1] == '\n') { Advance(); Advance(); continued = true; continue; }
				char escaped = Advance();
				// an escaped glob, brace or tilde character must stay QUOTED for the expansions that
				// come after lexing: the parser turns a marked character into a quoted part. Without
				// it `echo \*` and `find -name \*.txt` globbed (2026-10-03). An escaped backslash too:
				// left bare, `\\` reached a pattern as a lone `\` that escaped the next character, so
				// `case 'a\b' in *\\*)` and `[[ $x =~ \\ ]]` failed and `*\\*` matched `a*` (2026-10-08).
				if (escaped is '*' or '?' or '[' or '{' or '}' or ',' or '~' or '\\') sb.Append(EscapeMark);
				sb.Append(escaped);
				}
			else
				sb.Append(Advance());
			}
		// `cmd arg \<newline>| next`: the continuation was the whole "word" — there is no word.
		// (Reported 2026-09-05: every downstream tool received an empty argument.)
		if (sb.Length == 0 && continued) return null;
		var value = sb.ToString();
		var type = value.Length > 0 && value.All(char.IsAsciiDigit)
			? TokenType.Digit
			: TokenType.Word;
		return Make(type, value, startLine, startCol);
		}

	// ── helpers ───────────────────────────────────────────────────────────────

	private Token ConsumeNewline()
		{
		int l = _line, col = _col;
		Advance();
		return Make(TokenType.Newline, "\n", l, col);
		}

	private Token Consume1(TokenType type)
		{
		int l = _line, col = _col;
		string v = Advance().ToString();
		return Make(type, v, l, col);
		}

	private static Token Make(TokenType type, string value, int line, int col) =>
		new(type, value, line, col);

	private void SkipWhitespace()
		{
		while (_pos < _src.Length && Peek() is ' ' or '\t' or '\r')
			Advance();
		}

	private void SkipComment()
		{
		while (_pos < _src.Length && Peek() != '\n')
			Advance();
		}

	// Bounds-safe: returns '\0' at/after end of input so the many `Peek() == 'x'`
	// lookahead checks (operators, $-forms) naturally see "no char" at EOF instead
	// of throwing. Loops that scan content already guard with `_pos < _src.Length`.
	private char Peek() => _pos < _src.Length ? _src[_pos] : '\0';

	private char Advance()
		{
		char c = _src[_pos++];
		if (c == '\n') { _line++; _col = 1; }
		else _col++;
		return c;
		}
	}
