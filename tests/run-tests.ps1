<#
.SYNOPSIS
  Self-checking test runner for the Bash interpreter.

  Runs each script-mode test against the built Release exe, captures STDOUT only
  (stderr is discarded — job-id notices, prompts, and error text live there), and
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
$proj     = Join-Path $testsDir '..\Bash'
$exe      = Join-Path $proj 'bin\Release\net8.0\Bash.exe'
if ($Interpreter) { $exe = $Interpreter; $NoBuild = $true }

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
    # locks Bash.exe and fails the copy step). Match on resolved path — ours only.
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
    @{ Name = 'split';       Script = 'cases\split.sh';       Args = @() }   # split -l / -b → prefix+aa/ab/...
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
    @{ Name = 'specialvars'; Script = 'cases\specialvars.sh'; Args = @() }   # P1: OSTYPE PPID RANDOM UID BASH_VERSINFO LINENO PIPESTATUS $! $- FUNCNAME BASH_SOURCE, $(…) status
    @{ Name = 'heredoc';     Script = 'cases\heredoc.sh';     Args = @() }   # P1: heredoc/herestring, &>, 2>&1 >f order, /tmp & /c/ paths, nested $( ), "…" escapes, ${x:n:m} etc.
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
    @{ Name = 'native_args';        Script = 'cases\native_args.sh';        Args = @() }   # 2026-10-10: a Windows program gets /c/x and /tmp/x at the start of an argument as C:/x and the temp dir (as Git Bash); MSYS programs and the off switches untouched (authored: Windows-specific)
    @{ Name = 'python_utf8';        Script = 'cases\python_utf8.sh';        Args = @() }   # 2026-10-10: PYTHONIOENCODING=utf-8 exported unless set (piped py crashed on non-cp1252 text); not HGENCODING (authored: Windows-specific)
    @{ Name = 'utf8_bytes';         Script = 'cases\utf8_bytes.sh';         Args = @() }   # 2026-10-10: carried bytes spelling a UTF-8 char are that char ($'\xc3\xa9' is é: length, ==, case, file names, ${x^^}) (expected = GNU via WSL)
    @{ Name = 'awk_extended';       Script = 'cases\awk_extended.sh';       Args = @() }   # 2026-10-10: awk user functions, print > / >> file, getline [var] < file, close, fflush, 3-arg match, --version; BASH_COREUTILS=builtin (expected = gawk 5.1 via WSL)
    @{ Name = 'awk_escapes';        Script = 'cases\awk_escapes.sh';        Args = @() }   # 2026-10-10: awk "\xNN" / "\NNN" above 0x7F are bytes; bytes spelling a UTF-8 char are that char, across concatenation too (expected = gawk 5.1 via WSL)
    @{ Name = 'jq_raw_input';       Script = 'cases\jq_raw_input.sh';       Args = @() }   # 2026-10-10: jq -R (each line a string) and -Rs (whole input one string); inputs are one text; errors at file:line (expected = jq 1.6 via WSL)
    @{ Name = 'one_at_a_time';      Script = 'cases\one_at_a_time.sh';      Args = @() }   # 2026-10-11: scripts, -c, source, eval run one top-level command (one line) at a time: commands before a syntax error run; nothing after exit is parsed (expected = GNU via WSL)
    @{ Name = 'substitution_errors'; Script = 'cases\substitution_errors.sh'; Args = @() }   # 2026-10-11: a syntax error in $( ) / backquotes is met when it runs: $( ) abandons the top-level command (1), backquotes fail (2); subshell boundaries; -n silent (expected = GNU via WSL)
    @{ Name = 'coreutils_external'; Script = 'cases\coreutils_external.sh'; Args = @() }   # 2026-10-11: BASH_COREUTILS=external runs the PATH program (it recursed builtin -> RunCommand -> builtin until a stack overflow killed the shell); needs Git (authored)
    @{ Name = 'restricted_mode';    Script = 'cases\restricted_mode.sh';    Args = @() }   # 2026-10-11: -r / --restricted / -cr refuse to start, set -r ends the shell (2), with a prefilled-issue link carrying no command text (authored: no GNU oracle)
    @{ Name = 'umask_times_fc';     Script = 'cases\umask_times_fc.sh';     Args = @() }   # 2026-10-10: umask (octal, symbolic, -p, -S, per shell), times, fc (option check, then not supported) are builtins; fc ran fc.exe (expected = GNU via WSL)
    @{ Name = 'subshell_isolation'; Script = 'cases\subshell_isolation.sh'; Args = @() }   # 2026-10-04: options, shopts, traps (own EXIT trap fires at subshell end), aliases stay in the subshell (expected = GNU via WSL)   # 2026-10-03: \* \? \[ \{ \, \~ stay quoted (echo \*, find -name \*.txt globbed) (expected = GNU via WSL)   # 2026-10-03: a copy without -p gets the current mtime (expected = GNU via WSL)   # 2026-10-03: ${x#"$p"} ${x//$o/$n} ${x%%"lit"}: expansions + quotes in pattern/replacement (expected = GNU via WSL)   # 2026-10-03: each shell's own cwd: stages, subshells, in-process scripts (expected = GNU via WSL)   # 2026-10-03: arr[$k]=v / m+=(["$k"]=v) with an expanded index (expected = GNU via WSL)   # 2026-10-03: each pipeline stage is a subshell (own vars/functions/options/traps) (expected = GNU via WSL)   # 2026-10-03: text then byte readers on one stdin; head -c streams N bytes (yes|head -c hung) (expected = GNU via WSL)   # 2026-10-03: CR is data in every line tool, a lone CR ends no line, no BOM sniffing (expected = GNU via WSL)   # 2026-10-03: first external with inherited stdio in a console-less shell lost its output (meaningful under -Detached)   # 2026-09-12: byte transparency — printf/$'' escapes emit BYTES; non-UTF-8 survives variables, $( ), pipes, files
)

Push-Location $testsDir
$pass = 0; $fail = 0; $failed = @()
try {
    foreach ($t in $tests) {
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
            $actual = @(if ($text.Length -gt 0) { $text -replace "`r?`n$", '' -split "`r?`n" })
        } else {
            $actual = @(& $exe $t.Script @a 2>$null)
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
if ($fail -gt 0) { Write-Host ("Failed: {0}" -f ($failed -join ', ')) -ForegroundColor Red; exit 1 }
exit 0
