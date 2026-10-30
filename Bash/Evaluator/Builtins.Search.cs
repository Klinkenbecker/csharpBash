using System.Text;
using System.Text.RegularExpressions;

namespace Bash.Evaluator;

/// <summary>
/// grep (BRE/ERE/PCRE/fixed, context, -o, -m, -r with include/exclude, counts, file lists)
/// and sed (addresses incl. ranges, s///, y, a/i/c, d/D/p/P/n/N/q/Q/=/l, hold space,
/// branches/labels, blocks, -i in-place, -s, -z). Strict option parsing throughout.
/// </summary>
public sealed partial class Builtins
	{
	// ── POSIX regex → .NET ──────────────────────────────────────────────────────

	/// <summary>Translate a POSIX Basic Regular Expression to .NET syntax: `\(` `\)` `\{` `\}`
	/// `\+` `\?` `\|` are operators and their bare forms literals; `\&lt;` `\&gt;` → `\b`;
	/// `[[:alpha:]]` classes are expanded. Used by grep/sed in their default mode.</summary>
	private static string BreToNet(string pat)
		{
		// POSIX/GNU: `^` anchors only at the start of the RE or right after `\(` / `\|`, and `$` only
		// at the end or right before `\)` / `\|`; elsewhere both are literals. A `*` at a start (also
		// after an anchoring `^`) is a literal. .NET treats `^`/`$` as anchors anywhere and rejects a
		// leading `*`, so `s/^REV=$(hg id -n)$/.../` silently matched nothing (2026-10-05).
		var sb = new StringBuilder(pat.Length + 8);
		bool start = true;   // at the start of the RE, of a `\(` group, or of a `\|` branch
		for (int i = 0; i < pat.Length; i++)
			{
			char c = pat[i];
			if (c == '\\' && i + 1 < pat.Length)
				{
				char n = pat[++i];
				if (n is '(' or '|') { sb.Append(n); start = true; continue; }
				if (n is ')' or '{' or '}' or '+' or '?') sb.Append(n);
				else if (n is '<' or '>') sb.Append("\\b");
				else sb.Append('\\').Append(n);
				start = false;
				continue;
				}
			if (c == '^') { sb.Append(start ? "^" : "\\^"); continue; }   // `^*`: the `*` is still at a start
			if (c == '$')
				{
				bool anchor = i == pat.Length - 1
				              || (i + 2 < pat.Length && pat[i + 1] == '\\' && pat[i + 2] is ')' or '|');
				sb.Append(anchor ? "$" : "\\$");
				start = false;
				continue;
				}
			if (c == '*' && start) { sb.Append("\\*"); start = false; continue; }
			if (c == '[') i = AppendBracket(pat, i, sb);
			else if (c is '(' or ')' or '{' or '}' or '+' or '?' or '|') sb.Append('\\').Append(c);
			else sb.Append(c);
			start = false;
			}
		return sb.ToString();
		}

	/// <summary>The named group carrying a `\K` branch's reported part (see <see cref="PcreToNet"/>).</summary>
	internal const string KeepGroup = "csbashKeep";

	/// <summary>PCRE → .NET: identity, except `\K` ("keep": the reported match starts here), which .NET
	/// lacks ("Unrecognized escape sequence \K", reported 2026-10-05). In each top-level branch,
	/// `X\KY` becomes `X(?&lt;csbashKeep&gt;Y)`: the whole of XY is matched and consumed, as PCRE does,
	/// and `-o` reports the group (<see cref="KeptSpan"/>). A lookbehind `(?&lt;=X)Y` was tried first and
	/// rejected: it does not consume X, so `\w+ \K\w+` over "public void Alpha()" also matched
	/// "Alpha", which PCRE does not. The last `\K` of a branch wins, as in PCRE. A `\K` inside a group
	/// is refused loudly.</summary>
	private static string PcreToNet(string pat)
		{
		if (!pat.Contains("\\K")) return pat;
		var branches = new List<(int Start, int End, List<int> Keeps)>();
		int depth = 0, branchStart = 0;
		var keeps = new List<int>();
		for (int i = 0; i < pat.Length; i++)
			{
			char c = pat[i];
			if (c == '\\' && i + 1 < pat.Length)
				{
				if (pat[i + 1] == 'K')
					{
					if (depth != 0) throw new ArgumentException("\\K inside a group is not supported");
					keeps.Add(i);
					}
				i++;
				continue;
				}
			if (c == '[')
				{
				int j = i + 1;
				if (j < pat.Length && pat[j] == '^') j++;
				if (j < pat.Length && pat[j] == ']') j++;
				while (j < pat.Length && pat[j] != ']') { if (pat[j] == '\\') j++; j++; }
				i = j;
				continue;
				}
			if (c == '(') depth++;
			else if (c == ')') depth--;
			else if (c == '|' && depth == 0) { branches.Add((branchStart, i, keeps)); keeps = []; branchStart = i + 1; }
			}
		branches.Add((branchStart, pat.Length, keeps));

		var sb = new StringBuilder(pat.Length + 16 * branches.Count);
		foreach (var (bs, be, ks) in branches)
			{
			if (sb.Length > 0 || bs > 0) sb.Append('|');
			if (ks.Count == 0) { sb.Append(pat, bs, be - bs); continue; }
			int last = ks[^1];
			for (int i = bs; i < last; i++)
				{
				if (ks.Contains(i)) { i++; continue; }   // an earlier \K of this branch: dropped
				sb.Append(pat[i]);
				}
			sb.Append("(?<").Append(KeepGroup).Append('>').Append(pat, last + 2, be - (last + 2)).Append(')');
			}
		return sb.ToString();
		}

	/// <summary>The part of a match grep reports: the `\K` group when the matching branch had one.</summary>
	private static (int Index, string Value) KeptSpan(Match m)
		{
		var g = m.Groups[KeepGroup];
		return g.Success ? (g.Index, g.Value) : (m.Index, m.Value);
		}

	/// <summary>ERE → .NET: mostly identity, plus POSIX classes and `\&lt;` `\&gt;`. A `*` at the start
	/// of the RE, a group or a branch is a literal, as in GNU (.NET rejected it).</summary>
	private static string EreToNet(string pat)
		{
		var sb = new StringBuilder(pat.Length + 8);
		bool start = true;
		for (int i = 0; i < pat.Length; i++)
			{
			char c = pat[i];
			if (c == '\\' && i + 1 < pat.Length)
				{
				char n = pat[++i];
				if (n is '<' or '>') sb.Append("\\b");
				else sb.Append('\\').Append(n);
				start = false;
				continue;
				}
			if (c == '*' && start) { sb.Append("\\*"); start = false; continue; }
			if (c == '[') { i = AppendBracket(pat, i, sb); start = false; continue; }
			sb.Append(c);
			start = c is '(' or '|' or '^';
			}
		return sb.ToString();
		}

	/// <summary>Copy a bracket expression starting at <paramref name="i"/>, expanding POSIX
	/// classes; returns the index of the closing bracket (or of `[` when unterminated).</summary>
	private static int AppendBracket(string pat, int i, StringBuilder sb)
		{
		int j = i + 1;
		if (j < pat.Length && pat[j] == '^') j++;
		if (j < pat.Length && pat[j] == ']') j++;
		int close = -1;
		for (int k = j; k < pat.Length; k++)
			{
			if (pat[k] == '[' && k + 1 < pat.Length && pat[k + 1] == ':')
				{
				int end = pat.IndexOf(":]", k + 2, StringComparison.Ordinal);
				if (end > 0) { k = end + 1; continue; }
				}
			if (pat[k] == ']') { close = k; break; }
			}
		if (close < 0) { sb.Append("\\["); return i; }
		var body = pat[(i + 1)..close];
		sb.Append('[');
		int p = 0;
		if (p < body.Length && body[p] == '^') { sb.Append('^'); p++; }
		for (; p < body.Length; p++)
			{
			if (body[p] == '[' && p + 1 < body.Length && body[p + 1] == ':')
				{
				int end = body.IndexOf(":]", p + 2, StringComparison.Ordinal);
				if (end > 0)
					{
					sb.Append(body[(p + 2)..end] switch
						{
						"alpha" => "a-zA-Z", "digit" => "0-9", "alnum" => "a-zA-Z0-9", "upper" => "A-Z", "lower" => "a-z",
						"space" => "\\s", "blank" => " \\t", "punct" => "!-/:-@\\[-`{-~", "print" => "\\x20-\\x7e",
						"graph" => "\\x21-\\x7e", "cntrl" => "\\x00-\\x1f\\x7f", "xdigit" => "0-9A-Fa-f", "word" => "\\w", _ => "",
						});
					p = end + 1;
					continue;
					}
				}
			char bc = body[p];
			if (bc == '\\' && p + 1 < body.Length)
				{
				// GNU sed/grep extension: \t \n \r \f \v \\ \] inside a bracket expression
				char n = body[p + 1];
				string? esc = n switch { 't' => "\\t", 'n' => "\\n", 'r' => "\\r", 'f' => "\\f", 'v' => "\\v", '\\' => "\\\\", ']' => "\\]", _ => null };
				if (esc is not null) { sb.Append(esc); p++; continue; }
				sb.Append("\\\\"); continue;
				}
			if (bc is '\\' or '[') sb.Append('\\').Append(bc);
			else if (bc == ']' && p == 0) sb.Append("\\]");
			else sb.Append(bc);
			}
		sb.Append(']');
		return close;
		}

	private static Regex MakeRegex(string pattern, bool ere, bool pcre, bool fixedStr, bool icase, bool wholeWord, bool wholeLine)
		{
		string pat = fixedStr ? Regex.Escape(pattern) : pcre ? PcreToNet(pattern) : ere ? EreToNet(pattern) : BreToNet(pattern);
		if (wholeWord) pat = $@"(?<![\w])(?:{pat})(?![\w])";
		if (wholeLine) pat = $"^(?:{pat})$";
		var opts = RegexOptions.CultureInvariant | (icase ? RegexOptions.IgnoreCase : RegexOptions.None);
		return new Regex(pat, opts);
		}

	// ═══════════════════════════════════════════════════════════════════════════
	// grep
	// ═══════════════════════════════════════════════════════════════════════════

	private int Grep(List<string> args)
		{
		var o = Opts.Parse("grep", args, "ivncLlwxFEGPrRqshHoabm:A:B:C:e:f:zZUITd:D:",
			["ignore-case:i", "no-ignore-case", "invert-match:v", "line-number:n", "count:c", "files-without-match:L", "files-with-matches:l",
			 "word-regexp:w", "line-regexp:x", "fixed-strings:F", "extended-regexp:E", "basic-regexp:G", "perl-regexp:P", "recursive:r",
			 "dereference-recursive:R", "quiet:q", "silent:q", "no-messages:s", "no-filename:h", "with-filename:H", "only-matching:o",
			 "text:a", "byte-offset:b", "max-count=:m", "after-context=:A", "before-context=:B", "context=:C", "regexp=:e", "file=:f",
			 "null-data:z", "null:Z", "binary:U", "initial-tab:T", "include=", "exclude=", "exclude-dir=", "color", "color=", "colour", "colour=",
			 "label=", "line-buffered", "binary-files=", "directories=:d", "devices=:D", "exclude-from=", "no-group-separator", "group-separator="],
			allowNumeric: true);
		bool icase = o.Has('i') && !o.HasLong("no-ignore-case"), invert = o.Has('v'), lineNum = o.Has('n'), count = o.Has('c'), filesWithout = o.Has('L'), filesWith = o.Has('l'),
		     wordMatch = o.Has('w'), lineMatch = o.Has('x'), fixedStr = o.Has('F'), ere = o.Has('E'), pcre = o.Has('P'), recursive = o.Has('r') || o.Has('R'),
		     quiet = o.Has('q'), noMessages = o.Has('s'), noFilename = o.Has('h'), withFilename = o.Has('H'), onlyMatching = o.Has('o'), text = o.Has('a'),
		     byteOffset = o.Has('b'), nullData = o.Has('z'), nullAfterName = o.Has('Z');
		// a negative count is no limit, as GNU's; a non-number is its "invalid max count", exit 2 (it was rc 1, no word)
		int maxCount = o.Int('m', -1, int.MinValue, (_, _) => "grep: invalid max count", 2);
		if (maxCount == 0) return 1;   // -m 0: GNU reads nothing and prints nothing, -c included (it was no limit)
		// GNU's refusal (2026-10-30), exit 2: a bad -A/-B was taken as 0, a bad -C threw
		static string BadContext(string v, IntProblem _) => $"grep: {v}: invalid context length argument";
		int after = o.Int('A', 0, 0, BadContext, 2), before = o.Int('B', 0, 0, BadContext, 2);
		if (o.Get('C') is not null) after = before = o.Int('C', 0, 0, BadContext, 2);
		if (o.Numeric is not null) { after = before = int.Parse(o.Numeric, System.Globalization.CultureInfo.InvariantCulture); }
		bool skipBinary = o.Has('I') || o.GetLong("binary-files") == "without-match";
		string groupSep = o.HasLong("no-group-separator") ? "" : (o.GetLong("group-separator") ?? "--");
		var includes = o.All('\0').ToList(); // placeholder, filled below
		includes = GetLongAll(args, "--include");
		var excludes = GetLongAll(args, "--exclude");
		var excludeDirs = GetLongAll(args, "--exclude-dir");
		if (o.GetLong("directories") is "skip") { }

		var patterns = new List<string>();
		foreach (var p in o.All('e')) patterns.AddRange(p.Split('\n'));
		foreach (var f in o.All('f'))
			{
			try { patterns.AddRange(ShellEncoding.ReadAllLines(ShellEnvironment.TranslatePath(f))); }
			catch (Exception ex) { Console.Error.WriteLine($"grep: {f}: {IoError(ex)}"); return 2; }
			}
		var operands = new List<string>(o.Operands);
		if (patterns.Count == 0)
			{
			if (operands.Count == 0) { Console.Error.WriteLine("Usage: grep [OPTION]... PATTERNS [FILE]..."); return 2; }
			patterns.AddRange(operands[0].Split('\n'));
			operands.RemoveAt(0);
			}
		Regex re;
		try
			{
			var alternatives = patterns.Select(p => fixedStr ? Regex.Escape(p) : pcre ? PcreToNet(p) : ere ? EreToNet(p) : BreToNet(p));
			string pat = patterns.Count == 1 ? alternatives.First() : "(?:" + string.Join(")|(?:", alternatives) + ")";
			if (wordMatch) pat = $@"(?<![\w])(?:{pat})(?![\w])";
			if (lineMatch) pat = $"^(?:{pat})$";
			re = new Regex(pat, RegexOptions.CultureInvariant | (icase ? RegexOptions.IgnoreCase : RegexOptions.None));
			}
		catch (ArgumentException ex) { Console.Error.WriteLine($"grep: {ex.Message}"); return 2; }

		// inputs
		var inputs = new List<(string name, string? path)>();
		if (recursive)
			{
			foreach (var f in operands.Count == 0 ? ["."] : operands) GrepCollect(f, f, inputs, includes, excludes, excludeDirs, noMessages, operands.Count == 0);
			}
		else if (operands.Count == 0) inputs.Add((o.GetLong("label") ?? "(standard input)", null));
		else foreach (var f in operands) inputs.Add((f, f == "-" ? null : f));

		bool showName = withFilename || (!noFilename && (inputs.Count > 1 || recursive));
		bool anyMatch = false, errored = false;
		char nameSep = nullAfterName ? '\0' : ':';
		string term = nullData ? "\0" : "\n";

		foreach (var (name, path) in inputs)
			{
			if (path is not null && excludes.Count > 0 && excludes.Any(x => Glob.Match(x, Path.GetFileName(path)))) continue;
			if (path is not null && includes.Count > 0 && !includes.Any(x => Glob.Match(x, Path.GetFileName(path)))) continue;
			TextReader? reader = null;
			bool binary = false;
			try
				{
				if (path is null) reader = Console.In;
				else
					{
					var fs = ShellEnvironment.TranslatePath(path);
					if (Directory.Exists(fs)) { if (!noMessages && !recursive) Console.Error.WriteLine($"grep: {name}: Is a directory"); if (!recursive) errored = true; continue; }
					if (!text && ShellFile.Device(fs) is null)   // a device has no length to probe: read as text
						{
						using var probe = ShellFile.OpenRead(fs);
						var buf = new byte[Math.Min(8192, probe.Length)];
						int n = probe.Read(buf, 0, buf.Length);
						binary = Array.IndexOf(buf, (byte)0, 0, n) >= 0;
						}
					if (binary && skipBinary) continue;
					reader = new LfReader(ShellFile.OpenInput(fs));   // LF-only lines, no BOM sniffing: bytes are data; files and devices
					}
				int lineNo = 0, matched = 0; long offset = 0;
				bool fileHadMatch = false;
				var beforeBuf = new Queue<(int no, long off, string text)>();
				int afterLeft = 0; int lastPrinted = 0; bool printedAny = false;
				string? line;
				var rdr = reader;
				Func<string?> next = nullData ? () => ReadUntil(rdr, '\0') : ShellEncoding.LineReaderFor(reader);
				while ((line = next()) is not null)
					{
					lineNo++;
					long lineOff = offset;
					offset += ShellEncoding.Utf8.GetByteCount(line) + 1;
					bool m = re.IsMatch(line);
					if (invert) m = !m;
					if (!m)
						{
						if (afterLeft > 0 && !count && !filesWith && !filesWithout && !quiet)
							{
							PrintLine(name, lineNo, lineOff, line, '-', showName, lineNum, byteOffset, nameSep, term);
							afterLeft--; lastPrinted = lineNo;
							}
						else if (before > 0)
							{
							beforeBuf.Enqueue((lineNo, lineOff, line));
							while (beforeBuf.Count > before) beforeBuf.Dequeue();
							}
						continue;
						}
					anyMatch = true; fileHadMatch = true; matched++;
					if (quiet) return 0;
					if (filesWith || filesWithout) break;
					if (count) { if (maxCount > 0 && matched >= maxCount) break; continue; }
					if (binary) { Console.WriteLine($"Binary file {name} matches"); break; }
					// context separator between non-adjacent groups
					if ((before > 0 || after > 0) && printedAny && groupSep.Length > 0)
						{
						int firstToPrint = beforeBuf.Count > 0 ? beforeBuf.Peek().no : lineNo;
						if (firstToPrint > lastPrinted + 1) Console.WriteLine(groupSep);
						}
					while (beforeBuf.Count > 0)
						{
						var (bno, boff, btext) = beforeBuf.Dequeue();
						if (bno > lastPrinted) PrintLine(name, bno, boff, btext, '-', showName, lineNum, byteOffset, nameSep, term);
						}
					if (onlyMatching)
						{
						foreach (Match mm in re.Matches(line))
							{
							var (ki, kv) = KeptSpan(mm);   // `\K` (-P): only the kept part is printed
							if (kv.Length > 0) PrintLine(name, lineNo, lineOff + ShellEncoding.Utf8.GetByteCount(line[..ki]), kv, ':', showName, lineNum, byteOffset, nameSep, term);
							}
						}
					else PrintLine(name, lineNo, lineOff, line, ':', showName, lineNum, byteOffset, nameSep, term);
					printedAny = true; lastPrinted = lineNo; afterLeft = after;
					if (maxCount > 0 && matched >= maxCount) break;
					}
				if (count)
					{
					if (showName) Console.Out.Write(name + nameSep);
					Console.WriteLine(matched);
					}
				else if (filesWith && fileHadMatch) Console.Out.Write(name + (nullAfterName ? "\0" : "\n"));
				else if (filesWithout && !fileHadMatch) Console.Out.Write(name + (nullAfterName ? "\0" : "\n"));
				}
			catch (BrokenPipeException) { throw; }
			catch (Exception ex) { if (!noMessages) Console.Error.WriteLine($"grep: {name}: {IoError(ex)}"); errored = true; }
			finally { if (path is not null) reader?.Dispose(); }
			}
		return anyMatch ? 0 : errored ? 2 : 1;
		}

	private static List<string> GetLongAll(List<string> args, string name)
		{
		var list = new List<string>();
		for (int i = 0; i < args.Count; i++)
			{
			if (args[i] == "--") break;
			if (args[i].StartsWith(name + "=", StringComparison.Ordinal)) list.Add(args[i][(name.Length + 1)..]);
			else if (args[i] == name && i + 1 < args.Count) list.Add(args[++i]);
			}
		return list;
		}

	private static string? ReadUntil(TextReader r, char delim)
		{
		var sb = new StringBuilder();
		int ch;
		while ((ch = r.Read()) >= 0)
			{
			if (ch == delim) return sb.ToString();
			sb.Append((char)ch);
			}
		return sb.Length > 0 ? sb.ToString() : null;
		}

	private static void PrintLine(string name, int lineNo, long offset, string text, char sep, bool showName, bool lineNum, bool byteOffset, char nameSep, string term)
		{
		var sb = new StringBuilder();
		if (showName) sb.Append(name).Append(nameSep == ':' ? sep : nameSep);
		if (lineNum) sb.Append(lineNo).Append(sep);
		if (byteOffset) sb.Append(offset).Append(sep);
		sb.Append(text).Append(term);
		Console.Out.Write(sb.ToString());
		}

	private static void GrepCollect(string path, string display, List<(string, string?)> inputs, List<string> includes, List<string> excludes, List<string> excludeDirs, bool quiet, bool implicitDot)
		{
		var fs = ShellEnvironment.TranslatePath(path);
		if (File.Exists(fs)) { inputs.Add((display, path)); return; }
		if (!Directory.Exists(fs)) { if (!quiet) Console.Error.WriteLine($"grep: {display}: No such file or directory"); return; }
		string[] entries;
		try { entries = Directory.GetFileSystemEntries(fs); }
		catch (Exception ex) { if (!quiet) Console.Error.WriteLine($"grep: {display}: {IoError(ex)}"); return; }
		Array.Sort(entries, StringComparer.Ordinal);
		foreach (var e in entries)
			{
			var n = Path.GetFileName(e);
			bool isDir = Directory.Exists(e);
			if (isDir && excludeDirs.Any(x => Glob.Match(x, n))) continue;
			if (isDir && new DirectoryInfo(e).LinkTarget is not null) continue;
			var childDisplay = display == "." && implicitDot ? n : (display.EndsWith('/') ? display : display + "/") + n;
			var childPath = (path.EndsWith('/') ? path : path + "/") + n;
			if (isDir) GrepCollect(childPath, childDisplay, inputs, includes, excludes, excludeDirs, quiet, false);
			else inputs.Add((childDisplay, childPath));
			}
		}

	// ═══════════════════════════════════════════════════════════════════════════
	// sed
	// ═══════════════════════════════════════════════════════════════════════════

	private sealed class SedAddr
		{
		public int Kind;            // 0 none, 1 line, 2 last ($), 3 regex, 4 first~step, 5 zero (0,/re/)
		public int Num, Step;
		public Regex? Re;
		}

	private sealed class SedCmd
		{
		public SedAddr? A1, A2;
		public bool A2Plus, A2Tilde;   // addr1,+N  /  addr1,~N
		public bool Negate;
		public char Cmd;
		public string Text = "";        // a/i/c text, label, file, y sets
		public Regex? SRe;
		public string SRepl = "";
		public bool SGlobal, SPrint, SIcase;
		public int SNth;
		public string? SWriteFile;
		public int JumpTo = -1;         // for { : index of matching }, for b/t/T resolved label index
		public int QCode;
		public string YFrom = "", YTo = "";
		// range state (per command instance)
		public bool InRange; public int RangeEndLine;
		}

	private sealed class SedProgram
		{
		public List<SedCmd> Cmds = [];
		public Dictionary<string, int> Labels = new(StringComparer.Ordinal);
		/// <summary>`l`'s wrap width when it names none: -l N, else GNU's 70; 0 never wraps.</summary>
		public int LineLength = 70;
		}

	private int Sed(List<string> args)
		{
		var o = Opts.Parse("sed", args, "ne:f:rEi::szul:", ["quiet:n", "silent:n", "expression=:e", "file=:f", "regexp-extended:E", "in-place::i", "separate:s", "null-data:z", "unbuffered:u", "line-length=:l", "posix", "debug", "sandbox", "follow-symlinks", "binary"]);
		bool noAuto = o.Has('n'), ere = o.Has('E') || o.Has('r'), separate = o.Has('s'), nullData = o.Has('z');
		bool inPlace = o.Has('i') || o.HasLong("in-place");
		string? backupSuffix = inPlace ? (o.Get('i') ?? o.GetLong("in-place")) : null;
		if (backupSuffix is { Length: 0 }) backupSuffix = null;
		var scripts = new List<string>();
		foreach (var e in o.All('e')) scripts.Add(e);
		foreach (var f in o.All('f'))
			{
			try { scripts.Add(ShellEncoding.ReadAllText(ShellEnvironment.TranslatePath(f))); }
			catch (Exception ex) { Console.Error.WriteLine($"sed: couldn't open file {f}: {IoError(ex)}"); return 1; }
			}
		var files = new List<string>(o.Operands);
		if (scripts.Count == 0)
			{
			if (files.Count == 0) { Console.Error.WriteLine("Usage: sed [OPTION]... {script-only-if-no-other-script} [input-file]..."); return 1; }
			scripts.Add(files[0]); files.RemoveAt(0);
			}
		SedProgram prog;
		try { prog = SedCompile(string.Join("\n", scripts), ere); }
		catch (UnsupportedOptionException) { throw; }
		catch (Exception ex) { Console.Error.WriteLine($"sed: -e expression #1, char {(ex as SedSyntaxException)?.At ?? 0}: {ex.Message}"); return 1; }
		if (o.Get('l') is string ll)
			prog.LineLength = int.TryParse(ll, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var width)
				? width : throw new UnsupportedOptionException("sed", $"-l {ll}", "invalid line length");

		// Lines are read as the script asks for them: each file was read whole first, which failed over 2 GiB and never
		// ended on endless input (`yes | sed 3q`). What lasts across files is `shared`: the `w` files, opened (so
		// truncated) once at the start as GNU (-s and -i reopened them per file, keeping only the last file's lines;
		// a file never written kept stale content), and `q`, which ends the whole run (it ended only its file).
		// Hold space, ranges and line numbers start again with each file under -s and -i, as GNU and as before.
		char sep = nullData ? '\0' : '\n';
		using var shared = new SedShared(sep);
		foreach (var c in prog.Cmds)
			{
			string? wf = c.Cmd == 'w' ? c.Text : c.Cmd == 's' ? c.SWriteFile : null;
			if (wf is null) continue;
			try { shared.For(wf); }
			catch (Exception ex) { Console.Error.WriteLine($"sed: couldn't open file {wf}: {IoError(ex)}"); return 4; }
			}

		try { return SedFiles(prog, files, inPlace, separate, backupSuffix, noAuto, sep, shared); }
		// a runtime error (GNU's panic: `s//x/` before any regex has run) ends sed; under -i the file is left as it was
		catch (SedRuntimeException ex) { Console.Error.WriteLine($"sed: -e expression #1, char 0: {ex.Message}"); return 1; }
		}

	/// <summary>sed's files, in place, separately or as one stream (options parsed, the program compiled).</summary>
	private int SedFiles(SedProgram prog, List<string> files, bool inPlace, bool separate, string? backupSuffix, bool noAuto, char sep, SedShared shared)
		{
		if (inPlace)
			{
			if (files.Count == 0) { Console.Error.WriteLine("sed: no input files"); return 1; }
			int rc = 0;
			foreach (var f in files)
				{
				var p = ShellEnvironment.TranslatePath(f);
				Stream src;
				try { src = ShellFile.OpenInput(p); }
				catch (Exception ex) { Console.Error.WriteLine($"sed: can't read {f}: {IoError(ex)}"); rc = 2; continue; }
				// GNU's way: the output goes to a temporary file beside the original, which then replaces it, so a
				// failure leaves the original whole (it was rewritten in place: a failed write lost it). File.Replace
				// keeps the original's attributes and ACL; with -i SUFFIX the backup is the original itself.
				string dir = Path.GetDirectoryName(Path.GetFullPath(p))!;
				string temp = Path.Combine(dir, "sed" + Path.GetRandomFileName().Replace(".", ""));
				int code;
				try
					{
					using (var input = new SedInput(new LfReader(src), f, sep))
					using (var w = new StreamWriter(ShellFile.Create(temp), ShellEncoding.Utf8) { NewLine = "\n" })
						code = SedRun(prog, input, new SedOut(w, sep), shared, noAuto);
					string? backup = backupSuffix is null ? null
						: backupSuffix.Contains('*') ? Path.Combine(dir, backupSuffix.Replace("*", Path.GetFileName(p))) : p + backupSuffix;
					File.Replace(temp, p, backup, ignoreMetadataErrors: true);
					}
				catch (Exception ex)
					{
					try { File.Delete(temp); } catch { }
					if (ex is BrokenPipeException or SedRuntimeException) throw;
					Console.Error.WriteLine($"sed: couldn't edit {f}: {IoError(ex)}");
					rc = 4;
					continue;
					}
				if (code != 0 || shared.Quit) return code != 0 ? code : rc;
				}
			return rc;
			}

		if (separate)
			{
			int rc = 0;
			foreach (var f in files.Count == 0 ? ["-"] : files)
				{
				using var input = new SedInput([f], sep);
				int code = SedRun(prog, input, shared.Stdout, shared, noAuto);
				rc = Math.Max(rc, input.Rc);
				if (code != 0 || shared.Quit) return code != 0 ? code : rc;
				}
			return rc;
			}
		// all files form one stream
		using (var input = new SedInput(files.Count == 0 ? ["-"] : files, sep))
			{
			int code = SedRun(prog, input, shared.Stdout, shared, noAuto);
			return code != 0 ? code : input.Rc;
			}
		}

	/// <summary>sed's input: the lines of its files in turn, each read when the script needs it. Stdin is read through the
	/// shell's shared reader and never past the line, so what `q` leaves is the next command's (GNU leaves a file's offset
	/// just after the line: `{ sed 1q; head -n 1; } &lt; f` prints lines 1 and 2); `$` peeks a character for the same
	/// reason, and at a file's end opens the next files to see whether any has a line.</summary>
	private sealed class SedInput : IDisposable
		{
		private readonly List<string> _files;
		private readonly char _sep;
		private int _next;
		private TextReader? _r;      // the file being read; stdin is the shell's, never closed here
		private bool _rIsStdin;
		private string _opened = "-";
		/// <summary>The file the last line came from (`F`; "-" for stdin).</summary>
		public string Name { get; private set; } = "-";
		/// <summary>2 once a file could not be read (its message written when it was reached, as GNU).</summary>
		public int Rc { get; private set; }

		public SedInput(List<string> files, char sep) { _files = files; _sep = sep; }
		public SedInput(TextReader r, string name, char sep) { _files = []; _sep = sep; _r = r; _opened = Name = name; }

		/// <summary>The next line, and whether its separator ended it; false after the last line of the last file.</summary>
		public bool Next(out string line, out bool ended)
			{
			while (_r is not null || Open())
				{
				if (ShellEncoding.ReadRecord(_r!, _sep, out ended) is string l) { line = l; Name = _opened; return true; }
				Close();
				}
			line = ""; ended = true;
			return false;
			}

		/// <summary>No line follows (`$`): nothing is consumed.</summary>
		public bool AtEnd()
			{
			while (_r is not null || Open())
				{
				if (_r!.Peek() >= 0) return false;
				Close();
				}
			return true;
			}

		private bool Open()
			{
			while (_next < _files.Count)
				{
				var f = _files[_next++];
				try
					{
					if (f is "-" or "/dev/stdin")
						{
						// a console's own reader can peek "nothing" while input is still to come: read it through an LfReader
						var stdin = ConsoleMux.In;
						_r = stdin is LfReader or StringReader ? stdin : new LfReader(stdin);
						_rIsStdin = true;
						}
					else { _r = new LfReader(ShellFile.OpenInput(ShellEnvironment.TranslatePath(f))); _rIsStdin = false; }
					_opened = f;
					return true;
					}
				catch (Exception ex) { Console.Error.WriteLine($"sed: can't read {f}: {IoError(ex)}"); Rc = 2; }
				}
			return false;
			}

		private void Close() { if (!_rIsStdin) _r?.Dispose(); _r = null; }
		public void Dispose() => Close();
		}

	/// <summary>One of sed's outputs. A line whose input had no newline (or NUL) is written without it, and the output owes
	/// it: GNU writes it before the next thing written there, so only the very end of an output lacks it (`printf a |
	/// sed p` is `a\na`).</summary>
	private sealed class SedOut(TextWriter w, char sep)
		{
		public readonly TextWriter W = w;
		private readonly string _sep = sep.ToString();
		private bool _owed;

		public void Text(string s)
			{
			if (_owed) { W.Write(_sep); _owed = false; }
			W.Write(s);
			}

		public void Line(string s, bool ended)
			{
			if (_owed) { W.Write(_sep); _owed = false; }
			if (ended) W.Write(s + _sep);
			else { W.Write(s); _owed = true; }
			}

		/// <summary>Write the owed newline now.</summary>
		public void Settle() { if (_owed) { W.Write(_sep); _owed = false; } }
		}

	/// <summary>An error sed finds while running (GNU's panic): reported as "sed: MESSAGE".</summary>
	private sealed class SedRuntimeException(string message) : Exception(message);

	/// <summary>What lasts across sed's files: stdout, stderr and the `w` files, each opened once (`w /dev/stdout` is the
	/// real stdout under -i too, as GNU: it went into the edited file), and whether `q` ended the run.</summary>
	private sealed class SedShared(char sep) : IDisposable
		{
		public readonly SedOut Stdout = new(Console.Out, sep);
		private SedOut? _stderr;
		private readonly Dictionary<string, SedOut> _files = new(StringComparer.Ordinal);
		public bool Quit;
		private Regex? _lastRe;

		/// <summary>A regex as it runs: an empty one (`//`, null) is the last one used, and every other use makes itself
		/// the last, across files too (GNU's regex_last). With none used yet: GNU's runtime error.</summary>
		public Regex Use(Regex? re) => re is not null ? (_lastRe = re) : _lastRe ?? throw new SedRuntimeException("no previous regular expression");

		public SedOut For(string file)
			{
			if (file == "/dev/stdout") return Stdout;
			if (file == "/dev/stderr") return _stderr ??= new SedOut(Console.Error, sep);
			if (!_files.TryGetValue(file, out var o))
				_files[file] = o = new SedOut(new StreamWriter(ShellEnvironment.TranslatePath(file), false, ShellEncoding.Utf8) { NewLine = "\n", AutoFlush = true }, sep);
			return o;
			}

		public void Dispose() { foreach (var o in _files.Values) o.W.Dispose(); }
		}

	/// <summary>A sed script error found while parsing, at GNU's "char N": the characters read so far.</summary>
	private sealed class SedSyntaxException(string message, int at) : Exception(message)
		{
		public int At { get; } = at;
		}

	private static SedProgram SedCompile(string script, bool ere)
		{
		var prog = new SedProgram();
		var blockStack = new Stack<int>();
		bool anyRe = false;   // a regex compiled before this point of the script: `//` needs one
		int p = 0;
		string s = script;
		void SkipWs() { while (p < s.Length && (s[p] == ' ' || s[p] == '\t')) p++; }
		void SkipSep() { while (p < s.Length && (s[p] == ';' || s[p] == '\n' || s[p] == ' ' || s[p] == '\t')) p++; }
		Exception Bad(string message) => new SedSyntaxException(message, p);   // it said char 0 for every error (2026-10-30)
		SedAddr? ReadAddr()
			{
			if (p >= s.Length) return null;
			char c = s[p];
			if (char.IsDigit(c))
				{
				int n = 0; while (p < s.Length && char.IsDigit(s[p])) n = n * 10 + (s[p++] - '0');
				if (p < s.Length && s[p] == '~') { p++; int step = 0; while (p < s.Length && char.IsDigit(s[p])) step = step * 10 + (s[p++] - '0'); return new SedAddr { Kind = 4, Num = n, Step = step }; }
				return n == 0 ? new SedAddr { Kind = 5 } : new SedAddr { Kind = 1, Num = n };
				}
			if (c == '$') { p++; return new SedAddr { Kind = 2 }; }
			if (c == '/' || c == '\\')
				{
				char delim = '/';
				if (c == '\\') { p++; if (p >= s.Length) throw Bad("unexpected end of script"); delim = s[p]; }
				p++;
				var re = SedReadDelim(s, ref p, delim);
				bool ic = false, modifier = false;
				while (p < s.Length && (s[p] == 'I' || s[p] == 'M')) { if (s[p] == 'I') ic = true; modifier = true; p++; }
				// `//` is the last regex used (null here; SedShared.Use), as GNU; with none before it in the script, or
				// with a modifier, an error. It was Regex(""), which matched every line.
				if (re.Length == 0)
					{
					if (modifier) throw Bad("cannot specify modifiers on empty regexp");
					return anyRe ? new SedAddr { Kind = 3, Re = null } : throw Bad("no previous regular expression");
					}
				anyRe = true;
				return new SedAddr { Kind = 3, Re = new Regex(ere ? EreToNet(re) : BreToNet(re), ic ? RegexOptions.IgnoreCase : RegexOptions.None) };
				}
			return null;
			}
		string ReadToEol()
			{
			int start = p;
			while (p < s.Length && s[p] != '\n') p++;
			var t = s[start..p];
			if (p < s.Length) p++;
			return t;
			}
		string ReadText()
			{
			// GNU: `a\` newline text (with \ line continuation), or one-liner `a text`
			SkipWs();
			if (p < s.Length && s[p] == '\\') { p++; SkipWs(); if (p < s.Length && s[p] == '\n') p++; }
			var sb = new StringBuilder();
			bool first = true;
			while (p < s.Length)
				{
				var line = ReadToEol();
				if (first) { line = line.TrimStart(' ', '\t'); first = false; }
				if (line.EndsWith('\\')) { sb.Append(line[..^1]).Append('\n'); continue; }
				sb.Append(SedUnescapeText(line));
				break;
				}
			return sb.ToString();
			}

		while (true)
			{
			SkipSep();
			if (p >= s.Length) break;
			if (s[p] == '#') { ReadToEol(); continue; }
			var cmd = new SedCmd { A1 = ReadAddr() };
			if (cmd.A1 is not null)
				{
				SkipWs();
				if (p < s.Length && s[p] == ',')
					{
					p++; SkipWs();
					if (p < s.Length && s[p] == '+') { p++; int n = 0; while (p < s.Length && char.IsDigit(s[p])) n = n * 10 + (s[p++] - '0'); cmd.A2 = new SedAddr { Kind = 1, Num = n }; cmd.A2Plus = true; }
					else if (p < s.Length && s[p] == '~') { p++; int n = 0; while (p < s.Length && char.IsDigit(s[p])) n = n * 10 + (s[p++] - '0'); cmd.A2 = new SedAddr { Kind = 1, Num = n }; cmd.A2Tilde = true; }
					else cmd.A2 = ReadAddr() ?? throw Bad("unexpected `,'");
					}
				}
			SkipWs();
			while (p < s.Length && s[p] == '!') { cmd.Negate = !cmd.Negate; p++; SkipWs(); }
			if (p >= s.Length) throw Bad("missing command");
			char c = s[p++];
			cmd.Cmd = c;
			switch (c)
				{
				case '{': blockStack.Push(prog.Cmds.Count); break;
				case '}':
					{
					if (blockStack.Count == 0) throw Bad("unexpected `}'");
					int open = blockStack.Pop();
					prog.Cmds[open].JumpTo = prog.Cmds.Count;   // index of this '}' command
					break;
					}
				case 's':
					{
					if (p >= s.Length) throw Bad("unterminated `s' command");
					char delim = s[p++];
					string pat = SedReadDelim(s, ref p, delim);
					cmd.SRepl = SedReadDelim(s, ref p, delim, regex: false);
					bool ic = false, multiline = false, exec = false;
					// GNU skips blanks among the flags (`s/a/b/ p` is p) and refuses an unknown one where it stands. A blank
					// ended them, so the rest was read as more commands: `s#..#..#31 exclusion` ran `e` (2026-10-30)
					while (p < s.Length && s[p] is not (';' or '\n' or '}' or '#'))
						{
						char f = s[p++];
						if (f is ' ' or '\t') continue;
						if (f == 'g') cmd.SGlobal = true;
						else if (f == 'p') cmd.SPrint = true;
						else if (f == 'i' || f == 'I') ic = true;
						else if (f == 'm' || f == 'M') multiline = true;
						else if (f == 'w') { SkipWs(); cmd.SWriteFile = ReadToEol().Trim(); }
						else if (f == 'e') exec = true;
						else if (char.IsDigit(f)) cmd.SNth = cmd.SNth * 10 + (f - '0');
						else throw Bad($"unknown option to `s'");
						}
					// only a command that is otherwise valid goes elsewhere for `e`
					if (exec) throw new UnsupportedOptionException("sed", "s///e", "executing the pattern space as a command is not available");
					var opts = (ic ? RegexOptions.IgnoreCase : RegexOptions.None) | (multiline ? RegexOptions.Multiline : RegexOptions.None);
					// `s//x/`: the last regex used (null here), as GNU; it was refused always (2026-10-28)
					if (pat.Length == 0)
						{
						if (ic || multiline) throw Bad("cannot specify modifiers on empty regexp");
						if (!anyRe) throw Bad("no previous regular expression");
						cmd.SRe = null;
						}
					else { cmd.SRe = new Regex(ere ? EreToNet(pat) : BreToNet(pat), opts); anyRe = true; }
					break;
					}
				case 'y':
					{
					char delim = s[p++];
					cmd.YFrom = SedUnescapeText(SedReadDelim(s, ref p, delim, regex: false));
					cmd.YTo = SedUnescapeText(SedReadDelim(s, ref p, delim, regex: false));
					if (cmd.YFrom.Length != cmd.YTo.Length) throw Bad("strings for `y' command are different lengths");
					break;
					}
				case 'a': case 'i': case 'c': cmd.Text = ReadText(); break;
				case ':':
					{
					SkipWs();
					var label = ReadToEol().Trim();
					int semi = label.IndexOf(';');
					if (semi >= 0) { p -= label.Length - semi; label = label[..semi]; }
					if (label.Length == 0) throw Bad("\":\" lacks a label");
					prog.Labels[label] = prog.Cmds.Count;
					break;
					}
				case 'b': case 't': case 'T':
					{
					SkipWs();
					var sb = new StringBuilder();
					while (p < s.Length && s[p] is not (';' or '\n' or '}')) sb.Append(s[p++]);
					cmd.Text = sb.ToString().Trim();
					break;
					}
				case 'r': case 'w': case 'R': case 'W':
					SkipWs(); cmd.Text = ReadToEol().Trim();
					if (c == 'R' || c == 'W') throw new UnsupportedOptionException("sed", c.ToString(), "not implemented");
					break;
				case 'q': case 'Q': case 'l': case 'L':
					{
					SkipWs();
					int n = 0; bool any = false;
					while (p < s.Length && char.IsDigit(s[p])) { n = n * 10 + (s[p++] - '0'); any = true; }
					cmd.QCode = any ? n : c == 'l' ? -1 : 0;   // `l` with no width takes the program's (`l 0`: never wrap)
					break;
					}
				case 'd': case 'D': case 'p': case 'P': case 'n': case 'N': case '=': case 'h': case 'H': case 'g': case 'G': case 'x': case 'z': case 'F':
					break;
				case 'e': throw new UnsupportedOptionException("sed", "e", "executing commands is not available");
				default: throw Bad($"unknown command: `{c}'");
				}
			prog.Cmds.Add(cmd);
			}
		if (blockStack.Count > 0) throw new Exception("unmatched `{'");
		foreach (var c in prog.Cmds)
			if (c.Cmd is 'b' or 't' or 'T' && c.Text.Length > 0)
				c.JumpTo = prog.Labels.TryGetValue(c.Text, out var idx) ? idx : throw new Exception($"can't find label for jump to `{c.Text}'");
		return prog;
		}

	private static string SedUnescapeText(string t)
		{
		var sb = new StringBuilder();
		for (int i = 0; i < t.Length; i++)
			{
			if (t[i] == '\\' && i + 1 < t.Length) { sb.Append(t[i + 1] switch { 'n' => '\n', 't' => '\t', 'r' => '\r', 'a' => '\a', 'f' => '\f', 'v' => '\v', var x => x }); i++; }
			else sb.Append(t[i]);
			}
		return sb.ToString();
		}

	// Read up to (and consume) the next unescaped delim; \<delim> becomes a literal delim,
	// \n stays as a newline escape for the regex, other backslash escapes are preserved.
	// Only a REGEX has bracket expressions: the replacement and y's two parts are text, where `[` is a character
	// (`s/^/[/;s/$/]/` read `[/;s/$/]` as one bracket expression, and two -e scripts ran together: 2026-10-19).
	private static string SedReadDelim(string s, ref int p, char delim, bool regex = true)
		{
		var sb = new StringBuilder();
		while (p < s.Length && s[p] != delim)
			{
			if (s[p] == '\\' && p + 1 < s.Length)
				{
				if (s[p + 1] == delim) { sb.Append(delim == '&' || delim == '\\' ? "\\" + delim : delim.ToString()); p += 2; continue; }
				if (s[p + 1] == '\n') { sb.Append('\n'); p += 2; continue; }
				sb.Append(s[p]).Append(s[p + 1]); p += 2; continue;
				}
			if (regex && s[p] == '[' && delim != '[')
				{
				// a bracket expression may contain the delimiter: copy it whole
				int close = s.IndexOf(']', p + 1);
				if (close > 0 && s[p + 1] == ']') close = s.IndexOf(']', p + 2);
				if (close > 0) { sb.Append(s, p, close - p + 1); p = close + 1; continue; }
				}
			sb.Append(s[p]); p++;
			}
		if (p >= s.Length) throw new Exception($"unterminated `s' command");
		p++;
		return sb.ToString();
		}

	private int SedRun(SedProgram prog, SedInput input, SedOut output, SedShared shared, bool noAuto)
		{
		// The pattern space and the hold space each know whether their line's newline (or NUL) was there, as GNU: x swaps
		// them, h and g copy one, G and H give the target the appended one's, n and N the new line's; the hold space
		// starts with one. A line without it goes out without it (SedOut owes it). Until 2026-10-12 a newline was always
		// added (`sed -i` changed a file that ended without one); until 2026-10-27 only "the last line of the stream" went
		// without, so `x`, `g` and `G` there, and a last file that was empty, came out unlike GNU.
		foreach (var c in prog.Cmds) { c.InRange = false; }
		string hold = "";
		bool holdEnded = true, psEnded = true;
		int lineNo = 0;
		string? restart = null;      // D: what is left of the pattern space, for a cycle that reads nothing
		var appendQueue = new List<string>();
		while (true)
			{
			string ps;
			if (restart is not null) { ps = restart; restart = null; }
			else if (input.Next(out ps, out psEnded)) lineNo++;
			else break;
			bool autoprint = !noAuto;
			bool substituted = false;
			appendQueue.Clear();
			int pc = 0;
			bool deleted = false;
			while (pc < prog.Cmds.Count)
				{
				var cmd = prog.Cmds[pc];
				bool selected = SedSelect(cmd, lineNo, input, ps, shared);
				if (cmd.Negate) selected = !selected;
				if (cmd.Cmd == '}') { pc++; continue; }
				if (!selected)
					{
					pc = cmd.Cmd == '{' ? cmd.JumpTo + 1 : pc + 1;
					continue;
					}
				switch (cmd.Cmd)
					{
					case '{': pc++; continue;
					case 'd': deleted = true; break;
					case 'D':
						{
						int nl = ps.IndexOf('\n');
						if (nl < 0) { deleted = true; break; }
						// like d, no autoprint (the append queue is still flushed); the rest restarts the cycle
						restart = ps[(nl + 1)..];
						deleted = true;
						break;
						}
					case 'p': output.Line(ps, psEnded); pc++; continue;
					case 'P': { int nl = ps.IndexOf('\n'); if (nl < 0) output.Line(ps, psEnded); else output.Line(ps[..nl], true); pc++; continue; }
					case 'n':
						{
						if (!noAuto) output.Line(ps, psEnded);
						if (!input.Next(out var next, out var nextEnded)) { autoprint = false; deleted = true; break; }
						ps = next; psEnded = nextEnded; lineNo++;
						pc++; continue;
						}
					case 'N':
						{
						if (!input.Next(out var next, out var nextEnded)) { if (!noAuto) output.Line(ps, psEnded); autoprint = false; deleted = true; break; }
						ps = ps + "\n" + next; psEnded = nextEnded; lineNo++;
						pc++; continue;
						}
					case 's':
						{
						var (res, changed) = SedSub(cmd, ps, shared.Use(cmd.SRe));
						ps = res;
						if (changed)
							{
							substituted = true;
							if (cmd.SPrint) output.Line(ps, psEnded);
							if (cmd.SWriteFile is not null) shared.For(cmd.SWriteFile).Line(ps, psEnded);
							}
						pc++; continue;
						}
					case 'y':
						{
						var sb = new StringBuilder(ps.Length);
						foreach (var ch in ps) { int k = cmd.YFrom.IndexOf(ch); sb.Append(k >= 0 ? cmd.YTo[k] : ch); }
						ps = sb.ToString(); pc++; continue;
						}
					case 'a': appendQueue.Add(cmd.Text + "\n"); pc++; continue;
					case 'i': output.Text(cmd.Text + "\n"); pc++; continue;
					case 'c':
						{
						// with a range, print the text once at the end of the range
						bool endOfRange = cmd.A2 is null || !cmd.InRange || cmd.Negate;
						if (endOfRange) output.Text(cmd.Text + "\n");
						deleted = true; break;
						}
					case '=': output.Text(lineNo + "\n"); pc++; continue;
					case 'l': output.Text(SedVisible(ps, cmd.QCode >= 0 ? cmd.QCode : prog.LineLength) + "\n"); pc++; continue;
					case 'h': hold = ps; holdEnded = psEnded; pc++; continue;
					case 'H': hold = hold + "\n" + ps; holdEnded = psEnded; pc++; continue;
					case 'g': ps = hold; psEnded = holdEnded; pc++; continue;
					case 'G': ps = ps + "\n" + hold; psEnded = holdEnded; pc++; continue;
					case 'x': (ps, hold) = (hold, ps); (psEnded, holdEnded) = (holdEnded, psEnded); pc++; continue;
					case 'z': ps = ""; pc++; continue;
					case 'F': output.Text(input.Name + "\n"); pc++; continue;
					case 'r':
						{
						try { appendQueue.Add(ShellEncoding.ReadAllText(ShellEnvironment.TranslatePath(cmd.Text))); } catch { }
						pc++; continue;
						}
					case 'w': shared.For(cmd.Text).Line(ps, psEnded); pc++; continue;
					case 'b': pc = cmd.Text.Length == 0 ? prog.Cmds.Count : cmd.JumpTo; continue;
					case 't': if (substituted) { substituted = false; pc = cmd.Text.Length == 0 ? prog.Cmds.Count : cmd.JumpTo; } else pc++; continue;
					case 'T': if (!substituted) { pc = cmd.Text.Length == 0 ? prog.Cmds.Count : cmd.JumpTo; } else { substituted = false; pc++; } continue;
					case 'q':
						{
						if (autoprint) output.Line(ps, psEnded);
						foreach (var a in appendQueue) output.Text(a);
						output.Settle();   // GNU: q writes an owed newline (`printf a | sed 1q` is `a\n`); Q does not
						shared.Quit = true;
						return cmd.QCode;
						}
					case 'Q': shared.Quit = true; return cmd.QCode;
					default: pc++; continue;
					}
				break;   // reached only via d/D/c/n-at-eof
				}
			if (!deleted && autoprint) output.Line(ps, psEnded);
			foreach (var a in appendQueue) output.Text(a);
			}
		return 0;
		}

	private static bool SedSelect(SedCmd cmd, int lineNo, SedInput input, string ps, SedShared shared)
		{
		if (cmd.A1 is null) return true;
		bool M(SedAddr a) => a.Kind switch
			{
			1 => lineNo == a.Num,
			2 => input.AtEnd(),
			3 => shared.Use(a.Re).IsMatch(ps),
			4 => a.Step <= 0 ? lineNo == a.Num : lineNo >= a.Num && (lineNo - a.Num) % a.Step == 0,
			5 => false,
			_ => true,
			};
		if (cmd.A2 is null) return M(cmd.A1);
		if (!cmd.InRange)
			{
			// `0,/re/`: the range is open from the very first line and can start only there;
			// it must NOT re-open on every later line once it has closed (until 2026-09-08 it
			// did, turning "patch the first occurrence" into a silent global rewrite).
			bool start = cmd.A1.Kind == 5 ? lineNo == 1 : M(cmd.A1);
			if (!start) return false;
			// range starts; compute end
			if (cmd.A2Plus) { cmd.RangeEndLine = lineNo + cmd.A2.Num; cmd.InRange = cmd.A2.Num > 0; return true; }
			if (cmd.A2Tilde) { int n = cmd.A2.Num; cmd.RangeEndLine = n <= 0 ? lineNo : (lineNo % n == 0 ? lineNo : (lineNo / n + 1) * n); cmd.InRange = cmd.RangeEndLine > lineNo; return true; }
			if (cmd.A2.Kind == 1) { cmd.RangeEndLine = cmd.A2.Num; cmd.InRange = cmd.A2.Num > lineNo; return true; }
			if (cmd.A2.Kind == 2) { cmd.InRange = !input.AtEnd(); return true; }
			if (cmd.A2.Kind == 3)
				{
				// 0,/re/ lets the end match the very first line; addr1,/re/ starts checking on the next line
				if (cmd.A1.Kind == 5 && shared.Use(cmd.A2.Re).IsMatch(ps)) { cmd.InRange = false; return true; }
				cmd.InRange = true; return true;
				}
			cmd.InRange = true; return true;
			}
		// inside a range: does this line end it?
		if (cmd.A2Plus || cmd.A2Tilde || cmd.A2.Kind == 1) { if (lineNo >= cmd.RangeEndLine) cmd.InRange = false; return true; }
		if (cmd.A2.Kind == 2) { if (input.AtEnd()) cmd.InRange = false; return true; }
		if (cmd.A2.Kind == 3) { if (shared.Use(cmd.A2.Re).IsMatch(ps)) cmd.InRange = false; return true; }
		return true;
		}

	/// <summary>GNU sed's `l` (4.8, checked 2026-10-30): each byte as it is, `\\` and the C escapes, any other
	/// non-printable or non-ASCII byte as \ooo; the output wrapped so that no line holds more than
	/// <paramref name="width"/> - 1 of it, each ending in `\`, an escape never split (with width 1 the first line is a
	/// lone `\`, as GNU's); width 0 never wraps; `$` at the end. It never wrapped, and took a character at a time (each
	/// half of a 4-byte character encoded alone).</summary>
	private static string SedVisible(string s, int width)
		{
		var sb = new StringBuilder();
		int col = 0;
		foreach (var b in ShellEncoding.Utf8.GetBytes(s))
			{
			string item = b switch
				{
				(byte)'\\' => "\\\\", 7 => "\\a", 8 => "\\b", 12 => "\\f", 10 => "\\n", 13 => "\\r", 9 => "\\t", 11 => "\\v",
				< 32 or > 126 => "\\" + Convert.ToString(b, 8).PadLeft(3, '0'),
				_ => ((char)b).ToString(),
				};
			if (width > 0 && col + item.Length > width - 1) { sb.Append("\\\n"); col = 0; }
			sb.Append(item); col += item.Length;
			}
		return sb.Append('$').ToString();
		}

	private static (string result, bool changed) SedSub(SedCmd cmd, string input, Regex re)
		{
		var matches = re.Matches(input);
		if (matches.Count == 0) return (input, false);
		int target = cmd.SNth == 0 ? 1 : cmd.SNth;
		var sb = new StringBuilder();
		int last = 0, idx = 0; bool changed = false;
		foreach (Match mt in matches)
			{
			idx++;
			bool doRepl = cmd.SGlobal ? idx >= target : idx == target;
			sb.Append(input, last, mt.Index - last);
			if (doRepl) { sb.Append(SedExpand(cmd.SRepl, mt)); changed = true; }
			else sb.Append(mt.Value);
			last = mt.Index + mt.Length;
			if (!cmd.SGlobal && doRepl) break;
			}
		sb.Append(input, last, input.Length - last);
		return (sb.ToString(), changed);
		}

	/// <summary>Expand a sed replacement: & = whole match, \1..\9 = groups, \n \t literals,
	/// \L \U (until \E) and \l \u (next char) case conversion (GNU).</summary>
	private static string SedExpand(string repl, Match m)
		{
		var sb = new StringBuilder();
		int caseMode = 0;      // 0 none, 1 lower, 2 upper
		int oneShot = 0;       // 0 none, 1 lower next, 2 upper next
		void Put(string text)
			{
			foreach (var ch in text)
				{
				char c = ch;
				if (oneShot == 1) { c = char.ToLowerInvariant(c); oneShot = 0; }
				else if (oneShot == 2) { c = char.ToUpperInvariant(c); oneShot = 0; }
				else if (caseMode == 1) c = char.ToLowerInvariant(c);
				else if (caseMode == 2) c = char.ToUpperInvariant(c);
				sb.Append(c);
				}
			}
		for (int i = 0; i < repl.Length; i++)
			{
			char c = repl[i];
			if (c == '&') { Put(m.Value); continue; }
			if (c == '\\' && i + 1 < repl.Length)
				{
				char n = repl[++i];
				if (char.IsDigit(n)) { int g = n - '0'; if (g < m.Groups.Count) Put(m.Groups[g].Value); continue; }
				switch (n)
					{
					case 'n': sb.Append('\n'); break;
					case 't': sb.Append('\t'); break;
					case 'r': sb.Append('\r'); break;
					case 'L': caseMode = 1; break;
					case 'U': caseMode = 2; break;
					case 'E': caseMode = 0; break;
					case 'l': oneShot = 1; break;
					case 'u': oneShot = 2; break;
					default: sb.Append(n); break;
					}
				continue;
				}
			Put(c.ToString());
			}
		return sb.ToString();
		}
	}
