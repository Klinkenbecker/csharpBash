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
param([switch]$NoBuild)

# Keep 'Continue' (not 'Stop'): tests intentionally write to stderr (job notices,
# error-path messages), and under 'Stop' PowerShell 5.1 turns a native command's
# stderr into a terminating error. We check exit codes explicitly instead.
$ErrorActionPreference = 'Continue'
$testsDir = $PSScriptRoot
$proj     = Join-Path $testsDir '..\Bash'
$exe      = Join-Path $proj 'bin\Release\net8.0\Bash.exe'

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
)

Push-Location $testsDir
$pass = 0; $fail = 0; $failed = @()
try {
    foreach ($t in $tests) {
        $expectedPath = Join-Path $testsDir "expected\$($t.Name).out"
        if (-not (Test-Path $expectedPath)) { Write-Host ("MISS  {0}  (no expected file)" -f $t.Name) -ForegroundColor Yellow; $fail++; $failed += $t.Name; continue }

        $expected = @(Get-Content -LiteralPath $expectedPath)
        $a = $t.Args
        $actual = @(& $exe $t.Script @a 2>$null)

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
