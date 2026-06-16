namespace Bash.Evaluator;

/// <summary>
/// Evaluates a bash arithmetic expression string to a long.
/// Supports: +, -, *, /, %, ** (power), unary -, unary +, ~, !,
/// bitwise &amp;, |, ^, &lt;&lt;, &gt;&gt;,
/// comparison ==, !=, &lt;, &lt;=, &gt;, &gt;=,
/// logical &amp;&amp;, ||,
/// ternary ? :,
/// parentheses,
/// decimal / octal (0NNN) / hex (0xNN) literals,
/// and bare identifiers treated as 0.
///
/// Single-pass recursive-descent evaluator (no AST). Each precedence level is an
/// explicit method calling the next-deeper one — no delegates or per-call operator
/// arrays — so a hot `$((…))` allocates essentially nothing. Two-char operators are
/// matched before the single-char ones that share a prefix (e.g. `&&` vs `&`, `<<`
/// vs `<`); the single-char bitwise ops explicitly decline when a second identical
/// char follows, so they don't swallow the logical operators.
/// </summary>
public static class ArithParser
	{
	public static long Evaluate(string expr)
		{
		var p = new ArithParserState(expr);
		long result = p.ParseExpr();
		p.ExpectEnd();
		return result;
		}

	private sealed class ArithParserState
		{
		private readonly string _s;
		private int _pos;

		public ArithParserState(string s) { _s = s.Trim(); }

		public void ExpectEnd()
			{
			SkipWs();
			if (_pos < _s.Length)
				throw new EvalException($"Unexpected character '{_s[_pos]}' in arithmetic expression");
			}

		// ── expression grammar (lowest precedence first) ──────────────────────────

		public long ParseExpr() => ParseTernary();

		private long ParseTernary()
			{
			var val = ParseOr();
			SkipWs();
			if (TryConsume('?'))
				{
				var t = ParseExpr();
				SkipWs(); Consume(':');
				var f = ParseExpr();
				return val != 0 ? t : f;
				}
			return val;
			}

		private long ParseOr()
			{
			long left = ParseAnd();
			while (true) { SkipWs(); if (!Match2('|', '|')) break; long r = ParseAnd(); left = (left != 0 || r != 0) ? 1L : 0L; }
			return left;
			}

		private long ParseAnd()
			{
			long left = ParseBitOr();
			while (true) { SkipWs(); if (!Match2('&', '&')) break; long r = ParseBitOr(); left = (left != 0 && r != 0) ? 1L : 0L; }
			return left;
			}

		// Single-char | but not || (which belongs to ParseOr, a lower level).
		private long ParseBitOr()
			{
			long left = ParseBitXor();
			while (true) { SkipWs(); if (!(At('|') && !Ahead('|'))) break; _pos++; left |= ParseBitXor(); }
			return left;
			}

		private long ParseBitXor()
			{
			long left = ParseBitAnd();
			while (true) { SkipWs(); if (!At('^')) break; _pos++; left ^= ParseBitAnd(); }
			return left;
			}

		// Single-char & but not && (which belongs to ParseAnd).
		private long ParseBitAnd()
			{
			long left = ParseEquality();
			while (true) { SkipWs(); if (!(At('&') && !Ahead('&'))) break; _pos++; left &= ParseEquality(); }
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
				if      (At('+')) { _pos++; left += ParseMulDiv(); }
				else if (At('-')) { _pos++; left -= ParseMulDiv(); }
				else break;
				}
			return left;
			}

		// Single-char * but not ** (power, a deeper level).
		private long ParseMulDiv()
			{
			long left = ParsePower();
			while (true)
				{
				SkipWs();
				if (At('*') && !Ahead('*')) { _pos++; left *= ParsePower(); }
				else if (At('/')) { _pos++; long r = ParsePower(); left = r == 0 ? throw new EvalException("Division by zero") : left / r; }
				else if (At('%')) { _pos++; long r = ParsePower(); left = r == 0 ? throw new EvalException("Modulo by zero")   : left % r; }
				else break;
				}
			return left;
			}

		// Right-associative power.
		private long ParsePower()
			{
			long b = ParseUnary();
			SkipWs();
			if (Match2('*', '*')) return (long)System.Math.Pow(b, ParsePower());
			return b;
			}

		private long ParseUnary()
			{
			SkipWs();
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
				var v = ParseExpr();
				SkipWs(); Consume(')');
				return v;
				}
			return ParseNumber();
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

			// decimal, or octal when a leading 0 is followed by more digits
			if (char.IsAsciiDigit(c))
				{
				int start = _pos;
				while (_pos < _s.Length && char.IsAsciiDigit(_s[_pos])) _pos++;
				int @base = (_pos - start > 1 && _s[start] == '0') ? 8 : 10;
				long v = 0;
				for (int i = start; i < _pos; i++)
					{
					int d = _s[i] - '0';
					if (d >= @base) throw new EvalException($"Invalid octal digit '{_s[i]}' in arithmetic expression");
					v = v * @base + d;
					}
				return v;
				}

			// bare identifier → 0
			if (char.IsLetter(c) || c == '_')
				{
				while (_pos < _s.Length && (char.IsLetterOrDigit(_s[_pos]) || _s[_pos] == '_')) _pos++;
				return 0L;
				}

			throw new EvalException($"Unexpected '{c}' in arithmetic expression");
			}

		// ── helpers ───────────────────────────────────────────────────────────────

		private static int HexVal(char c) => c <= '9' ? c - '0' : (char.ToLowerInvariant(c) - 'a' + 10);

		private void SkipWs() { while (_pos < _s.Length && _s[_pos] is ' ' or '\t') _pos++; }

		private bool At(char c)    => _pos < _s.Length && _s[_pos] == c;
		private bool Ahead(char c) => _pos + 1 < _s.Length && _s[_pos + 1] == c;

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
