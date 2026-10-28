<#
.SYNOPSIS
  Self-checking test runner for the Bash interpreter.

  Runs each script-mode test against the built Release exe, captures STDOUT only
  (stderr is discarded - job-id notices, prompts, and error text live there), and
  compares line-by-line against tests/expected/<name>.out. Reports PASS/FAIL and
  exits non-zero if any test fails.

  Expected-output files are authored from bash semantics, not captured from the
  program, so a regression in the interpreter shows up as a FAIL rather than being
  silently baked in.

.PARAMETER NoBuild
  Skip the Release build and use the existing exe.

.EXAMPLE
  pwsh tests/run-tests.ps1
  pwsh tests/run-tests.ps1 -NoBuild
#>
param(
    [switch]$NoBuild,
    # Start every case as Claude Code starts its shell: DETACHED_PROCESS (no console), stdin NUL,
    # stdout to a file, stderr NUL. Exercises the console-less spawn path (DECISIONS 2026-10-03),
    # which a run from a console never reaches.
    [switch]$Detached,
    # Test this interpreter instead of the Release build (implies -NoBuild). Not named -Exe:
    # PowerShell names are case-insensitive, so it would BE $exe below and be overwritten.
    [string]$Interpreter
)

# Keep 'Continue' (not 'Stop'): tests intentionally write to stderr (job notices,
# error-path messages), and under 'Stop' PowerShell 5.1 turns a native command's
# stderr into a terminating error. We check exit codes explicitly instead.
$ErrorActionPreference = 'Continue'
$testsDir = $PSScriptRoot
# cases ask for unsupported options on purpose: not requests for the unserviced log (%USERPROFILE%/.bash; 2026-10-27)
$env:BASH_UNSERVICED_LOG = '/dev/null'
$proj     = Join-Path $testsDir '..\Bash'
$exe      = Join-Path $proj 'bin\Release\net8.0\Bash.exe'
if ($Interpreter) { $exe = $Interpreter; $NoBuild = $true }
# the Native AOT build (the shipped one, tested by tools/deploy.sh) has no Bash.dll beside it; AotOnly cases skip otherwise
$aot = -not (Test-Path (Join-Path (Split-Path $exe) 'Bash.dll'))

if ($Detached) {
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
    [DllImport("kernel32.dll")] static extern bool TerminateProcess(IntPtr h, uint code);
    [DllImport("kernel32.dll")] static extern bool CloseHandle(IntPtr h);

    // Returns false on timeout (the child is then killed).
    public static bool Run(string commandLine, string cwd, string outFile, uint timeoutMs) {
        var sa = new SECURITY_ATTRIBUTES(); sa.nLength = Marshal.SizeOf(typeof(SECURITY_ATTRIBUTES)); sa.bInheritHandle = 1;
        IntPtr nul  = CreateFileW("NUL", 0xC0000000, 3, ref sa, 3, 0x80, IntPtr.Zero);
        IntPtr outH = CreateFileW(outFile, 0x40000000, 3, ref sa, 2, 0x80, IntPtr.Zero);
        var si = new STARTUPINFO(); si.cb = Marshal.SizeOf(typeof(STARTUPINFO));
        si.dwFlags = 0x100; si.hStdInput = nul; si.hStdOutput = outH; si.hStdError = nul;   // STARTF_USESTDHANDLES
        PROCESS_INFORMATION pi;
        bool ok = CreateProcessW(null, new StringBuilder(commandLine), IntPtr.Zero, IntPtr.Zero, true,
            0x8 | 0x400, IntPtr.Zero, cwd, ref si, out pi);   // DETACHED_PROCESS | CREATE_UNICODE_ENVIRONMENT
        CloseHandle(nul); CloseHandle(outH);
        if (!ok) throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
        bool done = WaitForSingleObject(pi.hProcess, timeoutMs) == 0;
        if (!done) TerminateProcess(pi.hProcess, 99);
        CloseHandle(pi.hThread); CloseHandle(pi.hProcess);
        return done;
    }
}
'@
}

if (-not $NoBuild) {
    # Clear any stale interpreter instance under this project (a hung test process
    # locks Bash.exe and fails the copy step). Match on resolved path - ours only.
    $projFull = (Resolve-Path $proj).Path
    Get-Process -Name Bash -ErrorAction SilentlyContinue |
        Where-Object { $_.Path -and $_.Path.StartsWith($projFull, [System.StringComparison]::OrdinalIgnoreCase) } |
        Stop-Process -Force -ErrorAction SilentlyContinue
    Write-Host "Building (Release)..." -ForegroundColor Cyan
    & dotnet build -c Release $proj -nologo | Out-Null
    if ($LASTEXITCODE -ne 0) { Write-Error "build failed"; exit 2 }
}
if (-not (Test-Path $exe)) { Write-Error "interpreter not found: $exe (run without -NoBuild)"; exit 2 }
Write-Host ("interpreter: {0}  ({1}){2}" -f $exe, (@(& $exe --version)[-1]), $(if ($Detached) { '  DETACHED' } else { '' }))

# Test manifest. Script paths are relative to tests/ so $0 is deterministic.
$tests = @(
    @{ Name = 'phase5';      Script = 'phase5.sh';            Args = @() }
    @{ Name = 'gap';         Script = 'gap.sh';               Args = @() }
    @{ Name = 'quote';       Script = 'quote.sh';             Args = @() }
    @{ Name = 'args';        Script = 'args.sh';              Args = @('A','B','C') }
    @{ Name = 'andor';       Script = 'cases\andor.sh';       Args = @() }
    @{ Name = 'mixed';       Script = 'cases\mixed.sh';       Args = @() }
    @{ Name = 'funcdef';     Script = 'cases\funcdef.sh';     Args = @() }
    @{ Name = 'trap_exit';   Script = 'cases\trap_exit.sh';   Args = @() }
    @{ Name = 'trap_exit2';  Script = 'cases\trap_exit2.sh';  Args = @() }
    @{ Name = 'jobs_wait';   Script = 'cases\jobs_wait.sh';   Args = @() }
    @{ Name = 'eof_amp';     Script = 'cases\eof_amp.sh';     Args = @() }   # trailing & at EOF must not crash the lexer
    @{ Name = 'shebang';     Script = 'cases\shebang.sh';     Args = @() }   # ./script with #! dispatched in-process + exit code
    @{ Name = 'arith';       Script = 'cases\arith.sh';       Args = @() }   # arithmetic operators incl. && / || (regression) + hex
    @{ Name = 'varsplit';    Script = 'cases\varsplit.sh';    Args = @() }   # unquoted $var word-splits; empty -> zero fields (var fast-path guard)
    @{ Name = 'coreutils';   Script = 'cases\coreutils.sh';   Args = @() }   # in-process basename/dirname/seq/mkdir
    @{ Name = 'cat';         Script = 'cases\cat.sh';         Args = @() }   # cat: multi-file, $()-capture, pipe (byte-faithful I/O)
    @{ Name = 'textutils';   Script = 'cases\textutils.sh';   Args = @() }   # head/tail/wc/rev/tac (+ stdin via pipe)
    @{ Name = 'fieldutils';  Script = 'cases\fieldutils.sh';  Args = @() }   # tr/cut/uniq/nl/fold
    @{ Name = 'pipeline';    Script = 'cases\pipeline.sh';    Args = @() }   # 3/4-stage all-builtin pipes (race fix) + wc -c (CRLF fix)
    @{ Name = 'fileutils';   Script = 'cases\fileutils.sh';   Args = @() }   # paste/comm/cmp/tee/touch/rmdir
    @{ Name = 'destructive'; Script = 'cases\destructive.sh'; Args = @() }   # rm/mv/cp (+ test ! negation)
    @{ Name = 'sysutils';    Script = 'cases\sysutils.sh';    Args = @() }   # uname/factor/cal
    @{ Name = 'redirect';    Script = 'cases\redirect.sh';    Args = @() }   # /dev/null, fd-aware 2>, runtime-error continues
    @{ Name = 'expr';        Script = 'cases\expr.sh';        Args = @() }   # expr arith/rel/string/match + quoted globs stay literal
    @{ Name = 'du';          Script = 'cases\du.sh';          Args = @() }   # du -sb / -b (apparent bytes)
    @{ Name = 'od';          Script = 'cases\od.sh';          Args = @() }   # od -c / -tx1 / -An
    @{ Name = 'sort';        Script = 'cases\sort.sh';        Args = @() }   # sort -n / -r / -u / -k -t
    @{ Name = 'redirect_ext';Script = 'cases\redirect_ext.sh';Args = @() }   # external-cmd >file/2>file/2>/dev/null (no-crash regression)
    @{ Name = 'split';       Script = 'cases\split.sh';       Args = @() }   # split -l / -b -> prefix+aa/ab/...
    @{ Name = 'find';        Script = 'cases\find.sh';        Args = @() }   # find -name/-type/-maxdepth (pre-order walk)
    @{ Name = 'ls';          Script = 'cases\ls.sh';          Args = @() }   # ls -a/-A/-r/-d (scoped: one-per-line, no -l)
    @{ Name = 'xargs';       Script = 'cases\xargs.sh';       Args = @() }   # xargs default/-n/-I (dispatches via _eval)
    @{ Name = 'diff';        Script = 'cases\diff.sh';        Args = @() }   # diff normal format (LCS) + -q
    @{ Name = 'hash';        Script = 'cases\hash.sh';        Args = @() }   # hash -r/-p/name (PATH cache)
    @{ Name = 'grep';        Script = 'cases\grep.sh';        Args = @() }   # grep -i/-v/-n/-c/-w/-F + regex + rc
    @{ Name = 'sed';         Script = 'cases\sed.sh';         Args = @() }   # sed s///g/i/N, d, p, addresses, BRE groups; + which
    @{ Name = 'claude_wrapper'; Script = 'cases\claude_wrapper.sh'; Args = @() }   # P1: Claude Code's exact per-command wrapper (MSYS paths, snapshot, eval, pwd >|)
    @{ Name = 'flags';       Script = 'cases\flags.sh';       Args = @() }   # P1: -c -l order, -lc, --version, -o, -e, stdin script, $-, exit codes
    @{ Name = 'shellbuiltins'; Script = 'cases\shellbuiltins.sh'; Args = @() } # P1: shopt/alias/command/type/builtin/declare/readonly/let/pushd/set/read/mapfile/getopts/printf/test/[[ ]]/fd3
    @{ Name = 'specialvars'; Script = 'cases\specialvars.sh'; Args = @() }   # P1: OSTYPE PPID RANDOM UID BASH_VERSINFO LINENO PIPESTATUS $! $- FUNCNAME BASH_SOURCE, $(...) status
    @{ Name = 'heredoc';     Script = 'cases\heredoc.sh';     Args = @() }   # P1: heredoc/herestring, &>, 2>&1 >f order, /tmp & /c/ paths, nested $( ), "..." escapes, ${x:n:m} etc.
    @{ Name = 'pipes2';      Script = 'cases\pipes2.sh';      Args = @() }   # P2: threaded pipelines over managed pipes; yes|head, big producer|head -1, ext|builtin|builtin, captures, pipefail
    @{ Name = 'awk';         Script = 'cases\awk.sh';         Args = @() }   # P4: in-process awk subset (decision 7) vs gawk; loud boundary
    @{ Name = 'procsub';     Script = 'cases\procsub.sh';     Args = @() }   # P4: <( ) temp-file emulation (decision 6), |&, trap ERR (+errtrace), timeout
    @{ Name = 'sysutils2';   Script = 'cases\sysutils2.sh';   Args = @() }   # P4: date -d/-u/-I/-R, uname family, id/whoami/nproc/printenv/tty/arch
    @{ Name = 'procs';       Script = 'cases\procs.sh';       Args = @() }   # P5 follow-up: in-process pgrep/pkill/ps against a cmd.exe+ping guinea pig
    @{ Name = 'parser2';     Script = 'cases\parser2.sh';     Args = @() }   # 2026-09-05 defect report: $( ) inside $(( )) keeps its text; backslash-newline is no word; quoted $(cmd arg); echo -e/printf
    @{ Name = 'binary';      Script = 'cases\binary.sh';      Args = @() }   # 2026-09-11: bytes >= 0x80 through `<` and `|` are byte-faithful for cat/head -c/tail -c/wc -c/cmp/od/tee/checksums/base64
    @{ Name = 'paths';       Script = 'cases\paths.sh';       Args = @() }   # 2026-09-11: pwd/PWD/mktemp/realpath return forward-slash Windows form; backslash still escapes in arguments
    @{ Name = 'bytes';       Script = 'cases\bytes.sh';       Args = @() }
    @{ Name = 'inherit_ext'; Script = 'cases\inherit_ext.sh'; Args = @() }
    @{ Name = 'crlf';        Script = 'cases\crlf.sh';        Args = @() }
    @{ Name = 'stdin_shared';Script = 'cases\stdin_shared.sh';Args = @() }
    @{ Name = 'pipeline_isolation'; Script = 'cases\pipeline_isolation.sh'; Args = @() }
    @{ Name = 'array_index_assign'; Script = 'cases\array_index_assign.sh'; Args = @() }
    @{ Name = 'cwd_isolation';      Script = 'cases\cwd_isolation.sh';      Args = @() }
    @{ Name = 'pattern_operands';   Script = 'cases\pattern_operands.sh';   Args = @() }
    @{ Name = 'cp_mtime';           Script = 'cases\cp_mtime.sh';           Args = @() }
    @{ Name = 'escaped_glob';       Script = 'cases\escaped_glob.sh';       Args = @() }
    @{ Name = 'time_keyword';       Script = 'cases\time_keyword.sh';       Args = @() }   # 2026-10-04: time [-p] pipeline + TIMEFORMAT (was "command not found") (expected = GNU via WSL, digits masked)
    @{ Name = 'function_shadows_builtin'; Script = 'cases\function_shadows_builtin.sh'; Args = @() }   # 2026-10-04 (nupkg-28): a function named after a builtin utility shadows it (builtin ran instead) (expected = GNU via WSL)
    @{ Name = 'subshell_functions'; Script = 'cases\subshell_functions.sh'; Args = @() }   # 2026-10-04 (nupkg-28): functions defined in ( ) / $( ) / <( ) no longer leak to the parent (expected = GNU via WSL)
    @{ Name = 'command_word_split'; Script = 'cases\command_word_split.sh'; Args = @() }   # 2026-10-04: "$@" / $cmd in command position is field-split (was one word since <= rev 80) (expected = GNU via WSL)
    @{ Name = 'external_redirects'; Script = 'cases\external_redirects.sh'; Args = @() }   # 2026-10-04: the redirect rewrite -- real file handles, left-to-right dup2 order, newest stdout wins, read-ahead kept, script redirects (expected = GNU via WSL)
    @{ Name = 'non_ascii_output';   Script = 'cases\non_ascii_output.sh';   Args = @() }   # 2026-10-05: builtins' non-ASCII text is UTF-8 even with no console (was the ANSI code page under Claude Code) (expected = GNU via WSL)
    @{ Name = 'jq_subset';          Script = 'cases\jq_subset.sh';          Args = @() }   # 2026-10-05: the in-process jq subset, evidence cases + the language in scope (expected = jq 1.6 via WSL)
    @{ Name = 'jq_messages';        Script = 'cases\jq_messages.sh';        Args = @() }   # 2026-10-05: jq's error, parse-error and compile-error messages and positions (expected = jq 1.6 via WSL)
    @{ Name = 'regex_k_diff_stdin'; Script = 'cases\regex_k_diff_stdin.sh'; Args = @() }   # 2026-10-05: BRE mid-pattern $/^ and leading * are literals; grep -P \K; diff - reads stdin (expected = GNU via WSL)
    @{ Name = 'background_isolation'; Script = 'cases\background_isolation.sh'; Args = @() }   # 2026-10-05: a `&` job runs on its own copy of the shell taken at `&`; kill %n ends its loop; job numbers restart (expected = GNU via WSL)
    @{ Name = 'detach_builtins';    Script = 'cases\detach_builtins.sh';    Args = @() }   # 2026-10-04: nohup / setsid / disown (phase 2, option b) (expected = GNU via WSL)
    @{ Name = 'df';                 Script = 'cases\df.sh';                 Args = @() }   # 2026-10-08: df over Windows volumes in GNU's format; shape only here, numbers and headers by tests/df/compare-wsl.sh (vs GNU via WSL)
    @{ Name = 'literal_no_split';   Script = 'cases\literal_no_split.sh';   Args = @() }   # 2026-10-08: literal text is never field-split (a\ b/x* was two words; IFS chars; a tilde home with a space) (expected = GNU via WSL)
    @{ Name = 'declaration_assign'; Script = 'cases\declaration_assign.sh'; Args = @() }   # 2026-10-08: local/declare/typeset/export/readonly name=$v is one field (local a=$1 split "x y") (expected = GNU via WSL)
    @{ Name = 'operator_word';      Script = 'cases\operator_word.sh';      Args = @() }   # 2026-10-08: ${x:-word}/${x:+word} word expanded in the ${ }'s own context ("${v:+$v }" lost its space; ${v:+-o "$w"} split "$w") (expected = GNU via WSL)
    @{ Name = 'assignment_tilde';   Script = 'cases\assignment_tilde.sh';   Args = @() }   # 2026-10-08: x=~/y, PATH=$PATH:~/bin, local/export/a[0]= values expand ~ after = and : (was never expanded) (expected = GNU via WSL)
    @{ Name = 'declared_unset';     Script = 'cases\declared_unset.sh';     Args = @() }   # 2026-10-08: local x / export x / declare -a with no value leave x UNSET (local x stored ""; export put x="" in children) (expected = GNU via WSL)
    @{ Name = 'nounset';            Script = 'cases\nounset.sh';            Args = @() }   # 2026-10-08: set -u is enforced (was accepted, never applied); exits: -c 127, script 1, ( ) / $( ) / pipe stage / heredoc fail only themselves; ${x:-word} word expanded only when selected (expected = GNU via WSL)
    @{ Name = 'shell_paths';        Script = 'cases\shell_paths.sh';        Args = @() }   # 2026-10-08: $HOME, ~, $BASH and type/command -v/which/hash output are forward slash (were backslash) (authored: Windows-specific)
    @{ Name = 'env_options';        Script = 'cases\env_options.sh';        Args = @() }   # 2026-10-08: env NAME=VALUE with any name (STDOUT.OutputLevel=trace), -u / -i reach builtins and programs, -uNAME --unset= - -0 (expected = GNU via WSL)
    @{ Name = 'backslash_pattern';  Script = 'cases\backslash_pattern.sh';  Args = @() }   # 2026-10-08: unquoted \\ in case / [[ == ]] / =~ patterns is a literal backslash (*\\* matched a*); other \\ uses unchanged (expected = GNU via WSL)
    @{ Name = 'msys_redirects';     Script = 'cases\msys_redirects.sh';     Args = @() }   # 2026-10-08: MSYS programs (Git usr/bin) under >> and the shell's own redirects wrote nothing (append-only handle); needs Git (authored)
    @{ Name = 'command_lookup';     Script = 'cases\command_lookup.sh';     Args = @() }   # 2026-10-08: PATH lookups print the on-disk name (cmd.exe, not cmd.EXE); hash: hits table, -t -l -d -p (authored: Windows paths)
    @{ Name = 'stray_keyword';      Script = 'cases\stray_keyword.sh';      Args = @() }   # 2026-10-08: fi/done/esac/then/do/else/elif with no construct open is a syntax error, status 2 (ran as a command, 127) (expected = GNU via WSL)
    @{ Name = 'date_nanos';         Script = 'cases\date_nanos.sh';         Args = @() }   # 2026-10-08: %N width is a precision (%3N printed 9 digits), - and _ flags; -d @epoch.fraction (expected = GNU date via WSL)
    @{ Name = 'fold_tee';           Script = 'cases\fold_tee.sh';           Args = @() }   # 2026-10-08: fold columns as GNU (\b \r tab, bytes, -s, no added newline); tee /dev/null /dev/stdout /dev/stderr (expected = GNU via WSL)
    @{ Name = 'exit_status_range';  Script = 'cases\exit_status_range.sh';  Args = @() }   # 2026-10-08: a Windows exit code becomes 0-255 as on Linux (low byte; NTSTATUS crash = 128+signal); $? was the raw code (authored)
    @{ Name = 'nested_brace';       Script = 'cases\nested_brace.sh';       Args = @() }   # 2026-10-08: unquoted ${a#${b}} / ${1:-${HOME}} swallowed the rest of the file silently (nested ${ counted twice); unclosed ${ is an error (expected = GNU via WSL)
    @{ Name = 'brace_constructs';   Script = 'cases\brace_constructs.sh';   Args = @() }   # 2026-10-10: one matcher (Lexer.ConstructEnd) for ${ } $( ) $(( )) `` quotes, in every context; { } as words after the command name (expected = GNU via WSL)
    @{ Name = 'call_stack';       Script = 'cases\call_stack.sh';         Args = @() }   # 2026-10-10: FUNCNAME / BASH_SOURCE / BASH_LINENO as one frame stack (source kept the script; a function's own file; main; -c environment) (expected = GNU via WSL)
    @{ Name = 'error_prefix';       Script = 'cases\error_prefix.sh';       Args = @() }   # 2026-10-10: errors carry bash's 'name: line N: ' prefix (BASH_SOURCE[0] or $0; eval / -c input names); eval and $( ) bodies continue the caller's line numbers (expected = GNU via WSL)
    @{ Name = 'ls_operands';        Script = 'cases\ls_operands.sh';        Args = @() }   # 2026-10-10: ls headings when more than one operand (a failing one counts); directory operands in sort order (expected = GNU via WSL)
    @{ Name = 'trap_signals';       Script = 'cases\trap_signals.sh';       Args = @() }   # 2026-10-10: trap accepts bash's Linux signal specs only (BADSIG, 65 were accepted), sets the valid ones past a bad one; trap -p order/spelling/operands (expected = GNU via WSL)
    @{ Name = 'builtin_options';    Script = 'cases\builtin_options.sh';    Args = @() }   # 2026-10-10: builtins reject invalid options as bash does (message + usage line, 2), options end at the first operand; trap -- / kill -Z / source -- (expected = GNU via WSL)
    @{ Name = 'syntax_errors';      Script = 'cases\syntax_errors.sh';      Args = @() }   # 2026-10-10: syntax errors in bash's words and lines; empty bodies, a && at the end, f() echo x, [[ -f ]] rejected; heredoc EOF warning (expected = GNU via WSL)
    @{ Name = 'stdin_sharing';      Script = 'cases\stdin_sharing.sh';      Args = @() }   # 2026-10-10: a program run while the shell reads a file shares the offset (while read; do prog; done < f ended after one line); a child's read-ahead is given back (expected = GNU via WSL)
    @{ Name = 'utf8_boundary';      Script = 'cases\utf8_boundary.sh';      Args = @() }   # 2026-10-10: a UTF-8 char cut by a 4096-byte read block or split pipe writes is one char (read, pipe, mapfile were two raw bytes); invalid bytes stay raw (expected = GNU via WSL)
    @{ Name = 'device_files';       Script = 'cases\device_files.sh';       Args = @() }   # 2026-10-10: /dev/null zero random urandom stdin stdout stderr fd/N as operands, redirects and under test; tr streams; od offset for empty input (expected = GNU via WSL)
    @{ Name = 'fd_copy';            Script = 'cases\fd_copy.sh';            Args = @() }   # 2026-10-10: exec 3>&1 / 4>&2 / 3<&0 copy a standard fd (>&3 was Bad file descriptor); closing a copy keeps the other (expected = GNU via WSL)
    @{ Name = 'wait_next';          Script = 'cases\wait_next.sh';          Args = @() }   # 2026-10-10: wait -n waits for the next job and returns its status (127 with none); wait -n ID; wait -p VAR (were ignored) (expected = GNU via WSL)
    @{ Name = 'jobs_list';          Script = 'cases\jobs_list.sh';          Args = @() }   # 2026-10-10: jobs in bash's format with -l -p -r -s and job specs; a pipeline stage lists the parent's jobs (expected = GNU via WSL)
    @{ Name = 'native_args_msys';   Script = 'cases\native_args_msys.sh';   Args = @() }   # 2026-10-20: from native_args: an MSYS program gets /c/x unconverted (SKIP without an MSYS echo.exe on PATH)
    @{ Name = 'native_args_git';    Script = 'cases\native_args_git.sh';    Args = @() }   # 2026-10-20: from native_args: git -C /c/x reaches git as C:/x (SKIP without git on PATH)
    @{ Name = 'native_args';        Script = 'cases\native_args.sh';        Args = @() }   # 2026-10-10: a Windows program gets /c/x and /tmp/x at the start of an argument as C:/x and the temp dir (as Git Bash); MSYS programs and the off switches untouched (authored: Windows-specific)
    @{ Name = 'python_utf8';        Script = 'cases\python_utf8.sh';        Args = @() }   # 2026-10-10: PYTHONIOENCODING=utf-8 exported unless set (piped py crashed on non-cp1252 text); not HGENCODING (authored: Windows-specific)
    @{ Name = 'utf8_bytes';         Script = 'cases\utf8_bytes.sh';         Args = @() }   # 2026-10-10: carried bytes spelling a UTF-8 char are that char ($'\xc3\xa9' is e-acute: length, ==, case, file names, ${x^^}) (expected = GNU via WSL)
    @{ Name = 'awk_extended';       Script = 'cases\awk_extended.sh';       Args = @() }   # 2026-10-10: awk user functions, print > / >> file, getline [var] < file, close, fflush, 3-arg match, --version; BASH_COREUTILS=builtin (expected = gawk 5.1 via WSL)
    @{ Name = 'awk_escapes';        Script = 'cases\awk_escapes.sh';        Args = @() }   # 2026-10-10: awk "\xNN" / "\NNN" above 0x7F are bytes; bytes spelling a UTF-8 char are that char, across concatenation too (expected = gawk 5.1 via WSL)
    @{ Name = 'jq_raw_input';       Script = 'cases\jq_raw_input.sh';       Args = @() }   # 2026-10-10: jq -R (each line a string) and -Rs (whole input one string); inputs are one text; errors at file:line (expected = jq 1.6 via WSL)
    @{ Name = 'one_at_a_time';      Script = 'cases\one_at_a_time.sh';      Args = @() }   # 2026-10-11: scripts, -c, source, eval run one top-level command (one line) at a time: commands before a syntax error run; nothing after exit is parsed (expected = GNU via WSL)
    @{ Name = 'substitution_errors'; Script = 'cases\substitution_errors.sh'; Args = @() }   # 2026-10-11: a syntax error in $( ) / backquotes is met when it runs: $( ) abandons the top-level command (1), backquotes fail (2); subshell boundaries; -n silent (expected = GNU via WSL)
    @{ Name = 'path_lookup';        Script = 'cases\path_lookup.sh';        Args = @() }   # 2026-10-11: lookup follows the shell's PATH: assignment resets the hash, a temporary PATH= is searched, a subshell's PATH stays in it (authored)
    @{ Name = 'path_script';        Script = 'cases\path_script.sh';        Args = @() }   # 2026-10-17: a #! or .sh script found on PATH runs (it was "Permission denied"), $0 the path found; a match that cannot run is passed over, or named when it is the only one; a bare name never runs a script from the cwd (a planted git ran) (expected = GNU via WSL)
    @{ Name = 'pathext_programs';   Script = 'cases\pathext_programs.sh';   Args = @() }   # 2026-10-18: only .com/.exe/.bat/.cmd (and .sh, #!) are programs: a zz.js before zz.exe is passed over, -x of a .js is false, a .js with #! is a script (authored: Windows-specific)
    @{ Name = 'icu_lazy';           Script = 'cases\icu_lazy.sh';           Args = @(); AotOnly = $true }   # 2026-10-18 (review R4): icu.dll loads at the first culture-sensitive operation, not on every start (StartsWith, integer parses, ToLowerInvariant, the SID text, a file's local time); controls ${x^^} and sort load it (authored; Native AOT only: the JIT runtime loads ICU itself)
    @{ Name = 'awk_fallthrough';    Script = 'cases\awk_fallthrough.sh';    Args = @() }   # 2026-10-20: from awk_unsupported_once and awk_getline: the PATH awk runs a refused program once, and keeps CR (SKIP without a PATH awk)
    @{ Name = 'awk_unsupported_once'; Script = 'cases\awk_unsupported_once.sh'; Args = @() }   # 2026-10-18: a construct outside the in-process awk visible in the text (literal %e/%g, RS, system()) falls through before running; one met while running fails loud (2): the PATH awk ran the program again ("first" twice); +inf/-nan as gawk 5.1 (authored: needs Git's awk; the number lines are gawk's)
    @{ Name = 'printf_nonfinite';   Script = 'cases\printf_nonfinite.sh';   Args = @() }   # 2026-10-18: printf inf/-inf/nan/-nan as C (INF/NAN for capitals, sign flags, spaces not zeros), the words read in any case; %g of an infinity threw (expected = GNU via WSL)
    @{ Name = 'startup_env';        Script = 'cases\startup_env.sh';        Args = @() }   # 2026-10-18: a Git on PATH adds its usr/bin and mingw64/bin: a mingw64/bin entry names Git's root (it was never reached), an entry with / is not added twice; a child's PPID is $$ (authored)
    @{ Name = 'long_paths';         Script = 'cases\long_paths.sh';         Args = @() }   # 2026-10-17: cd into a directory over 260 characters (longPathAware manifest); a program from there, or with a long path of its own: the Windows limit said, 126 (authored: Windows-specific)
    @{ Name = 'xtrace_compound';    Script = 'cases\xtrace_compound.sh';    Args = @() }   # 2026-10-17: set -x traces [[ ]] tests (operands expanded, -n for a lone word, ! kept, quoted pattern chars backslashed), (( )), for heads per turn, arithmetic-for parts, case heads (expected = GNU via WSL)
    @{ Name = 'xtrace_fidelity';    Script = 'cases\xtrace_fidelity.sh';    Args = @() }   # 2026-10-17: set -x as bash: assignments traced, `++` levels per command substitution, words quoted, PS4 prompt escapes decoded (expected = GNU via WSL)
    @{ Name = 'msys_inherited_append'; Script = 'cases\msys_inherited_append.sh'; Args = @() }   # 2026-10-17: an MSYS program inside `Bash.exe s.sh >> out` writes (the inherited append-only handle was unwritable to MSYS: Bad file descriptor), 2>&1 sharing one handle (authored: needs Git)
    @{ Name = 'lookup_env';         Script = 'cases\lookup_env.sh';         Args = @() }   # 2026-10-17 (red team): a ~ PATH entry is $HOME's (a cwd copy ran); unset/export -n vars do not reach a child; a missing interpreter: bad interpreter 126 / env's 127 (expected = GNU via WSL)
    @{ Name = 'lookup_env_windows'; Script = 'cases\lookup_env_windows.sh'; Args = @() }   # 2026-10-17 (red team): /f/x PATH entries; \x and C:x in a moved stage; unset PATH gives a child none; an exported `path` does not replace PATH; cmd.exe by a full / path (authored: Windows-specific)
    @{ Name = 'gnu_differences';    Script = 'cases\gnu_differences.sh';    Args = @() }   # 2026-10-17: source PATH-first; hash/type -a refuse what cannot run, type -a lists all; mktemp in cwd; readlink -f form; Not a directory / Is a directory; s1. finds nothing; find -execdir; exec 5<> + read -u; hash of a relative entry's match (crashed) (expected = GNU via WSL)
    @{ Name = 'subshell_exec_redirect'; Script = 'cases\subshell_exec_redirect.sh'; Args = @() }   # 2026-10-17: a subshell's / $( )'s / stage's `exec > f` ends with it (it took every later line); its EXIT trap still writes inside it; a { } group's stays (expected = GNU via WSL)
    @{ Name = 'launcher_fallback';  Script = 'cases\launcher_fallback.sh';  Args = @() }   # 2026-10-17: a bare name not on PATH: C#Bash's folder, System32, Windows folder only, else 127; Windows' launcher ran a .exe planted in the cwd (ordinary terminal); `cmd` on an empty PATH still runs (authored: Windows-specific)
    @{ Name = 'path_empty_entries'; Script = 'cases\path_empty_entries.sh'; Args = @() }   # 2026-10-17: an empty PATH entry never reaches Windows (own process PATH at startup, a child's PATH): a planted .exe in the cwd ran under NoDefaultCurrentDirectoryInExePath=1; $PATH stays as written (authored: Windows-specific)
    @{ Name = 'background_cwd';     Script = 'cases\background_cwd.sh';     Args = @() }   # 2026-10-17: a & job works in its own directory after the parent cd's (rm, >, glob, test -f, pipeline, ./script, its own cd); it used the parent's: rm -f victim deleted the parent's victim (expected = GNU via WSL)
    @{ Name = 'ps4_epochrealtime';  Script = 'cases\ps4_epochrealtime.sh';  Args = @() }   # 2026-10-17: PS4 is expanded per trace line (parameters, arithmetic, $( ), quotes literal, $? kept); it printed raw. $EPOCHREALTIME has microseconds (it moved in ms steps) (expected = GNU via WSL)
    @{ Name = 'relative_path_launch'; Script = 'cases\relative_path_launch.sh'; Args = @() }   # 2026-10-17: a name with a slash is a path from the shell's cwd, never searched for: under NoDefaultCurrentDirectoryInExePath=1 `sub/w.exe` was "command not found", and a sub/v.exe under a PATH dir ran; a missing path is "No such file or directory" (authored: a Windows program)
    @{ Name = 'interpreter_loop';   Script = 'cases\interpreter_loop.sh';   Args = @() }   # 2026-10-17: a #!/usr/bin/env chain is capped ("bad interpreter", 126); `#!/usr/bin/env self` overflowed the stack and killed the shell (authored: GNU execs forever)
    @{ Name = 'command_map';        Script = 'cases\command_map.sh';        Args = @() }   # 2026-10-16: lookups kept across processes per PATH; misses recorded and obeyed by `hash name` alone (an earlier process's, said so), everything else searches; hash -r/-d forget, no map for a relative PATH, concurrent writers; BASH_PATH taken once and removed (authored: C#Bash-specific)
    @{ Name = 'array_element_append'; Script = 'cases\array_element_append.sh'; Args = @() }   # 2026-10-16: a[k]+=v appends to the element (indexed, assoc, declare -i adds, scalar is element 0, declare a[0]+=v); it ran as a command (expected = GNU via WSL)
    @{ Name = 'case_in_substitution'; Script = 'cases\case_in_substitution.sh'; Args = @() }   # 2026-10-12: a case's pattern `)` does not close `$( )`, "$( )" or backquotes (GNU bash)
    @{ Name = 'signal_list';        Script = 'cases\signal_list.sh';        Args = @() }   # 2026-10-12: kill -l / trap -l print bash's numbered table; kill's options as kill.def; SIG only before a signal name (GNU bash)
    @{ Name = 'dir_stack';          Script = 'cases\dir_stack.sh';          Args = @() }   # 2026-10-12: pushd/popd/dirs +N/-N, -n = no cd, dirs -p/-l, ~ at a / boundary (GNU bash)
    @{ Name = 'jobs_x';             Script = 'cases\jobs_x.sh';             Args = @() }   # 2026-10-12: jobs -x replaces jobspecs ($$ in a script, the job pid interactive) and runs the command (GNU bash)
    @{ Name = 'jobspec_names';      Script = 'cases\jobspec_names.sh';      Args = @() }   # 2026-10-12: %name, %?text, bare %, "ambiguous job spec" in jobs/kill/disown/jobs -x (GNU bash)
    @{ Name = 'and_or_background'; Script = 'cases\and_or_background.sh'; Args = @() }   # 2026-10-12: a && b & backgrounds the whole and-or list; its cd and assignments stay in the job (GNU bash)
    @{ Name = 'ifs_splitting';      Script = 'cases\ifs_splitting.sh';      Args = @() }   # 2026-10-12: IFS='' does not split; non-white IFS makes empty fields; $* joins with IFS[0] (GNU bash)
    @{ Name = 'case_fallthrough';   Script = 'cases\case_fallthrough.sh';   Args = @() }   # 2026-10-12: case ;& and ;;& (were syntax errors) (GNU bash)
    @{ Name = 'command_printing';   Script = 'cases\command_printing.sh';   Args = @() }   # 2026-10-12: declare -f / type print as bash's print_cmd.c (53 functions; eval round trip) (GNU bash)
    @{ Name = 'jobs_text';          Script = 'cases\jobs_text.sh';          Args = @() }   # 2026-10-12: jobs shows compound jobs as bash prints them (GNU bash)
    @{ Name = 'declaration_arrays'; Script = 'cases\declaration_arrays.sh'; Args = @() }   # 2026-10-12: local p=($(cmd)); export/readonly arrays; quoted (..) is a string; ${#scalar[@]} is 1 (GNU bash)
    @{ Name = 'diff_label';         Script = 'cases\diff_label.sh';         Args = @() }   # 2026-10-12 (adc-framework-d8): diff -u --label (GNU diff)
    @{ Name = 'variable_scope';     Script = 'cases\variable_scope.sh';     Args = @() }   # 2026-10-12: bash's dynamic scoping: globals from functions, local arrays and attributes, unset (GNU bash)
    @{ Name = 'awk_getline';        Script = 'cases\awk_getline.sh';        Args = @() }   # 2026-10-12 (msp430-a2): main-input getline in-process; BINMODE=3 on fall-through, CR kept (GNU awk)
    @{ Name = 'msys_arguments';     Script = 'cases\msys_arguments.sh';     Args = @() }   # 2026-10-12 (msp430-a2): arguments reach MSYS programs intact (braces, globs, quotes, backslashes) (GNU bash)
    @{ Name = 'sed_final_newline';  Script = 'cases\sed_final_newline.sh';  Args = @() }   # 2026-10-12: a last line without a newline stays without one; D does not autoprint (N;P;D doubled lines) (GNU sed)
    @{ Name = 'dev_tty_git_prompt'; Script = 'cases\dev_tty_git_prompt.sh'; Args = @() }   # 2026-10-20: from dev_tty_windowless: git's credential prompt fails at once with no window (SKIP without git on PATH)
    @{ Name = 'dev_tty_windowless'; Script = 'cases\dev_tty_windowless.sh'; Args = @() }   # 2026-10-15: /dev/tty in a console with no window (Claude Code's) is "No such device or address"; git's credential prompt hung (authored; GNU's messages)
    @{ Name = 'compgen';            Script = 'cases\compgen.sh';            Args = @() }   # 2026-10-15 (daq-d1): compgen as bash 5.1: -W -X -P -S -G -f -d, the name lists, jobs, order, status (was not a builtin) (GNU bash)
    @{ Name = 'compgen_local';      Script = 'cases\compgen_local.sh';      Args = @() }   # 2026-10-15: compgen describes THIS shell: -b every C#Bash builtin, -c no PATH scan (deferred), the notices (authored)
    @{ Name = 'strings_gnu';        Script = 'cases\strings_gnu.sh';        Args = @() }   # 2026-10-14: strings as GNU strings 2.38: -n/-N, -t/-o, -f, -w, -s, -e s|S|b|l|B|L, stdin, errors (was not a command) (GNU strings)
    @{ Name = 'strings_local';      Script = 'cases\strings_local.sh';      Args = @() }   # 2026-10-14: strings' own text: --version not GNU, usage errors, the option policy, the -d/-T/-U/@file notice (authored)
    @{ Name = 'rm_interactive';     Script = 'cases\rm_interactive.sh';     Args = @() }   # 2026-10-12 (adc-framework-d8): write-protected files removed when stdin is not a terminal; -i/-I ask; last of -f/-i/-I wins; a directory link removed, never descended into (GNU rm)
    @{ Name = 'arg0_path';          Script = 'cases\arg0_path.sh';          Args = @() }   # 2026-10-11: $0 (argv[0] as passed) in the shell's forward-slash form; a PATHEXT match's on-disk name as a child's argv[0] (authored: Windows-specific)
    @{ Name = 'traps_debug_return'; Script = 'cases\traps_debug_return.sh'; Args = @() }   # 2026-10-11: DEBUG/RETURN traps run (were silent), $BASH_COMMAND exists, -T functrace, trap $LINENO (expected = GNU via WSL)
    @{ Name = 'options_tkp';        Script = 'cases\options_tkp.sh';        Args = @() }   # 2026-10-11: -t (onecmd), -k (keyword), -p (privileged: no BASH_ENV) act as in bash (were ignored); $- order with s last (expected = GNU via WSL)
    @{ Name = 'coreutils_external'; Script = 'cases\coreutils_external.sh'; Args = @() }   # 2026-10-11: BASH_COREUTILS=external runs the PATH program (it recursed builtin -> RunCommand -> builtin until a stack overflow killed the shell); needs Git (authored)
    @{ Name = 'restricted_mode';    Script = 'cases\restricted_mode.sh';    Args = @() }   # 2026-10-11: -r / --restricted / -cr refuse to start, set -r ends the shell (2), with a prefilled-issue link carrying no command text (authored: no GNU oracle)
    @{ Name = 'umask_times_fc';     Script = 'cases\umask_times_fc.sh';     Args = @() }   # 2026-10-10: umask (octal, symbolic, -p, -S, per shell), times, fc (option check, then not supported) are builtins; fc ran fc.exe (expected = GNU via WSL)
    @{ Name = 'bat_args';           Script = 'cases\bat_args.sh';           Args = @() }   # 2026-10-19 (red team F4): a .bat/.cmd argument is data: cmd re-parsed the command line, so `"&echo INJECTED` ran and %OS% expanded (BatBadBut); quoted as Rust's std does, a line break refused (authored: GNU runs no batch files)
    @{ Name = 'tilde_literal';      Script = 'cases\tilde_literal.sh';      Args = @() }   # 2026-10-19 (red team F5): a quoted or data `~` is a name; it was the Windows profile whatever $HOME said (mkdir "~" said File exists, writes landed in the profile); an unquoted ~ is $HOME (expected = GNU via WSL)
    @{ Name = 'subshell_fd_close';  Script = 'cases\subshell_fd_close.sh';  Args = @() }   # 2026-10-19 (red team F1): a descriptor closed in ( ), $( ), a pipeline stage, a job or a piped { } stays open in the parent (its stream was disposed: "Bad file descriptor", lines lost) (expected = GNU via WSL)
    @{ Name = 'read_fd_offset';     Script = 'cases\read_fd_offset.sh';     Args = @() }   # 2026-10-19 (red team F2): after `read -u N` the descriptor's next reader or writer starts just after the line (cat <&8 printed nothing, head took the rest, echo >&8 wrote at the end); a pipe's readers share one buffer (expected = GNU via WSL)
    @{ Name = 'append_concurrent';  Script = 'cases\append_concurrent.sh';  Args = @() }   # 2026-10-19 (red team, older): two shells appending to one file keep every line (O_APPEND: an append-only handle); C#Bash's own >> wrote at its own offset, 3000 of 6000 lines left (expected = GNU via WSL)
    @{ Name = 'msys_append_shared'; Script = 'cases\msys_append_shared.sh'; Args = @() }   # 2026-10-19 (red team F3): an MSYS program's `>> o 2>> o` (direct, in a group, or a nested shell's) keeps both lines whole: one write handle per file (two at the same end overwrote); needs MSYS2 at F:/Tools/msys64 (authored)
    @{ Name = 'fallback_not_windows'; Script = 'cases\fallback_not_windows.sh'; Args = @() }   # 2026-10-19 (red team): a builtin's unsupported option hands over to the PATH program of that name, never Windows' own (System32's find.exe ran: FIND: Parameter format not correct) (authored)
    @{ Name = 'readlink_canon';     Script = 'cases\readlink_canon.sh';     Args = @() }   # 2026-10-19 (red team F6): realpath and readlink -f/-e/-m check each component as GNU does and drop a trailing slash (nothing was checked: readlink -f nodir/x printed a path, rc 0) (expected = GNU coreutils via WSL)
    @{ Name = 'find_execdir';       Script = 'cases\find_execdir.sh';       Args = @() }   # 2026-10-19 (red team F7): -execdir takes a starting point as written (. ran in the parent, proj/ inside proj) and refuses a PATH with the current directory or a relative entry, as GNU (a planted program ran) (expected = GNU findutils via WSL)
    @{ Name = 'which_program';      Script = 'cases\which_program.sh';      Args = @() }   # 2026-10-19 (red team F8): which reports only what can run (a .js with no #! and a .txt were reported) (expected = GNU which via WSL)
    @{ Name = 'shebang_options';    Script = 'cases\shebang_options.sh';    Args = @() }   # 2026-10-19 (red team F9, and beside it): a script run as a command takes its #! options (#!/bin/bash -e went on past a failure) and env -S (127) (expected = GNU via WSL)
    @{ Name = 'cwd_limit';          Script = 'cases\cwd_limit.sh';          Args = @() }   # 2026-10-19 (red team F10): a program started from a directory of 259 characters or more: the stated Windows limit, 126 (at exactly 259 Windows' own text with 127); 258 runs (authored)
    @{ Name = 'source_devices';     Script = 'cases\source_devices.sh';     Args = @() }   # 2026-10-19 (red team, older): source reads /dev/null, /dev/stdin (here-string, pipe) and /dev/fd/N as bash does (No such file or directory, 1) (expected = GNU via WSL)
    @{ Name = 'export_function';    Script = 'cases\export_function.sh';    Args = @() }   # 2026-10-19 (red team, older): export -f / declare -fx reach a child as BASH_FUNC_name%% and are imported; a forged definition never runs (export -f did nothing) (expected = GNU via WSL)
    @{ Name = 'arith_bad_value';    Script = 'cases\arith_bad_value.sh';    Args = @() }   # 2026-10-19 (red team, older): a variable whose value is no valid arithmetic expression is an error, not 0; self-reference is the recursion error (cut off as 0 at depth 8) (expected = GNU via WSL)
    @{ Name = 'path_through_file';  Script = 'cases\path_through_file.sh';  Args = @() }   # 2026-10-19 (red team F13): a path through a file is Not a directory for a command (hostname.exe/. ran), cat, ls and a redirect (expected = GNU via WSL)
    @{ Name = 'mktemp_suffix';      Script = 'cases\mktemp_suffix.sh';      Args = @() }   # 2026-10-19 (red team F16): a mktemp template not ending in X has an implied suffix (aXXXXb was the literal name); too few X's and --suffix errors as GNU (expected = GNU coreutils via WSL)
    @{ Name = 'xtrace_details';     Script = 'cases\xtrace_details.sh';     Args = @() }   # 2026-10-19 (red team F11): set -x as bash: octal control characters (and printf %q), export/readonly assignments, a subscript as written, eval one level deeper, BASH_XTRACEFD (expected = GNU via WSL)
    @{ Name = 'sed_brackets_text';  Script = 'cases\sed_brackets_text.sh';  Args = @() }   # 2026-10-19 (found beside red team F11): in sed only a regex has bracket expressions; a `[` in the replacement or y swallowed the delimiter (expected = GNU sed via WSL)
    @{ Name = 'xtrace_expanded';    Script = 'cases\xtrace_expanded.sh';    Args = @() }   # 2026-10-19 (red team F12): set -x shows (( )) and arithmetic for expanded (once), an empty for-part as (( 1 )), for i as for i in "$@", an empty [[ ]] operand as '', a tilde pattern quoted (and matched literally) (expected = GNU via WSL)
    @{ Name = 'tail_head_large';    Script = 'cases\tail_head_large.sh';    Args = @() }   # 2026-10-27 (koliada-net-28): tail and head -c/-n -N on a sparse file over 2 GiB (whole-file reads failed: "Stream was too long"); a file read from its end (expected = GNU via WSL; SKIP without fsutil or truncate)
    @{ Name = 'tail_head_exact';    Script = 'cases\tail_head_exact.sh';    Args = @() }   # 2026-10-27: tail and head's from-end forms byte for byte, file and pipe: no newline added to a last line, -n 0, +N, -z, CR, across 64 KiB blocks, several files, UTF-8 in a capture (expected = GNU via WSL)
    @{ Name = 'help_builtin';       Script = 'cases\help_builtin.sh';       Args = @() }   # 2026-10-27 (koliada-net-28; the architect): help and X --help answer with the manual page(s) from %USERPROFILE%/.bash/help.json (was Windows' help.exe / the PATH program's help); not implemented said; enable (authored)
    @{ Name = 'unserviced_log';     Script = 'cases\unserviced_log.sh';     Args = @() }   # 2026-10-27 (the architect): requests not serviced go to %USERPROFILE%/.bash/unserviced.jsonl from each place that gives up; BASH_UNSERVICED_LOG (authored)
    @{ Name = 'wc_checksum_large';  Script = 'cases\wc_checksum_large.sh';  Args = @() }   # 2026-10-27 (koliada-net-28): wc and md5sum on a sparse file over 2 GiB, file and pipe ("Stream was too long"); counted and hashed as read (expected = GNU via WSL; SKIP without fsutil or truncate)
    @{ Name = 'wc_checksum_exact';  Script = 'cases\wc_checksum_exact.sh';  Args = @() }   # 2026-10-27: wc and the four checksums unchanged where a word, a 2-/3-byte character or a CRLF straddles a 64 KiB block, file and pipe; -c, --tag, stdin (expected = GNU via WSL; fails with a non-carrying decoder, checked)
    @{ Name = 'bytes_tools_large';  Script = 'cases\bytes_tools_large.sh';  Args = @() }   # 2026-10-27: cmp, od, hexdump, base64 past 2 GiB on sparse files (whole-input reads failed); the shared byte layer (expected = GNU via WSL; SKIP without fsutil or truncate)
    @{ Name = 'bytes_tools_exact';  Script = 'cases\bytes_tools_exact.sh';  Args = @() }   # 2026-10-27: cmp/od/hexdump/base64 across 64 KiB edges and skips; rev 397's overflow (cmp -i, hexdump -s without -n) fixed; base64 -d's partial output (expected = GNU via WSL)
    @{ Name = 'split_tac_exact';    Script = 'cases\split_tac_exact.sh';    Args = @() }   # 2026-10-27: split writes pieces as it reads; tac reads from the end (a pipe spooled), a last line without a newline kept so (was b\na\n) (expected = GNU via WSL)
    @{ Name = 'head_lines_exact';   Script = 'cases\head_lines_exact.sh';   Args = @() }   # 2026-10-27: head -n N keeps a last line's missing newline missing (one was added), still through the shared line reader (expected = GNU via WSL)
    @{ Name = 'tee_exact';          Script = 'cases\tee_exact.sh';          Args = @() }   # 2026-10-27: tee copies exactly: a here-doc's surrogate pair cut by a 4096-char chunk became two U+FFFD in files and stdout; pipes through a pooled block, one carrying decoder for text targets (expected = GNU via WSL)
    @{ Name = 'tail_follow_exact';  Script = 'cases\tail_follow_exact.sh';  Args = @() }   # 2026-10-27: tail -f writes each appended byte once (the next round began at the length seen before the read: a busy writer's lines came out twice) (expected = GNU via WSL)
    @{ Name = 'paste_stream';       Script = 'cases\paste_stream.sh';       Args = @() }   # 2026-10-27: paste a row at a time; `paste - -` reads stdin in turn (the first - took it all); -d '\0' / '' empty; -z (expected = GNU via WSL)
    @{ Name = 'comm_stream';        Script = 'cases\comm_stream.sh';        Args = @() }   # 2026-10-27: comm merges a line at a time; --total, -z, the order check (default/--check-order/--nocheck-order), empty delimiter = NUL, file name in open errors (each accepted and ignored before) (expected = GNU via WSL)
    @{ Name = 'uniq_stream';        Script = 'cases\uniq_stream.sh';        Args = @() }   # 2026-10-27: uniq a group at a time; coreutils' -d/-u/-D switches (-d -u printed groups; -D went to the PATH uniq); -D -c refused; -z; -f blanks are space/tab (expected = GNU via WSL)
    @{ Name = 'sed_stream';         Script = 'cases\sed_stream.sh';         Args = @() }   # 2026-10-27: sed reads as it goes (q leaves stdin, $ peeks); per-line newline flags (x/g/G/H, q owes it); F; q ends -s/-i; w files once; -i via a temporary file (expected = GNU via WSL)
    @{ Name = 'astral_chunks';      Script = 'cases\astral_chunks.sh';      Args = @() }   # 2026-10-27: a surrogate pair across a writer's chunk boundary stays whole (the stateless encoder made two U+FFFD: printf/echo of a long capture, sed -i) (expected = GNU via WSL)
    @{ Name = 'subshell_isolation'; Script = 'cases\subshell_isolation.sh'; Args = @() }   # 2026-10-04: options, shopts, traps (own EXIT trap fires at subshell end), aliases stay in the subshell (expected = GNU via WSL)   # 2026-10-03: \* \? \[ \{ \, \~ stay quoted (echo \*, find -name \*.txt globbed) (expected = GNU via WSL)   # 2026-10-03: a copy without -p gets the current mtime (expected = GNU via WSL)   # 2026-10-03: ${x#"$p"} ${x//$o/$n} ${x%%"lit"}: expansions + quotes in pattern/replacement (expected = GNU via WSL)   # 2026-10-03: each shell's own cwd: stages, subshells, in-process scripts (expected = GNU via WSL)   # 2026-10-03: arr[$k]=v / m+=(["$k"]=v) with an expanded index (expected = GNU via WSL)   # 2026-10-03: each pipeline stage is a subshell (own vars/functions/options/traps) (expected = GNU via WSL)   # 2026-10-03: text then byte readers on one stdin; head -c streams N bytes (yes|head -c hung) (expected = GNU via WSL)   # 2026-10-03: CR is data in every line tool, a lone CR ends no line, no BOM sniffing (expected = GNU via WSL)   # 2026-10-03: first external with inherited stdio in a console-less shell lost its output (meaningful under -Detached)   # 2026-09-12: byte transparency - printf/$'' escapes emit BYTES; non-UTF-8 survives variables, $( ), pipes, files
)

Push-Location $testsDir
$pass = 0; $fail = 0; $skip = 0; $failed = @(); $skipped = @()
try {
    foreach ($t in $tests) {
        if ($t.AotOnly -and -not $aot) { Write-Host ("SKIP  {0}  (Native AOT only)" -f $t.Name) -ForegroundColor Yellow; $skip++; $skipped += "$($t.Name) (Native AOT only)"; continue }
        $expectedPath = Join-Path $testsDir "expected\$($t.Name).out"
        if (-not (Test-Path $expectedPath)) { Write-Host ("MISS  {0}  (no expected file)" -f $t.Name) -ForegroundColor Yellow; $fail++; $failed += $t.Name; continue }

        $expected = @(Get-Content -LiteralPath $expectedPath -Encoding UTF8)   # UTF-8 as generated (WSL); PS 5.1 defaults to ANSI and mangled non-ASCII expected text (2026-10-05)
        $a = $t.Args
        if ($Detached) {
            # decoded and split as PowerShell splits a native command's captured stdout
            $tmp = [System.IO.Path]::GetTempFileName()
            $cl  = (@($exe, $t.Script) + $a | ForEach-Object { '"' + $_ + '"' }) -join ' '
            if (-not [DetachedRun]::Run($cl, $testsDir, $tmp, 120000)) { Write-Host ("TIMEOUT {0}" -f $t.Name) -ForegroundColor Red }
            $text = [System.IO.File]::ReadAllText($tmp, [System.Text.Encoding]::UTF8)   # C#Bash writes UTF-8 (was the console's code page)
            Remove-Item -LiteralPath $tmp -ErrorAction SilentlyContinue
            $actual = @(if ($text.Length -gt 0) { $text -replace "`r?`n\z", '' -split "`r?`n" })   # \z: .NET's $ also matches before a final newline, dropping a last empty line (2026-10-12)
        } else {
            $actual = @(& $exe $t.Script @a 2>$null)
        }

        # a case whose program is missing says so as its only line, "SKIP: <reason>" (2026-10-20): never a pass, never
        # its own expected lines printed (three cases did that and could not fail)
        if ($actual.Count -eq 1 -and $actual[0] -clike 'SKIP: *') {
            $why = $actual[0].Substring(6)
            Write-Host ("SKIP  {0}  ({1})" -f $t.Name, $why) -ForegroundColor Yellow
            $skip++; $skipped += "$($t.Name) ($why)"; continue
        }
        $diff = Compare-Object $expected $actual -SyncWindow 0
        if ($null -eq $diff) {
            Write-Host ("PASS  {0}" -f $t.Name) -ForegroundColor Green
            $pass++
        } else {
            Write-Host ("FAIL  {0}" -f $t.Name) -ForegroundColor Red
            $fail++; $failed += $t.Name
            foreach ($d in $diff) {
                $side = if ($d.SideIndicator -eq '=>') { 'actual  ' } else { 'expected' }
                Write-Host ("        {0}: {1}" -f $side, $d.InputObject) -ForegroundColor DarkGray
            }
        }
    }
}
finally { Pop-Location }

Write-Host ""
$summaryColor = if ($fail -eq 0) { 'Green' } else { 'Red' }
Write-Host ("{0} passed, {1} failed" -f $pass, $fail) -ForegroundColor $summaryColor
if ($skip -gt 0) { Write-Host ("{0} skipped: {1}" -f $skip, ($skipped -join '; ')) -ForegroundColor Yellow }   # its own line: deploy.sh reads "N passed, N failed"
if ($fail -gt 0) { Write-Host ("Failed: {0}" -f ($failed -join ', ')) -ForegroundColor Red; exit 1 }
exit 0
