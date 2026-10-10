# Project Context: Bash

> ## RESUME ANCHOR (rewritten 2026-10-08) — read this before anything else
> Replaces the anchor kept from 2026-09-04 to 2026-10-07, whose dated blocks had piled up: resolved
> items were still listed as open, and the 2026-10-07 handoff missed one open item it carried. The
> old text is in hg rev 140's `PROJECT_CONTEXT.md`; every resolved item in it has a DECISIONS entry.
> Checked against the session transcripts of 2026-09-03 to 2026-10-07 on 2026-10-08.
>
> **TRUE STATE (verified 2026-10-10):**
> - hg: rev 179 (deployed), branch `default`. GitHub `main` = git `738ec92` = hg rev 140; a push is
>   in progress (the architect, 2026-10-10: "Let's also push at this time ... Push means another
>   benchmark and a doc review first").
> - **DEPLOYED 2026-10-10: `dist/Bash.exe` is rev 179** (`--version`: `1.0.0+hg.e6a9a866b65a+ 179+`):
>   everything below, revs 158-179 (22 fixes). Suite 101/101 on that exact AOT binary, with a console
>   and `-Detached`; live-verified in a Claude Code Bash call: the notice's 19 examples matched GNU
>   bash (the shell's own path aside). Rev 179 was found BY that check: a syntax error at the end of a
>   text with no final newline was a line early; rev 178 was installed for minutes, never announced.
>   Rollbacks: `dist/Bash.exe.rev156` (the previous deploy), `.rev178`, `.rev150`, `.rev143`, back to `.rev80`.
> - Running sessions told: 2026-10-08 `df`; 2026-10-09 revs 146-150; 2026-10-10 revs 152-156 (to
>   daq-d1, adc-framework-d8, msp430-a2, nupkg-28, koliada-net-f7); 2026-10-10 revs 158-179 to daq-d1,
>   adc-framework-d8, msp430-a2, nupkg-28, errorlogs-42 and koliada-net-6a, leading with three
>   behaviour changes (`while read` loops, the error prefix, the stricter parser). nupkg-28 confirmed
>   its awk report fixed on rev 179; adc-framework-d8 re-tested clean (loops, stderr capture); neither
>   had a workaround to drop. The architect, 2026-10-10: "koliada-net
>   is a new session" (koliada-net-6a; whether it needs koliada-net-f7's items is asked, not assumed).
> - Benchmark: `tests/bench/compare3.sh`, method frozen, every launch made and timed by
>   `tools/benchtimer` (DECISIONS 2026-10-06, both entries). 2026-10-10, quiet machine, rev 179: startup
>   C#Bash 0.0160 s / Git Bash 0.0157 / WSL 0.1098. A "+1.2 ms vs rev 156" from two single runs was
>   NOISE: an interleaved diagnostic put rev 179 0.44 ms faster (DECISIONS "the +1.2 ms startup …
>   was the benchmark's own noise"). The startup row is one sample, +-1.5 ms between runs. Before:
>   rev 140, C#Bash 0.0152 s /
>   Git Bash 0.0162 / WSL 0.1036. The README table is that run.
> - Other installs, not checked since: GUY-WINDOWS11 `C:/tools/bin/Bash.exe` was rev 60 (2026-09-08);
>   a copy went to the share at rev 76 (2026-09-12).
> - Untracked `The Windows Shell Tax.url` (a claude.ai artifact link, there since 2026-09-14): in
>   neither VCS, not ours, leave it.
> - Nothing in flight. Revs 158-179 are deployed; the push (benchmark, doc review) follows.
>
> **GREENLIT (2026-10-04: "Yes, then 2, 3 and 5 (commit between stop for ambiguities/forks). then
> we will discuss 4?"):**
> - Goal-2 backlog: 1 `time` DONE (rev 105) · 2 detached launch DONE (revs 113-114, 121, 123-124) ·
>   3 `jq` subset DONE (revs 130-133) · **5 `df` DONE (rev 142) and DEPLOYED (2026-10-08).**
>   - In-process, GNU 8.32's format: `C:` / `//host/share` sources, `C:/` mount points (the
>     path-form rule). DECISIONS 2026-10-08. `tests/df/compare-wsl.sh`: 76/76 identical to GNU;
>     suite 72/72 both modes on the Release and the AOT build.
> - The architect, 2026-10-08: "let's get df done and finish the escape-space probe and then we
>   will start chomping through the bugs." Order: `df` (done) → the probe (done: a real defect,
>   fixed in rev 146, deployed in rev 150) → the OPEN DEFECTS below, one at a time on the architect's go.
> - Serial I/O (item 4: COMn as a device file plus `stty`; 55 calls, all MSP430): to discuss.
>
> **AWAITING THE ARCHITECT (do NOT derive; Claude's positions are proposals, not rulings):**
> 1. **MSYS paths in arguments to native programs** (raised 2026-09-11 in rev 67, called the highest
>    priority since). `/tmp` means two places in one session: builtins and redirects map `/tmp` to
>    `%TEMP%`, while a native program gets `/tmp/x` or `/c/x` verbatim and resolves it against the
>    current drive. Re-verified on rev 136: `cmd.exe /c echo /tmp/x /c/Users/guy ~/y` prints
>    `/tmp/x /c/Users/guy C:\Users\guy/y`. Already on disk: `E:\c\Users\guy\`, `/tmp` content on C: and E:.
>    - **Question first:** on 2026-09-12, replying to a list that included this item, the architect
>      wrote "...backslash to forwardslash should be symetric across read/write operations. This
>      should include MSYS form mapping assuming /<d>/... as D:/...". Claude read that as the shell's
>      own paths only and built rev 71; native arguments stayed verbatim and went back on the open
>      list (2026-09-14, 2026-09-15, no reply). Did it cover native arguments too? If yes, this item
>      is ratified and half built.
>    - Claude's position (DECISIONS 2026-09-11, "Sharpened"): convert by shape, at the start of an
>      argument only, `//host/share/…`, `/<letter>/…`, `/tmp…`, `~…`; never a bare `/c` (`cmd.exe /c`);
>      an environment switch to turn it off. About half a day [estimate, not measured].
> 2. **UTF-8 for external programs** (koliada-net-dd, 2026-10-06). Piped `hg` and `python` write
>    cp1252; python crashes on `✓`.
>    - Claude's position: export `PYTHONIOENCODING=utf-8` and `HGENCODING=utf-8` at startup when
>      unset; not `PYTHONUTF8`, which also changes `open()`'s default for user scripts.
>    - koliada-net-dd withdrew its `PYTHONUTF8` suggestion and agrees with that position (peer
>      message 2026-10-06, which asked for its agreement to be recorded here).
>    - Doubt raised 2026-10-08 [recalled, not verified]: the 2026-10-06 position called both
>      variables stdio-only. `HGENCODING` probably also decodes commit messages, usernames and branch
>      names, including `hg commit -l` files. Test before building: a non-ASCII message via `-l` and a
>      non-ASCII filename, under both settings, in a scratch repo.
> 3. **awk scope requests** (installer-79, 2026-09-08): `print > file` and `close()`, 3-argument
>    `match()`, user functions, `awk --version`. (`df`, from the same list, is greenlit above.)
> 4. **Missing builtins `umask`, `times`, `fc`** (found 2026-10-10, measured): each is "command not
>    found" (status 127), and `fc` reaches Windows' `C:/Windows/System32/fc.exe` instead ("FC:
>    Insufficient number of file specifications"). `umask 077` before creating files is a common
>    script line, so a script that uses it stops working.
>    - Claude's position [proposal]: `umask` as a builtin that stores and prints the mask (`umask`,
>      `umask -S`, `umask -p`, octal and symbolic modes) with NO effect on file creation, since
>      Windows has no permission bits for it to clear, and that limit stated in the README. `times`
>      prints the shell's and its children's user and system times (the process times are available).
>      `fc` reports "not supported" instead of running fc.exe. Falsifier: if the architect wants
>      `umask` to deny writes through ACLs, it is a bigger, separate design.
> 5. **`strings`** (koliada-net-f7, 2026-10-10, one hit, not blocking): a scope request. Also
>    **`jq -R`** (msp430-a2, 2026-10-10: `jq -Rs '{text: .}' file` wraps a text file as a JSON string;
>    it used a C# path instead, not blocked): raw input is outside the jq subset.
> 6. **Parse-and-run one command at a time, as bash does?** (measured 2026-10-10, DECISIONS "syntax
>    errors in C#Bash's own words", Not done). bash reads, parses and runs each top-level command
>    in turn. So the lines before a syntax error RUN, and a syntax error inside `$( )` is reported at
>    run time (`name: command substitution: line N+1:`), fails only that command (1), and the script
>    goes on. C#Bash parses the whole text first and runs nothing when any of it is wrong.
>    - Claude's position [proposal, provisional]: match bash, since scripts with a late error
>      behave differently today. It is an architectural change to the script runner (parse a
>      command, run it, continue), and it also decides when an `alias` defined in a script takes
>      effect. Falsifier: if the architect values "a script with a syntax error runs nothing" as a
>      safety property, keep the current model and document it.
>    - Do NOT derive: this is the architect's call.
>
> **FIXED AND DEPLOYED (2026-10-09, in rev 150; suite 76/76 both modes on the Release and the AOT
> build, battery 227/237 with no regression):**
> - rev 146: literal text was field-split. `a\ b/x*` gave `a` and `b/x*` (any glob character,
>   matched or not); an IFS character in literal text split the word; `~` split when the home
>   directory has a space. DECISIONS "literal text was field-split"; case `literal_no_split`.
> - rev 147: `local a=$1` (and `declare typeset export readonly`, `name+=`, `name[i]=`) split the
>   value. DECISIONS "a declaration builtin's `name=$v` argument was field-split"; case
>   `declaration_assign`.
> - rev 148: the word of `${x:-word}` / `${x:+word}` lost its quoting context: `"${v:+$v }"` lost
>   its space, `${v:+-o "$w"}` split `"$w"`, `${1+"$@"}` broke spaced arguments (ten of 22 GNU
>   cases wrong). DECISIONS "the word of `${x:-word}` / `${x:+word}` lost its quoting context";
>   case `operator_word`.
> - rev 150: assignment values never expanded a tilde (`x=~/y`, `PATH=$PATH:~/bin`, `local y=~/z`,
>   `a[0]=~/a`). DECISIONS "assignment values never expanded a tilde"; case `assignment_tilde`.
>
> **FIXED AND DEPLOYED (2026-10-10, in rev 156; suite 80/80 both modes on the Release and the AOT build, battery 227/237, no regression):**
> - rev 152: a declaration with no value left the variable set: `local j` was `""`; `export m` on an
>   unset m gave children `m=""`; an array without element 0 read as set. DECISIONS "a declaration
>   with no value left the variable set"; case `declared_unset`.
> - rev 153: `set -u` is enforced (was accepted, never applied), with bash's exits (`-c` 127, or 1
>   where set -e is in force; script 1; `( )`, `$( )`, a pipeline stage, a heredoc fail only
>   themselves); and the word of `${x:-word}` is expanded only when selected (`${x:-$(cmd)}` ran
>   cmd with x set). DECISIONS "`set -u` (nounset) is enforced"; case `nounset`.
> - rev 154: `$HOME`, `~`, `$BASH` and `command -v`/`type`/`which`/`hash` output were backslash paths;
>   now the shell's forward-slash form. DECISIONS "`$HOME`, `~`, `$BASH` and `command -v` … gave
>   backslash paths"; case `shell_paths`.
> - rev 156: `env`'s options did not reach the command (koliada-net-f7's report): a non-identifier
>   NAME=VALUE (`STDOUT.OutputLevel=trace`) was run as the command; `-u` and `-i` missed builtins
>   (and `-u` programs too); `-uNAME`, `--unset=`, `-`, `-0` refused or ignored. DECISIONS "`env`'s
>   options did not reach the command"; case `env_options`. koliada-net-f7 told 2026-10-10.
>
> **FIXED AND DEPLOYED (2026-10-10, in rev 179; suite 101/101 both modes on the Release and the AOT build, battery 227/237 against baseline 226 (199 left it, rev 167; 187 joined, rev 168), no regression):**
> - rev 158: an unquoted `\\` in a `case` / `[[ == ]]` / `[[ =~ ]]` pattern did not stand for a
>   backslash (`*\\*` matched `a*`). DECISIONS "an unquoted `\\` in a pattern did not stand for a
>   backslash"; case `backslash_pattern`.
> - rev 159: an MSYS program (Git's usr/bin tools) under `>>` or the shell's own redirect wrote
>   nothing ("Bad file descriptor"): it now gets a write handle at the end, Windows programs keep the
>   atomic append handle. DECISIONS "an MSYS program wrote nothing under `>>`"; case `msys_redirects`.
>   nupkg-28 reported it independently (2026-10-10, awk `getline < file` with `> out`); its repro
>   passes on rev 163. Tell nupkg-28 at the deploy.
> - rev 160: PATH lookups printed PATHEXT's spelling (`git.EXE` for `git.exe`); `hash` had no hits
>   table and ignored `-t`/`-l`/`-d`. DECISIONS "PATH lookups printed PATHEXT's spelling"; case
>   `command_lookup`.
> - rev 161: a stray `fi`/`done`/`esac`/`then`/`do`/`else`/`elif` ran as a command (127); now a syntax
>   error, status 2, and `bash -n` reports it. DECISIONS "a stray `fi` … ran as a command"; case
>   `stray_keyword`. (Still different, separate: bash runs the lines BEFORE a syntax error.)
> - rev 162: `date +%3N` printed 9 digits (%N width is a precision; `-`/`_` flags); `date -d @S.F`
>   was refused. DECISIONS "`date +%3N` printed 9 digits"; case `date_nanos`.
> - rev 163: `fold` counted `\r`/`\b`/tab as one column (now a port of GNU fold.c, bytes, `-s`);
>   `tee /dev/null` failed. DECISIONS "`fold` counted `\r` … ; `tee /dev/null` failed"; case `fold_tee`.
> - rev 164: a Windows exit code reached `$?` raw (-1, 256); now 0-255 as on Linux (low byte; NTSTATUS
>   crash = 128 + signal). DECISIONS "a Windows program's exit code reached `$?` unreduced"; case
>   `exit_status_range`.
> - rev 165: an unquoted `${…${…}…}` (`${1:-${HOME}}`, `${a#${b}}`) swallowed the REST OF THE SCRIPT
>   silently (the nested `${` was counted twice); this was Git's `__git_ps1_show_upstream` "failing
>   to parse". DECISIONS "an unquoted `${…${…}…}` swallowed the rest of the script"; case `nested_brace`.
> - rev 166: `${…}`, `$( )`, `$(( ))`, backquotes and quotes are matched by ONE function,
>   `Lexer.ConstructEnd` (bash's parse_matched_pair rule), replacing seven scanners; 13 of 42 audit
>   cases were wrong on rev 165 (`${u:-\}}`, `${a//\}/x}`, `${a:-{x}}`, `${u:-$(echo })}`, …). `{` / `}`
>   are ordinary words after the command name (`echo }` was a syntax error). DECISIONS "One construct
>   matcher"; case `brace_constructs`.
> - rev 167: FUNCNAME / BASH_SOURCE / BASH_LINENO are one frame stack as in bash: after `source` the
>   script was gone from BASH_SOURCE, inside a function it was empty, BASH_LINENO did not exist,
>   FUNCNAME was `(main)` at top level (Git Bash 4.4's quirk; probe 199 left the battery baseline).
>   Written lazily (no per-call cost). DECISIONS "FUNCNAME / BASH_SOURCE / BASH_LINENO were two
>   drifting lists"; case `call_stack`. Step 1 of the `<script>: line N:` prefix item.
> - rev 168: errors carry bash's prefix, `script: line N:` (BASH_SOURCE[0], else $0; `eval:` / `-c:`
>   input names on syntax errors), where every one said `bash:`; `eval` and `$( )` text (quoted, in a
>   heredoc, in `${ }`, in arithmetic) continues the caller's line numbers instead of restarting at 1.
>   DECISIONS "every error said `bash:`"; case `error_prefix`. The item's other half, "a redirect
>   error prints after its scope", does NOT reproduce on rev 167: 11 forms (subshell, group,
>   function, `$( )`, pipeline, loop; builtins and externals) match GNU. Closed.
>   **A behaviour change to lead the next deploy notice with:** every error message's first word
>   changes (`bash: x: command not found` becomes `script.sh: line 3: …`, or `$0: line 1: …` under
>   `-c`), so a session matching `bash:` in stderr must adjust.
> - rev 169: `ls dir missing` printed no `dir:` heading (a failed operand did not count), and
>   directory operands were not sorted (`ls plain 'a b'`). The escaped space of the 2026-10-08 item
>   was irrelevant. DECISIONS "`ls dir missing` printed no `dir:` heading"; case `ls_operands`.
> - rev 170: `trap` accepted any word as a signal (`trap x BADSIG`, `65`), stopped at the first bad
>   one, refused `RTMIN+3`; `trap -p` printed `INT` not `SIGINT`, in hash order, ignoring operands.
>   DECISIONS "`trap` accepted any word as a signal"; case `trap_signals`.
> - rev 171: builtins check options as bash does (message + bash's usage line, 2; options end at the
>   first operand): `printf -Z`, `cd -Z`, `export -Z`, `source -Z`, `pushd +3` and more took them
>   as operands; `kill -Z 1` signalled; `trap -- 'cmd' SIG` (the form `trap -p` prints) failed;
>   `source -- f` read `--` as the file. DECISIONS "builtins took bad options as operands";
>   case `builtin_options`.
> - rev 172: syntax errors in bash's words at bash's lines (``near unexpected token `fi'`` + the
>   line; `unexpected end of file`; ``looking for matching `)'``; the `[[ ]]` forms); grammar bash
>   rejects is rejected (`then fi`, `{ }`, `a &&` at the end, `f() echo x`, `[[ -f ]]`, an unclosed
>   backquote); heredoc-at-EOF warning. 912 bash-completion scripts under `-n`: no new false
>   rejections. DECISIONS "syntax errors in C#Bash's own words"; case `syntax_errors`.
> - rev 173: `while read -r f; do prog; done < list` ended after ONE line whenever any program ran in
>   the loop (even `prog < /dev/null`); in every build back to rev 156. A program now shares the
>   file offset, and a C#Bash child gives back its read-ahead. DECISIONS "`while read -r f; do prog;
>   done < list` ended after one line"; case `stdin_sharing`. **Lead the next deploy notice with
>   this one**: sessions may have worked around it.
> - rev 174: a UTF-8 character cut by a read's 4096-byte block or split pipe writes became two raw
>   bytes (`read`, pipes, `mapfile`, `while read`: a line one char too long). The 2026-10-03 inferred
>   item, now measured and fixed. DECISIONS "a UTF-8 character cut by a read's 4096-byte block";
>   case `utf8_boundary`.
> - rev 175: Linux's device files exist (/dev/null zero random urandom stdin stdout stderr fd/N tty)
>   as operands, redirects and under `test`: `cat /dev/null`, `head -c 5 /dev/zero`, `diff -u /dev/null
>   new`, `cp /dev/null f`, `[ -e /dev/null ]` all failed. `tr` streams; `od` prints the offset of an
>   empty input. DECISIONS "Linux's device files did not exist"; case `device_files`.
> - rev 176: `exec 3>&1` / `4>&2` / `3<&0` did nothing (`>&3` was "Bad file descriptor": the
>   stderr-capture idiom `exec 3>&1; out=$(cmd 2>&1 >&3)` failed); and `exec 1>&3 3>&-` disposed fd
>   1's stream (the shell then crashed on `4>&1`). DECISIONS "`exec 3>&1` … did nothing"; case
>   `fd_copy`.
> - rev 177: `wait -n` did not wait (job pools ran unbounded); `wait -p VAR` was ignored. DECISIONS
>   "`wait -n` did not wait"; case `wait_next`.
> - rev 178: `jobs` ignored -l -p -r -s and job specs (`jobs -p` printed whole lines), had its own
>   format, and listed nothing in a pipeline stage. DECISIONS "`jobs` ignored its options"; case
>   `jobs_list`.
>
> **OPEN DEFECTS (the architect, 2026-10-10: "go meant 'still open' gogogo": work through them in turn, commit between, stop for ambiguities/forks):**
> - Found 2026-10-10 while fixing the error prefix, verified against GNU (DECISIONS "every error
>   said `bash:`", Not done):
> - Found 2026-10-10 by the `builtin_options` work (DECISIONS "builtins took bad options …", Not
>   done): accepted options whose effect is not implemented: `jobs -x` (reported as unsupported), the
>   directory stack's `+N`/`-N` rotation (reported as not supported), `kill -l`/`trap -l`'s
>   numbered table (a short list of nine names is printed).
> - Inferred, not traced (2026-09-15): git's credential prompt runs through C#Bash and fails on `/dev/tty`.
>
> **KNOWN LIMITATIONS (documented, not raised for action):**
> - Glob and brace quoting is tracked per field, not per character: `{x\,y,z}`, `"*"*` and `\**`
>   treat the quoted metacharacter as live. A fix needs its own escape marker, not backslash.
> - A PIPE into a loop (`producer | while read; do prog; done`) is pumped into `prog`: what `prog`
>   does not read (a one-line `read`) is lost to the loop. Pipeline stages are in-process pipes, so
>   fixing it needs OS pipes between stages (DECISIONS "`while read …` ended after one line").
> - bash parses `[[ $x == @(a|b) ]]` with extglob off; C#Bash rejects it (69 of Ubuntu's 912
>   bash-completion scripts), part of extglob being unsupported (DECISIONS 2026-09-04).
> - bash numbers a simple command from the line where its second word ends, so an error inside a
>   multi-line `x=$(…)` is reported a few lines later in bash than in C#Bash (not imitated).
> - Under Claude Code, an external that reads stdin, in a command containing `<`, blocks: Claude Code
>   leaves stdin an open pipe. Give it `< /dev/null`. Not ours.
> - With no console, a child reading `CONIN$` waits invisibly (accepted 2026-09-14). The GUI-child
>   100 ms attach spin is unmeasured.
> - Loud by design: `>( )` is rejected at parse time; awk outside its subset, unsupported options and
>   `ps` fields beyond pid/ppid/comm/args fail loudly or fall through to a PATH tool.
> - A UNC path to a local drive is correct but very slow for tools doing many file operations (2026-09-11).
> - CRLF in script text is tolerated, on purpose.
> - The compat battery has no Git reference while Git's `bash.exe` is renamed: pass
>   `-Reference F:/Tools/PortableGit/usr/bin/_bash.exe`.
>
> **SETTLED, do NOT re-litigate:**
> - Goal order (DECISIONS 2026-10-03): 1 C#Bash for itself, on Windows; 2 no PowerShell for anything
>   not Windows-dependent (in scope if the COMMAND needs PowerShell today, out of scope if the OUTPUT
>   is Windows-specific); 3 a seamlessly faster Claude, achieved, so maintain it. Goal 3 against 1-2
>   "depends on circumstance": surface each collision with numbers. Git Bash is not a target.
> - Where behaviour is open, match GNU bash + coreutils on Linux (live reference: `wsl.exe -e bash`).
> - hg is the development VCS. git and GitHub sync ONLY on a push; until then git shows the docs as
>   modified and build stamps show `+`. Regenerate `MERCURIAL_HISTORY.md` and `mercurial-history.hg`
>   before each push.
> - The benchmark is `tests/bench/compare3.sh [--cs <exe>]`, run unmodified from Git Bash. Method
>   changes are the architect's call.
> - Path form: forward-slash Windows form in both directions (DECISIONS 2026-09-11/12).
> - Steroids: not active, no ruling; do not ask.
>
> **DO NOT undo:**
> - The console-less test is `GetConsoleCP()==0` (`GetConsoleWindow()==0` is wrong, measured).
> - `run-tests.ps1 -Interpreter` (`-Exe` collides with `$exe`).
> - `LfReader`: never wrap a pipe in a `StreamReader` under it (+48.8 % pipeline stall); never call
>   `TextReader.ReadLine` in a tool (it ends lines at CR); call `CurrentRawStdin` once.
> - `TranslatePath` resolves relative paths only for stages that moved; never test a dup-redirect's
>   translated target (`2>&1` broke that way).
> - `ExpandOperand` works from the RAW text; the word parser loses `\*`.
>
> **Machine facts (do NOT "fix"):** Git's `bash.exe` is renamed `_bash.exe` (Git Bash is
> `F:/Tools/PortableGit/usr/bin/_bash.exe`; the `bin/` stub is broken by the rename), and
> `System32\bash.exe` (WSL) was moved away, both so nothing pre-empts C#Bash. Reach WSL with
> `wsl.exe -e bash`. Run Git Bash scripts as `"$BASH" script` from `_bash.exe -l`. Claude Code sets
> `NoDefaultCurrentDirectoryInExePath=1`.
>
> **For the architect's own files (suggested only):** the global CLAUDE.md's launcher line is
> obsolete since the rename (2026-10-04 scan), and its "Known limits of C#Bash" says UNC globs do
> not expand, fixed in rev 58 [recorded, not re-verified]. Its awk 3-argument `match()` line is still
> true. Its `df` line was corrected on 2026-10-08 at the architect's request ("You should fix it").

## Purpose
A standalone Windows bash interpreter written in C#. Targets a bash subset: full POSIX core plus the most-used bashisms (`[[ ]]`, arrays, `local`, arithmetic expansion, brace expansion).

The goal order (DECISIONS 2026-10-03):
1. A good shell for its own sake, on Windows.
2. Replace PowerShell for everything that is not Windows-dependent.
3. Keep Claude Code seamlessly faster. That is achieved; maintain it.

Git Bash is a reference, not a target.

## Documentation set
Four canonical docs at the repository root, each with a distinct job (see each file's header
for its maintenance contract):
- **`PROJECT_CONTEXT.md`** (this file) — what exists now / what's next (high-churn status).
- **`DECISIONS.md`** — why each choice was made (append-only).
- **`ARCHITECTURE.md`** — how the system is designed; conceptual model + 5 SVG diagrams.
  *Locked by default* — touched only on structural change.
- **`IMPLEMENTATION.md`** — where each concept lives in code (class/method map + end-to-end
  walk-through). Tracks the code surface; updated on any surface-texture change.

Diagrams and the bash-startup reference live in `docs/`.

## Architecture
```
<repo root>/
  README.md            ← overview, features, embedded-command list
  ARCHITECTURE.md / IMPLEMENTATION.md / PROJECT_CONTEXT.md / DECISIONS.md  ← doc set
  MERCURIAL_HISTORY.md ← exported hg log    mercurial-history.hg ← full hg bundle
  moosh-all-the-way-down.md / .png  ← the companion article (published on LinkedIn) + its art
  docs/                ← 5 architecture SVGs + bash-startup-reference.md
  Bash/                ← interpreter source + Bash.csproj
    Program.cs           ← REPL entry point
    Lexer/
      Lexer.cs           ← character stream → token stream
      Token.cs           ← Token record
      TokenType.cs       ← TokenType enum
      LexException.cs    ← lex error type
    Parser/              ← token stream → AST (Parser.cs, Nodes.cs)
    Evaluator/           ← AST → execution (Evaluator, WordExpander, ArithParser, Builtins,
                           Builtins.Shell [shell builtins proper], ShellEnvironment, ShellOptions,
                           Glob [pattern matching + pathname expansion], Printf [bash printf],
                           FileTests [test/[[ file primaries])
    IO/
      LineEditor.cs      ← readline-style editor (cursor, editing, history nav, tab)
      History.cs         ← command history + ~/.bash_history persistence
      CompletionEngine.cs← command (builtin/func/PATH) + filename + variable completion
      PromptExpander.cs  ← PS1/PS2 backslash-escape + promptvars expansion
  tests/               ← run-tests.ps1 + cases/ + expected/
  tools/               ← bench.csx throughput harness
  Bash.sln
```

`Program.cs` parses invocation flags bash-style (options in any order before the first
non-option, combined short flags, `--`; `-c` takes the first non-option as the command
string; `-l`/`--login`, `-i`, `-s`, `-o opt`, `-O shopt`, `-e/-u/-x/…`, `--norc`,
`--noprofile`, `--rcfile`, `--version`, `--help`), sources the appropriate startup files
(login: /etc/profile + ~/.bash_profile|.bash_login|.profile; interactive:
/etc/bash.bashrc + ~/.bashrc; non-interactive: $BASH_ENV), sets defaults (BASH_VERSION,
PS1=`\s-\v\$ `, PS2=`> `), then either runs the `-c` string, the script file, a
**non-interactive stdin script** (stdin redirected, no `-i`: no prompts, no editor), or the
REPL: PROMPT_COMMAND → expand PS1 via `PromptExpander` → `editor.ReadLine`. Startup files
are sourced through `Evaluator.SourceFile`; `-c`/scripts run via `Evaluator.RunString`
(syntax error → `bash: <origin>: line N: …`, status 2).

## Pending Actions
> Note: the phase numbering below is the original plan. BASH_PROJECT_HANDOFF.md
> (project root) uses a later, reorganised numbering. Status here is reconciled
> against the actual source.
- [x] Phase 1 — Lexer + token stream
- [x] Phase 2 — Parser → AST (commands, pipes, redirects, lists)
- [x] Phase 3 — Evaluator: external process execution + builtins
- [x] Phase 4 — Glob, brace expansion, tilde, $'...', heredoc body, set -e/-x/-u/-o
- [x] Phase 5 — Control flow: if/then/elif/else/fi, for, while, case
- [x] Phase 6 — Functions, local, return
- [x] Phase 7 — Bashisms: [[ ]], arrays (indexed + associative), brace expansion
      — all Phase 5 array tests (see tests/phase5.sh) now passing
- [~] Phase 8 — Line editor + REPL UX. Done: cursor movement incl. word motion,
      editing keys, Home/End, Ctrl-A/E/B/F/U/K/W/L/C/D, history up/down with
      ~/.bash_history persistence + HISTSIZE capping, reverse-i-search (Ctrl-R),
      tab completion for commands + filenames + variables (`$VAR`/`${VAR`),
      `history` builtin (`-c`, `history N`), and multi-line continuation
      (compound commands, unterminated quotes, backslash-newline) driven by the
      parser/lexer `Incomplete` flag. NEEDS interactive TTY testing for the
      ReadKey paths (this dev sandbox is non-TTY; redirected-input paths verified).
      Still deferred: `$(...)`/path-aware completion, completion menus, multi-line
      *re-editing* of a recalled block, `HISTFILESIZE`/timestamps.
- [~] Phase 9 — Script/shell invocation: `bash script.sh`, `bash -c`,
      `-l`/`--login`, `--norc`/`--noprofile`/`--rcfile` flags, startup-file
      sourcing, PS1/PS2/PROMPT_COMMAND, BASH_VERSION, and positional parameters
      ($0, $1.., $#, $@, $* incl. `"$@"`/`"$*"` field-splitting) all work.
      Shebang dispatch: running a `#!` script as a command (`./foo.sh`) reads the
      interpreter and runs it — in-process for bash/sh (child shell: inherits
      exported vars, isolated, `exit` ends only the script), spawning otherwise
      (`/usr/bin/env X` → X via PATH); a `.sh` file with no shebang also runs
      in-process. Still pending: command-substitution in prompts ($(...)).
- [~] Phase 10 — Traps & job control. Done: `trap` (EXIT + INT fire; reset `-`,
      ignore `''`, `-p`, `-l`; other sigspecs stored but never triggered),
      background `&` (thread-based jobs), `jobs`, `wait [%n]`, `fg` (= wait, no
      true terminal hand-off), `bg` (reports unsupported). Deferred: `DEBUG`/`ERR`/
      `RETURN` traps, `kill %n`, Ctrl+Z suspend, real fg/bg (need fork / SIGTSTP /
      process groups). In-process jobs share env+console with the foreground —
      safe for external commands, races on internal state (documented).

## Signal handling
- SIGINT (Ctrl+C) while a command runs sets `Evaluator._interrupted` from the REPL's
  `Console.CancelKeyPress` handler (e.Cancel=true so the shell survives); loop/command
  boundaries (`ExecScript`/`ExecFor`/`ExecWhile`) and `sleep` poll `CheckInterrupt()`
  and throw `InterruptException` (exit 130). External children get the console's
  CTRL_C_EVENT directly. During line editing the editor sets `TreatControlCAsInput`,
  so Ctrl+C is a key (clear-line) there. Only the interactive REPL registers the
  handler — scripts/-c keep default terminate-on-Ctrl+C. NEEDS interactive testing.
- An INT trap (`trap '…' INT`), if set, runs in `CheckInterrupt()` instead of
  aborting; `trap '' INT` ignores Ctrl+C. EXIT trap runs once at every shell-exit
  path (`Evaluator.RunExitTrap`).
- Ctrl+Z (suspend): still not implemented (no SIGTSTP on Windows).

## Performance / throughput
- **Primary harness: `tools/bench.csx`** (run with `dotnet-script`, already installed).
  References Release `Bash.dll` in-process (no process/JIT noise), best-of-6, reports
  **ns/iter AND bytes/iter** (`GC.GetTotalAllocatedBytes`) + gen0/1/2 counts. The
  authoritative measure for any perf A/B. **Always pass `--no-cache`** when A/B-ing
  after a rebuild — dotnet-script caches compiled scripts and will otherwise serve a
  stale DLL binding (this silently faked a "no change" result once).
- Coarse process-level numbers (`tests/bench/*.sh` via `Measure-Command`) include
  startup/JIT; fine for rough wall-clock, not for A/B of small changes.
- **The interpreter is allocation-bound** — no single CPU hot spot; gen0 GC is cheap so
  not GC-bound either. A `dotnet-trace` cpu-sampling profile MISLED here (only ~2.7k
  samples; its own EventPipe machinery polluted stacks, falsely showing 87% in
  `List.AddRange/CopyTo`); a controlled bench.csx A/B disproved it. Lesson: trust the
  in-process A/B, treat dotnet-trace as a hint.
- **Done (2026-06-14), cumulative loop ~711 → 356 ns/iter (≈50%), 1940 → 1100 B/iter:**
  (a) ExpandToFields single-literal fast-path (EtoF literal 232→64 B); (b) single bare
  `$var`/`${simple}` fast-path under default IFS with no split/glob/brace needed
  (EtoF $i 256→64 B; func.sh 1115→922 ns); (c) ArithParser rewritten to inline
  precedence methods (no `Func<long>` delegates/params arrays, substring-free numbers)
  — arith-eval 384→224 B; this also FIXED two real bugs: `$((1&&1))`/`$((1||0))` used
  to crash and `$((0xff))` returned 0. Marginal Add-vs-AddRange tweak kept.
- Residual: arith ~224 B/eval is now `ExpandVarsInArith`'s StringBuilder; loop ~1100 B
  is mostly per-command dispatch (args/tempAssign lists, scopes).
- **Next levers (measured):** (1) per-command allocation in ExecSimpleCommand
  (`args`/`tempAssign` lists allocated even when empty); (2) skip ExpandVarsInArith's
  StringBuilder when the expr has no identifier needing substitution;
  (3) exception-based break/continue in tight loops. Diminishing returns from here.
- **Re-measured 2026-09-04 (rev 47, `dist/Bash.exe` vs Git for Windows 2.28 bash, wall clock
  best of 3, seconds):** loop 0.56 vs 1.48 · arith 0.71 vs 2.07 · func 0.61 vs 2.30 ·
  loop_big (2 M iters) 1.67 vs 14.90 · startup `-c 'echo hi'` 0.083 vs 0.029 (the .NET runtime
  start; Git Bash wins) · 300× `$(basename) $(dirname) $(echo|wc)` 0.17 vs 20.28 (900 spawns
  in Git Bash) · 5× `grep|sed|sort|uniq|awk` over 50 k lines 0.50 vs 0.35 (native C tools beat
  the managed ones per line) · 3× `find … | wc -l` over the repo 0.13 vs 0.21. Outputs
  byte-identical in all three coreutil scripts. In-process harness (`bench.csx --no-cache`):
  loop 489 ns/it 1092 B/it, arith 757 ns 1435 B, func 1303 ns 3817 B. **Caveat:** the loop
  ns/it is above the 356 recorded on 2026-06-14 with the SAME bytes/it, while the coarse
  process-level loop rate is *higher* than June's (359 K vs 303 K iters/s) — so it is not
  established that anything regressed; an A/B removing the two per-node additions of P4
  (`<( )` mark/release, ERR-trap check) moved nothing outside the ±3 % noise band. Treat the
  in-process numbers as comparable only within one session on one machine state.
  **Correction, same day (the architect: same machine):** bisected by building hg revisions in a
  scratch clone and running the same harness — rev 38 (June's code) measures **358 ns/it today**,
  so the June figure reproduces and the +37 % is real: **P1 (rev 39) 454 ns · P2 (rev 41) 472 ·
  P3+P4 (rev 42+) ~490**, bytes/it 1116 → 1092 (less allocation, more CPU). Three guesses were
  A/B'd and were NOT it: the per-node `<( )` mark/release, the ERR-trap check, and
  `ShellEnvironment.Get`'s special-parameter switch + `int.TryParse` on every lookup (a fast path
  for lower-case names moved nothing; reverted, as did a lazy `BASH_COREUTILS` lookup and `Set`'s
  attribute checks). The P1 diff (language core: `ExecSimpleCommand`, `test`/`[` parser, word
  expander rewrite) is where the remaining ~100 ns lives — NOT yet attributed; next step is
  A/B-ing those three inside rev 39 with `bench.csx --no-cache`, the way the June work was done.
- **Attributed and mostly recovered, same day (rev 50; DECISIONS entry of that date):** a per-node
  harness (`tools/attrib.csx`) split the iteration against the June build. Two of the three were
  real and cheap to fix: the `[` builtin's parser object + argument-list copy (now the POSIX
  1/2/3-argument forms are answered directly: 191 -> 146 ns, 800 -> 544 B) and a `RedirectScope`
  allocated for every simple command with no redirects (74 -> 64 ns, 368 -> 232 B). **Loop now
  ~430 ns/it, 836 B/it** (June 358/1116; before the pass 489/1092; a marginal ~10 ns of that is a
  lower-case fast path in `ShellEnvironment.Get`, A/B-measured); arith ~720, func ~1180. The
  remaining ~65 ns is attributed and left: ~27 in arithmetic identifier resolution through
  `IArithVars` (the P1 redesign that makes `x=y+1` values correct; lever = the parsed-expression
  cache under "Next levers"), ~10 per-node bookkeeping, ~12 in bare-`$i` expansion.

## In-process coreutils (spawn-cost reduction)
On Windows a `CreateProcess` for a foreign exe is a ~10ms floor (no fork; AV scanning
adds variable ms) — see DECISIONS 2026-06-14. The real lever is to NOT spawn: implement
hot, simple, *shell-adjacent* utilities in-process. Boundary deliberately bash-leaning
(things bash syntax half-covers), NOT a full busybox.
- Frequency evidence (all biased, but convergent on the simple hot set): NL2Bash
  (find/xargs/grep — how-to-question bias), the command-line-customization study
  (git/ls/cd/grep — interactive), PaSh pipelines (cat/grep/sed/awk/sort/cut/tr/head/tail).
Target = the classic AT&T System V command set, run through complexity tests:
(1) self-contained (no uid/gid/mode/signal/tty/fifo/device/fork dependency);
(2) bounded (no embedded regex engine or scripting language → else "large");
(3) byte-faithful I/O where it matters (⬡); (4) destructive-safe (⚠).

**Current state (2026-09-04, after P3+P4 — supersedes the wave-by-wave history that stood here;
the old classification is preserved in DECISIONS 2026-06-14 and hg history):**
- **Every tool parses options strictly** (`Opts.Parse`, `Evaluator/Opts.cs`) and follows
  DECISIONS 2026-09-04 #2: an unimplemented option or construct is reported, then the command is
  re-run through a PATH external of the same name if one resolves, else exit 2 —
  `BASH_COREUTILS=auto|builtin|external` overrides. Nothing is silently ignored any more.
- **Text (`Builtins.Text.cs`):** `cat head tail wc rev tac tr cut uniq nl fold paste comm seq
  split od sort tee base64 md5sum sha1sum sha256sum sha512sum hexdump` — GNU flag sets (`head -c`,
  `tail -f`, `sort -k/-n/-V/-h/-u/-s/-o`, `wc` widths, `cut -d/-f/-c/-b/--complement`, …).
- **Files (`Builtins.Files.cs`):** `basename dirname mkdir rmdir touch rm mv cp ls du cmp stat
  mktemp realpath readlink chmod ln truncate` — `ls -l` family is in (decision 5 superseded the
  no-`-l` scoping; mode string is synthesised: `-rw-r--r--`, read-only attribute honoured),
  `chmod` maps `-w`/`+w` to the read-only attribute and is loud for everything else, `ln` makes
  hard links and symlinks (symlinks need Developer Mode or elevation).
- **Find / diff / xargs (`Builtins.Find.cs`):** a real `find` expression engine (`-name -iname
  -path -regex -type -maxdepth -mindepth -depth -newer -newermt -mtime -mmin -size -empty -prune
  -quit -print0 -ls -printf -delete -exec ;/+`), `diff` (normal/unified with context, `-r`,
  `-q`, `-s`, `-i -w -b -B -N -a -x`, binary detection, GNU hunk ordering), `xargs` (`-0 -n -I
  -L -r -t -d -a -P -s -E`, GNU exit codes).
- **Search (`Builtins.Search.cs`):** `grep` (all major flags incl. `-A/-B/-C` separators, `-o`,
  `-m`, `-r` with include/exclude, `-z`, `-b`, `-P` via .NET) and a full `sed` compiler/runtime
  (addresses, ranges incl. `+N`/`~N`, `!`, `{ }`, every common command, hold space, `s` flags,
  `-i[SUFFIX]`, `-s`, `-z`). BRE default, `-E`/`-r` ERE (`BreToNet`/`EreToNet`).
- **System (`Builtins.Sys.cs`):** `date` (full `strftime` flags, `-d` via `GnuDate` — ISO/RFC/
  month-name/`@epoch`/times/zones/relative items/weekdays — `-r -u -I -R -f`), `uname` (MSYS
  style: `MINGW64_NT-10.0-19045 … x86_64 Msys`), `hostname`, `arch`, `nproc`, `tty`, `whoami`,
  `id`, `printenv`, `timeout` (kills an external child on expiry; an in-process command is
  interrupted and reported as 124 / 143 with `--preserve-status`), `df` (2026-10-08: Windows
  volumes in GNU's format, `C:` / `//host/share` sources, `C:/` mount points; `-h -H -k -B -P -T
  -t -x -l --total --output`; inode options loud; checked by `tests/df/compare-wsl.sh`).
- **awk (`Builtins.Awk.cs`):** the DECISIONS 2026-09-04 #7 subset — 132 representative one-liners
  are byte-identical to gawk (`tests/cases/awk.sh`); outside the subset the tool is loud (or falls
  through to a PATH awk). gawk-verified deviations from POSIX kept on purpose: `substr` truncates
  positions and clamps a start below 1 without shortening the length; a single-character `FS` is
  literal even if it is a regex metacharacter; integral numbers print as integers at any
  magnitude.
- **Processes (`Builtins.Proc.cs`, ratified 2026-09-04):** `pgrep pkill ps` — Toolhelp32 list plus
  `NtQueryInformationProcess` command lines; `-f -x -l -a -c -n -o -v -P -d`, signals accepted
  (all forceful), `ps -e/-ef/aux/-p/-o pid,ppid,comm,args`. Names match case-insensitively; the
  shell excludes itself and `pkill` refuses aloud both its ancestors (ppid chain) and any process
  whose command line carries the pkill invocation itself (ppid chains break at MSYS `exec`
  boundaries — measured under Claude Code). awk's `print > "/dev/stderr"` and `"/dev/stdout"`
  were admitted at the same time.
- **Still in `Builtins.cs`:** `factor cal expr which hash kill trap jobs fg bg history exec`.
- **PATH:** Git for Windows' `usr/bin` and `mingw64/bin` are prepended when `git.exe` is on PATH
  (as Git Bash does), the shell's own directory is appended (nested `bash -c` resolves on a
  Git-less PATH), and `$PATH` assignments are mirrored into the process environment that command
  lookup reads. `Path` (Windows spelling) is imported as `PATH`.
- **Quoted-glob bug FIXED (2026-06-14):** `echo '*'` / `echo "*.cs"` / `echo '{a,b}'`
  used to glob/brace-expand (ExpandToFields applied them to ALL fields, losing quotedness).
  Now each field tracks an *unquoted*-metachar flag (`fieldGlob`); brace/glob apply only
  to those. Quoted stays literal; unquoted `*`/`{a,b}` still expand. (Found via `expr 2 '*' 4`.)
- **Redirect fixes (2026-06-14):** `/dev/null` (+ `/dev/stdout`/`/dev/stderr`) handled
  in `ApplyRedirects` (was crashing — became `F:\dev\null`); output redirects are now
  **fd-aware** for builtins (`2>file` → stderr, not stdout); a failed redirect (or other
  runtime `EvalException` like div-by-zero) now fails just that command and the
  list/script continues (via `RunStatement`), matching bash.
- **External-command redirect rewrite (2026-06-14):** `ApplyRedirects` (a `Console`
  TextWriter swap) cannot reach a child process's OS handles, and `ExecSimpleCommand`
  ran it *unconditionally* before `ExecExternal` re-opened the same files — so external
  `>file` threw a **sharing violation that crashed the shell**, and external
  `2>file`/`2>/dev/null`/`</dev/null` were silently ignored. Fix: redirects now apply
  in exactly ONE place per command type — builtins/functions take the `Console` swap
  (`ExecSimpleCommand` gates on `Builtins.Has(name) || _functions`), and `ExecExternal`
  owns a self-contained fd-map for externals: stdin/stdout/stderr each to file (trunc/
  append), `/dev/null` (→`Stream.Null`), `/dev/stdout`·`/dev/stderr`, `2>&1`/`1>&2`, and
  pipeline handles. Guarded by `cases/redirect_ext.sh`. See DECISIONS 2026-06-14.
- **Builtin `2>&1` FIXED (2026-06-14):** `2>&1` parses as `OutputDup`(fd 2, target "1"),
  but `ApplyRedirects` only handled it under a never-matching `InputDup` case, so builtin
  stderr→stdout merging silently did nothing (`$(cat missing 2>&1)` captured ""). Now both
  dup directions are `OutputDup` cases keyed on fd; `cmd >/dev/null 2>&1` suppresses both.
  Covered by `cases/redirect.sh`.

## Pipeline & output fidelity (both fixed 2026-06-14)
- **Multi-stage builtin pipeline race — FIXED.** All-in-process pipelines (every stage a
  builtin/function/assignment, per `AllStagesInProcess`) now run **sequentially with
  buffering** via `ExecPipelineSequential` (each stage's captured stdout feeds the next
  through `ExecuteWithStdin`) — no concurrent threads, so no Console-global race.
  Pipelines containing an external still use `ExecPipelineViaThreads`. Guarded by
  `tests/cases/pipeline.sh` (3- and 4-stage all-builtin). Caveat: sequential stages
  buffer in memory (fine for typical sizes) and are text (a binary mid-pipe loses
  byte-fidelity — rare); a mixed pipeline with a builtin *between* two externals can
  still race on Console (uncommon; revisit if hit).
- **CRLF output — FIXED.** `NewLine = "\n"` set on Console.Out/Error at startup and on
  every installed StreamWriter (redirects, pipe stages) + capture StringWriters. So
  `echo hi | wc -c` = 3 and redirected text is LF. (cat's raw byte path was already
  faithful.)
  - Byte-faithful I/O: `CurrentRawStdout()` returns the raw byte sink (file-redirect
    FileStream via `_rawStdout`, else pipeline `_pipeStdout`, else
    `Console.OpenStandardOutput`), or null when `$(...)`-captured (`_capturing`) →
    caller writes text. `RedirectScope` saves/restores `_rawStdout`; capture sites
    set `_capturing`. cat is byte-faithful for file args (verified: binary copy
    cmp-identical); stdin path is text for now.
- Wave 3 (⚠ destructive): `rm`, `mv`, `cp`.
- Wave 4 (moderate): `ls`(scoped), `sort`, `find`, `xargs`, `expr`, `date`, `du`, `split`,
  `od`, `diff`, `uname`, `hostname`, `kill`, `cal`, `factor`; + PATH `hash` cache.
- Wave 5 (large, optional): `grep`, then `sed`, then `awk`, `bc`.

## Open Questions
- General Unix root mapping (`/usr`, `/etc`) stays deferred — see DECISIONS.md. Drive-letter
  (`/c/…`) and `/tmp` mapping are done (DECISIONS 2026-09-04 #4).
- (Resolved 2026-09-04) Decision 3 spike: Claude Code's cwd read-back accepts the native
  `F:\…` form that `pwd -P` prints — verified with a nested `claude -p` session using
  `CLAUDE_CODE_GIT_BASH_PATH=…\Bash.exe`; `cd sub` in one call was still in effect in the next.

## Test Assets
- **`tests/run-tests.ps1 [-NoBuild] [-Detached] [-Interpreter <exe>]`** (51 cases on
  2026-10-03). `-Detached` starts every case as Claude Code starts its shell: DETACHED_PROCESS, no
  console, stdin NUL, stdout to a file. That is the only way to exercise the console-less spawn
  path; a run from a console cannot reach it. `-Interpreter` tests a given exe, such as the AOT
  publish or `dist/`. The runner prints the interpreter and build it tested.
  Self-checking runner. Builds Release, runs each
  script-mode test, compares STDOUT (stderr discarded) line-by-line against
  `tests/expected/<name>.out`, prints PASS/FAIL, exits non-zero on any failure.
  `-NoBuild` skips the build. Expected files are authored from bash semantics
  (not captured blindly): for `awk`/`procsub`/`sysutils2` (P4) they were generated by
  running the same script under Git Bash/gawk, diffed against C#Bash, and only the
  by-design lines (the loud boundary) kept from C#Bash's output. Currently 45 cases
  (…, hash, grep, sed, claude_wrapper, flags, shellbuiltins, specialvars, heredoc,
  pipes2, awk, procsub, sysutils2, procs, parser2) — all green. `parser2` guards the
  2026-09-05 defect report (nested `$( )` inside `$(( ))`, backslash-newline continuation,
  quoted `$(cmd arg)`, `echo -e`/`printf`). The build step first kills any stale
  interpreter instance under the project (a hung test process locks Bash.exe and fails
  the copy). `flags.sh` and `procsub.sh` invoke the interpreter recursively through
  `$BASH`. `tests/fixtures/awk/` holds the small input files the awk/procsub cases read.
  A test line whose producer writes two lines into `| head -1` is racy (141 vs the real
  status) in bash too — capture with `$( )` first, as `sysutils2.sh` does.
- **`tests/compat/run-compat.ps1`** — the Claude Code compatibility battery: runs every probe
  in `tests/compat/probes.txt` (probes separated by `----` lines; APPEND only — numbers are
  positional) through a reference bash and through C#Bash in identical fresh fixture dirs, and
  passes a probe only when stdout + exit code are byte-identical. `baseline.txt` is the ratchet:
  any listed probe that stops passing fails the run; newly passing probes are reported and
  folded in with `-UpdateBaseline`. Reference bash: `-Reference`, `CLAUDE_CODE_GIT_BASH_PATH`,
  the Git that owns `git.exe` on PATH (portable installs included), Program Files, or PATH —
  never `System32\bash.exe` (WSL, a different OS; it silently became the reference once when
  the battery was launched from PowerShell). `work/` is scratch (ignored). Seeded 2026-09-04
  at 86/228; 217/228 after P4. Probe 172 (`export -p | grep -c PATH`) is deliberately NOT in
  the baseline: its count depends on the launching environment (Git Bash adds `ORIGINAL_PATH`).
  **Probe authoring (web-d6's observation, 2026-09-08):** a parse-time defect is invisible to a
  probe that lives in the same file as the construct it tests — the whole probe dies before
  its first line runs and every other construct in it reports nothing. When a probe carries
  several constructs, or tests one suspected of failing to parse, wrap the risky one in
  `eval '…'` so the failure is confined to that construct and the rest still report. Probe 235
  (lone `$`) is the kind that would have hidden the sed and UNC results had they shared a probe.
- `tests/cases/*.sh` — runner-owned scripts; `tests/expected/*.out` — expected stdout.
- **`tests/MANUAL.md`** — checklist for interactive/TTY-only features (line editor,
  history, Ctrl+R, completion, PS2 continuation, prompt, Ctrl+C/traps, jobs) that
  can't be scripted. Walk it in a real terminal.
- `tests/phase5.sh` — array + associative-array coverage (single index, append,
  `"${arr[@]}"` field splitting, `${!arr[@]}` keys, `unset arr[n]`, assoc
  iterate, arithmetic index). All passing as of 2026-06-13.
- `tests/gap.sh` — empty/undeclared array → zero fields, quoted `""` → one field,
  unquoted unset → zero fields, single-quote no-split, mid-word `@` join.
- `tests/quote.sh` — single-quoted word-splitting guard.
- `tests/cont.in` — backslash-newline continuation input (pipe via stdin).
- `tests/test.bashrc` + `tests/repl.in` — startup-file sourcing, PS1/PROMPT_COMMAND
  exercised via `--rcfile … < repl.in`.
- `tests/args.sh` — positional parameters ($0/$1/$#/$@ incl. `for x in "$@"`),
  run as `args.sh apple banana cherry`.
- `tests/bench/{loop,arith,func,loop_big}.sh` — interpreter throughput; `coreutils.sh` (900
  coreutil calls: the spawn-cost case), `pipeline.sh` (grep|sed|sort|uniq|awk over 50 k lines,
  generated in `$TEMP`), `find.sh` (walk this repo) — added 2026-09-04. **`compare.sh`** runs every
  one under a reference bash and C#Bash (best of N, ratio, startup, output agreement); run it from
  Git Bash: `tests/bench/compare.sh [Bash.exe] [reference bash] [rounds]`. Wall clock includes
  startup; per-iteration A/Bs belong to `tools/bench.csx --no-cache`.

## Out of Scope
- Full bash 5.x fidelity (coproc, process substitution <(), etc.)
- Real job control with terminal hand-off / Ctrl+Z suspend (no fork/SIGTSTP on
  Windows). Basic background `&`/`jobs`/`wait` ARE implemented (Phase 10).
