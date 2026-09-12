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

	// ── public entry point ────────────────────────────────────────────────────

	public Script Parse()
		{
		var nodes = new List<Node>();
		SkipNewlines();
		while (!IsEof())
			{
			int before = _pos;
			var node = ParseList();
			if (node is not null)
				nodes.Add(node);
			SkipNewlines();
			// A token nothing could consume (a stray ')' or '}') must be an error, never
			// a silent spin: without this the loop never advances.
			if (_pos == before && !IsEof())
				throw Error($"syntax error near unexpected token '{Peek().Value}'");
			}
		return new Script(nodes);
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
		if (t.Line < 1 || t.Line > _lineStarts.Length) return -1;
		int off = _lineStarts[t.Line - 1] + t.Column - 1;
		return off <= _src.Length ? off : -1;
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

	private Node? ParseList()
		{
		var items = new List<(Node, ListOperator?)>();

		var first = ParsePipeline();
		if (first is null)
			return null;

		ListOperator? op = TryConsumeListOp();
		items.Add((first, op));

		while (op is not null && !IsListTerminator())
			{
			SkipNewlines();
			if (IsListTerminator()) break;
			var next = ParsePipeline();
			if (next is null) break;
			op = TryConsumeListOp();
			items.Add((next, op));
			}

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
		bool negated = false;
		if (PeekWord("!"))
			{
			Advance();
			negated = true;
			}

		var first = ParseCommand();
		if (first is null)
			return null;

		if (!IsPipeToken())
			return negated ? new Pipeline([(first, false)], true) : first;

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

		return new Pipeline(commands, negated);
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
			var redirects = ParseRedirects();
			return new ArithmeticCommand(t.Value, redirects) { Line = t.Line };
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
			var body = ParseCompoundCommandBody() ?? ParseCommand()
				?? throw Error($"Expected compound command body for function '{nameToken.Value}'");
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
			var body = ParseCompoundCommandBody() ?? ParseCommand()
				?? throw Error($"Expected compound command body for function '{nameToken.Value}'");
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
		int line = Peek().Line;

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
					Expect(TokenType.LParen);
					var values = new List<Word>();
					SkipNewlines();
					while (Peek().Type != TokenType.RParen && Peek().Type != TokenType.Eof)
						{
						if (IsWordToken()) values.Add(ParseWord()!);
						else break;
						SkipNewlines();
						}
					Expect(TokenType.RParen);
					while (true) { var tr = TryParseRedirect(); if (tr is null) break; redirects.Add(tr); }
					return new ArrayCompoundAssign(acName!, values, redirects) { Append = acAppend, Line = line };
					}

				// arr[n]=val — indexed element assignment
				if (name is null && args.Count == 0 && IsArrayElementAssign(word, out var aeName, out var aeIdx, out var aeVal))
					{
					while (true) { var tr = TryParseRedirect(); if (tr is null) break; redirects.Add(tr); }
					return new ArrayElementAssign(aeName!, aeIdx!, aeVal!, redirects);
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
					args.Add(Word.Literal(wl.Value + CollectParenText()));
					continue;
					}

				if (name is null)
					name = word;
				else
					args.Add(word);
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

		return new SimpleCommand(assignments, name, args, redirects) { Line = line };
		}

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
			if (t.Type == TokenType.LParen) depth++;
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
		if (last is null) throw Error("Expected ')' to close array literal");
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
		value = new Word(rhsParts);
		return true;
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

		// The literal should contain ] and = after the bracket
		// e.g. "arr[0]=" or the index may span multiple word parts
		var rest = lit.Value[(bracket + 1)..];
		int close = rest.IndexOf(']');
		if (close < 0) return false;

		var indexStr = rest[..close];
		var afterBracket = rest[(close + 1)..];
		if (!afterBracket.StartsWith('=')) return false;
		var valStr = afterBracket[1..];

		arrayName = lhs;
		index = new Word([new LiteralPart(indexStr)]);

		// Build value word from remaining literal + rest of word parts
		var valParts = new List<WordPart>();
		if (valStr.Length > 0) valParts.Add(new LiteralPart(valStr));
		valParts.AddRange(word.Parts.Skip(1));
		value = new Word(valParts);
		return true;
		}

	// ── compound command bodies ───────────────────────────────────────────────

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
		var body = ParseList() ?? new Script([]);
		SkipSeparators();
		Expect(TokenType.RBrace);
		var redirects = ParseRedirects();
		return new BraceGroup(body, redirects);
		}

	private Subshell ParseSubshell()
		{
		Expect(TokenType.LParen);
		SkipNewlines();
		var body = ParseList() ?? new Script([]);
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
		var then = ParseList() ?? new Script([]);

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
				var elifBody = ParseList() ?? new Script([]);
				elifs.Add((elifCond, elifBody));
				}
			else if (PeekWord("else"))
				{
				Advance();
				SkipNewlines();
				elseBody = ParseList() ?? new Script([]);
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
		var body = ParseList() ?? new Script([]);
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
			var parts = SplitArithFor(spec);
			SkipSeparators();
			ExpectWord("do");
			SkipNewlines();
			var abody = ParseList() ?? new Script([]);
			SkipSeparators();
			ExpectWord("done");
			var aredirects = ParseRedirects();
			return new ArithForCommand(parts[0], parts[1], parts[2], abody, aredirects) { Line = forTok.Line };
			}
		var varName = ExpectWordToken().Value;
		SkipNewlines();

		var words = new List<Word>();
		if (PeekWord("in"))
			{
			Advance(); // consume 'in'
			while (IsWordToken())
				words.Add(ParseWord()!);
			SkipSeparators();
			}
		else
			{
			// for var; do — iterates over "$@"
			SkipSeparators();
			}

		ExpectWord("do");
		SkipNewlines();
		var body = ParseList() ?? new Script([]);
		SkipSeparators();
		ExpectWord("done");
		var redirects = ParseRedirects();
		return new ForCommand(varName, words, body, redirects) { Line = forTok.Line };
		}

	/// <summary>Split "init; cond; step" at top-level semicolons (parens/quotes respected).</summary>
	private static string[] SplitArithFor(string spec)
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
		return [parts[0].Trim(), parts[1].Trim(), parts[2].Trim()];
		}

	// ── case ──────────────────────────────────────────────────────────────────

	private CaseCommand ParseCase()
		{
		ExpectWord("case");
		var subject = ParseWord() ?? throw Error("Expected word after 'case'");
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
		return new CaseCommand(subject, items, redirects);
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
		if (!PeekWord("esac") && Peek().Type != TokenType.SemicolonSemicolon)
			body = ParseList();

		SkipSeparators();
		if (Peek().Type == TokenType.SemicolonSemicolon)
			Advance();

		return new CaseItem(patterns, body);
		}

	// ── [[ ]] conditional expression ──────────────────────────────────────────

	private ConditionalExpression ParseConditionalExpression()
		{
		ExpectWord("[[");
		var expr = ParseCondExpr();
		ExpectWord("]]");
		return new ConditionalExpression(expr);
		}

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
			if (Peek().Type != TokenType.RParen) throw Error("Expected ')' in [[ ]]");
			Advance();
			return inner;
			}

		// unary: -f, -d, -z, -n, -v, -o etc.
		if (Peek().Type == TokenType.Word && Peek().Value.Length == 2 && Peek().Value[0] == '-'
		    && !(PeekAhead(1).Type == TokenType.Word && PeekAhead(1).Value == "]]"))
			{
			var op = Advance().Value;
			var operand = ParseWord() ?? throw Error($"Expected operand for unary operator '{op}'");
			return new CondUnary(op, operand);
			}

		var left = ParseWord() ?? throw Error("Expected expression in [[ ]]");

		// binary: =, !=, ==, <, >, -eq, -ne, -lt, -le, -gt, -ge, -nt, -ot, -ef, =~
		if (IsWordToken() && IsBinaryCondOp(Peek().Value))
			{
			var op = Advance().Value;
			if (op == "=~")
				return new CondBinary(op, left, ParseRegexOperand());
			var right = ParseWord() ?? throw Error($"Expected right operand for '{op}'");
			return new CondBinary(op, left, right);
			}
		// `<` and `>` are lexed as redirect operators: accept them as string comparison here
		if (Peek().Type is TokenType.Less or TokenType.Greater)
			{
			var op = Advance().Type == TokenType.Less ? "<" : ">";
			var right = ParseWord() ?? throw Error($"Expected right operand for '{op}'");
			return new CondBinary(op, left, right);
			}

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
		return new Word(parts);
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
			int bodyIdx = _tokens.FindIndex(_pos, t => t.Type == TokenType.HeredocBody);
			string body = "";
			if (bodyIdx >= 0) { body = _tokens[bodyIdx].Value; _tokens.RemoveAt(bodyIdx); }
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

		// Consume the first token unconditionally, then only continue if the
		// next token is immediately adjacent (no leading space).
		parts.AddRange(ParseWordParts());

		while (IsWordToken() && !Peek().HasLeadingSpace)
			parts.AddRange(ParseWordParts());

		return new Word(parts);
		}

	private IEnumerable<WordPart> ParseWordParts()
		{
		var t = Peek();
		switch (t.Type)
			{
			case TokenType.Word:
			case TokenType.Digit:
				Advance();
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
				throw Error("output process substitution >( ) is not supported: write to a temp file and read it after the command (DECISIONS 2026-09-04 #6)");

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
				i++; // skip $
				if (raw[i] == '(')
					{
					i++; // skip (
					bool arith = i < raw.Length && raw[i] == '(';
					if (arith) i++;
					int depth = arith ? 2 : 1;
					int start = i;
					while (i < raw.Length && depth > 0)
						{
						if (raw[i] == '(') depth++;
						else if (raw[i] == ')') depth--;
						if (depth > 0) i++;
						else i++;
						}
					var inner = raw[start..(i - (arith ? 2 : 1))];
					if (arith)
						parts.Add(new ArithmeticExpansionPart(inner));
					else
						{
						var innerLex = new Lexer.Lexer(inner);
						var innerParser = new Parser(innerLex.Tokenize(), inner);
						parts.Add(new CommandSubstitutionPart(innerParser.Parse()));
						}
					}
				else if (raw[i] == '{')
					{
					i++; // skip {
					int start = i;
					int depth = 1;
					while (i < raw.Length)
						{
						if (raw[i] == '{') depth++;
						else if (raw[i] == '}') { depth--; if (depth == 0) break; }
						i++;
						}
					parts.Add(new BraceExpansionPart(raw[start..i]));
					if (i < raw.Length) i++; // skip }
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
				i++;
				int start = i;
				while (i < raw.Length && raw[i] != '`') i++;
				var inner = raw[start..i];
				if (i < raw.Length) i++;
				var innerLex = new Lexer.Lexer(inner);
				var innerParser = new Parser(innerLex.Tokenize(), inner);
				parts.Add(new CommandSubstitutionPart(innerParser.Parse()));
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

	private CommandSubstitutionPart ParseCommandSubstitution()
		{
		// Collect tokens until the matching ). Every opener that the lexer emits as a
		// distinct token counts: "(", "$(", and "$((" (two levels).
		var inner = new List<Token>();
		int depth = 1;
		while (!IsEof() && depth > 0)
			{
			var t = Peek();
			if (t.Type is TokenType.LParen or TokenType.DollarLParen or TokenType.LessLParen or TokenType.GreaterLParen) depth++;
			else if (t.Type == TokenType.DollarDollarLParen) depth += 2;
			else if (t.Type == TokenType.RParen) { depth--; if (depth == 0) { Advance(); break; } }
			inner.Add(Advance());
			}
		if (depth > 0) throw Error("Unterminated command substitution");
		inner.Add(Token.Eof(0, 0));
		var subParser = new Parser(inner, _src);
		return new CommandSubstitutionPart(subParser.Parse());
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
		var inner = new List<Token>();
		while (!IsEof() && Peek().Type != TokenType.Backtick)
			inner.Add(Advance());
		if (Peek().Type == TokenType.Backtick) Advance();
		inner.Add(Token.Eof(0, 0));
		var subParser = new Parser(inner, _src);
		return new CommandSubstitutionPart(subParser.Parse());
		}

	// ── helpers ───────────────────────────────────────────────────────────────

	private Token Peek() => _tokens[_pos];

	private Token PeekAhead(int offset)
		{
		int idx = _pos + offset;
		return idx < _tokens.Count ? _tokens[idx] : Token.Eof(0, 0);
		}

	private Token Advance() => _tokens[_pos++];

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

	private ParseException Error(string message)
		{
		var t = Peek();
		return new ParseException(message, t.Line, t.Column) { Incomplete = t.Type == TokenType.Eof };
		}
	}
