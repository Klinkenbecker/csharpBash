using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;

namespace Bash.Evaluator;

/// <summary>
/// Minimal procps: `pgrep`, `pkill`, `ps` (DECISIONS 2026-09-04, P5 follow-up). Windows has no
/// procps, so on a Git-less PATH these did not exist at all; Claude Code's own `pkill` shim ends
/// in `command pkill`. Process list via Toolhelp32 (pid, ppid, exe name); command lines via
/// NtQueryInformationProcess(ProcessCommandLineInformation), unreadable ones (other users,
/// elevated) are treated as empty. Name matching is case-insensitive (Windows file names are),
/// and the process running this shell is never a match, as procps excludes itself.
/// </summary>
public sealed partial class Builtins
	{
	private sealed record ProcInfo(int Pid, int Ppid, string Name, string CommandLine, DateTime Start);

	// ── Win32 ───────────────────────────────────────────────────────────────────

	[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
	private struct PROCESSENTRY32W
		{
		public uint dwSize; public uint cntUsage; public uint th32ProcessID; public IntPtr th32DefaultHeapID;
		public uint th32ModuleID; public uint cntThreads; public uint th32ParentProcessID; public int pcPriClassBase; public uint dwFlags;
		[MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)] public string szExeFile;
		}

	[DllImport("kernel32.dll", SetLastError = true)] private static extern IntPtr CreateToolhelp32Snapshot(uint flags, uint pid);
	[DllImport("kernel32.dll", CharSet = CharSet.Unicode)] private static extern bool Process32FirstW(IntPtr snap, ref PROCESSENTRY32W entry);
	[DllImport("kernel32.dll", CharSet = CharSet.Unicode)] private static extern bool Process32NextW(IntPtr snap, ref PROCESSENTRY32W entry);
	[DllImport("kernel32.dll")] private static extern bool CloseHandle(IntPtr h);
	[DllImport("kernel32.dll")] private static extern IntPtr OpenProcess(uint access, bool inherit, uint pid);
	[DllImport("ntdll.dll")] private static extern int NtQueryInformationProcess(IntPtr h, int cls, IntPtr info, int len, out int returned);

	private static readonly IntPtr InvalidHandle = new(-1);

	private static List<ProcInfo> ListProcesses(bool withCommandLines)
		{
		var list = new List<ProcInfo>();
		var snap = CreateToolhelp32Snapshot(2 /* TH32CS_SNAPPROCESS */, 0);
		if (snap == InvalidHandle || snap == IntPtr.Zero) return list;
		try
			{
			var e = new PROCESSENTRY32W { dwSize = (uint)Marshal.SizeOf<PROCESSENTRY32W>() };
			if (!Process32FirstW(snap, ref e)) return list;
			do
				{
				int pid = (int)e.th32ProcessID;
				if (pid == 0) continue;
				string cmd = withCommandLines ? ReadCommandLine(pid) : "";
				list.Add(new ProcInfo(pid, (int)e.th32ParentProcessID, e.szExeFile, cmd, DateTime.MinValue));
				}
			while (Process32NextW(snap, ref e));
			}
		finally { CloseHandle(snap); }
		list.Sort((a, b) => a.Pid.CompareTo(b.Pid));
		return list;
		}

	private static string ReadCommandLine(int pid)
		{
		var h = OpenProcess(0x1000 /* PROCESS_QUERY_LIMITED_INFORMATION */, false, (uint)pid);
		if (h == IntPtr.Zero) return "";
		try
			{
			const int ProcessCommandLineInformation = 60;
			int status = NtQueryInformationProcess(h, ProcessCommandLineInformation, IntPtr.Zero, 0, out int needed);
			if (needed <= 0 || needed > 1 << 20) return "";
			var buf = Marshal.AllocHGlobal(needed);
			try
				{
				status = NtQueryInformationProcess(h, ProcessCommandLineInformation, buf, needed, out _);
				if (status != 0) return "";
				// UNICODE_STRING { ushort Length; ushort MaximumLength; PWSTR Buffer }
				ushort len = (ushort)Marshal.ReadInt16(buf);
				var ptr = Marshal.ReadIntPtr(buf, IntPtr.Size == 8 ? 8 : 4);
				return ptr == IntPtr.Zero ? "" : Marshal.PtrToStringUni(ptr, len / 2) ?? "";
				}
			finally { Marshal.FreeHGlobal(buf); }
			}
		catch { return ""; }
		finally { CloseHandle(h); }
		}

	private static HashSet<int> AncestorsOf(int pid, List<ProcInfo> procs)
		{
		var byPid = new Dictionary<int, ProcInfo>();
		foreach (var p in procs) byPid[p.Pid] = p;
		var result = new HashSet<int>();
		int cur = pid;
		for (int depth = 0; depth < 64; depth++)
			{
			if (!byPid.TryGetValue(cur, out var p) || p.Ppid == 0 || !result.Add(p.Ppid)) break;
			cur = p.Ppid;
			}
		return result;
		}

	private static DateTime StartTimeOf(int pid)
		{
		try { return System.Diagnostics.Process.GetProcessById(pid).StartTime; } catch { return DateTime.MinValue; }
		}

	private static string BaseName(string exe)
		{
		var n = exe;
		int slash = n.LastIndexOfAny(['\\', '/']);
		if (slash >= 0) n = n[(slash + 1)..];
		return n.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) ? n[..^4] : n;
		}

	private static readonly HashSet<string> SignalWords =
		["HUP", "INT", "QUIT", "ILL", "TRAP", "ABRT", "BUS", "FPE", "KILL", "USR1", "SEGV", "USR2", "PIPE", "ALRM", "TERM", "CHLD", "CONT", "STOP", "TSTP", "TTIN", "TTOU", "URG", "XCPU", "XFSZ", "VTALRM", "PROF", "WINCH", "IO", "PWR", "SYS"];

	// ── pgrep / pkill ───────────────────────────────────────────────────────────

	private int Pgrep(List<string> args) => PgrepImpl("pgrep", args, kill: false);
	private int Pkill(List<string> args) => PgrepImpl("pkill", args, kill: true);

	private int PgrepImpl(string tool, List<string> args, bool kill)
		{
		// pkill's signal argument (-9, -KILL, -SIGTERM, --signal X): accepted; every signal is a
		// forceful termination on Windows, as with the `kill` builtin
		var norm = new List<string>();
		for (int i = 0; i < args.Count; i++)
			{
			var a = args[i];
			if (kill && a.Length > 1 && a[0] == '-' && a[1] != '-')
				{
				var body = a[1..];
				if (body.All(char.IsAsciiDigit)) continue;
				var up = body.ToUpperInvariant();
				if (up.StartsWith("SIG")) up = up[3..];
				if (SignalWords.Contains(up)) continue;
				}
			if (kill && a == "--signal" && i + 1 < args.Count) { i++; continue; }
			if (kill && a.StartsWith("--signal=")) continue;
			norm.Add(a);
			}
		var o = Opts.Parse(tool, norm, "fxlacnovP:d:ie",
			["full:f", "exact:x", "list-name:l", "list-full:a", "count:c", "newest:n", "oldest:o", "inverse:v", "parent=:P",
			 "delimiter=:d", "ignore-case:i", "echo:e", "uid=", "euid=", "group=", "session=", "terminal=", "pidfile=", "logpidfile=", "ns=", "nslist=", "runstates=", "cgroup="]);
		foreach (var unsupported in new[] { "uid", "euid", "group", "session", "terminal", "pidfile", "logpidfile", "ns", "nslist", "runstates", "cgroup" })
			if (o.HasLong(unsupported)) throw new UnsupportedOptionException(tool, "--" + unsupported, "no such notion on Windows");
		if (o.Operands.Count > 1) { Console.Error.WriteLine($"{tool}: only one pattern can be provided"); return 2; }
		string? pattern = o.Operands.Count == 1 ? o.Operands[0] : null;
		int? parent = null;
		if (o.Get('P') is string ps)
			{
			if (!int.TryParse(ps, out var pp)) { Console.Error.WriteLine($"{tool}: invalid parent pid '{ps}'"); return 2; }
			parent = pp;
			}
		if (pattern is null && parent is null) { Console.Error.WriteLine($"{tool}: no matching criteria specified"); return 2; }
		bool full = o.Has('f'), exact = o.Has('x'), inverse = o.Has('v');
		Regex? re = null;
		if (pattern is not null && !exact)
			{
			try { re = new Regex(EreToNet(pattern), RegexOptions.CultureInvariant | RegexOptions.IgnoreCase); }
			catch (ArgumentException ex) { Console.Error.WriteLine($"{tool}: regex error: {ex.Message}"); return 2; }
			}
		int self = Environment.ProcessId;
		var procs = ListProcesses(full || o.Has('a'));
		var matched = new List<ProcInfo>();
		foreach (var p in procs)
			{
			if (p.Pid == self) continue;
			bool hit = true;
			if (parent is int pp2 && p.Ppid != pp2) hit = false;
			if (hit && pattern is not null)
				{
				if (full)
					hit = exact ? string.Equals(p.CommandLine, pattern, StringComparison.OrdinalIgnoreCase) : re!.IsMatch(p.CommandLine);
				else
					{
					var bn = BaseName(p.Name);
					hit = exact
						? string.Equals(bn, pattern, StringComparison.OrdinalIgnoreCase) || string.Equals(p.Name, pattern, StringComparison.OrdinalIgnoreCase)
						: re!.IsMatch(bn) || re.IsMatch(p.Name);
					}
				}
			if (hit != inverse) matched.Add(p);
			}
		if (o.Has('n') || o.Has('o'))
			{
			if (matched.Count > 0)
				{
				var stamped = matched.Select(p => p with { Start = StartTimeOf(p.Pid) }).ToList();
				var pick = o.Has('n') ? stamped.MaxBy(p => p.Start)! : stamped.MinBy(p => p.Start)!;
				matched = [pick];
				}
			}
		if (kill)
			{
			// Never kill this shell's own ancestors (the harness, Claude Code, the terminal): with
			// `-f` their command lines routinely contain the pattern because they carry the very
			// command that names it. procps would kill them; here that is announced and skipped.
			var ancestors = AncestorsOf(self, procs);
			// Windows ppid chains break wherever an MSYS process exec'd (the old pid is gone), so
			// the chain alone is not enough — measured under Claude Code: Bash -> claude ->
			// timeout -> timeout -> <gone>. Second rule, independent of ppids: with -f, a process
			// whose command line carries this very pkill/pgrep invocation (the pattern text next
			// to the word pkill/pgrep) is a shell relaying the command, never a target.
			bool CarriesThisInvocation(ProcInfo p) =>
				full && pattern is not null && p.CommandLine.Length > 0
				&& p.CommandLine.Contains(pattern, StringComparison.OrdinalIgnoreCase)
				&& Regex.IsMatch(p.CommandLine, @"\bp(kill|grep)\b", RegexOptions.IgnoreCase);
			int killed = 0;
			foreach (var p in matched)
				{
				if (ancestors.Contains(p.Pid))
					{
					Console.Error.WriteLine($"pkill: skipping pid {p.Pid} ({BaseName(p.Name)}): it is an ancestor of this shell");
					continue;
					}
				if (CarriesThisInvocation(p))
					{
					Console.Error.WriteLine($"pkill: skipping pid {p.Pid} ({BaseName(p.Name)}): its command line carries this pkill invocation");
					continue;
					}
				try
					{
					System.Diagnostics.Process.GetProcessById(p.Pid).Kill();
					killed++;
					if (o.Has('e')) Console.WriteLine($"{BaseName(p.Name)} killed (pid {p.Pid})");
					}
				catch (Exception ex) { Console.Error.WriteLine($"pkill: killing pid {p.Pid} failed: {ex.Message}"); }
				}
			if (o.Has('c')) Console.WriteLine(killed);
			return killed > 0 ? 0 : 1;   // skipped-only is "nothing killed", not success
			}
		if (o.Has('c')) { Console.WriteLine(matched.Count); return matched.Count > 0 ? 0 : 1; }
		var delim = o.Get('d') is string d ? UnescapeDelims(d) : "\n";
		var sb = new StringBuilder();
		for (int i = 0; i < matched.Count; i++)
			{
			var p = matched[i];
			if (i > 0) sb.Append(delim);
			sb.Append(p.Pid);
			if (o.Has('a')) sb.Append(' ').Append(p.CommandLine.Length > 0 ? p.CommandLine : BaseName(p.Name));
			else if (o.Has('l')) sb.Append(' ').Append(BaseName(p.Name));
			}
		if (matched.Count > 0) Console.WriteLine(sb.ToString());
		return matched.Count > 0 ? 0 : 1;
		}

	// ── ps ──────────────────────────────────────────────────────────────────────

	/// <summary>`ps [-e|-A|-a|-ef|aux|ax] [-p PIDS] [-o COLS] [--no-headers]`. Columns: pid ppid comm
	/// args (aliases cmd, command); anything else (user, etime, tty, %cpu…) is loud.</summary>
	private int Ps(List<string> args)
		{
		// BSD-style first operand without a dash: `ps aux`, `ps ax`
		var norm = new List<string>(args);
		if (norm.Count > 0 && Regex.IsMatch(norm[0], "^[auxefrw]+$")) norm[0] = "-e";
		var o = Opts.Parse("ps", norm, "eAadfp:o:hrwxu:", ["no-headers:h", "pid=:p", "format=:o", "user=:u", "sort=", "forest", "width=", "cols=", "columns=", "lines=", "headers"]);
		if (o.Get('u') is not null) throw new UnsupportedOptionException("ps", "-u", "user selection is not available");
		foreach (var unsupported in new[] { "sort", "forest" })
			if (o.HasLong(unsupported)) throw new UnsupportedOptionException("ps", "--" + unsupported, "not available");
		var cols = new List<(string key, string header)>();
		bool anyHeader = false;
		if (o.Get('o') is string fmt)
			{
			foreach (var raw in fmt.Split([',', ' '], StringSplitOptions.RemoveEmptyEntries))
				{
				string key = raw, header;
				int eq = raw.IndexOf('=');
				if (eq >= 0) { key = raw[..eq]; header = raw[(eq + 1)..]; }
				else header = key.ToLowerInvariant() switch { "pid" => "PID", "ppid" => "PPID", "comm" or "args" or "cmd" or "command" => "COMMAND", _ => key.ToUpperInvariant() };
				key = key.ToLowerInvariant();
				if (key is not ("pid" or "ppid" or "comm" or "args" or "cmd" or "command"))
					throw new UnsupportedOptionException("ps", "-o " + key, "only pid, ppid, comm, args/cmd/command are available");
				if (header.Length > 0) anyHeader = true;
				cols.Add((key, header));
				}
			}
		else
			{
			cols.Add(("pid", "PID")); cols.Add(("ppid", "PPID")); cols.Add(("args", "COMMAND"));
			anyHeader = true;
			}
		bool needCmd = cols.Any(c => c.key is "args" or "cmd" or "command");
		var procs = ListProcesses(needCmd);
		HashSet<int>? only = null;
		if (o.Get('p') is string plist)
			{
			only = [];
			foreach (var s in plist.Split([',', ' '], StringSplitOptions.RemoveEmptyEntries))
				{
				if (!int.TryParse(s, out var pid)) { Console.Error.WriteLine($"ps: invalid pid '{s}'"); return 1; }
				only.Add(pid);
				}
			}
		var rows = procs.Where(p => only is null || only.Contains(p.Pid)).ToList();
		bool headers = anyHeader && !o.Has('h') && !o.HasLong("no-headers");
		string Cell(ProcInfo p, string key) => key switch
			{
			"pid" => p.Pid.ToString(), "ppid" => p.Ppid.ToString(), "comm" => BaseName(p.Name),
			_ => p.CommandLine.Length > 0 ? p.CommandLine : p.Name,
			};
		var widths = cols.Select(c => c.key is "pid" or "ppid" ? Math.Max(c.header.Length, 7) : 0).ToArray();
		var line = new StringBuilder();
		if (headers)
			{
			for (int i = 0; i < cols.Count; i++)
				{
				if (i > 0) line.Append(' ');
				line.Append(widths[i] > 0 ? cols[i].header.PadLeft(widths[i]) : cols[i].header);
				}
			Console.WriteLine(line.ToString().TrimEnd());
			}
		foreach (var p in rows)
			{
			line.Clear();
			for (int i = 0; i < cols.Count; i++)
				{
				if (i > 0) line.Append(' ');
				var cell = Cell(p, cols[i].key);
				line.Append(widths[i] > 0 ? cell.PadLeft(widths[i]) : cell);
				}
			Console.WriteLine(line.ToString().TrimEnd());
			}
		return only is not null && rows.Count == 0 ? 1 : 0;
		}
	}
