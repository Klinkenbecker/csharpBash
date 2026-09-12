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
		private readonly IArithVars? _vars;
		private int _pos;
		private int _skip;      // >0 while parsing an operand whose value is discarded

		public ArithParserState(string s, IArithVars? vars) { _s = s.Trim(); _vars = vars; }

		public void ExpectEnd()
			{
			SkipWs();
			if (_pos < _s.Length)
				throw new EvalException($"Unexpected character '{_s[_pos]}' in arithmetic expression");
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
						"/="  => rhs == 0 ? (_skip > 0 ? 0 : throw new EvalException("Division by zero")) : cur / rhs,
						"%="  => rhs == 0 ? (_skip > 0 ? 0 : throw new EvalException("Modulo by zero"))   : cur % rhs,
						"**=" => (long)Math.Pow(cur, rhs),
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
			return ParseTernary();
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
				if (val != 0) { t = ParseAssign(); SkipWs(); Consume(':'); _skip++; try { f = ParseAssign(); } finally { _skip--; } return t; }
				_skip++; try { t = ParseAssign(); } finally { _skip--; }
				SkipWs(); Consume(':');
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
				else if (At('/') && !Ahead('=')) { _pos++; long r = ParsePower(); left = r == 0 ? (_skip > 0 ? 0 : throw new EvalException("Division by zero")) : left / r; }
				else if (At('%') && !Ahead('=')) { _pos++; long r = ParsePower(); left = r == 0 ? (_skip > 0 ? 0 : throw new EvalException("Modulo by zero"))   : left % r; }
				else break;
				}
			return left;
			}

		// Right-associative power.
		private long ParsePower()
			{
			long b = ParseUnary();
			SkipWs();
			if (_pos + 2 < _s.Length && _s[_pos] == '*' && _s[_pos + 1] == '*' && _s[_pos + 2] == '=') return b;
			if (Match2('*', '*')) return (long)Math.Pow(b, ParsePower());
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
			if (TryConsume('('))
				{
				var v = ParseComma();
				SkipWs(); Consume(')');
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
				var sub = _s[s0.._pos];
				if (_pos < _s.Length) _pos++;   // ]
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

		// Substring-free number parsing (no allocation).
		private long ParseNumber()
			{
			SkipWs();
			if (_pos >= _s.Length)
				throw new EvalException("Expected number in arithmetic expression");

			char c = _s[_pos];

			// hex: 0x…
			if (c == '0' && _pos + 1 < _s.Length && (_s[_pos + 1] is 'x' or 'X'))
				{
				_pos += 2;
				int start = _pos;
				long v = 0;
				while (_pos < _s.Length && Uri.IsHexDigit(_s[_pos])) { v = v * 16 + HexVal(_s[_pos]); _pos++; }
				if (_pos == start) throw new EvalException("Expected hex digits in arithmetic expression");
				return v;
				}

			// decimal, or octal when a leading 0 is followed by more digits, or base#digits
			if (char.IsAsciiDigit(c))
				{
				int start = _pos;
				while (_pos < _s.Length && char.IsAsciiDigit(_s[_pos])) _pos++;
				if (At('#'))
					{
					long b = 0;
					for (int i = start; i < _pos; i++) b = b * 10 + (_s[i] - '0');
					if (b < 2 || b > 64) throw new EvalException($"Invalid arithmetic base '{b}'");
					_pos++;
					long v = 0; int ds = _pos;
					while (_pos < _s.Length && char.IsLetterOrDigit(_s[_pos]))
						{
						char d = _s[_pos];
						int dv = char.IsAsciiDigit(d) ? d - '0'
							: char.IsAsciiLetterLower(d) ? d - 'a' + 10
							: char.IsAsciiLetterUpper(d) ? d - 'A' + (b <= 36 ? 10 : 36) : 99;
						if (dv >= b) throw new EvalException($"Value too great for base '{d}'");
						v = v * b + dv; _pos++;
						}
					if (_pos == ds) throw new EvalException("Expected digits after base#");
					return v;
					}
				int @base = (_pos - start > 1 && _s[start] == '0') ? 8 : 10;
				long val = 0;
				for (int i = start; i < _pos; i++)
					{
					int d = _s[i] - '0';
					if (d >= @base) throw new EvalException($"Invalid octal digit '{_s[i]}' in arithmetic expression");
					val = val * @base + d;
					}
				return val;
				}

			throw new EvalException($"Unexpected '{c}' in arithmetic expression");
			}

		// ── helpers ───────────────────────────────────────────────────────────────

		private static int HexVal(char c) => c <= '9' ? c - '0' : (char.ToLowerInvariant(c) - 'a' + 10);

		private void SkipWs() { while (_pos < _s.Length && _s[_pos] is ' ' or '\t' or '\n' or '\r') _pos++; }

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

		private void Consume(char c)
			{
			if (!At(c)) throw new EvalException($"Expected '{c}' in arithmetic expression");
			_pos++;
			}
		}
	}
