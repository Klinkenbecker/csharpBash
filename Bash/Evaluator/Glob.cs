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
						if (bc is '\\' or ']' or '[' or '^' ) sb.Append('\\').Append(bc);
						else if (bc == '-' && (k == 0 || k == body.Length - 1)) sb.Append("\\-");
						else sb.Append(bc);
						}
					sb.Append(']');
					i = close;
					break;
					}
				default:
					sb.Append(Regex.Escape(c.ToString()));
					break;
				}
			}
		return sb.ToString();
		}

	private static string PosixClass(string name) => name switch
		{
		"alpha" => "a-zA-Z", "digit" => "0-9", "alnum" => "a-zA-Z0-9", "upper" => "A-Z", "lower" => "a-z",
		"space" => "\\s", "blank" => " \\t", "punct" => "!-/:-@\\[-`{-~", "print" => "\\x20-\\x7e",
		"graph" => "\\x21-\\x7e", "cntrl" => "\\x00-\\x1f\\x7f", "xdigit" => "0-9A-Fa-f", "word" => "\\w",
		_ => "",
		};

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

	public static bool Match(string pattern, string input, bool ignoreCase = false)
		{
		if (pattern == "*") return true;
		if (!HasMeta(pattern)) return ignoreCase ? string.Equals(pattern, input, StringComparison.OrdinalIgnoreCase) : pattern == input;
		return ToRegex(pattern, ignoreCase).IsMatch(input);
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
			else { root = pattern[..1]; rest = pattern[1..]; }
			}
		// a trailing separator ("*/", "dir/*/") matches directories only and is kept on the result
		bool dirsOnly = rest.EndsWith('/') || rest.EndsWith('\\');
		var segs = rest.Split(['/', '\\'], StringSplitOptions.RemoveEmptyEntries);
		if (segs.Length == 0) return [];

		var results = new List<string>();
		string startDir = root.Length == 0 ? "." : ShellEnvironment.TranslatePath(root);
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
			var lit = seg.Replace("\\", "");
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
			if (name.StartsWith('.') && !dotglob && !seg.StartsWith('.')) continue;
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
			if (name.StartsWith('.') && !dotglob && !seg.StartsWith('.')) continue;
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
