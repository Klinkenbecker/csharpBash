# Using C#Bash

The [README](../README.md) has the short version. This is the whole of it: getting C#Bash, making it Claude Code's
shell, how it starts and finds commands, and how its help and its log of unsupported requests work.

## Using it as Claude Code's shell

Claude Code on Windows needs *a* `bash.exe`; it finds Git for Windows by default, or whatever `CLAUDE_CODE_GIT_BASH_PATH` names. C#Bash is built to be that shell (with or without Git installed):

To skip building it: run the installer (README, "Use it with Claude Code"; from [Releases](https://github.com/Klinkenbecker/csharpBash/releases), with its `.sha256`), which puts `Bash.exe` in `%USERPROFILE%/.bash/bin`, and go to step 2. It is not code-signed, so Windows may warn about it the first time. Since 2026-11-07 a release carries only the installer; up to hg-657 it also carried `Bash.exe` on its own.

1. Publish the Native AOT build somewhere the build never touches:
   ```cmd
   tools\publish-aot.cmd            REM -> dist\Bash.exe, ~7 MiB
   ```
   One static executable. No .NET runtime on the target, and **~9 ms startup, against Git Bash's ~23 ms as Claude Code starts it** — which matters because Claude Code spawns a shell for *every* tool call. The build machine needs the supported toolchain, two free downloads: the .NET 10 SDK and the Visual Studio C++ build tools ([Developing C#Bash, "The supported toolchain"](DEVELOPING.md#the-supported-toolchain): what to install, the one-line install, the license condition). The script finds the C++ tools itself; if AOT fails, read the comment at the top of it before concluding your toolchain is missing.

   The managed alternatives, if you cannot or would rather not build AOT:
   ```sh
   # self-contained, no runtime needed on the target: ~79 MB, ~74 ms
   dotnet publish Bash/Bash.csproj -c Release -r win-x64 \
       -p:PublishSingleFile=true -p:PublishReadyToRun=true -p:DebugType=none -o dist
   # framework-dependent, smallest download, needs .NET 10 installed: ~1.7 MB, ~68 ms
   dotnet publish Bash/Bash.csproj -c Release -r win-x64 -p:SelfContained=false \
       -p:PublishSingleFile=true -p:PublishReadyToRun=true -p:DebugType=none -o dist
   ```
   ReadyToRun keeps the runtime and its JIT, so it is about 11 % faster than AOT in *steady state* — but only past roughly 1.2 million shell operations in a single invocation, which no Claude Code tool call approaches. Pointing Claude Code at `bin\Release` is a bad idea either way: every rebuild replaces the file under a live session, and the test runner kills stale interpreter processes before building.
2. Set the variable for your user, then start Claude Code from a NEW terminal (`setx` reaches only programs started
   afterwards):
   ```cmd
   setx CLAUDE_CODE_GIT_BASH_PATH "C:/path/to/Bash.exe"
   ```
   Claude Code runs that shell even when another `bash.exe` (WSL's launcher in System32, Git's) comes first on PATH:
   measured with Claude Code 2.1.316 on 2026-11-03. Earlier versions ignored the variable and took the first
   `bash.exe` on PATH (Claude Code issue #94077); without the variable, that is still what happens. Check with
   `! bash --version` in a session: its last line must read `C#Bash build …`.
3. To go back to Git Bash, remove the variable (`setx` to an empty value, or System Properties → Environment Variables) and restart Claude Code.

### Start Claude Code with a PATH of one folder: the git it runs

Claude Code builds a snapshot of your shell when a session starts and replays its PATH into every Bash call. With C#Bash the PATH's length no longer costs a call anything (lookups are remembered), and `claude.exe` itself needs its PATH only to find `git`, which it runs by itself in the background while a session is open (its branch, status and worktree checks; 919 calls in one day on the development machine). A search of a long PATH is slow, and slower still when there is no git, so the installer's start file, `claude.cmd`, gives `claude.exe` a PATH of ONE folder, chosen by **`BASH_CLAUDE_GIT`**, and hands your full PATH to the shell through `BASH_PATH`:

- `BASH_CLAUDE_GIT=path` (the default): the folder of the first `git.exe` on your PATH, found once, at the start; with no git on your PATH, none.
- `BASH_CLAUDE_GIT=none`: no git for Claude Code (its git view stays empty). For a project kept in Mercurial and published through a git mirror, a working git would show the mirror's state, not yours.
- `BASH_CLAUDE_GIT=<git>`: that `git.exe`, or the folder holding it. A value naming neither says so and runs without git.

**Which to choose.** Only Claude Code's own view of the repository (its branch and status) depends on it. A full git answers each of Claude Code's calls in about 25 ms and gives it that view. `none` runs nothing, costs nothing, and leaves the view empty. Wombat's `git.exe` (a git replacement for Windows, not yet published), when it is the git found, refuses these calls for now, in about 3 ms each, with no view and a line in its log for each, until it serves them (planned). Measured on the development machine: a full git 24 to 30 ms a call (20 runs), Wombat's refusal 2.7 to 3.5 ms (medians, its current build).

The git Claude's commands get is C#Bash's, from your full PATH, whatever this says. Each install rewrites `claude.cmd`, so set `BASH_CLAUDE_GIT` where the installer never writes: in your own `claude.bat` (below) or with `setx`.

`setlocal` keeps the narrowed PATH out of the console you start it from, and `%*` passes your arguments (`--resume`, …) through. `CLAUDE_CODE_USE_POWERSHELL_TOOL=0` stops Claude Code reaching for PowerShell before bash. Hooks and the status line run through the shell, that is C#Bash, with your full PATH and its own folder first, so `bash statusline.sh` there finds C#Bash. Anything `claude.exe` starts directly (an MCP server, an editor) sees only that one folder, so name those by full path: `C:/Windows/py.exe server.py`, not `py server.py`.

To keep typing `claude` without putting C#Bash's folder on your PATH, give a folder that IS on your PATH a short `claude.bat` that hands off to the installed start file; it is also the place for your `BASH_CLAUDE_GIT`:

```bat
@echo off
rem optional: which git Claude Code itself runs (path, none, or a git.exe / its folder)
set "BASH_CLAUDE_GIT=path"
rem optional: a temp folder for Claude only (both lines: Claude Code exports both into every command)
set "TMP=D:/claude-tmp"
set "TEMP=D:/claude-tmp"
"%USERPROFILE%/.bash/bin/claude.cmd" %*
```

Each install rewrites `claude.cmd`, so the hand-off always starts the installed build; the installer never touches `claude.bat`.

A `set` in `claude.bat` reaches Claude Code and what it starts, nothing else, so it gives Claude its own temp folder while your account's `TMP` and `TEMP` stay as they are. Set both: C#Bash takes its one folder from `TMP` (see [One temp folder](#getting-the-best-out-of-it)), but Claude Code exports both `TMP` and `TEMP` into every command, so a `TEMP` left as it was would name a second folder. Set them here rather than anywhere C#Bash alone reads: Claude Code exports `TMP` and `TEMP` into every command, as far as seen the values it was started with, so it must start with the same folder.

### Getting the best out of it

- **Write paths with forward slashes** (`C:/x/y` or `/c/x/y`). A backslash is the shell's escape character, and a Windows program handed `/c/x` gets `C:/x`, as under Git Bash (a colon list, `/c/x:/f/y`, gets `C:/x;F:/y`; any other argument with a colon is passed as typed); pass a switch to `cmd` as `cmd //c …`, except under `MSYS_NO_PATHCONV=1` or `MSYS2_ARG_CONV_EXCL='*'`, which turn the `//` escape off with the rest: write `cmd /c` there (as in Git Bash). The same goes inside an argument after the first `=` and after `-` and one letter (`--file=/c/x` → `--file=C:/x`, `-I/c/x` → `-IC:/x`), and for the program's environment (`X=/c/x prog`, or an exported `X`, reaches it as `C:/x`); a word holding `;` is passed whole, a bare `/c` or `/` as typed. `MSYS_NO_PATHCONV=1` turns all of it off, `MSYS2_ARG_CONV_EXCL` (`*` or `;`-separated prefixes) the arguments, `MSYS2_ENV_CONV_EXCL` (prefixes of `NAME=value`) the environment, as in Git Bash. A nested `bash` (this shell) and an MSYS program get the environment as typed.
- **One temp folder.** `/tmp`, `$TMP`, `$TEMP` and `$TMPDIR` all name the same folder, with forward slashes
  (`F:/TEMP`): the one `TMP` named when the shell started (else `TEMP`, else your own temp folder). A Windows path
  assigned to `TMP`, `TEMP` or `TMPDIR` is stored with forward slashes (`TMP='F:\x'` reads `F:/x`), so Claude Code's
  own `export TEMP='F:\TEMP' TMP='F:\TEMP'` before every command reads `F:/TEMP` too. A C#Bash started
  from C#Bash keeps that folder, whatever `TMP`, `TEMP` or `TMPDIR` you give it; any other program gets what you give
  it. `mktemp` uses `$TMPDIR`, so `TMPDIR=/x mktemp` puts the file in `/x`.
- **Write files with LF line ends**, not CRLF. Every tool C#Bash provides writes LF, and every bash tool expects it.
  C#Bash copes with a Windows program's CRLF (a line tool reads it as the line end, `$( )` drops a trailing one),
  but text that keeps its CRs is text every later step has to cope with. A log, a script, a data file you write
  yourself: LF.
- **There is no terminal.** `/dev/tty` reports "No such device or address" and a program that would prompt on one fails instead of hanging; give tools their input on stdin or their non-interactive flags.
- **After installing a tool**, run `hash -r`.
- **Reproduce from a script file, not through PowerShell.** Windows PowerShell 5.1 strips the inner double quotes when it hands `-c 'echo "x (y) z"'` to a native executable, so the shell receives `echo x (y) z` — and then correctly reports a syntax error. A report observed only through `& Bash.exe -c "..."` from PowerShell measures PowerShell's quoting, not the shell (2026-09-05, one full false report).
- **Quote the build in a bug report.** `Bash.exe --version` prints a third line, `C#Bash build 1.0.0+hg.<hash> <rev>`, stamped from the Mercurial revision at build time (a trailing `+` means an uncommitted tree).
- **Measure a call yourself** with `BASH_TIMING_LOG=<file>`: the shell appends where its time went (start, set-up, each `source`, `eval` and external program, exit) to that file when it exits. It costs about 2 ms a launch while set, nothing when not.

Claude Code's per-command wrapper, its shell snapshot (`bash -c -l`), cwd tracking, heredocs, process substitution, `timeout`, and the `rg`/`pkill` shims it defines have all been verified end to end, both with Git on PATH and on a PATH with no Git at all.

## Startup and command lookup

**Why it matters on Windows: finding a program is slow, and not finding one is slowest.** A bare command name is looked up by trying every PATH entry with every `PATHEXT` extension, each try a file-system probe; a hit stops early, but a miss makes every probe and fails them all. On the machine this was measured on, 51 PATH entries × 12 names is about 600 probes, **~15 ms for one miss**, 8.8 ms of it two entries on a slow drive. Claude Code's snapshot makes exactly such a miss (`hash rg`) on every Bash call, so it was half of what each call cost. So C#Bash controls where its PATH comes from and remembers what it finds, misses included, as below; the measurements are in [Performance](PERFORMANCE.md) ("What a Claude Code call costs").

**At startup**, in this order:

1. The environment is imported; Windows' `Path` becomes `PATH`. **`$PATH` has two forms.** Inside the shell it reads in GNU/MSYS form, `:`-separated, `/c/x:/f/y`, whatever form it arrived in, so `IFS=:` and the POSIX idioms work as in any bash. An assignment may use that form, Windows form (`C:/x;F:/y`, either slash) or both mixed (`PATH="$PWD/bin:$PATH"`): it is converted on the way in. Empty entries stay as given; a one-letter relative entry is written `./A`, because `A:/x` reads as drive A's `/x`. Every program the shell starts gets PATH in Windows form with forward slashes, `C:/x;F:/y`: never a backslash, never an empty entry. `$PWD` and `$HOME` stay `C:/x`, so a test such as `case ":$PATH:" in *":$HOME/bin:"*)` does not match; compare in one form.
2. **`BASH_PATH`**, if set and not empty, becomes `PATH` and is then removed, from the process environment too, so a nested shell keeps its parent's PATH. It lets a launcher keep its own PATH narrow and still hand the shell a full one (see [the start file](#start-claude-code-with-a-path-of-one-folder-the-git-it-runs) above).
3. The shell's own directory is put FIRST on its PATH (moved there if it is already further on), so a nested `bash`, an `sh`, and the `sh` a program it starts looks up (make) are this shell, even when another `bash.exe` or `sh.exe` (WSL's launcher in System32, Git's) comes first on the PATH it was given. Only the shell's own PATH and its children's change; the system and user PATH are untouched. (Until 2026-11-03 it was added at the end, so another bash won.)
4. `HOME` (forward-slash form; the Windows profile when unset), `BASH`, `SHELL`, `SHLVL`, `UID`, `OSTYPE=msys` and the other bash variables are set; `PYTHONIOENCODING=utf-8` is exported when unset, so a piped Python prints UTF-8 instead of crashing on `✓`. Output is UTF-8, with or without a console.
5. Startup files: a login shell (`-l`) reads `/etc/profile`, then the first of `~/.bash_profile`, `~/.bash_login`, `~/.profile`; an interactive shell reads `~/.bashrc` (or `--rcfile`); a non-interactive one (`-c`, a script, every Claude Code call) reads only `$BASH_ENV`.

**Finding a command**, in this order: an alias, a keyword, a function, a builtin or embedded tool; a name with a slash (or a drive, `F:x`) is that path, from the shell's own directory and never searched for; anything else is looked for in the shell's own folder (`%USERPROFILE%/.bash/bin` when installed), then along [the fallback chain](#the-fallback-chain) (by default the shell's PATH, then WSL), trying each folder as named and then with every `PATHEXT` extension (`.COM`, `.EXE`, `.BAT`, `.CMD`, …), and taking the first match that can run (a program Windows starts, `.com` `.exe` `.bat` `.cmd`, a `.sh` file, or a `#!` line: what `[ -x ]` says). A `.js`, `.vbs` or other `PATHEXT` file that runs only through a file association is passed over, as Git Bash does. A `#!` script runs by its interpreter line (bash and sh scripts in-process), found on PATH or by its path; a `.sh` file without one runs in-process too, and so does any other text file with no `#!` named by its path (`./build`), as bash and msys run one (binary content is refused: "cannot execute binary file: Exec format error"); found on PATH by a bare name, such a file is not run. `/bin/NAME` and `/usr/bin/NAME`, when the drive has no such folder, run `NAME` (a builtin first, then PATH), as a `#!` line does. A bare name never runs a script from the current directory: write `./script`, as in bash.

- **`sh` is the shell under the name `sh.exe`**, beside `Bash.exe` (the installer and deploy make it a hard link; by hand a copy works), as Git for Windows and MSYS2 ship theirs (each one's `sh.exe` is byte-for-byte its `bash.exe`). Step 3 above puts that folder on PATH, so `sh -c`, `sh script`, `… | sh`, `xargs sh -c` and `find -exec sh -c` find it, and so do the programs the shell starts (`make`, say); something started outside C#Bash needs the folder on its own PATH. Started as `sh`, it runs bash's default mode, as a `#!/bin/sh` script does: POSIX mode is not available, and nothing is printed about it. Its messages name it by its path (`$0` is the path it was started by). `tools/deploy.sh` makes `dist/sh.exe` a hard link to `dist/Bash.exe` (since 2026-11-01; one file on disk, re-made at every deploy so it never keeps an old build). With only `Bash.exe` downloaded, make it yourself: a copy, or a hard link (`ln Bash.exe sh.exe` in C#Bash), made again after each new `Bash.exe`, since a link keeps the build it was made to.
- **Lookups are remembered across shells**, in `~/.bash/commands`, one small file per PATH value. A remembered program is used after one existence check, so a moved or deleted one is simply searched for again. A program installed later in a folder earlier on PATH stays hidden behind the remembered one until `hash -r`.
- **`hash name` also remembers a miss.** In a later shell it answers `hash: x: not found (remembered miss: hash -r searches again)` without searching. Running a program, `command -v` and `type` always search, as bash does. After installing a tool you had probed with `hash`, run `hash -r` (or `hash -d name`).
- **Windows limits that remain:** a program whose own path is over 260 characters, or one started from a directory over 258 characters, cannot be started (cmd cannot either; C#Bash says so, 126), though the shell itself can `cd` there and work with its files. A file name ending in a dot or a space cannot exist through Windows' file API, which drops it: a command name `s1.` finds nothing, but `cat s1.` reads `s1`.
- **When the shell's own search finds nothing, C#Bash looks in three folders only**, then WSL if the chain has it: its own, System32 and the Windows folder (`.exe` appended, as Windows' launcher would). So `cmd` runs even on an empty PATH, while `command -v cmd` reports nothing. Never the current directory: Windows' own launcher searched it (in an ordinary terminal, or through an empty PATH entry), so a `.exe` planted there could answer a command that is not installed.

### The fallback chain

A command C#Bash does not provide itself and its own folder does not hold, or a C#Bash tool given an option it does not
have, is looked for in these steps, in the order `%USERPROFILE%/.bash/etc/csharpBash.config` gives:

```
# %USERPROFILE%/.bash/etc/csharpBash.config
unix-tools = F:/Tools/UnixUtils/usr/local/wbin;PATH;WSL
```

- **A folder** of Unix tools: searched like a PATH entry, so a tools folder can come before PATH without touching the
  Windows PATH. Any number, each an absolute path. The shell's own folder (`%USERPROFILE%/.bash/bin` when installed)
  is not a step: it is looked in first, before every step and whatever PATH a script sets, so its `bash`, `sh` and
  other programs are never hidden by a folder named before `PATH` or by a folder a script puts first on PATH.
- **`PATH`**: the shell's PATH, the system PATH it was started with (`BASH_PATH` under the start file), as a script
  changes it, its own folder left out (looked in first already). Every entry is tried with every `PATHEXT` extension,
  so a name PATH lacks is probed in every entry before the next step. Measured on a machine with 49 PATH entries: a
  program in a folder named AFTER `PATH` took 6 to 8 ms to find the first time, 3 to 3.5 ms with the folder named
  before it. A program found is remembered (above), so later lookups take about 0.4 ms in any shell, in either
  order; a name found nowhere is searched in full each time it runs (about 4 ms there). So a folder you know holds the
  tools you use is quicker named before `PATH`.
- **`WSL`**: the tool run in WSL (`wsl.exe`), when no folder and no PATH program has it, wherever `WSL` is written:
  a start costs about 0.15 s, so it is always the last resort, after every folder and PATH. Windows-rooted
  arguments (`F:/x`) arrive as `/mnt/f/x`; a command WSL does not have either is "command not found", 127. A request
  handed on (an option one of C#Bash's own tools lacks, or a `.bash/bin` program's refusal, below) gets the answer it
  gets with no WSL step: the tool's refusal, status 2, or "does not serve this request", 127. It starts with
  `WSLENV` empty: your Windows variables are not passed to a fallback tool (your own `wsl` commands keep them).

The folders and `PATH` go in any order, separated by `;` (`WSL` is last wherever it is written); a step left out is
not used (leave out `WSL` and a command found nowhere is simply not found). **With no file, or no `unix-tools` line, the chain is `PATH;WSL`.** The file is read once
per shell, the first time a command needs looking up, never at startup: an edit applies from the next shell. A line
it cannot use is reported once on stderr and skipped. `type`, `command -v` and `hash` see the folders, as they see
PATH; they never report WSL, which has no file to name. Lookups are remembered per chain, so a changed folder list
never runs a remembered program from a folder you removed. The installer does not write the file: create it when you
want a different chain.

**A program in `%USERPROFILE%/.bash/bin` that cannot serve a request hands it on** (the refusal protocol, since build
864). The program decides before it reads its input or writes anything (reading files to decide is fine), and if it cannot
serve the request it exits **127** with nothing on stdout or stderr (it may log its reason to
`%USERPROFILE%/.bash/unserviced.jsonl`). C#Bash then runs the same command again from scratch, with the same
arguments, redirections and unread input, using the next program of that name in the chain above (`.bash/bin` left
out), WSL included when it is a step; with none (WSL lacking it too) it says `<name>: <program> does not serve this request, and
unix-tools has no other` and the status is 127. Any other exit status is the program's answer, an error included, and
is never run again. This applies to every program in that folder, yours included, but C#Bash's own `bash` and `sh`,
whose 127 is a script's "command not found", an answer: Wombat's `git.exe` (a git replacement for Windows, not yet
published) follows it; a program that exits 127 for
another reason is run again, which is its owner's to avoid. **C#Bash marks the calls it will run again:** the program
has `BASH_RERUN_ON_127=1` in its environment (unless the mark is written on the command, below) only when it was found
by a bare name in that folder and its input can be
handed to the next one (an empty input always can: a `< /dev/null` on a group or on `eval`, as Claude Code runs most
calls, or a closed stdin, gives the program NUL; when it cannot be, the program is not marked; if it exits 127 anyway, C#Bash says `<name>:
<program> does not serve this request; not run again:
<why>`); every other program C#Bash starts has the variable removed, an inherited or exported one
included, unless the mark is written on the command itself (below). A program can so refuse in silence where it has the mark, and say why, as an ordinary error, where it has
not (Wombat: exit 128 and `fatal: wombat does not support '<what>' yet`), as for cmd, an editor or a call by path.
To test a program against the protocol, write the mark on the command itself: `BASH_RERUN_ON_127=1 ./prog` (or
through `env`) reaches the program as written, by path too, and `BASH_RERUN_ON_127=0 prog` turns C#Bash's own off.

The same file holds **`access-time = default | preserve`**. On NTFS, reading a file's details moves its access time
(`stat` and `ls -l` read a file's first bytes for `#!`, as MSYS does, since Windows has no execute bit), as under Git
Bash; `preserve` makes those reads leave it alone, so `ls -u` and `find -atime` mean "last really read". A real read
(`cat`) moves it either way. With no line, `default`.

**WSL reads standard input**, even for a tool that does not: inside `while read …; do …; done`, a command run in WSL
takes the loop's remaining lines (measured). Give it `< /dev/null` there.

## Help points at the manuals

`help NAME` and every builtin's `NAME --help` print the manual page(s) of what was
asked (GNU's, or the manual of the program a builtin imitates), from `%USERPROFILE%/.bash/help.json`: written from
the built-in table when it is missing; your entries win name by name, the built-in table answers for the rest. A
bash builtin or keyword C#Bash does not implement (`ulimit`, `bind`, `select`, `coproc`...) says so beside its page.
`help` matches as bash's does (an exact name or glob, else every name it begins). A `help.json` that is still exactly
the table an earlier build wrote, never edited, is replaced by the current one the next time `help` reads it (and
says so); one you have edited is never touched.

## Requests C#Bash does not service are logged

One JSON line each, in `%USERPROFILE%/.bash/unserviced.jsonl`: an
unsupported option (whether a PATH program then ran it or not), a feature that is not available, a construct refused
when parsed, a bash builtin C#Bash lacks, a help topic not implemented or not found. It is the list of things to
consider implementing. The full arguments are kept, in your profile, as `~/.bash_history` keeps commands; nothing is
sent anywhere. `BASH_UNSERVICED_LOG` names another file; `/dev/null` turns the log off (the test runners do).

Beside it, the installer puts `KNOWN_ISSUES.md`: the known issues of the build it installs (possible bugs, being
worked on, and what was fixed since the release before), and `KNOWN_LIMITATIONS.md`: where C#Bash differs from bash by
Windows' limits or by design, each with the way that works. Their first lines name that build; they are the repository's
[`KNOWN_ISSUES.md`](../KNOWN_ISSUES.md) and [`KNOWN_LIMITATIONS.md`](../KNOWN_LIMITATIONS.md) as of that release.
