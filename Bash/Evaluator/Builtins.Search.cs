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
		var sb = new StringBuilder(pat.Length + 8);
		for (int i = 0; i < pat.Length; i++)
			{
			char c = pat[i];
			if (c == '\\' && i + 1 < pat.Length)
				{
				char n = pat[++i];
				if (n is '(' or ')' or '{' or '}' or '+' or '?' or '|') sb.Append(n);
				else if (n is '<' or '>') sb.Append("\\b");
				else { sb.Append('\\').Append(n); }
				}
			else if (c == '[') { i = AppendBracket(pat, i, sb); }
			else if (c is '(' or ')' or '{' or '}' or '+' or '?' or '|') { sb.Append('\\').Append(c); }
			else sb.Append(c);
			}
		return sb.ToString();
		}

	/// <summary>ERE → .NET: mostly identity, plus POSIX classes and `\&lt;` `\&gt;`.</summary>
	private static string EreToNet(string pat)
		{
		var sb = new StringBuilder(pat.Length + 8);
		for (int i = 0; i < pat.Length; i++)
			{
			char c = pat[i];
			if (c == '\\' && i + 1 < pat.Length)
				{
				char n = pat[++i];
				if (n is '<' or '>') sb.Append("\\b");
				else sb.Append('\\').Append(n);
				}
			else if (c == '[') { i = AppendBracket(pat, i, sb); }
			else sb.Append(c);
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
		string pat = fixedStr ? Regex.Escape(pattern) : pcre ? pattern : ere ? EreToNet(pattern) : BreToNet(pattern);
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
		int maxCount = o.Get('m') is string ms ? int.Parse(ms) : -1;
		int after = o.GetInt('A', 0), before = o.GetInt('B', 0);
		if (o.Get('C') is string cs) { after = before = int.Parse(cs); }
		if (o.Numeric is not null) { after = before = int.Parse(o.Numeric); }
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
			var alternatives = patterns.Select(p => fixedStr ? Regex.Escape(p) : pcre ? p : ere ? EreToNet(p) : BreToNet(p));
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
					if (!text)
						{
						using var probe = File.OpenRead(fs);
						var buf = new byte[Math.Min(8192, probe.Length)];
						int n = probe.Read(buf, 0, buf.Length);
						binary = Array.IndexOf(buf, (byte)0, 0, n) >= 0;
						}
					if (binary && skipBinary) continue;
					reader = new StreamReader(fs, ShellEncoding.Utf8);
					}
				int lineNo = 0, matched = 0; long offset = 0;
				bool fileHadMatch = false;
				var beforeBuf = new Queue<(int no, long off, string text)>();
				int afterLeft = 0; int lastPrinted = 0; bool printedAny = false;
				string? line;
				while ((line = nullData ? ReadUntil(reader, '\0') : reader.ReadLine()) is not null)
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
							if (mm.Length > 0) PrintLine(name, lineNo, lineOff + ShellEncoding.Utf8.GetByteCount(line[..mm.Index]), mm.Value, ':', showName, lineNum, byteOffset, nameSep, term);
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
			if (args[i].StartsWith(name + "=")) list.Add(args[i][(name.Length + 1)..]);
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
		catch (Exception ex) { Console.Error.WriteLine($"sed: -e expression #1, char 0: {ex.Message}"); return 1; }

		if (inPlace)
			{
			if (files.Count == 0) { Console.Error.WriteLine("sed: no input files"); return 1; }
			int rc = 0;
			foreach (var f in files)
				{
				var p = ShellEnvironment.TranslatePath(f);
				List<string> lines;
				try { lines = SedReadFile(p, nullData); }
				catch (Exception ex) { Console.Error.WriteLine($"sed: can't read {f}: {IoError(ex)}"); rc = 2; continue; }
				var outSb = new StringBuilder();
				int code;
				using (var w = new StringWriter(outSb) { NewLine = "\n" })
					code = SedRun(prog, lines, w, noAuto, nullData, f);
				try
					{
					if (backupSuffix is not null) File.Copy(p, backupSuffix.Contains('*') ? Path.Combine(Path.GetDirectoryName(p)!, backupSuffix.Replace("*", Path.GetFileName(p))) : p + backupSuffix, true);
					File.WriteAllText(p, outSb.ToString(), ShellEncoding.Utf8);
					}
				catch (Exception ex) { Console.Error.WriteLine($"sed: couldn't edit {f}: {IoError(ex)}"); rc = 4; }
				if (code != 0) return code;
				}
			return rc;
			}

		if (separate || files.Count == 0)
			{
			int rc = 0;
			foreach (var f in files.Count == 0 ? ["-"] : files)
				{
				List<string> lines;
				try { lines = f == "-" ? SedReadStdin(nullData) : SedReadFile(ShellEnvironment.TranslatePath(f), nullData); }
				catch (Exception ex) { Console.Error.WriteLine($"sed: can't read {f}: {IoError(ex)}"); rc = 2; continue; }
				int code = SedRun(prog, lines, Console.Out, noAuto, nullData, f);
				if (code != 0) return code;
				}
			return rc;
			}
		// all files form one stream
		var all = new List<string>();
		int frc = 0;
		foreach (var f in files)
			{
			try { all.AddRange(f == "-" ? SedReadStdin(nullData) : SedReadFile(ShellEnvironment.TranslatePath(f), nullData)); }
			catch (Exception ex) { Console.Error.WriteLine($"sed: can't read {f}: {IoError(ex)}"); frc = 2; }
			}
		int r2 = SedRun(prog, all, Console.Out, noAuto, nullData, files.Count > 0 ? files[0] : "-");
		return r2 != 0 ? r2 : frc;
		}

	private static List<string> SedReadFile(string p, bool nullData)
		{
		var text = ShellEncoding.ReadAllText(p);
		return SedSplit(text, nullData);
		}

	private static List<string> SedReadStdin(bool nullData) => SedSplit(Console.In.ReadToEnd(), nullData);

	private static List<string> SedSplit(string text, bool nullData)
		{
		char sep = nullData ? '\0' : '\n';
		var parts = text.Split(sep).ToList();
		if (parts.Count > 0 && parts[^1].Length == 0) parts.RemoveAt(parts.Count - 1);
		return parts;
		}

	private static SedProgram SedCompile(string script, bool ere)
		{
		var prog = new SedProgram();
		var blockStack = new Stack<int>();
		int p = 0;
		string s = script;
		void SkipWs() { while (p < s.Length && (s[p] == ' ' || s[p] == '\t')) p++; }
		void SkipSep() { while (p < s.Length && (s[p] == ';' || s[p] == '\n' || s[p] == ' ' || s[p] == '\t')) p++; }
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
				if (c == '\\') { p++; if (p >= s.Length) throw new Exception("unexpected end of script"); delim = s[p]; }
				p++;
				var re = SedReadDelim(s, ref p, delim);
				bool ic = false;
				while (p < s.Length && (s[p] == 'I' || s[p] == 'M')) { if (s[p] == 'I') ic = true; p++; }
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
					else cmd.A2 = ReadAddr() ?? throw new Exception("unexpected `,'");
					}
				}
			SkipWs();
			while (p < s.Length && s[p] == '!') { cmd.Negate = !cmd.Negate; p++; SkipWs(); }
			if (p >= s.Length) throw new Exception("missing command");
			char c = s[p++];
			cmd.Cmd = c;
			switch (c)
				{
				case '{': blockStack.Push(prog.Cmds.Count); break;
				case '}':
					{
					if (blockStack.Count == 0) throw new Exception("unexpected `}'");
					int open = blockStack.Pop();
					prog.Cmds[open].JumpTo = prog.Cmds.Count;   // index of this '}' command
					break;
					}
				case 's':
					{
					if (p >= s.Length) throw new Exception("unterminated `s' command");
					char delim = s[p++];
					string pat = SedReadDelim(s, ref p, delim);
					cmd.SRepl = SedReadDelim(s, ref p, delim);
					bool ic = false, multiline = false;
					while (p < s.Length && s[p] is not (';' or '\n' or '}' or ' ' or '\t' or '#'))
						{
						char f = s[p++];
						if (f == 'g') cmd.SGlobal = true;
						else if (f == 'p') cmd.SPrint = true;
						else if (f == 'i' || f == 'I') ic = true;
						else if (f == 'm' || f == 'M') multiline = true;
						else if (f == 'w') { SkipWs(); cmd.SWriteFile = ReadToEol().Trim(); }
						else if (f == 'e') throw new UnsupportedOptionException("sed", "s///e", "executing the pattern space as a command is not available");
						else if (char.IsDigit(f)) cmd.SNth = cmd.SNth * 10 + (f - '0');
						else throw new Exception($"unknown option to `s'");
						}
					var opts = (ic ? RegexOptions.IgnoreCase : RegexOptions.None) | (multiline ? RegexOptions.Multiline : RegexOptions.None);
					if (pat.Length == 0) throw new Exception("no previous regular expression");
					cmd.SRe = new Regex(ere ? EreToNet(pat) : BreToNet(pat), opts);
					break;
					}
				case 'y':
					{
					char delim = s[p++];
					cmd.YFrom = SedUnescapeText(SedReadDelim(s, ref p, delim));
					cmd.YTo = SedUnescapeText(SedReadDelim(s, ref p, delim));
					if (cmd.YFrom.Length != cmd.YTo.Length) throw new Exception("strings for `y' command are different lengths");
					break;
					}
				case 'a': case 'i': case 'c': cmd.Text = ReadText(); break;
				case ':':
					{
					SkipWs();
					var label = ReadToEol().Trim();
					int semi = label.IndexOf(';');
					if (semi >= 0) { p -= label.Length - semi; label = label[..semi]; }
					if (label.Length == 0) throw new Exception("\":\" lacks a label");
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
					cmd.QCode = any ? n : 0;
					break;
					}
				case 'd': case 'D': case 'p': case 'P': case 'n': case 'N': case '=': case 'h': case 'H': case 'g': case 'G': case 'x': case 'z': case 'F':
					break;
				case 'e': throw new UnsupportedOptionException("sed", "e", "executing commands is not available");
				default: throw new Exception($"unknown command: `{c}'");
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
	private static string SedReadDelim(string s, ref int p, char delim)
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
			if (s[p] == '[' && delim != '[')
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

	private int SedRun(SedProgram prog, List<string> lines, TextWriter output, bool noAuto, bool nullData, string fileName)
		{
		string term = nullData ? "\0" : "\n";
		var writers = new Dictionary<string, TextWriter>(StringComparer.Ordinal);
		TextWriter WriterFor(string file)
			{
			if (file == "/dev/stdout") return output;
			if (file == "/dev/stderr") return Console.Error;
			if (!writers.TryGetValue(file, out var w))
				{
				w = new StreamWriter(ShellEnvironment.TranslatePath(file), false, ShellEncoding.Utf8) { NewLine = "\n", AutoFlush = true };
				writers[file] = w;
				}
			return w;
			}
		foreach (var c in prog.Cmds) { c.InRange = false; }
		string hold = "";
		int i = 0;                   // index of the current input line (0-based)
		int lineNo = 0;
		int exitCode = 0;
		var appendQueue = new List<string>();
		try
			{
			while (i < lines.Count)
				{
				string? ps = lines[i]; lineNo = i + 1; i++;
				bool isLast = i >= lines.Count;
				bool autoprint = !noAuto;
				bool substituted = false;
				appendQueue.Clear();
				int pc = 0;
				bool deleted = false, restartNoRead = false;
				while (pc < prog.Cmds.Count)
					{
					var cmd = prog.Cmds[pc];
					bool selected = SedSelect(cmd, lineNo, isLast, ps!, lines, i);
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
							int nl = ps!.IndexOf('\n');
							if (nl < 0) { deleted = true; break; }
							ps = ps[(nl + 1)..];
							restartNoRead = true;
							break;
							}
						case 'p': output.Write(ps + term); pc++; continue;
						case 'P': { int nl = ps!.IndexOf('\n'); output.Write((nl < 0 ? ps : ps[..nl]) + term); pc++; continue; }
						case 'n':
							{
							if (!noAuto) output.Write(ps + term);
							if (i >= lines.Count) { autoprint = false; deleted = true; break; }
							ps = lines[i]; lineNo = i + 1; i++; isLast = i >= lines.Count;
							pc++; continue;
							}
						case 'N':
							{
							if (i >= lines.Count) { if (!noAuto) output.Write(ps + term); autoprint = false; deleted = true; break; }
							ps = ps + "\n" + lines[i]; lineNo = i + 1; i++; isLast = i >= lines.Count;
							pc++; continue;
							}
						case 's':
							{
							var (res, changed) = SedSub(cmd, ps!);
							ps = res;
							if (changed)
								{
								substituted = true;
								if (cmd.SPrint) output.Write(ps + term);
								if (cmd.SWriteFile is not null) WriterFor(cmd.SWriteFile).Write(ps + term);
								}
							pc++; continue;
							}
						case 'y':
							{
							var sb = new StringBuilder(ps!.Length);
							foreach (var ch in ps) { int k = cmd.YFrom.IndexOf(ch); sb.Append(k >= 0 ? cmd.YTo[k] : ch); }
							ps = sb.ToString(); pc++; continue;
							}
						case 'a': appendQueue.Add(cmd.Text + "\n"); pc++; continue;
						case 'i': output.Write(cmd.Text + "\n"); pc++; continue;
						case 'c':
							{
							// with a range, print the text once at the end of the range
							bool endOfRange = cmd.A2 is null || !cmd.InRange || cmd.Negate;
							if (endOfRange) output.Write(cmd.Text + "\n");
							deleted = true; break;
							}
						case '=': output.Write(lineNo + "\n"); pc++; continue;
						case 'l': output.Write(SedVisible(ps!) + "\n"); pc++; continue;
						case 'h': hold = ps!; pc++; continue;
						case 'H': hold = hold + "\n" + ps; pc++; continue;
						case 'g': ps = hold; pc++; continue;
						case 'G': ps = ps + "\n" + hold; pc++; continue;
						case 'x': (ps, hold) = (hold, ps!); pc++; continue;
						case 'z': ps = ""; pc++; continue;
						case 'F': output.Write((fileName == "-" ? "-" : fileName) + "\n"); pc++; continue;
						case 'r':
							{
							try { appendQueue.Add(ShellEncoding.ReadAllText(ShellEnvironment.TranslatePath(cmd.Text))); } catch { }
							pc++; continue;
							}
						case 'w': WriterFor(cmd.Text).Write(ps + term); pc++; continue;
						case 'b': pc = cmd.Text.Length == 0 ? prog.Cmds.Count : cmd.JumpTo; continue;
						case 't': if (substituted) { substituted = false; pc = cmd.Text.Length == 0 ? prog.Cmds.Count : cmd.JumpTo; } else pc++; continue;
						case 'T': if (!substituted) { pc = cmd.Text.Length == 0 ? prog.Cmds.Count : cmd.JumpTo; } else { substituted = false; pc++; } continue;
						case 'q':
							{
							if (autoprint) output.Write(ps + term);
							foreach (var a in appendQueue) output.Write(a);
							return cmd.QCode;
							}
						case 'Q': return cmd.QCode;
						default: pc++; continue;
						}
					break;   // reached only via d/D/c/n-at-eof
					}
				if (!deleted && autoprint) output.Write(ps + term);
				foreach (var a in appendQueue) output.Write(a);
				if (restartNoRead)
					{
					// D with remaining text: restart the cycle without reading input
					i--; lines[i] = ps!;
					}
				}
			}
		finally { foreach (var w in writers.Values) w.Dispose(); }
		return exitCode;
		}

	private static bool SedSelect(SedCmd cmd, int lineNo, bool isLast, string ps, List<string> lines, int nextIndex)
		{
		if (cmd.A1 is null) return true;
		bool M(SedAddr a) => a.Kind switch
			{
			1 => lineNo == a.Num,
			2 => isLast,
			3 => a.Re!.IsMatch(ps),
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
			if (cmd.A2.Kind == 2) { cmd.InRange = !isLast; return true; }
			if (cmd.A2.Kind == 3)
				{
				// 0,/re/ lets the end match the very first line; addr1,/re/ starts checking on the next line
				if (cmd.A1.Kind == 5 && cmd.A2.Re!.IsMatch(ps)) { cmd.InRange = false; return true; }
				cmd.InRange = true; return true;
				}
			cmd.InRange = true; return true;
			}
		// inside a range: does this line end it?
		if (cmd.A2Plus || cmd.A2Tilde || cmd.A2.Kind == 1) { if (lineNo >= cmd.RangeEndLine) cmd.InRange = false; return true; }
		if (cmd.A2.Kind == 2) { if (isLast) cmd.InRange = false; return true; }
		if (cmd.A2.Kind == 3) { if (cmd.A2.Re!.IsMatch(ps)) cmd.InRange = false; return true; }
		return true;
		}

	private static string SedVisible(string s)
		{
		var sb = new StringBuilder();
		foreach (var ch in s)
			{
			switch (ch)
				{
				case '\\': sb.Append("\\\\"); break;
				case '\a': sb.Append("\\a"); break;
				case '\b': sb.Append("\\b"); break;
				case '\f': sb.Append("\\f"); break;
				case '\n': sb.Append("\\n"); break;
				case '\r': sb.Append("\\r"); break;
				case '\t': sb.Append("\\t"); break;
				case '\v': sb.Append("\\v"); break;
				default:
					if (ch < ' ' || ch > '~') foreach (var b in ShellEncoding.Utf8.GetBytes(ch.ToString())) sb.Append('\\').Append(Convert.ToString(b, 8).PadLeft(3, '0'));
					else sb.Append(ch);
					break;
				}
			}
		return sb.Append('$').ToString();
		}

	private static (string result, bool changed) SedSub(SedCmd cmd, string input)
		{
		var matches = cmd.SRe!.Matches(input);
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
