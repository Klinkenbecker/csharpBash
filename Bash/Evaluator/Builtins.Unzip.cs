using System.Globalization;
using System.IO.Compression;

namespace Bash.Evaluator;

/// <summary>
/// `unzip` in-process (2026-11-05, DECISIONS "GO: unzip in-process"): UnZip 6.00's most-used forms (Debian's build, the
/// reference), its messages byte for byte and its exit codes, lines ended by LF. The 2001 UnxUtils unzip.exe first on
/// PATH wrote CRLF, so `unzip -l x | grep 'dll$'` found nothing (koliada-net-b9). The central directory is read here:
/// .NET 8 does not expose an entry's compression method, which UnZip's "extracting" (stored) and "inflating"
/// (deflated) name. Only the inflating is .NET's (DeflateStream). Anything else goes to the PATH unzip.
/// </summary>
public sealed partial class Builtins
	{
	/// <summary>One central-directory entry. <c>Data</c> is the local header's offset (a prefix before the archive, as
	/// in a self-extractor, already added); <c>Stamp</c> is the time as UnZip lists it, <c>Time</c> the mtime (local).</summary>
	private sealed record ZipItem(string Name, int Method, int Flags, uint Crc, long Packed, long Size, long Data,
		string Stamp, DateTime? Time, bool Link)
		{
		public bool IsDir => Name.EndsWith('/');
		}

	private static uint[]? _zipCrcTable;

	/// <summary>The CRC-32 a zip entry carries (IEEE, reflected; not cksum's POSIX CRC).</summary>
	private static uint[] ZipCrcTable()
		{
		if (_zipCrcTable is { } t0) return t0;
		var t = new uint[256];
		for (uint i = 0; i < 256; i++)
			{
			uint c = i;
			for (int k = 0; k < 8; k++) c = (c & 1) != 0 ? 0xEDB88320 ^ (c >> 1) : c >> 1;
			t[i] = c;
			}
		return _zipCrcTable = t;
		}

	private static int ZipU16(byte[] b, int at) => b[at] | b[at + 1] << 8;
	private static uint ZipU32(byte[] b, int at) => (uint)(b[at] | b[at + 1] << 8 | b[at + 2] << 16 | b[at + 3] << 24);
	private static ulong ZipU64(byte[] b, int at) => ZipU32(b, at) | (ulong)ZipU32(b, at + 4) << 32;

	private static byte[] ZipReadAt(FileStream fs, long at, int count)
		{
		var b = new byte[count];
		fs.Position = at;
		fs.ReadExactly(b, 0, count);
		return b;
		}

	/// <summary>The archive's entries in central-directory order, or null when it has no end-of-central-directory record
	/// that reads (not a zip). Zip64 and a prefix before the archive are read; a multi-part archive is handed off.</summary>
	private static List<ZipItem>? ZipDirectory(FileStream fs)
		{
		long len = fs.Length;
		if (len < 22) return null;
		int tail = (int)Math.Min(len, 22 + 65535);
		var buf = ZipReadAt(fs, len - tail, tail);
		int e = -1;
		for (int i = tail - 22; i >= 0; i--)
			if (buf[i] == 0x50 && buf[i + 1] == 0x4B && buf[i + 2] == 5 && buf[i + 3] == 6) { e = i; break; }
		if (e < 0) return null;
		long end = len - tail + e;   // where the directory ends: the end record, or the Zip64 one before it
		long disk = ZipU16(buf, e + 4), cdDisk = ZipU16(buf, e + 6);
		long count = ZipU16(buf, e + 10), cdSize = ZipU32(buf, e + 12), cdOffset = ZipU32(buf, e + 16);
		if (count == 0xFFFF || cdSize == 0xFFFFFFFF || cdOffset == 0xFFFFFFFF)
			{
			if (end < 20) return null;
			var loc = ZipReadAt(fs, end - 20, 20);
			if (ZipU32(loc, 0) != 0x07064B50) return null;
			long at = (long)ZipU64(loc, 8);
			if (at < 0 || at + 56 > len) return null;
			var rec = ZipReadAt(fs, at, 56);
			if (ZipU32(rec, 0) != 0x06064B50) return null;
			disk = ZipU32(rec, 16); cdDisk = ZipU32(rec, 20);
			count = (long)ZipU64(rec, 32); cdSize = (long)ZipU64(rec, 40); cdOffset = (long)ZipU64(rec, 48);
			end = at;
			}
		if (disk != 0 || cdDisk != 0)
			throw new UnsupportedOptionException("unzip", "multi-part", "a multi-part archive is not read in-process");
		long start = end - cdSize, shift = start - cdOffset;
		if (start < 0 || shift < 0 || cdSize > int.MaxValue) return null;
		var cd = ZipReadAt(fs, start, (int)cdSize);
		var list = new List<ZipItem>((int)Math.Min(count, 65536));
		int p = 0;
		for (long n = 0; n < count; n++)
			{
			if (p + 46 > cd.Length || ZipU32(cd, p) != 0x02014B50) return null;
			int host = cd[p + 5];   // the system that made it: 3 is Unix, whose mode is in the external attributes
			int flags = ZipU16(cd, p + 8), method = ZipU16(cd, p + 10), time = ZipU16(cd, p + 12), date = ZipU16(cd, p + 14);
			uint crc = ZipU32(cd, p + 16);
			long packed = ZipU32(cd, p + 20), size = ZipU32(cd, p + 24);
			int nl = ZipU16(cd, p + 28), xl = ZipU16(cd, p + 30), cl = ZipU16(cd, p + 32);
			uint ext = ZipU32(cd, p + 38);
			long data = ZipU32(cd, p + 42);
			if (p + 46 + nl + xl + cl > cd.Length) return null;
			string name = ShellEncoding.Utf8.GetString(cd, p + 46, nl);
			long? ut = null;
			for (int x = p + 46 + nl, xe = x + xl; x + 4 <= xe; )
				{
				int id = ZipU16(cd, x), sz = ZipU16(cd, x + 2), d = x + 4;
				if (d + sz > xe) break;
				if (id == 0x0001)   // Zip64: the 64-bit values, in this order, of those that read all ones
					{
					int q = d;
					if (size == 0xFFFFFFFF && q + 8 <= d + sz) { size = (long)ZipU64(cd, q); q += 8; }
					if (packed == 0xFFFFFFFF && q + 8 <= d + sz) { packed = (long)ZipU64(cd, q); q += 8; }
					if (data == 0xFFFFFFFF && q + 8 <= d + sz) data = (long)ZipU64(cd, q);
					}
				else if (id == 0x5455 && sz >= 5 && (cd[d] & 1) != 0) ut = (int)ZipU32(cd, d + 1);   // extended timestamp: mtime, UTC
				x = d + sz;
				}
			string stamp;
			DateTime? when = null;
			if (ut is { } secs)
				{
				// UnZip lists and sets the extended timestamp when there is one, in local time
				var local = DateTimeOffset.FromUnixTimeSeconds(secs).ToLocalTime().DateTime;
				stamp = local.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);
				when = DateTime.SpecifyKind(local, DateTimeKind.Local);
				}
			else
				{
				int y = (date >> 9) + 1980, mo = (date >> 5) & 15, dd = date & 31, h = time >> 11, mi = (time >> 5) & 63, s = (time & 31) * 2;
				stamp = string.Create(CultureInfo.InvariantCulture, $"{y:D4}-{mo:D2}-{dd:D2} {h:D2}:{mi:D2}");
				try { when = new DateTime(y, mo, dd, h, mi, s, DateTimeKind.Local); } catch (ArgumentOutOfRangeException) { }
				}
			bool link = host == 3 && ((ext >> 16) & 0xF000) == 0xA000;
			list.Add(new ZipItem(name, method, flags, crc, packed, size, data + shift, stamp, when, link));
			p += 46 + nl + xl + cl;
			}
		return list;
		}

	/// <summary>A window on the archive: <paramref name="length"/> bytes from <paramref name="start"/>.</summary>
	private sealed class ZipSlice(FileStream fs, long start, long length) : Stream
		{
		private long _at;
		public override bool CanRead => true;
		public override bool CanSeek => false;
		public override bool CanWrite => false;
		public override long Length => length;
		public override long Position { get => _at; set => throw new NotSupportedException(); }
		public override int Read(byte[] buffer, int offset, int count)
			{
			if (_at >= length) return 0;
			fs.Position = start + _at;
			int n = fs.Read(buffer, offset, (int)Math.Min(count, length - _at));
			_at += n;
			return n;
			}
		public override void Flush() { }
		public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
		public override void SetLength(long value) => throw new NotSupportedException();
		public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
		}

	/// <summary>An entry's data, inflated, to <paramref name="to"/> (or nowhere: a test); its CRC-32, or null when the
	/// deflated data is not valid.</summary>
	private static uint? ZipCopy(FileStream fs, ZipItem e, Action<byte[], int, int>? to)
		{
		var h = ZipReadAt(fs, e.Data, 30);
		if (ZipU32(h, 0) != 0x04034B50) return null;
		Stream src = new ZipSlice(fs, e.Data + 30 + ZipU16(h, 26) + ZipU16(h, 28), e.Packed);
		if (e.Method == 8) src = new DeflateStream(src, CompressionMode.Decompress);
		var t = ZipCrcTable();
		uint crc = 0xFFFFFFFF;
		using var block = Block.Rent();
		try
			{
			for (int n; (n = src.Read(block.Bytes, 0, IoBlock)) > 0; )
				{
				foreach (var b in block.Bytes.AsSpan(0, n)) crc = t[(crc ^ b) & 0xFF] ^ (crc >> 8);
				to?.Invoke(block.Bytes, 0, n);
				}
			}
		catch (InvalidDataException) { return null; }
		finally { src.Dispose(); }
		return ~crc;
		}

	/// <summary>A name part Windows can hold: a control character or one of `:*?"&lt;&gt;|` becomes `_`. A `:` would
	/// otherwise write an alternate data stream (`a:b` is stream b of file a); UnZip on Linux keeps them all.</summary>
	private static string ZipWindowsName(string s)
		{
		if (s.AsSpan().IndexOfAny(":*?\"<>|") < 0 && !s.Any(c => c < ' ')) return s;
		var chars = s.ToCharArray();
		for (int i = 0; i < chars.Length; i++) if (chars[i] < ' ' || ":*?\"<>|".Contains(chars[i])) chars[i] = '_';
		return new string(chars);
		}

	/// <summary>UnZip's name column: padded to 22 BYTES (C's %-22s), never cut.</summary>
	private static string ZipPad(string s)
		{
		int n = ShellEncoding.Utf8.GetByteCount(s);
		return n >= 22 ? s : s + new string(' ', 22 - n);
		}

	/// <summary>`unzip [-lptonqj] [-d exdir] file[.zip] [member ...] [-x member ...] [-d exdir]`, as UnZip 6.00.</summary>
	private int Unzip(List<string> args)
		{
		bool list = false, test = false, pipe = false, over = false, never = false, junk = false, excluding = false;
		int quiet = 0;
		string? exdir = null, archive = null;
		List<string> members = [], excludes = [];
		for (int i = 0; i < args.Count; i++)
			{
			var a = args[i];
			bool option = a.Length > 1 && a[0] == '-';
			if (archive is null && !option) { archive = a; continue; }
			if (archive is not null && a == "-x") { excluding = true; continue; }
			if (archive is not null && !a.StartsWith("-d", StringComparison.Ordinal)) { (excluding ? excludes : members).Add(a); continue; }
			for (int k = 1; k < a.Length; k++)
				{
				switch (a[k])
					{
					case 'l': list = true; continue;
					case 't': test = true; continue;
					case 'p': pipe = true; continue;
					case 'o': over = true; continue;
					case 'n': never = true; continue;
					case 'q': quiet++; continue;
					case 'j': junk = true; continue;
					case 'd':
						exdir = k + 1 < a.Length ? a[(k + 1)..] : i + 1 < args.Count ? args[++i] : null;
						if (exdir is null) { Console.Error.Write("error:  must specify directory to which to extract with -d option\n"); return 10; }
						// UnZip reads members after -x and then -d differently (an operand there is dropped): not guessed
						if (excluding && i + 1 < args.Count) throw new UnsupportedOptionException("unzip", "-d", "an operand after -x ... -d is not read in-process");
						k = a.Length;
						continue;
					default:
						throw new UnsupportedOptionException("unzip", "-" + a[k]);
					}
				}
			}
		if (archive is null) throw new UnsupportedOptionException("unzip", "usage", "no archive: the usage text is not printed in-process");
		if ((list ? 1 : 0) + (test ? 1 : 0) + (pipe ? 1 : 0) > 1) throw new UnsupportedOptionException("unzip", list ? "-l" : "-t", "only one of -l, -t and -p is read in-process");
		if (over && never) throw new UnsupportedOptionException("unzip", "-n", "-o with -n is not read in-process");

		string? found = null;
		foreach (var name in (string[])[archive, archive + ".zip", archive + ".ZIP"])
			if (File.Exists(ShellEnvironment.TranslatePath(name))) { found = name; break; }
		if (found is null) { Console.Error.Write($"unzip:  cannot find or open {archive}, {archive}.zip or {archive}.ZIP.\n"); return 9; }

		using var fs = ShellFile.OpenRead(ShellEnvironment.TranslatePath(found));
		var entries = ZipDirectory(fs);
		if (entries is null)
			{
			if (!pipe && quiet == 0) Console.Out.Write($"Archive:  {found}\n");
			Console.Out.Flush();
			Console.Error.Write("  End-of-central-directory signature not found.  Either this file is not\n" +
				"  a zipfile, or it constitutes one disk of a multi-part archive.  In the\n" +
				"  latter case the central directory and zipfile comment will be found on\n" +
				"  the last disk(s) of this archive.\n" +
				// UnZip's note is for a file with the execute bit: here, what `test -x` calls executable
				(FileTests.IsExecutable(ShellEnvironment.TranslatePath(found)) ? $"note:  {found} may be a plain executable, not an archive\n" : "") +
				$"unzip:  cannot find zipfile directory in one of {archive} or\n" +
				$"        {archive}.zip, and cannot find {archive}.ZIP, period.\n");
			return 9;
			}

		// the entries asked for: a member pattern (UnZip's wildcards match across /), less the -x ones
		var matched = new bool[members.Count];
		var xmatched = new bool[excludes.Count];
		var chosen = new List<ZipItem>();
		foreach (var e in entries)
			{
			bool take = members.Count == 0;
			for (int m = 0; m < members.Count; m++) if (Glob.Match(members[m], e.Name)) { matched[m] = take = true; }
			if (!take) continue;
			bool drop = false;
			for (int m = 0; m < excludes.Count; m++) if (Glob.Match(excludes[m], e.Name)) { xmatched[m] = drop = true; }
			if (!drop) chosen.Add(e);
			}
		// 11: a member matched nothing (a list: only when nothing at all was chosen)
		int rc = chosen.Count == 0 || (!list && matched.Contains(false)) ? 11 : 0;

		// what is not read in-process goes to the PATH unzip before anything is written
		if (!list)
			foreach (var e in chosen)
				{
				if (e.IsDir) continue;
				if ((e.Flags & 1) != 0) throw new UnsupportedOptionException("unzip", "encrypted", "an encrypted entry is not read in-process");
				if (e.Method is not (0 or 8)) throw new UnsupportedOptionException("unzip", $"method {e.Method}", "this compression method is not read in-process");
				if (e.Link && !test && !pipe) throw new UnsupportedOptionException("unzip", "symlink", "a symbolic-link entry is not made in-process");
				}

		var o = Console.Out;
		if (list)
			{
			if (quiet == 0) o.Write($"Archive:  {found}\n");
			if (quiet < 2) o.Write("  Length      Date    Time    Name\n---------  ---------- -----   ----\n");
			long total = 0;
			foreach (var e in chosen) { o.Write(string.Create(CultureInfo.InvariantCulture, $"{e.Size,9}  {e.Stamp}   {e.Name}\n")); total += e.Size; }
			if (quiet < 2)
				o.Write(string.Create(CultureInfo.InvariantCulture, $"---------                     -------\n{total,9}                     {chosen.Count} file{(chosen.Count == 1 ? "" : "s")}\n"));
			return rc;   // a list never cautions
			}

		void Cautions(TextWriter to)
			{
			Console.Out.Flush();   // what came before, first
			for (int m = 0; m < members.Count; m++) if (!matched[m]) to.Write($"caution: filename not matched:  {members[m]}\n");
			for (int m = 0; m < excludes.Count; m++) if (!xmatched[m]) to.Write($"caution: excluded filename not matched:  {excludes[m]}\n");
			}
		// UnZip writes "verb: name" first and the outcome after it; a damaged entry, quiet or piped, is named at the end
		const string Inflate = "  error:  invalid compressed data to inflate";
		static string Crc(uint got, uint want) => $"bad CRC {got:x8}  (should be {want:x8})";
		static string Quiet(string name, uint? got, uint want) =>
			got is { } g ? $"{ZipPad(name)}  {Crc(g, want)}\n" : $"{Inflate} {name}\n";

		if (test)
			{
			if (quiet == 0) o.Write($"Archive:  {found}\n");
			bool bad = false;
			foreach (var e in chosen)
				{
				if (quiet == 0) o.Write($"    testing: {ZipPad(e.Name)}  ");
				uint? crc = e.IsDir ? e.Crc : ZipCopy(fs, e, null);
				if (crc == e.Crc) { if (quiet == 0) o.Write(" OK\n"); continue; }
				bad = true;
				if (quiet > 0) o.Write(Quiet(e.Name, crc, e.Crc));
				else o.Write(crc is { } c ? $" {Crc(c, e.Crc)}\n" : $"\n{Inflate}\n");
				}
			Cautions(o);   // a test cautions on stdout
			if (bad) rc = Math.Max(rc, 2);
			if (quiet < 2)
				o.Write(rc != 0 ? $"At least one error was detected in {found}.\n"
					: members.Count > 0 ? $"No errors detected in {found} for the {chosen.Count} file{(chosen.Count == 1 ? "" : "s")} tested.\n"
					: $"No errors detected in compressed data of {found}.\n");
			return rc;
			}

		if (pipe)
			{
			var sink = new ByteSink();
			foreach (var e in chosen)
				{
				if (e.IsDir) continue;
				var crc = ZipCopy(fs, e, sink.Write);
				if (crc != e.Crc) { sink.Flush(); Console.Error.Write(Quiet(e.Name, crc, e.Crc)); rc = Math.Max(rc, 2); }
				}
			sink.Flush();
			Cautions(Console.Error);
			return rc;
			}

		// extract
		if (quiet == 0) o.Write($"Archive:  {found}\n");
		string prefix = string.IsNullOrEmpty(exdir) ? "" : (exdir.EndsWith('/') ? exdir[..^1] : exdir) + "/";
		string root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(ShellEnvironment.TranslatePath(string.IsNullOrEmpty(exdir) ? "." : exdir)));
		if (!string.IsNullOrEmpty(exdir)) Directory.CreateDirectory(root);
		string inside = root.EndsWith('\\') ? root : root + "\\";   // a drive's root keeps its separator
		var folderTimes = new List<(string Path, DateTime Time)>();
		bool all = over, none = never;
		foreach (var e in chosen)
			{
			// a drive or a leading / is stripped (UnZip's warning on stderr), a ".." component skipped (on stdout); `\` is a
			// separator here, as on Windows, so `..\` cannot climb out either
			string name = e.Name.Replace('\\', '/');
			bool absolute = false;
			if (name.Length >= 2 && name[1] == ':' && char.IsAsciiLetter(name[0])) { name = name[2..]; absolute = true; }
			if (name.StartsWith('/')) { name = name.TrimStart('/'); absolute = true; }
			if (absolute) { o.Flush(); Console.Error.Write($"warning:  stripped absolute path spec from {e.Name}\n"); rc = Math.Max(rc, 1); }
			bool folder = name.EndsWith('/');
			var parts = new List<string>();
			bool climbed = false;
			foreach (var s in name.Split('/'))
				if (s == "..") climbed = true;
				else if (s.Length > 0 && s != ".") parts.Add(ZipWindowsName(s));
			if (climbed) { if (quiet == 0) o.Write($"warning:  skipped \"../\" path component(s) in {e.Name}\n"); rc = Math.Max(rc, 1); }
			if (parts.Count == 0) continue;
			if (junk) { if (folder) continue; parts = [parts[^1]]; }
			string rel = string.Join('/', parts) + (folder ? "/" : "");
			string shown = prefix + rel;
			string target = Path.GetFullPath(Path.Combine(root, string.Join('\\', parts)));
			if (!target.StartsWith(inside, StringComparison.OrdinalIgnoreCase)) continue;   // never outside the folder
			if (folder)
				{
				if (!Directory.Exists(target)) { Directory.CreateDirectory(target); if (quiet == 0) o.Write($"   creating: {shown}\n"); }
				if (e.Time is { } ft) folderTimes.Add((target, ft));
				continue;
				}
			if (File.Exists(target) || Directory.Exists(target))
				{
				if (none) continue;
				if (!all)
					{
					bool skip = false;
					while (true)
						{
						o.Flush();
						Console.Error.Write($"replace {shown}? [y]es, [n]o, [A]ll, [N]one, [r]ename: ");
						var answer = ShellEncoding.ReadLine(Console.In);
						if (answer is null)
							{
							Console.Error.Write(" NULL\n(EOF or read error, treating as \"[N]one\" ...)\n");
							none = skip = true;
							rc = Math.Max(rc, 1);
							break;
							}
						char c = answer.Length > 0 ? answer[0] : '\0';
						if (c == 'y') break;
						if (c == 'n') { skip = true; break; }
						if (c == 'A') { all = true; break; }
						if (c == 'N') { none = skip = true; break; }
						if (c == 'r')
							{
							Console.Error.Write("new name: ");
							var renamed = ShellEncoding.ReadLine(Console.In);
							if (string.IsNullOrEmpty(renamed)) { skip = true; break; }
							shown = renamed;
							target = Path.GetFullPath(ShellEnvironment.TranslatePath(renamed));
							break;
							}
						Console.Error.Write($"error:  invalid response [{answer}]\n");
						}
					if (skip) continue;
					}
				}
			Directory.CreateDirectory(Path.GetDirectoryName(target)!);
			if (quiet == 0) o.Write($"{(e.Method == 0 ? " extracting" : "  inflating")}: {ZipPad(shown)}  ");
			uint? got;
			using (var w = ShellFile.Create(target)) got = ZipCopy(fs, e, w.Write);
			if (e.Time is { } mt) try { File.SetLastWriteTime(target, mt); } catch (Exception) { }
			if (got == e.Crc) { if (quiet == 0) o.Write("\n"); continue; }
			// the damage on stderr; stdout's line is left open, as UnZip leaves it
			rc = Math.Max(rc, 2);
			o.Flush();
			Console.Error.Write(quiet > 0 ? Quiet(shown, got, e.Crc) : got is { } g ? $" {Crc(g, e.Crc)}\n" : $"\n{Inflate}\n");
			}
		// a folder's time last, after its files were written into it (UnZip does the same)
		foreach (var (path, time) in folderTimes) try { Directory.SetLastWriteTime(path, time); } catch (Exception) { }
		Cautions(Console.Error);
		return rc;
		}
	}
