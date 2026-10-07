using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32.SafeHandles;

namespace Bash.Evaluator;

/// <summary>
/// The one way this shell starts an external program (DECISIONS 2026-10-04, "the real file
/// handle"). The child gets exactly the three std handles the caller chose -- a file, NUL, a pipe
/// end, or the shell's own -- and inherits nothing else.
/// <list type="bullet">
/// <item>A redirect is the file itself, as bash's dup2 makes it, so `server &gt; log &amp;` keeps
/// writing after the shell has gone and `cat log` can read it meanwhile. It used to be a pipe that
/// a shell thread copied into a file opened unshared: the output died with the shell, and the log
/// could not be read while the command ran.</item>
/// <item>Only the listed handles are inherited (PROC_THREAD_ATTRIBUTE_HANDLE_LIST). Otherwise Windows
/// hands a child every inheritable handle in the process, so a child started by one pipeline stage
/// could hold another stage's pipe open and its reader would never see EOF.</item>
/// </list>
/// Command line and environment block are built exactly as Process.Start builds them. The child is
/// created suspended, so its handle is held before it can exit and the exit code stays readable.
/// Superseded HiddenConsole.StartInherited (DECISIONS 2026-10-03), which did this for an
/// all-inherited child only, beside Process.Start for everything else.
/// </summary>
public static class ChildLauncher
	{
	/// <summary>Start <paramref name="psi"/> (FileName, ArgumentList, Environment, WorkingDirectory
	/// are used) on the given std handles; any may be zero. Windowless when <paramref name="noWindow"/>.
	/// With <paramref name="breakaway"/> the child leaves the job this shell is in, when that job
	/// allows it. Throws Win32Exception on failure, as Process.Start does.</summary>
	public static Process Start(ProcessStartInfo psi, IntPtr hIn, IntPtr hOut, IntPtr hErr, bool noWindow, bool breakaway = false)
		{
		var cmd = new StringBuilder();
		var file = psi.FileName.Trim();
		bool quoted = file.Length >= 2 && file[0] == '"' && file[^1] == '"';
		if (!quoted) cmd.Append('"');
		cmd.Append(file);
		if (!quoted) cmd.Append('"');
		foreach (var arg in psi.ArgumentList) { cmd.Append(' '); AppendArgument(cmd, arg); }

		var keys = new List<string>(psi.Environment.Keys);
		keys.Sort(StringComparer.OrdinalIgnoreCase);
		var env = new StringBuilder();
		foreach (var k in keys)
			if (psi.Environment[k] is { } v) env.Append(k).Append('=').Append(v).Append('\0');
		env.Append('\0');

		// one inheritable duplicate per distinct handle (stdout and stderr are often the same one)
		var self  = GetCurrentProcess();
		var dups  = new Dictionary<IntPtr, IntPtr>(3);
		IntPtr Inheritable(IntPtr h)
			{
			if (h == IntPtr.Zero || h == InvalidHandle) return IntPtr.Zero;
			if (dups.TryGetValue(h, out var d)) return d;
			if (!DuplicateHandle(self, h, self, out d, 0, true, DUPLICATE_SAME_ACCESS)) return IntPtr.Zero;
			dups[h] = d;
			return d;
			}
		IntPtr cIn = Inheritable(hIn), cOut = Inheritable(hOut), cErr = Inheritable(hErr);
		var list = dups.Values.ToArray();

		IntPtr attrList = IntPtr.Zero, handleBuf = IntPtr.Zero, envBuf = IntPtr.Zero;
		var pi = new ProcessInformation();
		try
			{
			var si = new StartupInfoEx();
			si.StartupInfo.cb         = Marshal.SizeOf<StartupInfoEx>();
			si.StartupInfo.dwFlags    = STARTF_USESTDHANDLES;
			si.StartupInfo.hStdInput  = cIn;
			si.StartupInfo.hStdOutput = cOut;
			si.StartupInfo.hStdError  = cErr;
			uint flags = CREATE_SUSPENDED | CREATE_UNICODE_ENVIRONMENT | (noWindow ? CREATE_NO_WINDOW : 0);
			if (list.Length > 0)
				{
				IntPtr size = IntPtr.Zero;
				InitializeProcThreadAttributeList(IntPtr.Zero, 1, 0, ref size);
				attrList = Marshal.AllocHGlobal(size);
				if (!InitializeProcThreadAttributeList(attrList, 1, 0, ref size))
					{ Marshal.FreeHGlobal(attrList); attrList = IntPtr.Zero; throw new Win32Exception(Marshal.GetLastWin32Error()); }
				handleBuf = Marshal.AllocHGlobal(IntPtr.Size * list.Length);
				for (int i = 0; i < list.Length; i++) Marshal.WriteIntPtr(handleBuf, i * IntPtr.Size, list[i]);
				if (!UpdateProcThreadAttribute(attrList, 0, PROC_THREAD_ATTRIBUTE_HANDLE_LIST, handleBuf,
				                               (IntPtr)(IntPtr.Size * list.Length), IntPtr.Zero, IntPtr.Zero))
					throw new Win32Exception(Marshal.GetLastWin32Error());
				si.lpAttributeList = attrList;
				flags |= EXTENDED_STARTUPINFO_PRESENT;
				}
			envBuf = Marshal.StringToHGlobalUni(env.ToString());
			string? cwd = string.IsNullOrEmpty(psi.WorkingDirectory) ? null : psi.WorkingDirectory;

			bool Create(uint f)
				{
				IntPtr cmdBuf = Marshal.StringToHGlobalUni(cmd.ToString());   // CreateProcessW may write to it
				try { return CreateProcessW(IntPtr.Zero, cmdBuf, IntPtr.Zero, IntPtr.Zero, list.Length > 0, f, envBuf, cwd, ref si, out pi); }
				finally { Marshal.FreeHGlobal(cmdBuf); }
				}
			bool ok = Create(flags | (breakaway ? CREATE_BREAKAWAY_FROM_JOB : 0));
			int err = ok ? 0 : Marshal.GetLastWin32Error();
			if (!ok && breakaway && err == ERROR_ACCESS_DENIED)
				{ ok = Create(flags); err = ok ? 0 : Marshal.GetLastWin32Error(); }   // our job forbids breakaway: start inside it
			if (!ok) throw new Win32Exception(err);
			try
				{
				var proc = Process.GetProcessById(pi.dwProcessId);
				_ = proc.Handle;   // held from here on: ExitCode works however soon the child exits
				ResumeThread(pi.hThread);
				return proc;
				}
			catch
				{
				TerminateProcess(pi.hProcess, 1);
				throw;
				}
			}
		finally
			{
			if (pi.hThread  != IntPtr.Zero) CloseHandle(pi.hThread);
			if (pi.hProcess != IntPtr.Zero) CloseHandle(pi.hProcess);
			foreach (var d in list) CloseHandle(d);
			if (attrList  != IntPtr.Zero) { DeleteProcThreadAttributeList(attrList); Marshal.FreeHGlobal(attrList); }
			if (handleBuf != IntPtr.Zero) Marshal.FreeHGlobal(handleBuf);
			if (envBuf    != IntPtr.Zero) Marshal.FreeHGlobal(envBuf);
			}
		}

	// ── files and pipes for a child's std handles ──────────────────────────────

	public enum Access { Read, Truncate, Append, ReadWrite, Null }

	/// <summary>This process's own std handle for fd 0, 1 or 2.</summary>
	public static IntPtr StdHandle(int fd) => GetStdHandle(fd switch { 0 => -10, 1 => -11, _ => -12 });

	/// <summary>Write bytes at the handle's own position (a diagnostic for a child that never ran).</summary>
	public static void Write(SafeFileHandle h, byte[] bytes) => WriteFile(h, bytes, bytes.Length, out _, IntPtr.Zero);

	/// <summary>Open <paramref name="path"/> for a child's std handle, shared for reading, writing and
	/// deleting (another command may read the log while this one writes it). Truncate opens and
	/// empties without recreating (attributes kept, a hidden file works); Append writes only at the
	/// end (FILE_APPEND_DATA: two appenders interleave instead of overwriting, as O_APPEND does);
	/// Null is the NUL device. Throws the .NET exception for the failure (FileNotFound,
	/// DirectoryNotFound, UnauthorizedAccess, or IOException).</summary>
	public static SafeFileHandle Open(string path, Access how)
		{
		var (access, disposition) = how switch
			{
			Access.Read     => (GENERIC_READ, OPEN_EXISTING),
			Access.Truncate => (GENERIC_WRITE, OPEN_ALWAYS),
			Access.Append   => (FILE_APPEND_DATA | FILE_READ_ATTRIBUTES | SYNCHRONIZE, OPEN_ALWAYS),
			Access.ReadWrite => (GENERIC_READ | GENERIC_WRITE, OPEN_ALWAYS),   // `<>`: created if missing, not truncated
			_               => (GENERIC_READ | GENERIC_WRITE, OPEN_EXISTING),
			};
		const uint share = FILE_SHARE_READ | FILE_SHARE_WRITE | FILE_SHARE_DELETE;
		var h = CreateFileW(how == Access.Null ? "NUL" : path, access, share, IntPtr.Zero, disposition, FILE_ATTRIBUTE_NORMAL, IntPtr.Zero);
		if (h.IsInvalid) throw IoException(Marshal.GetLastWin32Error(), path);
		if (how == Access.Truncate && !SetEndOfFile(h))
			{
			int err = Marshal.GetLastWin32Error();
			h.Dispose();
			throw IoException(err, path);
			}
		return h;
		}

	/// <summary>An anonymous pipe: the shell's end and the child's end. Neither is inheritable;
	/// <see cref="Start"/> makes the inheritable duplicate the child receives.</summary>
	public static (SafeFileHandle Shell, SafeFileHandle Child) Pipe(bool childReads)
		{
		if (!CreatePipe(out var read, out var write, IntPtr.Zero, 0))
			throw new Win32Exception(Marshal.GetLastWin32Error());
		return childReads ? (write, read) : (read, write);
		}

	private static Exception IoException(int err, string path) => err switch
		{
		2 => new FileNotFoundException(null, path),
		3 => new DirectoryNotFoundException(),
		5 => new UnauthorizedAccessException(),
		_ => new IOException(new Win32Exception(err).Message),
		};

	/// <summary>One argument, quoted the way .NET's own command-line builder (PasteArguments)
	/// quotes it, so the child parses exactly what Process.Start would have given it.</summary>
	private static void AppendArgument(StringBuilder sb, string arg)
		{
		bool plain = arg.Length != 0;
		foreach (var c in arg) if (char.IsWhiteSpace(c) || c == '"') { plain = false; break; }
		if (plain) { sb.Append(arg); return; }
		sb.Append('"');
		for (int i = 0; i < arg.Length; )
			{
			char c = arg[i++];
			if (c == '\\')
				{
				int n = 1;
				while (i < arg.Length && arg[i] == '\\') { i++; n++; }
				if (i == arg.Length)  sb.Append('\\', n * 2);
				else if (arg[i] == '"') { sb.Append('\\', n * 2 + 1).Append('"'); i++; }
				else                  sb.Append('\\', n);
				continue;
				}
			if (c == '"') { sb.Append('\\').Append('"'); continue; }
			sb.Append(c);
			}
		sb.Append('"');
		}

	// ── Win32 ───────────────────────────────────────────────────────────────────

	[StructLayout(LayoutKind.Sequential)]
	private struct StartupInfo
		{
		public int cb;
		public IntPtr lpReserved, lpDesktop, lpTitle;
		public int dwX, dwY, dwXSize, dwYSize, dwXCountChars, dwYCountChars, dwFillAttribute, dwFlags;
		public short wShowWindow, cbReserved2;
		public IntPtr lpReserved2, hStdInput, hStdOutput, hStdError;
		}

	[StructLayout(LayoutKind.Sequential)]
	private struct StartupInfoEx { public StartupInfo StartupInfo; public IntPtr lpAttributeList; }

	[StructLayout(LayoutKind.Sequential)]
	private struct ProcessInformation { public IntPtr hProcess, hThread; public int dwProcessId, dwThreadId; }

	[DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
	private static extern bool CreateProcessW(IntPtr lpApplicationName, IntPtr lpCommandLine, IntPtr lpProcessAttributes,
		IntPtr lpThreadAttributes, bool bInheritHandles, uint dwCreationFlags, IntPtr lpEnvironment,
		string? lpCurrentDirectory, ref StartupInfoEx lpStartupInfo, out ProcessInformation lpProcessInformation);
	[DllImport("kernel32.dll", SetLastError = true)]
	private static extern bool InitializeProcThreadAttributeList(IntPtr lpAttributeList, int dwAttributeCount, int dwFlags, ref IntPtr lpSize);
	[DllImport("kernel32.dll", SetLastError = true)]
	private static extern bool UpdateProcThreadAttribute(IntPtr lpAttributeList, uint dwFlags, IntPtr attribute,
		IntPtr lpValue, IntPtr cbSize, IntPtr lpPreviousValue, IntPtr lpReturnSize);
	[DllImport("kernel32.dll")] private static extern void DeleteProcThreadAttributeList(IntPtr lpAttributeList);
	[DllImport("kernel32.dll")] private static extern IntPtr GetCurrentProcess();
	[DllImport("kernel32.dll", SetLastError = true)]
	private static extern bool DuplicateHandle(IntPtr hSourceProcess, IntPtr hSource, IntPtr hTargetProcess,
		out IntPtr lpTarget, uint dwDesiredAccess, bool bInheritHandle, uint dwOptions);
	[DllImport("kernel32.dll", SetLastError = true)] private static extern uint ResumeThread(IntPtr hThread);
	[DllImport("kernel32.dll", SetLastError = true)] private static extern bool TerminateProcess(IntPtr hProcess, uint exitCode);
	[DllImport("kernel32.dll", SetLastError = true)] private static extern bool CloseHandle(IntPtr h);
	[DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
	private static extern SafeFileHandle CreateFileW(string lpFileName, uint dwDesiredAccess, uint dwShareMode,
		IntPtr lpSecurityAttributes, uint dwCreationDisposition, uint dwFlagsAndAttributes, IntPtr hTemplateFile);
	[DllImport("kernel32.dll", SetLastError = true)] private static extern bool SetEndOfFile(SafeFileHandle hFile);
	[DllImport("kernel32.dll")] private static extern IntPtr GetStdHandle(int nStdHandle);
	[DllImport("kernel32.dll", SetLastError = true)]
	private static extern bool WriteFile(SafeFileHandle hFile, byte[] lpBuffer, int nNumberOfBytesToWrite, out int lpNumberOfBytesWritten, IntPtr lpOverlapped);
	[DllImport("kernel32.dll", SetLastError = true)]
	private static extern bool CreatePipe(out SafeFileHandle hReadPipe, out SafeFileHandle hWritePipe, IntPtr lpPipeAttributes, int nSize);

	private static readonly IntPtr InvalidHandle = new(-1);
	private static readonly IntPtr PROC_THREAD_ATTRIBUTE_HANDLE_LIST = new(0x20002);
	private const uint CREATE_SUSPENDED = 0x4, CREATE_UNICODE_ENVIRONMENT = 0x400, CREATE_NO_WINDOW = 0x08000000,
	                   CREATE_BREAKAWAY_FROM_JOB = 0x01000000, EXTENDED_STARTUPINFO_PRESENT = 0x00080000;
	private const int  STARTF_USESTDHANDLES = 0x100, ERROR_ACCESS_DENIED = 5;
	private const uint DUPLICATE_SAME_ACCESS = 0x2;
	private const uint GENERIC_READ = 0x80000000, GENERIC_WRITE = 0x40000000, FILE_APPEND_DATA = 0x4,
	                   FILE_READ_ATTRIBUTES = 0x80, SYNCHRONIZE = 0x00100000;
	private const uint FILE_SHARE_READ = 1, FILE_SHARE_WRITE = 2, FILE_SHARE_DELETE = 4;
	private const uint OPEN_EXISTING = 3, OPEN_ALWAYS = 4, FILE_ATTRIBUTE_NORMAL = 0x80;
	}
