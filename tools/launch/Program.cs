// launch: starts one process the way the tests need it, in place of the C# the PowerShell helpers embedded
// (tests/run-tests.ps1's DetachedRun, tests/fixtures/console-kind.ps1, tests/detach/consoleless.ps1; the architect,
// 2026-11-14: PowerShell removed before the next publish).
//
//   launch --detached|--windowless|--window [--timeout <s>] [--cwd <dir>] [--stderr nul|out]
//          (--out <file> | --print) (--cmdline <line> | -- <exe> [args...])
//
//   --detached    DETACHED_PROCESS: no console at all, as Claude Code starts its tool shell
//   --windowless  a console with no window (CREATE_NO_WINDOW), as Claude Code gives its shell's children
//   --window      a console with a window, hidden (CREATE_NEW_CONSOLE + SW_HIDE), as a classic console gives
// stdin is NUL; stdout goes to --out's file, or with --print to a temporary file printed afterwards with CRs removed
// (and "TIMEOUT" after it when the child was killed); stderr is NUL unless --stderr out (then with stdout).
// --cmdline is used as written; after `--` each argument is quoted as CreateProcess expects.
// Exit: the child's exit code; 124 when it ran past --timeout (default 120 s) and was killed with its children.
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

static class Launch
	{
	static int Main(string[] args)
		{
		string kind = "", cwd = Directory.GetCurrentDirectory(), outFile = null, cmdline = null;
		bool print = false, stderrOut = false; uint timeoutMs = 120_000;
		int i = 0;
		for (; i < args.Length; i++)
			{
			switch (args[i])
				{
				case "--detached": case "--windowless": case "--window": kind = args[i][2..]; break;
				case "--timeout" when i + 1 < args.Length && uint.TryParse(args[i + 1], out uint s) && s > 0: timeoutMs = s * 1000; i++; break;
				case "--cwd" when i + 1 < args.Length: cwd = args[++i]; break;
				case "--out" when i + 1 < args.Length: outFile = args[++i]; break;
				case "--print": print = true; break;
				case "--stderr" when i + 1 < args.Length && args[i + 1] is "nul" or "out": stderrOut = args[++i] == "out"; break;
				case "--cmdline" when i + 1 < args.Length: cmdline = args[++i]; break;
				case "--": i++; goto done;
				default: return Usage($"unknown or incomplete option '{args[i]}'");
				}
			}
		done:
		if (kind == "") return Usage("one of --detached, --windowless, --window");
		if ((outFile is null) == !print) return Usage("one of --out <file> or --print");
		if (cmdline is null)
			{
			if (i >= args.Length) return Usage("a --cmdline or `-- <exe> [args...]`");
			var sb = new StringBuilder();
			if (args[i].IndexOfAny(new[] { '/', '\\' }) >= 0) args[i] = Path.GetFullPath(args[i]);   // as benchtimer: a path is made full
			for (int k = i; k < args.Length; k++) { if (sb.Length > 0) sb.Append(' '); sb.Append(Quote(args[k])); }
			cmdline = sb.ToString();
			}
		string file = outFile ?? Path.GetTempFileName();
		try
			{
			int code = Run(kind, cmdline, cwd, file, stderrOut, timeoutMs, out bool timedOut);
			if (print)
				{
				var text = ReadShared(file).Replace("\r", "");
				var stdout = Console.OpenStandardOutput();
				var bytes = Encoding.UTF8.GetBytes(text);
				stdout.Write(bytes, 0, bytes.Length);
				if (timedOut) { var t = Encoding.UTF8.GetBytes("TIMEOUT\n"); stdout.Write(t, 0, t.Length); }
				stdout.Flush();
				}
			return timedOut ? 124 : code;
			}
		catch (Exception e) { Console.Error.WriteLine("launch: " + e.Message + " [" + cmdline + "]"); return 125; }
		finally { if (print) try { File.Delete(file); } catch (IOException) { } }
		}

	static int Usage(string why)
		{
		Console.Error.WriteLine("launch: " + why);
		Console.Error.WriteLine("usage: launch --detached|--windowless|--window [--timeout <s>] [--cwd <dir>] [--stderr nul|out] (--out <file> | --print) (--cmdline <line> | -- <exe> [args...])");
		return 2;
		}

	static string ReadShared(string path)
		{
		using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
		using var r = new StreamReader(fs, new UTF8Encoding(false));
		return r.ReadToEnd();
		}

	// CreateProcess's quoting, as benchtimer quotes (a backslash run before a quote is doubled)
	static string Quote(string a)
		{
		if (a.Length > 0 && a.IndexOfAny(new[] { ' ', '\t', '"' }) < 0) return a;
		var sb = new StringBuilder("\"");
		int bs = 0;
		foreach (char c in a)
			{
			if (c == '\\') { bs++; continue; }
			if (c == '"') { sb.Append('\\', bs * 2 + 1); bs = 0; sb.Append('"'); continue; }
			sb.Append('\\', bs); bs = 0; sb.Append(c);
			}
		sb.Append('\\', bs * 2).Append('"');
		return sb.ToString();
		}

	static int Run(string kind, string commandLine, string cwd, string outFile, bool stderrOut, uint timeoutMs, out bool timedOut)
		{
		var sa = new SECURITY_ATTRIBUTES { nLength = Marshal.SizeOf<SECURITY_ATTRIBUTES>(), bInheritHandle = 1 };
		IntPtr nul = CreateFileW("NUL", 0xC0000000, 3, ref sa, 3, 0x80, IntPtr.Zero);
		IntPtr outH = CreateFileW(outFile, 0x40000000, 3, ref sa, 2, 0x80, IntPtr.Zero);   // GENERIC_WRITE, CREATE_ALWAYS
		if (outH == new IntPtr(-1)) { CloseHandle(nul); throw new Win32Exception(Marshal.GetLastWin32Error(), "cannot open " + outFile); }
		var si = new STARTUPINFO { cb = Marshal.SizeOf<STARTUPINFO>(), dwFlags = 0x100, hStdInput = nul, hStdOutput = outH, hStdError = stderrOut ? outH : nul };
		uint flags = 0x400;   // CREATE_UNICODE_ENVIRONMENT
		if (kind == "detached") flags |= 0x8;                 // DETACHED_PROCESS
		else if (kind == "windowless") flags |= 0x08000000;   // CREATE_NO_WINDOW
		else { flags |= 0x10; si.dwFlags |= 0x1; si.wShowWindow = 0; }   // CREATE_NEW_CONSOLE, STARTF_USESHOWWINDOW, SW_HIDE
		bool ok = CreateProcessW(null, new StringBuilder(commandLine), IntPtr.Zero, IntPtr.Zero, true, flags, IntPtr.Zero, cwd, ref si, out PROCESS_INFORMATION pi);
		CloseHandle(nul); CloseHandle(outH);
		if (!ok) throw new Win32Exception(Marshal.GetLastWin32Error());
		timedOut = WaitForSingleObject(pi.hProcess, timeoutMs) != 0;
		if (timedOut)
			{
			try { using var p = Process.GetProcessById(pi.dwProcessId); p.Kill(entireProcessTree: true); } catch (Exception) { }
			WaitForSingleObject(pi.hProcess, 10_000);
			}
		GetExitCodeProcess(pi.hProcess, out uint code);
		CloseHandle(pi.hThread); CloseHandle(pi.hProcess);
		return (int)code;
		}

	[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
	struct STARTUPINFO
		{
		public int cb; public string lpReserved, lpDesktop, lpTitle;
		public int dwX, dwY, dwXSize, dwYSize, dwXCountChars, dwYCountChars, dwFillAttribute, dwFlags;
		public short wShowWindow, cbReserved2; public IntPtr lpReserved2, hStdInput, hStdOutput, hStdError;
		}
	[StructLayout(LayoutKind.Sequential)] struct PROCESS_INFORMATION { public IntPtr hProcess, hThread; public int dwProcessId, dwThreadId; }
	[StructLayout(LayoutKind.Sequential)] struct SECURITY_ATTRIBUTES { public int nLength; public IntPtr lpSecurityDescriptor; public int bInheritHandle; }
	[DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
	static extern bool CreateProcessW(string app, StringBuilder cmd, IntPtr pa, IntPtr ta, bool inherit, uint flags, IntPtr env, string cwd, ref STARTUPINFO si, out PROCESS_INFORMATION pi);
	[DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
	static extern IntPtr CreateFileW(string name, uint access, uint share, ref SECURITY_ATTRIBUTES sa, uint disp, uint flags, IntPtr tmpl);
	[DllImport("kernel32.dll")] static extern uint WaitForSingleObject(IntPtr h, uint ms);
	[DllImport("kernel32.dll")] static extern bool GetExitCodeProcess(IntPtr h, out uint code);
	[DllImport("kernel32.dll")] static extern bool CloseHandle(IntPtr h);
	}
