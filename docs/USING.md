# Using C#Bash

The [README](../README.md) has the short version. This is the whole of it: getting C#Bash, making it Claude Code's
shell, how it starts and finds commands, and how its help and its log of unsupported requests work.

## Using it as Claude Code's shell

Claude Code on Windows needs *a* `bash.exe`; it finds Git for Windows by default, or whatever `CLAUDE_CODE_GIT_BASH_PATH` names. C#Bash is built to be that shell (with or without Git installed):

To skip building it: run the installer (README, "Use it with Claude Code"; from [Releases](https://github.com/Klinkenbecker/csharpBash/releases), with its `.sha256`), which puts `Bash.exe` in `%USERPROFILE%/.bash/bin`, and go to step 2. It is not code-signed, so Windows may warn about it the first time. Since 2026-11-07 a release carries only the installer; up to hg-657 it also carried `Bash.exe` on its own.

1. Publish the Native AOT build somewhere the build never touches:
   ```cmd
   tools\publish-aot.cmd            REM -> dist\Bash.exe, ~7.6 MiB
   ```
   One static executable. No .NET runtime on the target, and **~9 ms startup, against Git Bash's ~23 ms as Claude Code starts it** — which matters because Claude Code spawns a shell for *every* tool call. The script exists because AOT needs two things on the *build* machine that are easy to get wrong: the MSVC C++ toolchain (`vcvars64.bat`) and `vswhere.exe` on PATH, which the vcvars script does not add. Read the comment at the top of it before concluding your toolchain is missing.

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
2. Set the variable for your user, then start Claude Code from a NEW terminal (`setx` reaches only programs started
   afterwards):
   ```powershell
   setx CLAUDE_CODE_GIT_BASH_PATH "C:/path/to/Bash.exe"
   ```
   Claude Code runs that shell even when another `bash.exe` (WSL's launcher in System32, Git's) comes first on PATH:
   measured with Claude Code 2.1.316 on 2026-11-03. Earlier versions ignored the variable and took the first
   `bash.exe` on PATH (Claude Code issue #94077); without the variable, that is still what happens. Check with
   `! bash --version` in a session: its last line must read `C#Bash build …`.
3. To go back to Git Bash, remove the variable (`setx` to an empty value, or System Properties → Environment Variables) and restart Claude Code.

### Start Claude Code with no PATH of its own

Claude Code builds a snapshot of your shell when a session starts and replays its PATH into every Bash call. With C#Bash the PATH's length no longer costs a call anything (lookups are remembered), but `claude.exe` itself has no need to search your PATH either. This start file keeps it narrow and hands the full PATH to the shell through `BASH_PATH`:

```bat
@echo off
rem claude.cmd: Claude Code with no tool PATH of its own; C#Bash gets the full PATH through BASH_PATH
rem Adjust the two C#Bash paths, and claude.exe's if your installer put it elsewhere.
setlocal
set "BASH_PATH=%PATH%"
set "CLAUDE_CODE_USE_POWERSHELL_TOOL=0"
set "CLAUDE_CODE_GIT_BASH_PATH=C:/path/to/Bash.exe"
set "PATH=C:/path/to"
"%USERPROFILE%/.local/bin/claude.exe" %*
endlocal
```

`setlocal` keeps the narrowed PATH out of the console you start it from, and `%*` passes your arguments (`--resume`, …) through. `CLAUDE_CODE_USE_POWERSHELL_TOOL=0` stops Claude Code reaching for PowerShell before bash. The installer's `claude.cmd` is this file, finding its own folder. Anything `claude.exe` starts by itself rather than through the shell (the status line, hooks, MCP servers) then sees only that narrow PATH, so give those commands full paths: `C:/Windows/py.exe statusline.py`, not `py statusline.py`. `bash` alone is the exception: that PATH is C#Bash's own folder, so `bash statusline.sh` finds C#Bash.

To keep typing `claude` without putting C#Bash's folder on your PATH, give a folder that IS on your PATH a one-line `claude.bat` that hands off to the installed start file (the development machine starts this way since 2026-11-07):

```bat
@echo off
"%USERPROFILE%/.bash/bin/claude.cmd" %*
```

Each install rewrites `claude.cmd`, so the hand-off always starts the installed build; the installer never touches `claude.bat`.

### Getting the best out of it

- **Write paths with forward slashes** (`C:/x/y` or `/c/x/y`). A backslash is the shell's escape character, and a Windows program handed `/c/x` gets `C:/x`, as under Git Bash (a colon list, `/c/x:/f/y`, gets `C:/x;F:/y`; any other argument with a colon is passed as typed); pass a switch to `cmd` as `cmd //c …`, except under `MSYS_NO_PATHCONV=1` or `MSYS2_ARG_CONV_EXCL='*'`, which turn the `//` escape off with the rest: write `cmd /c` there (as in Git Bash). The same goes inside an argument after the first `=` and after `-` and one letter (`--file=/c/x` → `--file=C:/x`, `-I/c/x` → `-IC:/x`), and for the program's environment (`X=/c/x prog`, or an exported `X`, reaches it as `C:/x`); a word holding `;` is passed whole, a bare `/c` or `/` as typed. `MSYS_NO_PATHCONV=1` turns all of it off, `MSYS2_ARG_CONV_EXCL` (`*` or `;`-separated prefixes) the arguments, `MSYS2_ENV_CONV_EXCL` (prefixes of `NAME=value`) the environment, as in Git Bash. A nested `bash` (this shell) and an MSYS program get the environment as typed.
- **Write files with LF line ends**, not CRLF. Every tool C#Bash provides writes LF, and every bash tool expects it.
  C#Bash copes with a Windows program's CRLF (a line tool reads it as the line end, `$( )` drops a trailing one),
  but text that keeps its CRs is text every later step has to cope with. A log, a script, a data file you write
  yourself: LF.
- **There is no terminal.** `/dev/tty` reports "No such device or address" and a program that would prompt on one fails instead of hanging; give tools their input on stdin or their non-interactive flags.
- **After installing a tool**, run `hash -r`.
- **Reproduce from a script file, not through PowerShell.** Windows PowerShell 5.1 strips the inner double quotes when it hands `-c 'echo "x (y) z"'` to a native executable, so the shell receives `echo x (y) z` — and then correctly reports a syntax error. A report observed only through `& Bash.exe -c "..."` from PowerShell measures PowerShell's quoting, not the shell (2026-09-05, one full false report).
- **Quote the build in a bug report.** `Bash.exe --version` prints a third line, `C#Bash build 1.0.0+hg.<hash> <rev>`, stamped from the Mercurial revision at build time (a trailing `+` means an uncommitted tree).
- **Measure a call yourself** with `BASH_TIMING_LOG=<file>`: the shell appends where its time went (start, set-up, each `source`, `eval` and external program, exit) to that file when it exits. It costs about 2 ms a launch while set, nothing when not.

Claude Code's per-command wrapper, its shell snapshot (`bash -c -l`), cwd tracking, heredocs, process substitution, `timeout`, and the `rg`/`pkill` shims it defines have all been verified end to end, both with Git on PATH and on a PATH with no Git at all ([`DECISIONS.md`](../DECISIONS.md), 2026-09-04, "P5 verified: Claude Code driven by C#Bash on a Git-less PATH").

## Startup and command lookup

**Why it matters on Windows: finding a program is slow, and not finding one is slowest.** A bare command name is looked up by trying every PATH entry with every `PATHEXT` extension, each try a file-system probe; a hit stops early, but a miss makes every probe and fails them all. On the machine this was measured on, 51 PATH entries × 12 names is about 600 probes, **~15 ms for one miss**, 8.8 ms of it two entries on a slow drive. Claude Code's snapshot makes exactly such a miss (`hash rg`) on every Bash call, so it was half of what each call cost. So C#Bash controls where its PATH comes from and remembers what it finds, misses included, as below; the measurements are in [Performance](PERFORMANCE.md) ("What a Claude Code call costs") and in [`DECISIONS.md`](../DECISIONS.md) ("REVIEW R1 RESULT").

**At startup**, in this order:

1. The environment is imported; Windows' `Path` becomes `PATH`. **`$PATH` has two forms.** Inside the shell it reads in GNU/MSYS form, `:`-separated, `/c/x:/f/y`, whatever form it arrived in, so `IFS=:` and the POSIX idioms work as in any bash. An assignment may use that form, Windows form (`C:/x;F:/y`, either slash) or both mixed (`PATH="$PWD/bin:$PATH"`): it is converted on the way in. Empty entries stay as given; a one-letter relative entry is written `./A`, because `A:/x` reads as drive A's `/x`. Every program the shell starts gets PATH in Windows form with forward slashes, `C:/x;F:/y`: never a backslash, never an empty entry. `$PWD` and `$HOME` stay `C:/x`, so a test such as `case ":$PATH:" in *":$HOME/bin:"*)` does not match; compare in one form.
2. **`BASH_PATH`**, if set and not empty, becomes `PATH` and is then removed, from the process environment too, so a nested shell keeps its parent's PATH. It lets a launcher keep its own PATH narrow and still hand the shell a full one (see [the start file](#start-claude-code-with-no-path-of-its-own) above).
3. The shell's own directory is put FIRST on its PATH (moved there if it is already further on), so a nested `bash`, an `sh`, and the `sh` a program it starts looks up (make) are this shell, even when another `bash.exe` or `sh.exe` (WSL's launcher in System32, Git's) comes first on the PATH it was given. Only the shell's own PATH and its children's change; the system and user PATH are untouched. (Until 2026-11-03 it was added at the end, so another bash won.)
4. `HOME` (forward-slash form; the Windows profile when unset), `BASH`, `SHELL`, `SHLVL`, `UID`, `OSTYPE=msys` and the other bash variables are set; `PYTHONIOENCODING=utf-8` is exported when unset, so a piped Python prints UTF-8 instead of crashing on `✓`. Output is UTF-8, with or without a console.
5. Startup files: a login shell (`-l`) reads `/etc/profile`, then the first of `~/.bash_profile`, `~/.bash_login`, `~/.profile`; an interactive shell reads `~/.bashrc` (or `--rcfile`); a non-interactive one (`-c`, a script, every Claude Code call) reads only `$BASH_ENV`.

**Finding a command**, in this order: an alias, a keyword, a function, a builtin or embedded tool; a name with a slash (or a drive, `F:x`) is that path, from the shell's own directory and never searched for; anything else is searched along [the fallback chain](#the-fallback-chain) (by default PATH, then WSL), trying each folder as named and then with every `PATHEXT` extension (`.COM`, `.EXE`, `.BAT`, `.CMD`, …), and taking the first match that can run (a program Windows starts, `.com` `.exe` `.bat` `.cmd`, a `.sh` file, or a `#!` line: what `[ -x ]` says). A `.js`, `.vbs` or other `PATHEXT` file that runs only through a file association is passed over, as Git Bash does. A `#!` script runs by its interpreter line (bash and sh scripts in-process), found on PATH or by its path; a `.sh` file without one runs in-process too, and so does any other text file with no `#!` named by its path (`./build`), as bash and msys run one (binary content is refused: "cannot execute binary file: Exec format error"); found on PATH by a bare name, such a file is not run. `/bin/NAME` and `/usr/bin/NAME`, when the drive has no such folder, run `NAME` (a builtin first, then PATH), as a `#!` line does. A bare name never runs a script from the current directory: write `./script`, as in bash.

- **`sh` is the shell under the name `sh.exe`**, beside `Bash.exe` (the installer and deploy make it a hard link; by hand a copy works), as Git for Windows and MSYS2 ship theirs (each one's `sh.exe` is byte-for-byte its `bash.exe`). Step 4 above puts that folder on PATH, so `sh -c`, `sh script`, `… | sh`, `xargs sh -c` and `find -exec sh -c` find it, and so do the programs the shell starts (`make`, say); something started outside C#Bash needs the folder on its own PATH. Started as `sh`, it runs bash's default mode, as a `#!/bin/sh` script does: POSIX mode is not available, and nothing is printed about it. Its messages name it by its path (`$0` is the path it was started by). `tools/deploy.sh` makes `dist/sh.exe` a hard link to `dist/Bash.exe` (since 2026-11-01; one file on disk, re-made at every deploy so it never keeps an old build). With only `Bash.exe` downloaded, make it yourself: a copy, or a hard link (`ln Bash.exe sh.exe` in C#Bash), made again after each new `Bash.exe`, since a link keeps the build it was made to.
- **Lookups are remembered across shells**, in `~/.bash/commands`, one small file per PATH value. A remembered program is used after one existence check, so a moved or deleted one is simply searched for again. A program installed later in a folder earlier on PATH stays hidden behind the remembered one until `hash -r`.
- **`hash name` also remembers a miss.** In a later shell it answers `hash: x: not found (remembered miss: hash -r searches again)` without searching. Running a program, `command -v` and `type` always search, as bash does. After installing a tool you had probed with `hash`, run `hash -r` (or `hash -d name`).
- **Windows limits that remain:** a program whose own path is over 260 characters, or one started from a directory over 258 characters, cannot be started (cmd cannot either; C#Bash says so, 126), though the shell itself can `cd` there and work with its files. A file name ending in a dot or a space cannot exist through Windows' file API, which drops it: a command name `s1.` finds nothing, but `cat s1.` reads `s1`.
- **When the shell's own search finds nothing, C#Bash looks in three folders only**, then WSL if the chain has it: its own, System32 and the Windows folder (`.exe` appended, as Windows' launcher would). So `cmd` runs even on an empty PATH, while `command -v cmd` reports nothing. Never the current directory: Windows' own launcher searched it (in an ordinary terminal, or through an empty PATH entry), so a `.exe` planted there could answer a command that is not installed.

### The fallback chain

A command C#Bash does not provide itself, or a C#Bash tool given an option it does not have, is looked for in these
steps, in the order `%USERPROFILE%/.bash/etc/csharpBash.config` gives:

```
# %USERPROFILE%/.bash/etc/csharpBash.config
unix-tools = F:/Tools/UnixUtils/usr/local/wbin;PATH;WSL
```

- **A folder** of Unix tools: searched like a PATH entry, so a tools folder can come first for the shell without
  touching the Windows PATH. Any number, each an absolute path.
- **`PATH`**: the shell's PATH.
- **`WSL`**: the tool run in WSL (`wsl.exe`), when nothing earlier has it. Windows-rooted arguments (`F:/x`) arrive
  as `/mnt/f/x`; a tool WSL does not have either is "command not found", 127. A start costs about 0.15 s. It starts
  with `WSLENV` empty: your Windows variables are not passed to a fallback tool (your own `wsl` commands keep them).

The steps go in any order, separated by `;`; a step left out is not used (leave out `WSL` and a command found
nowhere is simply not found). **With no file, or no `unix-tools` line, the chain is `PATH;WSL`.** The file is read once
per shell, the first time a command needs looking up, never at startup: an edit applies from the next shell. A line
it cannot use is reported once on stderr and skipped. `type`, `command -v` and `hash` see the folders, as they see
PATH; they never report WSL, which has no file to name. Lookups are remembered per chain, so a changed folder list
never runs a remembered program from a folder you removed. The installer does not write the file: create it when you
want a different chain.

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
