<#
.SYNOPSIS
  Claude Code compatibility battery: differential test of C#Bash against a
  reference bash, with a ratcheted baseline.

  probes.txt holds probe scripts separated by lines containing only "----".
  Each probe is run as a script file by BOTH shells in a fresh fixture directory
  (a.txt, b.txt, c.txt, d2/sub/deep.txt, empty.txt, n.md), stdin closed, 20 s
  timeout. A probe PASSES when stdout and exit code are byte-identical.

  baseline.txt lists the probe numbers that are expected to pass. The run FAILS
  (exit 1) if any baseline probe regresses; probes that newly pass are reported
  so the baseline can be advanced with -UpdateBaseline. APPEND new probes at the
  end of probes.txt only - numbers are positional.

  The reference bash is found from -Reference, else $env:CLAUDE_CODE_GIT_BASH_PATH,
  else C:\Program Files\Git\bin\bash.exe, else the first bash.exe on PATH that is
  not our own build. Without one the battery cannot run (exit 2).

.PARAMETER Reference
  Path to the reference bash.exe.
.PARAMETER NoBuild
  Skip the Release build.
.PARAMETER UpdateBaseline
  Rewrite baseline.txt with every probe that passes in this run.
.PARAMETER Only
  Run just these probe numbers (e.g. -Only 12,34,97).
.PARAMETER ShowAll
  Print the real/C#Bash output for every DIFF (default: first 240 chars each).

.EXAMPLE
  powershell -File tests/compat/run-compat.ps1 -NoBuild
  powershell -File tests/compat/run-compat.ps1 -NoBuild -UpdateBaseline
#>
param(
    [string]$Reference = '',
    [switch]$NoBuild,
    [switch]$UpdateBaseline,
    [int[]]$Only = @(),
    [switch]$ShowAll
)

$ErrorActionPreference = 'Continue'
$here     = $PSScriptRoot
$proj     = Join-Path $here '..\..\Bash'
$exe      = (Resolve-Path (Join-Path $proj 'bin\Release\net8.0\Bash.exe') -ErrorAction SilentlyContinue).Path
$work     = Join-Path $here 'work'
$probes   = Join-Path $here 'probes.txt'
$baseFile = Join-Path $here 'baseline.txt'
$timeoutMs = 20000

if (-not $NoBuild) {
    Write-Host "Building (Release)..." -ForegroundColor Cyan
    & dotnet build -c Release $proj -nologo | Out-Null
    if ($LASTEXITCODE -ne 0) { Write-Error "build failed"; exit 2 }
    $exe = (Resolve-Path (Join-Path $proj 'bin\Release\net8.0\Bash.exe')).Path
}
if (-not $exe -or -not (Test-Path $exe)) { Write-Error "interpreter not found (run without -NoBuild)"; exit 2 }

# ---- locate the reference bash ------------------------------------------------
function Find-ReferenceBash {
    param([string]$explicit, [string]$ours)
    $cands = @()
    if ($explicit) { $cands += $explicit }
    if ($env:CLAUDE_CODE_GIT_BASH_PATH) { $cands += $env:CLAUDE_CODE_GIT_BASH_PATH }
    # Git's own bash, wherever Git lives (portable installs are not under Program Files)
    $git = Get-Command git.exe -ErrorAction SilentlyContinue
    if ($git) {
        $gitRoot = Split-Path (Split-Path $git.Source -Parent) -Parent
        $cands += (Join-Path $gitRoot 'bin\bash.exe')
        $cands += (Join-Path $gitRoot 'usr\bin\bash.exe')
    }
    $cands += 'C:\Program Files\Git\bin\bash.exe'
    $cands += 'C:\Program Files\Git\usr\bin\bash.exe'
    foreach ($d in ($env:PATH -split ';')) {
        if ($d) { $cands += (Join-Path $d 'bash.exe') }
    }
    # never WSL's launcher (System32\bash.exe): it is a different OS, not a reference
    $sysRoot = $env:SystemRoot
    foreach ($c in $cands) {
        if (-not (Test-Path $c)) { continue }
        $full = (Resolve-Path $c).Path
        if ($full -eq $ours) { continue }
        if ($sysRoot -and $full.StartsWith($sysRoot, [System.StringComparison]::OrdinalIgnoreCase)) { continue }
        return $full
    }
    return $null
}
$ref = Find-ReferenceBash $Reference $exe
if (-not $ref) { Write-Error "no reference bash found; pass -Reference <path to bash.exe>"; exit 2 }
Write-Host ("reference : {0}" -f $ref) -ForegroundColor DarkGray
Write-Host ("candidate : {0}" -f $exe) -ForegroundColor DarkGray

# ---- split probes ---------------------------------------------------------------
$text = [System.IO.File]::ReadAllText($probes)
$chunks = [regex]::Split($text.Replace("`r`n", "`n"), "(?m)^----\n")
$probeList = @()
$n = 0
foreach ($c in $chunks) {
    $n++
    $body = $c
    if (-not $body.EndsWith("`n")) { $body += "`n" }
    $probeList += @{ Num = $n; Body = $body; Title = ($body -split "`n")[0] }
}

if (Test-Path $work) { Remove-Item -Recurse -Force $work }
New-Item -ItemType Directory -Force (Join-Path $work 'probes') | Out-Null
foreach ($p in $probeList) {
    $path = Join-Path $work ('probes\{0:d3}.sh' -f $p.Num)
    [System.IO.File]::WriteAllText($path, $p.Body, (New-Object System.Text.UTF8Encoding($false)))
}

# ---- fixture + runner -----------------------------------------------------------
function New-Fixture([string]$dir) {
    if (Test-Path $dir) { Remove-Item -Recurse -Force $dir }
    New-Item -ItemType Directory -Force (Join-Path $dir 'd2\sub') | Out-Null
    $u = New-Object System.Text.UTF8Encoding($false)
    [System.IO.File]::WriteAllText((Join-Path $dir 'a.txt'), "a`nb`n", $u)
    [System.IO.File]::WriteAllText((Join-Path $dir 'b.txt'), "zz`n", $u)
    [System.IO.File]::WriteAllText((Join-Path $dir 'c.txt'), "aXb`n", $u)
    [System.IO.File]::WriteAllText((Join-Path $dir 'd2\sub\deep.txt'), "deep`n", $u)
    [System.IO.File]::WriteAllText((Join-Path $dir 'empty.txt'), "", $u)
    [System.IO.File]::WriteAllText((Join-Path $dir 'n.md'), "# md`n", $u)
}

function Invoke-Shell([string]$shell, [string]$script, [string]$cwd) {
    $psi = New-Object System.Diagnostics.ProcessStartInfo
    $psi.FileName = $shell
    $psi.Arguments = $script
    $psi.WorkingDirectory = $cwd
    $psi.UseShellExecute = $false
    $psi.RedirectStandardInput = $true
    $psi.RedirectStandardOutput = $true
    $psi.RedirectStandardError = $true
    $psi.StandardOutputEncoding = New-Object System.Text.UTF8Encoding($false)
    $psi.StandardErrorEncoding = New-Object System.Text.UTF8Encoding($false)
    $p = [System.Diagnostics.Process]::Start($psi)
    $p.StandardInput.Close()
    $outTask = $p.StandardOutput.ReadToEndAsync()
    $errTask = $p.StandardError.ReadToEndAsync()
    $timedOut = $false
    if (-not $p.WaitForExit($timeoutMs)) {
        $timedOut = $true
        & taskkill /T /F /PID $p.Id 2>$null | Out-Null
        $p.WaitForExit()
    }
    $code = if ($timedOut) { 124 } else { $p.ExitCode }
    return @{ Out = $outTask.Result; Err = $errTask.Result; Code = $code; TimedOut = $timedOut }
}

function Clip([string]$s, [int]$max) {
    $s = $s.Replace("`n", '|')
    if ($ShowAll -or $s.Length -le $max) { return $s }
    return $s.Substring(0, $max) + '...'
}

$baseline = @()
if (Test-Path $baseFile) {
    $baseline = @(Get-Content $baseFile | Where-Object { $_ -match '^\d+$' } | ForEach-Object { [int]$_ })
}

$passed = @(); $failed = @(); $hung = @()
$fxReal = Join-Path $work 'fx_real'
$fxCs   = Join-Path $work 'fx_cs'
foreach ($p in $probeList) {
    if ($Only.Count -gt 0 -and ($Only -notcontains $p.Num)) { continue }
    $script = '../probes/{0:d3}.sh' -f $p.Num
    New-Fixture $fxReal
    New-Fixture $fxCs
    $r = Invoke-Shell $ref $script $fxReal
    $c = Invoke-Shell $exe $script $fxCs
    $rOut = $r.Out + "exit=$($r.Code)`n"
    $cOut = $c.Out + "exit=$($c.Code)`n"
    if ($rOut -ceq $cOut) {
        $passed += $p.Num
    } else {
        $failed += $p.Num
        if ($c.TimedOut) { $hung += $p.Num }
        $tag = if ($baseline -contains $p.Num) { 'REGRESS' } else { 'DIFF' }
        $color = if ($tag -eq 'REGRESS') { 'Red' } else { 'DarkYellow' }
        Write-Host ("{0,-7} {1:d3}: {2}" -f $tag, $p.Num, $p.Title) -ForegroundColor $color
        Write-Host ("        real: {0}" -f (Clip $rOut 240)) -ForegroundColor DarkGray
        Write-Host ("        cs  : {0}" -f (Clip $cOut 240)) -ForegroundColor DarkGray
        if ($c.Err) { Write-Host ("        cserr: {0}" -f (Clip $c.Err 200)) -ForegroundColor DarkGray }
    }
}

$regressed = @($baseline | Where-Object { ($failed -contains $_) -and (($Only.Count -eq 0) -or ($Only -contains $_)) })
$newPass   = @($passed   | Where-Object { $baseline -notcontains $_ })

Write-Host ""
Write-Host ("{0} pass, {1} diff ({2} hung) of {3} probes; baseline {4}" -f $passed.Count, $failed.Count, $hung.Count, ($passed.Count + $failed.Count), $baseline.Count) -ForegroundColor Cyan
if ($newPass.Count -gt 0) { Write-Host ("NEW PASS (not in baseline): {0}" -f ($newPass -join ',')) -ForegroundColor Green }
if ($regressed.Count -gt 0) { Write-Host ("REGRESSIONS: {0}" -f ($regressed -join ',')) -ForegroundColor Red }

if ($UpdateBaseline) {
    $merged = @($baseline + $passed | Sort-Object -Unique)
    if ($Only.Count -gt 0) { $merged = @($merged | Where-Object { ($Only -notcontains $_) -or ($passed -contains $_) }) }
    else { $merged = @($passed | Sort-Object -Unique) }
    $merged | ForEach-Object { "$_" } | Set-Content -Encoding ascii $baseFile
    Write-Host ("baseline written: {0} probes" -f $merged.Count) -ForegroundColor Green
}

if ($regressed.Count -gt 0) { exit 1 }
exit 0
