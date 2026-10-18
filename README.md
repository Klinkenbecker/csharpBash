# C#Bash

*(The GitHub repo is named `csharpBash` — GitHub doesn't allow `#` in repository names.)*

A standalone **bash interpreter for Windows, written in C#** — no WSL, no msys2, no Cygwin. It targets POSIX shell core plus the most-used bashisms, and runs the common Unix command set **in-process** so everyday scripts don't pay Windows' per-process spawn cost.

> **Provenance, stated plainly.** The architecture was human-directed; the code was written, tested, and optimised by Claude (Anthropic). It was developed under **Mercurial** — the full commit history is in [`MERCURIAL_HISTORY.md`](MERCURIAL_HISTORY.md), and the real repository ships as the `mercurial-history.hg` bundle (`hg unbundle mercurial-history.hg`). The story behind it: [*It's moosh all the way down*](https://www.linkedin.com/pulse/its-moosh-all-way-down-guy-mcilroy-mer9c/).

**Bullshit walks, money talks:**

| benchmark (seconds) | C#Bash | Git Bash | WSL bash | vs Git | vs WSL |
|---|---:|---:|---:|---:|---:|
| `loop` — 200 k `while` iterations | **0.185** | 1.561 | 0.446 | 8.4× | 2.4× |
| `arith` — 200 k `$(( ))` evaluations | **0.254** | 2.148 | 0.646 | 8.5× | 2.5× |
| `func` — 100 k function calls | **0.229** | 2.391 | 0.597 | 10.4× | 2.6× |
| `loop_big` — 2 M iterations | **1.491** | 15.658 | 3.585 | 10.5× | 2.4× |
| `coreutils` — 900 `basename`/`dirname`/`wc` calls | **0.066** | 15.219 | 0.914 | **231×** | 13.8× |
| `pipeline` — `grep\|sed\|sort\|uniq\|awk` over 50 k lines | 0.266 | 0.286 | **0.122** | 1.1× | 0.5× |
| `find` — walk 400 files | **0.024** | 0.136 | 0.236 | 5.7× | 9.8× |
| startup — `bash -c 'exit 0'` | 0.018 | **0.016** | 0.111 | 0.9× | 6.1× |

Every row's output is identical in all three shells. How it was measured, where it loses, and what a Claude Code call costs: [Performance](#performance).

## What it does

**Language:** pipelines; lists (`&&`, `||`, `;`, `&`); redirections including fd-aware `2>`, `>>`, `<`, `&>`, `2>&1`, `1>&2`, numbered descriptors (`exec 3>f`, `exec 3>&1`, `>&3`); Linux's device files (`/dev/null`, `/dev/zero`, `/dev/urandom`, `/dev/stdin`, `/dev/stdout`, `/dev/stderr`, `/dev/fd/N`, `/dev/tty`, which is "No such device or address" where there is no terminal to type into, as in Claude Code) as redirect targets, as file arguments and under `test`; here-documents and here-strings; command substitution `$(…)` and backticks (nested, isolated like a subshell); arithmetic `$(( … ))`, `(( … ))` and `for (( ; ; ))` with assignment and `++`; brace expansion `{a,b}` / `{1..9}`; tilde and pathname (glob) expansion with `nullglob`/`dotglob`/`globstar`/`nocaseglob`; single / double / `$'…'` quoting with bash's exact backslash rules. A script, a `-c` string, `source` and `eval` are read and run one line at a time, as bash reads them: the lines before a syntax error run, and nothing after an `exit` is read (an installer's payload).

**Variables & structure:** assignments and `+=` (to an array element too: `a[k]+=v`), `export`, `local`, `readonly`, `declare -i/-a/-A/-r/-x/-p/-f`, special parameters (`$?`, `$#`, `$@`, `$*`, `$0…`, `$!`, `$-`, `PIPESTATUS`, `BASH_REMATCH`, `LINENO`, `FUNCNAME`, `BASH_SOURCE`, `BASH_LINENO`, `RANDOM`, `PPID`, `UID`, `BASH_COMMAND`); the parameter-expansion operators (`${x:-y}` `${x:n:m}` `${x#pat}` `${x/pat/rep}` `${x^^}` `${!x}` `${arr[@]:n}` …); indexed and associative arrays; `if` / `while` / `until` / `for` / `case` (with `;&` and `;;&`); functions (both syntaxes), with bash's dynamic scoping (`local` arrays and attributes included); `[[ … ]]` with `&&`/`||`/`( )`, `=~` and pattern matching; `set -e/-u/-x/-T/-o pipefail`, `shopt`; field splitting by IFS as POSIX and bash define it (`IFS=` splits nothing, `IFS=,` keeps empty fields).

**Paths:** MSYS drive-letter form (`/c/Users/…`) and `/tmp` are understood alongside native `C:\…`, and the conversion is symmetric — `pwd`, `$PWD`, `mktemp` and `realpath` hand back `E:/dir/file`, a valid Windows path containing no backslashes. That matters because backslash is the shell's *escape* character: a path with backslashes in it is a value the shell's own `${p#…}`, globs, `case` and `[[ … ]]` would misread.

**Bytes:** a shell string is a byte sequence, so `printf '\377'` emits one byte, not the UTF-8 encoding of a character, and data that is not valid UTF-8 survives variables, `$(…)`, pipes and files intact. (It survives NUL too, which real bash cannot hold.)

**Interactive & runtime:** a readline-style line editor with history (`~/.bash_history`), tab completion, and `PS1`/`PS2` prompts; startup-file sourcing (login + interactive); invocation flags (`-c`, `-l`/`--login`, `--norc`, `--rcfile`, script files, shebang dispatch); `trap` (EXIT, INT, ERR, DEBUG with `$BASH_COMMAND`, RETURN); background jobs (an and-or list ending in `&` is one job; jobspecs `%N` `%%` `%-` `%name` `%?text`); Ctrl-C handling. Restricted mode (`-r`, `--restricted`, `rbash`, `set -r`) is not implemented, so the shell refuses it rather than run unrestricted; POSIX mode (`--posix`, `set -o posix`) says it is not available and goes on. Both print a link that opens a prefilled GitHub issue to ask for the feature (nothing is sent by itself). `compgen` lists this shell's own names; programmable completion (`complete`, `compopt`) is not implemented and says so.

## Embedded commands

To avoid the Windows process-spawn cost, the common AT&T System V toolset is implemented in-process:

- **Shell:** `echo` `printf` (incl. `-v`) `cd` `pwd` `pushd` `popd` `dirs` `export` `unset` `readonly` `set` `shopt` `shift` `read` `mapfile`/`readarray` `getopts` `test`/`[` `type` `command` `builtin` `alias` `unalias` `eval` `exec` `source`/`.` `local` `declare`/`typeset` `let` `trap` `jobs` `wait` `fg` `bg` `disown` `kill` `sleep` `env` `history` `times` `umask` (stored and printed only: Windows files have no mode bits, so it does not change the files C#Bash creates) `fc` (says history editing is not supported) `compgen` `hash` (incl. `-r`, `-d`, `-p`, `-t`, `-l`) `exit` `return` `break` `continue` `true` `false` `:`
- **Text:** `cat` `head` `tail` `wc` `rev` `tac` `tr` `cut` `uniq` `nl` `fold` `paste` `comm` `seq` `split` `od` `sort` `tee` `base64` `md5sum`/`sha1sum`/`sha256sum`/`sha512sum` `hexdump` `strings` (GNU strings 2.38, with `-e l` for UTF-16) `yes`
- **Files & dirs:** `touch` (incl. `-d '40 days ago'`-style dates) `mkdir` `rmdir` `rm` `mv` `cp` `cmp` `du` `ls` `find` (incl. `-name`, `-type`, `-mmin`) `stat` `mktemp` `realpath` `readlink` `chmod` `ln` `truncate` `basename` `dirname`
- **Search & transform:** `grep`/`egrep`/`fgrep` (incl. `-P` with `\K`) `sed` `awk` (a documented subset, with user functions, `print > file`, `getline` and `getline < file`, 3-argument `match`) `jq` (a jq 1.6 subset, with `-R`/`-Rs`) `diff` (incl. `-u` and `--label`) `xargs` `expr` `factor` `cal`
- **System:** `date` (incl. `-d` "3 days ago"-style parsing) `uname` `hostname` `arch` `nproc` `tty` `whoami` `id` `printenv` `timeout` `nohup` `setsid` `which` `kill` `pgrep` `pkill` `ps` (Windows has no procps; `pkill -f` never kills this shell's own ancestors) `df` (every drive, mapped shares included, in GNU's format)

Every tool parses its options strictly: an option (or awk construct) that is not implemented is
**never silently ignored** — the tool reports it and, when a real external of the same name is on
PATH, re-runs the command there (`BASH_COREUTILS=builtin` forbids that, `=external` prefers it).
`grep` and `sed` default to POSIX **BRE** (`-E`/`-r` for ERE). Process substitution `<( )` works
(temp-file emulation); `>( )` is refused with a clear message. See [`ARCHITECTURE.md`](ARCHITECTURE.md) §10.

## Startup and command lookup

**Why it matters on Windows: finding a program is slow, and not finding one is slowest.** A bare command name is looked up by trying every PATH entry with every `PATHEXT` extension, each try a file-system probe; a hit stops early, but a miss makes every probe and fails them all. On the machine this was measured on, 51 PATH entries × 12 names is about 600 probes, **~15 ms for one miss**, 8.8 ms of it two entries on a slow drive. Claude Code's snapshot makes exactly such a miss (`hash rg`) on every Bash call, so it was half of what each call cost. So C#Bash controls where its PATH comes from and remembers what it finds, misses included, as below; the measurements are under [Performance](#performance) ("What a Claude Code call costs") and in [`DECISIONS.md`](DECISIONS.md) ("REVIEW R1 RESULT").

**At startup**, in this order:

1. The environment is imported; Windows' `Path` becomes `PATH`.
2. **`BASH_PATH`**, if set and not empty, becomes `PATH` and is then removed, from the process environment too, so a nested shell keeps its parent's PATH. It lets a launcher keep its own PATH narrow and still hand the shell a full one (see [the start file](#start-claude-code-with-no-path-of-its-own) below).
3. When `git.exe` is on PATH, Git for Windows' `usr/bin` and `mingw64/bin` are put in front of it, exactly as Git Bash does, so `tar`, `perl`, `ssh` and any tool outside the embedded set resolve the same way.
4. The shell's own directory is added at the end of PATH, so a nested `bash` always resolves, Git or no Git.
5. `HOME` (forward-slash form; the Windows profile when unset), `BASH`, `SHELL`, `SHLVL`, `UID`, `OSTYPE=msys` and the other bash variables are set; `PYTHONIOENCODING=utf-8` is exported when unset, so a piped Python prints UTF-8 instead of crashing on `✓`. Output is UTF-8, with or without a console.
6. Startup files: a login shell (`-l`) reads `/etc/profile`, then the first of `~/.bash_profile`, `~/.bash_login`, `~/.profile`; an interactive shell reads `~/.bashrc` (or `--rcfile`); a non-interactive one (`-c`, a script, every Claude Code call) reads only `$BASH_ENV`.

**Finding a command**, in this order: an alias, a keyword, a function, a builtin or embedded tool; a name with a slash (or a drive, `F:x`) is that path, from the shell's own directory and never searched for; anything else is searched on PATH, trying each entry as named and then with every `PATHEXT` extension (`.COM`, `.EXE`, `.BAT`, `.CMD`, …), and taking the first match that can run (a program Windows starts, `.com` `.exe` `.bat` `.cmd`, a `.sh` file, or a `#!` line: what `[ -x ]` says). A `.js`, `.vbs` or other `PATHEXT` file that runs only through a file association is passed over, as Git Bash does. A `#!` script runs by its interpreter line (bash and sh scripts in-process), found on PATH or by its path; a `.sh` file without one runs in-process too. A bare name never runs a script from the current directory: write `./script`, as in bash.

- **Lookups are remembered across shells**, in `~/.bash/commands`, one small file per PATH value. A remembered program is used after one existence check, so a moved or deleted one is simply searched for again.
- **`hash name` also remembers a miss.** In a later shell it answers `hash: x: not found (remembered miss: hash -r searches again)` without searching. Running a program, `command -v` and `type` always search, as bash does. After installing a tool you had probed with `hash`, run `hash -r` (or `hash -d name`).
- **Windows limits that remain:** a program whose own path is over 260 characters, or one started from a directory over 260 characters, cannot be started (cmd cannot either; C#Bash says so, 126), though the shell itself can `cd` there and work with its files. A file name ending in a dot or a space cannot exist through Windows' file API, which drops it: a command name `s1.` finds nothing, but `cat s1.` reads `s1`.
- **When the shell's own search finds nothing, C#Bash looks in three folders only**: its own, System32 and the Windows folder (`.exe` appended, as Windows' launcher would). So `cmd` runs even on an empty PATH, while `command -v cmd` reports nothing. Never the current directory: Windows' own launcher searched it (in an ordinary terminal, or through an empty PATH entry), so a `.exe` planted there could answer a command that is not installed.

## Using it as Claude Code's shell

Claude Code on Windows needs *a* `bash.exe`; it finds Git for Windows by default, or whatever `CLAUDE_CODE_GIT_BASH_PATH` names. C#Bash is built to be that shell (with or without Git installed):

1. Publish the Native AOT build somewhere the build never touches:
   ```cmd
   tools\publish-aot.cmd            REM -> dist\Bash.exe, ~6.9 MB
   ```
   One static executable. No .NET runtime on the target, and **~18 ms startup, close to Git Bash's** — which matters because Claude Code spawns a shell for *every* tool call. The script exists because AOT needs two things on the *build* machine that are easy to get wrong: the MSVC C++ toolchain (`vcvars64.bat`) and `vswhere.exe` on PATH, which the vcvars script does not add. Read the comment at the top of it before concluding your toolchain is missing.

   The managed alternatives, if you cannot or would rather not build AOT:
   ```sh
   # self-contained, no runtime needed on the target: ~79 MB, ~74 ms
   dotnet publish Bash/Bash.csproj -c Release -r win-x64 \
       -p:PublishSingleFile=true -p:PublishReadyToRun=true -p:DebugType=none -o dist
   # framework-dependent, smallest download, needs .NET 8 installed: ~1.7 MB, ~68 ms
   dotnet publish Bash/Bash.csproj -c Release -r win-x64 -p:SelfContained=false \
       -p:PublishSingleFile=true -p:PublishReadyToRun=true -p:DebugType=none -o dist
   ```
   ReadyToRun keeps the runtime and its JIT, so it is about 11 % faster than AOT in *steady state* — but only past roughly 1.2 million shell operations in a single invocation, which no Claude Code tool call approaches. Pointing Claude Code at `bin\Release` is a bad idea either way: every rebuild replaces the file under a live session, and the test runner kills stale interpreter processes before building.
2. Set the variable for your user and restart Claude Code:
   ```powershell
   setx CLAUDE_CODE_GIT_BASH_PATH "C:\path\to\Bash.exe"
   ```
3. To go back to Git Bash, remove the variable (`setx` to an empty value, or System Properties → Environment Variables) and restart Claude Code.

### Start Claude Code with no PATH of its own

Claude Code builds a snapshot of your shell when a session starts and replays its PATH into every Bash call. With C#Bash the PATH's length no longer costs a call anything (lookups are remembered), but `claude.exe` itself has no need to search your PATH either. This start file keeps it narrow and hands the full PATH to the shell through `BASH_PATH`:

```bat
@echo off
rem claude.cmd: Claude Code with no tool PATH of its own; C#Bash gets the full PATH through BASH_PATH
rem Adjust the two C#Bash paths, and claude.exe's if your installer put it elsewhere.
setlocal
set "BASH_PATH=%PATH%"
set "CLAUDE_CODE_GIT_BASH_PATH=C:/path/to/Bash.exe"
set "PATH=C:/path/to"
"%USERPROFILE%/.local/bin/claude.exe" %*
endlocal
```

`setlocal` keeps the narrowed PATH out of the console you start it from, and `%*` passes your arguments (`--resume`, …) through. Anything `claude.exe` starts by itself rather than through the shell (the status line, hooks, MCP servers) then sees only that narrow PATH, so give those commands full paths: `C:/Windows/py.exe statusline.py`, not `py statusline.py`.

### Getting the best out of it

- **Write paths with forward slashes** (`C:/x/y` or `/c/x/y`). A backslash is the shell's escape character, and a Windows program handed `/c/x` gets `C:/x`, as under Git Bash; pass a switch to `cmd` as `cmd //c …`.
- **There is no terminal.** `/dev/tty` reports "No such device or address" and a program that would prompt on one fails instead of hanging; give tools their input on stdin or their non-interactive flags.
- **After installing a tool** you had probed with `hash`, run `hash -r`.
- **Reproduce from a script file, not through PowerShell.** Windows PowerShell 5.1 strips the inner double quotes when it hands `-c 'echo "x (y) z"'` to a native executable, so the shell receives `echo x (y) z` — and then correctly reports a syntax error. A report observed only through `& Bash.exe -c "..."` from PowerShell measures PowerShell's quoting, not the shell (2026-09-05, one full false report).
- **Quote the build in a bug report.** `Bash.exe --version` prints a third line, `C#Bash build 1.0.0+hg.<hash> <rev>`, stamped from the Mercurial revision at build time (a trailing `+` means an uncommitted tree).
- **Measure a call yourself** with `BASH_TIMING_LOG=<file>`: the shell appends where its time went (start, set-up, each `source`, `eval` and external program, exit) to that file when it exits. It costs about 2 ms a launch while set, nothing when not.

Claude Code's per-command wrapper, its shell snapshot (`bash -c -l`), cwd tracking, heredocs, process substitution, `timeout`, and the `rg`/`pkill` shims it defines have all been verified end to end, both with Git on PATH and on a PATH with no Git at all (`PROJECT_CONTEXT.md`, P5).

## Performance

The table at the top was measured on 2026-10-18 (build rev 316) on one Windows 10 machine against the two other bash shells available on it: **Git for Windows** bash 4.4 (bash on the MSYS2 POSIX emulation layer) and **WSL 2** bash 5.1 (native Linux bash, reaching Windows files over `/mnt`). Same scripts, wall clock in seconds including process startup. Every shell is launched directly with `CreateProcess`, the way Claude Code launches one, and timed on the high-resolution clock by `tools/benchtimer`; C#Bash best of nine, the other two best of three; startup is the median of five samples, each the average over 100 consecutive launches. The C#Bash build is the one that runs every pipeline stage in its own subshell, as bash does. Reproduce with `tests/bench/compare3.sh`, after building the timer: `dotnet build tools/benchtimer -c Release -o tools/benchtimer/bin`. **The outputs of every row were compared and are identical in all three shells** — a fast wrong answer would not count as a win.

**Where it wins, and why.** Interpreter throughput is 8–11× Git Bash and 2.4–2.6× native Linux bash. Startup is close to Git Bash's (18 ms against 16 ms in this run; it was level, 19 ms each, on 2026-10-16) and six times faster than WSL. The `coreutils` row is the project's premise made visible: 900 tool invocations that are 900 `CreateProcess` calls under Git Bash and zero under C#Bash.

**Where it loses, and why.** One row: a long streaming text pipeline against WSL, where native C `grep`/`sed`/`sort` beat managed implementations per line. It does beat Git Bash there. C#Bash is fastest where a script does *many small things*; a pipe pushing 50 k lines through five real tools is not that shape.

**What a Claude Code call costs.** Every Bash tool call is a new shell running Claude Code's wrapper and sourcing its snapshot. `tests/bench/claudecall.sh` makes that exact call: a trivial one (`true`, `ls`, `cd && pwd`) takes **~16 ms**, against ~31 ms before lookups were remembered, with a 50-entry PATH. Where the 16 ms go:
- **~11 ms is the floor** for starting and ending *any* Native AOT program on this machine (measured with an empty one).
- **~3 ms is C#Bash's own start-up**: the console, the environment and PATH, the options.
- **Under 1 ms is the snapshot.** It was 15 ms: the snapshot's `hash rg` misses on every call (`rg` lives inside `claude.exe`), and that miss searched every PATH entry with every extension, 8.8 ms of it two entries on a slow drive. The miss is now remembered, so **PATH length no longer matters per call**.
- **Exit is ~0.25 ms**, the same as any AOT program. Three Windows DLLs (networking, security) load only when a command needs them, and the one left at start-up is `icu.dll` (~0.7 ms, Windows' Unicode library).

**Caveats worth stating**, and they are in the harness rather than glossed. One machine, one run, no statistical treatment beyond the startup row: a single total of 100 launches moves by about ±1.5 ms from run to run on this machine, so that row is the median of five such totals, and a difference of a millisecond between two runs is still noise. WSL reaches these files over the 9P `/mnt` bridge, a real cost of using it on Windows files, and the filesystem rows are marked. The `pipeline` row also writes its input to `$TMPDIR`, which for WSL is ext4 *inside* the VM and for the other two is the Windows temp directory — so that row hands WSL a filesystem advantage as well as a tool-speed one. Git Bash's bash is 4.4 where WSL's is 5.1. Git Bash's own times move between runs (`loop_big` took 18.9 s on 2026-10-03, 15.2 s on 2026-10-16 and 15.7 s here), and the "vs Git" ratios move with them. These numbers are for the Native AOT build; the managed builds start in 68–74 ms and are about 11 % faster than AOT past roughly 1.2 M shell operations in one invocation.

## Architecture

A classic four-stage pipeline: **lexer → parser → AST → tree-walking evaluator**.

![Pipeline](docs/pipeline.svg)

- [`ARCHITECTURE.md`](ARCHITECTURE.md) — the conceptual model (including how it maps onto GNU bash's own modules, and where Windows forces it to diverge).
- [`IMPLEMENTATION.md`](IMPLEMENTATION.md) — the class/method map and an end-to-end walk-through.
- [`DECISIONS.md`](DECISIONS.md) — the append-only design log.
- [`PROJECT_CONTEXT.md`](PROJECT_CONTEXT.md) — current state and scope.

## Build · run · test

```sh
dotnet build -c Release            # builds Bash.sln (.NET 8 SDK)
Bash/bin/Release/net8.0/Bash.exe   # run it (or: Bash.exe -c '<command>')
pwsh tests/run-tests.ps1           # 156 self-checking cases
pwsh tests/compat/run-compat.ps1   # 237-probe differential battery against a reference bash
```

The test suite's expected outputs are never captured from the interpreter: most are GNU bash's own output (bash 5.1, run under WSL, the reference every behaviour is matched against), the rest are authored from bash's documented behaviour where a case is Windows-specific — so a regression fails rather than being silently baked in. Windows-only.

## Layout

```
Bash/        interpreter source (Lexer/ Parser/ Evaluator/ IO/) + Bash.csproj
docs/        architecture diagrams (SVG) + bash startup reference
tests/       PowerShell test runner + cases/ and expected/; compat/ (the differential battery); bench/compare3.sh (the benchmark;
             --build measures the committed tree against the deployed build); bench/claudecall.sh (what a Claude Code
             Bash call costs, and where it goes); corpus/run-n.sh (bash -n against GNU on 912 real scripts)
tools/       publish-aot.cmd (the ship build); deploy.sh and publish.sh (the project's own deploy and push); benchtimer (the
             benchmark's launcher and clock); bench.csx, attrib.csx
*.md         README + the four project docs
mercurial-history.hg   full Mercurial history (hg bundle)
```

## On Mercurial

This was built in **Mercurial**, by preference. The git repository you're reading is a published snapshot; the genuine, commit-by-commit record lives in hg, preserved here in full as [`MERCURIAL_HISTORY.md`](MERCURIAL_HISTORY.md) and the `mercurial-history.hg` bundle — `hg unbundle mercurial-history.hg` reconstructs the lot.

## License

[MIT](LICENSE).
