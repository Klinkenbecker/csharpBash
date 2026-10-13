using Bash.Lexer;

namespace Bash.Parser;

/// <summary>
/// Recursive-descent parser.  Consumes a flat token stream from the Lexer
/// and produces an AST rooted at <see cref="Script"/>.
///
/// Grammar (simplified, bash subset):
///   script       := list* EOF
///   list         := pipeline ( list_op pipeline )* list_terminator?
///   list_op      := '&&' | '||'
///   list_term    := ';' | '&' | NEWLINE
///   pipeline     := '!'? command ( ('|' | '|&') command )*
///   command      := simple_command | compound_command | function_def
///   simple_cmd   := (ASSIGN* | WORD) word* redirect*
///   compound_cmd := if_cmd | while_cmd | until_cmd | for_cmd | case_cmd
///                 | brace_group | subshell | cond_expr
/// </summary>
public sealed class Parser
	{
	private readonly List<Token> _tokens;
	private readonly string? _src;
	private int[]? _lineStarts;
	private int _pos;

	/// <param name="source">The text the tokens came from; lets the parser keep the
	/// verbatim source of function definitions (for `declare -f`).</param>
	public Parser(List<Token> tokens, string? source = null)
		{
		_tokens = tokens;
		_src = source;
		_pos = 0;
		}

	/// <summary>A parser that has the lexer produce tokens as it asks for them (item 6, phase 2): with
	/// <see cref="ParseNext"/>, text after a top-level `exit` is never even lexed (bash: an installer's
	/// payload). A lexing error is the lexer's <c>DeferredError</c>, raised where it is reached.</summary>
	public Parser(Lexer.Lexer lexer, string? source = null) : this(new List<Token>(), source)
		{
		_lexer = lexer;
		}

	private readonly Lexer.Lexer? _lexer;

	/// <summary>Lex on demand until token <paramref name="i"/> exists or the lexer is done.</summary>
	private void Fill(int i)
		{
		while (i >= _tokens.Count && _lexer is { Done: false }) _lexer.Step(_tokens);
		}

	// ── public entry point ────────────────────────────────────────────────────

	public Script Parse()
		{
		var nodes = new List<Node>();
		for (Node? node; (node = ParseNext()) is not null;) nodes.Add(node);
		return new Script(nodes);
		}

	/// <summary>A lexing error the lexer held back (<c>Tokenize(deferErrors: true)</c>): raised when the
	/// parser reaches the end of the tokens that came before it.</summary>
	public Exception? DeferredError { get; init; }

	/// <summary>True when only newlines are left before the end of the tokens (looked at without raising
	/// a deferred error).</summary>
	public bool AtEnd
		{
		get
			{
			int i = _pos;
			Fill(i);
			while (_tokens[i].Type == TokenType.Newline) Fill(++i);
			return _tokens[i].Type == TokenType.Eof;
			}
		}

	/// <summary>The next top-level command, or null at the end: bash reads, parses and runs one at a
	/// time, so the commands before a syntax error run (DECISIONS 2026-10-11). Newlines are skipped
	/// BEFORE a command, never after: looking past a finished command could reach an error that bash
	/// would only meet after running it.</summary>
	public Node? ParseNext()
		{
		while (true)
			{
			SkipNewlines();
			if (IsEof()) return null;
			// a word that only closes a construct (`fi`, `done`, `esac`, `then`, `do`, `else`, `elif`),
			// with none open, is a syntax error (bash, status 2), not a command: it ran as one, status
			// 127, until 2026-10-08, and `bash -n` passed it. Unquoted only: `'fi'` is an ordinary word.
			if (Peek().Type == TokenType.Word && IsListTerminator())
				throw Error($"syntax error near unexpected token '{Peek().Value}'");
			int before = _pos;
			var node = ParseList(topLevel: true);
			// A token nothing could consume (a stray ')' or '}') must be an error, never
			// a silent spin: without this the loop never advances.
			if (_pos == before)
				throw Error($"syntax error near unexpected token '{Peek().Value}'");
			if (node is not null) return node;
			}
		}

	/// <summary>Lex and parse a string as a sequence of words (used by `declare -a x=(…)`
	/// and friends to expand the elements of an array literal given as an argument).</summary>
	public static List<Word> ParseWords(string src)
		{
		var p = new Parser(new Lexer.Lexer(src).Tokenize(), src);
		var words = new List<Word>();
		while (!p.IsEof())
			{
			if (p.Peek().Type == TokenType.Newline) { p.Advance(); continue; }
			var w = p.ParseWord();
			if (w is null) throw p.Error($"unexpected token '{p.Peek().Value}' in array literal");
			words.Add(w);
			}
		return words;
		}

	// ── source slicing (function definitions) ──────────────────────────────────

	private int OffsetOf(Token t)
		{
		if (_src is null) return -1;
		if (_lineStarts is null)
			{
			var starts = new List<int> { 0 };
			for (int i = 0; i < _src.Length; i++) if (_src[i] == '\n') starts.Add(i + 1);
			_lineStarts = [.. starts];
			}
		int line = t.Line + LineShift;   // a substitution body's tokens count their lines from the body's first
		if (line < 1 || line > _lineStarts.Length) return -1;
		int off = _lineStarts[line - 1] + t.Column - 1;
		return off <= _src.Length ? off : -1;
		}

	/// <summary>For a substitution body's parser: the lines its tokens were moved back by (Rebased), so
	/// <see cref="OffsetOf"/> finds them in the file's text.</summary>
	public int LineShift { get; init; }

	/// <summary>A substitution whose body does not parse: the error waits for the substitution to run,
	/// where bash meets it. It is the error of parsing <paramref name="shown"/> alone (lines from the
	/// body's first), the text bash parses and quotes back.</summary>
	private static CommandSubstitutionPart DeferredSubstitution(string shown, bool backquoted, Exception fallback)
		{
		Exception error = fallback;
		// lexed on demand, as bash reads it: in `"a $(if then) b"` the text parsed is `if then) b"`, and
		// `then` is met before the closing quote could end the text in a lexing error
		try { new Parser(new Lexer.Lexer(shown), shown).Parse(); }
		catch (Exception e) when (e is ParseException or Lexer.LexException) { error = e; }
		return new CommandSubstitutionPart(new SubstitutionSyntaxError(error, shown, backquoted));
		}

	/// <summary>The file's text between two tokens of this parser (after <paramref name="from"/>'s
	/// <paramref name="skip"/> characters, up to <paramref name="to"/>), or null when it cannot be found.</summary>
	private string? TextBetween(Token from, int skip, Token? to)
		{
		if (_src is null || to is null) return null;
		int s = OffsetOf(from), e = OffsetOf(to);
		return s >= 0 && e >= s + skip ? _src[(s + skip)..e] : null;
		}

	/// <summary>Source text from <paramref name="from"/> up to (not including) the current token.</summary>
	private string? SliceFrom(Token from)
		{
		if (_src is null) return null;
		int start = OffsetOf(from);
		if (start < 0) return null;
		int end = IsEof() ? _src.Length : OffsetOf(Peek());
		if (end < start) return null;
		return _src[start..end].TrimEnd();
		}

	// ── list ─────────────────────────────────────────────────────────────────

	/// <param name="topLevel">A top-level command (bash's simple_list) ends at a newline, after `;` or `&`
	/// too: the next line is the next command, run after this one. Inside a compound command a newline
	/// separates the commands of one list. Only `&&` / `||` carry a top-level command over newlines.</param>
	private Node? ParseList(bool topLevel = false)
		{
		var items = new List<(Node, ListOperator?)>();

		// a closing word where a list starts is no command: `then fi` has an empty body, which the
		// caller reports at `fi` (bash). It was parsed as a command named `fi` until 2026-10-10, and
		// the error came at the end of the file.
		if (Peek().Type == TokenType.Word && IsListTerminator()) return null;   // unquoted: `'fi'` is a command name
		var first = ParsePipeline();
		if (first is null)
			return null;

		ListOperator? op = topLevel && Peek().Type == TokenType.Newline ? null : TryConsumeListOp();
		items.Add((first, op));

		while (op is not null)
			{
			// after && or || a command must follow, newlines allowed (bash: `a &&` at the end is an
			// unexpected end of file, it ran `a` until 2026-10-10); after ; & or a newline it may
			bool required = op is ListOperator.And or ListOperator.Or;
			if (topLevel && !required && Peek().Type is TokenType.Newline or TokenType.Eof) break;   // `a;` then a newline
			SkipNewlines();
			if (IsListTerminator())
				{
				if (required) throw Error("a command must follow && or ||");
				// at the top level nothing is open for it to close, and bash rejects the whole line
				// before running any of it (`echo a; fi` prints nothing)
				if (topLevel) throw Error($"syntax error near unexpected token '{Peek().Value}'");
				break;
				}
			var next = ParsePipeline();
			if (next is null)
				{
				if (required) throw Error("a command must follow && or ||");
				if (topLevel) throw Error($"syntax error near unexpected token '{Peek().Value}'");   // `echo a; )`
				break;
				}
			op = topLevel && Peek().Type == TokenType.Newline ? null : TryConsumeListOp();
			items.Add((next, op));
			}

		// at the top level the line must end here: any other token is an error found before anything on
		// the line runs (`(echo a) fi`, `echo a )`), as bash parses the whole line first
		if (topLevel && op is null && Peek().Type is not (TokenType.Newline or TokenType.Eof))
			throw Error($"syntax error near unexpected token '{Peek().Value}'");

		// bash: a command is followed by a list operator, a newline, or a token that closes the
		// enclosing construct. `echo x (y) z` is "syntax error near unexpected token `('" — not
		// three commands run back to back, which is what this parser did until 2026-09-05 (a
		// peer's quote-stripping harness turned `echo "x (y) z"` into exactly that and the
		// wrong output came back with a clean exit instead of an error).
		if (op is null && !IsEof())
			{
			var t = Peek();
			if (t.Type == TokenType.LParen || (IsWordToken() && !IsListTerminator()))
				throw Error($"syntax error near unexpected token '{t.Value}'");
			}

		return items.Count == 1 && items[0].Item2 is null
			? items[0].Item1
			: new List(items);
		}

	private bool IsListTerminator()
		{
		if (!IsWordToken()) return false;
		var v = Peek().Value;
		return v is "done" or "fi" or "esac" or "elif" or "else" or "then" or "do";
		}

	private ListOperator? TryConsumeListOp()
		{
		var t = Peek();
		switch (t.Type)
			{
			case TokenType.AmpersandAmpersand: Advance(); return ListOperator.And;
			case TokenType.PipePipe:           Advance(); return ListOperator.Or;
			case TokenType.Semicolon:          Advance(); return ListOperator.Sequential;
			case TokenType.Ampersand:          Advance(); return ListOperator.Background;
			case TokenType.Newline:            Advance(); return ListOperator.Sequential;
			default: return null;
			}
		}

	// ── pipeline ──────────────────────────────────────────────────────────────

	private Node? ParsePipeline()
		{
		SkipNewlines();
		// `time [-p] [!] pipeline`: a reserved word timing the whole pipeline (it was parsed as a
		// command name: "time: command not found", 2026-10-04)
		bool timed = false, timePosix = false;
		if (PeekWord("time"))
			{
			Advance();
			timed = true;
			if (PeekWord("-p")) { Advance(); timePosix = true; }
			}
		bool negated = false;
		if (PeekWord("!"))
			{
			Advance();
			negated = true;
			}

		var first = ParseCommand();
		if (first is null)
			return timed ? new Pipeline([(new Script([]), false)], negated) { Timed = true, TimePosix = timePosix } : null;

		if (!IsPipeToken())
			return timed ? new Pipeline([(first, false)], negated) { Timed = true, TimePosix = timePosix }
			     : negated ? new Pipeline([(first, false)], true) : first;

		var commands = new List<(Node, bool)> { (first, false) };
		while (IsPipeToken())
			{
			bool stderrToo = Peek().Type == TokenType.PipeAmpersand;
			Advance();
			SkipNewlines();
			var next = ParseCommand() ?? throw Error("Expected command after pipe");
			// `|&` belongs to the producer: its stderr also flows into the pipe
			if (stderrToo) commands[^1] = (commands[^1].Item1, true);
			commands.Add((next, false));
			}

		return new Pipeline(commands, negated) { Timed = timed, TimePosix = timePosix };
		}

	private bool IsPipeToken() =>
		Peek().Type is TokenType.Pipe or TokenType.PipeAmpersand;

	// ── command dispatch ──────────────────────────────────────────────────────

	private Node? ParseCommand()
		{
		SkipNewlines();
		var t = Peek();

		if (t.Type == TokenType.Eof)
			return null;

		// compound commands keyed on reserved words
		if (t.Type == TokenType.Word)
			{
			switch (t.Value)
				{
				case "if":     return ParseIf();
				case "while":  return ParseWhile(until: false);
				case "until":  return ParseWhile(until: true);
				case "for":    return ParseFor();
				case "case":   return ParseCase();
				}
			}

		if (t.Type == TokenType.LBrace)
			return ParseBraceGroup();

		if (t.Type == TokenType.LParen)
			return ParseSubshell();

		// (( expr )) — arithmetic command
		if (t.Type == TokenType.ArithCommand)
			{
			Advance();
			var span = SpanFrom(t);
			var redirects = ParseRedirects();
			return new ArithmeticCommand(t.Value, redirects) { Line = t.Line, Span = span };
			}

		if (t.Type == TokenType.AmpersandAmpersand
		    || t.Type == TokenType.PipePipe
		    || t.Type == TokenType.Semicolon
		    || t.Type == TokenType.Newline
		    || t.Type == TokenType.RParen
		    || t.Type == TokenType.RBrace)
			return null;

		// [[ ]] conditional
		if (t.Type == TokenType.Word && t.Value == "[[")
			return ParseConditionalExpression();

		// function definition: `function NAME [()] body`
		if (t.Type == TokenType.Word && t.Value == "function"
		    && PeekAhead(1).Type == TokenType.Word)
			{
			var kw = Advance();           // function
			var nameToken = Advance();    // NAME
			if (Peek().Type == TokenType.LParen && PeekAhead(1).Type == TokenType.RParen)
				{ Advance(); Advance(); }
			SkipNewlines();
			var body = ParseFunctionBody();
			var redirects = ParseRedirects();
			return new FunctionDef(nameToken.Value, body, redirects) { Line = kw.Line, Source = SliceFrom(kw) };
			}

		// function definition: NAME ()
		if (t.Type == TokenType.Word && !t.Value.Contains('=') && !t.Value.Contains('[')
		    && PeekAhead(1).Type == TokenType.LParen && PeekAhead(2).Type == TokenType.RParen)
			{
			var nameToken = Advance(); // NAME
			Advance(); // (
			Expect(TokenType.RParen);
			SkipNewlines();
			var body = ParseFunctionBody();
			var redirects = ParseRedirects();
			return new FunctionDef(nameToken.Value, body, redirects) { Line = nameToken.Line, Source = SliceFrom(nameToken) };
			}

		return ParseSimpleCommand();
		}

	// ── simple command ────────────────────────────────────────────────────────

	private Node? ParseSimpleCommand()
		{
		var assignments = new List<(string, Word)>();
		var args = new List<Word>();
		var redirects = new List<Redirect>();
		Word? name = null;
		var firstTok = Peek();
		int line = firstTok.Line;
		_heredocEnd = -1;

		while (true)
			{
			// consume any leading redirects (bash allows them anywhere)
			var r = TryParseRedirect();
			if (r is not null) { redirects.Add(r); continue; }

			if (IsWordToken())
				{
				var word = ParseWord()!;

				// arr=(a b c) / arr+=(d) — compound array assignment
				if (name is null && args.Count == 0 && IsArrayCompoundAssign(word, out var acName, out bool acAppend))
					{
					var open = Peek();
					Expect(TokenType.LParen);
					var values = new List<Word>();
					SkipNewlines();
					while (Peek().Type != TokenType.RParen && Peek().Type != TokenType.Eof)
						{
						if (IsWordToken()) values.Add(ParseWord()!);
						else break;
						SkipNewlines();
						}
					// bash reports these with status 1: the end of the text as "unexpected EOF while
					// looking for matching `)'" alone, an operator inside (`x=(a; b)`) as an unexpected token
					if (Peek().Type == TokenType.Eof)
						throw new ParseException("unterminated array literal", open.Line, open.Column) { Incomplete = true, Missing = ')', NoEofLine = true, Status = 1 };
					if (Peek().Type != TokenType.RParen)
						throw new ParseException("unexpected token in an array literal", Peek().Line, Peek().Column) { At = Peek(), Status = 1 };
					Advance();
					while (true) { var tr = TryParseRedirect(); if (tr is null) break; redirects.Add(tr); }
					return new ArrayCompoundAssign(acName!, values, redirects) { Append = acAppend, Line = line, Span = SpanFrom(firstTok, _heredocEnd) };
					}

				// arr[n]=val — indexed element assignment
				if (name is null && args.Count == 0 && IsArrayElementAssign(word, out var aeName, out var aeIdx, out var aeVal))
					{
					while (true) { var tr = TryParseRedirect(); if (tr is null) break; redirects.Add(tr); }
					return new ArrayElementAssign(aeName!, aeIdx!, aeVal!, redirects) { Line = line, Span = SpanFrom(firstTok, _heredocEnd) };
					}

				// NAME=VALUE scalar assignment before the command name
				if (name is null && args.Count == 0 && IsAssignment(word, out var aName, out var aVal))
					{
					assignments.Add((aName!, aVal!));
					continue;
					}

				// declare -a x=(a b) / local arr=(…) / export …: an array literal as an
				// argument to a declaration builtin is folded into one literal word
				// "x=(…)" that the builtin expands itself.
				if (name is not null && name.Parts is [LiteralPart nl] && IsDeclarationBuiltin(nl.Value)
				    && word.Parts is [LiteralPart wl] && wl.Value.EndsWith('=')
				    && Peek().Type == TokenType.LParen && !Peek().HasLeadingSpace)
					{
					args.Add(Word.Literal(wl.Value + Lexer.Lexer.ArrayLiteralMark + CollectParenText()));
					continue;
					}

				// local a=$1 / declare -x b+="$x" / readonly c[$i]=$v: an assignment-shaped argument
				// of a declaration builtin (named literally, as bash requires: `$cmd a=$1` splits)
				// expands like an assignment. Until 2026-10-08 it was split, so `local a=$1` with
				// "x y" set a=x and declared a second variable y.
				if (name is not null && name.Parts is [LiteralPart dn] && IsDeclarationBuiltin(dn.Value))
					{
					if (IsAssignment(word, out var dName, out var dVal))
						{ args.Add(new Word([new AssignmentArgPart(dName!, null, dVal!)])); continue; }
					if (IsArrayElementAssign(word, out var eName, out var eIdx, out var eVal))
						{ args.Add(new Word([new AssignmentArgPart(eName!, eIdx, eVal!)])); continue; }
					}

				if (name is null)
					name = word;
				else
					args.Add(word);
				}
			// `{` and `}` are reserved only where a command starts (bash): after the command name they
			// are ordinary words, glued to adjacent text (`echo }`, `echo a } b`, `$(echo })`). Until
			// 2026-10-10 `echo }` was a syntax error, so `${u:-$(echo })}` could not work.
			else if (name is not null && Peek().Type is TokenType.RBrace or TokenType.LBrace)
				{
				var braceParts = new List<WordPart> { new LiteralPart(Advance().Value) };
				while (IsWordToken() && !Peek().HasLeadingSpace) braceParts.AddRange(ParseWordParts());
				args.Add(new Word(braceParts));
				}
			else
				break;
			}

		// trailing redirects
		while (true)
			{
			var r = TryParseRedirect();
			if (r is null) break;
			redirects.Add(r);
			}

		if (name is null && assignments.Count == 0 && redirects.Count == 0)
			return null;

		return new SimpleCommand(assignments, name, args, redirects) { Line = line, Span = SpanFrom(firstTok, _heredocEnd) };
		}

	/// <summary>The text from <paramref name="first"/> to the last token consumed (or to the end of a
	/// here-document body read for this command, which bash's `$BASH_COMMAND` includes).</summary>
	private SourceSpan SpanFrom(Token first, int atLeastEnd = -1)
		{
		if (_src is null || first.Start < 0 || _pos == 0) return default;
		int end = Math.Max(_tokens[_pos - 1].End, atLeastEnd);
		return end >= first.Start ? new SourceSpan(_src, first.Start, end) : default;
		}

	private int _heredocEnd = -1;   // the end of a here-document body bound to the command being parsed

	private static bool IsDeclarationBuiltin(string name) =>
		name is "declare" or "typeset" or "local" or "export" or "readonly";

	/// <summary>Consume "( … )" starting at the current LParen and return its source text
	/// including the parentheses (token values joined when no source is available).</summary>
	private string CollectParenText()
		{
		var open = Advance();   // (
		int depth = 1;
		var sb = new System.Text.StringBuilder("(");
		Token? last = null;
		while (!IsEof() && depth > 0)
			{
			var t = Peek();
			// `$(`, `<(`, `>(` open a parenthesis too (closed by a `)` token); until 2026-10-12 only `(` counted, so
			// `local p=($(cmd))` ended the literal at the substitution's `)`: a syntax error
			if (t.Type is TokenType.LParen or TokenType.DollarLParen or TokenType.LessLParen or TokenType.GreaterLParen) depth++;
			else if (t.Type == TokenType.DollarDollarLParen) depth += 2;
			else if (t.Type == TokenType.RParen) { depth--; if (depth == 0) { last = Advance(); break; } }
			var tok = Advance();
			if (tok.HasLeadingSpace && sb.Length > 1) sb.Append(' ');
			sb.Append(tok.Type switch
				{
				TokenType.SingleQuoted => "'" + tok.Value + "'",
				TokenType.DoubleQuoted => "\"" + tok.Value + "\"",
				TokenType.DollarLBrace => "${" + tok.Value + "}",
				TokenType.DollarSingleQuote => "$'" + tok.Value.Replace("'", "\\'") + "'",
				TokenType.DollarLParen => "$(",
				TokenType.DollarDollarLParen => "$((",
				TokenType.LessLParen => "<(",
				TokenType.GreaterLParen => ">(",
				_ => tok.Value,
				});
			}
		if (last is null)   // as for `x=(a b` at the end of the text (bash: status 1, one line)
			throw new ParseException("unterminated array literal", open.Line, open.Column) { Incomplete = true, Missing = ')', NoEofLine = true, Status = 1 };
		if (_src is not null)
			{
			int s = OffsetOf(open), e = OffsetOf(last);
			if (s >= 0 && e >= s) return _src[s..(e + 1)];
			}
		sb.Append(')');
		return sb.ToString();
		}

	/// <summary>NAME=value or NAME+=value (the latter reported with a trailing '+' on the name).</summary>
	private static bool IsAssignment(Word word, out string? name, out Word? value)
		{
		name = null; value = null;
		if (word.Parts.Count == 0) return false;

		// The first part must be a literal that contains '='
		if (word.Parts[0] is not LiteralPart lit) return false;
		int eq = lit.Value.IndexOf('=');
		if (eq <= 0) return false;

		// LHS must be a valid identifier (optionally followed by '+' for append)
		var lhs = lit.Value[..eq];
		bool append = lhs.EndsWith('+');
		var ident = append ? lhs[..^1] : lhs;
		if (!IsValidIdentifier(ident)) return false;

		name = lhs;
		var rhs = lit.Value[(eq + 1)..];
		var rhsParts = new List<WordPart>();
		if (rhs.Length > 0)
			rhsParts.Add(new LiteralPart(rhs));
		rhsParts.AddRange(word.Parts.Skip(1));
		value = WithAssignmentTildes(new Word(rhsParts));
		return true;
		}

	/// <summary>Tilde expansion in an assignment value, as bash does it: a `~` at the start of the
	/// value or right after an unquoted `:`, followed by `/`, `:` or the end of the value, is the home
	/// directory (`x=~/y`, `PATH=$PATH:~/bin`, `x=~`). Quoted or escaped tildes are not literal
	/// parts, so they stay. `~user` is not supported, as elsewhere. Until 2026-10-08 no assignment
	/// value expanded a tilde at all.</summary>
	private static Word WithAssignmentTildes(Word value)
		{
		var parts = value.Parts;
		if (!parts.Any(p => p is LiteralPart l && l.Value.Contains('~'))) return value;
		var result = new List<WordPart>();
		for (int pi = 0; pi < parts.Count; pi++)
			{
			if (parts[pi] is not LiteralPart lit || !lit.Value.Contains('~')) { result.Add(parts[pi]); continue; }
			var s = lit.Value;
			var sb = new System.Text.StringBuilder();
			for (int i = 0; i < s.Length; i++)
				{
				bool atPrefixStart = i > 0 ? s[i - 1] == ':'
				                   : pi == 0 || parts[pi - 1] is LiteralPart { Value: [.., ':'] };
				bool endsValue = i + 1 == s.Length && pi == parts.Count - 1;
				if (s[i] == '~' && atPrefixStart && (endsValue || (i + 1 < s.Length && s[i + 1] is '/' or ':')))
					{
					if (sb.Length > 0) { result.Add(new LiteralPart(sb.ToString())); sb.Clear(); }
					result.Add(new TildePart(""));
					continue;
					}
				sb.Append(s[i]);
				}
			if (sb.Length > 0) result.Add(new LiteralPart(sb.ToString()));
			}
		return new Word(result);
		}

	private static bool IsValidIdentifier(string s) =>
		s.Length > 0 && (char.IsLetter(s[0]) || s[0] == '_')
		&& s.All(c => char.IsLetterOrDigit(c) || c == '_');

	/// <summary>Detects arr=( or arr+=( — the word is just "arr=" / "arr+=" and the next token is (.</summary>
	private bool IsArrayCompoundAssign(Word word, out string? name, out bool append)
		{
		name = null; append = false;
		if (word.Parts is not [LiteralPart lit]) return false;
		if (!lit.Value.EndsWith('=')) return false;
		var lhs = lit.Value[..^1];
		if (lhs.EndsWith('+')) { append = true; lhs = lhs[..^1]; }
		if (!IsValidIdentifier(lhs)) return false;
		// Only treat as array compound assign if the very next token is (
		if (Peek().Type != TokenType.LParen || Peek().HasLeadingSpace) return false;
		name = lhs;
		return true;
		}

	/// <summary>Detects arr[expr]=val.</summary>
	private bool IsArrayElementAssign(Word word, out string? arrayName, out Word? index, out Word? value)
		{
		arrayName = null; index = null; value = null;
		if (word.Parts.Count == 0) return false;
		if (word.Parts[0] is not LiteralPart lit) return false;

		// Find arr[
		int bracket = lit.Value.IndexOf('[');
		if (bracket <= 0) return false;
		var lhs = lit.Value[..bracket];
		if (!IsValidIdentifier(lhs)) return false;

		// the index may span word parts: arr[0]=, arr[$k]=, arr["$k"]=, arr[$i+1]=
		if (!word.TrySplitIndexedAssignment(bracket + 1, out var idx, out var val)) return false;
		arrayName = lhs;
		index = idx;
		value = WithAssignmentTildes(val);
		return true;
		}

	// ── compound command bodies ───────────────────────────────────────────────

	/// <summary>A function's body: bash's grammar takes only a compound command ({ }, ( ), if, while,
	/// until, for, select, case, [[ ]], (( ))), so `f() echo x` is a syntax error near `echo`. Any
	/// command was accepted until 2026-10-10.</summary>
	private Node ParseFunctionBody()
		{
		if (ParseCompoundCommandBody() is { } braced) return braced;
		var t = Peek();
		bool compound = t.Type == TokenType.ArithCommand
		    || (t.Type == TokenType.Word && t.Value is "if" or "while" or "until" or "for" or "select" or "case" or "[[");
		if (!compound) throw Error("a function's body must be a compound command");
		return ParseCommand() ?? throw Error("a function's body must be a compound command");
		}

	private Node? ParseCompoundCommandBody()
		{
		var t = Peek();
		if (t.Type == TokenType.LBrace)  return ParseBraceGroup();
		if (t.Type == TokenType.LParen)  return ParseSubshell();
		return null;
		}

	private BraceGroup ParseBraceGroup()
		{
		Expect(TokenType.LBrace);
		SkipNewlines();
		var body = ParseList() ?? throw EmptyBody();
		SkipSeparators();
		Expect(TokenType.RBrace);
		var redirects = ParseRedirects();
		return new BraceGroup(body, redirects);
		}

	private Subshell ParseSubshell()
		{
		Expect(TokenType.LParen);
		SkipNewlines();
		var body = ParseList() ?? throw EmptyBody();
		SkipSeparators();
		Expect(TokenType.RParen);
		var redirects = ParseRedirects();
		return new Subshell(body, redirects);
		}

	// ── if ────────────────────────────────────────────────────────────────────

	private IfCommand ParseIf()
		{
		ExpectWord("if");
		SkipNewlines();
		var condition = ParseList() ?? throw Error("Expected condition after 'if'");
		SkipSeparators();
		ExpectWord("then");
		SkipNewlines();
		var then = ParseList() ?? throw EmptyBody();

		var elifs = new List<(Node, Node)>();
		Node? elseBody = null;

		while (true)
			{
			SkipSeparators();
			if (PeekWord("elif"))
				{
				Advance();
				SkipNewlines();
				var elifCond = ParseList() ?? throw Error("Expected condition after 'elif'");
				SkipSeparators();
				ExpectWord("then");
				SkipNewlines();
				var elifBody = ParseList() ?? throw EmptyBody();
				elifs.Add((elifCond, elifBody));
				}
			else if (PeekWord("else"))
				{
				Advance();
				SkipNewlines();
				elseBody = ParseList() ?? throw EmptyBody();
				break;
				}
			else
				break;
			}

		SkipSeparators();
		ExpectWord("fi");
		var redirects = ParseRedirects();
		return new IfCommand(condition, then, elifs, elseBody, redirects);
		}

	// ── while / until ─────────────────────────────────────────────────────────

	private WhileCommand ParseWhile(bool until)
		{
		ExpectWord(until ? "until" : "while");
		SkipNewlines();
		var condition = ParseList() ?? throw Error($"Expected condition after '{(until ? "until" : "while")}'");
		SkipSeparators();
		ExpectWord("do");
		SkipNewlines();
		var body = ParseList() ?? throw EmptyBody();
		SkipSeparators();
		ExpectWord("done");
		var redirects = ParseRedirects();
		return new WhileCommand(condition, body, until, redirects);
		}

	// ── for ───────────────────────────────────────────────────────────────────

	private Node ParseFor()
		{
		var forTok = Advance();   // for
		if (Peek().Type == TokenType.ArithCommand)
			{
			// for (( init; cond; step )); do body; done
			var spec = Advance().Value;
			var raw = SplitArithFor(spec, trim: false);
			var parts = raw.Select(p => p.Trim()).ToArray();
			SkipSeparators();
			ExpectWord("do");
			SkipNewlines();
			var abody = ParseList() ?? throw EmptyBody();
			SkipSeparators();
			ExpectWord("done");
			var aredirects = ParseRedirects();
			// `$BASH_COMMAND` for each part is bash's `((part))`, the part with its front trimmed
			return new ArithForCommand(parts[0], parts[1], parts[2], abody, aredirects)
				{ Line = forTok.Line, Heads = [.. raw.Select(p => $"(({p.TrimStart()}))")] };
			}
		var varName = ExpectWordToken().Value;
		var headSpan = SpanFrom(forTok);
		SkipNewlines();

		var words = new List<Word>();
		if (PeekWord("in"))
			{
			Advance(); // consume 'in'
			while (IsWordToken())
				words.Add(ParseWord()!);
			headSpan = SpanFrom(forTok);   // `for i in a b`: bash's `$BASH_COMMAND` for each turn
			SkipSeparators();
			}
		else
			{
			// for var; do — iterates over "$@"
			SkipSeparators();
			}

		ExpectWord("do");
		SkipNewlines();
		var body = ParseList() ?? throw EmptyBody();
		SkipSeparators();
		ExpectWord("done");
		var redirects = ParseRedirects();
		return new ForCommand(varName, words, body, redirects) { Line = forTok.Line, Span = headSpan };
		}

	/// <summary>Split "init; cond; step" at top-level semicolons (parens/quotes respected).</summary>
	private static string[] SplitArithFor(string spec, bool trim = true)
		{
		var parts = new List<string>();
		var sb = new System.Text.StringBuilder();
		int depth = 0;
		for (int i = 0; i < spec.Length; i++)
			{
			char c = spec[i];
			if (c == '(') depth++;
			else if (c == ')') depth--;
			if (c == ';' && depth == 0) { parts.Add(sb.ToString()); sb.Clear(); continue; }
			sb.Append(c);
			}
		parts.Add(sb.ToString());
		while (parts.Count < 3) parts.Add("");
		return trim ? [parts[0].Trim(), parts[1].Trim(), parts[2].Trim()] : [parts[0], parts[1], parts[2]];
		}

	// ── case ──────────────────────────────────────────────────────────────────

	private CaseCommand ParseCase()
		{
		int caseLine = Peek().Line;
		ExpectWord("case");
		var subjectTok = Peek();
		var subject = ParseWord() ?? throw Error("Expected word after 'case'");
		var subjectSpan = SpanFrom(subjectTok);   // bash's `$BASH_COMMAND`: `case WORD in `
		SkipNewlines();
		ExpectWord("in");
		SkipNewlines();

		var items = new List<CaseItem>();
		while (!PeekWord("esac") && !IsEof())
			{
			items.Add(ParseCaseItem());
			SkipNewlines();
			}
		ExpectWord("esac");
		var redirects = ParseRedirects();
		return new CaseCommand(subject, items, redirects) { Line = caseLine, Span = subjectSpan };
		}

	private CaseItem ParseCaseItem()
		{
		// optional leading (
		if (Peek().Type == TokenType.LParen) Advance();

		var patterns = new List<Word>();
		patterns.Add(ParseWord() ?? throw Error("Expected pattern in case item"));
		while (Peek().Type == TokenType.Pipe)
			{
			Advance();
			patterns.Add(ParseWord() ?? throw Error("Expected pattern after '|' in case item"));
			}
		Expect(TokenType.RParen);
		SkipNewlines();

		Node? body = null;
		if (!PeekWord("esac") && !IsCaseTerminator(Peek().Type))
			body = ParseList();

		SkipSeparators();
		var term = CaseTerminator.Break;
		if (IsCaseTerminator(Peek().Type))
			{
			term = Advance().Type switch
				{
				TokenType.SemicolonAmpersand          => CaseTerminator.FallThrough,
				TokenType.SemicolonSemicolonAmpersand => CaseTerminator.TestNext,
				_                                     => CaseTerminator.Break,
				};
			}

		return new CaseItem(patterns, body) { Terminator = term };
		}

	private static bool IsCaseTerminator(TokenType t) =>
		t is TokenType.SemicolonSemicolon or TokenType.SemicolonAmpersand or TokenType.SemicolonSemicolonAmpersand;

	// ── [[ ]] conditional expression ──────────────────────────────────────────

	private ConditionalExpression ParseConditionalExpression()
		{
		_condLine = Peek().Line;   // where bash says it was "looking for `]]'" from
		var openTok = Peek();
		ExpectWord("[[");
		var expr = ParseCondExpr();
		SkipNewlines();   // bash reads on for the `]]` (at the end of the text: "looking for `]]'")
		if (!PeekWord("]]")) throw CondError($"syntax error in conditional expression: unexpected token `{SyntaxMessage.Shown(Peek())}'");
		Advance();
		return new ConditionalExpression(expr) { Line = openTok.Line, Span = SpanFrom(openTok) };
		}

	/// <summary>A `[[ ]]` syntax error, reported as bash's cond_error does (measured against bash 5.1,
	/// 2026-10-10): <paramref name="lead"/>, then "syntax error near `X'" where X is the token found
	/// (the operator before it, when that is a newline: `[[ a ==⏎`), then the source line; at the end of
	/// the text, "unexpected EOF while looking for `]]'". It said "Expected right operand …" before.</summary>
	private ParseException CondError(string? lead, Token? before = null)
		{
		var t = Peek();
		if (t.Type == TokenType.Eof)
			return new ParseException("unexpected end of file in [[ ]]", _condLine, 1) { Incomplete = true, LookingFor = "]]", At = t };
		var near = t.Type == TokenType.Newline && before is not null ? before : t;
		return new ParseException(lead ?? "syntax error in [[ ]]", near.Line, near.Column) { Cond = true, Lead = lead, At = near };
		}

	/// <summary>An operand inside `[[ ]]`: a word, never the closing `]]` or an operator.</summary>
	private Word? CondOperand() => CondOperandAbsent() ? null : ParseWord();

	private bool CondOperandAbsent()
		{
		var t = Peek();
		return t.Type is TokenType.Eof or TokenType.Newline or TokenType.AmpersandAmpersand or TokenType.PipePipe or TokenType.RParen
		    || (t.Type == TokenType.Word && t.Value == "]]");
		}

	/// <summary>bash's unary test operators (test.c, test_unop).</summary>
	private static bool IsUnaryCondOp(string w) => w.Length == 2 && w[0] == '-' && "abcdefghknoprstuvwxzGLNORS".Contains(w[1]);

	private string Found() => SyntaxMessage.Shown(Peek());

	private int _condLine;   // the line of the `[[` being parsed

	private CondExpr ParseCondExpr()
		{
		SkipNewlines();
		var left = ParseCondAnd();
		while (Peek().Type == TokenType.PipePipe || PeekWord("||"))
			{
			Advance();
			SkipNewlines();
			left = new CondOr(left, ParseCondAnd());
			}
		return left;
		}

	private CondExpr ParseCondAnd()
		{
		var left = ParseCondNot();
		while (Peek().Type == TokenType.AmpersandAmpersand || PeekWord("&&"))
			{
			Advance();
			SkipNewlines();
			left = new CondAnd(left, ParseCondNot());
			}
		return left;
		}

	private CondExpr ParseCondNot()
		{
		if (PeekWord("!"))
			{
			Advance();
			return new CondNot(ParseCondNot());
			}
		return ParseCondPrimary();
		}

	private CondExpr ParseCondPrimary()
		{
		// ( expr ) grouping
		if (Peek().Type == TokenType.LParen)
			{
			Advance();
			var inner = ParseCondExpr();
			SkipNewlines();
			if (Peek().Type != TokenType.RParen) throw CondError($"unexpected token `{Found()}', expected `)'");
			Advance();
			return new CondGroup(inner);   // kept for the printer: bash prints the parentheses written
			}

		// unary: one of bash's test operators, whose operand must be a word (`[[ -f ]]` is an error;
		// it read `-f` as a string until 2026-10-10)
		if (Peek().Type == TokenType.Word && IsUnaryCondOp(Peek().Value))
			{
			var opTok = Advance();
			var operand = CondOperand() ?? throw CondError($"unexpected argument `{Found()}' to conditional unary operator", opTok);
			return new CondUnary(opTok.Value, operand);
			}

		var left = CondOperand() ?? throw CondError(null);   // `[[ ]]`, `[[ ! ]]`, `[[ a && ]]`

		// binary: =, !=, ==, <, >, -eq, -ne, -lt, -le, -gt, -ge, -nt, -ot, -ef, =~
		if (IsWordToken() && IsBinaryCondOp(Peek().Value))
			{
			var opTok = Advance();
			var op = opTok.Value;
			if (op == "=~")
				{
				if (CondOperandAbsent()) throw CondError($"unexpected argument `{Found()}' to conditional binary operator", opTok);
				return new CondBinary(op, left, ParseRegexOperand());
				}
			var right = CondOperand() ?? throw CondError($"unexpected argument `{Found()}' to conditional binary operator", opTok);
			return new CondBinary(op, left, right);
			}
		// `<` and `>` are lexed as redirect operators: accept them as string comparison here
		if (Peek().Type is TokenType.Less or TokenType.Greater)
			{
			var opTok = Advance();
			var op = opTok.Type == TokenType.Less ? "<" : ">";
			var right = CondOperand() ?? throw CondError($"unexpected argument `{Found()}' to conditional binary operator", opTok);
			return new CondBinary(op, left, right);
			}

		// a word where an operator belongs: `[[ a b ]]`
		if (IsWordToken() && !PeekWord("]]")) throw CondError("conditional binary operator expected");

		return new CondWord(left);
		}

	/// <summary>The right side of `=~`: an unquoted regex may contain `(`, `)`, `|`, `&`
	/// which the lexer treated as operators, so gather every token up to `]]`, `&&`, `||`
	/// or an unbalanced `)`. Quoted parts are kept as quoted (literal) parts, matching
	/// bash's rule that quoted portions of the pattern match literally.</summary>
	private Word ParseRegexOperand()
		{
		var parts = new List<WordPart>();
		int depth = 0;
		bool first = true;
		var firstTok = Peek();
		while (!IsEof())
			{
			var t = Peek();
			if (t.Type == TokenType.Word && t.Value == "]]") break;
			if (t.Type is TokenType.AmpersandAmpersand or TokenType.PipePipe && depth == 0) break;
			if (t.Type == TokenType.RParen && depth == 0) break;
			if (t.Type == TokenType.Newline) break;
			if (!first && t.HasLeadingSpace) parts.Add(new LiteralPart(" "));
			first = false;
			switch (t.Type)
				{
				case TokenType.LParen: depth++; Advance(); parts.Add(new LiteralPart("(")); break;
				case TokenType.RParen: depth--; Advance(); parts.Add(new LiteralPart(")")); break;
				case TokenType.Pipe: Advance(); parts.Add(new LiteralPart("|")); break;
				case TokenType.SingleQuoted: Advance(); parts.Add(new SingleQuotedPart(t.Value)); break;
				case TokenType.DoubleQuoted: Advance(); parts.Add(new DoubleQuotedPart(ParseDoubleQuotedInterior(t.Value))); break;
				default:
					if (IsWordToken()) parts.AddRange(ParseWordParts());
					else { Advance(); parts.Add(new LiteralPart(t.Value)); }
					break;
				}
			}
		if (parts.Count == 0) throw Error("Expected right operand for '=~'");
		return new Word(parts) { RawSpan = SpanFrom(firstTok) };   // as written, for the printer
		}

	private static bool IsBinaryCondOp(string s) =>
		s is "=" or "==" or "!=" or "<" or ">" or "=~"
		    or "-eq" or "-ne" or "-lt" or "-le" or "-gt" or "-ge" or "-nt" or "-ot" or "-ef";

	// ── redirects ─────────────────────────────────────────────────────────────

	private List<Redirect> ParseRedirects()
		{
		var list = new List<Redirect>();
		while (true)
			{
			var r = TryParseRedirect();
			if (r is null) break;
			list.Add(r);
			}
		return list;
		}

	private Redirect? TryParseRedirect()
		{
		int? fd = null;

		// optional fd digit immediately (no space) before a redirect operator
		if (Peek().Type == TokenType.Digit && IsFdDigit())
			fd = int.Parse(Advance().Value);

		if (!IsRedirectOp(Peek().Type))
			return null;

		var op = Advance();

		if (op.Type is TokenType.LessLess or TokenType.LessLessDash)
			{
			// The delimiter word follows on this line; the lexer emitted the body as a
			// HeredocBody token after the line's Newline. Bind that body here and take it
			// out of the stream. A quoted delimiter suppresses expansion in the body.
			var delim = ParseWord() ?? throw Error("Expected here-document delimiter");
			bool quoted = delim.Parts.Any(p => p is SingleQuotedPart or DoubleQuotedPart or AnsiCQuotedPart);
			// the body comes after this line's newline: lexing on demand, read on until it is there
			int bodyIdx;
			while ((bodyIdx = _tokens.FindIndex(_pos, t => t.Type == TokenType.HeredocBody)) < 0 && _lexer is { Done: false })
				_lexer.Step(_tokens);
			string body = "";
			if (bodyIdx >= 0) { body = _tokens[bodyIdx].Value; _heredocEnd = Math.Max(_heredocEnd, _tokens[bodyIdx].End); _tokens.RemoveAt(bodyIdx); }
			Word target = quoted
				? new Word([new SingleQuotedPart(body)])
				: new Word([new DoubleQuotedPart(ParseDoubleQuotedInterior(body, heredoc: true))]);
			return new Redirect(fd, op.Type == TokenType.LessLess ? RedirectKind.Heredoc : RedirectKind.HeredocStrip, target);
			}

		var word = ParseWord() ?? throw Error("Expected target after redirect operator");

		var kind = op.Type switch
			{
			TokenType.Less           => RedirectKind.Input,
			TokenType.Greater        => RedirectKind.Output,
			TokenType.GreaterGreater => RedirectKind.Append,
			TokenType.GreaterPipe    => RedirectKind.Clobber,
			TokenType.LessAmpersand  => RedirectKind.InputDup,
			TokenType.GreaterAmpersand => RedirectKind.OutputDup,
			TokenType.LessGreater    => RedirectKind.ReadWrite,
			TokenType.LessLessLess   => RedirectKind.HereString,
			TokenType.AmpersandGreater => RedirectKind.OutputBoth,
			TokenType.AmpersandGreaterGreater => RedirectKind.AppendBoth,
			_ => throw Error($"Unknown redirect operator '{op.Value}'")
			};

		return new Redirect(fd, kind, word);
		}

	/// <summary>True when the current Digit token is an fd number: a redirect operator
	/// follows it with no intervening space (`2>` yes; `2 >` no; `echo 2 >f` no).</summary>
	private bool IsFdDigit()
		{
		var next = PeekAhead(1);
		return IsRedirectOp(next.Type) && !next.HasLeadingSpace;
		}

	private static bool IsRedirectOp(TokenType t) =>
		t is TokenType.Less or TokenType.Greater or TokenType.GreaterGreater
		    or TokenType.GreaterPipe or TokenType.LessAmpersand or TokenType.GreaterAmpersand
		    or TokenType.LessGreater or TokenType.LessLess or TokenType.LessLessDash
		    or TokenType.LessLessLess or TokenType.AmpersandGreater or TokenType.AmpersandGreaterGreater;

	// ── word / word-part parsing ──────────────────────────────────────────────

	private bool IsWordToken()
		{
		var t = Peek();
		// A Digit token is only a word constituent when it is NOT an fd number (2>).
		if (t.Type == TokenType.Digit && IsFdDigit())
			return false;
		return t.Type is TokenType.Word or TokenType.Digit
		               or TokenType.SingleQuoted or TokenType.DoubleQuoted
		               or TokenType.Dollar or TokenType.DollarLParen
		               or TokenType.DollarDollarLParen or TokenType.DollarLBrace
		               or TokenType.DollarSingleQuote
		               or TokenType.Backtick
		               or TokenType.LessLParen or TokenType.GreaterLParen;
		}

	private Word? ParseWord()
		{
		if (!IsWordToken()) return null;
		var parts = new List<WordPart>();
		var firstTok = Peek();

		// Consume the first token unconditionally, then only continue if the
		// next token is immediately adjacent (no leading space).
		parts.AddRange(ParseWordParts());

		while (IsWordToken() && !Peek().HasLeadingSpace)
			parts.AddRange(ParseWordParts());

		// the word as written, for the command printer (`declare -f`): bash prints words verbatim
		return new Word(parts) { RawSpan = SpanFrom(firstTok) };
		}

	private IEnumerable<WordPart> ParseWordParts()
		{
		var t = Peek();
		switch (t.Type)
			{
			case TokenType.Word:
			case TokenType.Digit:
				Advance();
				// a backslash-escaped `* ? [ { } , ~` (marked by the lexer) is a QUOTED character
				if (t.Value.IndexOf(Lexer.Lexer.EscapeMark) >= 0)
					{
					var lit = new System.Text.StringBuilder();
					for (int i = 0; i < t.Value.Length; i++)
						{
						if (t.Value[i] == Lexer.Lexer.EscapeMark && i + 1 < t.Value.Length)
							{
							if (lit.Length > 0) { yield return new LiteralPart(lit.ToString()); lit.Clear(); }
							yield return new SingleQuotedPart(t.Value[++i].ToString());
							}
						else lit.Append(t.Value[i]);
						}
					if (lit.Length > 0) yield return new LiteralPart(lit.ToString());
					break;
					}
				// Tilde expansion: leading ~ or ~/
				if (t.Value == "~" || t.Value.StartsWith("~/") || t.Value.StartsWith("~\\"))
					{
					var suffix = t.Value.Length > 1 ? t.Value[1..] : "";
					yield return new TildePart(suffix);
					}
				else
					yield return new LiteralPart(t.Value);
				break;

			case TokenType.SingleQuoted:
				Advance();
				yield return new SingleQuotedPart(t.Value);
				break;

			case TokenType.DollarSingleQuote:
				Advance();
				yield return new AnsiCQuotedPart(t.Value);
				break;

			case TokenType.DoubleQuoted:
				Advance();
				yield return new DoubleQuotedPart(ParseDoubleQuotedInterior(t.Value));
				break;

			case TokenType.Dollar:
				// The lexer already consumed every `$name`/`$1`/`$?` form; a Dollar token is a `$`
				// that introduces nothing and is literal in bash (`a$`, `^[0-9]+$`). Until
				// 2026-09-08 this glued it to the NEXT word token even across a space, so
				// `[[ x =~ ^[0-9]+$ ]]` swallowed the `]]` and `[[ a$ == a$ ]]` read `==` as a
				// variable. The one non-literal case is `$"…"` (locale quoting == the string).
				Advance();
				if (Peek().Type == TokenType.DoubleQuoted && !Peek().HasLeadingSpace)
					break;   // `$"text"` — the $ contributes nothing; the string follows as its own part
				yield return new LiteralPart("$");
				break;

			case TokenType.DollarLBrace:
				Advance();
				// The lexer has already consumed the entire ${...} body and closing }.
				// t.Value is the raw interior string — wrap it directly.
				yield return new BraceExpansionPart(t.Value);
				break;

			case TokenType.DollarLParen:
				Advance();
				yield return ParseCommandSubstitution();
				break;

			case TokenType.LessLParen:
				{
				// <(cmd): same body as $(cmd); the expander runs it into a temp file
				Advance();
				var inner = (CommandSubstitutionPart)ParseCommandSubstitution();
				yield return new ProcessSubstitutionPart(inner.Command);
				break;
				}

			case TokenType.GreaterLParen:
				throw new ParseException("output process substitution >( ) is not supported: write to a temp file and read it after the command (DECISIONS 2026-09-04 #6)", Peek().Line, Peek().Column) { Unsupported = true };

			case TokenType.DollarDollarLParen:
				Advance();
				yield return ParseArithmeticExpansion();
				break;

			case TokenType.Backtick:
				Advance();
				yield return ParseBacktickSubstitution();
				break;

			default:
				yield break;
			}
		}

	/// <summary>
	/// Re-lex the interior of a double-quoted string (or an unquoted heredoc body) to
	/// find expansions. The lexer stored the raw interior (without the outer quotes,
	/// backslashes intact). Escape rules: \$ \` \\ and \newline are escapes (plus \" in
	/// double quotes); any other backslash is literal.
	/// </summary>
	/// <summary>The parts of text read as the inside of a double-quoted string (`$`, `` ` ``, `\` work;
	/// `'` and `~` are literal): a `${x:-word}` word inside "…" (WordExpander).</summary>
	public static List<WordPart> ParseDoubleQuotedText(string raw) => ParseDoubleQuotedInterior(raw);

	private static List<WordPart> ParseDoubleQuotedInterior(string raw, bool heredoc = false)
		{
		var parts = new List<WordPart>();
		var sb = new System.Text.StringBuilder();
		int i = 0;

		void FlushLiteral()
			{
			if (sb.Length > 0) { parts.Add(new LiteralPart(sb.ToString())); sb.Clear(); }
			}

		while (i < raw.Length)
			{
			if (raw[i] == '\\' && i + 1 < raw.Length)
				{
				char n = raw[i + 1];
				if (n is '$' or '`' or '\\' || (n == '"' && !heredoc)) { sb.Append(n); i += 2; continue; }
				if (n == '\n') { i += 2; continue; }
				sb.Append('\\'); i++; continue;
				}
			if (raw[i] == '$' && i + 1 < raw.Length)
				{
				FlushLiteral();
				int dollar = i;
				i++; // skip $
				if (raw[i] is '(' or '{')
					{
					// where the construct ends is Lexer.ConstructEnd's rule (quotes, escapes, nesting);
					// an unclosed one runs to the end of the text, as before
					int end = Lexer.Lexer.ConstructEnd(raw, dollar);
					if (end < 0) end = raw.Length + 1;
					if (raw[i] == '{')
						parts.Add(new BraceExpansionPart(raw[(i + 1)..Math.Min(end - 1, raw.Length)]));
					else if (i + 1 < raw.Length && raw[i + 1] == '(')
						parts.Add(new ArithmeticExpansionPart(raw[(i + 2)..Math.Max(i + 2, Math.Min(end - 2, raw.Length))]));
					else
						{
						var inner = raw[(i + 1)..Math.Min(end - 1, raw.Length)];
						try { parts.Add(new CommandSubstitutionPart(new Parser(new Lexer.Lexer(inner).Tokenize(), inner).Parse())); }
						catch (Exception e) when (e is ParseException or Lexer.LexException)
							{
							// bash parses from the body to the end of the string when it runs (phase 3)
							parts.Add(DeferredSubstitution(raw[(i + 1)..] + (heredoc ? "" : "\""), backquoted: false, e));
							}
						}
					i = Math.Min(end, raw.Length);
					}
				else if ("@*#?$!-".IndexOf(raw[i]) >= 0)
					{
					parts.Add(new VarExpansionPart(raw[i].ToString())); i++;   // $@ $* $# $? $$ $! $-
					}
				else if (char.IsDigit(raw[i]))
					{
					parts.Add(new VarExpansionPart(raw[i].ToString())); i++;   // single-digit positional
					}
				else
					{
					int start = i;
					while (i < raw.Length && (char.IsLetterOrDigit(raw[i]) || raw[i] == '_')) i++;
					if (i == start) sb.Append('$');                            // lone $ before a non-name
					else parts.Add(new VarExpansionPart(raw[start..i]));
					}
				}
			else if (raw[i] == '`')
				{
				FlushLiteral();
				int end = Lexer.Lexer.ConstructEnd(raw, i);
				if (end < 0) end = raw.Length + 1;
				var inner = raw[(i + 1)..Math.Min(end - 1, raw.Length)];
				i = Math.Min(end, raw.Length);
				try { parts.Add(new CommandSubstitutionPart(new Parser(new Lexer.Lexer(inner).Tokenize(), inner).Parse())); }
				catch (Exception e) when (e is ParseException or Lexer.LexException)
					{
					parts.Add(DeferredSubstitution(inner, backquoted: true, e));   // run-time, in its own process (phase 3)
					}
				}
			else
				{
				sb.Append(raw[i++]);
				}
			}
		FlushLiteral();
		return parts;
		}

	private BraceExpansionPart ParseBraceExpansion()
		{
		// The lexer has no ${...} context, so:
		//   - RBrace token: } with leading space
		//   - Word("}")  : } with no leading space but as its own token
		//   - Word("foo}"): } embedded at end of a word token (e.g. "empty}")
		// We must handle all three cases, and track nesting depth.
		var sb = new System.Text.StringBuilder();
		int depth = 1;
		while (!IsEof() && depth > 0)
			{
			var t = Peek();

			// Dedicated } token (leading space)
			if (t.Type == TokenType.RBrace)
				{
				depth--;
				Advance();
				if (depth == 0) break;
				if (sb.Length > 0 && t.HasLeadingSpace) sb.Append(' ');
				sb.Append('}');
				continue;
				}

			// Dedicated { token
			if (t.Type == TokenType.LBrace)
				{
				depth++;
				Advance();
				if (sb.Length > 0 && t.HasLeadingSpace) sb.Append(' ');
				sb.Append('{');
				continue;
				}

			// Word token — may contain an embedded } (e.g. "empty}")
			if (t.Type == TokenType.Word || t.Type == TokenType.Digit)
				{
				Advance();
				if (sb.Length > 0 && t.HasLeadingSpace) sb.Append(' ');

				// Scan the word value for embedded closing }
				var val = t.Value;
				int closeIdx = FindEmbeddedClose(val);
				if (closeIdx >= 0)
					{
					// Append everything before the }
					sb.Append(val[..closeIdx]);
					// Handle anything after the } — push it back isn't possible,
					// so just ignore (bash doesn't allow text after } inside ${}).
					depth--;
					if (depth == 0) break;
					sb.Append('}');
					if (closeIdx + 1 < val.Length)
						sb.Append(val[(closeIdx + 1)..]);
					}
				else
					sb.Append(val);
				continue;
				}

			// Any other token — include its value
			if (sb.Length > 0 && t.HasLeadingSpace) sb.Append(' ');
			sb.Append(Advance().Value);
			}
		return new BraceExpansionPart(sb.ToString());
		}

	/// <summary>
	/// Find the index of a closing } in a word value that is not preceded by
	/// an opening { at the same depth. Returns -1 if not found.
	/// </summary>
	private static int FindEmbeddedClose(string val)
		{
		int depth = 0;
		for (int i = 0; i < val.Length; i++)
			{
			if (val[i] == '{') depth++;
			else if (val[i] == '}')
				{
				if (depth == 0) return i;
				depth--;
				}
			}
		return -1;
		}

	/// <summary>Parse a substitution's rebased body; a syntax error in it is moved back to the file's
	/// own line numbers, where bash reports it. The body's end is its closing `)` or backquote, so a
	/// body cut short there is bash's "unexpected token `)'" (`$(if true)`), not the end of file.</summary>
	private static Node ParseBody(Parser sub, int rebase, Token? closer)
		{
		try { return sub.Parse(); }
		catch (ParseException e) when (rebase > 0 || e.At is { Type: TokenType.Eof })
			{
			var at = e.At is { Type: TokenType.Eof } && closer is not null ? closer
			       : e.At is { Line: > 0 } t ? t with { Line = t.Line + rebase } : e.At;
			int line = at is { Line: > 0 } ? at.Line : e.Line > 0 ? e.Line + rebase : e.Line;
			throw new ParseException(e.Reason, line, e.Col)
				{
				Incomplete = e.Incomplete && at is { Type: TokenType.Eof }, Missing = e.Missing,
				Unsupported = e.Unsupported, At = at,
				};
			}
		}

	/// <summary>A token of a substitution's body, its line counted from the substitution's first line.</summary>
	private static Token Rebased(Token t, int by) => by > 0 && t.Line > by ? t with { Line = t.Line - by } : t;

	private CommandSubstitutionPart ParseCommandSubstitution()
		{
		// Collect tokens until the matching ). Every opener that the lexer emits as a
		// distinct token counts: "(", "$(", and "$((" (two levels).
		var open = _tokens[_pos - 1];
		int rebase = open.Line - 1;   // the body counts its lines from the $( (Evaluator._lineBase)
		Token? closer = null;
		var inner = new List<Token>();
		int depth = 1;
		var cases = new Lexer.Lexer.CaseTracker();   // a `case` inside: its patterns' `)` are not ours (bash)
		while (!IsEof() && depth > 0)
			{
			var t = Peek();
			if (t.Type == TokenType.Word && !t.HasLeadingSpace && inner.Count > 0 && inner[^1].Type is not (TokenType.Newline or TokenType.Semicolon))
				cases.OtherWord();   // glued to the token before: part of that word
			else if (t.Type == TokenType.Word) cases.Word(t.Value, depth);
			else if (IsCaseTerminator(t.Type)) cases.EndOfBranch(depth);   // ;; ;& ;;&
			else if (t.Type is TokenType.Semicolon or TokenType.Newline or TokenType.Pipe or TokenType.PipeAmpersand
			         or TokenType.Ampersand or TokenType.AmpersandAmpersand or TokenType.PipePipe or TokenType.LParen
			         or TokenType.LBrace or TokenType.DollarLParen) cases.Separator();
			else if (t.Type is TokenType.SingleQuoted or TokenType.DoubleQuoted or TokenType.Dollar or TokenType.DollarLBrace
			         or TokenType.DollarSingleQuote or TokenType.Backtick or TokenType.Digit) cases.OtherWord();
			if (t.Type is TokenType.LParen or TokenType.RParen && cases.InPattern(depth))
				{
				if (t.Type == TokenType.RParen) cases.PatternClosed(depth);
				}
			else if (t.Type is TokenType.LParen or TokenType.DollarLParen or TokenType.LessLParen or TokenType.GreaterLParen) depth++;
			else if (t.Type == TokenType.DollarDollarLParen) depth += 2;
			else if (t.Type == TokenType.RParen) { depth--; if (depth == 0) { closer = Advance(); break; } }
			inner.Add(Rebased(Advance(), rebase));
			}
		// unclosed: bash reports the line of the `$(` ("looking for matching `)'"), then the end of file
		if (depth > 0) throw new ParseException("Unterminated command substitution", rebase + 1, 1) { Incomplete = true, Missing = ')' };
		inner.Add(Token.Eof(0, 0));
		var subParser = new Parser(inner, _src) { LineShift = LineShift + rebase };
		try { return new CommandSubstitutionPart(ParseBody(subParser, rebase, closer)); }
		catch (ParseException e)
			{
			// bash parses the body (with its `)`) when it runs: the error comes then (phase 3)
			var body = TextBetween(open, 2, closer) ?? string.Join(" ", inner.Where(t => t.Type != TokenType.Eof).Select(t => t.Value));
			return DeferredSubstitution(body + ")", backquoted: false, e);
			}
		}

	private ArithmeticExpansionPart ParseArithmeticExpansion()
		{
		// The interior is kept as SOURCE TEXT (sliced from _src when available), not as token
		// values glued together: `$(( $(wc -l < f) + 1 ))` must keep its spaces for the nested
		// substitution to run `wc -l < f` rather than `wc-l<f`. (Reported 2026-09-05: the wrong
		// value came back with a clean exit status.)
		var first = Peek();
		Token? close = null;
		var sb = new System.Text.StringBuilder();
		int depth = 0;   // open ( / $( / $(( inside the expression: `))` closes only at depth 0
		while (!IsEof())
			{
			var t = Peek();
			if (depth == 0 && t.Type == TokenType.RParen && PeekAhead(1).Type == TokenType.RParen)
				{
				close = Advance(); Advance(); // consume ))
				break;
				}
			// nesting: `$(( t + $(echo 3) ))` — the substitution's `)` followed by `)` is not the end
			if (t.Type is TokenType.LParen or TokenType.DollarLParen or TokenType.LessLParen or TokenType.GreaterLParen) depth++;
			else if (t.Type == TokenType.DollarDollarLParen) depth += 2;
			else if (t.Type == TokenType.RParen && depth > 0) depth--;
			var tok = Advance();
			if (tok.HasLeadingSpace && sb.Length > 0) sb.Append(' ');
			sb.Append(tok.Type switch
				{
				TokenType.SingleQuoted => "'" + tok.Value + "'",
				TokenType.DoubleQuoted => "\"" + tok.Value + "\"",
				TokenType.DollarLBrace => "${" + tok.Value + "}",
				TokenType.DollarLParen => "$(",
				TokenType.DollarDollarLParen => "$((",
				_ => tok.Value,
				});
			}
		if (_src is not null && close is not null)
			{
			int s = OffsetOf(first), e = OffsetOf(close);
			if (s >= 0 && e >= s) return new ArithmeticExpansionPart(_src[s..e]);
			}
		return new ArithmeticExpansionPart(sb.ToString());
		}

	private CommandSubstitutionPart ParseBacktickSubstitution()
		{
		// Collect tokens until next backtick
		var open = _tokens[_pos - 1];
		int rebase = open.Line - 1;   // as for $( )
		Token? closer = null;
		var inner = new List<Token>();
		while (!IsEof() && Peek().Type != TokenType.Backtick)
			inner.Add(Rebased(Advance(), rebase));
		if (Peek().Type == TokenType.Backtick) closer = Advance();
		else throw new ParseException("Unterminated backquote", rebase + 1, 1) { Incomplete = true, Missing = '`' };   // was accepted silently
		inner.Add(Token.Eof(0, 0));
		var subParser = new Parser(inner, _src) { LineShift = LineShift + rebase };
		try { return new CommandSubstitutionPart(ParseBody(subParser, rebase, closer)); }
		catch (ParseException e)
			{
			// bash parses a backquote body in the substitution's own process, when it runs (phase 3)
			var body = TextBetween(open, 1, closer) ?? string.Join(" ", inner.Where(t => t.Type != TokenType.Eof).Select(t => t.Value));
			return DeferredSubstitution(body, backquoted: true, e);
			}
		}

	// ── helpers ───────────────────────────────────────────────────────────────

	private Token Peek()
		{
		Fill(_pos);
		var t = _tokens[_pos];
		// the text ends in a lexing error here
		if (t.Type == TokenType.Eof && (_lexer?.DeferredError ?? DeferredError) is { } err) throw err;
		return t;
		}

	private Token PeekAhead(int offset)
		{
		int idx = _pos + offset;
		Fill(idx);
		return idx < _tokens.Count ? _tokens[idx] : Token.Eof(0, 0);
		}

	private Token Advance()
		{
		Fill(_pos);
		return _tokens[_pos++];
		}

	private bool IsEof() => Peek().Type == TokenType.Eof;

	private bool PeekWord(string value) =>
		Peek().Type == TokenType.Word && Peek().Value == value;

	private void SkipNewlines()
		{
		while (Peek().Type == TokenType.Newline) Advance();
		}

	private void SkipSeparators()
		{
		while (Peek().Type is TokenType.Newline or TokenType.Semicolon) Advance();
		}

	private Token Expect(TokenType type)
		{
		var t = Peek();
		if (t.Type != type)
			throw Error($"Expected {type} but got {t.Type} ('{t.Value}')");
		return Advance();
		}

	private void ExpectWord(string value)
		{
		var t = Peek();
		if (t.Type != TokenType.Word || t.Value != value)
			throw Error($"Expected '{value}' but got '{t.Value}'");
		Advance();
		}

	private Token ExpectWordToken()
		{
		var t = Peek();
		if (t.Type != TokenType.Word)
			throw Error($"Expected word but got {t.Type}");
		return Advance();
		}

	/// <summary>A body with no command (`then fi`, `do done`, `{ }`, `( )`): bash's grammar needs one,
	/// so the closing word is an unexpected token. They were accepted as empty until 2026-10-10.</summary>
	private ParseException EmptyBody() => Error("a compound command's body needs a command");

	private ParseException Error(string message)
		{
		var t = Peek();
		return new ParseException(message, t.Line, t.Column) { Incomplete = t.Type == TokenType.Eof, At = t };
		}
	}
