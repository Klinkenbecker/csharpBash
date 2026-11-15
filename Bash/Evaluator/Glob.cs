using System.Text;
using System.Text.RegularExpressions;

namespace Bash.Evaluator;

/// <summary>
/// Shell pattern matching (`*`, `?`, `[...]` incl. `[!...]`/`[^...]`, ranges and POSIX
/// classes, backslash escapes) for case patterns, `[[ == ]]`, `${x#pat}` and filename
/// globbing; plus the filesystem walker for pathname expansion (`dotglob`, `nullglob`,
/// `nocaseglob`, `globstar`).
/// </summary>
public static class Glob
	{
	// ── quoted metacharacters inside a field (2026-11-07) ──────────────────────────────────────
	// Brace and pathname expansion see a field after quote removal, so a quoted `,` `{` `*` was live: `{x\,y,z}` gave
	// x y z (bash: x,y z), `"{"a,b}` a b, `"a[1]"*` matched a1b. WordExpander marks each QUOTED one of these characters
	// with a Unicode noncharacter (never valid in text, U+FDE0 + its index here), and the brace characters of an unquoted
	// EXPANSION's result too (bash brace-expands only a word's literal text: `x='{a,b}'; echo $x` is {a,b}). Brace and
	// glob expansion take a mark as the plain character; Unmark turns the results back.
	internal const string Markable = "{},.*?[]\\!^-";
	internal const int BraceMarkable = 4;   // the first four: { } , . (brace expansion's own characters)
	internal const char FirstMark = (char)0xFDE0;   // U+FDE0.. (U+FDD0 and U+FDD1 are the lexer's EscapeMark and ArrayLiteralMark)
	internal static readonly System.Buffers.SearchValues<char> MarkableChars = System.Buffers.SearchValues.Create(Markable);

	internal static bool IsMark(char c) => c >= FirstMark && c < FirstMark + Markable.Length;

	private static readonly char BackslashMark = (char)(FirstMark + Markable.IndexOf('\\'));

	/// <summary>The character a mark stands for, or the character itself.</summary>
	internal static char Unmark(char c) => IsMark(c) ? Markable[c - FirstMark] : c;

	internal static string Unmark(string s)
		{
		int i = s.AsSpan().IndexOfAnyInRange(FirstMark, (char)(FirstMark + Markable.Length - 1));
		if (i < 0) return s;
		var chars = s.ToCharArray();
		for (; i < chars.Length; i++) chars[i] = Unmark(chars[i]);
		return new string(chars);
		}

	private static readonly Dictionary<(string, bool), Regex> _cache = new();

	/// <summary>Translate a shell pattern to an anchored .NET regex.</summary>
	public static Regex ToRegex(string pattern, bool ignoreCase = false)
		{
		lock (_cache)
			{
			if (_cache.TryGetValue((pattern, ignoreCase), out var cached)) return cached;
			var re = new Regex("^" + ToRegexBody(pattern) + "$",
				(ignoreCase ? RegexOptions.IgnoreCase : RegexOptions.None) | RegexOptions.Singleline | RegexOptions.CultureInvariant);
			if (_cache.Count > 512) _cache.Clear();
			_cache[(pattern, ignoreCase)] = re;
			return re;
			}
		}

	/// <summary>Regex body (unanchored) for a shell pattern. `*` and `?` match `/` too
	/// (this is string matching; the filename walker matches per path segment).</summary>
	public static string ToRegexBody(string pattern)
		{
		var sb = new StringBuilder();
		for (int i = 0; i < pattern.Length; i++)
			{
			char c = pattern[i];
			switch (c)
				{
				case '*': sb.Append(".*"); break;
				case '?': sb.Append('.'); break;
				case '\\':
					if (i + 1 < pattern.Length) { i++; sb.Append(Regex.Escape(pattern[i].ToString())); }
					else sb.Append("\\\\");
					break;
				case '[':
					{
					int close = FindBracketClose(pattern, i);
					if (close < 0) { sb.Append("\\["); break; }
					var body = pattern[(i + 1)..close];
					bool negate = body.Length > 0 && (body[0] == '!' || body[0] == '^');
					if (negate) body = body[1..];
					sb.Append('[');
					if (negate) sb.Append('^');
					for (int k = 0; k < body.Length; k++)
						{
						if (body[k] == '[' && k + 1 < body.Length && body[k + 1] == ':')
							{
							int end = body.IndexOf(":]", k + 2, StringComparison.Ordinal);
							if (end > 0)
								{
								sb.Append(PosixClass(body[(k + 2)..end]));
								k = end + 1;
								continue;
								}
							}
						char bc = body[k];
						if (IsMark(bc)) sb.Append('\\').Append(Unmark(bc));   // a quoted member: `["!"a]` is ! or a
						else if (bc is '\\' or ']' or '[' or '^' ) sb.Append('\\').Append(bc);
						else if (bc == '-' && (k == 0 || k == body.Length - 1)) sb.Append("\\-");
						else sb.Append(bc);
						}
					sb.Append(']');
					i = close;
					break;
					}
				default:
					sb.Append(Regex.Escape(Unmark(c).ToString()));   // a mark is its character, literally
					break;
				}
			}
		return sb.ToString();
		}

	// as GNU reads the classes in a UTF-8 locale (PosixClasses; ASCII until 2026-11-07: `case é in [[:alpha:]])` failed)
	private static string PosixClass(string name) => name == "word" ? "\\w" : PosixClasses.NetSet(name) ?? "";

	private static int FindBracketClose(string p, int open)
		{
		int i = open + 1;
		if (i < p.Length && (p[i] == '!' || p[i] == '^')) i++;
		if (i < p.Length && p[i] == ']') i++;          // a leading ] is literal
		for (; i < p.Length; i++)
			{
			if (p[i] == '[' && i + 1 < p.Length && p[i + 1] == ':')
				{
				int end = p.IndexOf(":]", i + 2, StringComparison.Ordinal);
				if (end > 0) { i = end + 1; continue; }
				}
			if (p[i] == ']') return i;
			}
		return -1;
		}

	/// <param name="extglob">read `@( ) *( ) +( ) ?( ) !( )` groups (`[[ == ]]` only: bash takes them there with the
	/// extglob option off; the option itself is refused, DECISIONS 2026-09-04).</param>
	public static bool Match(string pattern, string input, bool ignoreCase = false, bool extglob = false)
		{
		if (extglob)
			{
			if (pattern.Contains('(') && HasExtGroup(pattern)) return ExtMatch(pattern, 0, input, 0, ignoreCase);
			// a quoted value is backslashed for extglob (`\(`): still a plain string compare
			if (TryLiteral(pattern, out var lit)) return ignoreCase ? string.Equals(lit, input, StringComparison.OrdinalIgnoreCase) : lit == input;
			}
		if (pattern == "*") return true;
		if (!HasMeta(pattern)) return ignoreCase ? string.Equals(pattern, input, StringComparison.OrdinalIgnoreCase) : pattern == input;
		return ToRegex(pattern, ignoreCase).IsMatch(input);
		}

	// ── extended groups (`[[ x == @(a|b) ]]`, 2026-11-15) ─────────────────────────────────────────────────────
	// bash's five, as its manual gives them: ?(P) zero or one, *(P) zero or more, +(P) one or more, @(P) exactly one
	// of the |-separated alternatives, !(P) anything except one of them. A backtracking matcher over the pattern text
	// (a .NET regex has no "anything except" for a substring); patterns without a group keep the regex path.

	private static bool IsExtOp(char c) => c is '@' or '*' or '+' or '?' or '!';

	/// <summary>True when <paramref name="p"/> holds an extended group with its closing `)`.</summary>
	internal static bool HasExtGroup(string p)
		{
		for (int i = 0; i + 1 < p.Length; i++)
			{
			char c = p[i];
			if (c == '\\') { i++; continue; }
			if (c == '[' && FindBracketClose(p, i) is int b and > 0) { i = b; continue; }
			if (IsExtOp(c) && p[i + 1] == '(' && GroupClose(p, i + 1) > 0) return true;
			}
		return false;
		}

	// the `)` closing the `(` at open (nested groups and brackets skipped), or -1
	private static int GroupClose(string p, int open)
		{
		int depth = 0;
		for (int i = open; i < p.Length; i++)
			{
			char c = p[i];
			if (c == '\\') { i++; continue; }
			if (c == '[' && FindBracketClose(p, i) is int b and > 0) { i = b; continue; }
			if (c == '(') depth++;
			else if (c == ')' && --depth == 0) return i;
			}
		return -1;
		}

	// a group's alternatives, split at its own top-level `|`
	private static List<string> Alternatives(string p, int open, int close)
		{
		var alts = new List<string>();
		int depth = 0, start = open + 1;
		for (int i = open + 1; i < close; i++)
			{
			char c = p[i];
			if (c == '\\') { i++; continue; }
			if (c == '[' && FindBracketClose(p, i) is int b and > 0 && b < close) { i = b; continue; }
			if (c == '(') depth++;
			else if (c == ')') depth--;
			else if (c == '|' && depth == 0) { alts.Add(p[start..i]); start = i + 1; }
			}
		alts.Add(p[start..close]);
		return alts;
		}

	private static bool Whole(List<string> alts, string s, bool ic)
		{
		foreach (var a in alts) if (ExtMatch(a, 0, s, 0, ic)) return true;
		return false;
		}

	// *(P) from si: the rest here, or one more non-empty piece matching P and again
	private static bool Repeat(List<string> alts, string p, int rest, string s, int si, bool ic)
		{
		if (ExtMatch(p, rest, s, si, ic)) return true;
		for (int k = si + 1; k <= s.Length; k++)
			if (Whole(alts, s[si..k], ic) && Repeat(alts, p, rest, s, k, ic)) return true;
		return false;
		}

	private static bool ExtMatch(string p, int pi, string s, int si, bool ic)
		{
		while (pi < p.Length)
			{
			char c = p[pi];
			if (IsExtOp(c) && pi + 1 < p.Length && p[pi + 1] == '(' && GroupClose(p, pi + 1) is int close and > 0)
				{
				var alts = Alternatives(p, pi + 1, close);
				int rest = close + 1;
				switch (c)
					{
					case '*': return Repeat(alts, p, rest, s, si, ic);
					case '!':
						for (int k = si; k <= s.Length; k++)
							if (!Whole(alts, s[si..k], ic) && ExtMatch(p, rest, s, k, ic)) return true;
						return false;
					default:   // @ ? +: one piece first (? may also take none; + may repeat)
						if (c == '?' && ExtMatch(p, rest, s, si, ic)) return true;
						for (int k = si; k <= s.Length; k++)
							if (Whole(alts, s[si..k], ic) && (c == '+' ? Repeat(alts, p, rest, s, k, ic) : ExtMatch(p, rest, s, k, ic))) return true;
						return false;
					}
				}
			switch (c)
				{
				case '*':
					while (pi + 1 < p.Length && p[pi + 1] == '*' && !(pi + 2 < p.Length && p[pi + 2] == '(')) pi++;
					for (int k = si; k <= s.Length; k++) if (ExtMatch(p, pi + 1, s, k, ic)) return true;
					return false;
				case '?':
					if (si >= s.Length) return false;
					si++; pi++;
					continue;
				case '[' when FindBracketClose(p, pi) is int b and > 0:
					if (si >= s.Length || !ToRegex(p[pi..(b + 1)], ic).IsMatch(s[si].ToString())) return false;
					si++; pi = b + 1;
					continue;
				case '\\' when pi + 1 < p.Length:
					pi++;
					c = p[pi];
					break;
				}
			c = Unmark(c);
			if (si >= s.Length || !(c == s[si] || (ic && char.ToUpperInvariant(c) == char.ToUpperInvariant(s[si])))) return false;
			si++; pi++;
			}
		return si == s.Length;
		}

	/// <summary>True when <paramref name="pattern"/> has no live pattern character, only literal
	/// text and backslash escapes; <paramref name="literal"/> is that text with the escapes removed.
	/// Lets a quoted pattern (`${p#"$prefix"}`, escaped by the expander) take the plain-string path.</summary>
	public static bool TryLiteral(string pattern, out string literal)
		{
		literal = pattern;
		if (pattern.IndexOfAny(['*', '?', '[', '\\']) < 0) return true;
		var sb = new StringBuilder(pattern.Length);
		for (int i = 0; i < pattern.Length; i++)
			{
			char c = pattern[i];
			if (c == '\\') { if (i + 1 < pattern.Length) sb.Append(pattern[++i]); else sb.Append(c); continue; }
			if (c is '*' or '?' or '[') return false;
			sb.Append(c);
			}
		literal = sb.ToString();
		return true;
		}

	/// <summary>Make text match itself literally: escape every pattern character.</summary>
	public static string Escape(string s)
		{
		if (s.IndexOfAny(['*', '?', '[', ']', '\\']) < 0) return s;
		var sb = new StringBuilder(s.Length + 8);
		foreach (char c in s) { if (c is '*' or '?' or '[' or ']' or '\\') sb.Append('\\'); sb.Append(c); }
		return sb.ToString();
		}

	public static bool HasMeta(string s)
		{
		foreach (char c in s) if (c is '*' or '?' or '[' or '\\') return true;
		return false;
		}

	// ── filename expansion ─────────────────────────────────────────────────────

	/// <summary>
	/// Expand a pathname pattern against the filesystem. Returns matches in sorted order
	/// (each keeping the pattern's directory prefix text and separator style), or an empty
	/// list when nothing matches. Segments are matched one directory level at a time; a
	/// bare `**` segment recurses when <paramref name="globstar"/> is on.
	/// </summary>
	public static List<string> Expand(string pattern, bool dotglob, bool nocase, bool globstar)
		{
		// A quoted backslash arrives marked; in a path it is still a separator, as an unquoted one is: `"$TMP"/x*` (TMP is
		// F:\TEMP) gave F:/\TEMP/x... (`\TEMP` taken for one segment, then joined after `F:/`); MSYS gives F:\TEMP/x...
		// (facts-8a's workaround, 2026-11-15)
		pattern = pattern.Replace(BackslashMark, '\\');
		// Split into (root prefix, segments). Keep the separator the user typed.
		char sep = pattern.Contains('/') ? '/' : '\\';
		string root = "";
		string rest = pattern;
		bool IsSep(char c) => c is '/' or '\\';
		if (pattern.Length > 2 && IsSep(pattern[0]) && IsSep(pattern[1]) && !IsSep(pattern[2])
		    && pattern.IndexOfAny(['/', '\\'], 2) is int hostEnd && hostEnd > 2)
			{
			// UNC: `//host/share/` (or `\\host\share\`) is the root and the walk starts inside the
			// share. Until 2026-09-08 this fell into the "/" branch and looked for `\host` on the
			// current drive — a glob on a share silently matched nothing (reported by web-d6).
			int shareEnd = pattern.IndexOfAny(['/', '\\'], hostEnd + 1);
			if (shareEnd < 0) { root = pattern; rest = ""; }
			else { root = pattern[..(shareEnd + 1)]; rest = pattern[(shareEnd + 1)..]; }
			}
		else if (pattern.Length >= 2 && char.IsAsciiLetter(pattern[0]) && pattern[1] == ':')
			{ root = pattern[..2]; rest = pattern[2..]; if (rest.StartsWith('/') || rest.StartsWith('\\')) { root += rest[0]; rest = rest[1..]; } }
		else if (pattern.StartsWith('/') || pattern.StartsWith('\\'))
			{
			// MSYS drive form /c/... → C:\ root; otherwise the current drive's root
			if (pattern.Length >= 2 && char.IsAsciiLetter(pattern[1]) && (pattern.Length == 2 || pattern[2] is '/' or '\\'))
				{ root = pattern[..(pattern.Length >= 3 ? 3 : 2)]; rest = pattern.Length >= 3 ? pattern[3..] : ""; }
			// /tmp/... is the temporary folder, as TranslatePath has it for a plain path: the walk started at the drive's
			// root and looked for a folder named tmp, so `/tmp/x*` never matched (facts-8a, 2026-11-15)
			else if (pattern.StartsWith("/tmp/", StringComparison.Ordinal) || pattern.StartsWith("/tmp\\", StringComparison.Ordinal))
				{ root = pattern[..5]; rest = pattern[5..]; }
			else { root = pattern[..1]; rest = pattern[1..]; }
			}
		// the folders before the first segment holding a pattern character are the root too, kept as typed: the walk
		// joined every segment with one separator, so `F:\TEMP\d/x*` came out F:\TEMP/d/x-1.txt (MSYS: F:\TEMP\d/x-1.txt)
		int firstMeta = rest.IndexOfAny(['*', '?', '[']);
		if (firstMeta > 0 && rest.LastIndexOfAny(['/', '\\'], firstMeta) is int cut and >= 0)
			{ root += rest[..(cut + 1)]; rest = rest[(cut + 1)..]; }
		// a trailing separator ("*/", "dir/*/") matches directories only and is kept on the result
		bool dirsOnly = rest.EndsWith('/') || rest.EndsWith('\\');
		var segs = rest.Split(['/', '\\'], StringSplitOptions.RemoveEmptyEntries);
		if (segs.Length == 0) return [];

		var results = new List<string>();
		string startDir = ShellEnvironment.TranslatePath(root.Length == 0 ? "." : Unmark(root));   // "." is the running shell's cwd; a quoted character is itself
		Walk(startDir, root, segs, 0, results, sep, dotglob, nocase, globstar);
		if (dirsOnly)
			results = results.Where(r => Directory.Exists(ShellEnvironment.TranslatePath(r))).Select(r => r + sep).ToList();
		results.Sort(StringComparer.Ordinal);
		return results;
		}

	private static void Walk(string dir, string display, string[] segs, int idx, List<string> results,
		char sep, bool dotglob, bool nocase, bool globstar)
		{
		string seg = segs[idx];
		bool last = idx == segs.Length - 1;

		if (globstar && seg == "**")
			{
			// zero directories: continue matching the rest here
			if (last) { AddEntries(dir, display, "*", true, results, sep, dotglob, nocase); }
			else Walk(dir, display, segs, idx + 1, results, sep, dotglob, nocase, globstar);
			// one or more directories
			IEnumerable<string> subs;
			try { subs = Directory.EnumerateDirectories(dir); } catch { return; }
			foreach (var sub in subs)
				{
				var name = Path.GetFileName(sub);
				if (name.StartsWith('.') && !dotglob) continue;
				if (IsReparse(sub)) continue;
				var disp = Join(display, name, sep);
				if (last) results.Add(disp);
				Walk(sub, disp, segs, idx, results, sep, dotglob, nocase, globstar);
				}
			return;
			}

		if (!HasMeta(seg))
			{
			var lit = Unmark(seg.Replace("\\", ""));   // a quoted character is itself here
			var next = Path.Combine(dir, lit);
			var disp = Join(display, lit, sep);
			if (last) { if (File.Exists(next) || Directory.Exists(next)) results.Add(disp); }
			else if (Directory.Exists(next)) Walk(next, disp, segs, idx + 1, results, sep, dotglob, nocase, globstar);
			return;
			}

		if (last) { AddEntries(dir, display, seg, false, results, sep, dotglob, nocase); return; }

		IEnumerable<string> dirs;
		try { dirs = Directory.EnumerateDirectories(dir); } catch { return; }
		var re = ToRegex(seg, nocase);
		foreach (var d in dirs)
			{
			var name = Path.GetFileName(d);
			if (name.StartsWith('.') && !dotglob && Unmark(seg[0]) != '.') continue;
			if (!re.IsMatch(name)) continue;
			Walk(d, Join(display, name, sep), segs, idx + 1, results, sep, dotglob, nocase, globstar);
			}
		}

	private static void AddEntries(string dir, string display, string seg, bool dirsOnlyForStarStar,
		List<string> results, char sep, bool dotglob, bool nocase)
		{
		IEnumerable<string> entries;
		try { entries = Directory.EnumerateFileSystemEntries(dir); } catch { return; }
		var re = ToRegex(seg, nocase);
		bool trailingSlash = false;
		foreach (var e in entries)
			{
			var name = Path.GetFileName(e);
			if (name.StartsWith('.') && !dotglob && Unmark(seg[0]) != '.') continue;
			if (!re.IsMatch(name)) continue;
			results.Add(Join(display, name, sep));
			}
		_ = trailingSlash;
		}

	private static bool IsReparse(string path)
		{
		try { return (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0; } catch { return false; }
		}

	private static string Join(string display, string name, char sep)
		{
		if (display.Length == 0) return name;
		if (display.EndsWith('/') || display.EndsWith('\\')) return display + name;
		return display + sep + name;
		}
	}
