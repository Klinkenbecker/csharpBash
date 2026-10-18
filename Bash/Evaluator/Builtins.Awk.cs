using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Bash.Evaluator;

/// <summary>
/// In-process `awk` implementing the subset ratified in DECISIONS 2026-09-04 #7 and extended
/// 2026-10-10 (user functions, `print > file`, `getline [var] < file`, `close`, `fflush`,
/// 3-argument `match`, `--version`); the supported lists there are the contract. Anything outside
/// it raises an unsupported-construct error that the dispatcher handles per decision #2: fall
/// through to a PATH awk when one exists, else `awk: unsupported: …` exit 2. Never a silent
/// approximation.
/// </summary>
public sealed partial class Builtins
	{
	private int Awk(List<string> args)
		{
		if (args.Count > 0 && args[0] == "--version")
			{
			// never "GNU Awk": a script that greps for it must not take this for gawk
			Console.WriteLine("awk (C#Bash in-process subset, gawk-5.1 compatible) " + BuildInfo.Stamp);
			return 0;
			}
		var o = Opts.Parse("awk", args, "F:v:f:", ["field-separator=:F", "assign=:v", "file=:f"], stopAtFirstOperand: true);
		var operands = new List<string>(o.Operands);
		string program;
		var progFiles = o.All('f').ToList();
		if (progFiles.Count > 0)
			{
			var sb = new StringBuilder();
			foreach (var pf in progFiles)
				{
				try { sb.Append(pf is "-" or "/dev/stdin" ? Console.In.ReadToEnd() : ShellEncoding.ReadAllText(ShellEnvironment.TranslatePath(pf))).Append('\n'); }
				catch (Exception ex) { Console.Error.WriteLine($"awk: {pf}: {IoError(ex)}"); return 2; }
				}
			program = sb.ToString();
			}
		else
			{
			if (operands.Count == 0) { Console.Error.WriteLine("Usage: awk [-F fs][-v var=value][prog | -f progfile][file ...]"); return 2; }
			program = operands[0];
			operands.RemoveAt(0);
			}
		// gawk's location prefix: "cmd. line:N:" for a program operand, "FILE:N:" for -f
		var source = progFiles.Count > 0 ? progFiles[0] : "cmd. line";
		try
			{
			var interp = new AwkInterp { Source = source };
			foreach (var a in o.All('v'))
				{
				if (!interp.AssignOperand(a)) { Console.Error.WriteLine($"awk: invalid -v argument '{a}'"); return 2; }
				}
			if (o.Get('F') is string fs) interp.SetVar("FS", AwkVal.Str(fs == "t" ? "\t" : AwkInterp.Unescape(fs)));
			return interp.Run(program, operands);
			}
		catch (AwkFatalException ex)
			{
			// an error gawk also reports (and exits 2 for): not a subset gap, so no fall-through
			try { Console.Out.Flush(); } catch { }
			Console.Error.WriteLine($"awk: {source}:{ex.Line}: fatal: {ex.Message}");
			return 2;
			}
		catch (AwkUnsupportedException ex) { throw new UnsupportedOptionException("awk", "unsupported", ex.Message); }
		catch (AwkSyntaxException ex)
			{
			// a construct this subset does not parse: same policy as an unsupported one
			throw new UnsupportedOptionException("awk", "unsupported", "syntax: " + ex.Message);
			}
		}

	private sealed class AwkUnsupportedException(string what) : Exception(what);
	private sealed class AwkSyntaxException(string what) : Exception(what);
	/// <summary>An error gawk reports as fatal (exit 2) for the same program: an undefined function,
	/// an output file that cannot be opened. Printed with gawk's location prefix.</summary>
	private sealed class AwkFatalException(int line, string what) : Exception(what) { public int Line { get; } = line; }
	private sealed class AwkNextException : Exception;
	private sealed class AwkBreakException : Exception;
	private sealed class AwkContinueException : Exception;
	private sealed class AwkExitException(int code) : Exception { public int Code { get; } = code; }
	/// <summary>A main-input file that cannot be opened: gawk's fatal, exit 2.</summary>
	private sealed class AwkOpenException(string message) : Exception(message);

	// ═══════════════════════════════════════════════════════════════════════════
	// values
	// ═══════════════════════════════════════════════════════════════════════════

	private sealed class AwkVal
		{
		public enum K { Num, Str, StrNum, Uninit }
		public readonly K Kind;
		private readonly string? _s;
		private readonly double _n;
		private AwkVal(string? s, double n, K k) { _s = s; _n = n; Kind = k; }
		public static readonly AwkVal Uninit = new(null, 0, K.Uninit);
		public static readonly AwkVal Zero = new(null, 0, K.Num);
		public static readonly AwkVal One = new(null, 1, K.Num);
		public static AwkVal Num(double n) => new(null, n, K.Num);
		public static AwkVal Str(string s) => new(s, 0, K.Str);
		public static AwkVal Bool(bool b) => b ? One : Zero;
		/// <summary>Input-derived text: numeric-looking text compares numerically ("strnum").</summary>
		public static AwkVal StrNum(string s) => LooksNumeric(s, out var n) ? new(s, n, K.StrNum) : new(s, 0, K.Str);
		public double Number => Kind switch { K.Num or K.StrNum => _n, K.Str => NumericPrefix(_s!), _ => 0 };
		public string Text(string convfmt) => Kind switch { K.Num => NumToString(_n, convfmt), K.Uninit => "", _ => _s! };
		public bool NumericForCompare => Kind != K.Str;
		public bool Truth => Kind switch { K.Num or K.StrNum => _n != 0, K.Str => _s!.Length > 0, _ => false };

		public static bool LooksNumeric(string s, out double n)
			{
			var t = s.Trim(' ', '\t', '\n', '\r', '\f', '\v');
			n = 0;
			if (t.Length == 0) return false;
			if (!Regex.IsMatch(t, @"^[+-]?(\d+\.?\d*([eE][+-]?\d+)?|\.\d+([eE][+-]?\d+)?)$")) return false;
			return double.TryParse(t, NumberStyles.Float, CultureInfo.InvariantCulture, out n);
			}

		/// <summary>strtod-style: the longest numeric prefix, else 0.</summary>
		public static double NumericPrefix(string s)
			{
			var m = Regex.Match(s, @"^\s*[+-]?(\d+\.?\d*([eE][+-]?\d+)?|\.\d+([eE][+-]?\d+)?)");
			return m.Success && double.TryParse(m.Value.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var n) ? n : 0;
			}

		public static string NumToString(double n, string convfmt)
			{
			if (double.IsNaN(n)) return "nan";
			if (double.IsInfinity(n)) return n > 0 ? "inf" : "-inf";
			// integral values print as integers at any magnitude (gawk-verified: 1e30 prints all digits)
			if (n == Math.Floor(n)) return Math.Abs(n) < 9e18 ? ((long)n).ToString(CultureInfo.InvariantCulture) : n.ToString("F0", CultureInfo.InvariantCulture);
			var g = Regex.Match(convfmt, @"^%\.(\d+)g$");
			if (g.Success) return FormatG(n, int.Parse(g.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture));
			return AwkFormat(convfmt, [Num(n)], "%.6g");
			}
		}

	// ═══════════════════════════════════════════════════════════════════════════
	// printf / number formatting (C semantics for the supported conversions)
	// ═══════════════════════════════════════════════════════════════════════════

	private static string AwkFormat(string fmt, List<AwkVal> args, string convfmt)
		{
		var sb = new StringBuilder();
		int ai = 0;
		AwkVal Next() => ai < args.Count ? args[ai++] : AwkVal.Uninit;
		for (int i = 0; i < fmt.Length; i++)
			{
			char c = fmt[i];
			if (c != '%') { sb.Append(c); continue; }
			if (i + 1 >= fmt.Length) { sb.Append('%'); break; }
			if (fmt[i + 1] == '%') { sb.Append('%'); i++; continue; }
			int j = i + 1;
			bool left = false, plus = false, space = false, zero = false, alt = false;
			while (j < fmt.Length && "-+ 0#".IndexOf(fmt[j]) >= 0)
				{
				switch (fmt[j]) { case '-': left = true; break; case '+': plus = true; break; case ' ': space = true; break; case '0': zero = true; break; case '#': alt = true; break; }
				j++;
				}
			int width = 0; bool hasWidth = false;
			if (j < fmt.Length && fmt[j] == '*') { width = (int)Next().Number; hasWidth = true; if (width < 0) { left = true; width = -width; } j++; }
			else while (j < fmt.Length && char.IsAsciiDigit(fmt[j])) { width = width * 10 + (fmt[j] - '0'); hasWidth = true; j++; }
			int prec = -1;
			if (j < fmt.Length && fmt[j] == '.')
				{
				j++; prec = 0;
				if (j < fmt.Length && fmt[j] == '*') { prec = (int)Next().Number; j++; }
				else while (j < fmt.Length && char.IsAsciiDigit(fmt[j])) { prec = prec * 10 + (fmt[j] - '0'); j++; }
				}
			while (j < fmt.Length && "hlLqjzt".IndexOf(fmt[j]) >= 0) j++;   // length modifiers are ignored
			if (j >= fmt.Length) { sb.Append(fmt, i, fmt.Length - i); break; }
			char conv = fmt[j];
			string body;
			switch (conv)
				{
				case 'd' or 'i':
					{
					double d = Next().Number;
					if (double.IsNaN(d) || double.IsInfinity(d)) d = 0;
					d = Math.Truncate(d);
					var digits = Math.Abs(d) < 9e18 ? Math.Abs((long)d).ToString(CultureInfo.InvariantCulture) : Math.Abs(d).ToString("F0", CultureInfo.InvariantCulture);
					if (prec >= 0) { digits = digits.PadLeft(prec, '0'); if (prec == 0 && d == 0) digits = ""; }
					string sign = d < 0 ? "-" : plus ? "+" : space ? " " : "";
					body = PadNumber(sign, digits, width, left, zero && prec < 0);
					break;
					}
				case 'o' or 'x' or 'X' or 'u':
					{
					double d = Next().Number;
					ulong v = d < 0 ? unchecked((ulong)(long)Math.Truncate(d)) : (ulong)Math.Truncate(d);
					var digits = conv switch { 'o' => Convert.ToString((long)v, 8), 'x' => v.ToString("x"), 'X' => v.ToString("X"), _ => v.ToString(CultureInfo.InvariantCulture) };
					if (prec >= 0) { digits = digits.PadLeft(prec, '0'); if (prec == 0 && v == 0) digits = ""; }
					string prefix = alt && v != 0 ? conv switch { 'o' => "0", 'x' => "0x", 'X' => "0X", _ => "" } : "";
					body = PadNumber(prefix, digits, width, left, zero && prec < 0);
					break;
					}
				case 'f' or 'F':
					{
					double d = Next().Number;
					var digits = Math.Abs(d).ToString("F" + (prec < 0 ? 6 : prec), CultureInfo.InvariantCulture);
					if (alt && prec == 0) digits += ".";
					string sign = d < 0 || (d == 0 && double.IsNegative(d)) ? "-" : plus ? "+" : space ? " " : "";
					body = PadNumber(sign, digits, width, left, zero);
					break;
					}
				case 'e' or 'E' or 'g' or 'G':
					throw new AwkUnsupportedException($"printf %{conv}");
				case 'c':
					{
					var v = Next();
					string ch = v.Kind == AwkVal.K.Num ? char.ConvertFromUtf32((int)Math.Max(0, Math.Min(0x10FFFF, v.Number))) : v.Text(convfmt).Length > 0 ? v.Text(convfmt)[..1] : "";
					body = left ? ch.PadRight(width) : ch.PadLeft(width);
					break;
					}
				case 's':
					{
					var s = Next().Text(convfmt);
					if (prec >= 0 && prec < s.Length) s = s[..prec];
					body = left ? s.PadRight(width) : s.PadLeft(width);
					break;
					}
				default:
					throw new AwkUnsupportedException($"printf %{conv}");
				}
			sb.Append(body);
			i = j;
			}
		return sb.ToString();
		}

	private static string PadNumber(string sign, string digits, int width, bool left, bool zeroPad)
		{
		int len = sign.Length + digits.Length;
		if (len >= width) return sign + digits;
		if (left) return (sign + digits).PadRight(width);
		if (zeroPad) return sign + digits.PadLeft(width - sign.Length, '0');
		return (sign + digits).PadLeft(width);
		}

	/// <summary>C `%.Ng` for OFMT/CONVFMT ("%.6g" by default): the only place awk needs it internally.</summary>
	private static string FormatG(double v, int prec)
		{
		if (prec == 0) prec = 1;
		if (v == 0) return "0";
		if (double.IsNaN(v)) return "nan";
		if (double.IsInfinity(v)) return v > 0 ? "inf" : "-inf";
		var sci = Math.Abs(v).ToString("E" + (prec - 1), CultureInfo.InvariantCulture);   // d.dddE+ddd
		int ePos = sci.IndexOf('E');
		int exp = int.Parse(sci[(ePos + 1)..], CultureInfo.InvariantCulture);
		string sign = v < 0 ? "-" : "";
		if (exp < -4 || exp >= prec)
			{
			var mant = sci[..ePos];
			if (mant.Contains('.')) mant = mant.TrimEnd('0').TrimEnd('.');
			return $"{sign}{mant}e{(exp < 0 ? "-" : "+")}{Math.Abs(exp):D2}";
			}
		var fixedText = Math.Abs(v).ToString("F" + Math.Max(0, prec - 1 - exp), CultureInfo.InvariantCulture);
		if (fixedText.Contains('.')) fixedText = fixedText.TrimEnd('0').TrimEnd('.');
		return sign + fixedText;
		}

	// ═══════════════════════════════════════════════════════════════════════════
	// lexer
	// ═══════════════════════════════════════════════════════════════════════════

	private enum AT { Number, String, Regex, Name, FuncName, Builtin, Keyword, Op, Newline, Eof }

	private sealed record AwkTok(AT Type, string Text, double Num, int Line);

	private static readonly HashSet<string> AwkKeywords =
		["BEGIN", "END", "if", "else", "while", "do", "for", "in", "next", "exit", "break", "continue", "delete", "print", "printf", "getline", "function", "func", "return", "nextfile"];
	private static readonly HashSet<string> AwkBuiltinFuncs =
		["length", "substr", "index", "split", "sub", "gsub", "match", "tolower", "toupper", "sprintf", "int",
		 "system", "close", "fflush", "sqrt", "exp", "log", "sin", "cos", "atan2", "rand", "srand", "systime", "strftime", "gensub", "asort", "asorti"];

	private static List<AwkTok> AwkLex(string src)
		{
		var toks = new List<AwkTok>();
		int i = 0, line = 1;
		bool RegexAllowed()
			{
			if (toks.Count == 0) return true;
			var p = toks[^1];
			return p.Type switch
				{
				AT.Number or AT.String or AT.Regex or AT.Name or AT.Builtin => false,
				AT.Op => p.Text is not (")" or "]" or "$" or "++" or "--"),
				AT.Keyword => p.Text is not ("getline"),
				_ => true,
				};
			}
		while (i < src.Length)
			{
			char c = src[i];
			if (c == '\\' && i + 1 < src.Length && src[i + 1] == '\n') { i += 2; line++; continue; }
			if (c == '\\' && i + 2 < src.Length && src[i + 1] == '\r' && src[i + 2] == '\n') { i += 3; line++; continue; }
			if (c == '\n') { toks.Add(new(AT.Newline, "\n", 0, line)); line++; i++; continue; }
			if (c is ' ' or '\t' or '\r') { i++; continue; }
			if (c == '#') { while (i < src.Length && src[i] != '\n') i++; continue; }
			if (char.IsAsciiDigit(c) || (c == '.' && i + 1 < src.Length && char.IsAsciiDigit(src[i + 1])))
				{
				int s = i;
				if (c == '0' && i + 1 < src.Length && (src[i + 1] is 'x' or 'X'))
					{
					i += 2; while (i < src.Length && char.IsAsciiHexDigit(src[i])) i++;
					toks.Add(new(AT.Number, src[s..i], Convert.ToInt64(src[(s + 2)..i], 16), line));
					continue;
					}
				while (i < src.Length && char.IsAsciiDigit(src[i])) i++;
				if (i < src.Length && src[i] == '.') { i++; while (i < src.Length && char.IsAsciiDigit(src[i])) i++; }
				if (i < src.Length && (src[i] is 'e' or 'E'))
					{
					int save = i; i++;
					if (i < src.Length && (src[i] is '+' or '-')) i++;
					if (i < src.Length && char.IsAsciiDigit(src[i])) { while (i < src.Length && char.IsAsciiDigit(src[i])) i++; }
					else i = save;
					}
				toks.Add(new(AT.Number, src[s..i], double.Parse(src[s..i], NumberStyles.Float, CultureInfo.InvariantCulture), line));
				continue;
				}
			if (char.IsLetter(c) || c == '_')
				{
				int s = i;
				while (i < src.Length && (char.IsLetterOrDigit(src[i]) || src[i] == '_')) i++;
				var word = src[s..i];
				if (AwkKeywords.Contains(word)) toks.Add(new(AT.Keyword, word, 0, line));
				else if (AwkBuiltinFuncs.Contains(word)) toks.Add(new(AT.Builtin, word, 0, line));
				else if (i < src.Length && src[i] == '(') toks.Add(new(AT.FuncName, word, 0, line));
				else toks.Add(new(AT.Name, word, 0, line));
				continue;
				}
			if (c == '"')
				{
				i++;
				var sb = new StringBuilder();
				while (i < src.Length && src[i] != '"')
					{
					if (src[i] == '\\' && i + 1 < src.Length)
						{
						i++;
						switch (src[i])
							{
							case 'n': sb.Append('\n'); i++; break;
							case 't': sb.Append('\t'); i++; break;
							case 'r': sb.Append('\r'); i++; break;
							case 'a': sb.Append('\a'); i++; break;
							case 'b': sb.Append('\b'); i++; break;
							case 'f': sb.Append('\f'); i++; break;
							case 'v': sb.Append('\v'); i++; break;
							case '\\': sb.Append('\\'); i++; break;
							case '"': sb.Append('"'); i++; break;
							case '/': sb.Append('/'); i++; break;
							case '\n': i++; line++; break;
							case >= '0' and <= '7':
								{
								int v = 0, n = 0;
								while (n < 3 && i < src.Length && src[i] >= '0' && src[i] <= '7') { v = v * 8 + (src[i] - '0'); i++; n++; }
								sb.Append(ShellEncoding.ByteChar(v & 0xFF)); break;   // a byte, as in gawk ("\377" is ff)
								}
							case 'x':
								{
								i++; int v = 0, n = 0;
								while (n < 2 && i < src.Length && char.IsAsciiHexDigit(src[i])) { v = v * 16 + Convert.ToInt32(src[i].ToString(), 16); i++; n++; }
								if (n == 0) sb.Append("\\x"); else sb.Append(ShellEncoding.ByteChar(v));
								break;
								}
							default: sb.Append('\\').Append(src[i]); i++; break;   // gawk keeps unknown escapes
							}
						continue;
						}
					if (src[i] == '\n') throw new AwkSyntaxException($"unterminated string at line {line}");
					sb.Append(src[i++]);
					}
				if (i >= src.Length) throw new AwkSyntaxException($"unterminated string at line {line}");
				i++;
				// escaped bytes that spell a UTF-8 character are that character: "\xc3\xa9" is é, length 1
				toks.Add(new(AT.String, ShellEncoding.Canonical(sb.ToString()), 0, line));
				continue;
				}
			if (c == '/' && RegexAllowed())
				{
				i++;
				var sb = new StringBuilder();
				bool inBracket = false;
				while (i < src.Length && (src[i] != '/' || inBracket))
					{
					if (src[i] == '\n') throw new AwkSyntaxException($"unterminated regexp at line {line}");
					if (src[i] == '\\' && i + 1 < src.Length)
						{
						if (src[i + 1] == '/') { sb.Append('/'); i += 2; continue; }
						sb.Append(src[i]).Append(src[i + 1]); i += 2; continue;
						}
					if (src[i] == '[' && !inBracket)
						{
						inBracket = true; sb.Append(src[i++]);
						if (i < src.Length && src[i] == '^') sb.Append(src[i++]);
						if (i < src.Length && src[i] == ']') sb.Append(src[i++]);
						continue;
						}
					if (src[i] == ']' && inBracket) inBracket = false;
					sb.Append(src[i++]);
					}
				if (i >= src.Length) throw new AwkSyntaxException($"unterminated regexp at line {line}");
				i++;
				toks.Add(new(AT.Regex, sb.ToString(), 0, line));
				continue;
				}
			string[] ops2 = ["+=", "-=", "*=", "/=", "%=", "^=", "==", "!=", "<=", ">=", "&&", "||", "++", "--", "!~", ">>", "**"];
			string? op = null;
			if (string.CompareOrdinal(src, i, "**=", 0, 3) == 0) op = "**=";
			if (op is null) foreach (var o2 in ops2) if (string.CompareOrdinal(src, i, o2, 0, 2) == 0) { op = o2; break; }
			if (op is null)
				{
				if ("{}()[];,+-*/%^!><|?:~$=".IndexOf(c) >= 0) op = c.ToString();
				else throw new AwkSyntaxException($"unexpected character '{c}' at line {line}");
				}
			int consumed = op.Length;
			if (op == "**") op = "^"; else if (op == "**=") op = "^=";
			toks.Add(new(AT.Op, op, 0, line));
			i += consumed;
			}
		toks.Add(new(AT.Eof, "", 0, line));
		return toks;
		}

	// ═══════════════════════════════════════════════════════════════════════════
	// AST
	// ═══════════════════════════════════════════════════════════════════════════

	private abstract record AExpr;
	private sealed record ANum(double V) : AExpr;
	private sealed record AStr(string V) : AExpr;
	private sealed record ARegex(string Pattern) : AExpr;
	private sealed record AVar(string Name) : AExpr;
	private sealed record AField(AExpr Index) : AExpr;
	private sealed record AIndex(string Name, List<AExpr> Subs) : AExpr;
	private sealed record AAssign(AExpr Target, string Op, AExpr Value) : AExpr;
	private sealed record ACond(AExpr C, AExpr A, AExpr B) : AExpr;
	private sealed record ABinary(string Op, AExpr A, AExpr B) : AExpr;
	private sealed record AUnary(string Op, AExpr A) : AExpr;
	private sealed record AIncDec(AExpr Target, bool Prefix, int Delta) : AExpr;
	private sealed record AConcat(AExpr A, AExpr B) : AExpr;
	private sealed record AMatch(AExpr Subject, AExpr Pattern, bool Negate) : AExpr;
	private sealed record AIn(List<AExpr> Keys, string Array) : AExpr;
	private sealed record ACall(string Name, List<AExpr> Args, int Line = 0) : AExpr;
	private sealed record AUserCall(string Name, List<AExpr> Args, int Line) : AExpr;
	/// <summary>`getline [Target] [&lt; File]`; Target null sets $0 and NF; File null reads the MAIN input (and counts NR,
	/// FNR). `cmd | getline` is not in the subset.</summary>
	private sealed record AGetline(AExpr? Target, AExpr? File) : AExpr;
	private sealed record AGroupList(List<AExpr> Items) : AExpr;   // (a, b) — only valid as print's argument list

	private abstract record AStmt;
	private sealed record ABlock(List<AStmt> Body) : AStmt;
	private sealed record AExprStmt(AExpr E) : AStmt;
	/// <summary>Target null: stdout. Otherwise `> Target` (`>> Target` when Append): a file, or
	/// "/dev/stdout", "/dev/stderr", "-" and the other device names.</summary>
	private sealed record APrint(List<AExpr> Args, bool Formatted, AExpr? Target, bool Append, int Line) : AStmt;
	private sealed record AReturn(AExpr? Value) : AStmt;
	private sealed record AIf(AExpr C, AStmt Then, AStmt? Else) : AStmt;
	private sealed record AWhile(AExpr C, AStmt Body) : AStmt;
	private sealed record ADoWhile(AStmt Body, AExpr C) : AStmt;
	private sealed record AFor(AStmt? Init, AExpr? C, AStmt? Step, AStmt Body) : AStmt;
	private sealed record AForIn(string Var, string Array, AStmt Body) : AStmt;
	private sealed record ANext : AStmt;
	private sealed record AExit(AExpr? Code) : AStmt;
	private sealed record ABreak : AStmt;
	private sealed record AContinue : AStmt;
	private sealed record ADelete(string Array, List<AExpr>? Subs) : AStmt;

	private sealed record ARule(AExpr? Pattern, AExpr? PatternEnd, AStmt? Action);
	private sealed record AFunc(string Name, List<string> Params, ABlock Body);
	private sealed record AProgram(List<AStmt> Begin, List<ARule> Rules, List<AStmt> End, Dictionary<string, AFunc> Funcs);

	/// <summary>Names a function parameter may not take (gawk: "cannot use special variable").</summary>
	private static readonly HashSet<string> AwkSpecialVars =
		["NF", "NR", "FNR", "FS", "OFS", "ORS", "RS", "SUBSEP", "CONVFMT", "OFMT", "FILENAME", "RSTART", "RLENGTH",
		 "ENVIRON", "ARGC", "ARGV", "RT", "IGNORECASE", "BINMODE", "ERRNO", "FIELDWIDTHS", "FPAT", "LINT",
		 "PROCINFO", "TEXTDOMAIN", "SYMTAB", "FUNCTAB"];

	// ═══════════════════════════════════════════════════════════════════════════
	// parser
	// ═══════════════════════════════════════════════════════════════════════════

	private sealed class AwkParser(List<AwkTok> toks)
		{
		private int _p;
		private bool _noGt;   // inside print's argument list: `>` is a redirection, not a comparison
		private bool _noIn;   // inside for(...) header: `in` belongs to for-in
		private bool _inFunc;   // `return` is legal
		private int _loops;     // `break` / `continue` are legal when > 0 (gawk rejects them outside a loop)
		private readonly List<AUserCall> _calls = [];   // checked against the definitions after parsing
		private readonly HashSet<string> _names = new(StringComparer.Ordinal);   // variables and arrays used

		private AwkTok Peek => toks[_p];
		private AwkTok Advance() => toks[_p++];
		private bool IsOp(string s) => Peek.Type == AT.Op && Peek.Text == s;
		private bool IsKw(string s) => Peek.Type == AT.Keyword && Peek.Text == s;
		private void Expect(string op)
			{
			if (!IsOp(op)) throw new AwkSyntaxException($"expected '{op}' near '{Peek.Text}' at line {Peek.Line}");
			_p++;
			}
		private void SkipNl() { while (Peek.Type == AT.Newline) _p++; }
		private void SkipTerms() { while (Peek.Type == AT.Newline || IsOp(";")) _p++; }

		public AProgram ParseProgram()
			{
			var begin = new List<AStmt>(); var end = new List<AStmt>(); var rules = new List<ARule>();
			var funcs = new Dictionary<string, AFunc>(StringComparer.Ordinal);
			SkipTerms();
			while (Peek.Type != AT.Eof)
				{
				if (IsKw("BEGIN")) { _p++; SkipNl(); begin.Add(ParseBlock()); }
				else if (IsKw("END")) { _p++; SkipNl(); end.Add(ParseBlock()); }
				else if (IsKw("function") || IsKw("func")) ParseFunction(funcs);
				else if (IsOp("{")) rules.Add(new ARule(null, null, ParseBlock()));
				else
					{
					var pat = ParseExpr();
					AExpr? pat2 = null;
					if (IsOp(",")) { _p++; SkipNl(); pat2 = ParseExpr(); }
					AStmt? action = IsOp("{") ? ParseBlock() : null;
					rules.Add(new ARule(pat, pat2, action));
					}
				SkipTerms();
				}
			// gawk's checks, made once the whole program is read (a call may precede its definition)
			foreach (var name in _names)
				if (funcs.ContainsKey(name))
					throw new AwkSyntaxException($"function `{name}' called with space between name and `(', or used as a variable or an array");
			foreach (var call in _calls)
				if (!funcs.ContainsKey(call.Name)) throw new AwkFatalException(call.Line, $"function `{call.Name}' not defined");
			return new AProgram(begin, rules, end, funcs);
			}

		/// <summary>`function NAME(params) { body }` (`func` too): parameters beyond the arguments a
		/// call passes are its locals.</summary>
		private void ParseFunction(Dictionary<string, AFunc> funcs)
			{
			_p++;
			if (Peek.Type is not (AT.Name or AT.FuncName)) throw new AwkSyntaxException($"expected a function name near '{Peek.Text}' at line {Peek.Line}");
			var name = Advance().Text;
			if (funcs.ContainsKey(name)) throw new AwkSyntaxException($"function name `{name}' previously defined");
			Expect("(");
			var ps = new List<string>();
			SkipNl();
			while (!IsOp(")"))
				{
				if (Peek.Type != AT.Name) throw new AwkSyntaxException($"expected a parameter name near '{Peek.Text}' at line {Peek.Line}");
				var pn = Advance().Text;
				if (AwkSpecialVars.Contains(pn)) throw new AwkSyntaxException($"function `{name}': cannot use special variable `{pn}' as a function parameter");
				if (pn == name || ps.Contains(pn)) throw new AwkSyntaxException($"function `{name}': parameter `{pn}' is not unique");
				ps.Add(pn);
				SkipNl();
				if (IsOp(",")) { _p++; SkipNl(); continue; }
				if (!IsOp(")")) throw new AwkSyntaxException($"expected ',' or ')' near '{Peek.Text}' at line {Peek.Line}");
				}
			_p++;
			SkipNl();
			bool saveFunc = _inFunc; int saveLoops = _loops;
			_inFunc = true; _loops = 0;
			try { funcs[name] = new AFunc(name, ps, ParseBlock()); }
			finally { _inFunc = saveFunc; _loops = saveLoops; }
			}

		/// <summary>A loop body: `break` and `continue` are legal inside it.</summary>
		private AStmt ParseLoopBody()
			{
			_loops++;
			try { return ParseStmt(); }
			finally { _loops--; }
			}

		private ABlock ParseBlock()
			{
			Expect("{");
			var body = new List<AStmt>();
			SkipTerms();
			while (!IsOp("}"))
				{
				if (Peek.Type == AT.Eof) throw new AwkSyntaxException("unexpected end of program (missing '}')");
				body.Add(ParseStmt());
				SkipTerms();
				}
			_p++;
			return new ABlock(body);
			}

		private void EndSimple()
			{
			// a simple statement ends at ';', newline, '}' or EOF
			if (IsOp(";") || Peek.Type == AT.Newline) { _p++; return; }
			if (IsOp("}") || Peek.Type == AT.Eof) return;
			if (IsOp("|") && _p + 1 < toks.Count && toks[_p + 1] is { Type: AT.Keyword, Text: "getline" }) throw new AwkUnsupportedException("getline");
			if (IsOp("|")) throw new AwkUnsupportedException("output piped to a command");
			throw new AwkSyntaxException($"unexpected '{Peek.Text}' at line {Peek.Line}");
			}

		private AStmt ParseStmt()
			{
			if (IsOp("{")) return ParseBlock();
			if (IsOp(";")) { _p++; return new ABlock([]); }
			if (Peek.Type == AT.Keyword)
				{
				switch (Peek.Text)
					{
					case "if":
						{
						_p++; Expect("("); var c = ParseExpr(); Expect(")"); SkipNl();
						var then = ParseStmt();
						int save = _p; SkipTerms();
						if (IsKw("else")) { _p++; SkipNl(); return new AIf(c, then, ParseStmt()); }
						_p = save;
						return new AIf(c, then, null);
						}
					case "while":
						{
						_p++; Expect("("); var c = ParseExpr(); Expect(")");
						if (IsOp(";")) { _p++; return new AWhile(c, new ABlock([])); }
						SkipNl();
						return new AWhile(c, ParseLoopBody());
						}
					case "do":
						{
						_p++; SkipNl(); var body = ParseLoopBody(); SkipTerms();
						if (!IsKw("while")) throw new AwkSyntaxException("expected 'while' after do-body");
						_p++; Expect("("); var c = ParseExpr(); Expect(")"); EndSimple();
						return new ADoWhile(body, c);
						}
					case "for":
						{
						_p++; Expect("(");
						if (_p + 3 < toks.Count && Peek.Type == AT.Name && toks[_p + 1] is { Type: AT.Keyword, Text: "in" } && toks[_p + 2].Type == AT.Name && toks[_p + 3] is { Type: AT.Op, Text: ")" })
							{
							var v = Advance().Text; _p++; var arr = Advance().Text; _p++; SkipNl();
							_names.Add(v); _names.Add(arr);
							return new AForIn(v, arr, ParseLoopBody());
							}
						AStmt? init = IsOp(";") ? null : new AExprStmt(ParseExpr()); Expect(";"); SkipNl();
						AExpr? cond = IsOp(";") ? null : ParseExpr(); Expect(";"); SkipNl();
						AStmt? step = IsOp(")") ? null : new AExprStmt(ParseExpr()); Expect(")");
						if (IsOp(";")) { _p++; return new AFor(init, cond, step, new ABlock([])); }
						SkipNl();
						return new AFor(init, cond, step, ParseLoopBody());
						}
					case "next": _p++; EndSimple(); return new ANext();
					case "nextfile": throw new AwkUnsupportedException("nextfile");
					case "break" or "continue":
						{
						var kw = Advance();
						if (_loops == 0) throw new AwkSyntaxException($"`{kw.Text}' is not allowed outside a loop at line {kw.Line}");
						EndSimple();
						return kw.Text == "break" ? new ABreak() : new AContinue();
						}
					case "return":
						{
						var kw = Advance();
						if (!_inFunc) throw new AwkSyntaxException($"`return' used outside function context at line {kw.Line}");
						AExpr? value = IsOp(";") || IsOp("}") || Peek.Type is AT.Newline or AT.Eof ? null : ParseExpr();
						EndSimple();
						return new AReturn(value);
						}
					case "exit":
						{
						_p++;
						AExpr? code = IsOp(";") || IsOp("}") || Peek.Type is AT.Newline or AT.Eof ? null : ParseExpr();
						EndSimple();
						return new AExit(code);
						}
					case "delete":
						{
						_p++;
						if (Peek.Type != AT.Name) throw new AwkSyntaxException("expected array name after delete");
						var name = Advance().Text;
						_names.Add(name);
						List<AExpr>? subs = null;
						if (IsOp("[")) { _p++; subs = ParseExprList("]"); Expect("]"); }
						EndSimple();
						return new ADelete(name, subs);
						}
					case "print" or "printf":
						{
						var kwTok = Advance();
						bool formatted = kwTok.Text == "printf";
						var args = new List<AExpr>();
						bool saveGt = _noGt; _noGt = true;
						try
							{
							if (!(IsOp(";") || IsOp("}") || IsOp(">") || IsOp(">>") || IsOp("|") || Peek.Type is AT.Newline or AT.Eof))
								{
								var first = ParseExpr();
								if (first is AGroupList gl && (IsOp(";") || IsOp("}") || IsOp(">") || IsOp(">>") || IsOp("|") || Peek.Type is AT.Newline or AT.Eof)) args.AddRange(gl.Items);
								else
									{
									args.Add(first);
									while (IsOp(",")) { _p++; SkipNl(); args.Add(ParseExpr()); }
									}
								}
							}
						finally { _noGt = saveGt; }
						AExpr? target = null;
						bool append = false;
						if (IsOp(">") || IsOp(">>"))
							{
							append = Advance().Text == ">>";
							// a concatenation, as in gawk: `print > "out" n ".txt"` names one file
							target = ParseConcat();
							}
						if (IsOp("|")) throw new AwkUnsupportedException("print piped to a command");
						EndSimple();
						if (formatted && args.Count == 0) throw new AwkSyntaxException("printf: no format");
						return new APrint(args, formatted, target, append, kwTok.Line);
						}
					}
				}
			var e = ParseExpr();
			EndSimple();
			return new AExprStmt(e);
			}

		private List<AExpr> ParseExprList(string closer)
			{
			var list = new List<AExpr>();
			if (IsOp(closer)) return list;
			list.Add(ParseExpr());
			while (IsOp(",")) { _p++; SkipNl(); list.Add(ParseExpr()); }
			return list;
			}

		// ── expressions (lowest precedence first) ──────────────────────────────

		public AExpr ParseExpr() => ParseTernary();

		private static bool IsLvalue(AExpr e) => e is AVar or AField or AIndex;

		private AExpr ParseTernary()
			{
			var c = ParseOr();
			if (IsOp("?"))
				{
				_p++; SkipNl(); var a = ParseTernary(); SkipNl(); Expect(":"); SkipNl(); var b = ParseTernary();
				return new ACond(c, a, b);
				}
			if (Peek.Type == AT.Op && Peek.Text is "=" or "+=" or "-=" or "*=" or "/=" or "%=" or "^=" && IsLvalue(c))
				{
				var op = Advance().Text; SkipNl();
				var v = ParseTernary();
				return new AAssign(c, op, v);
				}
			return c;
			}

		private AExpr ParseOr()
			{
			var a = ParseAnd();
			while (IsOp("||")) { _p++; SkipNl(); a = new ABinary("||", a, ParseAnd()); }
			return a;
			}

		private AExpr ParseAnd()
			{
			var a = ParseIn();
			while (IsOp("&&")) { _p++; SkipNl(); a = new ABinary("&&", a, ParseIn()); }
			return a;
			}

		private AExpr ParseIn()
			{
			var a = ParseMatch();
			while (!_noIn && IsKw("in"))
				{
				_p++;
				if (Peek.Type != AT.Name) throw new AwkSyntaxException("expected array name after 'in'");
				var arr = Advance().Text;
				_names.Add(arr);
				a = new AIn(a is AGroupList gl ? gl.Items : [a], arr);
				}
			return a;
			}

		private AExpr ParseMatch()
			{
			var a = ParseRelational();
			while (IsOp("~") || IsOp("!~"))
				{
				bool neg = Advance().Text == "!~";
				a = new AMatch(a, ParseRelational(), neg);
				}
			return a;
			}

		private AExpr ParseRelational()
			{
			var a = ParseConcat();
			if (Peek.Type == AT.Op && Peek.Text is "<" or "<=" or "!=" or "==" or ">=" or ">")
				{
				if (Peek.Text == ">" && _noGt) return a;
				var op = Advance().Text;
				return new ABinary(op, a, ParseConcat());
				}
			return a;
			}

		private bool StartsConcatOperand()
			{
			var t = Peek;
			return t.Type switch
				{
				AT.Number or AT.String or AT.Regex or AT.Name or AT.FuncName or AT.Builtin => true,
				AT.Op => t.Text is "$" or "(",
				AT.Keyword => false,
				_ => false,
				};
			}

		private AExpr ParseConcat()
			{
			var a = ParseAdditive();
			while (StartsConcatOperand() && !(IsKw("in")))
				a = new AConcat(a, ParseAdditive());
			return a;
			}

		private AExpr ParseAdditive()
			{
			var a = ParseMultiplicative();
			while (IsOp("+") || IsOp("-")) { var op = Advance().Text; a = new ABinary(op, a, ParseMultiplicative()); }
			return a;
			}

		private AExpr ParseMultiplicative()
			{
			var a = ParseUnary();
			while (IsOp("*") || IsOp("/") || IsOp("%")) { var op = Advance().Text; a = new ABinary(op, a, ParseUnary()); }
			return a;
			}

		private AExpr ParseUnary()
			{
			if (IsOp("!")) { _p++; return new AUnary("!", ParseUnary()); }
			if (IsOp("-")) { _p++; return new AUnary("-", ParseUnary()); }
			if (IsOp("+")) { _p++; return new AUnary("+", ParseUnary()); }
			return ParsePower();
			}

		private AExpr ParsePower()
			{
			var a = ParsePostfix();
			if (IsOp("^")) { _p++; var b = ParseUnaryForPower(); return new ABinary("^", a, b); }
			return a;
			}

		private AExpr ParseUnaryForPower()
			{
			// right-associative, and `2^-1` is legal
			if (IsOp("-")) { _p++; return new AUnary("-", ParseUnaryForPower()); }
			if (IsOp("+")) { _p++; return ParseUnaryForPower(); }
			if (IsOp("!")) { _p++; return new AUnary("!", ParseUnaryForPower()); }
			return ParsePower();
			}

		private AExpr ParsePostfix()
			{
			if (IsOp("++") || IsOp("--"))
				{
				int d = Advance().Text == "++" ? 1 : -1;
				var t = ParsePostfix();
				if (!IsLvalue(t)) throw new AwkSyntaxException("++/-- needs a variable");
				return new AIncDec(t, true, d);
				}
			var e = ParsePrimary();
			if ((IsOp("++") || IsOp("--")) && IsLvalue(e))
				{
				int d = Advance().Text == "++" ? 1 : -1;
				return new AIncDec(e, false, d);
				}
			return e;
			}

		private AExpr ParsePrimary()
			{
			var t = Peek;
			switch (t.Type)
				{
				case AT.Number: _p++; return new ANum(t.Num);
				case AT.String: _p++; return new AStr(t.Text);
				case AT.Regex: _p++; return new ARegex(t.Text);
				case AT.Op when t.Text == "$":
					{
					_p++;
					// `$` binds tighter than everything but grouping/++/--: $NF, $(i+1), $i++ means ($i)++
					AExpr idx;
					if (IsOp("++") || IsOp("--")) { int d = Advance().Text == "++" ? 1 : -1; var tt = ParsePrimary(); idx = new AIncDec(tt, true, d); }
					else if (IsOp("-")) { _p++; idx = new AUnary("-", ParsePrimary()); }
					else idx = ParsePrimary();
					return new AField(idx);
					}
				case AT.Op when t.Text == "(":
					{
					_p++;
					bool saveGt = _noGt, saveIn = _noIn; _noGt = false; _noIn = false;
					SkipNl();
					var first = ParseExpr();
					if (IsOp(","))
						{
						var items = new List<AExpr> { first };
						while (IsOp(",")) { _p++; SkipNl(); items.Add(ParseExpr()); }
						SkipNl(); Expect(")");
						_noGt = saveGt; _noIn = saveIn;
						if (IsKw("in")) { _p++; if (Peek.Type != AT.Name) throw new AwkSyntaxException("expected array name after 'in'"); var arr = Advance().Text; _names.Add(arr); return new AIn(items, arr); }
						return new AGroupList(items);
						}
					SkipNl(); Expect(")");
					_noGt = saveGt; _noIn = saveIn;
					return first;
					}
				case AT.Op when t.Text == "!":
					_p++; return new AUnary("!", ParseUnary());
				case AT.Op when t.Text == "-":
					_p++; return new AUnary("-", ParseUnary());
				case AT.Op when t.Text == "+":
					_p++; return new AUnary("+", ParseUnary());
				case AT.FuncName:
					{
					_p += 2;   // the name and its `(` (the lexer makes a FuncName only of a name followed by one)
					bool saveGt = _noGt, saveIn = _noIn; _noGt = false; _noIn = false;
					SkipNl();
					var args = ParseExprList(")");
					SkipNl(); Expect(")");
					_noGt = saveGt; _noIn = saveIn;
					var call = new AUserCall(t.Text, args, t.Line);
					_calls.Add(call);
					return call;
					}
				case AT.Builtin:
					{
					_p++;
					if (t.Text is "system" or "systime" or "strftime" or "gensub" or "asort" or "asorti")
						throw new AwkUnsupportedException($"{t.Text}()");
					if (t.Text is "sqrt" or "exp" or "log" or "sin" or "cos" or "atan2" or "rand" or "srand")
						throw new AwkUnsupportedException($"{t.Text}()");
					if (!IsOp("("))
						{
						if (t.Text == "length") return new ACall("length", []);
						throw new AwkSyntaxException($"{t.Text} needs ( )");
						}
					_p++;
					bool saveGt = _noGt, saveIn = _noIn; _noGt = false; _noIn = false;
					SkipNl();
					var args = ParseExprList(")");
					SkipNl(); Expect(")");
					_noGt = saveGt; _noIn = saveIn;
					return new ACall(t.Text, args, t.Line);
					}
				case AT.Name:
					{
					_p++;
					if (t.Text == "ENVIRON") throw new AwkUnsupportedException("ENVIRON");
					_names.Add(t.Text);
					if (IsOp("["))
						{
						_p++;
						bool saveGt = _noGt, saveIn = _noIn; _noGt = false; _noIn = false;
						var subs = ParseExprList("]");
						Expect("]");
						_noGt = saveGt; _noIn = saveIn;
						return new AIndex(t.Text, subs);
						}
					return new AVar(t.Text);
					}
				case AT.Keyword when t.Text == "getline":
					{
					_p++;
					AExpr? target = null;
					if (Peek.Type == AT.Name || IsOp("$"))
						{
						target = ParsePrimary();
						if (!IsLvalue(target)) throw new AwkSyntaxException($"getline: not a variable at line {t.Line}");
						}
					// `getline` / `getline var` read the main input (until 2026-10-12 they were outside the subset, so
					// the program went to the PATH awk: Git's gawk, which strips every CR in text mode; msp430-a2)
					if (!IsOp("<")) return new AGetline(target, null);
					_p++;
					// gawk's simp_exp: arithmetic but no concatenation, so `getline < "a" "b"` reads "a"
					return new AGetline(target, ParseAdditive());
					}
				default:
					throw new AwkSyntaxException($"unexpected '{(t.Type == AT.Newline ? "newline" : t.Type == AT.Eof ? "end of program" : t.Text)}' at line {t.Line}");
				}
			}
		}

	// ═══════════════════════════════════════════════════════════════════════════
	// interpreter
	// ═══════════════════════════════════════════════════════════════════════════

	private sealed class AwkInterp
		{
		private readonly Dictionary<string, AwkVal> _vars = new(StringComparer.Ordinal);
		private readonly Dictionary<string, Dictionary<string, AwkVal>> _arrays = new(StringComparer.Ordinal);
		private readonly Dictionary<string, Regex> _regexCache = new(StringComparer.Ordinal);
		private readonly List<string> _fields = [];   // 1-based logical; _fields[0] is $1
		private string _record = "";
		private bool _fieldsValid = true;
		private int _nr, _fnr;
		private int _exitCode;
		private bool _exiting;
		private Dictionary<string, AFunc> _funcs = new(StringComparer.Ordinal);
		private Dictionary<string, AwkCell>? _frame;   // the running function's parameters and locals; null outside one
		private bool _returning;                        // a `return` ran: every enclosing statement stops (no exception per call)
		private AwkVal _returnValue = AwkVal.Uninit;
		private readonly Dictionary<string, TextWriter> _outputs = new(StringComparer.Ordinal);   // `print > name`, open until close()
		private readonly Dictionary<string, (TextReader Reader, IEnumerator<string> Lines)> _inputs = new(StringComparer.Ordinal);   // `getline < name`
		public string Source { get; init; } = "cmd. line";

		/// <summary>A function's parameter or local. A scalar argument arrives by value; an array by
		/// reference; an untyped caller variable arrives untyped, and if the function uses it as an
		/// array, that array becomes the caller's variable too (BindCaller), as in gawk.</summary>
		private sealed class AwkCell
			{
			public AwkVal Value = AwkVal.Uninit;
			public bool IsScalar;
			public Dictionary<string, AwkVal>? Array;
			public Action<Dictionary<string, AwkVal>>? BindCaller;
			}

		public AwkInterp()
			{
			SetVar("FS", AwkVal.Str(" "));
			SetVar("OFS", AwkVal.Str(" "));
			SetVar("ORS", AwkVal.Str("\n"));
			SetVar("RS", AwkVal.Str("\n"));
			SetVar("SUBSEP", AwkVal.Str("\x1c"));
			SetVar("CONVFMT", AwkVal.Str("%.6g"));
			SetVar("OFMT", AwkVal.Str("%.6g"));
			SetVar("FILENAME", AwkVal.Str(""));
			SetVar("RSTART", AwkVal.Zero);
			SetVar("RLENGTH", AwkVal.Num(-1));
			}

		public void SetVar(string name, AwkVal v) => _vars[name] = v;
		private string Convfmt => _vars["CONVFMT"].Text("%.6g");
		private string S(AwkVal v) => v.Text(Convfmt);

		public static string Unescape(string s)
			{
			if (!s.Contains('\\')) return s;
			var sb = new StringBuilder();
			for (int i = 0; i < s.Length; i++)
				{
				if (s[i] != '\\' || i + 1 >= s.Length) { sb.Append(s[i]); continue; }
				i++;
				switch (s[i])
					{
					case 'n': sb.Append('\n'); break; case 't': sb.Append('\t'); break; case 'r': sb.Append('\r'); break;
					case 'a': sb.Append('\a'); break; case 'b': sb.Append('\b'); break; case 'f': sb.Append('\f'); break;
					case 'v': sb.Append('\v'); break; case '\\': sb.Append('\\'); break; case '"': sb.Append('"'); break;
					case '/': sb.Append('/'); break;
					case >= '0' and <= '7':
						{
						// octal and \x make bytes, as in a string literal (gawk: -v s='\303\251' is é)
						int v = 0, n = 0;
						while (n < 3 && i < s.Length && s[i] >= '0' && s[i] <= '7') { v = v * 8 + (s[i] - '0'); i++; n++; }
						i--;   // the for loop steps past the last digit
						sb.Append(ShellEncoding.ByteChar(v & 0xFF));
						break;
						}
					case 'x':
						{
						int v = 0, n = 0, j = i + 1;
						while (n < 2 && j < s.Length && char.IsAsciiHexDigit(s[j])) { v = v * 16 + Convert.ToInt32(s[j].ToString(), 16); j++; n++; }
						if (n == 0) sb.Append("\\x");
						else { sb.Append(ShellEncoding.ByteChar(v)); i = j - 1; }
						break;
						}
					default: sb.Append('\\').Append(s[i]); break;
					}
				}
			return ShellEncoding.Canonical(sb.ToString());
			}

		/// <summary>`name=value` from -v or a file operand: escapes processed, strnum typed.</summary>
		public bool AssignOperand(string a)
			{
			int eq = a.IndexOf('=');
			if (eq <= 0) return false;
			var name = a[..eq];
			if (!Regex.IsMatch(name, @"^[A-Za-z_][A-Za-z0-9_]*$")) return false;
			SetSpecialOrVar(name, AwkVal.StrNum(Unescape(a[(eq + 1)..])));
			return true;
			}

		// ── records and fields ─────────────────────────────────────────────────

		private void SetRecord(string line)
			{
			_record = line;
			_fieldsValid = false;
			}

		private void EnsureFields()
			{
			if (_fieldsValid) return;
			_fields.Clear();
			_fields.AddRange(SplitFields(_record, S(_vars["FS"])));
			_fieldsValid = true;
			}

		private List<string> SplitFields(string s, string fs)
			{
			if (fs == " ")
				return [.. s.Split([' ', '\t', '\n'], StringSplitOptions.RemoveEmptyEntries)];
			if (s.Length == 0) return [];
			// a single character (other than space) is a literal separator, metacharacter or not:
			// `-F.` and `-F'|'` split on the character itself (gawk-verified)
			if (fs.Length == 1) return [.. s.Split(fs[0])];
			var re = GetRegex(fs);
			return [.. re.Split(s)];
			}

		private int NF { get { EnsureFields(); return _fields.Count; } }

		private AwkVal GetField(int n)
			{
			if (n == 0) return AwkVal.StrNum(_record);
			if (n < 0) throw new AwkSyntaxException($"attempt to access field {n}");
			EnsureFields();
			return n <= _fields.Count ? AwkVal.StrNum(_fields[n - 1]) : AwkVal.Uninit;
			}

		private void SetField(int n, string v)
			{
			if (n == 0) { SetRecord(v); return; }
			if (n < 0) throw new AwkSyntaxException($"attempt to access field {n}");
			EnsureFields();
			while (_fields.Count < n) _fields.Add("");
			_fields[n - 1] = v;
			RebuildRecord();
			}

		private void SetNF(int n)
			{
			EnsureFields();
			if (n < 0) n = 0;
			while (_fields.Count < n) _fields.Add("");
			if (_fields.Count > n) _fields.RemoveRange(n, _fields.Count - n);
			RebuildRecord();
			}

		private void RebuildRecord()
			{
			_record = string.Join(S(_vars["OFS"]), _fields);
			_fieldsValid = true;
			}

		// ── variables ──────────────────────────────────────────────────────────

		private AwkVal GetVar(string name)
			{
			switch (name)
				{
				case "NF": return AwkVal.Num(NF);
				case "NR": return AwkVal.Num(_nr);
				case "FNR": return AwkVal.Num(_fnr);
				}
			if (Local(name) is { } cell)
				{
				if (cell.Array is not null) throw new AwkSyntaxException($"attempt to use array {name} in a scalar context");
				return cell.Value;
				}
			if (_arrays.ContainsKey(name)) throw new AwkSyntaxException($"attempt to use array {name} in a scalar context");
			return _vars.TryGetValue(name, out var v) ? v : AwkVal.Uninit;
			}

		private AwkCell? Local(string name) => _frame is not null && _frame.TryGetValue(name, out var c) ? c : null;

		/// <summary>The array a name denotes now, without creating one.</summary>
		private bool TryArray(string name, out Dictionary<string, AwkVal> arr)
			{
			if (Local(name) is { } cell) { arr = cell.Array!; return arr is not null; }
			return _arrays.TryGetValue(name, out arr!);
			}

		private void SetSpecialOrVar(string name, AwkVal v)
			{
			switch (name)
				{
				case "NF": SetNF((int)v.Number); return;
				case "NR": _nr = (int)v.Number; return;
				case "FNR": _fnr = (int)v.Number; return;
				case "RS":
					if (S(v) != "\n") throw new AwkUnsupportedException("RS other than newline");
					break;
				}
			if (Local(name) is { } cell)
				{
				if (cell.Array is not null) throw new AwkSyntaxException($"attempt to use array {name} in a scalar context");
				cell.Value = v;
				cell.IsScalar = true;
				return;
				}
			if (_arrays.ContainsKey(name)) throw new AwkSyntaxException($"attempt to use array {name} in a scalar context");
			_vars[name] = v;
			}

		private Dictionary<string, AwkVal> GetArray(string name)
			{
			if (Local(name) is { } cell)
				{
				if (cell.Array is null)
					{
					if (cell.IsScalar) throw new AwkSyntaxException($"attempt to use scalar {name} as an array");
					cell.Array = new Dictionary<string, AwkVal>(StringComparer.Ordinal);
					cell.BindCaller?.Invoke(cell.Array);
					}
				return cell.Array;
				}
			if (!_arrays.TryGetValue(name, out var a))
				{
				if (_vars.ContainsKey(name)) throw new AwkSyntaxException($"attempt to use scalar {name} as an array");
				a = new Dictionary<string, AwkVal>(StringComparer.Ordinal);
				_arrays[name] = a;
				}
			return a;
			}

		private string SubscriptKey(List<AExpr> subs) =>
			subs.Count == 1 ? S(Eval(subs[0])) : string.Join(S(_vars["SUBSEP"]), subs.Select(e => S(Eval(e))));

		private Regex GetRegex(string pattern)
			{
			if (_regexCache.TryGetValue(pattern, out var re)) return re;
			// ExplicitCapture: awk has no back-references, and Regex.Split must not emit groups
			try { re = new Regex(EreToNet(pattern), RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture); }
			catch (ArgumentException ex) { throw new AwkSyntaxException($"invalid regular expression /{pattern}/: {ex.Message}"); }
			_regexCache[pattern] = re;
			return re;
			}

		private Regex RegexOf(AExpr e) => e is ARegex r ? GetRegex(r.Pattern) : GetRegex(S(Eval(e)));

		/// <summary>The pattern with its groups numbered (POSIX order, by opening parenthesis), for
		/// 3-argument match(); cached apart from the ExplicitCapture ones every other use takes.</summary>
		private Regex CapturingRegexOf(AExpr e)
			{
			var pattern = e is ARegex r ? r.Pattern : S(Eval(e));
			if (_captureCache.TryGetValue(pattern, out var re)) return re;
			try { re = new Regex(EreToNet(pattern), RegexOptions.CultureInvariant); }
			catch (ArgumentException ex) { throw new AwkSyntaxException($"invalid regular expression /{pattern}/: {ex.Message}"); }
			_captureCache[pattern] = re;
			return re;
			}
		private readonly Dictionary<string, Regex> _captureCache = new(StringComparer.Ordinal);

		// ── evaluation ─────────────────────────────────────────────────────────

		private AwkVal Eval(AExpr e)
			{
			switch (e)
				{
				case ANum n: return AwkVal.Num(n.V);
				case AStr s: return AwkVal.Str(s.V);
				case ARegex r: return AwkVal.Bool(GetRegex(r.Pattern).IsMatch(_record));
				case AVar v: return GetVar(v.Name);
				case AField f: return GetField((int)Eval(f.Index).Number);
				case AIndex ix:
					{
					var arr = GetArray(ix.Name);
					var key = SubscriptKey(ix.Subs);
					if (!arr.TryGetValue(key, out var v)) { v = AwkVal.Uninit; arr[key] = v; }   // reference creates the element
					return v;
					}
				case AGroupList:
					throw new AwkSyntaxException("unexpected expression list");
				case AAssign a:
					{
					var rhs = Eval(a.Value);
					AwkVal result;
					if (a.Op == "=") result = rhs;
					else
						{
						double l = Eval(a.Target).Number, r = rhs.Number;
						result = AwkVal.Num(a.Op switch
							{
							"+=" => l + r, "-=" => l - r, "*=" => l * r, "/=" => Div(l, r), "%=" => Mod(l, r), "^=" => Math.Pow(l, r), _ => r,
							});
						}
					Store(a.Target, result);
					return result;
					}
				case ACond c: return Eval(c.C).Truth ? Eval(c.A) : Eval(c.B);
				case ABinary b:
					{
					switch (b.Op)
						{
						case "&&": return AwkVal.Bool(Eval(b.A).Truth && Eval(b.B).Truth);
						case "||": return AwkVal.Bool(Eval(b.A).Truth || Eval(b.B).Truth);
						case "<" or "<=" or "==" or "!=" or ">" or ">=":
							{
							int cmp = Compare(Eval(b.A), Eval(b.B));
							return AwkVal.Bool(b.Op switch { "<" => cmp < 0, "<=" => cmp <= 0, "==" => cmp == 0, "!=" => cmp != 0, ">" => cmp > 0, _ => cmp >= 0 });
							}
						}
					double x = Eval(b.A).Number, y = Eval(b.B).Number;
					return AwkVal.Num(b.Op switch
						{
						"+" => x + y, "-" => x - y, "*" => x * y, "/" => Div(x, y), "%" => Mod(x, y), "^" => Math.Pow(x, y),
						_ => throw new AwkSyntaxException($"operator {b.Op}"),
						});
					}
				case AUnary u:
					{
					var v = Eval(u.A);
					return u.Op switch { "!" => AwkVal.Bool(!v.Truth), "-" => AwkVal.Num(-v.Number), _ => AwkVal.Num(v.Number) };
					}
				case AIncDec d:
					{
					double old = Eval(d.Target).Number;
					var nv = AwkVal.Num(old + d.Delta);
					Store(d.Target, nv);
					return d.Prefix ? nv : AwkVal.Num(old);
					}
				case AConcat c:
					{
					var l = S(Eval(c.A));
					var r = S(Eval(c.B));
					var s = l + r;
					// halves of a UTF-8 character carried as bytes join into it ("\xc3" "\xa9" is é, as in
					// gawk); each side is already canonical, so only a seam of two carried bytes can change
					if (l.Length > 0 && r.Length > 0 && ShellEncoding.IsByteChar(l[^1]) && ShellEncoding.IsByteChar(r[0]))
						s = ShellEncoding.Canonical(s);
					return AwkVal.Str(s);
					}
				case AMatch m:
					{
					var subject = S(Eval(m.Subject));
					bool hit = RegexOf(m.Pattern).IsMatch(subject);
					return AwkVal.Bool(hit != m.Negate);
					}
				case AIn i:
					{
					var arr = GetArray(i.Array);
					return AwkVal.Bool(arr.ContainsKey(SubscriptKey(i.Keys)));
					}
				case ACall call: return CallBuiltin(call);
				case AUserCall uc: return CallUser(uc);
				case AGetline g: return Getline(g);
				}
			throw new AwkSyntaxException("unknown expression");
			}

		// ── user functions ─────────────────────────────────────────────────────

		private AwkVal CallUser(AUserCall call)
			{
			var fn = _funcs[call.Name];   // every call was checked against the definitions after parsing
			// the interpreter recurses on the .NET stack; a stack overflow would kill the whole shell
			// (uncatchable), so a call that finds the stack nearly spent stops the program instead
			try { System.Runtime.CompilerServices.RuntimeHelpers.EnsureSufficientExecutionStack(); }
			catch (InsufficientExecutionStackException)
				{
				throw new AwkFatalException(call.Line, $"function `{call.Name}': calls nested too deep for C#Bash's in-process awk");
				}
			// arguments are evaluated in the caller's frame, left to right; surplus ones only for
			// their effects, then gawk's warning, at every call that has them (not while parsing)
			var frame = new Dictionary<string, AwkCell>(fn.Params.Count, StringComparer.Ordinal);
			for (int i = 0; i < call.Args.Count; i++)
				{
				if (i < fn.Params.Count) frame[fn.Params[i]] = BindArgument(call.Args[i]);
				else Eval(call.Args[i]);
				}
			if (call.Args.Count > fn.Params.Count)
				{
				Console.Out.Flush();
				Console.Error.WriteLine($"awk: {Source}:{call.Line}: warning: function `{call.Name}' called with more arguments than declared");
				}
			for (int i = call.Args.Count; i < fn.Params.Count; i++) frame[fn.Params[i]] = new AwkCell();   // a local
			var saved = _frame;
			_frame = frame;
			try { Exec(fn.Body); }
			finally { _frame = saved; }
			var result = _returning ? _returnValue : AwkVal.Uninit;
			_returning = false;
			_returnValue = AwkVal.Uninit;
			return result;
			}

		/// <summary>A bare variable name passes an array by reference, or an untyped variable that the
		/// function may turn into one; anything else passes its value.</summary>
		private AwkCell BindArgument(AExpr arg)
			{
			var cell = new AwkCell();
			if (arg is AVar v && v.Name is not ("NF" or "NR" or "FNR"))
				{
				if (Local(v.Name) is { } lc)
					{
					if (lc.Array is not null) { cell.Array = lc.Array; return cell; }
					if (!lc.IsScalar)
						{
						cell.BindCaller = arr => { lc.Array = arr; lc.BindCaller?.Invoke(arr); };
						return cell;
						}
					cell.Value = lc.Value;
					return cell;
					}
				if (_arrays.TryGetValue(v.Name, out var ga)) { cell.Array = ga; return cell; }
				if (!_vars.ContainsKey(v.Name))
					{
					var name = v.Name;
					cell.BindCaller = arr => _arrays[name] = arr;
					return cell;
					}
				}
			cell.Value = Eval(arg);
			return cell;
			}

		// ── getline < file, print > file, close, fflush ────────────────────────

		/// <summary>1 with a line read, 0 at the end of the file, -1 when it cannot be opened. Sets
		/// the variable, or $0 and NF; never NR or FNR (a file, not the main input).</summary>
		/// <summary>The main input, read record by record by the rule loop and by a plain `getline`: the
		/// operands in order (a `v=x` operand assigned as it is reached), standard input when there is no file.
		/// One reader for both, so a `getline` takes the record the loop would have taken next, across files
		/// too (FILENAME and FNR follow), as gawk.</summary>
		private List<string> _mainOps = [];
		private int _mainNext;
		private bool _mainAnyFile, _mainStdinDone;
		private TextReader? _mainReader;
		private IEnumerator<string>? _mainLines;

		private bool NextMainLine(out string line)
			{
			while (true)
				{
				if (_mainLines is not null)
					{
					if (_mainLines.MoveNext()) { line = _mainLines.Current; return true; }
					_mainLines.Dispose(); _mainReader?.Dispose(); _mainLines = null; _mainReader = null;
					}
				string op;
				if (_mainNext < _mainOps.Count) op = _mainOps[_mainNext++];
				else if (!_mainAnyFile && !_mainStdinDone) { _mainStdinDone = true; op = "-"; }   // no file operand: stdin
				else { line = ""; return false; }
				// an identifier then `=` is an assignment whatever the value holds (`v='\303\251'`, `out=C:\x`)
				if (op != "-" && !op.StartsWith('/') && Regex.IsMatch(op, @"^[A-Za-z_][A-Za-z0-9_]*=") && AssignOperand(op)) continue;
				_mainAnyFile = true;
				_vars["FILENAME"] = AwkVal.Str(op == "-" ? "" : op);
				_fnr = 0;
				try { _mainReader = OpenText(op); }
				catch (Exception ex) { throw new AwkOpenException($"awk: fatal: cannot open file `{op}' for reading: {IoError(ex)}"); }
				_mainLines = ReadLines(_mainReader).GetEnumerator();
				}
			}

		private void CloseMainInput() { _mainLines?.Dispose(); _mainReader?.Dispose(); _mainLines = null; _mainReader = null; }

		private AwkVal Getline(AGetline g)
			{
			if (g.File is null)
				{
				// the main input: the next record, counted in NR and FNR; 0 at its end (gawk)
				if (!NextMainLine(out var mline)) return AwkVal.Zero;
				_nr++; _fnr++;
				if (g.Target is null) SetRecord(mline);
				else Store(g.Target, AwkVal.StrNum(mline));
				return AwkVal.One;
				}
			var name = S(Eval(g.File));
			if (!_inputs.TryGetValue(name, out var input))
				{
				TextReader reader;
				try { reader = OpenText(name); }
				catch (Exception) { return AwkVal.Num(-1); }   // gawk: -1, no message
				input = (reader, ReadLines(reader).GetEnumerator());
				_inputs[name] = input;
				}
			string line;
			try
				{
				if (!input.Lines.MoveNext()) return AwkVal.Zero;
				line = input.Lines.Current;
				}
			catch (IOException) { return AwkVal.Num(-1); }
			catch (UnauthorizedAccessException) { return AwkVal.Num(-1); }
			if (g.Target is null) SetRecord(line);
			else Store(g.Target, AwkVal.StrNum(line));
			return AwkVal.One;
			}

		private TextWriter OutputFor(APrint p)
			{
			if (p.Target is null) return Console.Out;
			var name = S(Eval(p.Target));
			if (_outputs.TryGetValue(name, out var w)) return w;
			if (name == "-") return Console.Out;   // gawk 5.1: `print > "-"` is standard output
			switch (ShellFile.Device(name))
				{
				case "fd:1": return Console.Out;
				case "fd:2": return Console.Error;
				case "null" or "zero" or "random": w = TextWriter.Null; break;   // as the shell's own `> /dev/zero` discards
				default:
					try
						{
						var dev = ShellFile.DeviceFd(name);
						Stream s;
						if (dev >= 3)
							s = ShellEnvironment.Active?.FdStream?.Invoke(dev) is { CanWrite: true } fs ? fs : throw new IOException("Bad file descriptor");
						else if (dev == 0) throw new IOException("Bad file descriptor");
						else
							{
							var path = ShellEnvironment.TranslatePath(name);
							s = p.Append ? ShellFile.Append(path) : ShellFile.Create(path);
							}
						w = new StreamWriter(s, ShellEncoding.Utf8, 65536, leaveOpen: dev >= 3);
						}
					catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
						{
						throw new AwkFatalException(p.Line, $"cannot redirect to `{name}': {IoError(ex)}");
						}
					break;
				}
			_outputs[name] = w;
			return w;
			}

		/// <summary>close(name): 0 when an output or input of that name was open (and is now closed), else -1.</summary>
		private int Close(string name)
			{
			int rc = -1;
			if (_outputs.Remove(name, out var w)) { try { w.Dispose(); } catch { } rc = 0; }
			if (_inputs.Remove(name, out var input))
				{
				input.Lines.Dispose();
				if (!ReferenceEquals(input.Reader, Console.In)) input.Reader.Dispose();
				rc = 0;
				}
			return rc;
			}

		private void CloseAll()
			{
			foreach (var name in _outputs.Keys.ToList()) Close(name);
			foreach (var name in _inputs.Keys.ToList()) Close(name);
			}

		private static double Div(double a, double b)
			{
			if (b == 0) throw new AwkSyntaxException("division by zero");
			return a / b;
			}

		private static double Mod(double a, double b)
			{
			if (b == 0) throw new AwkSyntaxException("division by zero in %");
			return a % b;   // fmod: truncated toward zero, like awk
			}

		private int Compare(AwkVal a, AwkVal b)
			{
			if (a.NumericForCompare && b.NumericForCompare) return a.Number.CompareTo(b.Number);
			return string.CompareOrdinal(S(a), S(b));
			}

		private void Store(AExpr target, AwkVal v)
			{
			switch (target)
				{
				case AVar var: SetSpecialOrVar(var.Name, v); break;
				case AField f: SetField((int)Eval(f.Index).Number, S(v)); break;
				case AIndex ix: GetArray(ix.Name)[SubscriptKey(ix.Subs)] = v; break;
				default: throw new AwkSyntaxException("assignment to a non-variable");
				}
			}

		private AwkVal CallBuiltin(ACall c)
			{
			var a = c.Args;
			switch (c.Name)
				{
				case "length":
					{
					if (a.Count == 0) return AwkVal.Num(_record.Length);
					if (a[0] is AVar av && TryArray(av.Name, out var arr)) return AwkVal.Num(arr.Count);
					return AwkVal.Num(S(Eval(a[0])).Length);
					}
				case "substr":
					{
					if (a.Count < 2) throw new AwkSyntaxException("substr needs at least 2 arguments");
					var s = S(Eval(a[0]));
					double m = Eval(a[1]).Number;
					double n = a.Count > 2 ? Eval(a[2]).Number : double.PositiveInfinity;
					// gawk-verified: positions truncate (2.7 -> 2); a start below 1 is clamped to 1
					// without shortening the length (substr("hello",-1,3) == "hel")
					double start = Math.Truncate(m);
					if (start < 1) start = 1;
					double end = double.IsInfinity(n) ? s.Length + 1 : start + Math.Truncate(n);
					if (end > s.Length + 1) end = s.Length + 1;
					if (end <= start) return AwkVal.Str("");
					return AwkVal.Str(s.Substring((int)start - 1, (int)(end - start)));
					}
				case "index":
					{
					if (a.Count != 2) throw new AwkSyntaxException("index needs 2 arguments");
					var s = S(Eval(a[0])); var t = S(Eval(a[1]));
					return AwkVal.Num(s.IndexOf(t, StringComparison.Ordinal) + 1);
					}
				case "split":
					{
					if (a.Count < 2 || a[1] is not AVar arrVar) throw new AwkSyntaxException("split needs (string, array[, fs])");
					var s = S(Eval(a[0]));
					var arr = GetArray(arrVar.Name);
					arr.Clear();
					List<string> parts;
					if (a.Count > 2)
						{
						if (a[2] is ARegex rx) parts = s.Length == 0 ? [] : [.. GetRegex(rx.Pattern).Split(s)];
						else parts = SplitFields(s, S(Eval(a[2])));
						}
					else parts = SplitFields(s, S(_vars["FS"]));
					for (int i = 0; i < parts.Count; i++) arr[(i + 1).ToString(CultureInfo.InvariantCulture)] = AwkVal.StrNum(parts[i]);
					return AwkVal.Num(parts.Count);
					}
				case "sub" or "gsub":
					{
					if (a.Count < 2) throw new AwkSyntaxException($"{c.Name} needs (regex, replacement[, target])");
					var re = RegexOf(a[0]);
					var repl = S(Eval(a[1]));
					AExpr target = a.Count > 2 ? a[2] : new AField(new ANum(0));
					if (!(target is AVar or AField or AIndex)) throw new AwkSyntaxException($"{c.Name}: third argument is not a variable");
					var subject = S(Eval(target));
					int count = 0;
					bool global = c.Name == "gsub";
					var result = re.Replace(subject, m =>
						{
						if (!global && count > 0) return m.Value;
						count++;
						return ExpandReplacement(repl, m.Value);
						}, global ? -1 : 1);
					if (count > 0) Store(target, AwkVal.Str(result));
					return AwkVal.Num(count);
					}
				case "match":
					{
					if (a.Count is < 2 or > 3) throw new AwkSyntaxException("match needs (string, regex[, array])");
					if (a.Count == 3 && a[2] is not AVar) throw new AwkSyntaxException("match: third argument is not an array");
					var s = S(Eval(a[0]));
					var m = (a.Count == 3 ? CapturingRegexOf(a[1]) : RegexOf(a[1])).Match(s);
					_vars["RSTART"] = AwkVal.Num(m.Success ? m.Index + 1 : 0);
					_vars["RLENGTH"] = AwkVal.Num(m.Success ? m.Length : -1);
					if (a.Count == 3)
						{
						// gawk: cleared even on no match; arr[n] (input-like text, so strnum), arr[n,"start"],
						// arr[n,"length"] for the whole match (0) and every group that took part
						var arr = GetArray(((AVar)a[2]).Name);
						arr.Clear();
						if (m.Success)
							{
							var sep = S(_vars["SUBSEP"]);
							for (int gi = 0; gi < m.Groups.Count; gi++)
								{
								var grp = m.Groups[gi];
								if (!grp.Success) continue;
								var k = gi.ToString(CultureInfo.InvariantCulture);
								arr[k] = AwkVal.StrNum(grp.Value);
								arr[k + sep + "start"] = AwkVal.Num(grp.Index + 1);
								arr[k + sep + "length"] = AwkVal.Num(grp.Length);
								}
							}
						}
					return AwkVal.Num(m.Success ? m.Index + 1 : 0);
					}
				case "close":
					{
					if (a.Count != 1) throw new AwkSyntaxException("close needs (name)");
					return AwkVal.Num(Close(S(Eval(a[0]))));
					}
				case "fflush":
					{
					if (a.Count > 1) throw new AwkSyntaxException("fflush needs ([name])");
					if (a.Count == 0)
						{
						// gawk 4.0.2 on: every open output, and standard output
						Console.Out.Flush();
						foreach (var w in _outputs.Values) w.Flush();
						return AwkVal.Zero;
						}
					var name = S(Eval(a[0]));
					if (_outputs.TryGetValue(name, out var ow)) { ow.Flush(); return AwkVal.Zero; }
					if (name is "/dev/stdout" or "-") { Console.Out.Flush(); return AwkVal.Zero; }
					if (name == "/dev/stderr") { Console.Error.Flush(); return AwkVal.Zero; }
					Console.Error.WriteLine($"awk: {Source}:{c.Line}: warning: fflush: `{name}' is not an open file, pipe or co-process");
					return AwkVal.Num(-1);
					}
				case "tolower": return AwkVal.Str(S(Eval(a[0])).ToLowerInvariant());
				case "toupper": return AwkVal.Str(S(Eval(a[0])).ToUpperInvariant());
				case "sprintf":
					{
					if (a.Count == 0) throw new AwkSyntaxException("sprintf needs a format");
					return AwkVal.Str(AwkFormat(S(Eval(a[0])), a.Skip(1).Select(Eval).ToList(), Convfmt));
					}
				case "int": return AwkVal.Num(Math.Truncate(Eval(a[0]).Number));
				}
			throw new AwkUnsupportedException($"{c.Name}()");
			}

		private static string ExpandReplacement(string repl, string matched)
			{
			if (!repl.Contains('&') && !repl.Contains('\\')) return repl;
			var sb = new StringBuilder();
			for (int i = 0; i < repl.Length; i++)
				{
				if (repl[i] == '\\' && i + 1 < repl.Length && (repl[i + 1] == '&' || repl[i + 1] == '\\')) { sb.Append(repl[++i]); continue; }
				if (repl[i] == '&') { sb.Append(matched); continue; }
				sb.Append(repl[i]);
				}
			return sb.ToString();
			}

		// ── statements ─────────────────────────────────────────────────────────

		private void Exec(AStmt s)
			{
			switch (s)
				{
				case ABlock b:
					foreach (var st in b.Body)
						{
						Exec(st);
						if (_returning) break;
						}
					break;
				case AExprStmt es: Eval(es.E); break;
				case AReturn r:
					_returnValue = r.Value is null ? AwkVal.Uninit : Eval(r.Value);
					_returning = true;
					break;
				case APrint p:
					{
					var sink = OutputFor(p);
					if (p.Formatted)
						{
						var vals = p.Args.Select(Eval).ToList();
						sink.Write(AwkFormat(S(vals[0]), vals.Skip(1).ToList(), Convfmt));
						}
					else
						{
						var ofmt = _vars["OFMT"].Text("%.6g");
						string Out(AwkVal v) => v.Kind == AwkVal.K.Num ? AwkVal.NumToString(v.Number, ofmt) : S(v);
						var text = p.Args.Count == 0 ? _record : string.Join(S(_vars["OFS"]), p.Args.Select(e => Out(Eval(e))));
						sink.Write(text + S(_vars["ORS"]));
						}
					if (ReferenceEquals(sink, Console.Error)) sink.Flush();
					break;
					}
				case AIf i: if (Eval(i.C).Truth) Exec(i.Then); else if (i.Else is not null) Exec(i.Else); break;
				case AWhile w:
					while (Eval(w.C).Truth)
						{
						try { Exec(w.Body); }
						catch (AwkBreakException) { break; }
						catch (AwkContinueException) { }
						if (_returning) break;
						}
					break;
				case ADoWhile dw:
					do
						{
						try { Exec(dw.Body); }
						catch (AwkBreakException) { break; }
						catch (AwkContinueException) { }
						if (_returning) break;
						}
					while (Eval(dw.C).Truth);
					break;
				case AFor f:
					if (f.Init is not null) Exec(f.Init);
					while (f.C is null || Eval(f.C).Truth)
						{
						try { Exec(f.Body); }
						catch (AwkBreakException) { break; }
						catch (AwkContinueException) { }
						if (_returning) break;
						if (f.Step is not null) Exec(f.Step);
						}
					break;
				case AForIn fi:
					{
					var arr = GetArray(fi.Array);
					foreach (var key in arr.Keys.ToList())
						{
						SetSpecialOrVar(fi.Var, AwkVal.StrNum(key));
						try { Exec(fi.Body); }
						catch (AwkBreakException) { break; }
						catch (AwkContinueException) { }
						if (_returning) break;
						}
					break;
					}
				case ANext: throw new AwkNextException();
				case AExit x:
					if (x.Code is not null) _exitCode = (int)Eval(x.Code).Number;
					throw new AwkExitException(_exitCode);
				case ABreak: throw new AwkBreakException();
				case AContinue: throw new AwkContinueException();
				case ADelete d:
					{
					var arr = GetArray(d.Array);
					if (d.Subs is null) arr.Clear(); else arr.Remove(SubscriptKey(d.Subs));
					break;
					}
				default: throw new AwkSyntaxException("unknown statement");
				}
			}

		// ── driver ─────────────────────────────────────────────────────────────

		public int Run(string program, List<string> operands)
			{
			var prog = new AwkParser(AwkLex(program)).ParseProgram();
			_funcs = prog.Funcs;
			_mainOps = operands;   // BEGIN's `getline` reads it too
			var rangeActive = new bool[prog.Rules.Count];
			try
				{
				try { foreach (var b in prog.Begin) Exec(b); }
				catch (AwkExitException) { _exiting = true; }

				bool needInput = !_exiting && (prog.Rules.Count > 0 || prog.End.Count > 0);
				if (needInput)
					{
					// the main input (shared with a plain `getline`; see NextMainLine)
					while (!_exiting && NextMainLine(out var line))
						{
						_nr++; _fnr++;
						SetRecord(line);
						if (RunRules(prog, rangeActive)) { _exiting = true; break; }
						}
					}
				try { foreach (var b in prog.End) Exec(b); }
				catch (AwkExitException) { }
				}
			catch (AwkOpenException ex)
				{
				try { Console.Out.Flush(); } catch { }
				Console.Error.WriteLine(ex.Message);
				return 2;
				}
			finally
				{
				try { CloseMainInput(); } catch { }
				try { CloseAll(); } catch { }   // every `print > file` flushed and closed, as gawk does at exit
				try { Console.Out.Flush(); } catch { }
				}
			return _exitCode;
			}

		/// <summary>Runs the main rules for the current record; true when `exit` was executed.</summary>
		private bool RunRules(AProgram prog, bool[] rangeActive)
			{
			try
				{
				for (int i = 0; i < prog.Rules.Count; i++)
					{
					var rule = prog.Rules[i];
					bool fire;
					if (rule.Pattern is null) fire = true;
					else if (rule.PatternEnd is null) fire = Eval(rule.Pattern).Truth;
					else
						{
						if (!rangeActive[i])
							{
							if (Eval(rule.Pattern).Truth) { rangeActive[i] = true; fire = true; if (Eval(rule.PatternEnd).Truth) rangeActive[i] = false; }
							else fire = false;
							}
						else { fire = true; if (Eval(rule.PatternEnd).Truth) rangeActive[i] = false; }
						}
					if (!fire) continue;
					if (rule.Action is null) Console.Out.Write(_record + S(_vars["ORS"]));
					else Exec(rule.Action);
					}
				}
			catch (AwkNextException) { }
			catch (AwkExitException) { return true; }
			return false;
			}
		}
	}
