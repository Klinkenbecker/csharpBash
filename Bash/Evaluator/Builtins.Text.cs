using System.Text;

namespace Bash.Evaluator;

/// <summary>
/// Text coreutils with GNU option vocabulary and strict option parsing (an unknown option
/// raises <see cref="UnsupportedOptionException"/> → fall-through / loud, never ignored):
/// cat head tail wc rev tac tr cut uniq nl fold paste comm tee sort split od seq
/// base64 md5sum/sha1sum/sha256sum/sha512sum hexdump.
/// </summary>
public sealed partial class Builtins
	{
	// ── shared input helpers ────────────────────────────────────────────────────

	/// <summary>A reader over a file operand or stdin ("-"). Callers dispose file readers.</summary>
	private static TextReader OpenText(string src) =>
		src is "-" or "/dev/stdin" ? Console.In : new StreamReader(ShellEnvironment.TranslatePath(src), ShellEncoding.Utf8);

	private static IEnumerable<string> ReadLines(TextReader r)
		{
		string? l;
		while ((l = r.ReadLine()) is not null) yield return l;
		}

	private static byte[] ReadBytes(string src)
		{
		if (src == "-")
			{
			// bytes straight from the pipe/file behind stdin; text (here-doc) falls back to the reader
			var rin = Evaluator.CurrentRawStdin();
			if (rin is not null) { using var ms = new MemoryStream(); rin.CopyTo(ms); return ms.ToArray(); }
			return ShellEncoding.Utf8.GetBytes(Console.In.ReadToEnd());
			}
		return File.ReadAllBytes(ShellEnvironment.TranslatePath(src));
		}

	private static List<string> FilesOrStdin(Opts o) => o.Operands.Count == 0 ? ["-"] : o.Operands;

	private static byte[] ReadAll(Stream s) { using var ms = new MemoryStream(); s.CopyTo(ms); return ms.ToArray(); }

	internal static string IoError(Exception ex) => ex switch
		{
		FileNotFoundException or DirectoryNotFoundException => "No such file or directory",
		UnauthorizedAccessException => "Permission denied",
		_ => ex.Message,
		};

	private static long ParseCount(string tool, string v, char opt)
		{
		// GNU size suffixes: b=512, k/K=1024, m/M, g/G, and kB/MB (1000-based)
		var s = v.Trim();
		long mult = 1;
		if (s.EndsWith("kB")) { mult = 1000; s = s[..^2]; }
		else if (s.EndsWith("MB")) { mult = 1000_000; s = s[..^2]; }
		else if (s.EndsWith("GB")) { mult = 1000_000_000; s = s[..^2]; }
		else if (s.Length > 0 && char.IsLetter(s[^1]))
			{
			mult = char.ToLowerInvariant(s[^1]) switch { 'b' => 512, 'k' => 1024, 'm' => 1024L * 1024, 'g' => 1024L * 1024 * 1024, _ => -1 };
			if (mult < 0) throw new UnsupportedOptionException(tool, $"-{opt} {v}", "invalid number");
			s = s[..^1];
			}
		if (!long.TryParse(s, out var n)) throw new UnsupportedOptionException(tool, $"-{opt} {v}", "invalid number");
		return n * mult;
		}

	// ── cat ─────────────────────────────────────────────────────────────────────

	private static int Cat(List<string> args)
		{
		var o = Opts.Parse("cat", args, "nbsAETvetu", ["number:n", "number-nonblank:b", "squeeze-blank:s", "show-all:A", "show-ends:E", "show-tabs:T", "show-nonprinting:v"]);
		bool number = o.Has('n'), numberNonblank = o.Has('b'), squeeze = o.Has('s');
		bool showAll = o.Has('A'), showEnds = o.Has('E') || showAll || o.Has('e'), showTabs = o.Has('T') || showAll || o.Has('t');
		bool showNonprint = o.Has('v') || showAll || o.Has('e') || o.Has('t');
		bool formatting = number || numberNonblank || squeeze || showEnds || showTabs || showNonprint;
		var files = FilesOrStdin(o);
		int rc = 0;

		if (!formatting)
			{
			// byte-faithful copy (binary safe) through the raw sink when there is one
			var raw = Evaluator.CurrentRawStdout();
			if (raw is not null) Console.Out.Flush();
			foreach (var f in files)
				{
				try
					{
					if (f == "-")
						{
						var rin = Evaluator.CurrentRawStdin();
						if (rin is not null && raw is not null) { rin.CopyTo(raw); continue; }   // bytes in, bytes out
						var s = rin is not null ? ShellEncoding.Utf8.GetString(ReadAll(rin)) : Console.In.ReadToEnd();
						if (raw is not null) { var b = ShellEncoding.Utf8.GetBytes(s); raw.Write(b, 0, b.Length); }
						else Console.Out.Write(s);
						continue;
						}
					var p = ShellEnvironment.TranslatePath(f);
					if (Directory.Exists(p)) { Console.Error.WriteLine($"cat: {f}: Is a directory"); rc = 1; continue; }
					if (raw is not null) { using var fs = File.OpenRead(p); fs.CopyTo(raw); }
					else Console.Out.Write(ShellEncoding.ReadAllText(p));
					}
				catch (BrokenPipeException) { throw; }
				catch (Exception ex) { Console.Error.WriteLine($"cat: {f}: {IoError(ex)}"); rc = 1; }
				}
			raw?.Flush();
			return rc;
			}

		long n = 0; bool prevBlank = false;
		foreach (var f in files)
			{
			TextReader? r = null;
			try
				{
				r = OpenText(f);
				foreach (var raw in ReadLines(r))
					{
					var line = raw;
					bool blank = line.Length == 0;
					if (squeeze && blank && prevBlank) continue;
					prevBlank = blank;
					if (showNonprint) line = ShowNonprinting(line, showTabs);
					else if (showTabs) line = line.Replace("\t", "^I");
					if (showEnds) line += "$";
					if (numberNonblank) { if (!blank) line = $"{++n,6}\t{line}"; }
					else if (number) line = $"{++n,6}\t{line}";
					Console.WriteLine(line);
					}
				}
			catch (BrokenPipeException) { throw; }
			catch (Exception ex) { Console.Error.WriteLine($"cat: {f}: {IoError(ex)}"); rc = 1; }
			finally { if (f != "-") r?.Dispose(); }
			}
		return rc;
		}

	private static string ShowNonprinting(string s, bool tabs)
		{
		var sb = new StringBuilder();
		foreach (var ch in s)
			{
			if (ch == '\t') { sb.Append(tabs ? "^I" : "\t"); continue; }
			if (ch < ' ') { sb.Append('^').Append((char)(ch + 64)); continue; }
			if (ch == '\x7f') { sb.Append("^?"); continue; }
			sb.Append(ch);
			}
		return sb.ToString();
		}

	// ── head / tail ─────────────────────────────────────────────────────────────

	private static int Head(List<string> args)
		{
		var o = Opts.Parse("head", args, "n:c:qvz", ["lines=:n", "bytes=:c", "quiet:q", "silent:q", "verbose:v", "zero-terminated:z"], allowNumeric: true);
		var files = FilesOrStdin(o);
		long count = 10; bool bytes = false, fromEnd = false;
		if (o.Numeric is not null) count = long.Parse(o.Numeric);
		if (o.Get('n') is string nv) { fromEnd = nv.StartsWith('-'); count = ParseCount("head", nv.TrimStart('-', '+'), 'n'); }
		if (o.Get('c') is string cv) { bytes = true; fromEnd = cv.StartsWith('-'); count = ParseCount("head", cv.TrimStart('-', '+'), 'c'); }
		bool header = o.Has('v') || (files.Count > 1 && !o.Has('q'));
		int rc = 0; bool first = true;
		foreach (var f in files)
			{
			try
				{
				if (header) { if (!first) Console.WriteLine(); Console.WriteLine($"==> {(f == "-" ? "standard input" : f)} <=="); }
				first = false;
				if (bytes)
					{
					var data = ReadBytes(f);
					long take = fromEnd ? Math.Max(0, data.Length - count) : Math.Min(count, data.Length);
					var raw = Evaluator.CurrentRawStdout();
					if (raw is not null) { Console.Out.Flush(); raw.Write(data, 0, (int)take); raw.Flush(); }
					else Console.Out.Write(ShellEncoding.Utf8.GetString(data, 0, (int)take));
					continue;
					}
				if (o.Has('z'))
					{
					// -z: NUL-terminated records (was accepted and ignored until 2026-09-05 — a silent wrong answer)
					var (recs, term) = ZeroRecords(f);
					int end = fromEnd ? (int)Math.Max(0, recs.Count - count) : (int)Math.Min(count, recs.Count);
					WriteZeroRecords(recs, 0, end, term);
					continue;
					}
				if (fromEnd)
					{
					// all but the last N lines
					using var r0 = f == "-" ? null : OpenText(f);
					var all = ReadLines(r0 ?? Console.In).ToList();
					for (int i = 0; i < all.Count - count; i++) Console.WriteLine(all[i]);
					continue;
					}
				// Stream: stop after n lines so an infinite/slow producer is released (SIGPIPE).
				TextReader reader = OpenText(f);
				try
					{
					for (long i = 0; i < count; i++)
						{
						var line = reader.ReadLine();
						if (line is null) break;
						Console.WriteLine(line);
						}
					}
				finally { if (f != "-") reader.Dispose(); }
				}
			catch (BrokenPipeException) { throw; }
			catch (Exception ex) { Console.Error.WriteLine($"head: cannot open '{f}' for reading: {IoError(ex)}"); rc = 1; }
			}
		return rc;
		}

	private int Tail(List<string> args)
		{
		var o = Opts.Parse("tail", args, "n:c:qvfFs:z", ["lines=:n", "bytes=:c", "quiet:q", "silent:q", "verbose:v", "follow:f", "sleep-interval=:s", "zero-terminated:z"], allowNumeric: true);
		var files = FilesOrStdin(o);
		long count = 10; bool bytes = false, fromStart = false;
		if (o.Numeric is not null) count = long.Parse(o.Numeric);
		if (o.Get('n') is string nv) { fromStart = nv.StartsWith('+'); count = ParseCount("tail", nv.TrimStart('-', '+'), 'n'); }
		if (o.Get('c') is string cv) { bytes = true; fromStart = cv.StartsWith('+'); count = ParseCount("tail", cv.TrimStart('-', '+'), 'c'); }
		bool follow = o.Has('f') || o.Has('F');
		double interval = double.TryParse(o.Get('s'), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var iv) ? iv : 1.0;
		bool header = o.Has('v') || (files.Count > 1 && !o.Has('q'));
		int rc = 0; bool first = true;
		foreach (var f in files)
			{
			try
				{
				if (header) { if (!first) Console.WriteLine(); Console.WriteLine($"==> {(f == "-" ? "standard input" : f)} <=="); }
				first = false;
				if (bytes)
					{
					var data = ReadBytes(f);
					int start = fromStart ? (int)Math.Min(Math.Max(0, count - 1), data.Length) : (int)Math.Max(0, data.Length - count);
					var raw = Evaluator.CurrentRawStdout();
					if (raw is not null) { Console.Out.Flush(); raw.Write(data, start, data.Length - start); raw.Flush(); }
					else Console.Out.Write(ShellEncoding.Utf8.GetString(data, start, data.Length - start));
					continue;
					}
				if (o.Has('z'))
					{
					var (recs, term) = ZeroRecords(f);
					int from = fromStart ? (int)Math.Min(Math.Max(0, count - 1), recs.Count) : (int)Math.Max(0, recs.Count - count);
					WriteZeroRecords(recs, from, recs.Count, term);
					continue;
					}
				var lines = new List<string>();
				long lengthSeen = 0;
				if (f == "-") lines = ReadLines(Console.In).ToList();
				else
					{
					var p = ShellEnvironment.TranslatePath(f);
					using var fs = new FileStream(p, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
					using var r = new StreamReader(fs, ShellEncoding.Utf8);
					lines = ReadLines(r).ToList();
					lengthSeen = fs.Length;
					}
				if (fromStart) { for (long i = Math.Max(0, count - 1); i < lines.Count; i++) Console.WriteLine(lines[(int)i]); }
				else for (int i = (int)Math.Max(0, lines.Count - count); i < lines.Count; i++) Console.WriteLine(lines[i]);
				if (follow && f != "-")
					{
					// poll for growth until interrupted (Ctrl+C) or the reader goes away
					var p = ShellEnvironment.TranslatePath(f);
					while (true)
						{
						Thread.Sleep((int)(interval * 1000));
						_eval.CheckInterrupt();
						long len;
						try { len = new FileInfo(p).Length; } catch { continue; }
						if (len < lengthSeen) lengthSeen = 0;      // truncated: start over
						if (len == lengthSeen) continue;
						using var fs = new FileStream(p, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
						fs.Seek(lengthSeen, SeekOrigin.Begin);
						using var r = new StreamReader(fs, ShellEncoding.Utf8);
						Console.Out.Write(r.ReadToEnd());
						Console.Out.Flush();
						lengthSeen = len;
						}
					}
				}
			catch (BrokenPipeException) { throw; }
			catch (Exception ex) { Console.Error.WriteLine($"tail: cannot open '{f}' for reading: {IoError(ex)}"); rc = 1; }
			}
		return rc;
		}

	/// <summary>`-z` input: NUL-terminated records. A final record without its NUL is still a
	/// record, and is written back without one (GNU behaviour).</summary>
	private static (List<string> recs, bool lastTerminated) ZeroRecords(string f)
		{
		var data = ReadBytes(f);
		var recs = new List<string>();
		int start = 0;
		for (int i = 0; i < data.Length; i++)
			if (data[i] == 0) { recs.Add(ShellEncoding.Utf8.GetString(data, start, i - start)); start = i + 1; }
		bool lastTerminated = true;
		if (start < data.Length) { recs.Add(ShellEncoding.Utf8.GetString(data, start, data.Length - start)); lastTerminated = false; }
		return (recs, lastTerminated);
		}

	private static void WriteZeroRecords(List<string> recs, int from, int toExclusive, bool lastTerminated)
		{
		for (int i = from; i < toExclusive; i++)
			{
			Console.Out.Write(recs[i]);
			if (i < recs.Count - 1 || lastTerminated) Console.Out.Write('\0');
			}
		}

	// ── wc ──────────────────────────────────────────────────────────────────────

	private static int Wc(List<string> args)
		{
		var o = Opts.Parse("wc", args, "lwcmL", ["lines:l", "words:w", "bytes:c", "chars:m", "max-line-length:L"]);
		bool cl = o.Has('l'), cw = o.Has('w'), cc = o.Has('c'), cm = o.Has('m'), cL = o.Has('L');
		if (!cl && !cw && !cc && !cm && !cL) { cl = cw = cc = true; }
		var files = FilesOrStdin(o);
		bool multi = files.Count > 1;
		long tl = 0, tw = 0, tc = 0, tm = 0, tL = 0; int rc = 0;

		// GNU column width: the digit count of the summed sizes of the regular-file operands;
		// stdin gets no padding.
		long sizeSum = 0;
		foreach (var f in files)
			{
			if (f == "-") continue;
			try { sizeSum += new FileInfo(ShellEnvironment.TranslatePath(f)).Length; } catch { }
			}
		int width = Math.Max(1, sizeSum.ToString().Length);

		void Emit(long l, long w, long c, long m, long L, string name)
			{
			var parts = new List<string>();
			if (cl) parts.Add(l.ToString().PadLeft(width));
			if (cw) parts.Add(w.ToString().PadLeft(width));
			if (cm) parts.Add(m.ToString().PadLeft(width));
			if (cc) parts.Add(c.ToString().PadLeft(width));
			if (cL) parts.Add(L.ToString().PadLeft(width));
			Console.WriteLine(string.Join(" ", parts) + (name.Length > 0 ? " " + name : ""));
			}

		foreach (var f in files)
			{
			try
				{
				byte[] data = ReadBytes(f);
				string s = ShellEncoding.Utf8.GetString(data);
				long lines = 0, maxLen = 0, cur = 0;
				foreach (var ch in s) { if (ch == '\n') { lines++; if (cur > maxLen) maxLen = cur; cur = 0; } else cur++; }
				if (cur > maxLen) maxLen = cur;
				long words = s.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).Length;
				tl += lines; tw += words; tc += data.Length; tm += s.Length; if (maxLen > tL) tL = maxLen;
				Emit(lines, words, data.Length, s.Length, maxLen, f == "-" ? "" : f);
				}
			catch (Exception ex) { Console.Error.WriteLine($"wc: {f}: {IoError(ex)}"); rc = 1; }
			}
		if (multi) Emit(tl, tw, tc, tm, tL, "total");
		return rc;
		}

	// ── rev / tac ───────────────────────────────────────────────────────────────

	private static int Rev(List<string> args)
		{
		var o = Opts.Parse("rev", args, "");
		int rc = 0;
		foreach (var f in FilesOrStdin(o))
			{
			TextReader? r = null;
			try { r = OpenText(f); foreach (var line in ReadLines(r)) { var a = line.ToCharArray(); Array.Reverse(a); Console.WriteLine(new string(a)); } }
			catch (BrokenPipeException) { throw; }
			catch (Exception ex) { Console.Error.WriteLine($"rev: cannot open {f}: {IoError(ex)}"); rc = 1; }
			finally { if (f != "-") r?.Dispose(); }
			}
		return rc;
		}

	private static int Tac(List<string> args)
		{
		var o = Opts.Parse("tac", args, "");
		int rc = 0;
		foreach (var f in FilesOrStdin(o))
			{
			TextReader? r = null;
			try { r = OpenText(f); var lines = ReadLines(r).ToList(); for (int i = lines.Count - 1; i >= 0; i--) Console.WriteLine(lines[i]); }
			catch (BrokenPipeException) { throw; }
			catch (Exception ex) { Console.Error.WriteLine($"tac: failed to open '{f}' for reading: {IoError(ex)}"); rc = 1; }
			finally { if (f != "-") r?.Dispose(); }
			}
		return rc;
		}

	// ── tr ──────────────────────────────────────────────────────────────────────

	private static int Tr(List<string> args)
		{
		var o = Opts.Parse("tr", args, "cCdst", ["complement:c", "delete:d", "squeeze-repeats:s", "truncate-set1:t"]);
		bool complement = o.Has('c') || o.Has('C'), del = o.Has('d'), squeeze = o.Has('s'), truncate = o.Has('t');
		if (o.Operands.Count == 0) { Console.Error.WriteLine("tr: missing operand"); return 1; }
		string s1 = ExpandTrSet(o.Operands[0]);
		string s2 = o.Operands.Count > 1 ? ExpandTrSet(o.Operands[1]) : "";
		if (!del && o.Operands.Count < 2 && !squeeze) { Console.Error.WriteLine("tr: missing operand after '" + o.Operands[0] + "'"); return 1; }
		string input = Console.In.ReadToEnd();
		var sb = new StringBuilder(input.Length);
		var set1 = new HashSet<char>(s1);
		bool In1(char c) => complement ? !set1.Contains(c) : set1.Contains(c);

		if (del)
			{
			var sq = squeeze ? new HashSet<char>(s2) : null;
			char? last = null;
			foreach (char c in input)
				{
				if (In1(c)) continue;
				if (sq is not null && last == c && sq.Contains(c)) continue;
				sb.Append(c); last = c;
				}
			}
		else if (o.Operands.Count < 2)
			{
			// -s alone: squeeze repeats of SET1 characters
			char? last = null;
			foreach (char c in input) { if (In1(c) && last == c) continue; sb.Append(c); last = c; }
			}
		else
			{
			char? last = null;
			if (complement)
				{
				// every char NOT in set1 maps to the last char of set2
				char to = s2.Length > 0 ? s2[^1] : ' ';
				foreach (char c in input)
					{
					char oc = set1.Contains(c) ? c : to;
					if (squeeze && !set1.Contains(c) && last == oc) continue;
					sb.Append(oc); last = oc;
					}
				}
			else
				{
				var map = new Dictionary<char, char>();
				int limit = truncate ? Math.Min(s1.Length, s2.Length) : s1.Length;
				for (int i = 0; i < limit; i++)
					map[s1[i]] = i < s2.Length ? s2[i] : (s2.Length > 0 ? s2[^1] : s1[i]);
				foreach (char c in input)
					{
					char oc = map.TryGetValue(c, out var m) ? m : c;
					if (squeeze && last == oc && map.ContainsKey(c)) continue;
					sb.Append(oc); last = oc;
					}
				}
			}
		Console.Out.Write(sb.ToString());
		return 0;
		}

	private static string ExpandTrSet(string s)
		{
		var sb = new StringBuilder();
		int i = 0;
		while (i < s.Length)
			{
			if (s[i] == '\\' && i + 1 < s.Length)
				{
				if (s[i + 1] >= '0' && s[i + 1] <= '7')
					{
					int k = i + 1, v = 0, n = 0;
					while (k < s.Length && n < 3 && s[k] >= '0' && s[k] <= '7') { v = v * 8 + (s[k] - '0'); k++; n++; }
					sb.Append((char)v); i = k; continue;
					}
				sb.Append(s[i + 1] switch { 'n' => '\n', 't' => '\t', 'r' => '\r', '\\' => '\\', 'a' => '\a', 'b' => '\b', 'f' => '\f', 'v' => '\v', var x => x });
				i += 2;
				}
			else if (s[i] == '[' && i + 1 < s.Length && s[i + 1] == ':')
				{
				int end = s.IndexOf(":]", i, StringComparison.Ordinal);
				if (end > 0) { sb.Append(TrClass(s[(i + 2)..end])); i = end + 2; }
				else sb.Append(s[i++]);
				}
			else if (s[i] == '[' && i + 2 < s.Length && s[i + 2] == '*')
				{
				// [c*n] : n copies of c (n omitted = enough to pad set2)
				int end = s.IndexOf(']', i);
				if (end > 0)
					{
					var spec = s[(i + 3)..end];
					int n = int.TryParse(spec, out var nn) ? nn : 1;
					sb.Append(s[i + 1], Math.Max(1, n));
					i = end + 1;
					}
				else sb.Append(s[i++]);
				}
			else if (i + 2 < s.Length && s[i + 1] == '-')
				{ for (char c = s[i]; c <= s[i + 2]; c++) sb.Append(c); i += 3; }
			else sb.Append(s[i++]);
			}
		return sb.ToString();
		}

	private static string TrClass(string cls) => cls switch
		{
		"upper" => "ABCDEFGHIJKLMNOPQRSTUVWXYZ",
		"lower" => "abcdefghijklmnopqrstuvwxyz",
		"digit" => "0123456789",
		"alpha" => "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz",
		"alnum" => "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz",
		"space" => " \t\n\r\f\v",
		"blank" => " \t",
		"punct" => "!\"#$%&'()*+,-./:;<=>?@[\\]^_`{|}~",
		"xdigit" => "0123456789ABCDEFabcdef",
		"cntrl" => new string(Enumerable.Range(0, 32).Select(c => (char)c).Append('\x7f').ToArray()),
		"print" => new string(Enumerable.Range(32, 95).Select(c => (char)c).ToArray()),
		"graph" => new string(Enumerable.Range(33, 94).Select(c => (char)c).ToArray()),
		_        => "",
		};

	// ── cut ─────────────────────────────────────────────────────────────────────

	private static int Cut(List<string> args)
		{
		var o = Opts.Parse("cut", args, "b:c:f:d:sz", ["bytes=:b", "characters=:c", "fields=:f", "delimiter=:d", "only-delimited:s", "complement", "output-delimiter=", "zero-terminated:z"]);
		string? byteList = o.Get('b'), charList = o.Get('c'), fieldList = o.Get('f');
		int modes = (byteList is null ? 0 : 1) + (charList is null ? 0 : 1) + (fieldList is null ? 0 : 1);
		if (modes == 0) { Console.Error.WriteLine("cut: you must specify a list of bytes, characters, or fields"); return 1; }
		if (modes > 1) { Console.Error.WriteLine("cut: only one type of list may be specified"); return 1; }
		char delim = '\t';
		if (o.Get('d') is string d)
			{
			if (d.Length != 1) { Console.Error.WriteLine("cut: the delimiter must be a single character"); return 1; }
			delim = d[0];
			}
		bool complement = o.HasLong("complement"), onlyDelimited = o.Has('s');
		string outDelim = o.GetLong("output-delimiter") ?? (fieldList is not null ? delim.ToString() : "");
		var ranges = ParseRanges(byteList ?? charList ?? fieldList!);
		int rc = 0;
		foreach (var f in FilesOrStdin(o))
			{
			TextReader? r = null;
			try
				{
				r = OpenText(f);
				foreach (var line in ReadLines(r))
					{
					if (fieldList is null)
						{
						var sb = new StringBuilder();
						for (int k = 0; k < line.Length; k++)
							if (InRanges(ranges, k + 1) != complement) sb.Append(line[k]);
						Console.WriteLine(sb.ToString());
						}
					else
						{
						if (!line.Contains(delim)) { if (!onlyDelimited) Console.WriteLine(line); continue; }
						var parts = line.Split(delim);
						var sel = new List<string>();
						for (int k = 0; k < parts.Length; k++) if (InRanges(ranges, k + 1) != complement) sel.Add(parts[k]);
						Console.WriteLine(string.Join(outDelim, sel));
						}
					}
				}
			catch (BrokenPipeException) { throw; }
			catch (Exception ex) { Console.Error.WriteLine($"cut: {f}: {IoError(ex)}"); rc = 1; }
			finally { if (f != "-") r?.Dispose(); }
			}
		return rc;
		}

	private static List<(int lo, int hi)> ParseRanges(string list)
		{
		var r = new List<(int, int)>();
		foreach (var part in list.Split(','))
			{
			if (part.Length == 0) continue;
			int dash = part.IndexOf('-');
			if (dash < 0) { if (int.TryParse(part, out var v)) r.Add((v, v)); }
			else
				{
				int lo = dash == 0 ? 1 : int.Parse(part[..dash]);
				int hi = dash == part.Length - 1 ? int.MaxValue : int.Parse(part[(dash + 1)..]);
				r.Add((lo, hi));
				}
			}
		return r;
		}

	private static bool InRanges(List<(int lo, int hi)> r, int k)
		{
		foreach (var (lo, hi) in r) if (k >= lo && k <= hi) return true;
		return false;
		}

	// ── uniq ────────────────────────────────────────────────────────────────────

	private static int Uniq(List<string> args)
		{
		var o = Opts.Parse("uniq", args, "cduif:s:w:z", ["count:c", "repeated:d", "unique:u", "ignore-case:i", "skip-fields=:f", "skip-chars=:s", "check-chars=:w", "zero-terminated:z", "all-repeated:D"]);
		bool count = o.Has('c'), onlyD = o.Has('d') || o.Has('D'), onlyU = o.Has('u'), icase = o.Has('i');
		int skipFields = o.GetInt('f', 0), skipChars = o.GetInt('s', 0), checkChars = o.GetInt('w', 0);
		string input = o.Operands.Count > 0 ? o.Operands[0] : "-";
		string? output = o.Operands.Count > 1 ? o.Operands[1] : null;

		string KeyOf(string s)
			{
			var k = s;
			for (int i = 0; i < skipFields; i++)
				{
				int p = 0;
				while (p < k.Length && char.IsWhiteSpace(k[p])) p++;
				while (p < k.Length && !char.IsWhiteSpace(k[p])) p++;
				k = k[p..];
				}
			if (skipChars > 0) k = skipChars < k.Length ? k[skipChars..] : "";
			if (checkChars > 0 && k.Length > checkChars) k = k[..checkChars];
			return icase ? k.ToLowerInvariant() : k;
			}

		TextReader? r = null;
		TextWriter w = Console.Out; StreamWriter? fw = null;
		try
			{
			r = OpenText(input);
			if (output is not null) { fw = new StreamWriter(ShellEnvironment.TranslatePath(output), false, ShellEncoding.Utf8) { NewLine = "\n" }; w = fw; }
			var lines = ReadLines(r).ToList();
			int i = 0;
			while (i < lines.Count)
				{
				var key = KeyOf(lines[i]);
				int j = i + 1;
				while (j < lines.Count && KeyOf(lines[j]) == key) j++;
				int n = j - i;
				bool show = onlyD ? n > 1 : onlyU ? n == 1 : true;
				if (show) w.WriteLine(count ? $"{n,7} {lines[i]}" : lines[i]);
				i = j;
				}
			}
		catch (BrokenPipeException) { throw; }
		catch (Exception ex) { Console.Error.WriteLine($"uniq: {input}: {IoError(ex)}"); return 1; }
		finally { if (input != "-") r?.Dispose(); fw?.Dispose(); }
		return 0;
		}

	// ── nl ──────────────────────────────────────────────────────────────────────

	private static int Nl(List<string> args)
		{
		var o = Opts.Parse("nl", args, "b:n:w:s:v:i:p", ["body-numbering=:b", "number-format=:n", "number-width=:w", "number-separator=:s", "starting-line-number=:v", "line-increment=:i"]);
		string style = o.Get('b') ?? "t", fmt = o.Get('n') ?? "rn", sep = o.Get('s') ?? "\t";
		int width = o.GetInt('w', 6), n = o.GetInt('v', 1) - o.GetInt('i', 1), incr = o.GetInt('i', 1);
		System.Text.RegularExpressions.Regex? bodyRe = style.StartsWith('p') ? new System.Text.RegularExpressions.Regex(BreToNet(style[1..])) : null;
		int rc = 0;
		foreach (var f in FilesOrStdin(o))
			{
			TextReader? r = null;
			try
				{
				r = OpenText(f);
				foreach (var line in ReadLines(r))
					{
					bool number = style[0] switch { 'a' => true, 'n' => false, 'p' => bodyRe!.IsMatch(line), _ => line.Length > 0 };
					if (!number) { Console.WriteLine(new string(' ', width) + (style[0] == 'n' || line.Length == 0 ? "" : sep) + line); continue; }
					n += incr;
					var num = fmt switch { "ln" => n.ToString().PadRight(width), "rz" => n.ToString().PadLeft(width, '0'), _ => n.ToString().PadLeft(width) };
					Console.WriteLine(num + sep + line);
					}
				}
			catch (BrokenPipeException) { throw; }
			catch (Exception ex) { Console.Error.WriteLine($"nl: {f}: {IoError(ex)}"); rc = 1; }
			finally { if (f != "-") r?.Dispose(); }
			}
		return rc;
		}

	// ── fold ────────────────────────────────────────────────────────────────────

	private static int Fold(List<string> args)
		{
		var o = Opts.Parse("fold", args, "w:sb", ["width=:w", "spaces:s", "bytes:b"], allowNumeric: true);
		int w = o.Numeric is not null ? int.Parse(o.Numeric) : o.GetInt('w', 80);
		bool atSpaces = o.Has('s');
		if (w < 1) w = 1;
		int rc = 0;
		foreach (var f in FilesOrStdin(o))
			{
			TextReader? r = null;
			try
				{
				r = OpenText(f);
				foreach (var line in ReadLines(r))
					{
					if (line.Length == 0) { Console.WriteLine(); continue; }
					int i = 0;
					while (i < line.Length)
						{
						int len = Math.Min(w, line.Length - i);
						if (atSpaces && i + len < line.Length)
							{
							int sp = line.LastIndexOf(' ', i + len - 1, len);
							if (sp > i) len = sp - i + 1;
							}
						Console.WriteLine(line.Substring(i, len));
						i += len;
						}
					}
				}
			catch (BrokenPipeException) { throw; }
			catch (Exception ex) { Console.Error.WriteLine($"fold: {f}: {IoError(ex)}"); rc = 1; }
			finally { if (f != "-") r?.Dispose(); }
			}
		return rc;
		}

	// ── paste / comm ────────────────────────────────────────────────────────────

	private static int Paste(List<string> args)
		{
		var o = Opts.Parse("paste", args, "d:sz", ["delimiters=:d", "serial:s", "zero-terminated:z"]);
		string delims = o.Get('d') is string d ? UnescapeDelims(d) : "\t";
		if (delims.Length == 0) delims = "\t";
		bool serial = o.Has('s');
		var files = FilesOrStdin(o);
		var cols = new List<List<string>>();
		foreach (var f in files)
			{
			TextReader? r = null;
			try { r = OpenText(f); cols.Add(ReadLines(r).ToList()); }
			catch (Exception ex) { Console.Error.WriteLine($"paste: {f}: {IoError(ex)}"); return 1; }
			finally { if (f != "-") r?.Dispose(); }
			}
		if (serial)
			{
			foreach (var col in cols)
				{
				var sb = new StringBuilder();
				for (int i = 0; i < col.Count; i++) { if (i > 0) sb.Append(delims[(i - 1) % delims.Length]); sb.Append(col[i]); }
				Console.WriteLine(sb.ToString());
				}
			return 0;
			}
		int max = cols.Count == 0 ? 0 : cols.Max(c => c.Count);
		for (int row = 0; row < max; row++)
			{
			var sb = new StringBuilder();
			for (int c = 0; c < cols.Count; c++)
				{
				if (c > 0) sb.Append(delims[(c - 1) % delims.Length]);
				if (row < cols[c].Count) sb.Append(cols[c][row]);
				}
			Console.WriteLine(sb.ToString());
			}
		return 0;
		}

	private static string UnescapeDelims(string s)
		{
		var sb = new StringBuilder();
		for (int i = 0; i < s.Length; i++)
			if (s[i] == '\\' && i + 1 < s.Length) sb.Append(s[++i] switch { 't' => '\t', 'n' => '\n', '\\' => '\\', '0' => '\0', var x => x });
			else sb.Append(s[i]);
		return sb.ToString();
		}

	private static int Comm(List<string> args)
		{
		var o = Opts.Parse("comm", args, "123iz", ["check-order", "nocheck-order", "output-delimiter=", "total", "zero-terminated:z"]);
		bool s1 = o.Has('1'), s2 = o.Has('2'), s3 = o.Has('3'), icase = o.Has('i');
		string od = o.GetLong("output-delimiter") ?? "\t";
		if (o.Operands.Count < 2) { Console.Error.WriteLine("comm: missing operand"); return 1; }
		List<string> A, B;
		try
			{
			using var ra = OpenText(o.Operands[0]); A = ReadLines(ra).ToList();
			using var rb = OpenText(o.Operands[1]); B = ReadLines(rb).ToList();
			}
		catch (Exception ex) { Console.Error.WriteLine($"comm: {IoError(ex)}"); return 1; }
		string p2 = s1 ? "" : od;
		string p3 = (s1 ? "" : od) + (s2 ? "" : od);
		var cmp = icase ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
		int i = 0, j = 0;
		while (i < A.Count && j < B.Count)
			{
			int c = cmp.Compare(A[i], B[j]);
			if      (c < 0) { if (!s1) Console.WriteLine(A[i]); i++; }
			else if (c > 0) { if (!s2) Console.WriteLine(p2 + B[j]); j++; }
			else            { if (!s3) Console.WriteLine(p3 + A[i]); i++; j++; }
			}
		while (i < A.Count) { if (!s1) Console.WriteLine(A[i]); i++; }
		while (j < B.Count) { if (!s2) Console.WriteLine(p2 + B[j]); j++; }
		return 0;
		}

	// ── tee ─────────────────────────────────────────────────────────────────────

	private static int Tee(List<string> args)
		{
		var o = Opts.Parse("tee", args, "aip", ["append:a", "ignore-interrupts:i", "output-error="]);
		bool append = o.Has('a');
		var files = new List<FileStream>();
		int rc = 0;
		foreach (var f in o.Operands)
			{
			try
				{
				var p = ShellEnvironment.TranslatePath(f);
				files.Add(append ? File.Open(p, FileMode.Append, FileAccess.Write) : File.Create(p));
				}
			catch (Exception ex) { Console.Error.WriteLine($"tee: {f}: {IoError(ex)}"); rc = 1; }
			}
		try
			{
			// Stream: copy as it arrives so `cmd | tee log` shows progress. Bytes when stdin is a
			// pipe or file (binary-safe), characters only for a text source (here-doc).
			var rin = Evaluator.CurrentRawStdin();
			if (rin is not null)
				{
				var rout = Evaluator.CurrentRawStdout();
				if (rout is not null) Console.Out.Flush();
				var buf = new byte[8192];
				int n;
				while ((n = rin.Read(buf, 0, buf.Length)) > 0)
					{
					if (rout is not null) { rout.Write(buf, 0, n); rout.Flush(); }
					else { Console.Out.Write(ShellEncoding.Utf8.GetString(buf, 0, n)); Console.Out.Flush(); }
					foreach (var fs in files) fs.Write(buf, 0, n);
					}
				}
			else
				{
				var buf = new char[4096];
				int n;
				while ((n = Console.In.Read(buf, 0, buf.Length)) > 0)
					{
					Console.Out.Write(buf, 0, n);
					Console.Out.Flush();
					var b = ShellEncoding.Utf8.GetBytes(buf, 0, n);
					foreach (var fs in files) fs.Write(b, 0, b.Length);
					}
				}
			}
		finally { foreach (var fs in files) { try { fs.Flush(); } catch { } fs.Dispose(); } }
		return rc;
		}

	// ── sort ────────────────────────────────────────────────────────────────────

	private sealed record SortKey(int Start, int StartChar, int End, int EndChar, bool Numeric, bool Human, bool Version, bool Reverse, bool Fold, bool IgnoreBlanks, bool Dictionary);

	private static int Sort(List<string> args)
		{
		var o = Opts.Parse("sort", args, "nrufbsVhdk:t:o:cmzRi",
			["numeric-sort:n", "reverse:r", "unique:u", "ignore-case:f", "ignore-leading-blanks:b", "stable:s",
			 "version-sort:V", "human-numeric-sort:h", "dictionary-order:d", "key=:k", "field-separator=:t", "output=:o",
			 "check:c", "merge:m", "zero-terminated:z", "random-sort:R", "ignore-nonprinting:i", "sort=", "parallel=", "buffer-size=", "temporary-directory="]);
		bool gNumeric = o.Has('n'), gReverse = o.Has('r'), unique = o.Has('u'), gFold = o.Has('f'), gBlanks = o.Has('b'),
		     gVersion = o.Has('V'), gHuman = o.Has('h'), gDict = o.Has('d'), check = o.Has('c'), random = o.Has('R');
		if (o.GetLong("sort") is string sm) { if (sm == "numeric") gNumeric = true; else if (sm == "version") gVersion = true; else if (sm == "human-numeric") gHuman = true; else if (sm == "random") random = true; }
		char? delim = null;
		if (o.Get('t') is string t)
			{
			var td = UnescapeDelims(t);
			if (td.Length != 1) { Console.Error.WriteLine($"sort: multi-character tab '{t}'"); return 2; }
			delim = td[0];
			}
		var keys = new List<SortKey>();
		foreach (var kd in o.All('k'))
			{
			var parts = kd.Split(',');
			(int f, int c, string opts) ParseSpec(string spec)
				{
				int p = 0; var num = new StringBuilder();
				while (p < spec.Length && char.IsDigit(spec[p])) num.Append(spec[p++]);
				int field = num.Length > 0 ? int.Parse(num.ToString()) : 0;
				int ch = 0;
				if (p < spec.Length && spec[p] == '.') { p++; var cn = new StringBuilder(); while (p < spec.Length && char.IsDigit(spec[p])) cn.Append(spec[p++]); ch = cn.Length > 0 ? int.Parse(cn.ToString()) : 0; }
				return (field, ch, spec[p..]);
				}
			var (sf, sc, so) = ParseSpec(parts[0]);
			var (ef, ec, eo) = parts.Length > 1 ? ParseSpec(parts[1]) : (0, 0, "");
			var kopts = so + eo;
			bool any = kopts.Length > 0;
			keys.Add(new SortKey(sf, sc, ef, ec,
				any ? kopts.Contains('n') : gNumeric, any ? kopts.Contains('h') : gHuman, any ? kopts.Contains('V') : gVersion,
				any ? kopts.Contains('r') : gReverse, any ? kopts.Contains('f') : gFold, any ? kopts.Contains('b') : gBlanks, any ? kopts.Contains('d') : gDict));
			}
		if (keys.Count == 0) keys.Add(new SortKey(0, 0, 0, 0, gNumeric, gHuman, gVersion, gReverse, gFold, gBlanks, gDict));

		var lines = new List<string>();
		try
			{
			foreach (var f in FilesOrStdin(o))
				{
				TextReader? r = null;
				try { r = OpenText(f); lines.AddRange(ReadLines(r)); }
				finally { if (f != "-") r?.Dispose(); }
				}
			}
		catch (Exception ex) { Console.Error.WriteLine($"sort: {IoError(ex)}"); return 2; }

		string[] SplitFields(string s)
			{
			if (delim is char d) return s.Split(d);
			// default: fields are separated by the transition from non-blank to blank; each field
			// includes its leading blanks
			var res = new List<string>(); var sb = new StringBuilder(); bool inField = false;
			foreach (var ch in s)
				{
				bool blank = ch is ' ' or '\t';
				if (inField && blank) { res.Add(sb.ToString()); sb.Clear(); inField = false; }
				if (!blank) inField = true;
				sb.Append(ch);
				}
			res.Add(sb.ToString());
			return res.ToArray();
			}

		string KeyText(string line, SortKey k)
			{
			if (k.Start == 0) return line;
			var fields = SplitFields(line);
			var sb = new StringBuilder();
			int last = k.End == 0 ? fields.Length : Math.Min(k.End, fields.Length);
			for (int fi = k.Start; fi <= last; fi++)
				{
				if (fi - 1 >= fields.Length) break;
				var fs = fields[fi - 1];
				if (fi == k.Start && k.StartChar > 1) fs = k.StartChar - 1 < fs.Length ? fs[(k.StartChar - 1)..] : "";
				if (fi == k.End && k.EndChar > 0) fs = fs.Length > k.EndChar ? fs[..k.EndChar] : fs;
				if (fi > k.Start && delim is char d2) sb.Append(d2);
				sb.Append(fs);
				}
			var text = sb.ToString();
			if (k.IgnoreBlanks) text = text.TrimStart(' ', '\t');
			if (k.Dictionary) text = new string(text.Where(c => char.IsLetterOrDigit(c) || c is ' ' or '\t').ToArray());
			return text;
			}

		int CompareKey(string a, string b, SortKey k)
			{
			string ka = KeyText(a, k), kb = KeyText(b, k);
			int c;
			if (k.Numeric) c = NumericPrefix(ka).CompareTo(NumericPrefix(kb));
			else if (k.Human) c = HumanValue(ka).CompareTo(HumanValue(kb));
			else if (k.Version) c = VersionCompare(ka, kb);
			else c = string.Compare(ka, kb, k.Fold ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);
			return k.Reverse ? -c : c;
			}

		int Compare(string a, string b)
			{
			foreach (var k in keys) { int c = CompareKey(a, b, k); if (c != 0) return c; }
			if (o.Has('s') || unique) return 0;
			int last = string.CompareOrdinal(a, b);           // last-resort whole-line compare
			return gReverse ? -last : last;
			}

		if (check)
			{
			for (int i = 1; i < lines.Count; i++)
				if (Compare(lines[i - 1], lines[i]) > 0) { Console.Error.WriteLine($"sort: -:{i + 1}: disorder: {lines[i]}"); return 1; }
			return 0;
			}
		if (random) { var rng = new Random(); lines = lines.OrderBy(_ => rng.Next()).ToList(); }
		else
			{
			// stable merge sort (List.Sort is unstable)
			lines = lines.Select((l, i) => (l, i)).OrderBy(p => p, Comparer<(string l, int i)>.Create((x, y) => { int c = Compare(x.l, y.l); return c != 0 ? c : x.i.CompareTo(y.i); })).Select(p => p.l).ToList();
			}

		TextWriter w = Console.Out; StreamWriter? fw = null;
		if (o.Get('o') is string outFile) { fw = new StreamWriter(ShellEnvironment.TranslatePath(outFile), false, ShellEncoding.Utf8) { NewLine = "\n" }; w = fw; }
		try
			{
			string? prev = null;
			foreach (var l in lines)
				{
				if (unique && prev is not null && keys.All(k => CompareKey(prev, l, k) == 0)) continue;
				w.WriteLine(l); prev = l;
				}
			}
		finally { fw?.Dispose(); }
		return 0;
		}

	private static double NumericPrefix(string s)
		{
		var t = s.TrimStart();
		int i = 0;
		if (i < t.Length && (t[i] == '-' || t[i] == '+')) i++;
		while (i < t.Length && (char.IsDigit(t[i]) || t[i] == '.' || t[i] == ',')) i++;
		var num = t[..i].Replace(",", "");
		return double.TryParse(num, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var d) ? d : 0;
		}

	private static double HumanValue(string s)
		{
		var t = s.TrimStart();
		int i = 0;
		if (i < t.Length && (t[i] == '-' || t[i] == '+')) i++;
		while (i < t.Length && (char.IsDigit(t[i]) || t[i] == '.')) i++;
		double v = double.TryParse(t[..i], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var d) ? d : 0;
		double mult = i < t.Length ? char.ToUpperInvariant(t[i]) switch { 'K' => 1e3, 'M' => 1e6, 'G' => 1e9, 'T' => 1e12, 'P' => 1e15, _ => 1 } : 1;
		return v * mult;
		}

	/// <summary>GNU version-sort: digit runs compare numerically, other runs ordinally.</summary>
	private static int VersionCompare(string a, string b)
		{
		int i = 0, j = 0;
		while (i < a.Length && j < b.Length)
			{
			if (char.IsDigit(a[i]) && char.IsDigit(b[j]))
				{
				int si = i, sj = j;
				while (i < a.Length && char.IsDigit(a[i])) i++;
				while (j < b.Length && char.IsDigit(b[j])) j++;
				var na = a[si..i].TrimStart('0'); var nb = b[sj..j].TrimStart('0');
				if (na.Length != nb.Length) return na.Length.CompareTo(nb.Length);
				int c = string.CompareOrdinal(na, nb);
				if (c != 0) return c;
				}
			else
				{
				int c = a[i].CompareTo(b[j]);
				if (c != 0) return c;
				i++; j++;
				}
			}
		return (a.Length - i).CompareTo(b.Length - j);
		}

	// ── split ───────────────────────────────────────────────────────────────────

	private static int Split(List<string> args)
		{
		var o = Opts.Parse("split", args, "l:b:a:dn:e", ["lines=:l", "bytes=:b", "suffix-length=:a", "numeric-suffixes:d", "additional-suffix=", "elide-empty-files:e", "number=:n"], allowNumeric: true);
		int lines = o.Numeric is not null ? int.Parse(o.Numeric) : o.GetInt('l', 1000);
		long byteSize = o.Get('b') is string bs ? ParseCount("split", bs, 'b') : 0;
		if (o.Get('n') is not null) throw new UnsupportedOptionException("split", "-n", "chunk mode is not implemented");
		int suffixLen = o.GetInt('a', 2);
		bool numeric = o.Has('d');
		string extra = o.GetLong("additional-suffix") ?? "";
		string file = o.Operands.Count > 0 ? o.Operands[0] : "-";
		string prefix = o.Operands.Count > 1 ? o.Operands[1] : "x";
		byte[] data;
		try { data = ReadBytes(file); }
		catch (Exception ex) { Console.Error.WriteLine($"split: {file}: {IoError(ex)}"); return 1; }

		string Suffix(int idx)
			{
			if (numeric) return idx.ToString().PadLeft(suffixLen, '0') + extra;
			var sb = new StringBuilder();
			for (int k = 0; k < suffixLen; k++) { sb.Insert(0, (char)('a' + idx % 26)); idx /= 26; }
			return sb + extra;
			}
		void Write(int idx, int off, int len)
			{
			using var fs = File.Create(ShellEnvironment.TranslatePath(prefix + Suffix(idx)));
			fs.Write(data, off, len);
			}
		int piece = 0;
		if (byteSize > 0)
			{
			for (long off = 0; off < data.Length; off += byteSize)
				Write(piece++, (int)off, (int)Math.Min(byteSize, data.Length - off));
			}
		else
			{
			int start = 0, count = 0;
			for (int i = 0; i < data.Length; i++)
				{
				if (data[i] != (byte)'\n') continue;
				if (++count == lines) { Write(piece++, start, i - start + 1); start = i + 1; count = 0; }
				}
			if (start < data.Length) Write(piece++, start, data.Length - start);
			}
		return 0;
		}

	// ── od / hexdump ────────────────────────────────────────────────────────────

	private static int Od(List<string> args)
		{
		var o = Opts.Parse("od", args, "A:t:j:N:w:vbcxdo", ["address-radix=:A", "format=:t", "skip-bytes=:j", "read-bytes=:N", "width=:w", "output-duplicates:v"]);
		string addr = o.Get('A') ?? "o";
		string fmt = o.Get('t') ?? (o.Has('c') ? "c" : o.Has('b') ? "o1" : o.Has('x') ? "x2" : o.Has('d') ? "d2" : "o2");
		if (o.Has('c')) fmt = "c";
		long skip = o.Get('j') is string js ? ParseCount("od", js, 'j') : 0;
		long limit = o.Get('N') is string ns ? ParseCount("od", ns, 'N') : long.MaxValue;
		int width = o.GetInt('w', 16);
		string file = o.Operands.Count > 0 ? o.Operands[0] : "-";
		byte[] data;
		try { data = ReadBytes(file); }
		catch (Exception ex) { Console.Error.WriteLine($"od: {file}: {IoError(ex)}"); return 1; }
		if (skip > 0) data = data.Skip((int)Math.Min(skip, data.Length)).ToArray();
		if (limit < data.Length) data = data.Take((int)limit).ToArray();

		string Addr(long off) => addr switch
			{
			"n" => "",
			"x" => off.ToString("x6"),
			"d" => off.ToString("D7"),
			_ => Convert.ToString(off, 8).PadLeft(7, '0'),
			};
		int size = fmt.Length > 1 && char.IsDigit(fmt[^1]) ? fmt[^1] - '0' : (fmt[0] == 'c' || fmt[0] == 'a' ? 1 : 2);
		char kind = fmt[0];
		for (int off = 0; off < data.Length; off += width)
			{
			var sb = new StringBuilder(Addr(off));
			int end = Math.Min(off + width, data.Length);
			for (int i = off; i < end; i += size)
				{
				if (kind == 'c') { sb.Append(' ').Append(OdChar(data[i]).PadLeft(3)); continue; }
				if (kind == 'a') { sb.Append(' ').Append((data[i] >= 32 && data[i] < 127 ? ((char)data[i]).ToString() : "nul").PadLeft(3)); continue; }
				long v = 0;
				for (int b = 0; b < size && i + b < end; b++) v |= (long)data[i + b] << (8 * b);
				sb.Append(' ').Append(kind switch
					{
					'x' => v.ToString("x" + (size * 2)),
					'd' => (size == 1 ? (sbyte)v : size == 2 ? (short)v : (int)v).ToString().PadLeft(size * 3 + 1),
					'u' => v.ToString().PadLeft(size * 3),
					_ => Convert.ToString(v, 8).PadLeft(size * 3, '0'),
					});
				}
			Console.WriteLine(sb.ToString());
			}
		if (addr != "n" && data.Length > 0) Console.WriteLine(Addr(data.Length));
		return 0;
		}

	private static string OdChar(byte b) => b switch
		{
		0 => "\\0", 7 => "\\a", 8 => "\\b", 9 => "\\t", 10 => "\\n", 11 => "\\v", 12 => "\\f", 13 => "\\r",
		_ => b >= 32 && b < 127 ? ((char)b).ToString() : Convert.ToString(b, 8).PadLeft(3, '0')
		};

	private static int Hexdump(List<string> args)
		{
		var o = Opts.Parse("hexdump", args, "Cn:s:v", ["canonical:C", "length=:n", "skip=:s", "no-squeezing:v"]);
		if (!o.Has('C')) throw new UnsupportedOptionException("hexdump", "(default format)", "only -C (canonical) is implemented");
		string file = o.Operands.Count > 0 ? o.Operands[0] : "-";
		byte[] data;
		try { data = ReadBytes(file); }
		catch (Exception ex) { Console.Error.WriteLine($"hexdump: {file}: {IoError(ex)}"); return 1; }
		long skip = o.Get('s') is string ss ? ParseCount("hexdump", ss, 's') : 0;
		long len = o.Get('n') is string ns ? ParseCount("hexdump", ns, 'n') : long.MaxValue;
		int start = (int)Math.Min(skip, data.Length), end = (int)Math.Min(data.Length, start + len);
		for (int off = start; off < end; off += 16)
			{
			var sb = new StringBuilder(off.ToString("x8")).Append(' ');
			int lineEnd = Math.Min(off + 16, end);
			for (int i = off; i < off + 16; i++)
				{
				if (i == off + 8) sb.Append(' ');
				sb.Append(i < lineEnd ? " " + data[i].ToString("x2") : "   ");
				}
			sb.Append("  |");
			for (int i = off; i < lineEnd; i++) sb.Append(data[i] >= 32 && data[i] < 127 ? (char)data[i] : '.');
			sb.Append('|');
			Console.WriteLine(sb.ToString());
			}
		Console.WriteLine((end).ToString("x8"));
		return 0;
		}

	// ── seq ─────────────────────────────────────────────────────────────────────

	private static int Seq(List<string> args)
		{
		var o = Opts.Parse("seq", args, "ws:f:", ["equal-width:w", "separator=:s", "format=:f"]);
		var nums = new List<decimal>();
		foreach (var a in o.Operands)
			{
			if (!decimal.TryParse(a, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var v))
				{ Console.Error.WriteLine($"seq: invalid floating point argument: '{a}'"); return 1; }
			nums.Add(v);
			}
		if (nums.Count == 0) { Console.Error.WriteLine("seq: missing operand"); return 1; }
		decimal first = 1, incr = 1, last;
		if      (nums.Count == 1) last = nums[0];
		else if (nums.Count == 2) { first = nums[0]; last = nums[1]; }
		else                      { first = nums[0]; incr = nums[1]; last = nums[2]; }
		if (incr == 0) { Console.Error.WriteLine("seq: invalid Zero increment value: '0'"); return 1; }
		string sep = o.Get('s') is string s ? UnescapeDelims(s) : "\n";
		int decimals = o.Operands.Max(a => { int d = a.IndexOf('.'); return d < 0 ? 0 : a.Length - d - 1; });
		string? fmt = o.Get('f');
		var items = new List<string>();
		for (decimal v = first; incr > 0 ? v <= last : v >= last; v += incr)
			{
			if (fmt is not null) { PrintfFormatter.Format(fmt, [v.ToString(System.Globalization.CultureInfo.InvariantCulture)], Console.Error, out var t); items.Add(t); }
			else items.Add(v.ToString("F" + decimals, System.Globalization.CultureInfo.InvariantCulture));
			if (items.Count > 10_000_000) break;
			}
		if (o.Has('w') && fmt is null)
			{
			int width = items.Count == 0 ? 0 : items.Max(x => x.Length);
			items = items.Select(x => x.StartsWith('-') ? "-" + x[1..].PadLeft(width - 1, '0') : x.PadLeft(width, '0')).ToList();
			}
		var sb = new StringBuilder();
		foreach (var it in items) { sb.Append(it).Append(sep); if (sb.Length > 1 << 16) { Console.Out.Write(sb.ToString()); sb.Clear(); } }
		if (sep != "\n" && sb.Length >= sep.Length) { sb.Length -= sep.Length; sb.Append('\n'); }
		Console.Out.Write(sb.ToString());
		return 0;
		}

	// ── base64 / checksums ──────────────────────────────────────────────────────

	private static int Base64(List<string> args)
		{
		var o = Opts.Parse("base64", args, "dw:i", ["decode:d", "wrap=:w", "ignore-garbage:i"]);
		string file = o.Operands.Count > 0 ? o.Operands[0] : "-";
		byte[] data;
		try { data = ReadBytes(file); }
		catch (Exception ex) { Console.Error.WriteLine($"base64: {file}: {IoError(ex)}"); return 1; }
		if (o.Has('d'))
			{
			var text = Encoding.ASCII.GetString(data).Where(c => !char.IsWhiteSpace(c)).ToArray();
			byte[] outBytes;
			try { outBytes = Convert.FromBase64String(new string(text)); }
			catch (FormatException) { Console.Error.WriteLine("base64: invalid input"); return 1; }
			var raw = Evaluator.CurrentRawStdout();
			if (raw is not null) { Console.Out.Flush(); raw.Write(outBytes, 0, outBytes.Length); raw.Flush(); }
			else Console.Out.Write(ShellEncoding.Utf8.GetString(outBytes));
			return 0;
			}
		int wrap = o.GetInt('w', 76);
		var enc = Convert.ToBase64String(data);
		if (wrap <= 0) { Console.WriteLine(enc); return 0; }
		for (int i = 0; i < enc.Length; i += wrap) Console.WriteLine(enc.Substring(i, Math.Min(wrap, enc.Length - i)));
		return 0;
		}

	private static int Checksum(string tool, List<string> args)
		{
		var o = Opts.Parse(tool, args, "bctz", ["binary:b", "check:c", "text:t", "tag", "quiet", "status", "zero:z"]);
		System.Security.Cryptography.HashAlgorithm Algo() => tool switch
			{
			"md5sum" => System.Security.Cryptography.MD5.Create(),
			"sha1sum" => System.Security.Cryptography.SHA1.Create(),
			"sha512sum" => System.Security.Cryptography.SHA512.Create(),
			_ => System.Security.Cryptography.SHA256.Create(),
			};
		string HexOf(byte[] data) { using var h = Algo(); return Convert.ToHexString(h.ComputeHash(data)).ToLowerInvariant(); }
		int rc = 0;
		if (o.Has('c'))
			{
			bool quiet = o.HasLong("quiet") || o.HasLong("status");
			int failed = 0;
			foreach (var f in FilesOrStdin(o))
				{
				TextReader? r = null;
				try
					{
					r = OpenText(f);
					foreach (var line in ReadLines(r))
						{
						var m = System.Text.RegularExpressions.Regex.Match(line, @"^([0-9a-fA-F]+)\s+[ *]?(.+)$");
						if (!m.Success) continue;
						var target = m.Groups[2].Value;
						string actual;
						try { actual = HexOf(File.ReadAllBytes(ShellEnvironment.TranslatePath(target))); }
						catch { Console.WriteLine($"{target}: FAILED open or read"); failed++; continue; }
						bool ok = string.Equals(actual, m.Groups[1].Value, StringComparison.OrdinalIgnoreCase);
						if (!ok) failed++;
						if (!quiet || !ok) if (!o.HasLong("status")) Console.WriteLine($"{target}: {(ok ? "OK" : "FAILED")}");
						}
					}
				catch (Exception ex) { Console.Error.WriteLine($"{tool}: {f}: {IoError(ex)}"); rc = 1; }
				finally { if (f != "-") r?.Dispose(); }
				}
			if (failed > 0) { if (!o.HasLong("status")) Console.Error.WriteLine($"{tool}: WARNING: {failed} computed checksum did NOT match"); return 1; }
			return rc;
			}
		foreach (var f in FilesOrStdin(o))
			{
			try
				{
				var hex = HexOf(ReadBytes(f));
				if (o.HasLong("tag")) Console.WriteLine($"{tool[..^3].ToUpperInvariant()} ({f}) = {hex}");
				else Console.WriteLine($"{hex}  {f}");
				}
			catch (Exception ex) { Console.Error.WriteLine($"{tool}: {f}: {IoError(ex)}"); rc = 1; }
			}
		return rc;
		}
	}
