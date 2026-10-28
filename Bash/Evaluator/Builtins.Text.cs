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
		src is "-" or "/dev/stdin" ? Console.In : new LfReader(ShellFile.OpenInput(ShellEnvironment.TranslatePath(src)));   // files and devices  // LF-only lines, no BOM sniffing: bytes are data

	private static IEnumerable<string> ReadLines(TextReader r) => ShellEncoding.Lines(r);   // LF only: a CR is data

	// (ReadBytes, the whole input as one array, is gone: it failed over 2 GiB; the tools read through OpenBytes and
	// a rented Block, Builtins.Bytes.cs, 2026-10-27)

	private static List<string> FilesOrStdin(Opts o) => o.Operands.Count == 0 ? ["-"] : o.Operands;


	/// <summary>Signal <paramref name="changed"/> when <paramref name="path"/> is written or resized;
	/// null when the directory cannot be watched (a share, say), and then the caller's interval is
	/// the only wake-up.</summary>
	private static FileSystemWatcher? WatchFile(string path, AutoResetEvent changed)
		{
		try
			{
			var full = Path.GetFullPath(path);
			var w = new FileSystemWatcher(Path.GetDirectoryName(full)!, Path.GetFileName(full))
				{ NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size | NotifyFilters.FileName };
			FileSystemEventHandler wake = (_, _) => changed.Set();
			w.Changed += wake; w.Created += wake;
			w.Renamed += (_, _) => changed.Set();
			w.EnableRaisingEvents = true;
			return w;
			}
		catch { return null; }
		}

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
		if (s.EndsWith("kB", StringComparison.Ordinal)) { mult = 1000; s = s[..^2]; }
		else if (s.EndsWith("MB", StringComparison.Ordinal)) { mult = 1000_000; s = s[..^2]; }
		else if (s.EndsWith("GB", StringComparison.Ordinal)) { mult = 1000_000_000; s = s[..^2]; }
		else if (s.Length > 0 && char.IsLetter(s[^1]))
			{
			mult = char.ToLowerInvariant(s[^1]) switch { 'b' => 512, 'k' => 1024, 'm' => 1024L * 1024, 'g' => 1024L * 1024 * 1024, _ => -1 };
			if (mult < 0) throw new UnsupportedOptionException(tool, $"-{opt} {v}", "invalid number");
			s = s[..^1];
			}
		if (!long.TryParse(s, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var n)) throw new UnsupportedOptionException(tool, $"-{opt} {v}", "invalid number");
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
						if (rin is not null) { CopyAll(rin); continue; }   // into a capture, a block at a time
						var s = Console.In.ReadToEnd();   // stdin is text (a here-document)
						if (raw is not null) { var b = ShellEncoding.Utf8.GetBytes(s); raw.Write(b, 0, b.Length); }
						else Console.Out.Write(s);
						continue;
						}
					var p = ShellEnvironment.TranslatePath(f);
					if (Directory.Exists(p)) { Console.Error.WriteLine($"cat: {f}: Is a directory"); rc = 1; continue; }
					using var fs = ShellFile.OpenInput(p);   // a file or a device
					// into a capture: decoded a block at a time (it read the whole file into one string, 2026-10-27)
					if (raw is not null) fs.CopyTo(raw); else CopyAll(fs);
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
		if (o.Numeric is not null) count = long.Parse(o.Numeric, System.Globalization.CultureInfo.InvariantCulture);
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
				// Exactly N bytes, then stop. Reading all of the input first hung `yes | head -c 5` and,
				// on a shared stdin, consumed what the next command should read (2026-10-03). Taken
				// ONCE: CurrentRawStdin hands over a text reader's read-ahead.
				var src = bytes && !fromEnd
					? (f == "-" ? Evaluator.CurrentRawStdin() : ShellFile.OpenInput(ShellEnvironment.TranslatePath(f)))
					: null;
				if (src is not null)
					{
					try
						{
						// into a capture, decoded a block at a time (it was gathered whole, 2026-10-27)
						var sink = new ByteSink();
						using var block = Block.Rent();
						for (long left = count; left > 0; )
							{
							int n = src.Read(block.Bytes, 0, (int)Math.Min(left, IoBlock));
							if (n <= 0) break;
							sink.Write(block.Bytes, 0, n);
							left -= n;
							}
						sink.Flush();
						}
					finally { if (f != "-") src.Dispose(); }
					continue;
					}
				if (fromEnd || o.Has('z'))
					{
					// All but the last N bytes or lines, and -z's NUL-terminated records (accepted and ignored
					// until 2026-09-05): bytes as they are, a file read from its end. They read the whole input
					// into one array or list, which failed on a file over 2 GiB (2026-10-27).
					var input = OpenBytes(f);
					try
						{
						var sink = new ByteSink();
						byte term = o.Has('z') ? (byte)0 : (byte)'\n';
						if (fromEnd) FromEnd(input, count, bytes, term, head: true, sink);
						else CopyRecords(input, count, term, sink);
						sink.Flush();
						}
					finally { if (f != "-") input.Dispose(); }
					continue;
					}
				// Stream: stop after n lines so an infinite/slow producer is released (SIGPIPE). Through the shared line
				// reader, never past the last line taken, so the next command reads on from there, on a pipe too (GNU's
				// head takes a pipe's whole block). A last line without a newline stays without one, as GNU (one was
				// added until 2026-10-27: `printf a | head -n 1` was a\n).
				TextReader reader = OpenText(f);
				try
					{
					for (long i = 0; i < count; i++)
						{
						if (ShellEncoding.ReadRecord(reader, '\n', out bool ended) is not string line) break;
						if (ended) Console.WriteLine(line);   // not Write(line + "\n"): a string a line was 20% slower (1M lines)
						else Console.Write(line);
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
		if (o.Numeric is not null) count = long.Parse(o.Numeric, System.Globalization.CultureInfo.InvariantCulture);
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
				// Bytes as they are (a last line keeps having no newline), a file read from its end and a pipe
				// through a bounded window. Every mode read the whole input into one array or list until
				// 2026-10-27, which failed on a file over 2 GiB (koliada-net-28, a 93 GB log).
				long lengthSeen;
				var input = OpenBytes(f);   // a file or a device (`tail /dev/null`)
				try
					{
					var sink = new ByteSink();
					byte term = o.Has('z') ? (byte)0 : (byte)'\n';
					lengthSeen = fromStart ? CopyAfter(input, Math.Max(0, count - 1), bytes, term, sink)
					                       : FromEnd(input, count, bytes, term, head: false, sink);
					sink.Flush();
					}
				finally { if (f != "-") input.Dispose(); }
				if (follow && f != "-")
					{
					// Follow until interrupted (Ctrl+C) or the reader goes away. A file-change watcher wakes
					// us as soon as Windows reports one; the interval (GNU's -s poll period) is the backstop,
					// since NTFS reports a growing file's size lazily. An interrupt ends the wait at once.
					// It was Thread.Sleep(interval) (the architect: never sleep-poll for synchronization).
					var p = ShellEnvironment.TranslatePath(f);
					using var changed = new AutoResetEvent(false);
					using var watcher = WatchFile(p, changed);
					var wakers = new List<WaitHandle>(_eval.InterruptHandles()) { changed }.ToArray();
					while (true)
						{
						WaitHandle.WaitAny(wakers, (int)(interval * 1000));
						_eval.CheckInterrupt();
						long len;
						try { len = new FileInfo(p).Length; } catch { continue; }
						if (len < lengthSeen) lengthSeen = 0;      // truncated: start over
						if (len == lengthSeen) continue;
						using var fs = new FileStream(p, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
						fs.Seek(lengthSeen, SeekOrigin.Begin);
						// What was appended, as bytes, a block at a time, and the next round starts where this read
						// ended. It was read to the end as one string but the next round started at the length seen
						// before the read, so whatever a busy writer added in between came out twice (30000 lines
						// appended as fast as a loop could: ~56600 out, measured 2026-10-27).
						var sink = new ByteSink();
						lengthSeen = CopyAfter(fs, 0, true, (byte)'\n', sink);
						sink.Flush();
						}
					}
				}
			catch (BrokenPipeException) { throw; }
			catch (InterruptException) { throw; }   // `tail -f` ended by Ctrl+C / `timeout`: not a read error
			catch (Exception ex) { Console.Error.WriteLine($"tail: cannot open '{f}' for reading: {IoError(ex)}"); rc = 1; }
			}
		return rc;
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
				var input = OpenBytes(f);
				try
					{
					var (lines, words, bytes, chars, maxLen) = WcCount(input, text: cw || cm || cL, perChar: cw || cL, bytesOnly: !cl && !cw && !cm && !cL);
					tl += lines; tw += words; tc += bytes; tm += chars; if (maxLen > tL) tL = maxLen;
					Emit(lines, words, bytes, chars, maxLen, f == "-" ? "" : f);
					}
				finally { if (f != "-") input.Dispose(); }
				}
			catch (Exception ex) { Console.Error.WriteLine($"wc: {f}: {IoError(ex)}"); rc = 1; }
			}
		if (multi) Emit(tl, tw, tc, tm, tL, "total");
		return rc;
		}

	/// <summary>One input's counts, taken as it is read, a block at a time: the whole input was one array and then one
	/// string, which failed over 2 GiB, file or pipe (koliada-net-28, 2026-10-27). The counts are the ones the whole-
	/// string code gave: lines and bytes on the bytes; words (runs of non-white-space), characters and the longest line
	/// (in characters) on the text, decoded only when asked, by the carrying decoder (a character split between two
	/// blocks is one). `-c` alone on a seekable input takes its length and moves past it, as GNU does. For speed: blocks
	/// and characters are rented (<see cref="Block"/>); an ASCII block with nothing carried is counted on its bytes, with
	/// no decoding (an ASCII byte is the character it encodes; <see cref="WcAscii"/> holds char.IsWhiteSpace for each);
	/// characters alone (-m) need no look at each one (<paramref name="perChar"/> false).</summary>
	/// <summary>char.IsWhiteSpace of each ASCII character, built on first use (not at the shell's start).</summary>
	private static class WcAscii
		{
		public static readonly bool[] White = [.. Enumerable.Range(0, 128).Select(c => char.IsWhiteSpace((char)c))];
		}

	private static (long Lines, long Words, long Bytes, long Chars, long MaxLen) WcCount(Stream s, bool text, bool perChar, bool bytesOnly)
		{
		if (bytesOnly && s.CanSeek)
			{
			long size = Math.Max(0, s.Length - s.Position);
			s.Seek(0, SeekOrigin.End);
			return (0, 0, size, 0, 0);
			}
		long lines = 0, words = 0, bytes = 0, chars = 0, maxLen = 0, cur = 0;
		bool inWord = false;
		using var block = Block.Rent();
		var buf = block.Bytes;
		var decoder = text ? (ShellEncoding.CarryDecoder)ShellEncoding.Utf8.GetDecoder() : null;
		// characters only for a block that is not ASCII: a pipe stage runs on a new thread, where the pool has none to
		// give and the 256 KB was allocated every time (+5 us on `echo a b c | wc -w`, measured 2026-10-27)
		char[]? cs = null;
		try
			{
			for (int n; (n = s.Read(buf, 0, IoBlock)) > 0; )
				{
				bytes += n;
				var span = buf.AsSpan(0, n);
				lines += span.Count((byte)'\n');
				if (!text) continue;
				if (!decoder!.Carrying && System.Text.Ascii.IsValid(span))
					{
					chars += n;
					if (perChar) CountAscii(span);
					}
				else Count(n, flush: false);
				}
			if (text && decoder!.Carrying) Count(0, flush: true);   // with nothing carried a flush gives nothing
			}
		finally { if (cs is not null) System.Buffers.ArrayPool<char>.Shared.Return(cs); }
		if (cur > maxLen) maxLen = cur;
		return (lines, words, bytes, chars, maxLen);

		void Count(int n, bool flush)
			{
			cs ??= System.Buffers.ArrayPool<char>.Shared.Rent(IoBlock + 4);   // up to 3 carried bytes come out with a block
			int k = decoder!.GetChars(buf, 0, n, cs, 0, flush);
			chars += k;
			if (!perChar) return;
			for (int i = 0; i < k; i++)
				{
				char ch = cs[i];
				if (ch == '\n') { if (cur > maxLen) maxLen = cur; cur = 0; } else cur++;
				bool white = char.IsWhiteSpace(ch);
				if (!white && !inWord) words++;
				inWord = !white;
				}
			}

		void CountAscii(ReadOnlySpan<byte> span)
			{
			var white = WcAscii.White;
			foreach (var b in span)
				{
				if (b == '\n') { if (cur > maxLen) maxLen = cur; cur = 0; } else cur++;
				bool w = white[b];
				if (!w && !inWord) words++;
				inWord = !w;
				}
			}
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
			// The lines last to first, as bytes: a seekable input read from its end, a pipe first copied to a temporary
			// file (GNU's tac does the same). It held every line in a list and added a newline to a last line that had
			// none (`a\nb`: b\na\n; GNU: ba\n), 2026-10-27.
			Stream? input = null, spool = null;
			try
				{
				input = OpenBytes(f);
				if (!input.CanSeek)
					{
					spool = new FileStream(Path.GetTempFileName(), FileMode.Open, FileAccess.ReadWrite, FileShare.None, IoBlock, FileOptions.DeleteOnClose);
					input.CopyTo(spool, IoBlock);
					spool.Position = 0;
					}
				var sink = new ByteSink();
				TacRecords(spool ?? input, sink);
				sink.Flush();
				}
			catch (BrokenPipeException) { throw; }
			catch (Exception ex) { Console.Error.WriteLine($"tac: failed to open '{f}' for reading: {IoError(ex)}"); rc = 1; }
			finally { spool?.Dispose(); if (f != "-") input?.Dispose(); }
			}
		return rc;
		}

	/// <summary>A seekable input's newline-ended records from the last to the first, read backwards a block at a time; a
	/// record inside the block in hand is written from it, a longer one read again forwards.</summary>
	private static void TacRecords(Stream s, ByteSink sink)
		{
		long lo = s.Position, hi = Math.Max(lo, s.Length), end = hi, pos = hi;
		ReadAt read = (at, b, n) => { s.Seek(at, SeekOrigin.Begin); s.ReadExactly(b, 0, n); };
		using var block = Block.Rent();
		var buf = block.Bytes;
		int len = 0;
		while (pos > lo)
			{
			len = (int)Math.Min(IoBlock, pos - lo);
			pos -= len;
			read(pos, buf, len);
			for (int i = len; (i = buf.AsSpan(0, i).LastIndexOf((byte)'\n')) >= 0; ) Emit(pos + i + 1);
			}
		Emit(lo);

		// the record [from, end): from the block in hand when it is all there, else read again forwards
		void Emit(long from)
			{
			if (end > from)
				{
				if (end <= pos + len) sink.Write(buf, (int)(from - pos), (int)(end - from));
				else CopyRange(read, from, end, sink);
				}
			end = from;
			}
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
		var set1 = new HashSet<char>(s1);
		bool In1(char c) => complement ? !set1.Contains(c) : set1.Contains(c);
		var sb = new StringBuilder(8192);
		char? last = null;

		// One step per input character, the state (`last`, for -s) kept across blocks.
		Action<char> step;
		if (del)
			{
			var sq = squeeze ? new HashSet<char>(s2) : null;
			step = c =>
				{
				if (In1(c)) return;
				if (sq is not null && last == c && sq.Contains(c)) return;
				sb.Append(c); last = c;
				};
			}
		else if (o.Operands.Count < 2)
			step = c => { if (In1(c) && last == c) return; sb.Append(c); last = c; };   // -s alone: squeeze SET1
		else if (complement)
			{
			// every char NOT in set1 maps to the last char of set2
			char to = s2.Length > 0 ? s2[^1] : ' ';
			step = c =>
				{
				char oc = set1.Contains(c) ? c : to;
				if (squeeze && !set1.Contains(c) && last == oc) return;
				sb.Append(oc); last = oc;
				};
			}
		else
			{
			var map = new Dictionary<char, char>();
			int limit = truncate ? Math.Min(s1.Length, s2.Length) : s1.Length;
			for (int i = 0; i < limit; i++)
				map[s1[i]] = i < s2.Length ? s2[i] : (s2.Length > 0 ? s2[^1] : s1[i]);
			step = c =>
				{
				char oc = map.TryGetValue(c, out var m) ? m : c;
				if (squeeze && last == oc && map.ContainsKey(c)) return;
				sb.Append(oc); last = oc;
				};
			}

		// a filter, a block at a time, as GNU tr: it read ALL its input first until 2026-10-10, so
		// `tr '\0' z < /dev/zero | head -c 3` never ended and a large input had to fit in memory
		// its own buffer: a pooled one measured no faster (2026-10-27)
		var buf = new char[8192];
		int n;
		while ((n = Console.In.Read(buf, 0, buf.Length)) > 0)
			{
			for (int i = 0; i < n; i++) step(buf[i]);
			Console.Out.Write(sb.ToString());
			sb.Clear();
			}
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
					int n = int.TryParse(spec, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var nn) ? nn : 1;
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
			if (dash < 0) { if (int.TryParse(part, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var v)) r.Add((v, v)); }
			else
				{
				int lo = dash == 0 ? 1 : int.Parse(part[..dash], System.Globalization.CultureInfo.InvariantCulture);
				int hi = dash == part.Length - 1 ? int.MaxValue : int.Parse(part[(dash + 1)..], System.Globalization.CultureInfo.InvariantCulture);
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
		var o = Opts.Parse("uniq", args, "cduDif:s:w:z", ["count:c", "repeated:d", "unique:u", "ignore-case:i", "skip-fields=:f", "skip-chars=:s", "check-chars=:w", "zero-terminated:z", "all-repeated:D"]);
		// coreutils' three switches: -d and -D turn off the lone lines, -u the first line of a repeated group, -D turns
		// on its later lines. `-d -u` printed the repeated groups (GNU: nothing); -D went to the PATH uniq, which
		// differs from GNU on -D -d and -D -u (2026-10-27).
		bool count = o.Has('c'), icase = o.Has('i');
		bool unique = !(o.Has('d') || o.Has('D')), firstRepeated = !o.Has('u'), laterRepeated = o.Has('D');
		if (count && laterRepeated)
			{
			Console.Error.WriteLine("uniq: printing all duplicated lines and repeat counts is meaningless");
			Console.Error.WriteLine("Try 'uniq --help' for more information.");
			return 1;
			}
		char sep = o.Has('z') ? '\0' : '\n';
		int skipFields = o.GetInt('f', 0), skipChars = o.GetInt('s', 0), checkChars = o.GetInt('w', 0);
		string input = o.Operands.Count > 0 ? o.Operands[0] : "-";
		string? output = o.Operands.Count > 1 ? o.Operands[1] : null;

		string KeyOf(string s)
			{
			var k = s;
			for (int i = 0; i < skipFields; i++)
				{
				// blanks are space and tab, as GNU (char.IsWhiteSpace also took a CR: `uniq -f 1` of CRLF lines differed)
				int p = 0;
				while (p < k.Length && k[p] is ' ' or '\t') p++;
				while (p < k.Length && k[p] is not (' ' or '\t')) p++;
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
			// A group at a time: its first line, key and size (every line was read into a list first, which failed over
			// 2 GiB and held a pipe's whole input). -z: NUL-ended records (it was accepted and ignored).
			string? first = null, firstKey = null;
			long n = 0;
			void EndGroup()
				{
				if (first is null) return;
				if (n == 1 ? unique : firstRepeated && !laterRepeated) w.Write((count ? $"{n,7} {first}" : first) + sep);
				}
			while (ShellEncoding.ReadRecord(r, sep, out _) is string line)
				{
				var key = KeyOf(line);
				if (first is not null && key == firstKey)
					{
					if (++n == 2 && laterRepeated && firstRepeated) w.Write(first + sep);
					if (laterRepeated) w.Write(line + sep);
					continue;
					}
				EndGroup();
				first = line; firstKey = key; n = 1;
				}
			EndGroup();
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

	/// <summary>GNU fold, ported from coreutils fold.c: a column per byte, except that `\b` moves one
	/// back, `\r` returns to 0 and a tab to the next multiple of 8 (`-b`: every byte is one column);
	/// a byte that would pass the width starts the next line, and `-s` breaks after the last blank
	/// instead. Input with no final newline gets none. Until 2026-10-08 it cut each line into
	/// fixed runs of characters, so `\r`, `\b` and tabs counted as one column each.</summary>
	private static int Fold(List<string> args)
		{
		var o = Opts.Parse("fold", args, "w:sb", ["width=:w", "spaces:s", "bytes:b"], allowNumeric: true);
		int width = o.Numeric is not null ? int.Parse(o.Numeric, System.Globalization.CultureInfo.InvariantCulture) : o.GetInt('w', 80);
		bool breakSpaces = o.Has('s'), countBytes = o.Has('b');
		if (width < 1) width = 1;
		var rout = Evaluator.CurrentRawStdout();
		if (rout is not null) Console.Out.Flush();
		void Write(List<byte> bytes, int count)
			{
			var chunk = bytes.GetRange(0, count).ToArray();
			if (rout is not null) rout.Write(chunk, 0, chunk.Length);
			else Console.Out.Write(ShellEncoding.Utf8.GetString(chunk));
			}
		int Adjust(int column, byte c) => countBytes ? column + 1 : c switch
			{
			(byte)'\b' => column > 0 ? column - 1 : 0,
			(byte)'\r' => 0,
			(byte)'\t' => column + 8 - column % 8,
			_          => column + 1,
			};
		int rc = 0;
		foreach (var f in FilesOrStdin(o))
			{
			Stream? input = null;
			bool own = f != "-";
			try
				{
				input = own ? ShellFile.OpenInput(ShellEnvironment.TranslatePath(f))
				      : Evaluator.CurrentRawStdin() ?? new MemoryStream(ShellEncoding.Utf8.GetBytes(Console.In.ReadToEnd()));
				var line = new List<byte>();
				int column = 0;
				// a pooled block, read 8 KiB at a time as before: 2000 runs on a tiny file 45 -> 43 ms (2026-10-27)
				using var block = Block.Rent();
				var buf = block.Bytes;
				int n;
				while ((n = input.Read(buf, 0, 8192)) > 0)
					for (int k = 0; k < n; k++)
						{
						byte c = buf[k];
						if (c == (byte)'\n') { line.Add(c); Write(line, line.Count); line.Clear(); column = 0; continue; }
					rescan:
						column = Adjust(column, c);
						if (column > width)
							{
							if (breakSpaces)
								{
								int end = line.Count;
								bool found = false;
								while (end > 0) { --end; if (line[end] is (byte)' ' or (byte)'\t') { found = true; break; } }
								if (found)
									{
									end++;
									Write(line, end);
									Write([(byte)'\n'], 1);
									line.RemoveRange(0, end);
									column = 0;
									foreach (var b in line) column = Adjust(column, b);
									goto rescan;
									}
								}
							if (line.Count == 0) { line.Add(c); continue; }
							line.Add((byte)'\n');
							Write(line, line.Count);
							line.Clear();
							column = 0;
							goto rescan;
							}
						line.Add(c);
						}
				if (line.Count > 0) Write(line, line.Count);
				}
			catch (BrokenPipeException) { throw; }
			catch (Exception ex) { Console.Error.WriteLine($"fold: {f}: {IoError(ex)}"); rc = 1; }
			finally { if (own) input?.Dispose(); }
			}
		if (rout is not null) rout.Flush(); else Console.Out.Flush();
		return rc;
		}

	// ── paste / comm ────────────────────────────────────────────────────────────

	private static int Paste(List<string> args)
		{
		var o = Opts.Parse("paste", args, "d:sz", ["delimiters=:d", "serial:s", "zero-terminated:z"]);
		var delims = PasteDelims(o.Get('d') ?? "\t");
		bool serial = o.Has('s');
		char sep = o.Has('z') ? '\0' : '\n';
		var files = FilesOrStdin(o);
		// A row at a time, a line from each input (each was read into a list first: failed over 2 GiB, and `paste - -`
		// gave the first `-` all of stdin: one line a row, as GNU reads each `-` in turn from the one stdin). -z: NUL
		// records (was ignored).
		var r = new TextReader?[files.Count];
		try
			{
			for (int k = 0; k < files.Count; k++)
				{
				try { r[k] = OpenText(files[k]); }
				catch (Exception ex) { Console.Error.WriteLine($"paste: {files[k]}: {IoError(ex)}"); return 1; }
				}
			var w = Console.Out;
			var sb = new StringBuilder();
			if (serial)
				{
				for (int k = 0; k < files.Count; k++)
					{
					sb.Clear();
					for (int i = 0; ShellEncoding.ReadRecord(r[k]!, sep, out _) is string line; i++)
						{
						if (i > 0) sb.Append(delims[(i - 1) % delims.Length]);
						sb.Append(line);
						if (sb.Length >= 65536) { w.Write(sb.ToString()); sb.Clear(); }   // one input is one output line: written as it grows
						}
					w.Write(sb.Append(sep).ToString());
					}
				return 0;
				}
			var done = new bool[files.Count];
			while (true)
				{
				sb.Clear();
				bool any = false;
				for (int c = 0; c < files.Count; c++)
					{
					if (c > 0) sb.Append(delims[(c - 1) % delims.Length]);
					if (done[c]) continue;
					if (ShellEncoding.ReadRecord(r[c]!, sep, out _) is string line) { sb.Append(line); any = true; }
					else done[c] = true;
					}
				if (!any) return 0;
				w.Write(sb.Append(sep).ToString());
				}
			}
		catch (BrokenPipeException) { throw; }
		catch (Exception ex) { Console.Error.WriteLine($"paste: {IoError(ex)}"); return 1; }
		finally
			{
			for (int k = 0; k < files.Count; k++) if (files[k] != "-") r[k]?.Dispose();
			}
		}

	/// <summary>paste's delimiter list, one entry per character, as GNU: `\0` is an empty delimiter and so is an empty
	/// list (they were a NUL and a tab); `\t`, `\n`, `\\` as written.</summary>
	private static string[] PasteDelims(string d)
		{
		var list = new List<string>();
		for (int i = 0; i < d.Length; i++)
			if (d[i] == '\\' && i + 1 < d.Length)
				list.Add(d[++i] switch { 't' => "\t", 'n' => "\n", '\\' => "\\", '0' => "", var x => x.ToString() });
			else list.Add(d[i].ToString());
		return list.Count == 0 ? [""] : [.. list];
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
		bool s1 = o.Has('1'), s2 = o.Has('2'), s3 = o.Has('3'), icase = o.Has('i'), total = o.HasLong("total");
		// as GNU: an empty delimiter is one NUL; -z NUL records; --total and the order check (each was accepted and
		// ignored until 2026-10-27). -i is C#Bash's own (GNU's comm has none).
		string od = o.GetLong("output-delimiter") is string ods ? (ods.Length == 0 ? "\0" : ods) : "\t";
		char sep = o.Has('z') ? '\0' : '\n';
		if (o.Operands.Count != 2)
			{
			Console.Error.WriteLine(o.Operands.Count == 0 ? "comm: missing operand" : o.Operands.Count == 1 ? $"comm: missing operand after '{o.Operands[0]}'" : $"comm: extra operand '{o.Operands[2]}'");
			Console.Error.WriteLine("Try 'comm --help' for more information.");
			return 1;
			}
		// The order check, as GNU: --check-order stops at the first line out of order (status 1); by default a file is
		// checked only once an unpairable line has been seen, warned once, and the status is 1 at the end;
		// --nocheck-order never checks.
		int check = o.HasLong("check-order") ? 2 : o.HasLong("nocheck-order") ? 0 : 1;
		var cmp = icase ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
		var r = new TextReader?[2];
		try
			{
			// Both inputs merged a line at a time (each was read into a list first: failed over 2 GiB).
			for (int k = 0; k < 2; k++)
				{
				try { r[k] = OpenText(o.Operands[k]); }
				catch (Exception ex) { Console.Error.WriteLine($"comm: {o.Operands[k]}: {IoError(ex)}"); return 1; }
				}
			var line = new string?[2];
			var warned = new bool[2];
			bool unpairable = false;
			long n1 = 0, n2 = 0, n3 = 0;
			// the next line of file k, checked against the one before it; false when --check-order stops
			bool Advance(int k)
				{
				var prev = line[k];
				line[k] = ShellEncoding.ReadRecord(r[k]!, sep, out _);
				if (line[k] is null || prev is null || check == 0 || (check == 1 && !unpairable) || warned[k] || cmp.Compare(prev, line[k]) <= 0) return true;
				Console.Error.WriteLine($"comm: file {k + 1} is not in sorted order");
				warned[k] = true;
				return check != 2;
				}
			if (!Advance(0) || !Advance(1)) return 1;
			string p2 = s1 ? "" : od, p3 = (s1 ? "" : od) + (s2 ? "" : od);
			var w = Console.Out;
			while (line[0] is not null || line[1] is not null)
				{
				int c = line[0] is null ? 1 : line[1] is null ? -1 : cmp.Compare(line[0], line[1]);
				if (c == 0) { n3++; if (!s3) w.Write(p3 + line[0] + sep); }
				else
					{
					unpairable = true;
					if (c < 0) { n1++; if (!s1) w.Write(line[0] + sep); }
					else { n2++; if (!s2) w.Write(p2 + line[1] + sep); }
					}
				if (c <= 0 && !Advance(0)) return 1;
				if (c >= 0 && !Advance(1)) return 1;
				}
			if (total) w.Write($"{n1}{od}{n2}{od}{n3}{od}total{sep}");
			if (warned[0] || warned[1]) { Console.Error.WriteLine("comm: input is not in sorted order"); return 1; }
			return 0;
			}
		catch (BrokenPipeException) { throw; }
		catch (Exception ex) { Console.Error.WriteLine($"comm: {IoError(ex)}"); return 1; }
		finally
			{
			for (int k = 0; k < 2; k++) if (o.Operands[k] != "-") r[k]?.Dispose();
			}
		}

	// ── tee ─────────────────────────────────────────────────────────────────────

	private static int Tee(List<string> args)
		{
		var o = Opts.Parse("tee", args, "aip", ["append:a", "ignore-interrupts:i", "output-error="]);
		bool append = o.Has('a');
		var files = new List<FileStream>();
		var copies = new List<TextWriter>();   // /dev/stdout and /dev/stderr operands
		int rc = 0;
		foreach (var f in o.Operands)
			{
			// the device names, as the redirects take them: /dev/null was translated to <drive>/dev/null
			// and failed ("No such file or directory", status 1) until 2026-10-08
			if (f == "/dev/null") continue;
			if (f == "/dev/stdout") { copies.Add(Console.Out); continue; }
			if (f == "/dev/stderr") { copies.Add(Console.Error); continue; }
			try
				{
				var p = ShellEnvironment.TranslatePath(f);
				files.Add(append ? ShellFile.Append(p) : ShellFile.Create(p));
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
				// A pooled block, read 8 KiB at a time: reading the whole block from a pipe made `cat f | tee g > h`
				// slower (100 MB: 537 ms at 8 KiB, 656 ms at 64 KiB; AOT, 5 interleaved rounds, 2026-10-27). Text
				// targets (stdout when it is text, a capture; /dev/stdout and /dev/stderr operands) get each chunk
				// through one carrying decoder, so a character cut by the chunk's end is not two raw bytes there.
				const int chunk = 8192;
				using var block = Block.Rent();
				var buf = block.Bytes;
				var decoder = rout is null || copies.Count > 0 ? ShellEncoding.Utf8.GetDecoder() : null;
				var chars = decoder is null ? null : System.Buffers.ArrayPool<char>.Shared.Rent(chunk + 4);
				try
					{
					void Text(int nc)
						{
						if (nc <= 0) return;
						if (rout is null) { Console.Out.Write(chars!, 0, nc); Console.Out.Flush(); }
						foreach (var w in copies) { w.Write(chars!, 0, nc); w.Flush(); }
						}
					int n;
					while ((n = rin.Read(buf, 0, chunk)) > 0)
						{
						if (rout is not null) { rout.Write(buf, 0, n); rout.Flush(); }
						foreach (var fs in files) fs.Write(buf, 0, n);
						if (decoder is not null) Text(decoder.GetChars(buf, 0, n, chars!, 0, false));
						}
					if (decoder is not null) Text(decoder.GetChars([], 0, 0, chars!, 0, true));
					}
				finally { if (chars is not null) System.Buffers.ArrayPool<char>.Shared.Return(chars); }
				}
			else
				{
				// A text source (a here-document): a high surrogate that ends a chunk waits for the next, so no target
				// gets half a pair (the files got two U+FFFD, and stdout's Flush wrote the held half alone: 2026-10-27).
				var buf = new char[4097];
				int keep = 0, n;
				while ((n = Console.In.Read(buf, keep, 4096)) > 0 || keep > 0)
					{
					bool more = n > 0;   // false: the input ended on a held half, which goes alone, as before
					n += keep;
					keep = more && char.IsHighSurrogate(buf[n - 1]) ? 1 : 0;
					int m = n - keep;
					Console.Out.Write(buf, 0, m);
					Console.Out.Flush();
					var b = ShellEncoding.Utf8.GetBytes(buf, 0, m);
					foreach (var fs in files) fs.Write(b, 0, b.Length);
					foreach (var w in copies) { w.Write(buf, 0, m); w.Flush(); }
					if (keep == 1) buf[0] = buf[n - 1];
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
				int field = num.Length > 0 ? int.Parse(num.ToString(), System.Globalization.CultureInfo.InvariantCulture) : 0;
				int ch = 0;
				if (p < spec.Length && spec[p] == '.') { p++; var cn = new StringBuilder(); while (p < spec.Length && char.IsDigit(spec[p])) cn.Append(spec[p++]); ch = cn.Length > 0 ? int.Parse(cn.ToString(), System.Globalization.CultureInfo.InvariantCulture) : 0; }
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
		int lines = o.Numeric is not null ? int.Parse(o.Numeric, System.Globalization.CultureInfo.InvariantCulture) : o.GetInt('l', 1000);
		long byteSize = o.Get('b') is string bs ? ParseCount("split", bs, 'b') : 0;
		if (o.Get('n') is not null) throw new UnsupportedOptionException("split", "-n", "chunk mode is not implemented");
		// -a 0 is the default length, as GNU; -a x / -a -1 were taken as the default and as no suffix at all.
		int suffixLen = 2;
		bool widen = true;
		if (o.Get('a') is string av)
			{
			if (!int.TryParse(av, System.Globalization.NumberStyles.AllowLeadingWhite | System.Globalization.NumberStyles.AllowLeadingSign,
					System.Globalization.CultureInfo.InvariantCulture, out suffixLen) || suffixLen < 0)
				{ Console.Error.WriteLine($"split: invalid suffix length: '{av}'"); return 1; }
			widen = suffixLen == 0;
			if (widen) suffixLen = 2;
			}
		bool numeric = o.Has('d');
		string extra = o.GetLong("additional-suffix") ?? "";
		string file = o.Operands.Count > 0 ? o.Operands[0] : "-";
		string prefix = o.Operands.Count > 1 ? o.Operands[1] : "x";

		// The suffix counts up as GNU's next_file_name: each character an index into the alphabet. Without -a it
		// never runs out: when its first character would become the last letter, that letter joins the prefix and
		// the suffix grows by one, restarting (xyz, xzaaa; -d: x89, x9000). With -a the last name is the last:
		// "output file suffixes exhausted". It wrapped to xaa and overwrote the first pieces (2026-10-27).
		string alphabet = numeric ? "0123456789" : "abcdefghijklmnopqrstuvwxyz";
		var digits = new int[suffixLen];
		bool started = false;
		string? NextName()
			{
			if (started)
				for (int i = digits.Length - 1; ; i--)
					{
					if (i < 0) return null;
					if (++digits[i] < alphabet.Length)
						{
						if (widen && i == 0 && digits[0] == alphabet.Length - 1)
							{ prefix += alphabet[^1]; digits = new int[digits.Length + 1]; }
						break;
						}
					digits[i] = 0;
					}
			started = true;
			var sb = new StringBuilder(prefix);
			foreach (int d in digits) sb.Append(alphabet[d]);
			return sb.Append(extra).ToString();
			}
		FileStream? NewPiece()
			{
			if (NextName() is not string name) { Console.Error.WriteLine("split: output file suffixes exhausted"); return null; }
			try { return ShellFile.Create(ShellEnvironment.TranslatePath(name)); }
			catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
				{ Console.Error.WriteLine($"split: {name}: {IoError(ex)}"); return null; }
			}
		// Each piece written as the input is read, a block at a time: it read the whole input first, which failed over
		// 2 GiB (2026-10-27). A piece is opened only when there is a byte for it, as before.
		Stream? input = null;
		FileStream? piece = null;
		try
			{
			input = OpenBytes(file);
			using var block = Block.Rent();
			var buf = block.Bytes;
			long inPiece = 0, linesIn = 0;
			for (int n; (n = input.Read(buf, 0, IoBlock)) > 0; )
				{
				for (int at = 0; at < n; )
					{
					piece ??= NewPiece();
					if (piece is null) return 1;
					int take = n - at;
					bool full = false;
					if (byteSize > 0) { take = (int)Math.Min(take, byteSize - inPiece); full = inPiece + take == byteSize; }
					else if (lines > 0)
						for (int scanned = 0, nl; (nl = buf.AsSpan(at + scanned, n - at - scanned).IndexOf((byte)'\n')) >= 0; )
							{
							scanned += nl + 1;
							if (++linesIn == lines) { take = scanned; full = true; break; }
							}
					piece.Write(buf, at, take);
					at += take; inPiece += take;
					if (full) { piece.Dispose(); piece = null; inPiece = 0; linesIn = 0; }
					}
				}
			return 0;
			}
		catch (BrokenPipeException) { throw; }
		catch (Exception ex) { Console.Error.WriteLine($"split: {file}: {IoError(ex)}"); return 1; }
		finally { piece?.Dispose(); if (file != "-") input?.Dispose(); }
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
		width = Math.Max(1, width);   // -w 0 looped forever

		string Addr(long off) => addr switch
			{
			"n" => "",
			"x" => off.ToString("x6"),
			"d" => off.ToString("D7"),
			_ => Convert.ToString(off, 8).PadLeft(7, '0'),
			};
		int size = fmt.Length > 1 && char.IsDigit(fmt[^1]) ? fmt[^1] - '0' : (fmt[0] == 'c' || fmt[0] == 'a' ? 1 : 2);
		char kind = fmt[0];
		void Line(long off, ReadOnlySpan<byte> line)
			{
			var sb = new StringBuilder(Addr(off));
			for (int i = 0; i < line.Length; i += size)
				{
				if (kind == 'c') { sb.Append(' ').Append(OdChar(line[i]).PadLeft(3)); continue; }
				if (kind == 'a') { sb.Append(' ').Append((line[i] >= 32 && line[i] < 127 ? ((char)line[i]).ToString() : "nul").PadLeft(3)); continue; }
				long v = 0;
				for (int b = 0; b < size && i + b < line.Length; b++) v |= (long)line[i + b] << (8 * b);
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
		// a line at a time as it is read (it read the whole input, which failed over 2 GiB, 2026-10-27)
		Stream? input = null;
		long total;
		try
			{
			input = OpenBytes(file);
			using (var block = Block.Rent()) SkipBytes(input, skip, block.Bytes);
			total = ForEachPiece(input, limit, width, Line);
			}
		catch (BrokenPipeException) { throw; }
		catch (Exception ex) { Console.Error.WriteLine($"od: {file}: {IoError(ex)}"); return 1; }
		finally { if (file != "-") input?.Dispose(); }
		if (addr != "n") Console.WriteLine(Addr(total));   // GNU prints it for empty input too (`0000000`)
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
		long skip = o.Get('s') is string ss ? ParseCount("hexdump", ss, 's') : 0;
		long len = o.Get('n') is string ns ? ParseCount("hexdump", ns, 'n') : long.MaxValue;
		long start = 0;
		void Line(long at, ReadOnlySpan<byte> line)
			{
			long off = start + at;
			var sb = new StringBuilder(off.ToString("x8")).Append(' ');
			for (int i = 0; i < 16; i++)
				{
				if (i == 8) sb.Append(' ');
				sb.Append(i < line.Length ? " " + line[i].ToString("x2") : "   ");
				}
			sb.Append("  |");
			foreach (var b in line) sb.Append(b >= 32 && b < 127 ? (char)b : '.');
			sb.Append('|');
			Console.WriteLine(sb.ToString());
			}
		// a line at a time as it is read (it read the whole input, which failed over 2 GiB, 2026-10-27)
		Stream? input = null;
		long total;
		try
			{
			input = OpenBytes(file);
			using (var block = Block.Rent()) start = SkipBytes(input, skip, block.Bytes);
			total = ForEachPiece(input, len, 16, Line);
			}
		catch (BrokenPipeException) { throw; }
		catch (Exception ex) { Console.Error.WriteLine($"hexdump: {file}: {IoError(ex)}"); return 1; }
		finally { if (file != "-") input?.Dispose(); }
		Console.WriteLine((start + total).ToString("x8"));
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
		int wrap = o.Has('d') ? 0 : o.GetInt('w', 76);
		// a block at a time as it is read (it read the whole input, which failed over 2 GiB, 2026-10-27)
		Stream? input = null;
		try
			{
			input = OpenBytes(file);
			return o.Has('d') ? Base64Decode(input) : Base64Encode(input, wrap);
			}
		catch (BrokenPipeException) { throw; }
		catch (Exception ex) { Console.Error.WriteLine($"base64: {file}: {IoError(ex)}"); return 1; }
		finally { if (file != "-") input?.Dispose(); }
		}

	/// <summary>Whole groups of 3 bytes encoded as they come, the 0-2 left over carried to the next block; lines of
	/// <paramref name="wrap"/> characters (none for 0: one line, and a newline even for no input, as before).</summary>
	private static int Base64Encode(Stream src, int wrap)
		{
		using var block = Block.Rent();
		var buf = block.Bytes;
		var chars = System.Buffers.ArrayPool<char>.Shared.Rent(IoBlock / 3 * 4 + 4);
		long col = 0;
		try
			{
			int carry = 0;
			while (true)
				{
				int n = src.Read(buf, carry, IoBlock - carry), total = carry + n;
				int whole = n > 0 ? total / 3 * 3 : total;   // at the end the rest, padded
				Convert.TryToBase64Chars(buf.AsSpan(0, whole), chars, out int written);
				Put(chars.AsSpan(0, written));
				if (n <= 0) break;
				carry = total - whole;
				buf.AsSpan(whole, carry).CopyTo(buf);
				}
			if (wrap <= 0 || col > 0) Console.WriteLine();
			return 0;
			}
		finally { System.Buffers.ArrayPool<char>.Shared.Return(chars); }

		void Put(ReadOnlySpan<char> s)
			{
			if (wrap <= 0) { Console.Out.Write(s); return; }
			while (s.Length > 0)
				{
				int take = (int)Math.Min(wrap - col, s.Length);
				Console.Out.Write(s[..take]);
				col += take; s = s[take..];
				if (col == wrap) { Console.WriteLine(); col = 0; }
				}
			}
		}

	/// <summary>White space dropped (char.IsWhiteSpace, as before), whole groups of 4 decoded as they come; the last
	/// group, which may hold the padding, waits for the next block or the end. Invalid input: what came before it is
	/// written, then "invalid input", 1 (it wrote nothing then, the whole input decoded at once).</summary>
	private static int Base64Decode(Stream src)
		{
		using var inBlock = Block.Rent();
		using var pendBlock = Block.Rent();
		using var outBlock = Block.Rent();
		byte[] input = inBlock.Bytes, pend = pendBlock.Bytes, output = outBlock.Bytes;
		var white = WcAscii.White;
		var sink = new ByteSink();
		int have = 0;
		bool ok = true;
		for (int n; ok && (n = src.Read(input, 0, IoBlock - 4)) > 0; )   // room for the group held back
			{
			foreach (var b in input.AsSpan(0, n)) if (b >= 128 || !white[b]) pend[have++] = b;
			int keep = have % 4 == 0 ? Math.Min(4, have) : have % 4, ready = have - keep;
			ok = Decode(ready, final: true);
			pend.AsSpan(ready, keep).CopyTo(pend);
			have = keep;
			}
		if (ok && have % 4 is 2 or 3)
			{
			// a last group cut short (no padding): written as GNU's base64 writes it, then invalid
			pend.AsSpan(have, 4 - have % 4).Fill((byte)'=');
			Decode(have + 4 - have % 4, final: true);
			ok = false;
			}
		else if (ok) ok = Decode(have, final: true);
		sink.Flush();
		if (!ok) { Console.Error.WriteLine("base64: invalid input"); return 1; }
		return 0;

		bool Decode(int count, bool final)
			{
			if (count == 0) return true;
			var status = System.Buffers.Text.Base64.DecodeFromUtf8(pend.AsSpan(0, count), output, out int used, out int written, final);
			sink.Write(output, 0, written);
			return status == System.Buffers.OperationStatus.Done && used == count;
			}
		}

	private static int Checksum(string tool, List<string> args)
		{
		var o = Opts.Parse(tool, args, "bctz", ["binary:b", "check:c", "text:t", "tag", "quiet", "status", "zero:z"]);
		var algorithm = tool switch
			{
			"md5sum" => System.Security.Cryptography.HashAlgorithmName.MD5,
			"sha1sum" => System.Security.Cryptography.HashAlgorithmName.SHA1,
			"sha512sum" => System.Security.Cryptography.HashAlgorithmName.SHA512,
			_ => System.Security.Cryptography.HashAlgorithmName.SHA256,
			};
		// hashed as it is read, a block at a time: the whole input was one array, which failed over 2 GiB (2026-10-27)
		string HexOf(Stream s)
			{
			using var h = System.Security.Cryptography.IncrementalHash.CreateHash(algorithm);
			using var block = Block.Rent();
			for (int n; (n = s.Read(block.Bytes, 0, IoBlock)) > 0; ) h.AppendData(block.Bytes, 0, n);
			return Convert.ToHexString(h.GetHashAndReset()).ToLowerInvariant();
			}
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
						try { using var t = ShellFile.OpenInput(ShellEnvironment.TranslatePath(target)); actual = HexOf(t); }
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
				var input = OpenBytes(f);
				string hex;
				try { hex = HexOf(input); }
				finally { if (f != "-") input.Dispose(); }
				if (o.HasLong("tag")) Console.WriteLine($"{tool[..^3].ToUpperInvariant()} ({f}) = {hex}");
				else Console.WriteLine($"{hex}  {f}");
				}
			catch (Exception ex) { Console.Error.WriteLine($"{tool}: {f}: {IoError(ex)}"); rc = 1; }
			}
		return rc;
		}

	// ── strings ─────────────────────────────────────────────────────────────────

	/// <summary>GNU strings 2.38's usage text (to standard error after a bad value, to standard output for
	/// `--help`), all but its last line: GNU's names its BFD targets, and C#Bash reads no object format.</summary>
	private const string StringsUsage =
		"Usage: strings [option(s)] [file(s)]\n" +
		" Display printable strings in [file(s)] (stdin by default)\n" +
		" The options are:\n" +
		"  -a - --all                Scan the entire file, not just the data section [default]\n" +
		"  -d --data                 Only scan the data sections in the file\n" +
		"  -f --print-file-name      Print the name of the file before each string\n" +
		"  -n <number>               Locate & print any sequence of at least <number>\n" +
		"    --bytes=<number>         displayable characters.  (The default is 4).\n" +
		"  -t --radix={o,d,x}        Print the location of the string in base 8, 10 or 16\n" +
		"  -w --include-all-whitespace Include all whitespace as valid string characters\n" +
		"  -o                        An alias for --radix=o\n" +
		"  -T --target=<BFDNAME>     Specify the binary file format\n" +
		"  -e --encoding={s,S,b,l,B,L} Select character size and endianness:\n" +
		"                            s = 7-bit, S = 8-bit, {b,l} = 16-bit, {B,L} = 32-bit\n" +
		"  --unicode={default|show|invalid|hex|escape|highlight}\n" +
		"  -U {d|s|i|x|e|h}          Specify how to treat UTF-8 encoded unicode characters\n" +
		"  -s --output-separator=<string> String used to separate strings in output.\n" +
		"  @<file>                   Read options from <file>\n" +
		"  -h --help                 Display this information\n" +
		"  -v -V --version           Print the program's version number\n" +
		"strings: C#Bash in-process: no object formats; -d, -T, -U / --unicode and @file are not available\n";

	/// <summary>
	/// `strings` as GNU strings 2.38 (binutils; scope ratified by the architect 2026-10-14). The whole file is
	/// scanned for runs of at least `-n` (4) printable characters, each written with the separator after it
	/// (`-s`, a newline). Printable is ASCII 32-126 and tab; `-e S` adds bytes above 127, `-w` all white space.
	/// `-e b|l|B|L` read 16- and 32-bit characters, big- or little-endian, one above 255 never printable. After
	/// a character that is not printable, scanning resumes ONE BYTE past its start (GNU's unget_part_char), so
	/// odd-aligned UTF-16 is found. `-f` names the file, `-t`/`-o` give the run's byte offset. `-d`, `-T`,
	/// `-U`/`--unicode` and `@file` need object formats or UTF-8 display C#Bash lacks: a PATH `strings` takes
	/// the command if there is one (DECISIONS 2026-09-04 #2), else the not-available notice. Until 2026-10-14
	/// `strings` was not a command here at all (Git for Windows ships no binutils).
	/// </summary>
	private int Strings(List<string> args)
		{
		var o = Opts.Parse("strings", args, "adfhn:wot:e:T:s:U:vV",
			["all:a", "data:d", "print-file-name:f", "bytes=:n", "include-all-whitespace:w", "radix=:t", "target=:T",
			 "encoding=:e", "unicode=:U", "output-separator=:s", "help:h", "version:v"], allowNumeric: true);
		if (o.Has('h')) { Console.Out.Write(StringsUsage); return 0; }
		if (o.Has('v') || o.Has('V'))
			{
			// never "GNU strings": a script that checks for binutils must not take this for it
			Console.WriteLine("strings (C#Bash in-process, GNU strings 2.38 compatible) " + BuildInfo.Stamp);
			return 0;
			}
		string? missing = o.Has('d') ? "strings -d (--data)" : o.Has('T') ? "strings -T (--target)"
			: o.Has('U') ? "strings -U (--unicode)" : o.Operands.Any(a => a.Length > 1 && a[0] == '@') ? "strings @file" : null;
		if (missing is not null)
			{
			if (_env.Get("BASH_COREUTILS") != "builtin" && _eval.ResolveOnPath("strings") is not null)
				throw new UnsupportedOptionException("strings", missing, "not available in-process");   // the PATH strings runs it
			foreach (var line in NotAvailable.Lines("strings: ", missing)) Console.Error.WriteLine(line);
			return 2;
			}
		int Usage() { Console.Error.Write(StringsUsage); return 1; }

		// -n / --bytes as C's strtoul(s, &end, 0): 010 is octal, 0x5 hex, trailing text an error; a negative
		// number wraps to a huge minimum (GNU prints nothing); `-N` the digits alone
		int min = 4;
		if (o.Get('n') is { } nv)
			{
			if (!StrToUl(nv, out var v)) { Console.Error.WriteLine($"strings: invalid integer argument {nv}"); return 1; }
			min = v > int.MaxValue ? int.MaxValue : (int)v;
			}
		else if (o.Numeric is { } digits) min = int.TryParse(digits, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var d) ? d : int.MaxValue;
		if (min < 1) { Console.Error.WriteLine($"strings: invalid minimum string length {min}"); return 1; }
		int radix = 0;   // the last of -t / -o wins
		foreach (var (opt, val) in o.Sequence)
			if (opt == 'o') radix = 8;
			else if (opt == 't') radix = val switch { "o" => 8, "d" => 10, "x" => 16, _ => -1 };
		if (radix < 0) return Usage();
		char enc = 's';
		if (o.Get('e') is { } e) { if (e.Length != 1 || !"sSblBL".Contains(e[0])) return Usage(); enc = e[0]; }
		string sep = o.Has('s') ? o.Get('s')! : "\n";
		bool showName = o.Has('f'), allWs = o.Has('w');
		var files = o.Operands.Where(a => a != "-").ToList();   // `-` is GNU's alias for -a, not standard input
		if (o.Operands.Count > 0 && files.Count == 0) return Usage();

		var rout = Evaluator.CurrentRawStdout();
		if (rout is not null) Console.Out.Flush();
		var output = new StringsOutput(rout);
		void Scan(Stream input, string name) => StringsScan(input, name, output, min, radix, enc, allWs, showName, sep);
		int rc = 0;
		void Fail(string message) { output.Flush(); Console.Error.WriteLine(message); rc = 1; }   // stdout first, as GNU's non_fatal
		if (files.Count == 0)
			Scan(Evaluator.CurrentRawStdin() ?? new MemoryStream(ShellEncoding.Utf8.GetBytes(Console.In.ReadToEnd())), "{standard input}");
		foreach (var f in files)
			{
			var p = ShellEnvironment.TranslatePath(f);
			if (Directory.Exists(p)) { Fail($"strings: Warning: '{f}' is a directory"); continue; }
			if (!ShellFile.Exists(p)) { Fail($"strings: '{f}': No such file"); continue; }
			try { using var fs = ShellFile.OpenInput(p); Scan(fs, f); }
			catch (Exception ex) when (ex is not BrokenPipeException && ex is IOException or UnauthorizedAccessException)
				{ Fail($"strings: {f}: {IoError(ex)}"); }
			}
		output.Flush();
		return rc;
		}

	/// <summary>C's strtoul(s, &amp;end, 0) where all of <paramref name="s"/> must be used: leading white space and
	/// a sign, `0x` hex, a leading `0` octal, else decimal; a minus wraps around as in C.</summary>
	private static bool StrToUl(string s, out ulong value)
		{
		value = 0;
		int i = 0;
		while (i < s.Length && char.IsWhiteSpace(s[i])) i++;
		bool negative = false;
		if (i < s.Length && s[i] is '+' or '-') { negative = s[i] == '-'; i++; }
		int radix = 10;
		if (i + 2 < s.Length && s[i] == '0' && s[i + 1] is 'x' or 'X' && Uri.IsHexDigit(s[i + 2])) { radix = 16; i += 2; }
		else if (i < s.Length && s[i] == '0') radix = 8;
		int first = i;
		for (; i < s.Length; i++)
			{
			int digit = Uri.IsHexDigit(s[i]) ? Uri.FromHex(s[i]) : 99;
			if (digit >= radix) break;
			value = unchecked(value * (ulong)radix + (ulong)digit);
			}
		if (i == first) return s.Length == 0;   // no digits: strtoul consumed nothing, valid only for ""
		if (i < s.Length) return false;
		if (negative) value = unchecked(0 - value);
		return true;
		}

	/// <summary>`strings`' output: bytes, written raw when standard output is a byte stream, in 64 KB pieces.</summary>
	private sealed class StringsOutput(Stream? raw)
		{
		private readonly MemoryStream _buffer = new();
		public void Byte(int b) => _buffer.WriteByte((byte)b);
		public void Text(string s) { var b = ShellEncoding.Utf8.GetBytes(s); _buffer.Write(b, 0, b.Length); }
		public void EndOfString(string separator) { Text(separator); if (_buffer.Length >= 65536) Flush(); }
		public void Flush()
			{
			if (_buffer.Length == 0) return;
			if (raw is not null) { raw.Write(_buffer.GetBuffer(), 0, (int)_buffer.Length); raw.Flush(); }
			else { Console.Out.Write(ShellEncoding.Utf8.GetString(_buffer.GetBuffer(), 0, (int)_buffer.Length)); Console.Out.Flush(); }
			_buffer.SetLength(0);
			}
		}

	/// <summary>GNU strings' print_strings over one input: a run starts where <paramref name="min"/> printable
	/// characters follow one another and goes on to the next character that is not printable.</summary>
	private static void StringsScan(Stream input, string name, StringsOutput output, int min, int radix, char enc,
		bool allWhitespace, bool showName, string separator)
		{
		int width = enc is 's' or 'S' ? 1 : enc is 'b' or 'l' ? 2 : 4;
		using var block = Block.Rent();   // pooled (it was 64 KiB of its own): 2000 runs on a tiny file 59 -> 55 ms (2026-10-27)
		var buffer = block.Bytes;
		int pos = 0, len = 0;
		var pushedBack = new byte[8];   // bytes an unget put back, read before the stream (last in, first out)
		int pushed = 0;
		var ch = new byte[4];
		long address = 0;
		int ReadByte()
			{
			if (pushed > 0) return pushedBack[--pushed];
			if (pos == len) { len = input.Read(buffer, 0, buffer.Length); pos = 0; if (len <= 0) { len = 0; return -1; } }
			return buffer[pos++];
			}
		long GetChar()
			{
			for (int k = 0; k < width; k++) { int b = ReadByte(); if (b < 0) return -1; ch[k] = (byte)b; address++; }
			return enc switch
				{
				'b' => (ch[0] << 8) | ch[1],
				'l' => (ch[1] << 8) | ch[0],
				'B' => ((long)ch[0] << 24) | ((long)ch[1] << 16) | ((long)ch[2] << 8) | ch[3],
				'L' => ((long)ch[3] << 24) | ((long)ch[2] << 16) | ((long)ch[1] << 8) | ch[0],
				_ => ch[0],
				};
			}
		void UngetPart()   // resume one byte past the start of the character just read
			{
			if (width == 1) return;
			address -= width - 1;
			for (int k = width - 1; k >= 1; k--) pushedBack[pushed++] = ch[k];
			}
		bool Printable(long c) => c is >= 0 and <= 255 && (c == '\t' || c is >= 32 and < 127 || (enc == 'S' && c > 127)
			|| (allWhitespace && c is ' ' or '\n' or '\v' or '\f' or '\r'));

		var run = new List<byte>();
		while (true)
			{
			long start = address;
			run.Clear();
			bool broken = false;
			while (run.Count < min)
				{
				long c = GetChar();
				if (c < 0) return;
				if (!Printable(c)) { UngetPart(); broken = true; break; }
				run.Add((byte)c);
				}
			if (broken) continue;
			if (showName) output.Text(name + ": ");
			if (radix != 0)
				output.Text((radix == 8 ? Convert.ToString(start, 8) : radix == 16 ? start.ToString("x") : start.ToString()).PadLeft(7) + " ");
			foreach (var b in run) output.Byte(b);
			while (true)
				{
				long c = GetChar();
				if (c < 0) break;
				if (!Printable(c)) { UngetPart(); break; }
				output.Byte((int)c);
				}
			output.EndOfString(separator);
			}
		}
	}
