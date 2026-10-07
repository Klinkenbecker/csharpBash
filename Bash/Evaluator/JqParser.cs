using System.Globalization;
using System.Text;

namespace Bash.Evaluator;

// ── jq filters: tokens, syntax tree, parser (DECISIONS 2026-10-05, the in-process jq subset) ──
// Grammar and precedence follow jq 1.6 (parser.y): `|` < `,` < `//` < assignments < `or` < `and`
// < comparisons < `+ -` < `* / %` < unary minus < postfix (`.a`, `[..]`, `?`). Anything outside
// the ratified scope is refused HERE, before any input is read, so the dispatcher can still hand
// the whole command to a PATH jq (DECISIONS 2026-09-04 #2). Compile errors read as jq 1.6's do
// (measured against it): bison's token names, "(Unix shell quoting issues?)", the source line
// padded to the offending token's column.

internal abstract record JqNode;
internal sealed record JIdentity : JqNode;
internal sealed record JRecurse : JqNode;                                         // ..
internal sealed record JField(JqNode Target, string Name) : JqNode;               // .a
internal sealed record JIndex(JqNode Target, JqNode Index) : JqNode;              // .[e]  (e runs on the term's own input)
internal sealed record JSlice(JqNode Target, JqNode? From, JqNode? To) : JqNode;  // .[a:b]
internal sealed record JIterate(JqNode Target) : JqNode;                          // .[]
internal sealed record JTry(JqNode Body, JqNode? Catch) : JqNode;                 // try / ?
internal sealed record JLiteral(object? Value) : JqNode;
internal sealed record JString(List<object> Parts, string? Format) : JqNode;      // parts: string | JqNode
internal sealed record JFormat(string Name) : JqNode;                             // @csv as a filter
internal sealed record JArray(JqNode? Body) : JqNode;
internal sealed record JObject(List<(JqNode Key, JqNode Value)> Entries) : JqNode;
internal sealed record JNeg(JqNode Body) : JqNode;
internal sealed record JPipe(JqNode Left, JqNode Right) : JqNode;
internal sealed record JComma(JqNode Left, JqNode Right) : JqNode;
internal sealed record JBinary(string Op, JqNode Left, JqNode Right) : JqNode;    // + - * / % == != < <= > >=
internal sealed record JAnd(JqNode Left, JqNode Right) : JqNode;
internal sealed record JOr(JqNode Left, JqNode Right) : JqNode;
internal sealed record JAlt(JqNode Left, JqNode Right) : JqNode;                  // //
internal sealed record JAssign(string Op, JqNode Lhs, JqNode Rhs) : JqNode;       // = |= += -= *= /= %= //=
internal sealed record JIf(JqNode Cond, JqNode Then, JqNode? Else) : JqNode;      // elif is a nested JIf
internal sealed record JReduce(JqNode Source, JqPattern Pattern, JqNode Init, JqNode Update) : JqNode;
internal sealed record JBind(JqNode Source, JqPattern Pattern, JqNode Body) : JqNode;   // Term as $x | body
internal sealed record JVar(string Name) : JqNode;
internal sealed record JCall(string Name, List<JqNode> Args) : JqNode;

internal abstract record JqPattern;
internal sealed record JPVar(string Name) : JqPattern;
internal sealed record JPArray(List<JqPattern> Items) : JqPattern;
internal sealed record JPObject(List<(JqNode Key, JqPattern Value)> Entries) : JqPattern;

/// <summary>A filter uses something outside the in-process subset: the dispatcher defers or refuses.</summary>
internal sealed class JqUnsupportedException(string what) : Exception(what) { public string What { get; } = what; }

/// <summary>A jq compile error (exit 3): the message, and the offset in the filter it points at.</summary>
internal sealed class JqSyntaxException(string message, int offset) : Exception(message) { public int Offset { get; } = offset; }

internal enum JqTok { Ident, Field, Var, Num, Str, Format, Dot, DotDot, Op, Invalid, End }

/// <summary>A token; <c>Start</c> is its offset in the whole filter text.</summary>
internal sealed record JqToken(JqTok Kind, string Text, int Start, object? Value = null);

internal static class JqLexer
	{
	public static List<JqToken> Lex(string src)
		{
		int i = 0;
		return Lex(src, ref i, untilParen: false);
		}

	private static readonly string[] Ops =
		["?//", "//=", "|=", "+=", "-=", "*=", "/=", "%=", "==", "!=", "<=", ">=", "//",
		 "|", ",", "=", "<", ">", "+", "-", "*", "/", "%", "(", ")", "[", "]", "{", "}", ":", ";", "?", "$"];

	private static List<JqToken> Lex(string s, ref int i, bool untilParen)
		{
		var toks = new List<JqToken>();
		var open = new Stack<char>();   // jq's lexer answers an unmatched closer with INVALID_CHARACTER
		while (true)
			{
			while (i < s.Length && (char.IsWhiteSpace(s[i]) || s[i] == '#'))
				{
				if (s[i] == '#') { while (i < s.Length && s[i] != '\n') i++; }
				else i++;
				}
			if (i >= s.Length)
				{
				toks.Add(new(JqTok.End, "$end", toks.Count > 0 ? toks[^1].Start : 0));   // jq points at the last token
				return toks;
				}
			char c = s[i];
			int st = i;
			if (untilParen && c == ')' && open.Count == 0) { i++; toks.Add(new(JqTok.End, ")", st)); return toks; }
			if (c == '"') { toks.Add(StringToken(s, ref i)); continue; }
			if (c == '.')
				{
				if (i + 1 < s.Length && s[i + 1] == '.') { toks.Add(new(JqTok.DotDot, "..", st)); i += 2; continue; }
				if (i + 1 < s.Length && (char.IsAsciiLetter(s[i + 1]) || s[i + 1] == '_'))
					{
					i++;
					while (i < s.Length && (char.IsAsciiLetterOrDigit(s[i]) || s[i] == '_')) i++;
					toks.Add(new(JqTok.Field, s[(st + 1)..i], st));
					continue;
					}
				if (i + 1 < s.Length && char.IsAsciiDigit(s[i + 1])) { toks.Add(Number(s, ref i)); continue; }
				toks.Add(new(JqTok.Dot, ".", st)); i++;
				continue;
				}
			if (char.IsAsciiDigit(c)) { toks.Add(Number(s, ref i)); continue; }
			if (char.IsAsciiLetter(c) || c == '_')
				{
				while (i < s.Length && (char.IsAsciiLetterOrDigit(s[i]) || s[i] == '_' || (s[i] == ':' && i + 1 < s.Length && s[i + 1] == ':'))) i += s[i] == ':' ? 2 : 1;
				toks.Add(new(JqTok.Ident, s[st..i], st));
				continue;
				}
			if (c == '$' && i + 1 < s.Length && (char.IsAsciiLetter(s[i + 1]) || s[i + 1] == '_'))
				{
				i++;
				while (i < s.Length && (char.IsAsciiLetterOrDigit(s[i]) || s[i] == '_')) i++;
				toks.Add(new(JqTok.Var, s[(st + 1)..i], st));
				continue;
				}
			if (c == '@')
				{
				i++;
				while (i < s.Length && (char.IsAsciiLetterOrDigit(s[i]) || s[i] == '_')) i++;
				toks.Add(new(JqTok.Format, s[(st + 1)..i], st));
				continue;
				}
			int at = i;
			var op = Ops.FirstOrDefault(o => string.CompareOrdinal(s, at, o, 0, o.Length) == 0);
			if (op is null) { toks.Add(new(JqTok.Invalid, c.ToString(), st)); i++; continue; }
			i += op.Length;
			if (op is "(" or "[" or "{") open.Push(op[0]);
			else if (op is ")" or "]" or "}")
				{
				char want = op == ")" ? '(' : op == "]" ? '[' : '{';
				if (open.Count == 0 || open.Peek() != want) { toks.Add(new(JqTok.Invalid, op, st)); continue; }
				open.Pop();
				}
			toks.Add(new(JqTok.Op, op, st));
			}
		}

	private static JqToken Number(string s, ref int i)
		{
		int st = i;
		while (i < s.Length && char.IsAsciiDigit(s[i])) i++;
		if (i < s.Length && s[i] == '.') { i++; while (i < s.Length && char.IsAsciiDigit(s[i])) i++; }
		if (i < s.Length && (s[i] == 'e' || s[i] == 'E'))
			{
			int save = i++;
			if (i < s.Length && (s[i] == '+' || s[i] == '-')) i++;
			if (i < s.Length && char.IsAsciiDigit(s[i])) { while (i < s.Length && char.IsAsciiDigit(s[i])) i++; }
			else i = save;
			}
		var text = s[st..i];
		return new(JqTok.Num, text, st, double.Parse(text, NumberStyles.Float, CultureInfo.InvariantCulture));
		}

	/// <summary>A string literal; `\(expr)` interpolations become token lists of their own.</summary>
	private static JqToken StringToken(string s, ref int i)
		{
		int st = i;
		i++;   // opening quote
		var parts = new List<object>();
		var sb = new StringBuilder();
		while (true)
			{
			if (i >= s.Length)
				throw new JqSyntaxException("syntax error, unexpected $end, expecting QQSTRING_TEXT or QQSTRING_INTERP_START or QQSTRING_END (Unix shell quoting issues?)", st + 1);   // at the text after the quote, as jq's lexer splits it
			char c = s[i++];
			if (c == '"') break;
			if (c != '\\') { sb.Append(c); continue; }
			if (i >= s.Length) continue;
			char e = s[i++];
			switch (e)
				{
				case '"': sb.Append('"'); break;
				case '\\': sb.Append('\\'); break;
				case '/': sb.Append('/'); break;
				case 'b': sb.Append('\b'); break;
				case 'f': sb.Append('\f'); break;
				case 'n': sb.Append('\n'); break;
				case 'r': sb.Append('\r'); break;
				case 't': sb.Append('\t'); break;
				case 'u':
					if (i + 4 > s.Length || !int.TryParse(s.AsSpan(i, 4), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out int u))
						throw new JqSyntaxException("syntax error, unexpected INVALID_CHARACTER (Unix shell quoting issues?)", i - 2);
					sb.Append((char)u); i += 4;
					break;
				case '(':
					if (sb.Length > 0) { parts.Add(sb.ToString()); sb.Clear(); }
					parts.Add(Lex(s, ref i, untilParen: true));
					break;
				default: throw new JqSyntaxException("syntax error, unexpected INVALID_CHARACTER (Unix shell quoting issues?)", i - 2);
				}
			}
		if (sb.Length > 0 || parts.Count == 0) parts.Add(sb.ToString());
		return new(JqTok.Str, "string", st, parts);
		}
	}

internal sealed class JqParser
	{
	private readonly List<JqToken> _t;
	private int _p;
	private readonly Func<string, int, bool> _supported;   // builtin name/arity implemented in-process
	private readonly Func<string, int, bool> _defined;     // builtin name/arity jq 1.6 defines
	private readonly List<string> _scope;                  // variables in scope, innermost last

	private JqParser(List<JqToken> tokens, Func<string, int, bool> supported, Func<string, int, bool> defined, List<string> scope)
		{ _t = tokens; _supported = supported; _defined = defined; _scope = scope; }

	public static JqNode Parse(string src, Func<string, int, bool> supported, Func<string, int, bool> defined, IEnumerable<string> namedVars)
		{
		var p = new JqParser(JqLexer.Lex(src), supported, defined, ["ENV", .. namedVars]);
		if (p.Peek.Kind == JqTok.End) return new JIdentity();   // jq '' is `.`
		if (!p.StartsTerm()) throw p.Unexpected(expectingEnd: true);
		var n = p.ParsePipe();
		if (p.Peek.Kind != JqTok.End) throw p.Unexpected(expectingEnd: true);
		return n;
		}

	private JqToken Peek => _t[_p];
	private JqToken Next() => _t[_p++];
	private bool IsOp(string op) => Peek.Kind == JqTok.Op && Peek.Text == op;
	private bool IsWord(string w) => Peek.Kind == JqTok.Ident && Peek.Text == w;
	private void Expect(string op) { if (!IsOp(op)) throw Unexpected(); _p++; }
	private void ExpectWord(string w) { if (!IsWord(w)) throw Unexpected(); _p++; }

	private static readonly HashSet<string> Keywords =
		["def", "if", "then", "elif", "else", "end", "as", "reduce", "foreach", "try", "catch", "label", "import", "include", "and", "or", "__loc__"];

	/// <summary>bison's name for a token, as jq's messages print it.</summary>
	private static string BisonName(JqToken t) => t.Kind switch
		{
		JqTok.End => "$end",
		JqTok.Ident => Keywords.Contains(t.Text) ? t.Text : "IDENT",
		JqTok.Field => "FIELD",
		JqTok.Var => "'$'",
		JqTok.Num => "LITERAL",
		JqTok.Str => "QQSTRING_START",
		JqTok.Format => "FORMAT",
		JqTok.Dot => "'.'",
		JqTok.DotDot => "..",
		JqTok.Invalid => "INVALID_CHARACTER",
		JqTok.Op when t.Text.Length == 1 => $"'{t.Text}'",
		_ => t.Text,
		};

	private JqSyntaxException Unexpected(bool expectingEnd = false) =>
		new($"syntax error, unexpected {BisonName(Peek)}{(expectingEnd ? ", expecting $end" : "")} (Unix shell quoting issues?)", Peek.Start);

	private bool StartsTerm() => Peek.Kind switch
		{
		JqTok.Num or JqTok.Str or JqTok.Format or JqTok.Dot or JqTok.Field or JqTok.DotDot or JqTok.Var => true,
		JqTok.Ident => !Keywords.Contains(Peek.Text) || Peek.Text is "if" or "reduce" or "try" or "def" or "foreach" or "label" or "import" or "include",
		JqTok.Op => Peek.Text is "(" or "[" or "{" or "-",
		_ => false,
		};

	// Pipe: `Term as Patterns | Pipe`, or Comma ('|' Pipe)?
	private JqNode ParsePipe()
		{
		if (IsWord("def")) throw new JqUnsupportedException("def (function definitions)");
		if (IsWord("label")) throw new JqUnsupportedException("label/break");
		int save = _p;
		if (StartsTerm() && !IsOp("-"))
			{
			JqNode? term = null;
			try { term = ParsePostfix(); } catch (JqSyntaxException) { }
			if (term is not null && IsWord("as"))
				{
				_p++;
				var pat = ParsePattern();
				if (IsOp("?//")) throw new JqUnsupportedException("?// (alternative destructuring)");
				Expect("|");
				int bound = _scope.Count;
				_scope.AddRange(PatternVars(pat));
				try { return new JBind(term, pat, ParsePipe()); }
				finally { _scope.RemoveRange(bound, _scope.Count - bound); }
				}
			_p = save;
			}
		var left = ParseComma();
		if (IsOp("|")) { _p++; return new JPipe(left, ParsePipe()); }
		return left;
		}

	private static IEnumerable<string> PatternVars(JqPattern p) => p switch
		{
		JPVar v => [v.Name],
		JPArray a => a.Items.SelectMany(PatternVars),
		JPObject o => o.Entries.SelectMany(e => PatternVars(e.Value)),
		_ => [],
		};

	private JqNode ParseComma()
		{
		var left = ParseAlt();
		while (IsOp(",")) { _p++; left = new JComma(left, ParseAlt()); }
		return left;
		}

	private JqNode ParseAlt()
		{
		var left = ParseAssign();
		if (IsOp("//")) { _p++; return new JAlt(left, ParseAlt()); }   // right-associative
		return left;
		}

	private static readonly HashSet<string> AssignOps = ["=", "|=", "+=", "-=", "*=", "/=", "%=", "//="];

	private JqNode ParseAssign()
		{
		var left = ParseOr();
		if (Peek.Kind == JqTok.Op && AssignOps.Contains(Peek.Text))
			{
			var op = Next().Text;
			return new JAssign(op, left, ParseAlt());
			}
		return left;
		}

	private JqNode ParseOr()
		{
		var left = ParseAnd();
		while (IsWord("or")) { _p++; left = new JOr(left, ParseAnd()); }
		return left;
		}

	private JqNode ParseAnd()
		{
		var left = ParseCompare();
		while (IsWord("and")) { _p++; left = new JAnd(left, ParseCompare()); }
		return left;
		}

	private JqNode ParseCompare()
		{
		var left = ParseAdditive();
		if (Peek.Kind == JqTok.Op && Peek.Text is "==" or "!=" or "<" or "<=" or ">" or ">=")
			{
			var op = Next().Text;
			return new JBinary(op, left, ParseAdditive());
			}
		return left;
		}

	private JqNode ParseAdditive()
		{
		var left = ParseMultiplicative();
		while (Peek.Kind == JqTok.Op && Peek.Text is "+" or "-") { var op = Next().Text; left = new JBinary(op, left, ParseMultiplicative()); }
		return left;
		}

	private JqNode ParseMultiplicative()
		{
		int start = Peek.Start;
		var left = ParseUnary();
		while (Peek.Kind == JqTok.Op && Peek.Text is "*" or "/" or "%")
			{
			var op = Next().Text;
			var right = ParseUnary();
			// jq folds constants while compiling: a literal divided by a literal 0 is a compile error
			if (op == "/" && left is JLiteral { Value: double } && right is JLiteral { Value: 0.0 })
				throw new JqSyntaxException("Division by zero?", start);
			left = new JBinary(op, left, right);
			}
		return left;
		}

	private JqNode ParseUnary()
		{
		if (IsOp("-")) { _p++; return new JNeg(ParsePostfix()); }
		return ParsePostfix();
		}

	private JqNode ParsePostfix()
		{
		var t = ParsePrimary();
		while (true)
			{
			if (Peek.Kind == JqTok.Field) { t = new JField(t, Next().Text); continue; }
			if (Peek.Kind == JqTok.Dot && _p + 1 < _t.Count && _t[_p + 1].Kind == JqTok.Str) { _p++; t = new JIndex(t, ParseString(Next(), null)); continue; }
			if (Peek.Kind == JqTok.Dot && _p + 1 < _t.Count && _t[_p + 1] is { Kind: JqTok.Op, Text: "[" }) { _p++; t = ParseBracket(t); continue; }
			if (IsOp("[")) { t = ParseBracket(t); continue; }
			if (IsOp("?")) { _p++; t = new JTry(t, null); continue; }
			return t;
			}
		}

	/// <summary>`[]`, `[e]`, `[a:b]`, `[:b]`, `[a:]` after a term (the `[` is next).</summary>
	private JqNode ParseBracket(JqNode target)
		{
		Expect("[");
		if (IsOp("]")) { _p++; return new JIterate(target); }
		if (IsOp(":")) { _p++; var to = ParsePipe(); Expect("]"); return new JSlice(target, null, to); }
		var e = ParsePipe();
		if (IsOp(":"))
			{
			_p++;
			if (IsOp("]")) { _p++; return new JSlice(target, e, null); }
			var to = ParsePipe(); Expect("]");
			return new JSlice(target, e, to);
			}
		Expect("]");
		return new JIndex(target, e);
		}

	private JqNode ParsePrimary()
		{
		var t = Peek;
		switch (t.Kind)
			{
			case JqTok.Num: _p++; return new JLiteral(t.Value);
			case JqTok.Str: _p++; return ParseString(t, null);
			case JqTok.Format:
				_p++;
				if (Peek.Kind == JqTok.Str) throw new JqUnsupportedException($"@{t.Text} \"...\" (format strings)");
				if (!IsSupportedFormat(t.Text)) throw new JqUnsupportedException($"@{t.Text}");
				return new JFormat(t.Text);
			case JqTok.Dot:
				_p++;
				if (Peek.Kind == JqTok.Str) return new JIndex(new JIdentity(), ParseString(Next(), null));
				if (IsOp("[")) return ParseBracket(new JIdentity());
				return new JIdentity();
			case JqTok.Field: _p++; return new JField(new JIdentity(), t.Text);
			case JqTok.DotDot: _p++; return new JRecurse();
			case JqTok.Var:
				_p++;
				if (t.Text == "__loc__") throw new JqUnsupportedException("$__loc__");
				if (!_scope.Contains(t.Text)) throw new JqSyntaxException($"${t.Text} is not defined", t.Start);
				return new JVar(t.Text);
			case JqTok.Op when t.Text == "(":
				{
				_p++;
				var e = ParsePipe();
				Expect(")");
				return e;
				}
			case JqTok.Op when t.Text == "[":
				{
				_p++;
				if (IsOp("]")) { _p++; return new JArray(null); }
				var e = ParsePipe();
				Expect("]");
				return new JArray(e);
				}
			case JqTok.Op when t.Text == "{": return ParseObject();
			case JqTok.Ident:
				switch (t.Text)
					{
					case "if": return ParseIf();
					case "reduce": return ParseReduce();
					case "try":
						{
						_p++;
						var body = ParsePostfix();
						JqNode? handler = null;
						if (IsWord("catch")) { _p++; handler = ParsePostfix(); }
						return new JTry(body, handler);
						}
					case "foreach": throw new JqUnsupportedException("foreach");
					case "def": throw new JqUnsupportedException("def (function definitions)");
					case "label": throw new JqUnsupportedException("label/break");
					case "import" or "include": throw new JqUnsupportedException("modules (import/include)");
					}
				if (Keywords.Contains(t.Text)) throw Unexpected();
				return ParseCall();
			}
		throw Unexpected();
		}

	private JqNode ParseCall()
		{
		var tok = Next();
		var name = tok.Text;
		var args = new List<JqNode>();
		if (IsOp("("))
			{
			_p++;
			args.Add(ParsePipe());
			while (IsOp(";")) { _p++; args.Add(ParsePipe()); }
			Expect(")");
			}
		if (name is "true" or "false" or "null" && args.Count == 0) return new JLiteral(name == "null" ? null : name == "true");
		if (!_supported(name, args.Count))
			{
			if (!_defined(name, args.Count)) throw new JqSyntaxException($"{name}/{args.Count} is not defined", tok.Start);
			throw new JqUnsupportedException($"{name}/{args.Count}");
			}
		return new JCall(name, args);
		}

	private JqNode ParseIf()
		{
		ExpectWord("if");
		var cond = ParsePipe();
		ExpectWord("then");
		var then = ParsePipe();
		return ParseElse(cond, then);
		}

	private JIf ParseElse(JqNode cond, JqNode then)
		{
		if (IsWord("elif"))
			{
			_p++;
			var c2 = ParsePipe(); ExpectWord("then");
			var t2 = ParsePipe();
			return new JIf(cond, then, ParseElse(c2, t2));
			}
		if (IsWord("else"))
			{
			_p++;
			var e = ParsePipe(); ExpectWord("end");
			return new JIf(cond, then, e);
			}
		ExpectWord("end");   // jq 1.7: no else means `else .`
		return new JIf(cond, then, null);
		}

	private JqNode ParseReduce()
		{
		ExpectWord("reduce");
		var src = ParsePostfix();
		ExpectWord("as");
		var pat = ParsePattern();
		Expect("(");
		var init = ParsePipe(); Expect(";");
		int bound = _scope.Count;
		_scope.AddRange(PatternVars(pat));
		JqNode update;
		try { update = ParsePipe(); }
		finally { _scope.RemoveRange(bound, _scope.Count - bound); }
		Expect(")");
		return new JReduce(src, pat, init, update);
		}

	private JqPattern ParsePattern()
		{
		if (Peek.Kind == JqTok.Var) return new JPVar(Next().Text);
		if (IsOp("["))
			{
			_p++;
			var items = new List<JqPattern> { ParsePattern() };
			while (IsOp(",")) { _p++; items.Add(ParsePattern()); }
			Expect("]");
			return new JPArray(items);
			}
		if (IsOp("{"))
			{
			_p++;
			var entries = new List<(JqNode, JqPattern)>();
			while (true)
				{
				if (Peek.Kind == JqTok.Var)
					{
					var v = Next().Text;
					if (IsOp(":")) throw new JqUnsupportedException("{$name: pattern} destructuring");
					entries.Add((new JLiteral(v), new JPVar(v)));
					}
				else
					{
					JqNode key = Peek.Kind switch
						{
						JqTok.Ident => new JLiteral(Next().Text),
						JqTok.Str => ParseString(Next(), null),
						JqTok.Op when Peek.Text == "(" => ParseParenKey(),
						_ => throw Unexpected(),
						};
					Expect(":");
					entries.Add((key, ParsePattern()));
					}
				if (IsOp(",")) { _p++; continue; }
				Expect("}");
				return new JPObject(entries);
				}
			}
		throw Unexpected();
		}

	private JqNode ParseParenKey() { Expect("("); var e = ParsePipe(); Expect(")"); return e; }

	private JqNode ParseObject()
		{
		Expect("{");
		var entries = new List<(JqNode, JqNode)>();
		if (IsOp("}")) { _p++; return new JObject(entries); }
		while (true)
			{
			var t = Peek;
			if (t.Kind == JqTok.Var)
				{
				_p++;
				if (t.Text == "__loc__") throw new JqUnsupportedException("$__loc__");
				if (!_scope.Contains(t.Text)) throw new JqSyntaxException($"${t.Text} is not defined", t.Start);
				if (IsOp(":")) { _p++; entries.Add((new JVar(t.Text), ParseObjectValue())); }
				else entries.Add((new JLiteral(t.Text), new JVar(t.Text)));                          // {$x}
				}
			else if (t.Kind == JqTok.Ident)
				{
				_p++;
				if (IsOp(":")) { _p++; entries.Add((new JLiteral(t.Text), ParseObjectValue())); }
				else entries.Add((new JLiteral(t.Text), new JField(new JIdentity(), t.Text)));         // {a}
				}
			else if (t.Kind == JqTok.Str)
				{
				_p++;
				var key = ParseString(t, null);
				if (IsOp(":")) { _p++; entries.Add((key, ParseObjectValue())); }
				else entries.Add((key, new JIndex(new JIdentity(), key)));                             // {"a b"}
				}
			else if (t.Kind == JqTok.Format) throw new JqUnsupportedException($"@{t.Text} as an object key");
			else if (IsOp("("))
				{
				var key = ParseParenKey();
				Expect(":");
				entries.Add((key, ParseObjectValue()));
				}
			else throw Unexpected();
			if (IsOp(",")) { _p++; continue; }
			Expect("}");
			return new JObject(entries);
			}
		}

	/// <summary>An object value: jq allows a pipe of terms here (no bare `,`). This accepts any
	/// expression short of a comma, a harmless superset.</summary>
	private JqNode ParseObjectValue()
		{
		var left = ParseAlt();
		if (IsOp("|")) { _p++; return new JPipe(left, ParseObjectValue()); }
		return left;
		}

	private JString ParseString(JqToken t, string? format)
		{
		var parts = new List<object>();
		foreach (var part in (List<object>)t.Value!)
			{
			if (part is string s) parts.Add(s);
			else
				{
				var sub = new JqParser((List<JqToken>)part, _supported, _defined, _scope);
				var e = sub.ParsePipe();
				if (sub.Peek.Kind != JqTok.End) throw sub.Unexpected();
				parts.Add(e);
				}
			}
		return new JString(parts, format);
		}

	public static bool IsSupportedFormat(string name) => name is "text" or "json" or "csv" or "tsv" or "base64";
	}
