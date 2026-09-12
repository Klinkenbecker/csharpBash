# Project Context: Bash

> ## RESUME ANCHOR (2026-09-04) — read this before anything else
> **TRUE STATE (2026-09-04, after P3+P4, hg rev 42):** suite 45/45 green; the 228-probe **Claude Code
> compatibility battery** (`tests/compat/`, Git Bash vs C#Bash) is at 217/228 with 0 hangs, baseline 217
> (the 11 remaining DIFFs are environment/by-design: tools absent from Git Bash, `BASH_VERSINFO` 5 vs 4,
> `which`/`type` on builtins, `$'\u…'`, `/dev/stderr` inside `$( )`). Every coreutil parses options
> strictly and follows decision 2 (`BASH_COREUTILS=auto|builtin|external`); in-process `awk` subset,
> `<( )`, `trap ERR`/`-E`, `|&`, GNU `date -d`, `timeout`, `id`/`whoami`/`nproc`/`printenv`/`tty`/`arch`
> are in. Public git snapshot `a65b44f` is BEHIND hg — the architect cuts snapshots.
> Claude Code's exact Windows shell contract is recorded in memory `claude-code-bash-contract`: spawn is
> `bash.exe -c [-l] "<wrapper>"`; wrapper = `source <MSYS-path snapshot> 2>/dev/null || true && export
> TEMP=… TMP=… && shopt -u extglob 2>/dev/null || true && { \builtin unalias -- …; \builtin unset -f -- …; }
> >/dev/null 2>&1 || true && eval '<cmd>' < /dev/null && pwd -P >| <MSYS-path cwd file>`. Claude Code does
> NOT require Git for Windows — any `bash.exe`/`sh.exe` via `CLAUDE_CODE_GIT_BASH_PATH`; with none, its Bash
> tool is simply unavailable.
> **KNOWN GAPS after P4 (all loud, none silent):** `>( )` is rejected at parse time (decision 6);
> awk constructs outside the ratified subset (`getline`, user functions, `print >`/`|` redirections,
> `printf %e/%g`, `ENVIRON`, `RS`) fail loudly or fall through to a PATH awk (decision 2); `set -o
> functrace`, `nextfile`, `-ok`/`-perm` in `find`, `xargs -p` are loud unsupported. Externals resolve
> through the *process* PATH, which now tracks `$PATH` assignments (P4 fix). `ps` knows only
> pid/ppid/comm/args (no user/etime/tty — loud). No known hangs.
> **FIXED IN P3+P4 (rev 42):** strict option parsing + fall-through for every coreutil (`Opts`);
> `sed -i`/`N,Mp`/hold space/`{ }`; `grep -A/-B/-C/-o/-r/-l/-c/-w/-x/-z/-P`; `find` expression engine
> incl. `-exec +`/`-printf`/`-newermt`; `diff -u/-r/-q` (GNU hunk order); `xargs -0/-n/-I/-P`; `ls -l`
> family; `sort -k/-V/-h`; `date -d/-r/-u/-I/-R` + full `strftime`; `uname -a` MSYS-style;
> `timeout`; `|&` (was attached to the consumer); `<( )`; `trap ERR` incl. `-E`/errtrace semantics
> (verified line-by-line against Git Bash); `awk` subset (132 one-liners byte-identical to gawk);
> Git's `usr/bin`+`mingw64/bin` prepended to PATH when Git is discoverable (as Git Bash does) and
> the shell's own directory appended (so nested `bash -c` resolves on a Git-less PATH).
> **FIXED IN P1 (rev 39; the earlier "done" claims are now true):** heredocs/here-strings, nested
> `$( )`, `"…"` backslash rules, `&>`/`&>>`, `2>&1 >f` ordering, subshell/`$( )` isolation (vars, cwd,
> `exit`), `x=$(false)` status, `command`/`type`/`builtin`, `(( ))` and `for ((;;))`, arithmetic
> assignment/`++`, `${x:n:m}` `${x^^}` `${!x}` `${arr[@]:n}` `${x/#pat/}` and friends, `function name {`,
> `/c/…` + `/tmp` paths, `-c -l` / `-lc` / `--version` / `-o` / `-e` flags, `shopt`/`alias`/`declare -f
> -F -p -i -r`/`readonly`/`exec`/`let`/`pushd`/`mapfile`/`getopts`/`read -a -d -n -u`/`printf` (full
> formats, `-v`, arg recycling), fd 3+ (`exec 3>f`, `>&3`, `read -u 3`), special vars (`OSTYPE`
> `PPID` `RANDOM` `UID` `PIPESTATUS` `BASH_REMATCH` `LINENO` `$-` `$!` `FUNCNAME` `BASH_SOURCE`
> `BASH_VERSINFO` `SHLVL`), `command not found` text honouring `2>/dev/null`, syntax errors inside
> `source`/`eval` contained, externals inside `$( )` / `{ … } >file` / heredoc loops routed correctly,
> MSYS-form `PATH` normalised on assignment. Compat battery 86 → 150 / 228; suite 36 → 41 cases.
> **FIXED 2026-09-05 (rev 51, from session web-d6's defect report in `E:\CLAUDE\bash-defects-2026-09-05.md`):**
> `$(( $(cmd args) ))` lost the spaces inside the nested substitution (a plausible wrong value
> with status 0 — the serious one); a backslash-newline that was a whole word became an empty
> argument (`cat f \` + newline + `| sed`). Two more items in that report were already fixed by
> P1. `bash --version` now prints a third line `C#Bash build 1.0.0+hg.<hash> <rev>` (stamped from
> `hg id` at build time, `+` = uncommitted tree) so a reporter's binary can be identified — until
> now every build carried the git snapshot's hash. Suite 47/47; battery 219/230, baseline 219.
> **FIXED 2026-09-05 (rev 55, web-d6's second report over Remote Control):** `$(( t + $(cmd) ))`
> closed one `)` early when the substitution was not first (parse error aborting the script);
> `echo x (y) z` now is the syntax error bash gives instead of three commands. Two further items
> in that report were the reporter's harness stripping double quotes (byte-identical here).
> Battery 222/232, baseline 221.
> **FIXED 2026-09-08 (rev 58, web-d6's third report):** globs on UNC paths (`//host/share/*.md`,
> `for f in //host/share/*`) matched nothing — `Glob.Expand` rooted the walk at `/`; now the
> share is the root, verified identical to Git Bash incl. globstar. `head`/`tail -z` fixed rev 57.
> Battery 223/233, baseline 222. **Rev 59:** sed `0,/re/` re-opened its range on every line
> (first-occurrence patch became a global rewrite, silently) — fixed, probe 234, baseline 223.
> **Rev 60:** `CSHARPBASH_BUILD`/`CSHARPBASH_REV` shell variables (build identity as values);
> a lone `$` glued itself to the next word — `[[ $x =~ ^[0-9]+$ ]]` and `[[ a$ == a$ ]]` failed
> to parse — fixed, probe 235, baseline 224. **Rev 60 is installed on GUY-WINDOWS11
> (`C:\tools\bin\Bash.exe`) and verified there by web-d6 with negative controls on all three.**
> **Rev 62 (installer-79's report):** `#` inside a word was a comment — `$((10#0010))` truncated
> the line (10 printed by coincidence, `x=$((10#0010)); echo $x` printed nothing, a subshell never
> closed) — fixed per bash's start-of-word rule, probe 236, baseline 225.
> **Rev 63 (installer-79):** binary data through `< file` and pipes was UTF-8-decoded and
> re-encoded (0xFF → EF BF BD) — byte builtins now read the raw stream behind stdin
> (`ConsoleMux.RawIn`, the mirror of P2's raw stdout); suite case `binary`, probe 237, baseline
> 226. **AWAITING THE ARCHITECT:** `printf '\377'`/`echo -e '\xff'`/`$'\xff'` emit the UTF-8
> encoding, not the byte — recommendation is the surrogateescape design (DECISIONS 2026-09-11),
> half a day, not started. installer-79 (local session; runs `dist\Bash.exe` via
> `CLAUDE_CODE_GIT_BASH_PATH`, so this machine's Claude sessions are on C#Bash) confirmed rev 63
> on its real binaries and offered its end-to-end binary suite as a regression check for that
> work — ask it (`SendMessage` to `installer-79`) when the surrogateescape change is built.
> **Scope requests from that report, AWAITING THE ARCHITECT:**
> awk `print > file`/`close()`, 3-arg `match()`, user functions; `df`; `awk --version` (estimates
> in the 2026-09-08 report to the architect).
> **CLOSED 2026-09-11 (rev 66): the `dotnet publish` slowness is NOT a C#Bash cost.** Measured on
> identical files, one variable moved: drive-letter cwd 2.7 s, UNC cwd 235.7 s — and Git Bash
> (233.7 s) and PowerShell (233.7 s) are within 1 % of C#Bash on the same UNC path, so no shell is
> implicated. A UNC path to a LOCAL drive routes every file operation through the SMB redirector
> over loopback; MSBuild does thousands of them. **Known limit:** a UNC path to a local drive is
> silently correct and catastrophically slow for tools doing many small file operations —
> invisible to a functional test, visible only as duration. (The report's "~1 s from PowerShell"
> premise was wrong: PowerShell on a UNC path is equally slow.)
> **AWAITING THE ARCHITECT — HIGHEST PRIORITY OPEN ITEM (rev 67):** C#Bash passes MSYS-form paths
> to native programs verbatim, so **`/tmp` means TWO different places inside one session**:
> `echo x > /tmp/f` (builtin) writes `%TEMP%\f`, while `tool /tmp/f` (native) resolves against the
> CURRENT DRIVE — measured, and already on disk here (`E:\c\Users\guy\` exists; `C:\tmp` 18 entries
> vs `E:\tmp` 3). Internally inconsistent, no failing observable, same class as the sed range and
> the stdin bytes. Recommendation in the DECISIONS entry of 2026-09-11: apply `TranslatePath`'s own
> mapping to arguments of native children for `//host/share/…`, `/<letter>/…`, `/tmp…`, `~…` only,
> with a **bare `/c` excluded** (else every `cmd.exe /c` breaks) and an env escape hatch. ~half a
> day, not started.
> **RATIFIED + BUILT 2026-09-11 (rev 68) — SUPERSEDES DECISION 3:** `pwd`, `$PWD`, `$OLDPWD`,
> `dirs`, `mktemp` and `realpath` now return the FORWARD-SLASH Windows form (`E:/Claude/x`,
> `//host/share/x`). The architect: "If Bash is handed a windows form path (using backslash), it
> must convert it to a forward slash path before using." Decision 3 was ratified on Claude Code's
> cwd read-back alone; backslash is the shell's ESCAPE character, so `PWD=E:\x` was a value the
> shell's own `${p#pat}`/globs/`[[ ]]`/`case` misread. Falsifier re-tested: a nested `claude -p`
> session still tracks `cd` across calls. **OUTBOUND ONLY** — arguments are untouched, so
> `grep '\.txt'`, `printf 'a\tb'` and `find -name '\*'` keep working; guarded by `tests/cases/paths.sh`.
> **Also rev 68:** an unhandled file-open exception no longer kills the shell (reproduced:
> `split -l 1 f nodir/sub/pre` died with `DirectoryNotFoundException`); guards in
> `Builtins.TryExecute` and `RunString` EXCLUDE `BrokenPipeException`, which derives from
> `IOException` and carries SIGPIPE (141) — nearly a regression.
> **RATIFIED + BUILT 2026-09-12 (rev 71), completing the above:** the path form is forward slash
> in BOTH directions — `TranslatePath` now maps `/c/x` → `C:/x` and `/tmp/x` → `<temp>/x` and
> normalises backslashes, so a path that goes out through `pwd` and comes back in is the same
> string. IO failures are loud, never fatal, at EVERY entry point (three holes closed: the script
> read in `Program.cs`, `SourceFile`, the interactive loop). A failed redirect now reports bash's
> wording and status 1, not a .NET message and 2. **Known message gaps, not fixed:** bash prefixes
> script errors `<script>: line N:` where we say `bash:`, and we print a redirect error after the
> enclosing scope is released so `( cmd > bad ) 2>/dev/null` does not suppress it.
> **RATIFIED + BUILT 2026-09-12 (rev 72–73): ONE BYTE-TRANSPARENT ENCODING.** `ShellEncoding` is
> UTF-8 with surrogateescape (an undecodable byte ↔ U+DC00+byte), used by every file, pipe,
> capture and escape — so `printf '\377'` emits ONE byte and non-UTF-8 data survives variables,
> `$( )`, pipes and files. One class, ~58 mechanical sites. Three silent bugs were found wiring it
> (the escape range overlaps real surrogate pairs; `File.ReadAllText` ignores its encoding argument
> when the file starts with a BOM; `StreamWriter`'s default encoding THROWS on a lone surrogate).
> We preserve NUL where bash drops it. Guard: `tests/cases/bytes.sh`, identical to Git Bash.
> **SHIP BUILD is now ReadyToRun** (`-p:PublishReadyToRun=true`): startup 86 → 74 ms, ~60 ms of
> which is the .NET single-file runtime floor. **Native AOT is blocked on the missing MSVC linker**,
> not rejected — it is the only lever on that floor; test the `ConsoleMux` reflection first.
> `tests/bench/compare3.sh` compares C#Bash / Git Bash / WSL bash and checks their outputs agree.
> **FOUND, NOT FIXED (repros in `scratchpad/sub*.sh`):** `${v/pat/repl}` does no quote removal or
> escape processing on the replacement (`${V//b/\\}` → `a\\`), and **does not expand `$` in the
> pattern or replacement at all** — `${path//$old/$new}` silently returns the input unchanged.
> ~half a day; no failing observable.
> **SHIP-READY at rev 76 (2026-09-12) — the snapshot is the architect's to cut.** Suite 50/50;
> battery 226/237 (baseline 226, the 11 diffs environment/by-design); `dist/Bash.exe` published
> ReadyToRun at rev 76 and copied to the share. `MERCURIAL_HISTORY.md` (77 changesets) and
> `mercurial-history.hg` regenerated — both are hg-IGNORED snapshot artefacts, so **regenerate
> them again after any further commit** or the published history is short by that commit.
> README carries the three-way performance table (losses included) and the Claude Code recipe.
> **DO NOT undo:** nothing in flight in the tree — working tree clean.
> **DOCUMENTED / settled — do NOT re-litigate:** four-doc set; coreutils scope = AT&T SysV filtered
> (DECISIONS 2026-06-14); BRE default; redirects-in-exactly-one-place; expectations authored, not captured.
> **RATIFIED 2026-09-04 (DECISIONS entry of that date):** (1) `OSTYPE=msys`; (2) unsupported coreutil
> option → loud error, fall through to a PATH external if present, env override; (3) `pwd` native Windows
> form, VERIFY by spike against Claude Code's cwd read-back in P1; (4) map drive-letter `/c/…` + `/tmp`
> now, general root mapping stays DEFERRED; (5) supersede the subshell-leak and `ls`-without-`-l`
> decisions; (6) `<( )` by temp-file emulation, `>( )` rejected loudly, end of P4; (7) in-process `awk`
> covering Claude's habitual subset (scope list in the DECISIONS entry), unsupported constructs → per
> decision 2 (PATH awk if present, else loud `awk: unsupported:` exit 2), joins P4.
> **AWAITING THE ARCHITECT (do NOT derive):** nothing. (Both P5 items were ratified 2026-09-04 —
> "1/ yes, 2/ build both in-process" — and are built: awk `print > "/dev/stderr"`/`"/dev/stdout"`,
> in-process `pgrep`/`pkill`/`ps`. See the DECISIONS entry of that date.)
> **GREENLIT (2026-09-04, "all phases, commit each phase or combo, stop for ambiguities/forks"):** P0–P5
> as listed below, in order. Commits go to **hg** (development VCS per README); the git snapshot is the
> architect's to cut. Progress: (update this line per phase) P0 DONE (rev 38: compat battery, baseline 86/228); P1 DONE
> (rev 39 + tail rev 40: invocation contract + language core, baseline 151/228, suite 41/41).
> **Decision-3 SPIKE DONE (rev 40): a nested Claude Code session driven by C#Bash tracked `cd`
> across calls from the native-form `pwd -P` — native output is accepted.** The spike also found
> and fixed a startup crash when spawned without a console (`Console.OutputEncoding`).
> **P2 DONE (rev 41): per-thread console streams (`ConsoleMux`), managed pipes with
> consumer-close (SIGPIPE emulation, `yes | head` works), every pipeline stage on its own thread,
> externals killed when their reader goes away, kill-on-close job object for children (verified:
> killing the shell killed its `ping`), streaming `head`, GNU-style `wc` widths, `${x:-word}` word
> expansion. Battery 151 → 170 / 228 with 0 hangs; suite 42/42. Re-spiked inside Claude Code:
> the snapshot generator now succeeds (5 KB snapshot), the `rg` shim runs the embedded ripgrep,
> heredocs work, and `cd` is tracked across calls (instrumented with `BASH_ENV`).**
> **P3+P4 DONE (rev 42): `Opts` strict parser + decision-2 dispatcher; every tool family rewritten
> (`Builtins.Text/Files/Find/Search/Sys/Awk.cs`, `GnuDate.cs`); `<( )`, `trap ERR`, `|&`, `timeout`
> and friends. Battery 170 → 217 / 228, 0 hangs; suite 42 → 45 (awk, procsub, sysutils2 — expected
> outputs generated from Git Bash/gawk and verified, loud-boundary lines authored).**
> **P5 DONE (2026-09-04, spike, rev 43 docs): a nested `claude -p` session with
> `CLAUDE_CODE_GIT_BASH_PATH=…\Bash.exe` on a PATH holding ONLY `System32`, `WINDOWS`, PowerShell
> and `~/.local/bin` (no Git anywhere) created its snapshot (3181 bytes), ran 15 Bash-tool calls
> and reported them all: `OSTYPE=msys`, MSYS `uname`, native `pwd`, `cd` persisting across calls,
> builtin `awk`, the `rg` shim (embedded ripgrep 14.1.1), `ls -la`, `date -d`, `grep -rn`,
> `timeout` → 124, `command -v bash` → this exe (own-dir PATH append), `diff <( ) <( )`, heredoc
> into `/tmp`, `find | sort | head`, `sed -n`. The one failure: `pkill -f …` → 127. Claude Code's
> `pkill` shim (pulled from `claude.exe`) guards Claude's own PID with `command pgrep` and then runs
> `command pkill "$@"` — it needs a real procps `pkill`/`pgrep`, which no Git-less Windows PATH has.
> That is a PATH-content gap, not a shell gap (Git Bash supplies procps from `usr/bin`).**
> **P5 follow-up DONE (rev 44, ratified "1/ yes, 2/ build both in-process"):** awk admits
> `print`/`printf … > "/dev/stderr"` and `"/dev/stdout"` (files/pipes stay loud); in-process
> `pgrep`/`pkill`/`ps` (`Builtins.Proc.cs`) — self never matches, `pkill` skips this shell's
> ancestors aloud (with `-f` their command lines contain the pattern), suite case `procs`.
> **Readiness check (rev 46): a nested Claude Code session on the NORMAL PATH (Git present) ran
> git/hg, `pkill -f` (skipped the relaying shells aloud), `ps`, `command -v`, awk, `date -d`; the
> only miss was my harness's cwd. `pkill` gained a ppid-free self-protection rule (DECISIONS of
> that date) because MSYS `exec` breaks Windows ppid chains. A deployable copy lives in `dist/`
> (hg-ignored); the placement recipe is in README "Using it as Claude Code's shell".**
> All greenlit phases are complete; nothing is in flight.
> **PLAN (proposed, not greenlit):** P0 compat battery + Claude-wrapper case into `tests/` · P1 invocation
> contract (bash-style option parser, MSYS drive paths, `function`, source-error containment,
> "command not found" text honouring `2>/dev/null`, `shopt`/`alias`/`builtin`/`command`/`type`/`declare`/
> `exec`/`readonly`, special vars) · P2 never-hang (heredoc, nested `$( )`, ext→builtin pipes incl.
> early-consumer-exit, subshell isolation, child-kill on timeout) · P3 loud-not-silent (strict option
> parsing in every coreutil + fall-through policy; `&>` `|&` redirect order; quoting; `$?` from `$( )`;
> `echo`/`printf`; glob prefix) · P4 Claude's vocabulary (sed/grep/find/ls/head/tail/cat/xargs/sort/diff
> flags; mktemp/realpath/stat/timeout/md5sum; `<<<`, `for((;;))`, `[[ =~ ]]`, `[[ && ]]`, `+=`, mapfile,
> getopts, param-expansion ops) · P5 live integration (`CLAUDE_CODE_GIT_BASH_PATH` → C#Bash, Git-less PATH).
> **GREENLIT:** nothing yet.

## Purpose
A standalone Windows bash interpreter written in C#. Targets a bash subset: full POSIX core plus the most-used bashisms (`[[ ]]`, arrays, `local`, arithmetic expansion, brace expansion). Intended as a drop-in replacement for msys2 bash on Windows without requiring msys2.

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
  interrupted and reported as 124 / 143 with `--preserve-status`).
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
- **`tests/run-tests.ps1`** — self-checking runner. Builds Release, runs each
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
