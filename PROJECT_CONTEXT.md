# Project Context: Bash

> ## RESUME ANCHOR (rewritten 2026-10-18) — read this before anything else
> Replaces the 2026-10-16 head, which had filled with resolved items; that text is in hg rev 316's
> `PROJECT_CONTEXT.md`, and every item in it is resolved with a DECISIONS entry or carried below. The tail (KNOWN
> LIMITATIONS, SETTLED, DO NOT undo, machine facts) is kept.
>
> **TRUE STATE (verified 2026-10-18):**
> - ON A BREAK (the architect, 2026-10-18: "lets publish and we can take a break until someone files a bug"):
>   nothing is greenlit. The next work starts from a bug report (a peer session's or the architect's); the held
>   items below stay held.
> - `dist/Bash.exe` = rev 324 (`1.0.0+hg.7b2c529cd84f 324`, by tools/deploy.sh 2026-10-18; rollback
>   `dist/Bash.exe.rev320`). Suite 160/160 both modes on that AOT binary; battery 226/237. Told: gps-time-2c,
>   adc-framework-d8, msp430-a2, nupkg-28, koliada-net-7c, daq-fe, guy-97, ananta-f5. A Claude call ~11 ms
>   (rev 320 ~12, rev 316 ~13, back to back); compare3 startup 8.5 ms.
> - GitHub main = git `9943884` = hg rev 325 (published 2026-10-18 by tools/publish.sh: the architect, "Yes, deploy
>   and publish"). hg is clean apart from the untracked `The Windows Shell Tax.url` (not ours, leave it).
> - The architect starts Claude Code through a start file (README "Start Claude Code with no PATH of its own"):
>   `BASH_PATH=%PATH%`, `CLAUDE_CODE_GIT_BASH_PATH=F:/Koliada/Tools/bash/dist/Bash.exe`, `PATH=F:/Koliada/Tools/bash/dist`.
>   claude.exe's PATH is `dist`; the shell's is the full Windows PATH. A session whose Bash PATH is `dist` alone was
>   not started that way: `hg` is then `C:/Program Files/TortoiseHg/hg.exe`, and the build, deploy, publish and
>   bench tools need the full PATH handed to them. The status line runs `C:/Windows/py.exe` (claude.exe has no PATH).
>
> **DONE 2026-10-16 (do NOT re-open; DECISIONS has each):** review R1 (a Claude call's anatomy) · lazy binding of
> ws2_32/iphlpapi/secur32 (rev 277, kept) · `BASH_PATH` + the command map (`~/.bash/commands`, one file per PATH
> value; hits from every lookup; misses recorded and obeyed by `hash name` alone, an EARLIER process's; touched on
> read hourly, pruned after 30 days unused): a Claude call ~16 ms, was ~31 · the exit trace (exit ~0.25 ms as any
> AOT program; the ~3 ms extra is start-up; R1's "+3 ms exit" was the timing switch itself) · `a[k]+=v` (rev 285) ·
> README reworked (revs 287, 289).
>
> **DO NOT UNDO / DO NOT DERIVE:** the map's folder (`~/.bash/commands`: the architect's call, not AppData) ·
> "only `hash` records and obeys misses" (Claude's call, delegated; falsifier in DECISIONS) · `BASH_PATH` removed
> from the process environment at startup (a nested shell keeps its parent's PATH) · no change to `$BASH_COMMAND`
> for compound commands without raising it · namerefs (`local -n`) and shopts localvar_inherit/unset: out of scope.
>
> **DONE 2026-10-17/18 (do NOT re-open; DECISIONS has each), all deployed in rev 316:** the four defects (PATH `#!`
> scripts run, no cwd script by bare name; `hash -d`; relative paths from the shell's cwd; PS4, `$EPOCHREALTIME`) ·
> background jobs in their own directory · no empty PATH entry reaches Windows · C#Bash's lookup the only authority
> (Windows' launcher never searches the cwd) · a subshell's `exec` ends with it · eight smaller GNU differences and the
> `hash` crash · the red team's lookup findings (`~` entries, `/f/x` `\x` `C:x`, a child's environment = the exported
> variables, missing interpreters, cmd.exe by a `/` path) · MSYS tools under an inherited `>>` · set -x as bash
> (assignments, `++`, quoting, PS4 escapes, compound commands) · long paths (`cd` via the longPathAware manifest; the
> Windows limit said) · RULED: only .com .exe .bat .cmd (and .sh, #!) are programs; `..` after a junction stays
> lexical; the shell's `$PATH` keeps empty entries as given.
>
> **AWAITING THE ARCHITECT (do NOT derive; Claude's positions are proposals):**
> 1. The performance review is DONE (R2, R3, R4 in DECISIONS 2026-10-18). The architect picked R4's 1-3 ("1, 2, 3
>    concur; go, deploy, notify, publish"): BUILT (DECISIONS "BUILT (R4 picks 1-3)"): icu.dll loads only when needed,
>    the `[[ ]]` trace text only under set -x, the start-up split; deployed rev 320, published rev 321. Then ("ok,
>    your recommendations; go") BUILT, NOT DEPLOYED (DECISIONS "BUILT (the split's candidates…)"): the Git-tools probe
>    (mingw64/bin, slash-blind compare), PPID's pseudo-handle, the invariant culture (U+2212), printf's inf/nan.
>    Then ("Awk - your way") BUILT: the in-process awk never runs a program twice (literal formats and RS refused at
>    parse time, a refusal while running loud, 2) and out-of-range numbers as gawk 5.1 (DECISIONS "RULED + BUILT: the
>    in-process awk never runs a program twice"). AWAITING the architect: R4's 4-9 (held: calls are start-up bound)
>    and OutputEncoding 0.25 ms. NO optimisation code before a pick.
> 2. Parked, not greenlit: serial I/O (COMn as a device file + `stty`, "to discuss"); `compgen -c` scanning PATH;
>    the battery's reference moved to GNU.
> 3. Not raised (measured 2026-10-12): bash keeps finished jobs in its table until it reports them (`jobs` lists
>    them as Done, numbers keep counting); C#Bash's `wait` removes them.
>
> 4. Not done, recorded (the architect's to schedule): an independent red-team pass on revs 307-316 (the subshell
>    `exec`, the eight GNU differences, set -x, long paths, the MSYS append handle, PATHEXT, and the fixes to the red
>    team's own findings); the no-windows hook's false positive (`Start-Process … -NoNewWindow` flagged when the flag
>    follows a `$( )`); the user Path's trailing `;` (tidiness only, harmless: measured).
>
> **RULES:** deploy only on "deploy and notify"; publish only on "publish" / "push".
>
> **KNOWN LIMITATIONS (documented, not raised for action):**
> - Windows starts no program whose path, or whose working directory, is over 260 characters (cmd neither): C#Bash
>   says so, 126; the shell itself can `cd` there (longPathAware manifest, 2026-10-17). Win32 drops a trailing dot or
>   space from a file name: command lookup refuses `s1.`, but `cat s1.` reads `s1` (every file operation).
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
>   `ps` fields beyond pid/ppid/comm/args fail loudly or fall through to a PATH tool; awk falls through only
>   before its program starts (a refusal met while it runs is loud, 2: never a second run, 2026-10-18).
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
> - **Repetitive tools are CONTRACTS (the architect, 2026-10-12: "repetitive tools here need to be
>   contracts, period").** A check that is run more than once lives in the repo, runs one way, and
>   is invoked as written: never rebuilt from memory, a transcript, or a scratch directory. The three:
>   `tests/bench/compare3.sh [--build | --cs <exe>]` (the benchmark; `--build` is THE way to measure
>   an undeployed rev), `tests/corpus/run-n.sh` (the `-n` corpus), `tests/compat/run-compat.ps1`
>   (the battery; no `-Reference` needed), `tools/deploy.sh` (on "deploy": publish to dist.staging,
>   suite both modes + battery on it, rename-and-swap into dist, rollback `dist/Bash.exe.rev<old>`),
>   `tools/publish.sh <msg> <token-file>` (on "push": needs a compare3 run on the deployed build and
>   the committed doc review; regenerates the history files, stages exactly hg's files, pushes,
>   verifies). All run from Git Bash or PowerShell as their headers say.
> - The benchmark is `tests/bench/compare3.sh`, run unmodified from Git Bash; its startup
>   row is the median of 5 samples since 2026-10-10 (ratified: "Median of 5 sounds fair"). Method
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
> `F:/Tools/PortableGit/usr/bin/_bash.exe`; the `bin/` stub is broken by the rename: re-verified 2026-10-16, it
> looks for `usr/bin/bash.exe`), and
> `System32\bash.exe` (WSL) was moved away, both so nothing pre-empts C#Bash. Reach WSL with
> `wsl.exe -e bash`. Run Git Bash scripts as `"$BASH" script` from `_bash.exe -l`. Claude Code sets
> `NoDefaultCurrentDirectoryInExePath=1`.
>
> **The global CLAUDE.md is CLAUDE'S file** (the architect, 2026-10-17: "it is _not_ my file, it is
> 'claudes'"): fix a stale line there directly and commit it (`~/.claude` is a git repo). Done
> 2026-10-17, git `4ef2f2e`: the C#Bash limits re-checked on rev 286 (UNC globs expand, awk's 3-argument
> `match()` works, two new limits), the start-file arrangement added, the obsolete launcher wording gone.

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
    app.manifest         ← longPathAware (cd into directories over 260 characters), asInvoker, Windows 10
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
      true terminal hand-off), `bg` (reports unsupported). `DEBUG`/`ERR`/`RETURN` traps
      BUILT since (rev 217). Deferred: `kill %n`, Ctrl+Z suspend, real fg/bg (need fork / SIGTSTP /
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
  magnitude. Extended 2026-10-10 (rev 193, DECISIONS "SUBSET EXTENDED: awk user functions …"):
  user functions, `print`/`printf > file` / `>> file`, `getline [var] < file`, `close`, `fflush`,
  3-argument `match`, `--version`; checked against gawk 5.1 by `tests/cases/awk_extended.sh`.
  Recursion stops with a fatal error at about 900 calls deep on AOT (300 on JIT), never a crash.
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
  publish or `dist/`. The runner prints the interpreter and build it tested. A case registered
  `AotOnly = $true` (icu_lazy, 2026-10-18) runs only on the Native AOT build (no `Bash.dll` beside the
  exe) and is reported "SKIP" on a line of its own otherwise; tools/deploy.sh runs the suite on the AOT build.
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
  the Git that owns `git.exe` on PATH (portable installs included; its root is the first ancestor
  with `usr\bin`, and `usr\bin\_bash.exe` counts), Program Files, or PATH —
  never `System32\bash.exe` (WSL, a different OS; it silently became the reference once when
  the battery was launched from PowerShell). Each candidate is RUN: C#Bash (any build) or one that
  does not start is skipped, and `-Reference` naming C#Bash is an error (2026-10-12: with
  `CLAUDE_CODE_GIT_BASH_PATH` naming the deployed C#Bash the battery had compared C#Bash with itself).
- **`tests/corpus/run-n.sh`** (from Git Bash) — the `-n` corpus: C#Bash's `bash -n` verdict against GNU's
  on WSL's 912 bash-completion scripts (fetched each run into `tests/corpus/files`, ignored: third-party
  files are not committed). `manifest.txt` pins each file's sha256 and GNU's verdict (drift stops the run,
  exit 2); `baseline.txt` is the ratchet (844 at rev 230; a lost agreement is exit 1). Builds Release
  first unless `--no-build` / `--cs`.
- **`tests/bench/compare3.sh --build`** — the benchmark of the committed tree: refuses a dirty tree,
  publishes AOT with `tools/publish-aot.cmd` to `tests/bench/aot` (ignored), checks the stamp, and times
  it against `dist/Bash.exe` in alternating rounds (columns `deployed` and `change`). `work/` is scratch (ignored). Seeded 2026-09-04
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
