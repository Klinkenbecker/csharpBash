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

			tokens.Add(ConsumeHeredocBody(delim, stripTabs));
			}
		}

	// ── core dispatch ─────────────────────────────────────────────────────────

	private Token? NextToken(int currentTokenCount)
		{
		char c = Peek();

		if (c == '\n') return ConsumeNewline();
		if (c == '#')  { SkipComment(); return null; }
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
				return Make(TokenType.Ampersand, "&", startLine, startCol);

			case ';':
				if (Peek() == ';') { Advance(); return Make(TokenType.SemicolonSemicolon,  ";;",  startLine, startCol); }
				return Make(TokenType.Semicolon, ";", startLine, startCol);

			case '<':
				if (Peek() == '<')
					{
					Advance();
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
				return Make(TokenType.Less, "<", startLine, startCol);

			case '>':
				if (Peek() == '>') { Advance(); return Make(TokenType.GreaterGreater,  ">>", startLine, startCol); }
				if (Peek() == '&') { Advance(); return Make(TokenType.GreaterAmpersand,">&", startLine, startCol); }
				if (Peek() == '|') { Advance(); return Make(TokenType.GreaterPipe,     ">|", startLine, startCol); }
				return Make(TokenType.Greater, ">", startLine, startCol);

			case '(':  return Make(TokenType.LParen, "(", startLine, startCol);
			case ')':  return Make(TokenType.RParen, ")", startLine, startCol);
			case '{':  return Make(TokenType.LBrace, "{", startLine, startCol);
			case '}':  return Make(TokenType.RBrace, "}", startLine, startCol);

			default:
				throw new LexException($"Unexpected operator char '{c}'", startLine, startCol);
			}
		}

	// ── heredoc body ──────────────────────────────────────────────────────────

	private Token ConsumeHeredocBody(string delimiter, bool stripTabs)
		{
		int startLine = _line, startCol = _col;
		var sb = new System.Text.StringBuilder();

		while (_pos < _src.Length)
			{
			var lineSb = new System.Text.StringBuilder();
			while (_pos < _src.Length && Peek() != '\n')
				lineSb.Append(Advance());
			if (_pos < _src.Length) Advance(); // consume '\n'

			var line = lineSb.ToString();
			var check = stripTabs ? line.TrimStart('\t') : line;

			if (check == delimiter) break;

			sb.Append(check);
			sb.Append('\n');
			}

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
			Advance(); // consume {
			// Read the entire ${...} body as a raw string to avoid # being
			// treated as a comment and other operator mis-tokenisation inside
			// parameter expansions. The parser's ParseBraceExpansion will
			// handle the content.
			var sb = new System.Text.StringBuilder();
			int depth = 1;
			while (_pos < _src.Length && depth > 0)
				{
				char ch = Peek();
				if (ch == '{') depth++;
				if (ch == '}') { depth--; if (depth == 0) { Advance(); break; } }
				// Handle nested ${ or $( so we don't miscount }
				if (ch == '$' && _pos + 1 < _src.Length)
					{
					sb.Append(Advance()); // $
					char after = Peek();
					if (after == '{') depth++;
					else if (after == '(') { /* don't affect brace depth */ }
					continue;
					}
				// Handle quoted strings inside ${} so } inside quotes isn't counted
				if (ch == '\'' || ch == '"')
					{
					char q = Advance();
					sb.Append(q);
					while (_pos < _src.Length && Peek() != q)
						sb.Append(Advance());
					if (_pos < _src.Length) sb.Append(Advance()); // closing quote
					continue;
					}
				sb.Append(Advance());
				}
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
			'0'        => '\0',
			'x'        => ConsumeHexEscape(2),
			'u'        => ConsumeHexEscape(4),
			'U'        => ConsumeHexEscape(8),
			_          => c
			};
		}

	private char ConsumeHexEscape(int maxDigits)
		{
		int val = 0;
		for (int i = 0; i < maxDigits && _pos < _src.Length && IsHexDigit(Peek()); i++)
			val = val * 16 + HexVal(Advance());
		return (char)val;
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
			throw new LexException("Unterminated single-quoted string", startLine, startCol) { Incomplete = true };
		Advance(); // closing '
		return Make(TokenType.SingleQuoted, sb.ToString(), startLine, startCol);
		}

	private Token ConsumeDoubleQuoted()
		{
		int startLine = _line, startCol = _col;
		Advance(); // opening "
		var sb = new System.Text.StringBuilder();
		while (_pos < _src.Length && Peek() != '"')
			{
			if (Peek() == '\\' && _pos + 1 < _src.Length)
				{ Advance(); sb.Append(Advance()); }
			else
				sb.Append(Advance());
			}
		if (_pos >= _src.Length)
			throw new LexException("Unterminated double-quoted string", startLine, startCol) { Incomplete = true };
		Advance(); // closing "
		return Make(TokenType.DoubleQuoted, sb.ToString(), startLine, startCol);
		}

	// ── words ─────────────────────────────────────────────────────────────────

	private static bool IsWordChar(char c) =>
		c is not (' ' or '\t' or '\r' or '\n' or '|' or '&' or ';' or '<' or '>' or
				  '(' or ')' or '\'' or '"' or '`' or '$' or '#');

	private Token ConsumeWord()
		{
		int startLine = _line, startCol = _col;
		var sb = new System.Text.StringBuilder();
		while (_pos < _src.Length && IsWordChar(Peek()))
			{
			if (Peek() == '\\' && _pos + 1 < _src.Length)
				{
				Advance();
				if (Peek() == '\n') { Advance(); continue; } // line continuation
				sb.Append(Advance());
				}
			else
				sb.Append(Advance());
			}
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
