namespace Bash.Evaluator;

/// <summary>Variable access for arithmetic evaluation. <paramref name="subscript"/> is the
/// raw text inside `name[...]` (null for a plain name); the implementer decides whether it
/// is an arithmetic index or an associative key.</summary>
public interface IArithVars
	{
	long Get(string name, string? subscript);
	void Set(string name, string? subscript, long value);
	}

/// <summary>
/// Evaluates a bash arithmetic expression string to a long.
/// Supports: + - * / % ** (power), unary - + ~ !, prefix/postfix ++ --,
/// bitwise &amp; | ^ &lt;&lt; &gt;&gt;, comparison == != &lt; &lt;= &gt; &gt;=, logical &amp;&amp; ||
/// (short-circuit), ternary ? : (lazy), assignment = += -= *= /= %= **= &lt;&lt;= &gt;&gt;= &amp;= |= ^=
/// (right-associative), the comma operator, parentheses, decimal / octal (0NNN) / hex (0xNN)
/// / base#NNN literals, and identifiers (with optional [subscript]) resolved through
/// <see cref="IArithVars"/> — unset names read as 0, as in bash.
///
/// Single-pass recursive-descent evaluator (no AST). Each precedence level is an explicit
/// method calling the next-deeper one; number parsing is substring-free. Skipped operands
/// (the untaken ternary branch, the right side of a decided &amp;&amp;/||) are parsed with side
/// effects and faults suppressed, matching bash's lazy evaluation.
/// </summary>
public static class ArithParser
	{
	public static long Evaluate(string expr) => Evaluate(expr, null);

	public static long Evaluate(string expr, IArithVars? vars)
		{
		var p = new ArithParserState(expr, vars);
		long result = p.ParseComma();
		p.ExpectEnd();
		return result;
		}

	private sealed class ArithParserState
		{
		private readonly string _s;
		private readonly string _raw;    // the text as given: a message shows it front-trimmed, its end kept (trimmed only then)
		private readonly IArithVars? _vars;
		private int _pos;
		private int _skip;      // >0 while parsing an operand whose value is discarded
		// where the token being looked at starts (bash's "error token" runs from here to the end): set by SkipWs, which
		// comes before every token; the end of the text never moves it, so at the end it names the last token read
		private int _tok;

		public ArithParserState(string s, IArithVars? vars) { _s = s.Trim(); _raw = s; _vars = vars; }

		/// <summary>An error as bash words it (measured against bash 5.1, 2026-11-15): `EXPR: MESSAGE (error token is
		/// "TOKEN")`; it was C#Bash's own words ("Expected number in arithmetic expression").</summary>
		private ArithException Error(string message)
			{
			var full = _raw.TrimStart();
			return new($"{full}: {message} (error token is \"{full[Math.Min(_tok, full.Length)..]}\")");
			}

		/// <summary>A malformed number, as bash names it: the number itself stands for the expression (`2#102: value too
		/// great for base (error token is "2#102")`).</summary>
		private ArithException NumberError(int start, string message)
			{
			int end = start;
			while (end < _s.Length && (char.IsLetterOrDigit(_s[end]) || _s[end] is '#' or '@' or '_')) end++;
			var text = _s[start..end];
			return new($"{text}: {message} (error token is \"{text}\")");
			}

		public void ExpectEnd()
			{
			SkipWs();
			if (_pos < _s.Length) throw Error("syntax error in expression");
			}

		// ── expression grammar (lowest precedence first) ──────────────────────────

		public long ParseComma()
			{
			long v = ParseAssign();
			while (true) { SkipWs(); if (!TryConsume(',')) break; v = ParseAssign(); }
			return v;
			}

		private long ParseAssign()
			{
			SkipWs();
			int save = _pos;
			if (_pos < _s.Length && (char.IsLetter(_s[_pos]) || _s[_pos] == '_'))
				{
				var (name, sub) = ReadLvalue();
				SkipWs();
				string? op = MatchAssignOp();
				if (op is not null)
					{
					long rhs = ParseAssign();
					long cur = op == "=" ? 0 : GetVar(name, sub);
					long val = op switch
						{
						"="   => rhs,
						"+="  => cur + rhs,
						"-="  => cur - rhs,
						"*="  => cur * rhs,
						"/="  => rhs == 0 ? (_skip > 0 ? 0 : throw Error("division by 0")) : cur / rhs,
						"%="  => rhs == 0 ? (_skip > 0 ? 0 : throw Error("division by 0")) : cur % rhs,
						"**=" => Power(cur, rhs),
						"<<=" => cur << (int)rhs,
						">>=" => cur >> (int)rhs,
						"&="  => cur & rhs,
						"|="  => cur | rhs,
						"^="  => cur ^ rhs,
						_     => rhs,
						};
					SetVar(name, sub, val);
					return val;
					}
				_pos = save;
				}
			long result = ParseTernary();
			// `3 = 4`: an assignment to what is not a variable (it was "syntax error in expression"'s kind)
			SkipWs();
			int at = _pos;
			if (MatchAssignOp() is not null) { _pos = at; throw Error("attempted assignment to non-variable"); }
			return result;
			}

		private string? MatchAssignOp()
			{
			if (_pos >= _s.Length) return null;
			// = but not ==
			if (_s[_pos] == '=' && !Ahead('=')) { _pos++; return "="; }
			foreach (var op in new[] { "**=", "<<=", ">>=", "+=", "-=", "*=", "/=", "%=", "&=", "|=", "^=" })
				{
				if (string.CompareOrdinal(_s, _pos, op, 0, op.Length) == 0)
					{
					// avoid treating "<=" / ">=" / "!=" / "==" as compound assigns: those never start
					// with the two-char prefixes above except "<<=" / ">>=" which are checked first.
					_pos += op.Length; return op;
					}
				}
			return null;
			}

		private long ParseTernary()
			{
			var val = ParseOr();
			SkipWs();
			if (At('?'))
				{
				_pos++;
				long t, f;
				// bash: `1 ? : 2` and `1 ? 2 :` are "expression expected", a missing `:` its own message
				SkipWs(); if (_pos >= _s.Length || At(':')) throw Error("expression expected");
				if (val != 0) t = ParseAssign(); else { _skip++; try { t = ParseAssign(); } finally { _skip--; } }
				SkipWs(); Consume(':', "`:' expected for conditional expression");
				SkipWs(); if (_pos >= _s.Length) throw Error("expression expected");
				if (val != 0) { _skip++; try { f = ParseAssign(); } finally { _skip--; } return t; }
				f = ParseAssign();
				return f;
				}
			return val;
			}

		private long ParseOr()
			{
			long left = ParseAnd();
			while (true)
				{
				SkipWs(); if (!Match2('|', '|')) break;
				if (left != 0) { _skip++; try { ParseAnd(); } finally { _skip--; } left = 1; }
				else { long r = ParseAnd(); left = r != 0 ? 1L : 0L; }
				}
			return left;
			}

		private long ParseAnd()
			{
			long left = ParseBitOr();
			while (true)
				{
				SkipWs(); if (!Match2('&', '&')) break;
				if (left == 0) { _skip++; try { ParseBitOr(); } finally { _skip--; } left = 0; }
				else { long r = ParseBitOr(); left = r != 0 ? 1L : 0L; }
				}
			return left;
			}

		// Single-char | but not || (ParseOr) and not |= (assignment).
		private long ParseBitOr()
			{
			long left = ParseBitXor();
			while (true) { SkipWs(); if (!(At('|') && !Ahead('|') && !Ahead('='))) break; _pos++; left |= ParseBitXor(); }
			return left;
			}

		private long ParseBitXor()
			{
			long left = ParseBitAnd();
			while (true) { SkipWs(); if (!(At('^') && !Ahead('='))) break; _pos++; left ^= ParseBitAnd(); }
			return left;
			}

		// Single-char & but not && (ParseAnd) and not &=.
		private long ParseBitAnd()
			{
			long left = ParseEquality();
			while (true) { SkipWs(); if (!(At('&') && !Ahead('&') && !Ahead('='))) break; _pos++; left &= ParseEquality(); }
			return left;
			}

		private long ParseEquality()
			{
			long left = ParseRelational();
			while (true)
				{
				SkipWs();
				if      (Match2('=', '=')) left = left == ParseRelational() ? 1L : 0L;
				else if (Match2('!', '=')) left = left != ParseRelational() ? 1L : 0L;
				else break;
				}
			return left;
			}

		private long ParseRelational()
			{
			long left = ParseShift();
			while (true)
				{
				SkipWs();
				if      (Match2('<', '=')) left = left <= ParseShift() ? 1L : 0L;
				else if (Match2('>', '=')) left = left >= ParseShift() ? 1L : 0L;
				else if (At('<') && !Ahead('<')) { _pos++; left = left < ParseShift() ? 1L : 0L; }
				else if (At('>') && !Ahead('>')) { _pos++; left = left > ParseShift() ? 1L : 0L; }
				else break;
				}
			return left;
			}

		private long ParseShift()
			{
			long left = ParseAddSub();
			while (true)
				{
				SkipWs();
				if (_pos + 2 < _s.Length && _s[_pos + 2] == '=' && (Match2Peek('<', '<') || Match2Peek('>', '>'))) break; // <<= >>= handled by assign
				if      (Match2('<', '<')) left <<= (int)ParseAddSub();
				else if (Match2('>', '>')) left >>= (int)ParseAddSub();
				else break;
				}
			return left;
			}

		private long ParseAddSub()
			{
			long left = ParseMulDiv();
			while (true)
				{
				SkipWs();
				if      (At('+') && !Ahead('=') && !Ahead('+')) { _pos++; left += ParseMulDiv(); }
				else if (At('-') && !Ahead('=') && !Ahead('-')) { _pos++; left -= ParseMulDiv(); }
				else break;
				}
			return left;
			}

		// Single-char * but not ** (power) and not *=.
		private long ParseMulDiv()
			{
			long left = ParsePower();
			while (true)
				{
				SkipWs();
				if (At('*') && !Ahead('*') && !Ahead('=')) { _pos++; left *= ParsePower(); }
				else if (At('/') && !Ahead('=')) { _pos++; long r = ParsePower(); left = r == 0 ? (_skip > 0 ? 0 : throw Error("division by 0")) : left / r; }
				else if (At('%') && !Ahead('=')) { _pos++; long r = ParsePower(); left = r == 0 ? (_skip > 0 ? 0 : throw Error("division by 0")) : left % r; }
				else break;
				}
			return left;
			}

		/// <summary>b ** e in 64-bit integers, wrapping as bash's repeated multiplication; it was Math.Pow through a double
		/// (3**39 came out 4052555153018976256, bash 4052555153018976267) and a negative exponent gave 0, where bash says
		/// "exponent less than 0" (2026-11-15), even in a branch not taken (`0 && 2**-1`), unlike division by 0.</summary>
		private long Power(long b, long e)
			{
			if (e < 0) throw Error("exponent less than 0");   // even in a branch not taken, as bash (measured)
			long r = 1;
			unchecked { while (e > 0) { if ((e & 1) != 0) r *= b; b *= b; e >>= 1; } }
			return r;
			}

		// Right-associative power.
		private long ParsePower()
			{
			long b = ParseUnary();
			SkipWs();
			if (_pos + 2 < _s.Length && _s[_pos] == '*' && _s[_pos + 1] == '*' && _s[_pos + 2] == '=') return b;
			if (Match2('*', '*')) return Power(b, ParsePower());
			return b;
			}

		private long ParseUnary()
			{
			SkipWs();
			if (Match2('+', '+') || Match2('-', '-'))
				{
				bool inc = _s[_pos - 1] == '+';
				SkipWs();
				if (_pos < _s.Length && (char.IsLetter(_s[_pos]) || _s[_pos] == '_'))
					{
					var (name, sub) = ReadLvalue();
					long v = GetVar(name, sub) + (inc ? 1 : -1);
					SetVar(name, sub, v);
					return v;
					}
				// "++5" style: treat as unary applied twice
				long inner = ParseUnary();
				return inner;
				}
			if (TryConsume('-')) return -ParseUnary();
			if (TryConsume('+')) return  ParseUnary();
			if (TryConsume('~')) return ~ParseUnary();
			if (At('!') && !Ahead('=')) { _pos++; return ParseUnary() == 0 ? 1L : 0L; }   // ! but not !=
			return ParsePrimary();
			}

		private long ParsePrimary()
			{
			SkipWs();
			int open = _pos;
			if (TryConsume('('))
				{
				var v = ParseComma();
				SkipWs(); Consume(')', "missing `)'");
				_tok = open;   // bash reads a parenthesized group as one token: `5 / (2 - 2)` names "(2 - 2)"
				return v;
				}
			if (_pos < _s.Length && (char.IsLetter(_s[_pos]) || _s[_pos] == '_'))
				{
				var (name, sub) = ReadLvalue();
				long v = GetVar(name, sub);
				SkipWs();
				if (Match2('+', '+')) { SetVar(name, sub, v + 1); return v; }
				if (Match2('-', '-')) { SetVar(name, sub, v - 1); return v; }
				return v;
				}
			return ParseNumber();
			}

		/// <summary>Read `name` or `name[subscript]` (subscript text kept raw, brackets balanced).</summary>
		private (string name, string? sub) ReadLvalue()
			{
			int start = _pos;
			while (_pos < _s.Length && (char.IsLetterOrDigit(_s[_pos]) || _s[_pos] == '_')) _pos++;
			var name = _s[start.._pos];
			if (At('['))
				{
				_pos++;
				int depth = 1, s0 = _pos;
				while (_pos < _s.Length && depth > 0)
					{
					if (_s[_pos] == '[') depth++;
					else if (_s[_pos] == ']') { depth--; if (depth == 0) break; }
					_pos++;
					}
				// no `]`: bash's "bad array subscript", its token the whole name on (`a[1`)
				if (_pos >= _s.Length) { _tok = start; throw Error("bad array subscript"); }
				var sub = _s[s0.._pos];
				_pos++;   // ]
				return (name, sub);
				}
			return (name, null);
			}

		private long GetVar(string name, string? sub) => _vars?.Get(name, sub) ?? 0L;

		private void SetVar(string name, string? sub, long value)
			{
			if (_skip > 0) return;
			_vars?.Set(name, sub, value);
			}

		// Substring-free number parsing (no allocation). bash's words for a bad one (measured against bash 5.1,
		// 2026-11-15): a missing operand "syntax error: operand expected"; `08`, `2#102`, `0xg`, `12a` "value too great for
		// base"; `65#1` "invalid arithmetic base"; `2#` "invalid integer constant". `0x` alone is 0, and base 64 takes
		// `@` (62) and `_` (63) as digits (`64#@` was an error).
		private long ParseNumber()
			{
			SkipWs();
			if (_pos >= _s.Length || !char.IsAsciiDigit(_s[_pos])) throw Error("syntax error: operand expected");
			int start = _pos;
			long v = 0;

			if (_s[_pos] == '0' && _pos + 1 < _s.Length && (_s[_pos + 1] is 'x' or 'X'))
				{
				_pos += 2;
				while (_pos < _s.Length && Uri.IsHexDigit(_s[_pos])) { v = v * 16 + HexVal(_s[_pos]); _pos++; }
				}
			else
				{
				while (_pos < _s.Length && char.IsAsciiDigit(_s[_pos])) _pos++;
				if (At('#'))
					{
					long b = 0;
					for (int i = start; i < _pos && b <= 64; i++) b = b * 10 + (_s[i] - '0');
					if (b < 2 || b > 64) throw NumberError(start, "invalid arithmetic base");
					_pos++;
					int ds = _pos;
					while (_pos < _s.Length && (char.IsAsciiLetterOrDigit(_s[_pos]) || _s[_pos] is '@' or '_'))
						{
						char d = _s[_pos];
						int dv = char.IsAsciiDigit(d) ? d - '0'
							: char.IsAsciiLetterLower(d) ? d - 'a' + 10
							: char.IsAsciiLetterUpper(d) ? d - 'A' + (b <= 36 ? 10 : 36)
							: d == '@' ? 62 : 63;
						if (dv >= b) throw NumberError(start, "value too great for base");
						v = v * b + dv; _pos++;
						}
					if (_pos == ds) throw NumberError(start, "invalid integer constant");
					return v;
					}
				int @base = (_pos - start > 1 && _s[start] == '0') ? 8 : 10;
				for (int i = start; i < _pos; i++)
					{
					int d = _s[i] - '0';
					if (d >= @base) throw NumberError(start, "value too great for base");
					v = v * @base + d;
					}
				}
			// a number runs on into letters (`0xg`, `12a`): one bad number, as bash reads it
			if (_pos < _s.Length && (char.IsAsciiLetterOrDigit(_s[_pos]) || _s[_pos] is '@' or '_'))
				throw NumberError(start, "value too great for base");
			return v;
			}

		// ── helpers ───────────────────────────────────────────────────────────────

		private static int HexVal(char c) => c <= '9' ? c - '0' : (char.ToLowerInvariant(c) - 'a' + 10);

		private void SkipWs()
			{
			while (_pos < _s.Length && _s[_pos] is ' ' or '\t' or '\n' or '\r') _pos++;
			if (_pos < _s.Length) _tok = _pos;
			}

		private bool At(char c)    => _pos < _s.Length && _s[_pos] == c;
		private bool Ahead(char c) => _pos + 1 < _s.Length && _s[_pos + 1] == c;

		private bool Match2Peek(char a, char b) => _pos + 1 < _s.Length && _s[_pos] == a && _s[_pos + 1] == b;

		private bool Match2(char a, char b)
			{
			if (_pos + 1 < _s.Length && _s[_pos] == a && _s[_pos + 1] == b) { _pos += 2; return true; }
			return false;
			}

		private bool TryConsume(char c)
			{
			if (At(c)) { _pos++; return true; }
			return false;
			}

		private void Consume(char c, string message)
			{
			if (!At(c)) throw Error(message);
			_pos++;
			}
		}
	}
