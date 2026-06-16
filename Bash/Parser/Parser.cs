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
	private int _pos;

	public Parser(List<Token> tokens)
		{
		_tokens = tokens;
		_pos = 0;
		}

	// ── public entry point ────────────────────────────────────────────────────

	public Script Parse()
		{
		var nodes = new List<Node>();
		SkipNewlines();
		while (!IsEof())
			{
			var node = ParseList();
			if (node is not null)
				nodes.Add(node);
			SkipNewlines();
			}
		return new Script(nodes);
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
			commands.Add((next, stderrToo));
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

		// function definition: NAME ()
		if (t.Type == TokenType.Word && !t.Value.Contains('=') && !t.Value.Contains('[')
		    && PeekAhead(1).Type == TokenType.LParen)
			{
			// make sure it's actually "name()" and not "name (args)"
			// bash requires no space before () for function defs — but we'll be lenient
			var nameToken = Advance(); // NAME
			Advance(); // (
			Expect(TokenType.RParen);
			SkipNewlines();
			var body = ParseCompoundCommandBody()
				?? throw Error($"Expected compound command body for function '{nameToken.Value}'");
			var redirects = ParseRedirects();
			return new FunctionDef(nameToken.Value, body, redirects);
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

		while (true)
			{
			// consume any leading redirects (bash allows them anywhere)
			var r = TryParseRedirect();
			if (r is not null) { redirects.Add(r); continue; }

			if (IsWordToken())
				{
				var word = ParseWord()!;

				// arr=(a b c) — compound array assignment
				if (name is null && args.Count == 0 && IsArrayCompoundAssign(word, out var acName))
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
					return new ArrayCompoundAssign(acName!, values, redirects);
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

		return new SimpleCommand(assignments, name, args, redirects);
		}

	private static bool IsAssignment(Word word, out string? name, out Word? value)
		{
		name = null; value = null;
		if (word.Parts.Count == 0) return false;

		// The first part must be a literal that contains '='
		if (word.Parts[0] is not LiteralPart lit) return false;
		int eq = lit.Value.IndexOf('=');
		if (eq <= 0) return false;

		// LHS must be a valid identifier
		var lhs = lit.Value[..eq];
		if (!IsValidIdentifier(lhs)) return false;

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

	/// <summary>Detects arr=( — the word is just "arr=" and the next token is (.</summary>
	private bool IsArrayCompoundAssign(Word word, out string? name)
		{
		name = null;
		if (word.Parts is not [LiteralPart lit]) return false;
		if (!lit.Value.EndsWith('=')) return false;
		var lhs = lit.Value[..^1];
		if (!IsValidIdentifier(lhs)) return false;
		// Only treat as array compound assign if the very next token is (
		if (Peek().Type != TokenType.LParen) return false;
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

	private ForCommand ParseFor()
		{
		ExpectWord("for");
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
		return new ForCommand(varName, words, body, redirects);
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
		var left = ParseCondAnd();
		while (PeekWord("||"))
			{
			Advance();
			left = new CondOr(left, ParseCondAnd());
			}
		return left;
		}

	private CondExpr ParseCondAnd()
		{
		var left = ParseCondNot();
		while (PeekWord("&&"))
			{
			Advance();
			left = new CondAnd(left, ParseCondNot());
			}
		return left;
		}

	private CondExpr ParseCondNot()
		{
		if (PeekWord("!"))
			{
			Advance();
			return new CondNot(ParseCondPrimary());
			}
		return ParseCondPrimary();
		}

	private CondExpr ParseCondPrimary()
		{
		// unary: -f, -d, -z, -n, etc.
		if (Peek().Type == TokenType.Word && Peek().Value.Length == 2 && Peek().Value[0] == '-')
			{
			var op = Advance().Value;
			var operand = ParseWord() ?? throw Error($"Expected operand for unary operator '{op}'");
			return new CondUnary(op, operand);
			}

		var left = ParseWord() ?? throw Error("Expected expression in [[ ]]");

		// binary: =, !=, ==, <, >, -eq, -ne, -lt, -le, -gt, -ge, =~
		if (IsWordToken() && IsBinaryCondOp(Peek().Value))
			{
			var op = Advance().Value;
			var right = ParseWord() ?? throw Error($"Expected right operand for '{op}'");
			return new CondBinary(op, left, right);
			}

		return new CondWord(left);
		}

	private static bool IsBinaryCondOp(string s) =>
		s is "=" or "==" or "!=" or "<" or ">" or "=~"
		    or "-eq" or "-ne" or "-lt" or "-le" or "-gt" or "-ge";

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

		// optional fd digit immediately before a redirect operator
		if (Peek().Type == TokenType.Digit)
			{
			var kind2 = PeekAhead(1).Type;
			if (IsRedirectOp(kind2))
				{
				fd = int.Parse(Advance().Value);
				}
			}

		if (!IsRedirectOp(Peek().Type))
			return null;

		var op = Advance();
		var target = ParseWord() ?? throw Error("Expected target after redirect operator");

		var kind = op.Type switch
			{
			TokenType.Less           => RedirectKind.Input,
			TokenType.Greater        => RedirectKind.Output,
			TokenType.GreaterGreater => RedirectKind.Append,
			TokenType.GreaterPipe    => RedirectKind.Clobber,
			TokenType.LessAmpersand  => RedirectKind.InputDup,
			TokenType.GreaterAmpersand => RedirectKind.OutputDup,
			TokenType.LessGreater    => RedirectKind.ReadWrite,
			TokenType.LessLess       => RedirectKind.Heredoc,
			TokenType.LessLessDash   => RedirectKind.HeredocStrip,
			_ => throw Error($"Unknown redirect operator '{op.Value}'")
			};

		return new Redirect(fd, kind, target);
		}

	private static bool IsRedirectOp(TokenType t) =>
		t is TokenType.Less or TokenType.Greater or TokenType.GreaterGreater
		    or TokenType.GreaterPipe or TokenType.LessAmpersand or TokenType.GreaterAmpersand
		    or TokenType.LessGreater or TokenType.LessLess or TokenType.LessLessDash;

	// ── word / word-part parsing ──────────────────────────────────────────────

	private bool IsWordToken()
		{
		var t = Peek();
		// A Digit token is only a word constituent when it is NOT immediately
		// followed by a redirect operator (i.e. it's not an fd number like 2>).
		if (t.Type == TokenType.Digit && IsRedirectOp(PeekAhead(1).Type))
			return false;
		return t.Type is TokenType.Word or TokenType.Digit
		               or TokenType.SingleQuoted or TokenType.DoubleQuoted
		               or TokenType.Dollar or TokenType.DollarLParen
		               or TokenType.DollarDollarLParen or TokenType.DollarLBrace
		               or TokenType.DollarSingleQuote
		               or TokenType.HeredocBody
		               or TokenType.Backtick;
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

			case TokenType.HeredocBody:
				Advance();
				yield return new HeredocBodyPart(t.Value);
				break;

			case TokenType.DoubleQuoted:
				Advance();
				yield return new DoubleQuotedPart(ParseDoubleQuotedInterior(t.Value));
				break;

			case TokenType.Dollar:
				Advance();
				if (IsWordToken())
					{
					var name = Advance();
					yield return new VarExpansionPart(name.Value);
					}
				else
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
	/// Re-lex the interior of a double-quoted string to find expansions.
	/// The lexer stored the raw interior (without the outer quotes).
	/// </summary>
	private static List<WordPart> ParseDoubleQuotedInterior(string raw)
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
						var innerParser = new Parser(innerLex.Tokenize());
						parts.Add(new CommandSubstitutionPart(innerParser.Parse()));
						}
					}
				else if (raw[i] == '{')
					{
					i++; // skip {
					int start = i;
					while (i < raw.Length && raw[i] != '}') i++;
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
				var innerParser = new Parser(innerLex.Tokenize());
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
		// Collect tokens until matching )
		var inner = new List<Token>();
		int depth = 1;
		while (!IsEof() && depth > 0)
			{
			var t = Peek();
			if (t.Type == TokenType.LParen) depth++;
			if (t.Type == TokenType.RParen) { depth--; if (depth == 0) { Advance(); break; } }
			inner.Add(Advance());
			}
		inner.Add(Token.Eof(0, 0));
		var subParser = new Parser(inner);
		return new CommandSubstitutionPart(subParser.Parse());
		}

	private ArithmeticExpansionPart ParseArithmeticExpansion()
		{
		// Collect raw text until ))
		var sb = new System.Text.StringBuilder();
		while (!IsEof())
			{
			var t = Peek();
			if (t.Type == TokenType.RParen && PeekAhead(1).Type == TokenType.RParen)
				{
				Advance(); Advance(); // consume ))
				break;
				}
			sb.Append(Advance().Value);
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
		var subParser = new Parser(inner);
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
