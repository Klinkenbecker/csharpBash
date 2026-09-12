using System.Text;

namespace Bash.Evaluator;

/// <summary>
/// System-ish coreutils: date (full strftime, -d/-r/-u/-I/-R), uname, timeout, id, whoami,
/// nproc, printenv, tty, arch. Strict option parsing throughout.
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
		var worker = new Thread(() =>
			{
			ConsoleMux.Apply(stdio);
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
