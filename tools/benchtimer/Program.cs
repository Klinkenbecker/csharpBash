// benchtimer: the benchmark's launcher and clock (tests/bench/compare3.sh).
//
//   benchtimer <n> <exe> [args...]
//
// Launches <exe> [args...] n times in a row, each straight from CreateProcessW, the way Claude
// Code launches a shell, and prints "<min> <total> <n>" in seconds. The interval is CreateProcessW
// to the process's exit, on the high-resolution clock. Before the timed launches it runs one
// untimed launch, so first-use costs of the timer itself are not timed.
//
// Why it exists (DECISIONS 2026-10-06): compare3.sh used bash's `time` inside Git Bash, so every
// shell was launched BY Git Bash. Starting a native program from Git Bash costs about 30 ms, which
// landed on C#Bash and WSL but not on Git Bash launching itself.
//
// The child gets NUL as stdin, stdout and stderr, inherits only that handle, inherits this
// process's console (none created per launch), and must exit 0 every time or the run fails.
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

static class BenchTimer
	{
	static int Main(string[] args)
		{
		if (args.Length < 2 || !int.TryParse(args[0], out int n) || n < 1)
			{
			Console.Error.WriteLine("usage: benchtimer <n> <exe> [args...]");
			return 2;
			}
		if (GetConsoleCP() == 0)
			{
			Console.Error.WriteLine("benchtimer: FATAL: no console to inherit; each launch would create its own. Run from a terminal.");
			return 2;
			}
		string exe = args[1].IndexOfAny(new[] { '/', '\\' }) >= 0 ? Path.GetFullPath(args[1]) : args[1];
		var cmd = new StringBuilder(Quote(exe));
		for (int i = 2; i < args.Length; i++) cmd.Append(' ').Append(Quote(args[i]));
		string line = cmd.ToString();

		IntPtr nul = CreateFileW("NUL", GENERIC_READ | GENERIC_WRITE, FILE_SHARE_READ | FILE_SHARE_WRITE,
			IntPtr.Zero, OPEN_EXISTING, 0, IntPtr.Zero);
		if (nul == INVALID_HANDLE) return Fail("open NUL");
		SetHandleInformation(nul, HANDLE_FLAG_INHERIT, HANDLE_FLAG_INHERIT);

		IntPtr attrs = HandleListAttribute(nul);
		try
			{
			Launch(line, nul, attrs);                       // untimed
			double min = double.MaxValue, total = 0;
			for (int i = 0; i < n; i++)
				{
				double t = Launch(line, nul, attrs);
				total += t;
				if (t < min) min = t;
				}
			Console.WriteLine($"{min:F6} {total:F6} {n}");
			return 0;
			}
		catch (Exception e)
			{
			Console.Error.WriteLine("benchtimer: FATAL: " + e.Message + " [" + line + "]");
			return 1;
			}
		}

	// One launch; returns elapsed seconds. Throws if the launch fails or the child exits non-zero.
	static double Launch(string line, IntPtr nul, IntPtr attrs)
		{
		var si = new STARTUPINFOEX();
		si.StartupInfo.cb = Marshal.SizeOf<STARTUPINFOEX>();
		si.StartupInfo.dwFlags = STARTF_USESTDHANDLES;
		si.StartupInfo.hStdInput = si.StartupInfo.hStdOutput = si.StartupInfo.hStdError = nul;
		si.lpAttributeList = attrs;
		var buf = new StringBuilder(line);   // CreateProcessW may write to the command line

		long start = Stopwatch.GetTimestamp();
		if (!CreateProcessW(null, buf, IntPtr.Zero, IntPtr.Zero, true, EXTENDED_STARTUPINFO_PRESENT,
				IntPtr.Zero, null, ref si, out PROCESS_INFORMATION pi))
			throw new Win32Exception(Marshal.GetLastWin32Error());
		WaitForSingleObject(pi.hProcess, INFINITE);
		long end = Stopwatch.GetTimestamp();

		GetExitCodeProcess(pi.hProcess, out uint code);
		CloseHandle(pi.hThread);
		CloseHandle(pi.hProcess);
		if (code != 0) throw new Exception($"child exited {code}");
		return (end - start) / (double)Stopwatch.Frequency;
		}

	// PROC_THREAD_ATTRIBUTE_HANDLE_LIST holding only NUL, so no other inheritable handle (such as
	// the pipe compare3.sh reads this program's output from) reaches the child.
	static IntPtr HandleListAttribute(IntPtr handle)
		{
		IntPtr size = IntPtr.Zero;
		InitializeProcThreadAttributeList(IntPtr.Zero, 1, 0, ref size);
		IntPtr list = Marshal.AllocHGlobal(size);
		if (!InitializeProcThreadAttributeList(list, 1, 0, ref size)) throw new Win32Exception(Marshal.GetLastWin32Error());
		IntPtr value = Marshal.AllocHGlobal(IntPtr.Size);
		Marshal.WriteIntPtr(value, handle);
		if (!UpdateProcThreadAttribute(list, 0, (IntPtr)PROC_THREAD_ATTRIBUTE_HANDLE_LIST, value, (IntPtr)IntPtr.Size, IntPtr.Zero, IntPtr.Zero))
			throw new Win32Exception(Marshal.GetLastWin32Error());
		return list;
		}

	// The MSVCRT command-line quoting rules (what CommandLineToArgvW and the C runtimes parse).
	static string Quote(string a)
		{
		if (a.Length > 0 && a.IndexOfAny(new[] { ' ', '\t', '"' }) < 0) return a;
		var sb = new StringBuilder("\"");
		int slashes = 0;
		foreach (char c in a)
			{
			if (c == '\\') { slashes++; continue; }
			if (c == '"') sb.Append('\\', slashes * 2 + 1);
			else sb.Append('\\', slashes);
			slashes = 0;
			sb.Append(c);
			}
		sb.Append('\\', slashes * 2).Append('"');
		return sb.ToString();
		}

	static int Fail(string what)
		{
		Console.Error.WriteLine("benchtimer: FATAL: " + what + ": " + new Win32Exception(Marshal.GetLastWin32Error()).Message);
		return 1;
		}

	const uint GENERIC_READ = 0x80000000, GENERIC_WRITE = 0x40000000;
	const uint FILE_SHARE_READ = 1, FILE_SHARE_WRITE = 2, OPEN_EXISTING = 3;
	const uint HANDLE_FLAG_INHERIT = 1, STARTF_USESTDHANDLES = 0x100;
	const uint EXTENDED_STARTUPINFO_PRESENT = 0x00080000, INFINITE = 0xFFFFFFFF;
	const int PROC_THREAD_ATTRIBUTE_HANDLE_LIST = 0x00020002;
	static readonly IntPtr INVALID_HANDLE = new IntPtr(-1);

	[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
	struct STARTUPINFO
		{
		public int cb; public string lpReserved, lpDesktop, lpTitle;
		public int dwX, dwY, dwXSize, dwYSize, dwXCountChars, dwYCountChars, dwFillAttribute;
		public uint dwFlags; public short wShowWindow, cbReserved2; public IntPtr lpReserved2;
		public IntPtr hStdInput, hStdOutput, hStdError;
		}

	[StructLayout(LayoutKind.Sequential)]
	struct STARTUPINFOEX { public STARTUPINFO StartupInfo; public IntPtr lpAttributeList; }

	[StructLayout(LayoutKind.Sequential)]
	struct PROCESS_INFORMATION { public IntPtr hProcess, hThread; public int dwProcessId, dwThreadId; }

	[DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
	static extern bool CreateProcessW(string lpApplicationName, StringBuilder lpCommandLine, IntPtr lpProcessAttributes,
		IntPtr lpThreadAttributes, bool bInheritHandles, uint dwCreationFlags, IntPtr lpEnvironment,
		string lpCurrentDirectory, ref STARTUPINFOEX lpStartupInfo, out PROCESS_INFORMATION lpProcessInformation);

	[DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
	static extern IntPtr CreateFileW(string name, uint access, uint share, IntPtr security, uint disposition, uint flags, IntPtr template);

	[DllImport("kernel32.dll", SetLastError = true)]
	static extern bool SetHandleInformation(IntPtr h, uint mask, uint flags);

	[DllImport("kernel32.dll", SetLastError = true)]
	static extern bool InitializeProcThreadAttributeList(IntPtr list, int count, int flags, ref IntPtr size);

	[DllImport("kernel32.dll", SetLastError = true)]
	static extern bool UpdateProcThreadAttribute(IntPtr list, uint flags, IntPtr attribute, IntPtr value, IntPtr size, IntPtr prev, IntPtr ret);

	[DllImport("kernel32.dll")] static extern uint WaitForSingleObject(IntPtr h, uint ms);
	[DllImport("kernel32.dll")] static extern bool GetExitCodeProcess(IntPtr h, out uint code);
	[DllImport("kernel32.dll")] static extern bool CloseHandle(IntPtr h);
	[DllImport("kernel32.dll")] static extern uint GetConsoleCP();
	}
