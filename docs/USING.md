# Using C#Bash

The [README](../README.md) has the short version. This is the whole of it: getting C#Bash, making it Claude Code's
shell, how it starts and finds commands, and how its help and its log of unsupported requests work.

## Using it as Claude Code's shell

Claude Code on Windows needs *a* `bash.exe`; it finds Git for Windows by default, or whatever `CLAUDE_CODE_GIT_BASH_PATH` names. C#Bash is built to be that shell (with or without Git installed):

To skip building it: download [the latest `Bash.exe`](https://github.com/Klinkenbecker/csharpBash/releases/latest/download/Bash.exe) and its `Bash.exe.sha256` from [Releases](https://github.com/Klinkenbecker/csharpBash/releases), compare the two (`certutil -hashfile Bash.exe SHA256`), put it in a folder of its own, and go to step 2. It is not code-signed, so Windows may warn about it the first time.

1. Publish the Native AOT build somewhere the build never touches:
   ```cmd
   tools\publish-aot.cmd            REM -> dist\Bash.exe, ~6.9 MB
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
- **After installing a tool**, run `hash -r`.
- **Reproduce from a script file, not through PowerShell.** Windows PowerShell 5.1 strips the inner double quotes when it hands `-c 'echo "x (y) z"'` to a native executable, so the shell receives `echo x (y) z` — and then correctly reports a syntax error. A report observed only through `& Bash.exe -c "..."` from PowerShell measures PowerShell's quoting, not the shell (2026-09-05, one full false report).
- **Quote the build in a bug report.** `Bash.exe --version` prints a third line, `C#Bash build 1.0.0+hg.<hash> <rev>`, stamped from the Mercurial revision at build time (a trailing `+` means an uncommitted tree).
- **Measure a call yourself** with `BASH_TIMING_LOG=<file>`: the shell appends where its time went (start, set-up, each `source`, `eval` and external program, exit) to that file when it exits. It costs about 2 ms a launch while set, nothing when not.

Claude Code's per-command wrapper, its shell snapshot (`bash -c -l`), cwd tracking, heredocs, process substitution, `timeout`, and the `rg`/`pkill` shims it defines have all been verified end to end, both with Git on PATH and on a PATH with no Git at all ([`PROJECT_CONTEXT.md`](../PROJECT_CONTEXT.md), P5).

## Startup and command lookup

**Why it matters on Windows: finding a program is slow, and not finding one is slowest.** A bare command name is looked up by trying every PATH entry with every `PATHEXT` extension, each try a file-system probe; a hit stops early, but a miss makes every probe and fails them all. On the machine this was measured on, 51 PATH entries × 12 names is about 600 probes, **~15 ms for one miss**, 8.8 ms of it two entries on a slow drive. Claude Code's snapshot makes exactly such a miss (`hash rg`) on every Bash call, so it was half of what each call cost. So C#Bash controls where its PATH comes from and remembers what it finds, misses included, as below; the measurements are in [Performance](PERFORMANCE.md) ("What a Claude Code call costs") and in [`DECISIONS.md`](../DECISIONS.md) ("REVIEW R1 RESULT").

**At startup**, in this order:

1. The environment is imported; Windows' `Path` becomes `PATH`.
2. **`BASH_PATH`**, if set and not empty, becomes `PATH` and is then removed, from the process environment too, so a nested shell keeps its parent's PATH. It lets a launcher keep its own PATH narrow and still hand the shell a full one (see [the start file](#start-claude-code-with-no-path-of-its-own) above).
3. When `git.exe` is on PATH, Git for Windows' `usr/bin` and `mingw64/bin` are put in front of it, exactly as Git Bash does, so `tar`, `perl`, `ssh` and any tool outside the embedded set resolve the same way.
4. The shell's own directory is added at the end of PATH, so a nested `bash` always resolves, Git or no Git.
5. `HOME` (forward-slash form; the Windows profile when unset), `BASH`, `SHELL`, `SHLVL`, `UID`, `OSTYPE=msys` and the other bash variables are set; `PYTHONIOENCODING=utf-8` is exported when unset, so a piped Python prints UTF-8 instead of crashing on `✓`. Output is UTF-8, with or without a console.
6. Startup files: a login shell (`-l`) reads `/etc/profile`, then the first of `~/.bash_profile`, `~/.bash_login`, `~/.profile`; an interactive shell reads `~/.bashrc` (or `--rcfile`); a non-interactive one (`-c`, a script, every Claude Code call) reads only `$BASH_ENV`.

**Finding a command**, in this order: an alias, a keyword, a function, a builtin or embedded tool; a name with a slash (or a drive, `F:x`) is that path, from the shell's own directory and never searched for; anything else is searched on PATH, trying each entry as named and then with every `PATHEXT` extension (`.COM`, `.EXE`, `.BAT`, `.CMD`, …), and taking the first match that can run (a program Windows starts, `.com` `.exe` `.bat` `.cmd`, a `.sh` file, or a `#!` line: what `[ -x ]` says). A `.js`, `.vbs` or other `PATHEXT` file that runs only through a file association is passed over, as Git Bash does. A `#!` script runs by its interpreter line (bash and sh scripts in-process), found on PATH or by its path; a `.sh` file without one runs in-process too. A bare name never runs a script from the current directory: write `./script`, as in bash.

- **Lookups are remembered across shells**, in `~/.bash/commands`, one small file per PATH value. A remembered program is used after one existence check, so a moved or deleted one is simply searched for again. A program installed later in a folder earlier on PATH stays hidden behind the remembered one until `hash -r`.
- **`hash name` also remembers a miss.** In a later shell it answers `hash: x: not found (remembered miss: hash -r searches again)` without searching. Running a program, `command -v` and `type` always search, as bash does. After installing a tool you had probed with `hash`, run `hash -r` (or `hash -d name`).
- **Windows limits that remain:** a program whose own path is over 260 characters, or one started from a directory over 258 characters, cannot be started (cmd cannot either; C#Bash says so, 126), though the shell itself can `cd` there and work with its files. A file name ending in a dot or a space cannot exist through Windows' file API, which drops it: a command name `s1.` finds nothing, but `cat s1.` reads `s1`.
- **When the shell's own search finds nothing, C#Bash looks in three folders only**: its own, System32 and the Windows folder (`.exe` appended, as Windows' launcher would). So `cmd` runs even on an empty PATH, while `command -v cmd` reports nothing. Never the current directory: Windows' own launcher searched it (in an ordinary terminal, or through an empty PATH entry), so a `.exe` planted there could answer a command that is not installed.

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
