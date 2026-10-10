using System.Runtime.InteropServices;
using System.Text;

namespace Bash.Evaluator;

/// <summary>
/// System-ish coreutils: date (full strftime, -d/-r/-u/-I/-R), uname, timeout, id, whoami,
/// nproc, printenv, tty, arch, df. Strict option parsing throughout.
/// </summary>
public sealed partial class Builtins
	{
	// ── date ────────────────────────────────────────────────────────────────────

	private static int Date(List<string> args)
		{
		var o = Opts.Parse("date", args, "d:r:uRI::s:f:", ["date=:d", "reference=:r", "utc:u", "universal:u", "rfc-email:R", "rfc-2822:R", "iso-8601=?:I", "rfc-3339=", "set=:s", "file=:f", "debug"]);
		if (o.Get('s') is not null) throw new UnsupportedOptionException("date", "-s", "setting the system clock is not supported");
		bool utc = o.Has('u');
		DateTime when = utc ? DateTime.UtcNow : DateTime.Now;
		if (o.Get('d') is string ds)
			{
			if (!GnuDate.TryParse(ds, utc, out when)) { Console.Error.WriteLine($"date: invalid date '{ds}'"); return 1; }
			}
		if (o.Get('r') is string rf)
			{
			try { var p = ShellEnvironment.TranslatePath(rf); when = utc ? File.GetLastWriteTimeUtc(p) : File.GetLastWriteTime(p); }
			catch (Exception ex) { Console.Error.WriteLine($"date: {rf}: {IoError(ex)}"); return 1; }
			}
		var fmtArg = o.Operands.FirstOrDefault(a => a.StartsWith('+'));
		if (o.Operands.Any(a => !a.StartsWith('+'))) throw new UnsupportedOptionException("date", o.Operands.First(a => !a.StartsWith('+')), "setting the date is not supported");
		string fmt;
		if (fmtArg is not null) fmt = fmtArg[1..];
		else if (o.Has('I'))
			{
			fmt = (o.Get('I') ?? o.GetLong("iso-8601") ?? "date") switch
				{
				"hours" => "%Y-%m-%dT%H%:z", "minutes" => "%Y-%m-%dT%H:%M%:z", "seconds" => "%Y-%m-%dT%H:%M:%S%:z",
				"ns" => "%Y-%m-%dT%H:%M:%S,%N%:z", _ => "%Y-%m-%d",
				};
			}
		else if (o.HasLong("rfc-3339"))
			{
			fmt = o.GetLong("rfc-3339") switch { "seconds" => "%Y-%m-%d %H:%M:%S%:z", "ns" => "%Y-%m-%d %H:%M:%S.%N%:z", _ => "%Y-%m-%d" };
			}
		else if (o.Has('R')) fmt = "%a, %d %b %Y %H:%M:%S %z";
		else fmt = "%a %b %e %H:%M:%S %Z %Y";
		if (o.Get('f') is string ff)
			{
			try
				{
				foreach (var line in ShellEncoding.ReadAllLines(ShellEnvironment.TranslatePath(ff)))
					{
					if (!GnuDate.TryParse(line, utc, out var d)) { Console.Error.WriteLine($"date: invalid date '{line}'"); return 1; }
					Console.WriteLine(Strftime(fmt, d, utc));
					}
				return 0;
				}
			catch (Exception ex) { Console.Error.WriteLine($"date: {ff}: {IoError(ex)}"); return 1; }
			}
		Console.WriteLine(Strftime(fmt, when, utc));
		return 0;
		}

	/// <summary>strftime with GNU date's flags (`-` no pad, `_` space pad, `0` zero pad, `^` upper,
	/// `#` swap case) and the common conversions.</summary>
	private static string Strftime(string f, DateTime d, bool utc)
		{
		var inv = System.Globalization.CultureInfo.InvariantCulture;
		var sb = new StringBuilder();
		var dto = utc ? new DateTimeOffset(d, TimeSpan.Zero) : new DateTimeOffset(d);
		for (int i = 0; i < f.Length; i++)
			{
			if (f[i] != '%' || i + 1 >= f.Length) { sb.Append(f[i]); continue; }
			i++;
			char pad = '\0'; bool upper = false, swap = false;
			while (i < f.Length && "-_0^#".IndexOf(f[i]) >= 0) { if (f[i] == '^') upper = true; else if (f[i] == '#') swap = true; else pad = f[i]; i++; }
			int width = 0;
			while (i < f.Length && char.IsDigit(f[i])) width = width * 10 + (f[i++] - '0');
			bool colon = false;
			if (i < f.Length && f[i] == ':') { colon = true; i++; }
			if (i >= f.Length) { sb.Append('%'); break; }
			char c = f[i];
			string Num(int v, int w) => pad == '-' ? v.ToString() : pad == '_' ? v.ToString().PadLeft(w) : v.ToString().PadLeft(w, '0');
			string val = c switch
				{
				'Y' => d.Year.ToString(), 'C' => (d.Year / 100).ToString("D2"), 'y' => Num(d.Year % 100, 2),
				'm' => Num(d.Month, 2), 'd' => Num(d.Day, 2), 'e' => pad == '\0' ? d.Day.ToString().PadLeft(2) : Num(d.Day, 2),
				'H' => Num(d.Hour, 2), 'I' => Num(d.Hour % 12 == 0 ? 12 : d.Hour % 12, 2), 'k' => d.Hour.ToString().PadLeft(2), 'l' => (d.Hour % 12 == 0 ? 12 : d.Hour % 12).ToString().PadLeft(2),
				'M' => Num(d.Minute, 2), 'S' => Num(d.Second, 2), 'N' => (d.Ticks % TimeSpan.TicksPerSecond * 100).ToString("D9"),
				'p' => d.Hour < 12 ? "AM" : "PM", 'P' => d.Hour < 12 ? "am" : "pm",
				'A' => d.ToString("dddd", inv), 'a' => d.ToString("ddd", inv), 'B' => d.ToString("MMMM", inv), 'b' or 'h' => d.ToString("MMM", inv),
				'j' => Num(d.DayOfYear, 3), 'u' => (((int)d.DayOfWeek + 6) % 7 + 1).ToString(), 'w' => ((int)d.DayOfWeek).ToString(),
				'U' => Num((d.DayOfYear + 6 - (int)d.DayOfWeek) / 7, 2),
				'W' => Num((d.DayOfYear + 6 - ((int)d.DayOfWeek + 6) % 7) / 7, 2),
				'V' => Num(System.Globalization.ISOWeek.GetWeekOfYear(d), 2), 'G' => System.Globalization.ISOWeek.GetYear(d).ToString(), 'g' => Num(System.Globalization.ISOWeek.GetYear(d) % 100, 2),
				'T' => d.ToString("HH:mm:ss"), 'R' => d.ToString("HH:mm"), 'r' => d.ToString("hh:mm:ss tt", inv), 'D' => d.ToString("MM/dd/yy"), 'F' => d.ToString("yyyy-MM-dd"),
				'c' => d.ToString("ddd MMM d HH:mm:ss yyyy", inv), 'x' => d.ToString("MM/dd/yyyy"), 'X' => d.ToString("HH:mm:ss"),
				's' => dto.ToUnixTimeSeconds().ToString(),
				'z' => Zone(dto.Offset, colon ? 1 : 0), 'Z' => utc ? "UTC" : TimeZoneAbbrev(d),
				'n' => "\n", 't' => "\t", '%' => "%",
				_ => "%" + c,
				};
			if (upper) val = val.ToUpperInvariant();
			if (swap) val = val == val.ToUpperInvariant() ? val.ToLowerInvariant() : val.ToUpperInvariant();
			// %N's width is a precision (GNU): the first W digits (default 9; zeros on the right past 9),
			// and its trailing zeros are the padding: `-` drops them, `_` makes them spaces, at least one
			// digit stays. It was padded on the left, so %3N printed all 9 digits (until 2026-10-08).
			if (c == 'N')
				{
				int w = width > 0 ? width : val.Length;
				val = w <= val.Length ? val[..w] : val.PadRight(w, '0');
				if (pad is '-' or '_')
					{
					int keep = Math.Max(1, val.TrimEnd('0').Length);
					val = pad == '-' ? val[..keep] : val[..keep].PadRight(val.Length, ' ');
					}
				width = 0;
				}
			if (width > 0) val = val.PadLeft(width, pad == '_' ? ' ' : '0');
			sb.Append(val);
			}
		return sb.ToString();
		}

	private static string Zone(TimeSpan off, int colons)
		{
		string sign = off < TimeSpan.Zero ? "-" : "+";
		off = off.Duration();
		return colons == 0 ? $"{sign}{off.Hours:D2}{off.Minutes:D2}" : $"{sign}{off.Hours:D2}:{off.Minutes:D2}";
		}

	private static string TimeZoneAbbrev(DateTime d)
		{
		var tz = TimeZoneInfo.Local;
		var name = tz.IsDaylightSavingTime(d) ? tz.DaylightName : tz.StandardName;
		// Windows names are long ("W. Europe Standard Time"); abbreviate to initials like GNU prints "CET"
		var initials = new string(name.Split(' ', StringSplitOptions.RemoveEmptyEntries).Where(w => w.Length > 0 && char.IsLetter(w[0])).Select(w => w[0]).ToArray());
		return initials.Length >= 2 ? initials : name;
		}

	// ── uname / arch / hostname / nproc / tty / whoami / id / printenv ─────────

	private static int Uname(List<string> args)
		{
		var o = Opts.Parse("uname", args, "asnrvmpio", ["all:a", "kernel-name:s", "nodename:n", "kernel-release:r", "kernel-version:v", "machine:m", "processor:p", "hardware-platform:i", "operating-system:o"]);
		bool all = o.Has('a');
		var parts = new List<string>();
		string arch = System.Runtime.InteropServices.RuntimeInformation.OSArchitecture switch
			{
			System.Runtime.InteropServices.Architecture.X64 => "x86_64",
			System.Runtime.InteropServices.Architecture.Arm64 => "aarch64",
			var x => x.ToString().ToLowerInvariant(),
			};
		var ver = Environment.OSVersion.Version;
		string kernel = $"MINGW64_NT-{ver.Major}.{ver.Minor}-{ver.Build}";
		if (all || o.Has('s') || args.Count == 0 || (!o.Has('n') && !o.Has('r') && !o.Has('v') && !o.Has('m') && !o.Has('p') && !o.Has('i') && !o.Has('o'))) parts.Add(kernel);
		if (all || o.Has('n')) parts.Add(Environment.MachineName);
		if (all || o.Has('r')) parts.Add($"{ver.Major}.{ver.Minor}.{ver.Build}");
		if (all || o.Has('v')) parts.Add($"#{ver.Build} C#Bash");
		if (all || o.Has('m')) parts.Add(arch);
		if (o.Has('p')) parts.Add(arch);
		if (o.Has('i')) parts.Add(arch);
		if (all || o.Has('o')) parts.Add("Msys");
		Console.WriteLine(string.Join(" ", parts));
		return 0;
		}

	private static int Arch(List<string> args)
		{
		Opts.Parse("arch", args, "");
		Console.WriteLine(System.Runtime.InteropServices.RuntimeInformation.OSArchitecture == System.Runtime.InteropServices.Architecture.Arm64 ? "aarch64" : "x86_64");
		return 0;
		}

	private static int Hostname(List<string> args)
		{
		var o = Opts.Parse("hostname", args, "fsdiI", ["fqdn:f", "short:s", "domain:d", "ip-address:i", "all-ip-addresses:I"]);
		if (o.Has('i') || o.Has('I'))
			{
			try
				{
				var addrs = System.Net.Dns.GetHostAddresses(System.Net.Dns.GetHostName()).Where(a => a.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork).Select(a => a.ToString());
				Console.WriteLine(o.Has('I') ? string.Join(" ", addrs) : addrs.FirstOrDefault() ?? "");
				}
			catch { Console.WriteLine(""); }
			return 0;
			}
		var name = Environment.MachineName.ToLowerInvariant();
		if (o.Has('f')) { try { name = System.Net.Dns.GetHostEntry("").HostName.ToLowerInvariant(); } catch { } }
		if (o.Has('d')) { int dot = name.IndexOf('.'); name = dot < 0 ? "" : name[(dot + 1)..]; }
		if (o.Has('s')) { int dot = name.IndexOf('.'); if (dot > 0) name = name[..dot]; }
		Console.WriteLine(name);
		return 0;
		}

	private static int Nproc(List<string> args)
		{
		var o = Opts.Parse("nproc", args, "", ["all", "ignore="]);
		int n = Environment.ProcessorCount;
		if (o.GetLong("ignore") is string ig && int.TryParse(ig, out var k)) n = Math.Max(1, n - k);
		Console.WriteLine(n);
		return 0;
		}

	private static int Tty(List<string> args)
		{
		var o = Opts.Parse("tty", args, "s", ["silent:s", "quiet:s"]);
		bool isTty = !Console.IsInputRedirected;
		if (!o.Has('s')) Console.WriteLine(isTty ? "/dev/tty" : "not a tty");
		return isTty ? 0 : 1;
		}

	private int Whoami(List<string> args)
		{
		Opts.Parse("whoami", args, "");
		Console.WriteLine(Environment.UserName);
		return 0;
		}

	private int Id(List<string> args)
		{
		var o = Opts.Parse("id", args, "ugGnrz", ["user:u", "group:g", "groups:G", "name:n", "real:r", "zero:z"]);
		var uid = _env.Get("UID");
		var user = Environment.UserName;
		string gid = "197121", group = "None";
		if (o.Has('u')) { Console.WriteLine(o.Has('n') ? user : uid); return 0; }
		if (o.Has('g')) { Console.WriteLine(o.Has('n') ? group : gid); return 0; }
		if (o.Has('G')) { Console.WriteLine(o.Has('n') ? group : gid); return 0; }
		Console.WriteLine($"uid={uid}({user}) gid={gid}({group}) groups={gid}({group})");
		return 0;
		}

	private int Printenv(List<string> args)
		{
		var o = Opts.Parse("printenv", args, "0", ["null:0"]);
		string term = o.Has('0') ? "\0" : "\n";
		var env = _env.GetExportedVars();
		if (o.Operands.Count == 0)
			{
			foreach (var (k, v) in env.OrderBy(kv => kv.Key, StringComparer.Ordinal)) Console.Out.Write($"{k}={v}{term}");
			return 0;
			}
		int rc = 0;
		foreach (var name in o.Operands)
			{
			if (env.TryGetValue(name, out var v)) Console.Out.Write(v + term); else rc = 1;
			}
		return rc;
		}

	// ── df ──────────────────────────────────────────────────────────────────────
	// GNU df over Windows volumes (DECISIONS 2026-10-08). With no operand: every drive letter
	// whose volume is ready, A to Z. With operands: the volume holding each one
	// (GetVolumePathName, so a folder mount point or a UNC share works). Filesystem is `C:`, or
	// `//host/share` for a mapped network drive (as a Linux cifs mount shows its source);
	// Mounted on is the shell's path form `C:/`; Type is the volume's file system name in lower
	// case. Numbers, rounding, headers and column widths follow coreutils 8.32, checked against it
	// through WSL's /mnt/<d>. Inode counts do not exist on Windows: -i is loud (decision 2).

	[DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
	private static extern bool GetDiskFreeSpaceExW(string directory, out ulong freeToCaller, out ulong total, out ulong totalFree);
	[DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
	private static extern bool GetVolumePathNameW(string fileName, [Out] char[] volumePath, int length);
	[DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
	private static extern bool GetVolumeInformationW(string root, IntPtr volumeName, int volumeNameSize, IntPtr serial, IntPtr maxComponent, IntPtr flags, [Out] char[] fsName, int fsNameSize);
	[DllImport("kernel32.dll", CharSet = CharSet.Unicode)] private static extern uint GetDriveTypeW(string root);
	[DllImport("kernel32.dll")] private static extern uint GetLogicalDrives();
	[DllImport("kernel32.dll")] private static extern bool SetThreadErrorMode(uint mode, out uint oldMode);
	[DllImport("mpr.dll", CharSet = CharSet.Unicode)] private static extern int WNetGetConnectionW(string localName, [Out] char[] remoteName, ref int length);

	private const uint DriveRemote = 4, SemFailCriticalErrors = 0x0001, SemNoOpenFileErrorBox = 0x8000;

	private enum DfField { Source, FsType, Size, Used, Avail, Pcent, File, Target }

	private sealed record DfVolume(string Source, string FsType, string Target, ulong Total, ulong Free, ulong Avail, bool Remote);

	/// <summary>How df prints a byte count: gnulib's human options (autoscale, base 1024, SI
	/// suffix letter, B suffix) and the output block size.</summary>
	private readonly record struct DfUnits(bool Autoscale, bool Base1024, bool Si, bool SuffixB, ulong Block);

	private int Df(List<string> args)
		{
		var o = Opts.Parse("df", args, "B:hHklPt:Tx:v", ["block-size=:B", "human-readable:h", "si:H", "local:l", "no-sync", "sync", "output=?:O", "portability:P", "type=:t", "print-type:T", "exclude-type=:x", "total"]);
		bool posix = o.Has('P');

		// --output fields (repeatable, in order); -T and -P conflict with it, as in GNU
		var outSpecs = o.Sequence.Where(s => s.opt == 'O').Select(s => s.val).ToList();
		bool outputMode = outSpecs.Count > 0;
		var cols = new List<DfField>();
		if (outputMode)
			{
			if (o.Has('T')) return DfUsage("options -T and --output are mutually exclusive");
			if (posix) return DfUsage("options -P and --output are mutually exclusive");
			foreach (var spec in outSpecs)
				{
				if (spec is null) throw new UnsupportedOptionException("df", "--output", "the full field list includes inode counts, which Windows does not have");
				foreach (var name in spec.Split(','))
					{
					DfField? f = name switch
						{
						"source" => DfField.Source, "fstype" => DfField.FsType, "size" => DfField.Size, "used" => DfField.Used,
						"avail" => DfField.Avail, "pcent" => DfField.Pcent, "file" => DfField.File, "target" => DfField.Target,
						"itotal" or "iused" or "iavail" or "ipcent" => throw new UnsupportedOptionException("df", "--output=" + name, "inode counts are not available on Windows"),
						_ => null,
						};
					if (f is null) return DfUsage($"option --output: field '{name}' unknown");
					if (cols.Contains(f.Value)) return DfUsage($"option --output: field '{name}' used more than once");
					cols.Add(f.Value);
					}
				}
			}

		// units: the last of -h / -H / -k / -B wins; else -P's 1024, else DF_BLOCK_SIZE and friends
		DfUnits? units = null;
		foreach (var (opt, val) in o.Sequence)
			{
			if (opt == 'h') units = new(true, true, true, false, 1);
			else if (opt == 'H') units = new(true, false, true, false, 1);
			else if (opt == 'k') units = new(false, false, false, false, 1024);
			else if (opt == 'B')
				{
				units = DfBlockSpec(val!, out var err);
				if (units is null)
					{
					string which = args.Any(a => a.StartsWith("--block-size")) ? "--block-size" : "-B";
					Console.Error.WriteLine(err switch
						{
						"suffix" => $"df: invalid suffix in {which} argument '{val}'",
						"overflow" => $"df: {which} argument '{val}' too large",
						_ => $"df: invalid {which} argument '{val}'",
						});
					return 1;
					}
				}
			}
		var env = _env.GetExportedVars();
		ulong defaultBlock = env.ContainsKey("POSIXLY_CORRECT") ? 512u : 1024u;
		if (units is null)
			{
			string? spec = posix ? null : env.GetValueOrDefault("DF_BLOCK_SIZE") ?? env.GetValueOrDefault("BLOCK_SIZE") ?? env.GetValueOrDefault("BLOCKSIZE");
			units = (spec is null ? null : DfBlockSpec(spec, out _)) ?? new DfUnits(false, false, false, false, defaultBlock);
			}
		var u = units.Value;
		string mode = outputMode ? "output" : u.Autoscale ? "human" : posix ? "posix" : "default";
		if (!outputMode)
			{
			cols.Add(DfField.Source);
			if (o.Has('T')) cols.Add(DfField.FsType);
			cols.AddRange([DfField.Size, DfField.Used, DfField.Avail, DfField.Pcent, DfField.Target]);
			}

		var types = o.All('t').ToList();
		var excluded = o.All('x').ToList();
		foreach (var t in types)
			if (excluded.Contains(t)) { Console.Error.WriteLine($"df: file system type '{t}' both selected and excluded"); return 1; }
		bool Wanted(DfVolume v) => !(o.Has('l') && v.Remote) && (types.Count == 0 || types.Contains(v.FsType)) && !excluded.Contains(v.FsType);

		string Num(ulong bytes) => GnuHuman(bytes, u.Block, u.Autoscale, u.Base1024, u.Si, u.SuffixB, suppressPointZero: false);
		var rows = new List<string[]>();
		ulong sumTotal = 0, sumUsed = 0, sumAvail = 0;
		void Add(DfVolume v, string? file)
			{
			ulong used = v.Total >= v.Free ? v.Total - v.Free : 0;
			sumTotal += v.Total; sumUsed += used; sumAvail += v.Avail;
			rows.Add([.. cols.Select(f => f switch
				{
				DfField.Source => v.Source, DfField.FsType => v.FsType, DfField.Size => Num(v.Total), DfField.Used => Num(used),
				DfField.Avail => Num(v.Avail), DfField.Pcent => DfPcent(used, v.Avail), DfField.File => file ?? "-", _ => v.Target,
				})]);
			}

		int rc = 0;
		if (o.Operands.Count == 0)
			{
			uint drives = GetLogicalDrives();
			for (int d = 0; d < 26; d++)
				{
				if ((drives & (1u << d)) == 0) continue;
				string root = $"{(char)('A' + d)}:\\";
				if (o.Has('l') && GetDriveTypeW(root) == DriveRemote) continue;   // before any network I/O, as GNU's -l
				var v = DfProbe(root, out int err);
				if (v is null)
					{
					// as GNU skips a mount it cannot access (ENOENT/EACCES) when listing: no media, access denied
					if (err is not (21 or 2 or 3 or 5)) { Console.Error.WriteLine($"df: '{ShellEnvironment.ToShellPath(root)}': {Win32Text(err)}"); rc = 1; }
					continue;
					}
				if (Wanted(v)) Add(v, null);
				}
			}
		else
			{
			foreach (var op in o.Operands)
				{
				string full = "";
				try { full = Path.GetFullPath(ShellEnvironment.TranslatePath(op)); } catch (Exception) { }
				if (full.Length == 0 || !(File.Exists(full) || Directory.Exists(full)))
					{ Console.Error.WriteLine($"df: {DfQuoteF(op)}: No such file or directory"); rc = 1; continue; }
				var buf = new char[1024];
				if (!GetVolumePathNameW(full, buf, buf.Length))
					{ Console.Error.WriteLine($"df: {DfQuoteF(op)}: {Win32Text(Marshal.GetLastWin32Error())}"); rc = 1; continue; }
				int nul = Array.IndexOf(buf, '\0');
				string root = new(buf, 0, nul < 0 ? buf.Length : nul);
				if (o.Has('l') && GetDriveTypeW(root) == DriveRemote) continue;
				var v = DfProbe(root, out int err);
				if (v is null) { Console.Error.WriteLine($"df: {DfQuoteF(op)}: {Win32Text(err)}"); rc = 1; continue; }
				if (Wanted(v)) Add(v, op);
				}
			}
		if (rows.Count == 0)
			{
			if (rc == 0) { Console.Error.WriteLine("df: no file systems processed"); rc = 1; }
			return rc;
			}
		if (o.HasLong("total"))
			{
			bool hasSource = cols.Contains(DfField.Source);
			rows.Add([.. cols.Select(f => f switch
				{
				DfField.Source => "total", DfField.FsType => "-", DfField.Size => Num(sumTotal), DfField.Used => Num(sumUsed),
				DfField.Avail => Num(sumAvail), DfField.Pcent => DfPcent(sumUsed, sumAvail), DfField.File => "-", _ => hasSource ? "-" : "total",
				})]);
			}
		rows.Insert(0, [.. cols.Select(f => f switch
			{
			DfField.Source => "Filesystem", DfField.FsType => "Type",
			DfField.Size => mode == "human" || (mode == "output" && u.Autoscale) ? "Size" : mode == "posix" ? $"{u.Block}-blocks" : DfBlockHeader(u) + "-blocks",
			DfField.Used => "Used", DfField.Avail => mode is "default" or "posix" ? "Available" : "Avail",
			DfField.Pcent => mode == "posix" ? "Capacity" : "Use%", DfField.File => "File", _ => "Mounted on",
			})]);

		// GNU's table: minimum widths per field, text left-aligned, numbers right-aligned,
		// one space between columns, no padding after a left-aligned last column
		int[] width = [.. cols.Select(f => f switch { DfField.Source => 14, DfField.FsType or DfField.Pcent or DfField.File => 4, DfField.Target => 0, _ => 5 })];
		foreach (var r in rows)
			for (int c = 0; c < cols.Count; c++) width[c] = Math.Max(width[c], r[c].Length);
		var sb = new StringBuilder();
		foreach (var r in rows)
			{
			sb.Clear();
			for (int c = 0; c < cols.Count; c++)
				{
				if (c > 0) sb.Append(' ');
				bool left = cols[c] is DfField.Source or DfField.FsType or DfField.File or DfField.Target;
				sb.Append(!left ? r[c].PadLeft(width[c]) : c == cols.Count - 1 ? r[c] : r[c].PadRight(width[c]));
				}
			Console.WriteLine(sb.ToString());
			}
		return rc;
		}

	private static int DfUsage(string message)
		{
		Console.Error.WriteLine($"df: {message}");
		Console.Error.WriteLine("Try 'df --help' for more information.");
		return 1;
		}

	/// <summary>A volume's sizes and names; null (with the Win32 error) when it cannot be read.
	/// <paramref name="root"/> ends in a backslash: `C:\`, `\\host\share\`, `C:\mnt\x\`.</summary>
	private static DfVolume? DfProbe(string root, out int error)
		{
		error = 0;
		// no "insert a disk" dialog for an empty drive: a console-less shell would hang on it
		SetThreadErrorMode(SemFailCriticalErrors | SemNoOpenFileErrorBox, out uint old);
		try
			{
			var fs = new char[261];
			if (!GetVolumeInformationW(root, IntPtr.Zero, 0, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, fs, fs.Length)) { error = Marshal.GetLastWin32Error(); return null; }
			if (!GetDiskFreeSpaceExW(root, out ulong avail, out ulong total, out ulong free)) { error = Marshal.GetLastWin32Error(); return null; }
			int nul = Array.IndexOf(fs, '\0');
			string type = new string(fs, 0, nul < 0 ? fs.Length : nul).ToLowerInvariant();
			bool remote = GetDriveTypeW(root) == DriveRemote;
			bool letterRoot = root.Length == 3 && root[1] == ':';
			string bare = ShellEnvironment.ToShellPath(root.TrimEnd('\\'));
			string source = letterRoot && remote ? DfRemoteName(root[..2]) ?? root[..2] : letterRoot ? root[..2] : bare;
			string target = letterRoot ? ShellEnvironment.ToShellPath(root) : bare;
			return new DfVolume(source, type, target, total, free, avail, remote);
			}
		finally { SetThreadErrorMode(old, out _); }
		}

	/// <summary>The share a mapped drive letter (`X:`) points at, as `//host/share`.</summary>
	private static string? DfRemoteName(string drive)
		{
		var buf = new char[1024];
		int len = buf.Length;
		if (WNetGetConnectionW(drive, buf, ref len) != 0) return null;
		int nul = Array.IndexOf(buf, '\0');
		return ShellEnvironment.ToShellPath(new string(buf, 0, nul < 0 ? buf.Length : nul));
		}

	private static string Win32Text(int error) => new System.ComponentModel.Win32Exception(error).Message.TrimEnd('.');

	/// <summary>gnulib quotef: the operand as given, single-quoted only when the shell would need it.</summary>
	private static string DfQuoteF(string s) => s.Length > 0 && s.IndexOfAny(" \t\n\"$&'()*;<>?[\\]^`{|}~!#".ToCharArray()) < 0 ? s : "'" + s.Replace("'", "'\\''") + "'";

	/// <summary>Use%: used / (used + available), rounded up, as GNU computes it; `-` when both are 0.</summary>
	private static string DfPcent(ulong used, ulong avail)
		{
		UInt128 total = (UInt128)used + avail;
		if (total == 0) return "-";
		UInt128 u100 = (UInt128)used * 100;
		return $"{u100 / total + (u100 % total != 0 ? 1u : 0u)}%";
		}

	/// <summary>gnulib humblock: a --block-size / DF_BLOCK_SIZE value. Null when invalid, with
	/// <paramref name="error"/> "invalid", "suffix" or "overflow" (xstrtol's three failures).</summary>
	private static DfUnits? DfBlockSpec(string spec, out string error)
		{
		error = "invalid";
		if (spec == "human-readable") return new DfUnits(true, true, true, false, 1);
		if (spec == "si") return new DfUnits(true, false, true, false, 1);
		int i = 0;
		ulong n = 1;
		bool digits = spec.Length > 0 && char.IsAsciiDigit(spec[0]);
		if (digits)
			{
			n = 0;
			for (; i < spec.Length && char.IsAsciiDigit(spec[i]); i++)
				{
				if (n > (ulong.MaxValue - 9) / 10) { error = "overflow"; return null; }
				n = n * 10 + (ulong)(spec[i] - '0');
				}
			}
		else if (spec.Length == 0 || "eEgGkKmMpPtTyYzZ".IndexOf(spec[0]) < 0) return null;   // `'` (digit grouping) lands here too
		bool si = false, suffixB = false, base1024 = true;
		if (i < spec.Length)
			{
			int exp = "kmgtpezy".IndexOf(char.ToLowerInvariant(spec[i]));
			if (exp < 0 || "eEgGkKmMpPtTyYzZ".IndexOf(spec[i]) < 0) { error = "suffix"; return null; }
			i++;
			ulong b = 1024;
			if (i + 1 < spec.Length && spec[i] == 'i' && spec[i + 1] == 'B') i += 2;
			else if (i < spec.Length && (spec[i] == 'B' || spec[i] == 'D')) { b = 1000; i++; }
			if (i != spec.Length) { error = "suffix"; return null; }
			for (int k = 0; k <= exp; k++)
				{
				if (n > ulong.MaxValue / b) { error = "overflow"; return null; }
				n *= b;
				}
			if (!digits)
				{
				// letters only (-BM, -BKB, -BMiB): the unit letter is printed after every number
				si = true;
				suffixB = spec[^1] == 'B';
				base1024 = spec[^1] != 'B' || spec[^2] == 'i';
				}
			}
		if (n == 0) return null;
		return new DfUnits(false, digits ? false : base1024, si, suffixB, n);
		}

	/// <summary>The "1K" of "1K-blocks": GNU df's get_header, which picks the base that states the
	/// block size exactly.</summary>
	private static string DfBlockHeader(DfUnits u)
		{
		bool base1024 = u.Base1024, suffixB = u.SuffixB;
		ulong q1000 = u.Block, q1024 = u.Block;
		bool by1000, by1024;
		do
			{
			by1000 = q1000 % 1000 == 0; q1000 /= 1000;
			by1024 = q1024 % 1024 == 0; q1024 /= 1024;
			}
		while (by1000 && by1024);
		if (!by1000 && by1024) base1024 = true;
		if (!by1024 && by1000) base1024 = false;
		if (!base1024) suffixB = true;
		return GnuHuman(u.Block, 1, true, base1024, true, suffixB, suppressPointZero: true);
		}

	/// <summary>gnulib human_readable for an input in bytes, with df's rounding (up). Integer
	/// arithmetic throughout, as gnulib uses when the unit conversion is exact.</summary>
	private static string GnuHuman(ulong n, ulong toBlock, bool autoscale, bool base1024, bool si, bool suffixB, bool suppressPointZero)
		{
		const int exponentMax = 8;   // power letters K M G T P E Z Y (coreutils 8.32)
		uint b = base1024 ? 1024u : 1000u;
		ulong amt;
		int tenths = 0, rounding = 0, exponent = -1;
		// rounding: 0 exact, 1 below the half tenth, 2 exactly half, 3 above half
		if (toBlock <= 1) amt = n;
		else
			{
			UInt128 r10 = (UInt128)(n % toBlock) * 10;
			UInt128 r2 = r10 % toBlock * 2;
			amt = n / toBlock;
			tenths = (int)(r10 / toBlock);
			rounding = r2 < toBlock ? (r2 > 0 ? 1 : 0) : 2 + (toBlock < r2 ? 1 : 0);
			}
		string fraction = "";
		if (autoscale)
			{
			exponent = 0;
			if (b <= amt)
				{
				do
					{
					uint r10 = (uint)(amt % b) * 10 + (uint)tenths;
					uint r2 = r10 % b * 2 + (uint)(rounding >> 1);
					amt /= b;
					tenths = (int)(r10 / b);
					rounding = r2 < b ? (r2 + rounding != 0 ? 1 : 0) : 2 + (b < r2 + rounding ? 1 : 0);
					exponent++;
					}
				while (b <= amt && exponent < exponentMax);
				if (amt < 10)
					{
					if (rounding > 0)
						{
						tenths++; rounding = 0;
						if (tenths == 10) { amt++; tenths = 0; }
						}
					if (amt < 10 && (tenths != 0 || !suppressPointZero)) { fraction = "." + tenths; tenths = rounding = 0; }
					}
				}
			}
		if (tenths + rounding > 0)
			{
			amt++;
			if (autoscale && amt == b && exponent < exponentMax)
				{
				exponent++;
				if (!suppressPointZero) fraction = ".0";
				amt = 1;
				}
			}
		var sb = new StringBuilder().Append(amt).Append(fraction);
		if (si)
			{
			if (exponent < 0)
				{
				exponent = 0;
				for (UInt128 power = 1; power < toBlock; power *= b)
					if (++exponent == exponentMax) break;
				}
			if (exponent > 0) sb.Append(!base1024 && exponent == 1 ? 'k' : "KMGTPEZY"[exponent - 1]);
			if (suffixB)
				{
				if (base1024 && exponent > 0) sb.Append('i');
				sb.Append('B');
				}
			}
		return sb.ToString();
		}

	// ── timeout ─────────────────────────────────────────────────────────────────

	/// <summary>`timeout [-s SIG] [-k N] [--preserve-status] [--foreground] DURATION COMMAND…`:
	/// the command runs (builtin, function or external) on a worker; on expiry an external
	/// child is killed (tree) and the status is 124; an in-process command cannot be
	/// interrupted mid-flight, so it is abandoned and 124 is reported once it finishes.</summary>
	private int Timeout(List<string> args)
		{
		var o = Opts.Parse("timeout", args, "s:k:vp", ["signal=:s", "kill-after=:k", "preserve-status:p", "foreground", "verbose:v"], stopAtFirstOperand: true);
		if (o.Operands.Count < 2) { Console.Error.WriteLine("Usage: timeout [OPTION] DURATION COMMAND [ARG]..."); return 125; }
		var durSpec = o.Operands[0];
		double mult = 1;
		if (durSpec.Length > 0 && char.IsLetter(durSpec[^1]))
			{
			mult = durSpec[^1] switch { 's' => 1, 'm' => 60, 'h' => 3600, 'd' => 86400, _ => -1 };
			if (mult < 0) { Console.Error.WriteLine($"timeout: invalid time interval '{durSpec}'"); return 125; }
			durSpec = durSpec[..^1];
			}
		if (!double.TryParse(durSpec, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var secs))
			{ Console.Error.WriteLine($"timeout: invalid time interval '{o.Operands[0]}'"); return 125; }
		var cmd = o.Operands[1]; var rest = o.Operands.Skip(2).ToList();
		bool preserve = o.Has('p');
		int rc = 0; bool done = false;
		var stdio = ConsoleMux.Capture();
		var job = new BackgroundJob { Id = 0, Command = cmd };
		var active = ShellEnvironment.Active;
		var worker = new Thread(() =>
			{
			ConsoleMux.Apply(stdio);
			ShellEnvironment.Active = active;   // the command runs in this shell's cwd
			try { rc = _eval.RunAsJob(job, cmd, rest); }
			catch (ExitException ex) { rc = ex.Code; }
			catch (InterruptException) { rc = 143; }   // 128+TERM, what --preserve-status reports
			catch (Exception) { rc = 1; }
			finally { done = true; }
			}) { IsBackground = true };
		worker.Start();
		int waitMs = secs <= 0 ? int.MaxValue : (int)Math.Min(int.MaxValue, secs * 1000);
		if (worker.Join(waitMs)) return rc;
		// expired
		int child = job.ChildPid;
		if (child > 0) { try { System.Diagnostics.Process.GetProcessById(child).Kill(true); } catch { } }
		if (o.Has('v')) Console.Error.WriteLine($"timeout: sending signal {(o.Get('s') ?? "TERM")} to command '{cmd}'");
		if (child > 0) worker.Join(5000);
		if (!done && child == 0)
			{
			// in-process command: cannot be interrupted safely; wait for it but report expiry
			_eval.RequestInterrupt();
			worker.Join();
			}
		return preserve ? rc : 124;
		}
	}
