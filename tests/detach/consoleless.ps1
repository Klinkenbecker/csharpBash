# Start an outer C#Bash CONSOLE-LESS (as Claude Code starts its tool shell), which runs a nested
# shell that backgrounds the probe and exits. Then read the probe's log: if the probe lost its
# console when the outer shell's job closed, its pings fail ("ping failed" lines, fast ticks).
param([string]$Exe, [string]$Log)
Add-Type -TypeDefinition @'
using System;
using System.Runtime.InteropServices;
using System.Text;
public static class DetachedRun {
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
    [DllImport("kernel32.dll")] static extern bool CloseHandle(IntPtr h);
    public static void Run(string commandLine, string cwd) {
        var sa = new SECURITY_ATTRIBUTES(); sa.nLength = Marshal.SizeOf(typeof(SECURITY_ATTRIBUTES)); sa.bInheritHandle = 1;
        IntPtr nul = CreateFileW("NUL", 0xC0000000, 3, ref sa, 3, 0x80, IntPtr.Zero);
        var si = new STARTUPINFO(); si.cb = Marshal.SizeOf(typeof(STARTUPINFO));
        si.dwFlags = 0x100; si.hStdInput = nul; si.hStdOutput = nul; si.hStdError = nul;
        PROCESS_INFORMATION pi;
        bool ok = CreateProcessW(null, new StringBuilder(commandLine), IntPtr.Zero, IntPtr.Zero, true,
            0x8 | 0x400, IntPtr.Zero, cwd, ref si, out pi);   // DETACHED_PROCESS | CREATE_UNICODE_ENVIRONMENT
        CloseHandle(nul);
        if (!ok) throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
        WaitForSingleObject(pi.hProcess, 30000);
        CloseHandle(pi.hThread); CloseHandle(pi.hProcess);
    }
}
'@
$dir = Split-Path -Parent $MyInvocation.MyCommand.Path
if (Test-Path "$dir/$Log") { Remove-Item "$dir/$Log" }
[DetachedRun]::Run("`"$Exe`" `"$dir/consoleless-outer.sh`" `"$Log`"", $dir)
Start-Sleep -Milliseconds 4000
$lines = @(Get-Content "$dir/$Log")
"{0}: {1} lines 4 s after the outer shell exited; 'ping failed': {2}" -f $Exe, $lines.Count, @($lines | Where-Object { $_ -match 'ping failed' }).Count
