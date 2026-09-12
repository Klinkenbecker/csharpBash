using System.Globalization;
using System.Text;

namespace Bash.Evaluator;

/// <summary>
/// bash `printf` semantics: %[flags][width][.precision]conv with conv in
/// d i u o x X f F e E g G s c b q %, `*` width/precision from the argument list,
/// backslash escapes in the format (and in %b arguments), and format reuse — the
/// format is applied again while arguments remain, provided it consumed at least one.
/// </summary>
public static class PrintfFormatter
	{
	/// <summary>Format <paramref name="args"/> with <paramref name="fmt"/>. Returns the exit
	/// status (1 if any argument was not a valid number) and the produced text; warnings go
	/// to <paramref name="err"/>.</summary>
	public static int Format(string fmt, IReadOnlyList<string> args, TextWriter err, out string text)
		{
		var sb = new StringBuilder();
		int ai = 0, rc = 0;
		while (true)
			{
			int before = ai;
			bool hasConv = FormatOnce(fmt, args, ref ai, sb, err, ref rc, out bool stop);
			if (stop) break;
			if (!hasConv || ai >= args.Count || ai == before) break;
			}
		text = sb.ToString();
		return rc;
		}

	private static bool FormatOnce(string fmt, IReadOnlyList<string> args, ref int ai, StringBuilder sb,
		TextWriter err, ref int rc, out bool stop)
		{
		stop = false;
		bool hasConv = false;
		int i = 0;
		while (i < fmt.Length)
			{
			char c = fmt[i];
			if (c == '\\')
				{
				i++;
				if (i >= fmt.Length) { sb.Append('\\'); break; }
				i = AppendEscape(fmt, i, sb, out bool halt);
				if (halt) { stop = true; return hasConv; }
				continue;
				}
			if (c != '%') { sb.Append(c); i++; continue; }
			i++;
			if (i >= fmt.Length) { sb.Append('%'); break; }
			if (fmt[i] == '%') { sb.Append('%'); i++; continue; }

			// flags
			bool left = false, plus = false, space = false, alt = false, zero = false;
			while (i < fmt.Length && "-+ #0'".IndexOf(fmt[i]) >= 0)
				{
				switch (fmt[i]) { case '-': left = true; break; case '+': plus = true; break; case ' ': space = true; break; case '#': alt = true; break; case '0': zero = true; break; }
				i++;
				}
			// width
			int width = -1;
			if (i < fmt.Length && fmt[i] == '*')
				{ i++; width = (int)ToLong(NextArg(args, ref ai), err, ref rc); if (width < 0) { left = true; width = -width; } }
			else
				{ int s = i; while (i < fmt.Length && char.IsAsciiDigit(fmt[i])) i++; if (i > s) width = int.Parse(fmt.AsSpan(s, i - s)); }
			// precision
			int prec = -1;
			if (i < fmt.Length && fmt[i] == '.')
				{
				i++;
				if (i < fmt.Length && fmt[i] == '*') { i++; prec = (int)ToLong(NextArg(args, ref ai), err, ref rc); }
				else { int s = i; while (i < fmt.Length && char.IsAsciiDigit(fmt[i])) i++; prec = i > s ? int.Parse(fmt.AsSpan(s, i - s)) : 0; }
				}
			// length modifiers (ignored; note `q` is a conversion, not a modifier)
			while (i < fmt.Length && "hlLjzt".IndexOf(fmt[i]) >= 0) i++;
			if (i >= fmt.Length) { sb.Append('%'); break; }
			char conv = fmt[i++];
			hasConv = true;

			string body;
			switch (conv)
				{
				case 'd': case 'i':
					{
					long v = ToLong(NextArg(args, ref ai), err, ref rc);
					body = FormatInteger(v, plus, space, prec);
					break;
					}
				case 'u':
					{
					long v = ToLong(NextArg(args, ref ai), err, ref rc);
					body = ((ulong)v).ToString(CultureInfo.InvariantCulture);
					if (prec >= 0) body = body.PadLeft(prec, '0');
					break;
					}
				case 'o':
					{
					long v = ToLong(NextArg(args, ref ai), err, ref rc);
					body = Convert.ToString(v, 8);
					if (prec >= 0) body = body.PadLeft(prec, '0');
					if (alt && !body.StartsWith('0')) body = "0" + body;
					break;
					}
				case 'x': case 'X':
					{
					long v = ToLong(NextArg(args, ref ai), err, ref rc);
					body = v.ToString(conv == 'x' ? "x" : "X", CultureInfo.InvariantCulture);
					if (prec >= 0) body = body.PadLeft(prec, '0');
					if (alt && v != 0) body = (conv == 'x' ? "0x" : "0X") + body;
					break;
					}
				case 'f': case 'F': case 'e': case 'E': case 'g': case 'G': case 'a': case 'A':
					{
					double d = ToDouble(NextArg(args, ref ai), err, ref rc);
					body = FormatDouble(d, conv, prec, plus, space, alt);
					break;
					}
				case 'c':
					{
					var a = NextArg(args, ref ai);
					body = a.Length > 0 ? a[..1] : "";
					break;
					}
				case 's':
					{
					var a = NextArg(args, ref ai);
					body = prec >= 0 && prec < a.Length ? a[..prec] : a;
					break;
					}
				case 'b':
					{
					var a = NextArg(args, ref ai);
					var tmp = new StringBuilder();
					int k = 0;
					bool halt = false;
					while (k < a.Length)
						{
						if (a[k] == '\\' && k + 1 < a.Length) { k = AppendEscape(a, k + 1, tmp, out halt, inArg: true); if (halt) break; }
						else tmp.Append(a[k++]);
						}
					body = tmp.ToString();
					if (prec >= 0 && prec < body.Length) body = body[..prec];
					if (halt) { sb.Append(Pad(body, width, left, false)); stop = true; return hasConv; }
					break;
					}
				case 'q':
					{
					body = ShellQuote(NextArg(args, ref ai));
					break;
					}
				default:
					err.WriteLine($"printf: `{conv}': invalid format character");
					rc = 1;
					body = "";
					break;
				}
			bool zeroPad = zero && !left && conv is 'd' or 'i' or 'u' or 'o' or 'x' or 'X' or 'f' or 'F' or 'e' or 'E' or 'g' or 'G';
			sb.Append(Pad(body, width, left, zeroPad));
			}
		return hasConv;
		}

	private static string Pad(string body, int width, bool left, bool zeroPad)
		{
		if (width < 0 || body.Length >= width) return body;
		if (left) return body.PadRight(width);
		if (zeroPad)
			{
			int signLen = body.Length > 0 && (body[0] is '-' or '+' or ' ') ? 1 : 0;
			return body[..signLen] + body[signLen..].PadLeft(width - signLen, '0');
			}
		return body.PadLeft(width);
		}

	private static string FormatInteger(long v, bool plus, bool space, int prec)
		{
		var digits = Math.Abs(v).ToString(CultureInfo.InvariantCulture);
		if (v == long.MinValue) digits = "9223372036854775808";
		if (prec >= 0) digits = digits.PadLeft(prec, '0');
		string sign = v < 0 ? "-" : plus ? "+" : space ? " " : "";
		return sign + digits;
		}

	private static string FormatDouble(double d, char conv, int prec, bool plus, bool space, bool alt)
		{
		if (prec < 0) prec = 6;
		string s;
		switch (conv)
			{
			case 'f': case 'F': s = d.ToString("F" + prec, CultureInfo.InvariantCulture); break;
			case 'e': case 'E':
				s = d.ToString((conv == 'e' ? "e" : "E") + prec, CultureInfo.InvariantCulture);
				// .NET emits e+006; C emits e+06
				s = System.Text.RegularExpressions.Regex.Replace(s, @"([eE][+-])0*(\d\d)$", "$1$2");
				break;
			case 'g': case 'G':
				{
				int p = prec == 0 ? 1 : prec;
				if (d == 0) { s = "0"; break; }
				int exp = (int)Math.Floor(Math.Log10(Math.Abs(d)));
				if (exp < -4 || exp >= p)
					{
					s = d.ToString((conv == 'g' ? "e" : "E") + (p - 1), CultureInfo.InvariantCulture);
					s = System.Text.RegularExpressions.Regex.Replace(s, @"([eE][+-])0*(\d\d)$", "$1$2");
					if (!alt)
						{
						int e = s.IndexOfAny(['e', 'E']);
						string mant = s[..e], rest = s[e..];
						if (mant.Contains('.')) mant = mant.TrimEnd('0').TrimEnd('.');
						s = mant + rest;
						}
					}
				else
					{
					s = d.ToString("F" + Math.Max(0, p - 1 - exp), CultureInfo.InvariantCulture);
					if (!alt && s.Contains('.')) s = s.TrimEnd('0').TrimEnd('.');
					}
				break;
				}
			default: s = d.ToString("R", CultureInfo.InvariantCulture); break;
			}
		if (d >= 0 && !s.StartsWith('-')) { if (plus) s = "+" + s; else if (space) s = " " + s; }
		return s;
		}

	private static string NextArg(IReadOnlyList<string> args, ref int ai) => ai < args.Count ? args[ai++] : "";

	/// <summary>bash numeric argument rules: leading 'c or "c gives the character code;
	/// otherwise a C-style integer (0x hex, leading-0 octal). Invalid → warning, 0, rc=1.</summary>
	private static long ToLong(string a, TextWriter err, ref int rc)
		{
		if (a.Length == 0) return 0;
		if ((a[0] == '\'' || a[0] == '"') && a.Length >= 2) return a[1];
		var t = a.Trim();
		bool neg = false;
		if (t.StartsWith('-')) { neg = true; t = t[1..]; } else if (t.StartsWith('+')) t = t[1..];
		long v; bool ok;
		if (t.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
			ok = long.TryParse(t.AsSpan(2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out v);
		else if (t.Length > 1 && t[0] == '0' && t.All(char.IsAsciiDigit))
			{ ok = true; v = 0; foreach (var ch in t) { if (ch > '7') { ok = false; break; } v = v * 8 + (ch - '0'); } }
		else
			ok = long.TryParse(t, NumberStyles.Integer, CultureInfo.InvariantCulture, out v);
		if (!ok)
			{
			// bash prints the leading numeric prefix's value and warns
			int k = 0; while (k < t.Length && char.IsAsciiDigit(t[k])) k++;
			long.TryParse(k > 0 ? t[..k] : "0", out v);
			err.WriteLine($"printf: {a}: invalid number");
			rc = 1;
			}
		return neg ? -v : v;
		}

	private static double ToDouble(string a, TextWriter err, ref int rc)
		{
		if (a.Length == 0) return 0;
		if ((a[0] == '\'' || a[0] == '"') && a.Length >= 2) return a[1];
		if (double.TryParse(a.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var d)) return d;
		err.WriteLine($"printf: {a}: invalid number");
		rc = 1;
		return 0;
		}

	/// <summary>Decode one backslash escape starting at <paramref name="i"/> (the char after
	/// the backslash). Returns the index after the escape. `\c` sets <paramref name="halt"/>.</summary>
	public static int AppendEscape(string s, int i, StringBuilder sb, out bool halt, bool inArg = false)
		{
		halt = false;
		char e = s[i];
		switch (e)
			{
			case 'a': sb.Append('\a'); return i + 1;
			case 'b': sb.Append('\b'); return i + 1;
			case 'e': case 'E': sb.Append('\x1b'); return i + 1;
			case 'f': sb.Append('\f'); return i + 1;
			case 'n': sb.Append('\n'); return i + 1;
			case 'r': sb.Append('\r'); return i + 1;
			case 't': sb.Append('\t'); return i + 1;
			case 'v': sb.Append('\v'); return i + 1;
			case '\\': sb.Append('\\'); return i + 1;
			case '"': sb.Append('"'); return i + 1;
			case '\'': sb.Append('\''); return i + 1;
			case '?': sb.Append('?'); return i + 1;
			case 'c': halt = true; return i + 1;
			case 'x':
				{
				int k = i + 1, v = 0, n = 0;
				while (k < s.Length && n < 2 && Uri.IsHexDigit(s[k])) { v = v * 16 + Convert.ToInt32(s[k].ToString(), 16); k++; n++; }
				if (n == 0) { sb.Append("\\x"); return i + 1; }
				// \xNN is a BYTE escape in bash, not a code point: 0xFF means the byte 0xFF, not
				// U+00FF (which would encode as two bytes). ShellEncoding carries it intact.
				sb.Append(ShellEncoding.ByteChar(v)); return k;
				}
			case 'u': case 'U':
				{
				int max = e == 'u' ? 4 : 8, k = i + 1, v = 0, n = 0;
				while (k < s.Length && n < max && Uri.IsHexDigit(s[k])) { v = v * 16 + Convert.ToInt32(s[k].ToString(), 16); k++; n++; }
				if (n == 0) { sb.Append('\\').Append(e); return i + 1; }
				sb.Append(char.ConvertFromUtf32(v)); return k;
				}
			default:
				if (e >= '0' && e <= '7')
					{
					// \NNN in the format (up to 3 digits); \0NNN in %b arguments (0 then up to 3)
					int k = i, v = 0, n = 0, max = 3;
					if (inArg) { if (e != '0') { sb.Append('\\').Append(e); return i + 1; } k++; }
					while (k < s.Length && n < max && s[k] >= '0' && s[k] <= '7') { v = v * 8 + (s[k] - '0'); k++; n++; }
					sb.Append(ShellEncoding.ByteChar(v)); return k;   // \NNN is a byte escape too
					}
				sb.Append('\\').Append(e); return i + 1;
			}
		}

	/// <summary>%q: quote a string so it can be reused as shell input.</summary>
	public static string ShellQuote(string s)
		{
		if (s.Length == 0) return "''";
		bool control = s.Any(ch => ch < ' ' || ch == '\x7f');
		if (control)
			{
			var sb = new StringBuilder("$'");
			foreach (var ch in s)
				{
				switch (ch)
					{
					case '\n': sb.Append("\\n"); break;
					case '\t': sb.Append("\\t"); break;
					case '\r': sb.Append("\\r"); break;
					case '\a': sb.Append("\\a"); break;
					case '\b': sb.Append("\\b"); break;
					case '\f': sb.Append("\\f"); break;
					case '\v': sb.Append("\\v"); break;
					case '\x1b': sb.Append("\\E"); break;
					case '\\': sb.Append("\\\\"); break;
					case '\'': sb.Append("\\'"); break;
					default:
						if (ch < ' ' || ch == '\x7f') sb.Append("\\x").Append(((int)ch).ToString("x2"));
						else sb.Append(ch);
						break;
					}
				}
			return sb.Append('\'').ToString();
			}
		const string specials = "|&;<>()$`\\\"' \t*?[]#~=%{}!^,";
		var o = new StringBuilder();
		foreach (var ch in s)
			{
			if (specials.IndexOf(ch) >= 0) o.Append('\\');
			o.Append(ch);
			}
		return o.ToString();
		}
	}
