# Project Context: Bash

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
    Evaluator/           ← AST → execution (Evaluator, WordExpander, ArithParser, Builtins, ShellEnvironment, ShellOptions)
    IO/
      LineEditor.cs      ← readline-style editor (cursor, editing, history nav, tab)
      History.cs         ← command history + ~/.bash_history persistence
      CompletionEngine.cs← command (builtin/func/PATH) + filename + variable completion
      PromptExpander.cs  ← PS1/PS2 backslash-escape + promptvars expansion
  tests/               ← run-tests.ps1 + cases/ + expected/
  tools/               ← bench.csx throughput harness
  Bash.sln
```

`Program.cs` parses invocation flags (`-c`, `-l`/`--login`, `--norc`,
`--noprofile`, `--rcfile`, script file), sources the appropriate startup files
(login: /etc/profile + ~/.bash_profile|.bash_login|.profile; interactive:
/etc/bash.bashrc + ~/.bashrc; script: $BASH_ENV), sets defaults (BASH_VERSION,
PS1=`\s-\v\$ `, PS2=`> `), then runs the REPL: PROMPT_COMMAND → expand PS1 via
`PromptExpander` → `editor.ReadLine`. Startup files are sourced through
`Evaluator.SourceFile`; `-c`/scripts run via `Evaluator.RunString`. The editor
falls back to `Console.ReadLine` when input is redirected.

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

**Classification:**
- ✅ trivial: `cat`⬡ `head`⬡ `tail`⬡ `tee`⬡ `wc` `tr` `cut` `nl` `rev` `tac` `cmp`⬡
  `comm` `uniq` `paste` `fold` `rmdir` `touch` `yes` `cal` `factor`
  (done: `basename` `dirname` `seq` `mkdir`; builtins: `echo` `printf` `test`/`[` `pwd`
  `true` `false` `sleep` `env`).
- ✅ moderate: `cp` `rm`⚠ `mv`⚠ `ls`(scoped) `sort` `find` `xargs` `expr` `date` `du`
  `od` `split`/`csplit` `join` `dd`⬡ `diff` `uname` `hostname` `kill`(PID + TERM/KILL).
- 🟠 large (engines, feasible but big; do last/optional): `grep`/`egrep`/`fgrep`
  (.NET Regex, BRE/ERE dialect caveat), `sed`, `awk`, `bc`/`dc`.
- ❌ Windows-infeasible (fail test 1 — kernel/identity/tty): `ps` `chmod` `chown`
  `chgrp` `id` `who` `tty` `stty` `mount`/`umount` `sync` `nice` `nohup` `mknod`
  `mkfifo` `ipcs`/`ipcrm` `crontab`/`at` `lp`/`lpr` `passwd` `su` `login` `mail`/`write`
  `ln -s`; `df`/`tput`/`clear` partial/degraded.
- ⏭ skip: editors (`ed`/`vi`), pagers (`pg`/`more`), toolchain (`cc`/`make`/`ar`),
  archives (`tar`/`cpio`/`pax`), `compress`/`crypt`/`spell`/`banner`/`units`/`pr`.

**Build waves (feasible set, by usage):**
- Wave 1 — DONE: `basename` `dirname` `seq` `mkdir`.
- Wave 2 — DONE: byte-faithful stdout accessor, `cat`, `head`, `tail`, `wc` (-l/-w/-c),
  `rev`, `tac`, `tr` (ranges/\esc/[:class:], -d/-s), `cut` (-d/-f, -c; n,n-m,n-,-m lists),
  `uniq` (-c/-d/-u), `nl`, `fold` (-w), `touch`, `rmdir`, `cmp` (byte, exit 0/1/2),
  `tee` (-a), `comm` (-1/-2/-3), `paste` (-d). (head/tail `-n` only.)
  - **`yes` DEFERRED**: it's an infinite producer, and neither pipeline model gives
    SIGPIPE-style consumer-close, so `yes | head` would hang (sequential buffers forever;
    threads never close the read-end when the consumer exits). Needs consumer-exit→pipe-
    close first.
- Wave 3 — DONE: `rm` (-r/-f, refuses filesystem root), `mv` (multi-src→dir, overwrite,
  cross-volume copy+delete fallback for dirs), `cp` (-r, multi-src→dir). Destructive →
  footgun-guarded. Also fixed a pre-existing `test`/`[` gap: `!` negation (`[ ! -f x ]`).
  Guarded by `tests/cases/destructive.sh`.
- Wave 4 (in progress): DONE — `uname`, `hostname`, `factor`, `cal`, `date`
  (+FORMAT strftime subset), `kill` (PID; -0 existence; signals → forceful Kill),
  `expr` (arith/relational/string `length`/`substr`/`index`; anchored `:` regex match),
  `du` (-s/-b/-h), `od` (-c/-tx1/-b/-An; default octal words),
  `sort` (-n/-r/-u/-f, -k single-field, -t delim), `split` (-l lines / -b bytes[k|m|g],
  prefix+aa/ab/...), `find` (-name/-iname/-type/-maxdepth, pre-order walk, ignores
  unknown predicates), `ls` (SCOPED: -a/-A/-r/-d, one-per-line, NO -l — owner/perms/
  mtime not byte-faithful on Windows; Ordinal sort),
  `xargs` (-n/-0/-I TOKEN; default echo; dispatches via `Evaluator.RunCommand`),
  `diff` (LCS, GNU normal format NcM/NdM/NaM; -q brief, -i ignore-case; line-based),
  `hash` (-r/-p/name; backs the PATH cache used as ExecExternal's resolution fast-path),
  `grep`/`egrep`/`fgrep` (-i/-v/-n/-c/-l/-w/-F/-E/-r/-q/-h/-H/-e),
  `which` (PATH-only, -a; not builtins — use `type`),
  `sed` (-n/-e/-r/-E; `[addr]s/re/repl/g·p·i·N`, `[addr]d`, `[addr]p`; addr = N·$·/re/;
  & and \1..\9 in repl; no -i in-place, no addr ranges/hold-space).
  **Regex:** grep & sed default to **BRE** (translated to .NET via `BreToNet`: `\(...\)`,
  `\{m,n\}`, `\+ \? \|` are operators); `-E`/`-r` selects ERE; `-F` literal.
  **Wave-4 coreutils complete; PATH hash cache done; grep/sed/which added.**
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
- How to handle Windows path separators in cd / file args? (Unix root mapping
  deferred — see DECISIONS.md)

## Test Assets
- **`tests/run-tests.ps1`** — self-checking runner. Builds Release, runs each
  script-mode test, compares STDOUT (stderr discarded) line-by-line against
  `tests/expected/<name>.out`, prints PASS/FAIL, exits non-zero on any failure.
  `-NoBuild` skips the build. Expected files are authored from bash semantics
  (not captured), so a regression FAILs rather than being baked in. Currently
  36 cases (…, hash, grep, sed) — all green. The build step first kills
  any stale interpreter instance under the project (a hung test process locks
  Bash.exe and fails the copy).
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
- `tests/bench/{loop,arith,func}.sh` — throughput benchmarks (Release exe + timer).

## Out of Scope
- Full bash 5.x fidelity (coproc, process substitution <(), etc.)
- Real job control with terminal hand-off / Ctrl+Z suspend (no fork/SIGTSTP on
  Windows). Basic background `&`/`jobs`/`wait` ARE implemented (Phase 10).
