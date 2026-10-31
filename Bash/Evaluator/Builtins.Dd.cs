using System.Diagnostics;
using System.Globalization;

namespace Bash.Evaluator;

/// <summary>
/// `dd` and `cksum`, in-process as GNU coreutils 8.32 (2026-10-31, installer-a4's unmet need; DECISIONS "GO: `dd` and
/// `cksum`"). PATH found only a 1997 port here (UnixUtils' "GNU fileutils 3.16"): its dd refused `status=none`, its cksum
/// wrote CRLF. What dd does not do (conv=block/unblock/ascii/ebcdic/ibm/sparse, iflag=direct and the like, --help) is
/// unsupported: handed over to a PATH program and logged, as every coreutil (DECISIONS 2026-09-04 #2).
/// </summary>
public sealed partial class Builtins
	{
	// ── dd ──────────────────────────────────────────────────────────────────────

	private static readonly HashSet<string> DdConvs = ["notrunc", "sync", "ucase", "lcase", "swab", "excl", "nocreat", "noerror", "fsync", "fdatasync"];
	private static readonly HashSet<string> DdConvsElsewhere = ["ascii", "ebcdic", "ibm", "block", "unblock", "sparse"];
	private static readonly HashSet<string> DdIflags = ["fullblock", "skip_bytes", "count_bytes", "binary", "text"];
	private static readonly HashSet<string> DdOflags = ["seek_bytes", "append", "binary", "text"];
	private static readonly HashSet<string> DdFlagsElsewhere = ["direct", "dsync", "sync", "nonblock", "noatime", "nocache", "noctty", "nofollow", "directory"];

	private static int Dd(List<string> args)
		{
		string? inName = null, outName = null;
		long ibs = 512, obs = 512, count = -1, skip = 0, seek = 0;
		bool blockForBlock = false;   // bs= given: each block written as read; otherwise output is reblocked to obs
		var conv = new HashSet<string>();
		var iflag = new HashSet<string>();
		var oflag = new HashSet<string>();
		string status = "default";
		foreach (var a in args)
			{
			int eq = a.IndexOf('=');
			if (eq <= 0) return DdUsage($"unrecognized operand '{a}'");
			string key = a[..eq], val = a[(eq + 1)..];
			switch (key)
				{
				case "if": inName = val; break;
				case "of": outName = val; break;
				case "bs": case "ibs": case "obs":
					{
					if (DdNumber(val) is not long n || n <= 0) return DdError($"invalid number: '{val}'");
					if (key == "bs") { ibs = obs = n; blockForBlock = true; }
					else if (key == "ibs") ibs = n;
					else obs = n;
					break;
					}
				case "count": case "skip": case "seek":
					{
					if (DdNumber(val) is not long n) return DdError($"invalid number: '{val}'");
					if (key == "count") count = n; else if (key == "skip") skip = n; else seek = n;
					break;
					}
				case "conv":
					foreach (var c in val.Split(','))
						{
						if (DdConvsElsewhere.Contains(c)) throw new UnsupportedOptionException("dd", a, $"dd: conv={c} is not available in-process");
						if (!DdConvs.Contains(c)) return DdUsage($"invalid conversion: '{c}'");
						conv.Add(c);
						}
					break;
				case "iflag": case "oflag":
					{
					var known = key == "iflag" ? DdIflags : DdOflags;
					foreach (var f in val.Split(','))
						{
						if (DdFlagsElsewhere.Contains(f)) throw new UnsupportedOptionException("dd", a, $"dd: {key}={f} is not available in-process");
						if (!known.Contains(f)) return DdUsage($"invalid {(key == "iflag" ? "input" : "output")} flag: '{f}'");
						(key == "iflag" ? iflag : oflag).Add(f);
						}
					break;
					}
				case "status":
					if (val is not ("none" or "noxfer" or "progress")) return DdUsage($"invalid status level: '{val}'");
					status = val;
					break;
				default: return DdUsage($"unrecognized operand '{a}'");
				}
			}
		// swab moves a byte across blocks, so GNU reblocks even with bs= (measured: `conv=swab bs=3` over 5 bytes is
		// 1+1 records out, not 0+3)
		if (conv.Contains("swab")) blockForBlock = false;
		if (conv.Contains("lcase") && conv.Contains("ucase")) return DdError("cannot combine lcase and ucase");
		if (conv.Contains("excl") && conv.Contains("nocreat")) return DdError("cannot combine excl and nocreat");

		long fullIn = 0, partIn = 0, fullOut = 0, partOut = 0, bytesOut = 0;
		var clock = Stopwatch.StartNew();
		void Report()
			{
			if (status == "none") return;
			var e = Console.Error;
			e.Write($"{fullIn}+{partIn} records in\n{fullOut}+{partOut} records out\n");
			if (status == "noxfer") { e.Flush(); return; }
			e.Write(DdBytesLine(bytesOut, clock.Elapsed.TotalSeconds) + "\n");
			e.Flush();
			}

		// the input
		Stream input;
		bool closeInput = false;
		if (inName is null) input = OpenBytes("-");
		else
			{
			var p = ShellEnvironment.TranslatePath(inName);
			// GNU opens a directory and fails its first read, so its report follows; Windows refuses the open
			if (ShellFile.Device(inName) is null && Directory.Exists(p)) { Console.Error.WriteLine($"dd: error reading '{inName}': Is a directory"); Report(); return 1; }
			try { input = ShellFile.OpenInput(p); closeInput = true; }
			catch (Exception ex) { Console.Error.WriteLine($"dd: failed to open '{inName}': {IoError(ex)}"); return 1; }
			}
		int Quit(int code) { if (closeInput) input.Dispose(); return code; }

		// the output
		Stream? output = null;
		ByteSink? sink = null;
		bool closeOutput = false;
		string outShown = outName ?? "standard output";
		long seekBytes = oflag.Contains("seek_bytes") ? seek : checked(seek * obs);
		try
			{
			string? device = outName is null ? "fd:1" : ShellFile.Device(outName);
			if (device is "null" or "zero" or "random") output = Stream.Null;
			else if (device == "fd:1" || (outName is null))
				{
				if (seekBytes > 0)
					{
					if (Evaluator.CurrentRawStdout() is { CanSeek: true } raw) { output = raw; raw.Seek(seekBytes, SeekOrigin.Current); }
					else { Console.Error.WriteLine("dd: 'standard output': cannot seek: Illegal seek"); return Quit(1); }
					}
				else sink = new ByteSink();
				}
			else if (device == "fd:2") output = Console.OpenStandardError();
			else if (device is not null) throw new UnsupportedOptionException("dd", $"of={outName}", $"dd: of={outName} is not available in-process");
			else
				{
				var p = ShellEnvironment.TranslatePath(outName!);
				string? fail = Directory.Exists(p) ? "Is a directory"
				             : conv.Contains("excl") && File.Exists(p) ? "File exists"
				             : conv.Contains("nocreat") && !File.Exists(p) ? "No such file or directory" : null;
				if (fail is not null) { Console.Error.WriteLine($"dd: failed to open '{outName}': {fail}"); return Quit(1); }
				FileStream fs;
				try { fs = new FileStream(p, conv.Contains("excl") ? FileMode.CreateNew : FileMode.OpenOrCreate, FileAccess.Write, ShellFile.Share); }
				catch (Exception ex) { Console.Error.WriteLine($"dd: failed to open '{outName}': {IoError(ex)}"); return Quit(1); }
				closeOutput = true;
				output = fs;
				// GNU: truncated at the seek offset unless notrunc; appending writes at the end
				if (!conv.Contains("notrunc") && !oflag.Contains("append")) fs.SetLength(seekBytes);
				fs.Position = oflag.Contains("append") ? fs.Length : seekBytes;
				}
			}
		catch (UnsupportedOptionException) { Quit(0); throw; }

		int rc = 0;
		try
			{
			// skip: seek a seekable input, read and drop the bytes of any other
			long skipBytes = iflag.Contains("skip_bytes") ? skip : checked(skip * ibs);
			if (skipBytes > 0)
				{
				if (input.CanSeek) input.Position = Math.Min(input.Length, input.Position + skipBytes);
				else
					{
					using var drop = Block.Rent();
					for (long left = skipBytes; left > 0; )
						{
						int n = input.Read(drop.Bytes, 0, (int)Math.Min(left, IoBlock));
						if (n <= 0) break;
						left -= n;
						}
					}
				}

			bool countBytes = iflag.Contains("count_bytes"), fullBlock = iflag.Contains("fullblock"), sync = conv.Contains("sync");
			bool ucase = conv.Contains("ucase"), lcase = conv.Contains("lcase"), swab = conv.Contains("swab");
			if (ibs > int.MaxValue - 1 || obs > int.MaxValue) { Console.Error.WriteLine("dd: memory exhausted"); return 1; }
			var ibuf = new byte[ibs + 1];   // one byte before the block: swab's carried byte
			var obuf = blockForBlock ? null : new byte[obs];
			int ofill = 0;
			int saved = -1;   // swab: the odd last byte of a block, carried to the next
			long readSoFar = 0, blocks = 0;

			void Put(byte[] b, int at, int n)
				{
				if (n <= 0) return;
				if (sink is not null) sink.Write(b, at, n); else output!.Write(b, at, n);
				bytesOut += n;
				}
			void Emit(byte[] b, int at, int n)   // one output record
				{
				Put(b, at, n);
				if (n == obs) fullOut++; else partOut++;
				}

			while (true)
				{
				if (!countBytes && count >= 0 && blocks >= count) break;
				int want = (int)ibs;
				if (countBytes && count >= 0) { if (readSoFar >= count) break; want = (int)Math.Min(ibs, count - readSoFar); }
				int got = input.Read(ibuf, 1, want);
				if (fullBlock) while (got > 0 && got < want && input.Read(ibuf, 1 + got, want - got) is var more and > 0) got += more;
				if (got <= 0) break;
				blocks++;
				readSoFar += got;
				if (got == ibs) fullIn++; else partIn++;
				if (sync && got < ibs) { Array.Clear(ibuf, 1 + got, (int)ibs - got); got = (int)ibs; }
				int start = 1, len = got;
				if (ucase || lcase)
					for (int k = start; k < start + len; k++)
						{
						byte c = ibuf[k];
						if (ucase && c is >= (byte)'a' and <= (byte)'z') ibuf[k] = (byte)(c - 32);
						else if (lcase && c is >= (byte)'A' and <= (byte)'Z') ibuf[k] = (byte)(c + 32);
						}
				if (swab)
					{
					if (saved >= 0) { start = 0; ibuf[0] = (byte)saved; len++; saved = -1; }
					if ((len & 1) == 1) { saved = ibuf[start + len - 1]; len--; }
					for (int k = start; k + 1 < start + len; k += 2) (ibuf[k], ibuf[k + 1]) = (ibuf[k + 1], ibuf[k]);
					}
				if (blockForBlock) Emit(ibuf, start, len);
				else
					for (int at = start, left = len; left > 0; )
						{
						int take = Math.Min(left, (int)obs - ofill);
						Buffer.BlockCopy(ibuf, at, obuf!, ofill, take);
						ofill += take; at += take; left -= take;
						if (ofill == obs) { Emit(obuf!, 0, ofill); ofill = 0; }
						}
				}
			if (saved >= 0)
				{
				if (blockForBlock) { ibuf[0] = (byte)saved; Emit(ibuf, 0, 1); }
				else { obuf![ofill++] = (byte)saved; if (ofill == obs) { Emit(obuf, 0, ofill); ofill = 0; } }
				}
			if (ofill > 0) Emit(obuf!, 0, ofill);
			sink?.Flush();
			output?.Flush();
			if (output is FileStream f && (conv.Contains("fsync") || conv.Contains("fdatasync"))) f.Flush(true);
			}
		catch (IOException ex) { Console.Error.WriteLine($"dd: error writing '{outShown}': {IoError(ex)}"); rc = 1; }
		finally
			{
			if (closeInput) input.Dispose();
			if (closeOutput) output!.Dispose();
			}
		Report();
		return rc;
		}

	private static int DdError(string message) { Console.Error.WriteLine($"dd: {message}"); return 1; }

	private static int DdUsage(string message)
		{
		Console.Error.WriteLine($"dd: {message}");
		Console.Error.WriteLine("Try 'dd --help' for more information.");
		return 1;
		}

	/// <summary>A dd number as GNU reads one: digits, then a multiplier (c 1, w 2, b 512; k K M G T P E 1024s, the same
	/// letter with `B` 1000s, with `iB` 1024s), `AxB` a product; null when it is none (a sign, an empty string,
	/// anything else) or too large.</summary>
	private static long? DdNumber(string s)
		{
		int x = s.IndexOf('x');
		if (x > 0 && x < s.Length - 1) return DdNumber(s[..x]) is long a && DdNumber(s[(x + 1)..]) is long b ? Mul(a, b) : null;
		int i = 0;
		while (i < s.Length && char.IsAsciiDigit(s[i])) i++;
		if (i == 0 || !long.TryParse(s.AsSpan(0, i), NumberStyles.None, CultureInfo.InvariantCulture, out long n)) return null;
		long mult = s[i..] switch
			{
			"" => 1, "c" => 1, "w" => 2, "b" => 512,
			"k" or "K" or "KiB" => 1L << 10, "kB" or "KB" => 1000,
			"M" or "MiB" => 1L << 20, "MB" => 1000_000,
			"G" or "GiB" => 1L << 30, "GB" => 1000_000_000,
			"T" or "TiB" => 1L << 40, "TB" => 1000_000_000_000,
			"P" or "PiB" => 1L << 50, "PB" => 1000_000_000_000_000,
			"E" or "EiB" => 1L << 60, "EB" => 1000_000_000_000_000_000,
			_ => -1,
			};
		return mult < 0 ? null : Mul(n, mult);
		static long? Mul(long a, long b) { try { return checked(a * b); } catch (OverflowException) { return null; } }
		}

	/// <summary>dd's last report line: the bytes, with GNU's SI and IEC sizes from 1000 and 1024 bytes up, then the
	/// seconds (`%g`) and the rate.</summary>
	private static string DdBytesLine(long bytes, double seconds)
		{
		string head = bytes == 1 ? "1 byte copied"
		            : bytes < 1000 ? $"{bytes} bytes copied"
		            : bytes < 1024 ? $"{bytes} bytes ({DdHuman(bytes, 1000, DdSi)}) copied"
		            : $"{bytes} bytes ({DdHuman(bytes, 1000, DdSi)}, {DdHuman(bytes, 1024, DdIec)}) copied";
		if (seconds <= 0) seconds = 1e-9;
		var secs = seconds.ToString("G6", CultureInfo.InvariantCulture).Replace('E', 'e');
		double rate = bytes / seconds;
		string rateText;
		if (rate < 999.5) rateText = (rate / 1000).ToString("0.0", CultureInfo.InvariantCulture) + " kB";
		else
			{
			int u = 0;
			while (u < DdSi.Length - 1 && rate >= 999.5) { rate /= 1000; u++; }
			rateText = (rate < 10 ? rate.ToString("0.0", CultureInfo.InvariantCulture) : rate.ToString("0", CultureInfo.InvariantCulture)) + " " + DdSi[u - 1];
			}
		return $"{head}, {secs} s, {rateText}/s";
		}

	private static readonly string[] DdSi = ["kB", "MB", "GB", "TB", "PB", "EB"];
	private static readonly string[] DdIec = ["KiB", "MiB", "GiB", "TiB", "PiB", "EiB"];

	/// <summary>A size as GNU's human_readable writes it for dd (autoscale, to nearest): one decimal below 10, else
	/// whole; a value that rounds up to the base moves to the next unit (999999 bytes: 1.0 MB).</summary>
	private static string DdHuman(long bytes, int b, string[] units)
		{
		double v = bytes;
		int e = 0;
		while (v >= b && e < units.Length) { v /= b; e++; }
		while (true)
			{
			if (v < 10)
				{
				double r = Math.Round(v * 10, MidpointRounding.AwayFromZero) / 10;
				if (r < 10) return r.ToString("0.0", CultureInfo.InvariantCulture) + " " + units[e - 1];
				v = r;
				}
			double whole = Math.Round(v, MidpointRounding.AwayFromZero);
			if (whole < b || e == units.Length) return whole.ToString("0", CultureInfo.InvariantCulture) + " " + units[e - 1];
			v = whole / b;
			e++;
			}
		}

	// ── cksum ───────────────────────────────────────────────────────────────────

	private static uint[]? _cksumTable;

	/// <summary>The POSIX CRC (polynomial 0x04C11DB7, most significant bit first), built on first use.</summary>
	private static uint[] CksumTable()
		{
		if (_cksumTable is { } t) return t;
		t = new uint[256];
		for (uint i = 0; i < 256; i++)
			{
			uint c = i << 24;
			for (int k = 0; k < 8; k++) c = (c & 0x80000000) != 0 ? (c << 1) ^ 0x04C11DB7 : c << 1;
			t[i] = c;
			}
		return _cksumTable = t;
		}

	/// <summary>`cksum [FILE]...`: the POSIX CRC of each input and its length, then its name (none for standard input
	/// read without operands), a line ending in LF; GNU's errors, exit 1.</summary>
	private static int Cksum(List<string> args)
		{
		var o = Opts.Parse("cksum", args, "", []);
		var table = CksumTable();
		List<string?> names = o.Operands.Count == 0 ? [null] : [.. o.Operands];
		int rc = 0;
		foreach (var f in names)
			{
			var shown = f ?? "-";
			try
				{
				if (f is not null && f != "-" && ShellFile.Device(f) is null && Directory.Exists(ShellEnvironment.TranslatePath(f)))
					{ Console.Error.WriteLine($"cksum: {f}: Is a directory"); rc = 1; continue; }
				var input = OpenBytes(shown);
				uint crc = 0;
				long size = 0;
				try
					{
					using var block = Block.Rent();
					for (int n; (n = input.Read(block.Bytes, 0, IoBlock)) > 0; )
						{
						size += n;
						foreach (var b in block.Bytes.AsSpan(0, n)) crc = (crc << 8) ^ table[((crc >> 24) ^ b) & 0xFF];
						}
					}
				finally { if (shown != "-") input.Dispose(); }
				for (long len = size; len != 0; len >>= 8) crc = (crc << 8) ^ table[((crc >> 24) ^ (uint)(len & 0xFF)) & 0xFF];
				crc = ~crc;
				Console.Out.Write(f is null ? $"{crc} {size}\n" : $"{crc} {size} {f}\n");
				}
			catch (Exception ex) when (ex is not UnsupportedOptionException) { Console.Error.WriteLine($"cksum: {shown}: {IoError(ex)}"); rc = 1; }
			}
		return rc;
		}
	}
