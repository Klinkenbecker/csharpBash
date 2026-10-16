# Run a command in a console of a chosen kind and print what it wrote (stdout and stderr, to a file first).
#   -Kind windowless : a console with no window (CREATE_NO_WINDOW), as Claude Code gives its shell and
#                      everything that shell starts; GetConsoleWindow() is NULL
#   -Kind window     : a console with a window, hidden (CREATE_NEW_CONSOLE + SW_HIDE); GetConsoleWindow() is not
#                      NULL, as in a classic console or Windows Terminal (ConPTY), measured 2026-10-15
# The same in both suite modes: the child gets a console of its own either way. A child still running after
# 15 s is killed and "TIMEOUT" printed, so a hang fails the case instead of stalling the suite.
param([string]$Kind, [string]$CommandLine, [string]$Cwd)
Add-Type -TypeDefinition @'
using System;
using System.Runtime.InteropServices;
using System.Text;
public static class ConsoleKind {
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    struct STARTUPINFO { public int cb; public string lpReserved, lpDesktop, lpTitle;
        public int dwX, dwY, dwXSize, dwYSize, dwXCountChars, dwYCountChars, dwFillAttribute, dwFlags;
        public short wShowWindow, cbReserved2; public IntPtr lpReserved2, hStdInput, hStdOutput, hStdError; }
    [StructLayout(LayoutKind.Sequential)] struct PROCESS_INFORMATION { public IntPtr hProcess, hThread; public int dwProcessId, dwThreadId; }
    [StructLayout(LayoutKind.Sequential)] struct SECURITY_ATTRIBUTES { public int nLength; public IntPtr lpSecurityDescriptor; public int bInheritHandle; }
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    static extern bool CreateProcessW(string app, StringBuilder cmd, IntPtr pa, IntPtr ta, bool inherit, uint flags,
        IntPtr env, string cwd, ref STARTUPINFO si, out PROCESS_INFORMATION pi);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    static extern IntPtr CreateFileW(string name, uint access, uint share, ref SECURITY_ATTRIBUTES sa, uint disp, uint flags, IntPtr tmpl);
    [DllImport("kernel32.dll")] static extern uint WaitForSingleObject(IntPtr h, uint ms);
    [DllImport("kernel32.dll")] static extern bool TerminateProcess(IntPtr h, uint code);
    [DllImport("kernel32.dll")] static extern bool CloseHandle(IntPtr h);
    public static bool Run(string kind, string commandLine, string cwd, string outFile) {
        var sa = new SECURITY_ATTRIBUTES(); sa.nLength = Marshal.SizeOf(typeof(SECURITY_ATTRIBUTES)); sa.bInheritHandle = 1;
        IntPtr nul = CreateFileW("NUL", 0xC0000000, 3, ref sa, 3, 0x80, IntPtr.Zero);
        IntPtr outH = CreateFileW(outFile, 0x40000000, 3, ref sa, 2, 0x80, IntPtr.Zero);   // GENERIC_WRITE, CREATE_ALWAYS
        var si = new STARTUPINFO(); si.cb = Marshal.SizeOf(typeof(STARTUPINFO));
        si.dwFlags = 0x100; si.hStdInput = nul; si.hStdOutput = outH; si.hStdError = outH;   // STARTF_USESTDHANDLES
        uint flags = 0x400;   // CREATE_UNICODE_ENVIRONMENT
        if (kind == "windowless") flags |= 0x08000000;   // CREATE_NO_WINDOW
        else { flags |= 0x10; si.dwFlags |= 0x1; si.wShowWindow = 0; }   // CREATE_NEW_CONSOLE, STARTF_USESHOWWINDOW, SW_HIDE
        PROCESS_INFORMATION pi;
        bool ok = CreateProcessW(null, new StringBuilder(commandLine), IntPtr.Zero, IntPtr.Zero, true, flags, IntPtr.Zero, cwd, ref si, out pi);
        CloseHandle(nul); CloseHandle(outH);
        if (!ok) throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
        bool done = WaitForSingleObject(pi.hProcess, 15000) == 0;
        if (!done) { TerminateProcess(pi.hProcess, 1); WaitForSingleObject(pi.hProcess, 5000); }
        CloseHandle(pi.hThread); CloseHandle(pi.hProcess);
        return done;
    }
}
'@
$out = [System.IO.Path]::GetTempFileName()
$done = [ConsoleKind]::Run($Kind, $CommandLine, $Cwd, $out)
$fs = [System.IO.File]::Open($out, 'Open', 'Read', 'ReadWrite, Delete'); $text = (New-Object System.IO.StreamReader($fs)).ReadToEnd(); $fs.Dispose()
$text -replace "`r", '' | Write-Host -NoNewline
Remove-Item -LiteralPath $out -ErrorAction SilentlyContinue
if (-not $done) { Write-Host "TIMEOUT" }
