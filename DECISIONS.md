# Decisions

## 2026-06-14 — expr + FIXED quoted-glob/brace expansion
**Decision:** Added `expr` (recursive-descent over arg tokens: `|`/`&`, relational,
`+ - * / %`, anchored `:` regex match with BRE `\(\)`→`()`, and `length`/`substr`/`index`).
Testing `expr 2 '*' 4` surfaced a real bug: **quoted globs/braces were being expanded** —
`echo '*'` listed files, `echo "*.cs"` matched, `echo '{a,b}'` brace-expanded — because
`ExpandToFields` ran brace/glob over ALL result fields, having discarded quotedness.
**Fix:** the field-builder now records, per field, whether it contains an *unquoted*
metachar (`{ } * ? [`) in a parallel `fieldGlob` list; brace and glob expansion apply
only to flagged fields. Quoted text is literal; unquoted `*`/`{a,b}` still expand.
**Consequences:** correctness fix touching core `ExpandToFields` (suite 24/24 re-verified).
Edge case: a field mixing quoted and unquoted metachars (e.g. `'*'*`) is flagged from the
unquoted one and globs the whole field — rare, documented. `expr`'s `:` uses .NET regex
(BRE approximation), so exotic POSIX BRE may differ.

## 2026-06-14 — Wave 4b: date/kill + /dev/null & fd-aware redirects & error-continue
**Decision:** Added `date` (+FORMAT strftime subset; clock-set unsupported) and `kill`
(PID; -0 = existence; any signal → forceful `Process.Kill`, since Windows lacks graceful
per-signal delivery; `%n` job-specs unsupported). Testing surfaced redirect bugs, fixed:
- `/dev/null` (+ `/dev/stdout`/`/dev/stderr`) handled in `ApplyRedirects` — previously
  `TranslatePath` turned it into `F:\dev\null` and `File.Create` threw **uncaught**,
  crashing the shell (e.g. any `2>/dev/null`).
- Output redirects are now **fd-aware**: `2>file` redirects stderr (was always stdout).
- A failed redirect / runtime `EvalException` (div-by-zero, etc.) now fails just that
  command; `ExecScript`/`ExecList` catch it via `RunStatement`, print `bash: …`, set
  $?=2, and continue — matching bash (these don't abort a script).
**Consequences:** robustness win (no more crash on bad redirect path). **Known gap:**
EXTERNAL commands still ignore `2>file`/`2>/dev/null` — `ExecExternal`'s fd-map only wires
fd 1; stderr leaks (becomes moot as tools become builtins, but needs its own fix).
Guarded by `tests/cases/redirect.sh`; suite 23/23.

## 2026-06-14 — Wave 3: rm/mv/cp (destructive, guarded) + fixed `test !` negation
**Decision:** Added `rm` (-r/-f; **refuses to recursively delete a filesystem root** as a
footgun guard), `mv` (multi-src→dir, overwrite, cross-volume copy+delete fallback for
directories since `Directory.Move` can't cross volumes), `cp` (-r, multi-src→dir). Glob is
already expanded by the shell, so these just operate on the given paths. Also fixed a
pre-existing `test`/`[` gap surfaced by the wave-3 test: leading `!` negation
(`[ ! -f x ]`) now works. Guarded by `tests/cases/destructive.sh`; suite 21/21.
**Rationale:** rm/mv/cp are everyday; the root guard + careful per-path error handling
keep the destructive ops safe. The `test !` fix unblocks the most common conditional idiom.
**Consequences:** `rm -i` (interactive prompt) is ignored (non-interactive); `cp -p`
(preserve attrs) not honored yet. Wave 4 (ls/sort/find/date/… + PATH hash cache) next.

## 2026-06-14 — Wave 2 finished (touch/rmdir/cmp/tee/comm/paste); `yes` deferred
**Decision:** Added `touch`, `rmdir` (empty-only), `cmp` (byte compare; exit 0 silent /
1 differ / 2 error), `tee` (-a), `comm` (sorted, -1/-2/-3), `paste` (-d, cycled).
Guarded by `tests/cases/fileutils.sh`; suite 20/20. **`yes` deferred** — it never
terminates, and neither pipeline model implements SIGPIPE-style consumer-close, so
`yes | head` hangs (sequential buffers the infinite stream; the thread model doesn't
close the read-end when the consumer exits). Implement consumer-exit→pipe-close before
adding `yes` (and it'd also benefit `tail -f` and huge `cat | head`).
**Consequences:** wave 2 (text/file utilities) complete bar `yes`. `rm`/`mv`/`cp`
(wave 3, destructive) next.

## 2026-06-14 — Fixed: builtin-pipeline race (sequential) + CRLF output (LF)
**Decision (debt-first, before more builtins):** (1) An all-in-process pipeline
(`AllStagesInProcess`: every stage a builtin/function/assignment with a literal name)
now runs **sequentially** in `ExecPipelineSequential` — each stage's stdout is captured
and fed as the next stage's stdin via the existing `ExecuteWithStdin`. No concurrent
threads ⇒ no race on the process-global `Console`. Pipelines with any external keep the
thread model (externals use real pipe handles and don't touch Console).
(2) `NewLine = "\n"` is set on Console.Out/Error at startup and on every StreamWriter we
install (redirect targets, pipe stages) and capture StringWriter, so output is LF like
bash (`echo hi | wc -c` = 3, not 4).
**Rationale:** these were compounding tech debt — every new builtin made `a|b|c` more
likely to hit the race, and CRLF corrupted byte counts / exact-byte output. Fixing the
foundation before finishing wave 2.
**Consequences:** sequential stages buffer in memory (fine for typical sizes) and pass
text between stages, so a binary mid-pipe loses byte-fidelity (rare). A builtin sandwiched
*between two externals* in a mixed pipeline can still race on Console (uncommon; documented,
revisit if hit). Guarded by `tests/cases/pipeline.sh`; full suite 19/19.

## 2026-06-14 — Wave 2 text utils + discovered: builtin pipeline race & CRLF output
**Decision:** Added `tr` (ranges/\escapes/[:class:], -d/-s), `cut` (-d/-f, -c, list
ranges), `uniq` (-c/-d/-u), `nl`, `fold` (-w) as builtins (guarded by
`tests/cases/fieldutils.sh`). All correct standalone and in 2-stage pipes.
**Discovered two pre-existing issues (now the top priority — see PROJECT_CONTEXT):**
1. **Multi-stage all-builtin pipelines race.** Stages run as concurrent threads in
   `ExecPipelineViaThreads`, but builtins use process-global `Console.In`/`Out` swapped
   per stage — 3+ all-builtin stages clobber each other → "closed pipe". Externals are
   unaffected (they get real pipe handles). Right fix: thread-local I/O for builtins, or
   run all-in-process pipelines sequentially with buffering (`ExecuteWithStdin` already
   does stdin-inject + capture). Must fix before adding many more builtins, since it
   makes `a | b | c` of builtins unreliable.
2. **Output is CRLF** (`Console.WriteLine`), so byte counts/redirected text carry CRs
   (`echo hi | wc -c` = 4 vs bash 3). Hidden by the line-based runner. Fix: set
   `NewLine="\n"` on Console.Out and installed StreamWriters.
**Consequences:** committed test deliberately uses only single/2-stage pipes. These two
fixes come before wave-2 finish (`tee`/`comm`/`paste`/`cmp`/`touch`/`rmdir`/`yes`).

## 2026-06-14 — Byte-faithful stdout for builtins (`CurrentRawStdout`) + cat
**Decision:** Builtins normally write through `Console.Out` (text), which re-encodes —
fine for echo, wrong for `cat`/`head`/`tail`/`tee`/`cmp`/`od`. Added
`Evaluator.CurrentRawStdout()` returning the raw byte sink for the current command:
file-redirect `FileStream` (new `_rawStdout`, saved/restored by `RedirectScope`), else
the pipeline stage's `_pipeStdout`, else `Console.OpenStandardOutput()`; returns null
when output is `$(...)`-captured (`_capturing`, set at both capture sites) so the caller
writes decoded text into the capture instead. `cat` uses it: raw `FileStream.CopyTo` for
file operands (byte-faithful — verified a binary copy is cmp-identical), text for the
capture path; flushes `Console.Out` before raw writes to keep ordering.
**Rationale:** can't rely on `Console.Out is StreamWriter` (SetOut wraps in a
synchronized writer), so the raw sink is tracked explicitly alongside Console.Out at the
redirect/pipe/capture set-points.
**Consequences:** stdin for `cat -`/no-args is still the text path (a later raw-stdin
accessor would make `cat < binary` byte-faithful too). High-blast-radius change
(ApplyRedirects/RedirectScope/capture) — full suite (16) re-verified green.

## 2026-06-14 — Coreutils scope = AT&T System V set filtered by feasibility
**Decision:** Target the classic SysV command set (user's muscle memory) rather than a
hand-picked few, gated by complexity tests: self-contained (no uid/gid/mode/signal/tty/
device/fork), bounded (no embedded regex/lang engine), byte-faithful I/O, destructive-
safe. Everything that PASSES gets built, ordered by usage; engines (grep/sed/awk/bc) are
"large/optional, last"; Windows-kernel-dependent commands (ps/chmod/chown/id/tty/mount/
…) are excluded because they can't be *faithful* on Windows — an unfaithful one silently
breaks scripts that parse it (worse than a clean "not found"). Full classification +
build waves in PROJECT_CONTEXT.
**Rationale:** the SysV set is small, stable, and mostly predates the kernel-entangled
features that don't exist on Windows, so it filters cleanly. Frequency ordering is
reasoned from NL2Bash / customization-study / PaSh (each biased; no clean per-command
table exists) plus everyday judgment.
**Supersedes** the earlier "bash-adjacent, not busybox" boundary — the user opted to
include any utility that passes the complexity test, not just shell-syntax-adjacent ones.

## 2026-06-14 — In-process coreutils: scope = bash-adjacent, not busybox
**Decision:** To cut Windows process-spawn cost, implement a curated set of hot, simple
utilities as builtins rather than spawning them. Scope is deliberately bash-leaning:
prefer utilities the shell's own syntax half-covers, and stop before the heavy
text-processing tools. **In:** `basename`, `dirname`, `seq`, `mkdir` (tier 1, done);
planned `cat`, `tail`, then `rm`, `mv`, then `ls`, plus a PATH `hash` cache. **Out:**
`grep`/`sed`/`awk`/`find` (a regex engine + language — OS paradigm, endless GNU-flag
compat) and **`ps`** (Unix TTY/STAT/TIME columns can't be reconstructed on Windows; a
faithful impl is near-impossible and an unfaithful one silently breaks scripts that
parse it).
**Rationale:** each builtin turns a ~10ms spawn (DECISIONS 2026-06-14 spawn-floor
finding) into a ~µs call. Frequency data (NL2Bash, command-line-customization study,
PaSh pipelines) is individually biased but converges on this simple hot set.
**Consequences:** `cat`/`tail -c` must write RAW bytes through redirects/pipes, not the
text Console path (the 2026-06-13 cat/UTF-8 lesson) — requires a raw-stdout accessor
for builtins, handled in tier 2. `rm`/`mv` are destructive — careful error handling.
`ls -l` will be best-effort (no Unix perms/owner on Windows). Glob is already expanded
by the shell before args reach these builtins.

## 2026-06-14 — Perf round 3: single-`$var` expansion fast-path
**Decision:** `ExpandToFields` now fast-paths a word that is a single bare
`$var`/`${simple}` (a `BraceExpansionPart` passing `IsSimpleParam`, excluding `@`/`*`)
under default IFS: it returns the value as one field, or `[]` if empty/unset, when the
value has no whitespace (no split needed) and no brace/glob metachars. Falls through to
the full field-builder otherwise.
**Result:** `EtoF $i` 256→64 B / 81→36 ns; loop 426→356 ns, 1296→1100 B; func.sh
1115→922 ns. Cumulative this session: loop ~711→356 ns/iter (≈50%), 1940→1100 B/iter.
**Correctness:** exact — empty→zero fields, whitespace→full split path, glob/brace→full
path, `$@`/`$*` excluded, non-default IFS excluded. Guarded by `tests/cases/varsplit.sh`
(unquoted `$var` with spaces splits; empty→zero iterations). Suite 14.
**Remaining (diminishing):** per-command `args`/`tempAssign` lists, the ExpandVarsInArith
StringBuilder, exception-based break/continue.

## 2026-06-14 — Perf round 2: ArithParser inline rewrite (+ fixes && / || / hex)
**Decision:** Rewrote `ArithParser` from the `ParseLeft(Func operand, params Op[])`
design to explicit per-precedence inline methods (each calls the next level directly),
with substring-free number parsing. No delegates, no per-call arrays. Two-char
operators are matched before the single-char ops sharing a prefix, and the single-char
bit ops (`&`,`|`,`*`,`<`,`>`) decline when a second identical char follows.
**This fixed two real bugs the perf test exposed:** `$((1&&1))` and `$((1||0))` used to
THROW ("Unexpected '&'/'|'") because the deeper bit-level `&`/`|` consumed the first
char of the logical operators; and `$((0xff))` returned 0 because `ExpandVarsInArith`
(WordExpander) treated the `ff` after `0x` as an unset variable — now it copies a full
numeric literal (incl. `0x…`) verbatim.
**Result:** arith-eval 384→224 B / 144→76 ns; loop 516→422 ns, 1460→1296 B; arith.sh
924→686 ns. Cumulative this session: loop ~711→422 ns/iter. Residual arith allocation
is now `ExpandVarsInArith`'s StringBuilder, not ArithParser.
**Consequences:** arithmetic remains single-pass non-short-circuiting (`$((1||(1/0)))`
still evaluates the RHS — unchanged from before; this evaluator can't skip-parse).
Guarded by `tests/cases/arith.sh` (all operators incl. `&&`/`||` discriminators + hex).
Suite now 13.

## 2026-06-14 — Shebang dispatch for running scripts as commands
**Decision:** When a command resolves to an existing script file, `ExecExternal` reads
its `#!` line (or treats a `.sh` file with no shebang as a shell script) and dispatches
itself, because Windows `CreateProcess` can't launch a `#!` script. bash/sh interpreters
run **in-process** in a fresh child `Evaluator` (inherits our exported vars, isolated
mutations, `exit` ends only the script and propagates its code); any other interpreter
is spawned via `ExecExternal(basename(interp), [shebangArgs…, scriptPath, args…])`,
honouring `/usr/bin/env CMD` (real interpreter = CMD). The interpreter is launched by
**basename so PATH resolves it** — Unix paths like `/usr/bin/python3` won't exist on
Windows (consistent with the deferred Unix-root-mapping decision).
**Rationale:** makes `./foo.sh` work. Doing bash/sh in-process avoids needing an
external bash and is exact (we are the shell). Detection only fires for files that
actually exist at the given path and are `#!`/`.sh`, so native `.exe`/PATH commands are
untouched (no first-line read for them).
**Consequences:** the in-process child is a *fresh* Evaluator seeded from
`GetExportedVars()` — it does not see non-exported shell-local vars (correct child-shell
semantics). PATH-resident extensionless scripts aren't auto-detected yet (only explicit
paths / cwd files). Test: `tests/cases/shebang.sh` runs `./cases/inner_sh.sh` and checks
output + exit-code propagation. The runner now also kills stale interpreter instances
before building (recurring locked-Bash.exe build failures).

## 2026-06-14 — Perf round 1: literal fast-path + static arith op-tables
**Decision:** Two allocation-reducing changes, each A/B'd with `tools/bench.csx`
(`--no-cache`): (1) `ExpandToFields` returns `[lit.Value]` directly for a single
LiteralPart with no brace/glob metachars — skips the per-call `StringBuilder` and
field-builder; (2) `ArithParser`'s per-precedence-level operator tables are now
`static readonly` fields (via an `Op` tuple alias) instead of `params[]` literals
allocated on every `Parse*` call.
**Result:** loop benchmark 686→516 ns/iter, 1940→1460 B/iter; `EtoF literal`
232→64 B. The arith hoist helped time (~175→144 ns/eval) but NOT allocation —
allocation attribution showed the arith cost is the ~10 `Func<long>` operand
delegates (instance method-group conversions) + number substrings, not the arrays.
**Process note:** `dotnet-script` caches compiled scripts; an unchanged bench.csx
served a stale DLL binding and faked a "no change" result until re-run with
`--no-cache`. A/B perf only with `--no-cache`.
**Consequences:** literal fast-path is exact (literals never word-split; metachar
guard preserves brace/glob). Suite 11/11 green. Deferred: ArithParser inline-call
rewrite to kill the operand-delegate/substring allocation (needs care with `||`/`|`
and `&&`/`&` matching + dedicated arith correctness tests), and a `$var` fast-path.

## 2026-06-14 — Perf measurement: in-process harness is authoritative; trace misled
**Decision:** Adopt `tools/bench.csx` (dotnet-script, references Release `Bash.dll`
in-process, best-of-6, reports ns/iter + bytes/iter + GC counts) as the authoritative
perf measure. `dotnet-trace` cpu-sampling is kept only as an optional pointer, not a
source of truth.
**Why:** A `dotnet-trace` profile of the tight loop attributed ~87% to
`List.AddRange`→`CopyTo` and pointed at `ExecSimpleCommand`'s `args.AddRange`. A
controlled A/B (replace `AddRange` with `Add` for the 1-field common case) moved the
loop only ~711→680 ns/iter — within noise. The trace was coarse (~2.7k samples over
2.2s) and its own EventPipe instrumentation appeared in the stacks, so the `CopyTo`
attribution was largely the tracer, not us.
**Findings:** the interpreter is allocation-bound (~1940 B/iter in the loop body) with
no single CPU hot spot; gen0 GC is cheap, so it's not GC-bound either. Meaningful gains
require broad allocation reduction (ExpandToFields list/closure machinery, per-command
lists, StringBuilder churn), not a hot-method fix.
**Consequences:** Kept the Add-vs-AddRange micro-opt (correct, marginally fewer
instructions) but did not claim it as a win. Deeper allocation-reduction refactor is
deferred and must be A/B'd with bench.csx. Lesson recorded: don't optimize on a coarse
profile without an in-process A/B.

## 2026-06-14 — Lexer Peek() made bounds-safe (operator at EOF crash)
**Decision:** `Lexer.Peek()` now returns `'\0'` at/after end of input instead of
indexing past the buffer. **Bug:** a control operator as the final character —
e.g. `sleep 5 &` with no trailing newline — made `ConsumeOperator` consume the
`&` then `Peek()` for a second `&`, reading `_src[_src.Length]` →
`IndexOutOfRangeException`, crashing the whole process (found via manual job-control
testing). Affected every operator at true EOF (`& | ; < >`).
**Rationale:** A `'\0'` sentinel fixes all lookahead sites at once (`Peek() == 'x'`
checks simply see "not that char"); content-scanning loops already guard with
`_pos < _src.Length`, so they're unaffected.
**Consequences:** Operators at EOF now produce graceful parse errors or run, never
crash. Regression-guarded by `tests/cases/eof_amp.sh` (a file ending in a bare
`&`, no trailing newline — the exact trigger). Suite now 11 cases.

## 2026-06-13 — Line-editor flicker: hide cursor during redraw + fast-path appends
**Decision:** `Render`/`DrawSearch` now set `Console.CursorVisible = false` around the
reposition+rewrite and restore it after (via `CursorVisibleSafe` for the getter), and
the printable-char handler fast-paths the common case — appending at end-of-line with
room on the current row just `Console.Write`s the one char (no SetCursorPosition, no
full rewrite). Mid-line inserts, wrapping writes, backspace, and history recall still
go through the full `Render`.
**Rationale:** The per-keystroke "jump to anchor, rewrite whole line, jump back" with a
*visible* hardware cursor is the flicker source. Hiding the cursor during the redraw
removes the visible darting; the append fast-path removes the redraw entirely for
ordinary typing.
**Consequences:** Interactive-only, so verified by reasoning + the manual checklist,
not the automated suite. The append fast-path keeps `_prevRenderLen` in sync so a later
full `Render` still erases trailing characters correctly. Backspace/mid-line edits are
now flicker-free (cursor hidden) but still rewrite the tail — fine; the dominant cost
was typing.

## 2026-06-13 — Test harness: PowerShell runner + authored expectations + manual checklist
**Decision:** Correctness tests run via `tests/run-tests.ps1` (PowerShell, external to
the interpreter), which executes each script-mode case against the Release exe,
captures STDOUT only (stderr discarded — job notices/errors live there), and diffs
against `tests/expected/<name>.out` with `Compare-Object`. Expected files are authored
from bash semantics, not captured from the program. TTY-only features live in
`tests/MANUAL.md` as a hand-run checklist.
**Rationale:** An external runner stays trustworthy (not dependent on the thing under
test); authored expectations mean a regression FAILs instead of being silently
re-baselined. Script-mode + stdout-only keeps cases deterministic; scripts are invoked
with paths relative to tests/ so `$0` is stable. Runs with `$ErrorActionPreference =
'Continue'` because tests intentionally write to stderr and PS 5.1 turns native stderr
into a terminating error under 'Stop'.
**Consequences:** Interactive surface (keys, signals, prompt rendering, completion) is
not auto-tested — covered only by the manual checklist. Failure detection verified
(Compare-Object catches line diffs). Benchmarks remain separate (timing, not pass/fail).

## 2026-06-13 — `&&`/`||` short-circuit fixed to be left-associative & list-bounded
**Decision:** Rewrote `ExecList` to evaluate items with a per-connector decision
(run item N based on the *previous* connector and the running exit code) instead of
a flat `break` on the first failed `&&`/`||`. A skipped item leaves the exit code
unchanged so it propagates to the next connector.
**Rationale:** The parser builds one flat `List` across `;`/newlines, and the old
`break` terminated the *entire* list on a failed `&&` — so `false && echo x` swallowed
every following command (even on later lines), and mixed forms like
`false && b || c` were evaluated wrong. Per-connector evaluation gives correct
left-associative `(a && b) || c` semantics and stops short-circuiting at the next
`;`/newline/`&` boundary.
**Consequences:** Pre-existing latent bug, surfaced while testing job control; fixed
here. `a && b &` still backgrounds only `b`, not the whole `a && b` (the flat list
can't group it — documented limitation). Hot-path cost unchanged (~297K loop iters/s).

## 2026-06-13 — Traps: EXIT and INT only; stored-but-inert for the rest
**Decision:** `trap` is a builtin backed by a `Dictionary<string,string>` on Evaluator.
Only `EXIT` (run once via `RunExitTrap` at every shell-exit path in Program.cs) and
`INT` (run in `CheckInterrupt` instead of aborting; empty command = ignore) actually
fire. Other sigspecs are canonicalised and stored but never triggered. Supports
`trap 'cmd' SIG…`, `trap - SIG…` (reset), `trap '' SIG` (ignore), `trap -p`, `trap -l`.
A `_inTrap` guard prevents handlers from re-entering traps.
**Rationale:** Windows has no general POSIX signal delivery, so wiring TERM/HUP/etc.
would be dishonest. EXIT and INT are the high-value, actually-deliverable cases
(cleanup + Ctrl+C handling) and integrate with the existing interrupt mechanism.
**Consequences:** `DEBUG`/`ERR`/`RETURN` pseudo-signals deferred. `trap 'x' TERM`
parses and stores but won't run — acceptable and non-erroring.

## 2026-06-13 — Background jobs as in-process threads (no fork)
**Decision:** `cmd &` runs the command's AST on a background thread tracked in a job
table on Evaluator; `jobs` lists (and reaps) them, `wait [%n]` joins, `fg` waits
(echoing the command — no real terminal hand-off), `bg` reports unsupported. Job
labels come from a literal AST renderer (`DescribeNode`, no expansion/side effects).
**Rationale:** Without `fork`, a separate-process job model would require serialising
all shell state. A thread is the pragmatic Windows analogue and works correctly for
the common case (`sleep … &`, external commands, simple pipelines).
**Consequences:** Background threads share `_env` and the console with the foreground,
so internal state mutation or `Console.SetOut` redirects in a background job race the
foreground — safe for external commands, not for heavy internal work (documented).
`kill %n`, true `fg`/`bg`, and Ctrl+Z suspend are out (need process groups/SIGTSTP).
`a && b &` backgrounds only `b` (flat-list limitation).

## 2026-06-13 — SIGINT (Ctrl+C) handling via a polled interrupt flag
**Decision:** The interactive REPL registers `Console.CancelKeyPress` with
`e.Cancel = true` (so Ctrl+C never terminates the shell) and sets a volatile
`Evaluator._interrupted`. Loop and statement boundaries (`ExecScript`, `ExecFor`,
`ExecWhile`) and the `sleep` builtin call `CheckInterrupt()`, which throws
`InterruptException`; the REPL catches it, prints a newline, sets $?=130, and
continues. The flag is cleared before each command. External processes are
interrupted by the console's own CTRL_C_EVENT (they share the console), so no
extra plumbing is needed for them.
**Rationale:** A cooperative polled flag is simple and thread-safe (the handler runs
on a separate thread) and avoids trying to abort arbitrary .NET work. It interleaves
correctly with the line editor, which sets `TreatControlCAsInput` so Ctrl+C is a
clear-line *key* during editing rather than a signal.
**Consequences:** Only the interactive REPL installs the handler — scripts and `-c`
keep the default (Ctrl+C terminates), matching bash. Interrupt latency is bounded by
the polling granularity (per loop iteration / 50 ms in sleep); a tight non-looping
builtin won't interrupt until it returns. `trap`/SIGTERM/Ctrl+Z (job control) are
still unimplemented. Interactive-only — not exercised in the non-TTY sandbox.

## 2026-06-13 — Throughput benchmarking + hot-path allocation reduction
**Decision:** Added `tests/bench/{loop,arith,func}.sh`, benchmarked against the
Release exe directly (Measure-Command, best of 3). Two hot-path optimisations in
`WordExpander`: (1) a fast-path in `ExpandBraceParam` for bare names / positionals /
special params (skip the operator-detection predicates); (2) `ExpandToFields` only
allocates new lists for brace/glob expansion when a field actually contains `{` or a
glob metachar. The brace/glob guard gave ~13–17%; the var fast-path was marginal
(~1–4%), which located the real cost in pipeline allocation, not variable lookup.
**Rationale:** Establish a repeatable throughput measure and confirm bottlenecks by
measurement rather than guesswork. Baseline now ~303K simple loop iters/sec.
**Consequences:** Correctness preserved (phase5/gap/args re-verified). Bigger remaining
levers — caching parsed `$((…))` expressions and removing exception-based
break/continue — are deferred; documented in PROJECT_CONTEXT. Benchmarks must use the
Release build run as a standalone exe; `dotnet run`/Debug numbers are not comparable.

## 2026-06-13 — `}` recognised as a group terminator in command position
**Decision:** The lexer now treats `}` as `RBrace` when it has leading space OR is
preceded by a newline OR a `;` (previously: leading space only). This fixes the
canonical multi-line function/group form with `}` alone at column 1, which failed
with "Expected RBrace but got Eof".
**Rationale:** `}` is a reserved word in command position; the leading-space-only
heuristic missed `}` after a newline — breaking ordinary multi-line `f() { … }`.
**Consequences:** A `}` adjacent to word characters ({a..e}, pre{x,y}) is still a
plain word. `echo }` (space before a non-command-position `}`) remains treated as a
terminator — a pre-existing heuristic limitation, not changed here.

## 2026-06-13 — All `$`-expansions routed through one path; positional parameters
**Decision:** The lexer's `ConsumeDollar` now consumes the parameter itself for
`$name`, `$1`, and the special params `$@ $* $# $? $$ $! $-`, emitting a single
`DollarLBrace` token (raw = the name) — the same token `${…}` produces. So every
parameter expansion flows through `BraceExpansionPart` → `ExpandBraceParam`, and
the old `Dollar`+`Word` two-token path now only fires for a lone `$`.
`ParseDoubleQuotedInterior` got the matching special-param/single-digit handling.
Added `ShellEnvironment.SetArg0`/`SetPositionals`/`GetPositionals`; `Program.cs`
sets $0 + $1.. for script mode (script path + following args) and `-c` mode (name
+ args, bash semantics). `ExpandToFields` special-cases `$@`/`$*` (as
VarExpansionPart or BraceExpansionPart) through the same `AppendArray` used for
`${arr[@]}`, so `"$@"` splits to one field per parameter and `"$*"` joins by the
first IFS char.
**Rationale:** Unbraced `$#`/`$@`/`$*` were broken — `$#` started a comment, `$@`/`$*`
mis-tokenised, and the double-quoted parser read an empty name. Consuming the
parameter in the lexer (before `#` can start a comment) and unifying on the
`${…}` machinery fixes all forms at once and removes a parallel code path.
**Consequences:** Unbraced positional is a single digit (`$10` == `${1}0`), matching
bash. Plain `$VAR` now becomes a `BraceExpansionPart` rather than `VarExpansionPart`
— behaviour is identical for plain names (both end at `_env.Get`), verified against
phase5/gap/smoke tests. `$'...'`, `$(...)`, `$((...))`, `${...}` paths are
unchanged. `VarExpansionPart` is still produced inside double quotes.

## 2026-06-13 — Startup files, invocation flags, and prompt (PS1/PS2/PROMPT_COMMAND)
**Decision:** `Program.cs` now parses invocation flags and sources startup files
mirroring bash, mapped to Windows paths (`~` → %USERPROFILE%, `/etc/*` attempted
and silently skipped when absent). Login → /etc/profile + first of
~/.bash_profile|~/.bash_login|~/.profile; interactive non-login → /etc/bash.bashrc
+ (--rcfile | ~/.bashrc); non-interactive script → $BASH_ENV. Prompts use a new
`IO/PromptExpander`: backslash escapes (\u \h \H \w \W \s \v \V \$ \n \r \t \T \@
\A \d \! \# \j \a \e \[ \] \nnn \\) then promptvars ($VAR/${VAR}). Defaults:
BASH_VERSION=5.1.0-koliada, PS1=`\s-\v\$ ` (bash's built-in default), PS2=`> `,
set before sourcing so rc files can override. PROMPT_COMMAND runs before each
primary prompt. Two reusable helpers added to Evaluator: `RunString` (lex+parse+
execute a string, rethrowing ExitException) and `SourceFile` (silentIfMissing).
**Rationale:** `RunString`/`SourceFile` centralise the lex→parse→execute→error
pipeline that Program.cs, `-c`, scripts, startup files, and PROMPT_COMMAND all
need, instead of duplicating try/catch blocks. Defaulting PS1 to the *built-in*
bash default (not a distro `\u@\h` style) keeps fidelity — distros set the fancy
prompt in their rc, which our rc sourcing will pick up.
**Consequences:** `\[`/`\]` are stripped (the line editor measures prompt width
from the real cursor, so non-printing markers are unneeded); ANSI colour codes in
prompts rely on the terminal's VT processing. Command substitution in prompts
($(...)) and \D{strftime} are not yet supported. \$ always renders `$` (no Unix
EUID concept on Windows). Positional parameters for scripts/-c are still pending.

## 2026-06-13 — Multi-line continuation via an `Incomplete` exception flag
**Decision:** `LexException` and `ParseException` carry a `bool Incomplete`. The
parser sets it when an error occurs with the current token at EOF (`Error()`); the
lexer sets it on unterminated single/double quotes. The REPL accumulates input: on
an `Incomplete` failure it reads another line (prompt `> `) and re-parses the whole
buffer; non-`Incomplete` errors are reported immediately. Backslash-newline is
handled separately at the REPL level (odd trailing-backslash count → splice the
next line on with the backslash and newline both removed, per bash).
**Rationale:** Re-parsing the accumulated source is far more robust than a
hand-written "is this balanced?" scanner — it reuses the real grammar, so
`if/for/while/case`, unclosed `(`/quotes, etc. all work without duplicating
parser logic.
**Consequences:** Heredoc-body continuation is not yet covered (the lexer doesn't
flag a missing heredoc delimiter as `Incomplete`). Multi-line commands are stored
as one in-memory history entry but, because `~/.bash_history` is line-based, they
reload as separate lines — acceptable, matches naive bash history behaviour.

## 2026-06-13 — Phase 8 follow-ups: history builtin, HISTSIZE, Ctrl-R, var completion
**Decision:** Added (a) a `history` builtin (`history`, `history -c`, `history N`)
reading `Evaluator.History` (set by the REPL, null in script mode); (b) HISTSIZE
capping in `History` — in-memory and file both trimmed to MaxEntries, file
rewritten only when a trim occurs; (c) reverse-i-search (Ctrl-R) as a self-managed
sub-loop in `LineEditor` returning a line to submit or null to resume; (d) variable
completion in `CompletionEngine` for `$VAR`/`${VAR`, with `CompletionResult.
SpaceOnSingle=false` so accepting a variable doesn't append a space.
**Rationale:** These round out expected interactive-shell UX. `SpaceOnSingle`
generalises the earlier "no trailing space after a directory" rule into the result
type rather than special-casing in the editor.
**Consequences:** `Evaluator` now references `Bash.IO.History` (one-directional;
History has no Evaluator dependency, so no cycle). Ctrl-R uses a single-line search
UI and may leave artifacts when the pre-search buffer spanned wrapped rows — flagged
for live testing. No `HISTFILESIZE`/timestamps/`$(...)` completion yet.

## 2026-06-13 — Phase 8 line editor: key-by-key reader with self-correcting anchor
**Decision:** Replaced `Console.ReadLine` in the REPL with `IO/LineEditor`, a
`Console.ReadKey(intercept:true)` loop. Rendering re-derives the input anchor row
after every write: it reprints the buffer from `(anchorLeft, anchorTop)`, then
sets `anchorTop = Console.CursorTop - (consumedColumns / bufferWidth)` so wrapping
and scrolling stay correct without tracking scroll events. Trailing erase uses a
remembered previous render length.
**Rationale:** `Console.ReadLine` gives no history/editing/completion. Tracking an
absolute start row breaks on scroll; deriving it from the post-write cursor is
robust to the console scrolling under us.
**Alternatives considered:** A third-party readline (adds a dependency, against the
no-dependency goal); ANSI escape sequences (fragile across Windows terminals vs.
the .NET Console cursor API).
**Consequences:** Interactive behaviour cannot be tested in a non-TTY/CI sandbox —
the editor falls back to `Console.ReadLine` when `Console.IsInputRedirected`, so
piped/script input is unaffected and testable. The exact-last-column autowrap
quirk may cause rare cursor glitches on some terminals. `TreatControlCAsInput` is
toggled true only while a line is being read so Ctrl+C clears the line; signal
handling for running commands remains a separate open item.

## 2026-06-13 — History persisted to ~/.bash_history, ignore consecutive dups
**Decision:** `History` loads/saves `~/.bash_history` (append per accepted line),
skipping empty lines and consecutive duplicates. File I/O errors are swallowed.
**Rationale:** Matches bash's default location and dedup-ish behaviour; persistence
across sessions is expected of a shell. Non-fatal on read/write failure keeps the
REPL usable on locked-down profiles.
**Consequences:** No `HISTSIZE`/`HISTFILESIZE` capping yet (file grows unbounded);
no `history` builtin yet; no timestamping. Revisit if size becomes an issue.

## 2026-06-13 — Empty expansions yield zero fields; quoting fixes
**Supersedes the "Consequences" note in the 2026-06-13 array `@`/`*` entry below**
that left empty-array words yielding one empty field.
**Decision:** `ExpandToFields` now returns `[]` (zero fields) for a word that
expands to nothing — `"${empty[@]}"`, an undeclared `"${nodef[@]}"`, or an
unquoted unset `$nodef`. An *explicit* empty string (`""` / `''`) still anchors
exactly one empty field. Implemented via: (a) an empty `DoubleQuotedPart`
(`d.Parts.Count == 0`) appends one quoted empty field; (b) `TryArrayExpansion`
returns true with an empty list for a plain-identifier subscript form whose array
is undefined (non-identifier names like `#arr` are left to `ExpandBraceParam`);
(c) the final `fields.Count == 0 ? [""]` fallback was removed.
**Rationale:** `for x in "${empty[@]}"` must iterate zero times; the old `[""]`
fallback gave a spurious single iteration. All call sites `SelectMany` over the
result, so `[]` flattens away cleanly (command args, array assigns, for-lists).
**Also fixed (regression from the entry below, caught before delivery):** the new
`Process` walk only marked `DoubleQuotedPart` as quoted, so a top-level
`SingleQuotedPart`/`AnsiCQuotedPart`/`HeredocBodyPart` was word-split
(`for x in 'p q'` wrongly iterated twice). These parts are now always treated as
quoted regardless of context.
**Verification:** `tests/gap.sh` (7 cases) + `tests/phase5.sh` + `tests/quote.sh`.

## 2026-06-13 — Array `@`/`*` field expansion unified in ExpandToFields
**Decision:** Replaced the bare-single-part `${arr[@]}` short-circuit (and the
`(text, quoted)` segment-based `SplitFields`) with a single field-builder pass in
`WordExpander.ExpandToFields`. A recursive `Process(part, quoted)` walks parts,
descending into `DoubleQuotedPart` with `quoted=true`; a new `TryArrayExpansion`
recognises `${arr[@]}`, `${arr[*]}`, `${!arr[@]}`, `${!arr[*]}` (indexed + assoc),
and `AppendArray` applies bash splitting rules:
- quoted `@` → one field per element (first joins pending text, last stays open);
- quoted `*` → single field joined by first IFS char;
- unquoted `@`/`*` → space-joined then word-split.
**Rationale:** The old short-circuit only fired when the whole word was a single
unquoted `BraceExpansionPart`, so `"${arr[@]}"` (a `DoubleQuotedPart`) collapsed to
one field — wrong. Bash's rule is that `"${arr[@]}"` yields one field per element
even when quoted, and array expansions embedded mid-word concatenate at the edges.
**Also fixed:** (1) `ExpandBraceParam` checked the `[@]`/`[*]` *values* branch before
the `!`-prefixed *keys* branch, so `${!arr[@]}` (ends in `[@]`) never reached its
handler — guarded the values branch with `!raw.StartsWith('!')`. (2) Associative
single-element subscripts (`${colors[$k]}`) were passed to `GetAssocElement`
unexpanded — added `ExpandSubscript` which expands `$var`/`${...}` while keeping a
bare word literal (assoc subscripts are NOT arithmetic, unlike indexed subscripts).
**Consequences:** `ExpandToString` still routes array `@` forms through
`ExpandBraceParam` (space-joined), which is correct for string contexts. Empty-array
edge cases (e.g. `for x in "${empty[@]}"`) still fall through the final
`fields.Count > 0 ? fields : [""]` guard and yield one empty field rather than zero
— pre-existing, not addressed here.

## DEFERRED — Unix root path mapping
**Decision:** Deferred. Current implementation only maps `/tmp` → `%TEMP%` and `~` → `%USERPROFILE%`.
**User has specific thoughts on approach.** When revisited, options include:
- Configurable root via env var `BASH_ROOT` (e.g. `C:\msys64`) so `/usr/bin/grep` → `C:\msys64\usr\bin\grep`
- PATH-only resolution — don't translate `/usr/...` paths, rely on `PATH` containing correct Windows dirs
- Reject unknown Unix paths with a clear error rather than silently mangling them
**Do not implement until discussed with user.**

## 2026-03-11 — Phase 4: glob, brace, tilde, $'...', heredoc, set options
**Decision:** Implemented as a single phase since all features touch the same expansion pipeline.
**Rationale:** Glob and brace expansion both live in WordExpander.ExpandToFields; tilde and $'...' are natural additions to the same pass. set -e/-x/-u/-o pipefail required ShellOptions shared state.
**Consequences:** WordExpander now owns the full bash expansion order (tilde → param → command → arith → word-split → brace → glob). ShellOptions is a public field on Evaluator so builtins can read/write it.

## 2026-03-11 — Target bash subset not strict POSIX
**Decision:** Target POSIX core + common bashisms (`[[ ]]`, arrays, `local`, arithmetic expansion, brace expansion).
**Rationale:** Matches msys2 day-to-day usage without committing to full bash 5.x fidelity. Tractable scope with a well-defined feature boundary.
**Alternatives considered:** Strict POSIX sh (too restrictive — breaks real-world scripts); full bash (years of work, enormous edge-case surface).
**Consequences:** Must maintain an explicit supported-features list. Scripts using unsupported bash features should fail with a clear error, not silently misbehave.

## 2026-03-11 — Pipeline implementation via thread + MemoryStream
**Decision:** Pipelines capture each stage's stdout into a MemoryStream and feed it as stdin to the next stage, running stages sequentially.
**Rationale:** Simple and correct for typical pipeline sizes. Avoids OS-level pipe wiring complexity at this stage.
**Alternatives considered:** True async OS pipes with threads — deferred to Phase 8 (job control).
**Consequences:** Will deadlock on pipelines that produce data larger than available memory, or stages that require simultaneous read/write. Acceptable for Phase 3.

## 2026-03-11 — Subshell runs in-process with a pushed environment frame
**Decision:** `( )` subshells push/pop an environment scope rather than forking a child process.
**Rationale:** Windows has no fork(). A child process subshell requires full serialization of shell state, deferred to a later phase.
**Consequences:** Subshells cannot truly isolate side effects like cd or variable mutation beyond the scope boundary. Known limitation.


**Decision:** Classic four-stage pipeline. Lexer emits a flat token stream; parser builds an AST; evaluator walks the AST.
**Rationale:** Clean separation of concerns, testable at each boundary, well-understood for shell grammars.
**Alternatives considered:** Direct interpretation without AST (harder to implement control flow and functions correctly).
**Consequences:** Parser and evaluator are separate phases; each can be developed and tested independently.

## 2026-06-14 — Redirects apply in exactly one place per command type
**Decision:** `ExecSimpleCommand` no longer calls `ApplyRedirects` unconditionally. It
now gates: if the command is a builtin (`Builtins.Has(name)`) or a function, it takes the
`ApplyRedirects` `Console`-TextWriter swap; otherwise control passes to `ExecExternal`,
which owns a self-contained fd-map (file / `/dev/null` / `/dev/stdout` / `/dev/stderr` /
`2>&1` / `1>&2` / pipeline handles) for stdin, stdout AND stderr.
**Context/bug:** Previously `ApplyRedirects` ran for *every* simple command, then
`ExecExternal` re-derived and re-opened the same redirect files. A `Console` TextWriter
swap cannot reach a child process's OS handles, so for externals the swap was useless;
worse, the second `File.Create` on the already-open file threw a `FileShare` sharing
violation that propagated unhandled and **crashed the whole shell** on `extcmd >file`.
External `2>file`, `2>/dev/null`, and `</dev/null` were also silently dropped (old fd-map
only wired fd 0/1 and `2>&1`), and `</dev/null` would have tried to open `F:\dev\null`.
**Alternatives considered:** (a) Have `ExecExternal` reuse the `FileStream`s
`ApplyRedirects` opened — rejected: `ApplyRedirects` wraps them in `StreamWriter`/
`TextWriter` for the `Console` swap, no clean way to hand the raw handle to a child, and
it couples two layers. (b) Make `File.Create` use `FileShare.ReadWrite` so both opens
succeed — rejected: two writers to one file is a data race and the `Console` writer still
never receives the child's bytes. One-owner-per-command is the correct model.
**Consequences:** `Builtins.Has(name)` is now the authoritative builtin-membership test
(backed by a `HashSet`); keep it in sync with the dispatch switch (same as `Names`).
Precedence preserved (builtin checked before function, matching prior order). Bash's
order-sensitive dup semantics (`cmd 2>&1 >file` vs `cmd >file 2>&1`) are still collapsed
to flags — `2>&1` follows stdout's *final* destination; a true ordered fd-stack is future
work. Guarded by `tests/cases/redirect_ext.sh` (28/28).

Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>

## 2026-06-14 — Wave-4 coreutils scope & per-tool simplifications
**Decision:** Implemented od, sort, split, find, ls, xargs, diff, plus the `hash`
builtin/PATH cache, each scoped to the common cases rather than full GNU fidelity:
- **ls** — names only, ALWAYS one entry per line (never column layout); `-l` deliberately
  NOT implemented (owner/group/perms/mtime are platform-specific and cannot be reproduced
  byte-faithfully on Windows, so no stable expected-output is possible). Sort is `Ordinal`
  (ASCII, case-sensitive), not GNU's locale/case-insensitive collation. Flags: -a/-A/-r/-d.
- **sort** — `-k` is a SINGLE field, not GNU's "field N to end of line"; numeric key parses
  a leading `double`. Flags: -n/-r/-u/-f/-k/-t.
- **find** — best-effort predicate set (-name/-iname/-type/-maxdepth); UNKNOWN predicates
  are silently ignored rather than erroring (so a script using -mtime etc. still walks).
  Pre-order, ordinal-sorted entries, forward-slash start-prefixed paths.
- **diff** — line-based LCS (DP table + alignment backtrack), GNU "normal" format only
  (NcM/NdM/NaM with `<`/`---`/`>`); no -u/-c context formats; trailing-newline differences
  not reported (File.ReadAllLines strips EOLs). -q/-i supported.
- **xargs** — executes via new `Evaluator.RunCommand(name,args)` (builtin→function→external
  dispatch, no redirects), so it can drive in-process builtins. -n/-0/-I TOKEN; default echo.
- **hash / PATH cache** — `ResolveOnPath` scans PATH(+PATHEXT) and memoizes. Wired as
  `psi.FileName = ResolveOnPath(name) ?? name`: a cache miss falls back to the bare name so
  the OS launcher still does its own PATH search — the cache can only speed things up, never
  break resolution that previously worked.
**Rationale:** These tools' full feature surface is enormous; the scoped subset covers
day-to-day scripting while every behaviour stays byte-faithfully testable (the test suite
authors expected output from real-tool semantics, so a regression FAILs). Platform-specific
or non-deterministic surfaces (`ls -l`, locale collation) are omitted rather than faked.
**Consequences:** Each simplification is documented at its method in Builtins.cs and is a
known deviation, not a bug. Test count 34/34. Future fidelity work (ls -l, sort multi-key,
diff -u, find full expression engine) can layer on without changing the dispatch contract.

Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>

## 2026-06-14 — grep/sed default to BRE (translated), not raw .NET regex
**Decision:** grep and sed translate their default-mode patterns from POSIX Basic Regular
Expressions to .NET syntax via a shared `BreToNet` helper. `-E`/`-r` opts into ERE (passed
to .NET directly); grep `-F` is literal (Regex.Escape). sed replacements use a hand-written
expander (`&`, `\1`..`\9`, `\n \t \ \&`) rather than .NET's `$`-substitution.
**Context:** First cut treated all patterns as .NET regex (≈ERE). That silently broke the
single most common classic idiom — `sed 's/\(.*\) \(.*\)/\2 \1/'` — because in .NET `\(`
is a *literal* paren, so the capture groups never formed and the line passed through
unchanged. The project's user learned Unix on AT&T System V and writes BRE by reflex, so
BRE-by-default is the correct, least-surprising behaviour.
**Alternatives considered:** (a) Document "use -E and ERE syntax" — rejected: pushes the
burden onto the user for the most common case and breaks muscle memory. (b) Use .NET's
`Regex.Replace` `$n` substitution for sed — rejected: sed uses `&` and `\n`, and `$` is a
literal in sed replacements, so a translation layer would be needed anyway; a direct
expander is clearer and avoids `$`-escaping bugs.
**Consequences:** `BreToNet` maps `\( \) \{ \} \+ \? \|` → operators and bare
`( ) { } + ? |` → literals; backreferences and other escapes (`\. \* \ \1`) pass through.
It is NOT a full BRE engine (e.g. POSIX char classes `[[:alpha:]]`, leading-`*`-as-literal,
and collating elements are not handled) — but it covers the idioms that matter. grep was
retrofitted to BRE too for consistency (its test suite was unaffected — those patterns use
no BRE/ERE-divergent metacharacters). Guarded by tests/cases/grep.sh + sed.sh (36/36).

Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>

## 2026-06-15 — Backfill: foundational lexer/parser/evaluator invariants (from project handoff)
**Context:** Consolidating `BASH_PROJECT_HANDOFF.md` into the canonical docs before deleting it.
These four invariants were only implicit in the code (or mentioned in passing); recording the
rationale so a future reader can't accidentally regress them. (The handoff's other insights —
`${…}` consumed whole in the lexer, pipeline EOF/deadlock rules, RedirectScope stream disposal —
are already covered by the 2026-06-13 "$-expansions routed through one path", the pipeline
entries, and the redirect entries respectively.)

- **`HasLeadingSpace` is the word-boundary signal.** Every `Token` carries `HasLeadingSpace`.
  `ParseWord` keeps consuming adjacent tokens into one word only while `!Peek().HasLeadingSpace`,
  so `echo"hi"` is one word but `echo "hi"` is two. Array-compound detection (`arr=(...)`) also
  keys on the `(` having no leading space, which prevents `UNSET=` followed by `;` being misread
  as an array assignment. Removing/ignoring this flag silently breaks word splitting.

- **Bare identifiers inside `$(( ))` resolve as variables.** In bash arithmetic the operand has
  no `$` (`$((x+1))`). `ExpandVarsInArith` expands bare letter-runs as variable lookups (unset →
  `0`) before handing the string to `ArithParser`; without it `x` reached the parser literally and
  failed. (ArithParser itself only sees numbers/operators.)

- **`ParseList` must continue through `;`, not just `&&`/`||`.** A list is pipeline items joined by
  `&&`, `||`, or `;`; parsing continues while the connector is non-null AND the next word isn't a
  list terminator (`done`/`fi`/`esac`/`elif`/`else`/`then`/`do`). An earlier cut stopped at the
  first `;`, so `while …; do a; b; done` only ran `a`.

- **File-append redirects need explicit `FileAccess.Write`.** `File.Open(path, FileMode.Append)`
  defaults to `FileAccess.ReadWrite`, which is invalid with append on Windows and silently writes
  nothing. All append paths use `File.Open(path, FileMode.Append, FileAccess.Write)`.

Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>

## 2026-09-04 — Goal: C#Bash as Claude Code's Windows shell (the compat plan's five decisions)
**Context:** A 228-probe differential battery (Git Bash vs C#Bash) and the extracted Claude Code shell
contract (memory `claude-code-bash-contract`; PROJECT_CONTEXT RESUME anchor) showed C#Bash cannot be
driven by Claude Code's Bash tool today: MSYS `/c/…` paths, `-c -l` flag order and `function name {`
each break the per-command wrapper, and the in-process coreutils diverge silently on Claude's most-used
flags. Claude Code does NOT require Git for Windows — any `bash.exe` via `CLAUDE_CODE_GIT_BASH_PATH` — so
on a Git-less machine C#Bash is the enabling piece. The architect ratified the following five decisions
on 2026-09-04 (items 6 process-substitution and 7 awk remain open; see the anchor).

**1. `OSTYPE=msys`.** Rationale: scripts and Claude Code's own `rg`/`pkill` shims detect Windows by
`msys*`/`cygwin*`; the project already positions itself as an msys2-bash drop-in. Alternative `win32`
(also accepted by the shims) rejected: rarely tested for by real scripts. Revisit if presenting as MSYS
causes a script to invoke MSYS-only machinery we don't provide.

**2. Unsupported option in an in-process coreutil → "auto" policy.** The builtin rejects the option
loudly (exit 2, `X: invalid option -- 'y'`); if an external of the same name exists on PATH the command
is re-dispatched to it; an env override forces `builtin` (loud only) or `external`. Rationale: silent
ignoring was the worst class of failure in the battery (`sed -i`, `find -not`, `grep --include`); a
present GNU tool should never be masked by a partial in-process one. Alternative "builtin-only, loud"
rejected as default: PATH-independent but throws away working tools the user has. Alternative
"silently ignore" (status quo) rejected: corrupts an agent's model of what happened.

**3. `pwd` prints native Windows form (`F:\x`), pending a spike.** Rationale: no fake VFS (existing
stance); Claude adapts to whatever `pwd` returns. Must be VERIFIED against Claude Code's cwd read-back
in P1 before relying on it; if the harness rejects native form, `pwd -P` emits MSYS form instead.

**4. Path input mapping: drive-letter `/c/…` → `C:\…` and `/tmp` → `%TEMP%` now; general `/usr` `/etc`
root mapping stays DEFERRED** per the existing "DEFERRED — Unix root path mapping" entry (which wrongly
states `/tmp` is already mapped — it is not; this entry corrects the record without editing it).
Rationale: Claude Code itself hands the shell MSYS-form paths (snapshot, cwd file) and Claude habitually
writes `/c/…`; the drive-letter form is unambiguous, the root form is not.

**5. Supersedes 2026-03-11 "Subshell runs in-process with a pushed environment frame" (the accepted
cwd/variable leak) and the `ls`-without-`-l` scoping in 2026-06-14 "Wave-4 coreutils scope".** `( )`
and `$( )` will restore cwd, variables, options and traps on exit, and `exit` inside them ends only the
subshell; `ls -l` will be implemented with best-effort Windows fields (mode from attributes, size,
mtime). Rationale: `(cd dir && …)` is the standard idiom for running elsewhere without moving, and its
leak trips Claude Code's cwd reset; `ls -la` is how Claude reads sizes and dates. Byte-fidelity of `ls -l`
was the original objection; for an agent, approximate fields beat none.

**Consequences:** PROJECT_CONTEXT RESUME anchor updated; phases P0–P5 are proposed, not yet greenlit.
Each phase corrects the doc lines it makes true (heredocs, `(( ))`, substrings, `command`, `/tmp` are
currently claimed done but broken — see the anchor's KNOWN BROKEN list).

## 2026-09-04 — Process substitution by temp-file emulation; in-process awk subset (decisions 6 and 7)
**Context:** the two items left open by the entry above; ratified by the architect 2026-09-04.

**6. `<(cmd)` is emulated with a temp file; `>(cmd)` is rejected loudly.** The inner command runs to
completion, its stdout goes to a temp file, the file's path is substituted, and the file is deleted
after the outer command finishes. Scheduled at the end of P4 (low frequency in Claude's usage).
**Rationale:** correct for every finite-output use (`diff <(a) <(b)`, `< <(cmd)` loops) at small cost;
Windows has no `/dev/fd` and the outer command is usually an external that needs a real path.
**Alternatives:** Windows named pipes (`\\.\pipe\…`) — true streaming, several times the work, some
tools stat/seek the path; rejected for now. Out of scope — loud parse error, Claude rewrites with temp
files; rejected because emulation is cheap. **Known limits (documented, loud where detectable):**
streaming producers (`<(tail -f …)`) never complete; `>( )` would lose interleaving, hence rejected.
**Revisit:** if streaming scripts become a target, switch to named pipes.

**7. In-process `awk` covering the subset Claude habitually writes; anything outside it fails loudly.**
This supersedes the "large / optional / do last" classification of awk in 2026-06-14 "Coreutils scope"
only in priority — it joins P4. **Scope (the supported list is the contract; extend it deliberately):**
options `-F sep` (char or regex), `-v var=val`, `-f progfile`; program = `BEGIN`/`END` blocks and
`pattern { action }` rules; patterns = `/re/`, `!/re/`, expressions (`NR==5`, `$1=="x"`, `NF>2`,
`length($0)>80`, `&&`, `||`), and ranges `/a/,/b/`; actions = `print` (comma → `OFS`), `printf` with the
common `%s %d %i %f %x %c %%` and width/precision flags, assignments incl. `+= -= *= /= %= ++ --`,
`if/else`, `while`, `do/while`, `for(;;)`, `for (k in a)`, `next`, `exit`, `break`, `continue`,
associative arrays with `in` and `delete`; builtins `length substr index split sub gsub match tolower
toupper sprintf int`; variables `NR NF FNR FS OFS ORS FILENAME RSTART RLENGTH` and `$0`, `$n`, `$NF`,
`$(expr)` with `$n=` rebuilding `$0`; awk string/number ("strnum") comparison rules; ERE via the
existing .NET translation. **Explicitly unsupported → loud:** `getline`, user `function`s, `RS` other
than newline, `system()`, `close()`, `print | "cmd"` / `print > "file"` redirections, `ENVIRON`,
`printf %e/%g`, uninitialised-array edge semantics beyond the above. **On an unsupported construct
the tool follows decision 2:** fall through to a PATH `awk` if one exists, otherwise
`awk: unsupported: <construct>` exit 2 — never a silent approximation.
**Rationale:** on the Git-less target there is no awk at all, and Claude reaches for
`awk '{print $2}'`-class one-liners constantly; a strict, documented subset gives correct results
there while the loud boundary prevents the partial-language trap. **Alternatives:** external-only
(rejected: nothing to fall through to on the target); full awk (rejected: largest item in the plan).
**Revisit:** grow the list from P5 measurements of what Claude actually sends, never by inference.

## 2026-09-04 — P1 implemented: invocation contract + language core (choices made on the way)
**Context:** P1 of the greenlit plan (rev 39). The contract items were fixed as specified; the
following are the design choices that were NOT pre-decided and that a future reader would
otherwise have to infer from the code.

- **Heredoc bodies are bound in the parser, not the evaluator.** The lexer still emits the body
  as a `HeredocBody` token after the line's newline; `TryParseRedirect` finds and removes it and
  makes it the redirect's `Target` word (quoted delimiter → literal, else double-quote expansion
  rules with `heredoc: true`, i.e. `\"` is not an escape). Alternative — evaluator-side lookup —
  rejected: the body must travel with the command it belongs to through pipelines and `$( )`.
- **Double-quoted strings are lexed raw.** `\` is preserved in the token; `ParseDoubleQuotedInterior`
  applies bash's rules (`\$ \` \" \\ \newline` only). The lexer skips nested `$( )`, `${ }` and
  backticks as balanced units so their inner quotes cannot end the string. Previously every
  backslash was dropped, which silently mangled Windows paths.
- **`(( ))` is recognised in the lexer** (`ArithCommand` token, raw interior, balanced parens, quotes
  skipped) — bash's own approach. A `((` with no matching `))` falls back to a plain `(`.
- **Arithmetic assignment lives in `ArithParser`** through an `IArithVars` callback (identifiers,
  subscripts, `= += ++ --`, comma, lazy `?:`/`&&`/`||` with side effects suppressed on the untaken
  branch). `ExpandVarsInArith` now only pre-expands `$var`/`${…}`/`$(( ))`/`$( )`.
- **errexit model:** a counter `_errexitSuppress` covers commands followed by `&&`/`||`, `if`/`while`
  conditions, `!`-negated pipelines and the stages inside a pipeline; `ExecList` re-checks the
  final status. This is bash's rule set, minus `ERR` traps (P4).
- **Subshell isolation = snapshot/restore of the environment** (frames, arrays, attributes,
  positionals, cwd) around `( )` and `$( )`; `exit` and a fatal error end only the subshell.
  Options, traps, functions and aliases are NOT isolated (documented; revisit if a script trips it).
- **External stdio follows swapped Console streams.** When `Console.Out/Error/In` differ from the
  process originals — `$( )` capture, `{ … } >file`, a heredoc on a loop — the child's stdout/stderr
  are redirected and copied into the swapped writer (bytes into a file's raw stream, text into a
  capture). Before this, `x=$(git …)` never captured anything.
- **fd 3+ are an in-process table** (`Evaluator._fds`) opened by `exec 3>f` / `cmd 3<f`, used by
  `>&3`, `<&3`, `read -u 3`, closed by `3>&-`; children get `>&3` via a copy thread. Children never
  inherit numbered descriptors (no fork) — documented limitation.
- **`$!` is a synthetic pid** (`0x40000000 + job id`) so `wait $!`/`kill $!` can tell a job from a
  real pid; `kill` on a job kills the external child it is running. Job notices `[n] pid` print only
  in interactive shells, as in bash.
- **Syntax-error statuses:** 2 for `-c`/script/`source` (bash), 1 for `eval` (bash). A syntax error
  inside a sourced file fails only `source`.
- **`command not found`** is reported as bash spells it, with exit 127 (126 for permission/format
  errors), and is written to wherever the command's stderr was redirected — so `cmd 2>/dev/null`
  is silent, and `$(cmd 2>&1)` captures the message.
- **PATH normalisation:** assigning a colon-separated, `/`-rooted PATH (a Git Bash snapshot's
  `export PATH='/c/…:/usr/bin'`, or `PATH=/c/x:$PATH`) converts it to `;`-separated native paths,
  dropping entries that contain a newline (a profile `echo` leaking into a captured PATH).
- **`shopt -s extglob` is refused loudly** (decision 2's principle applied to a shell option whose
  silent acceptance would change matching semantics); `shopt -u extglob` — what Claude Code's
  wrapper sends — is a no-op.
- **Declaration builtins take array literals as arguments** (`declare -a x=(a b)`): the parser folds
  `x=(…)` into one literal word when the command is `declare`/`typeset`/`local`/`export`/`readonly`;
  the builtin re-parses and expands the elements (`Parser.ParseWords`).
- **`pwd` prints native form** (decision 3) — the Claude Code read-back spike is still outstanding.

**Not done in P1, deliberately:** the ext→builtin pipe rewrite (P2), coreutil option strictness and
fall-through (P3), the coreutil flag vocabulary and `<( )` (P4).

## 2026-09-04 — Decision 3 verified by spike: native `pwd` output is accepted by Claude Code
**Context:** decision 3 (2026-09-04 entry above) chose the native Windows form for `pwd` pending
verification against Claude Code's cwd read-back.
**Spike:** a nested non-interactive session (`claude -p --model haiku --allowedTools 'Bash(*)'`
with `CLAUDE_CODE_GIT_BASH_PATH` pointing at `Bash.exe`, the parent's `CLAUDECODE*` variables
unset) ran three Bash-tool calls: `cd sub && pwd`, `pwd`, `echo $OSTYPE $BASH_VERSION`. The
second call reported `…\spike\sub` — the harness read the native-form cwd file and kept the
directory — and the third printed `msys 5.1.0-koliada`. **Decision 3 stands as ratified; no
MSYS-form `pwd` fallback is needed.**
**Also found:** (a) C#Bash crashed at startup when spawned with pipes and no console
(`Console.OutputEncoding` throws "The handle is invalid") — now guarded; (b) Claude Code's
snapshot generator (full text recovered from the debug log: `shopt -p`, `declare -F | cut | grep
| while read`, `set -o | grep | awk | head >>file`, `alias | grep | sed | sed | head`, heredoc'd
`rg`/`pkill` shims, `export PATH=…`) times out in C#Bash at the `set -o | grep | awk | head`
pipeline: builtins on either side of an external run on the threaded path, whose per-thread
`Console.SetOut/SetIn` swaps race on the process-global Console. Compound pipeline stages
(`… | while read`) were moved to the sequential path as a stopgap; the real fix is P2.
**Consequence for P2's design:** builtins must get per-thread console streams (a multiplexing
`Console.Out/In/Error` that dispatches on a thread-static slot, so the existing Console-based
builtins need no changes), pipelines must run every stage on its own thread over managed pipes
with reader-close signalling (SIGPIPE emulation, which also unblocks the deferred `yes`), and
externals must be killed when their consumer closes.

## 2026-09-04 — P2: the pipeline model — per-thread console, managed pipes, SIGPIPE emulation
**Status:** Active. **Supersedes** 2026-03-11 "Pipeline implementation via thread + MemoryStream"
and 2026-06-14 "Fixed: builtin-pipeline race (sequential)" (the sequential path is gone), and
resolves 2026-06-14 Wave 2's "`yes` DEFERRED".

**Decision:** (1) `ConsoleMux` replaces the process Console writers/reader once with
multiplexers dispatching on thread-static slots; every redirect/capture/pipeline sets the slot
for its thread; child threads inherit their parent's slots. Installed *unsynchronized* via
reflection on `Console.s_out/s_error/s_in` (fallback: the synchronized wrapper), each target
writer locked individually. (2) `PipeBuffer`: an in-memory bounded pipe (1 MB back-pressure)
with EOF on writer close and `BrokenPipeException` on write after reader close. (3) Every
pipeline stage — builtin, compound or external — runs on its own thread over `PipeBuffer`s; a
finished stage closes its read end; a builtin producer then unwinds with status 141 and an
external producer is killed by its drain thread. (4) `ChildJobs`: all children go into a
kill-on-close job object so a host that kills the shell on a timeout takes the children too.
(5) `head` streams (stops reading after n lines) so `yes | head` terminates; other slurping
consumers (`grep -m`, `sed q`) are P4 work.

**Rationale:** the process-global Console was the root of both the "sequential builtin
pipeline" workaround and the Console race that broke Claude Code's snapshot generator
(builtins on either side of an external). Bash's model is concurrency with SIGPIPE; the only
way to get it without `fork()` is threads with thread-private stdio and an explicit
broken-pipe signal. A global Console lock was ruled out by construction: a stage blocked on a
full pipe would hold it and stall the consumer that must drain the pipe — a deadlock.

**Alternatives considered:** keep the sequential path for all-builtin pipelines (rejected:
cannot express an infinite producer, and the mixed case still raced); OS anonymous pipes
(rejected: handle-ownership disposal races produced "Cannot access a closed pipe", and no
reader-close signal); rewriting every builtin to take explicit streams (rejected: same result,
far larger diff, and third-party-style contributions would keep reaching for Console).

**Known limits (documented):** stages share variables and cwd (bash: subshells); a builtin
that catches all exceptions internally can swallow a broken pipe and keep looping until its
own input ends; the reflection install is .NET-8-specific (field names), with a functional
fallback.

**Measured (2026-09-04):** compat battery 151 → 158 with **0 hangs** (was 2); suite 42/42
with the new `pipes2` case; Claude Code's snapshot generator runs in ~0.2 s through C#Bash and
its output re-sources cleanly (`rg` shim defined); killing the shell kills its `ping` child.

**Implemented by:** rev 41 — `Evaluator/ConsoleMux.cs`, `Evaluator.cs` (`ExecPipelineThreaded`,
`ExecExternal`, `RedirectScope`), `WordExpander.RunSubstitution`, `Builtins.cs` (`head`, `yes`),
`Program.cs` (`ConsoleMux.Install`). **Affects ARCHITECTURE.md §3(c), §9.0–9.3.**

## 2026-09-04 -- P3+P4 implemented: strict coreutil options with fall-through, the tool families, awk, <( ), trap ERR (choices made on the way)
**Context:** the greenlit P3 ("loud, not silent") and P4 ("Claude's coreutil vocabulary") phases, built
together because every P4 tool had to be rewritten on the P3 option parser anyway. hg rev 42.

**Decisions taken while implementing (each a small fork resolved by the ratified decisions or by
measurement; none re-opens a ratified one):**
- **One strict option parser for every tool (`Evaluator/Opts.cs`).** GNU conventions (clusters,
  attached/separate values, `c::` optional values for `sed -i[SUF]`, long `name=`/`name=?`/aliases,
  `--`, `stopAtFirstOperand` for xargs/env/timeout/awk, numeric `-N`). Anything not in a tool's
  spec throws `UnsupportedOptionException`; the dispatcher (`Builtins.TryExecute`) implements
  decision 2 in exactly one place. Rejected: per-tool ad-hoc parsing (that is what silently
  ignored options before) and a lenient parser with warnings (a warning is still a silent wrong
  answer to a script).
- **Policy environment variable is read per call** (`BASH_COREUTILS`), so a script can flip it
  mid-run and tests can pin `builtin` to keep the loud boundary deterministic regardless of PATH.
- **Git for Windows' `usr/bin` + `mingw64/bin` are prepended to PATH when `git.exe` is on PATH
  (`ShellEnvironment.AugmentPathWithGitTools`).** This is what the MSYS runtime does for Git Bash,
  so scripts that work there find the same externals here (tar, perl, ssh, xxd, nohup...).
  Measured: launched from PowerShell the battery lost awk/tar/nohup/xxd (127) until this landed.
  Rejected: appending instead of prepending (System32's `tar.exe` is bsdtar and `find.exe` is not
  find; in-process tools shadow PATH anyway, so prepending only affects names we do not implement).
- **The shell's own directory is appended to PATH** so `bash -c ...` from a script resolves to this
  interpreter on a machine without Git (verified with `PATH=C:\WINDOWS\System32;C:\WINDOWS`).
  Appended, not prepended: a real Git bash wins when present, matching PATH semantics.
- **`$PATH` assignments are mirrored into the process environment** (`ShellEnvironment.Set`). Found
  while measuring: `ResolveOnPath`/`FindInPath` read `Environment.GetEnvironmentVariable`, so
  `export PATH=...` had never affected the shell's own command lookup (children got it, we did
  not). Windows' `Path` is imported as `PATH` (MSYS does the same).
- **`|&` belongs to the producer.** The parser attached the flag to the consumer, so the evaluator
  (which correctly applies it to the producing stage) never saw it. Fixed in the parser; probe 167.
- **`trap ERR` semantics copied from bash and verified line by line against Git Bash** (16-case
  differential in `tests/cases/procsub.sh`): fires after a failing simple command, pipeline,
  `[[ ]]` or `(( ))`; not inside `&&`/`||`/`!`/`if`/`while` conditions; not inside functions
  unless `set -E`/`-o errtrace` (now real options, listed in `set -o` and `$-`); never re-entrantly.
- **`<( )` lifetime = the node whose expansion created it.** `Evaluator.Execute` marks the
  per-thread temp-file list on entry and deletes anything registered during that node on exit, so
  files survive exactly as long as the consuming command (including a compound command's
  redirect target, `while ... done < <(cmd)`), and nested substitutions cannot delete an outer one.
  `>( )` is a parse-time error whose message states the workaround.
- **awk deviations from POSIX are gawk-verified, not inferred:** `substr` truncates positions and
  clamps a start below 1 without shortening the length; a single-character `FS` is always literal
  (`-F.` and `-F'|'` split on the character); integral values print as integers at any magnitude;
  `for (k in a)` iterates in insertion order (gawk's order is unspecified; scripts sort anyway).
  A parse failure is treated like an unsupported construct (fall through if a PATH awk exists),
  because a gap in this parser must not masquerade as a user error. Parse-time `print > ...`,
  `| getline`, `function`, `ENVIRON`, `printf %e/%g` are loud per the contract.
  **Open (anchor, AWAITING):** admitting `> "/dev/stderr"` / `> "/dev/stdout"` as the one
  redirection form -- Claude writes it often; extension is the architect's call per decision 7.
- **`date -d` is a real parser (`GnuDate.cs`), shared with `touch -d`,** and `-u` parses as UTC as
  well as printing in UTC (GNU behaviour; the first cut converted local->UTC and was 8 h off).
  `uname` prints MSYS-style (`MINGW64_NT-10.0-19045 ... x86_64 Msys`) because scripts key off it.
- **`timeout` runs the command on a worker thread with a job record** (`Evaluator.RunAsJob`), so an
  external child is killed on expiry (124); an in-process command cannot be pre-empted safely, so
  it is interrupted and reported as 124 (143 with `--preserve-status`).
- **Compat harness: the reference bash is never `System32\bash.exe`.** WSL's launcher answered to
  `bash` when the battery was run from PowerShell and quietly became the reference (OSTYPE
  linux-gnu, 29 "regressions"); the finder now derives Git's root from `git.exe` and skips
  SystemRoot. Probe 172 was removed from the baseline as launcher-dependent.
- **Test expectations for the new cases were generated from Git Bash/gawk, diffed, and only the
  by-design lines kept from C#Bash** (documented in PROJECT_CONTEXT Test Assets) -- consistent with
  "authored, not captured": the reference run is the author, the diff is the review.

**Measured:** battery 170 -> 217 / 228 (0 hangs; the 11 left are environment or by-design), suite
42 -> 45 green, 132 awk one-liners byte-identical to gawk.

**Implemented by:** rev 42 -- `Evaluator/Opts.cs`, `Builtins.{Text,Files,Find,Search,Sys,Awk}.cs`,
`GnuDate.cs`, `Builtins.cs` (dispatcher), `Evaluator.cs` (`Execute` mark/release, ERR trap,
`RunAsJob`), `WordExpander.cs` (`RunProcessSubstitution`), `Parser.cs`/`Lexer.cs`/`Nodes.cs`
(`<(`, `>(`, `|&`), `ShellEnvironment.cs` (PATH), `ShellOptions.cs` (`-E`), `tests/`.
**Affects PROJECT_CONTEXT.md** anchor + "In-process coreutils" + Test Assets; **ARCHITECTURE.md**
section 10; **IMPLEMENTATION.md** file map + section 7; **README.md** command list.

## 2026-09-04 -- P5 verified: Claude Code driven by C#Bash on a Git-less PATH
**Context:** the last greenlit phase -- prove the integration end to end where it matters, on a
machine layout with no Git for Windows (Claude Code only needs *a* `bash.exe` through
`CLAUDE_CODE_GIT_BASH_PATH`; the whole point of P0-P4 was to be that bash).

**Method:** a nested `claude -p --model haiku --allowedTools 'Bash(*)' --debug` session launched
with `PATH=C:\WINDOWS\System32;C:\WINDOWS;...\WindowsPowerShell\v1.0;C:\Users\guy\.local\bin`
and `CLAUDE_CODE_GIT_BASH_PATH=F:\Koliada\Tools\bash\Bash\bin\Release\net8.0\Bash.exe`, asked to
run 15 fixed commands as separate tool calls and quote each result; the debug log confirms the
snapshot was created (3181 bytes) and cleaned up.

**Result:** 14 of 15 as intended -- `OSTYPE`, `uname`, native `pwd`, `cd` persisting across calls,
builtin `awk`, the `rg` shim running the embedded ripgrep, `ls -la`, `date -d`, `grep -rn`,
`timeout` (124), `command -v bash` resolving to this exe, `diff <( ) <( )`, heredoc into `/tmp`,
`find | sort | head`, `sed -n`. **The failure:** `pkill -f pattern` -> 127. Claude Code's shim
(extracted from `claude.exe`) is a PID guard (`command pgrep ... | grep -qx "$CLAUDE_PID"`)
followed by `command pkill ${1+"$@"}`; both need procps binaries, which exist on Windows only
inside Git's `usr/bin`. Loud, not silent -- but a real gap for Claude's habitual
`pkill -f <server>` cleanup on this target.

**Decision:** phase closed as verified; the `pgrep`/`pkill`/`ps` gap is recorded as an AWAITING
item in the anchor with a recommendation (minimal in-process versions), NOT built -- it extends the
coreutil scope beyond the P4 list and is the architect's call. Alternatives noted for that call:
ship procps-like tools in-process (recommended; Claude sends them constantly); document
"install Git for procps" (rejected as the default answer: it re-introduces the dependency the
project exists to remove); make the shim's `command pkill` fail softer (rejected: it is Claude
Code's code, not ours, and silent is wrong).

**Implemented by:** rev 43 (docs only). **Affects PROJECT_CONTEXT.md** anchor (P5 line, KNOWN GAPS,
AWAITING).

## 2026-09-04 -- Ratified: awk standard-stream redirection; in-process pgrep / pkill / ps
**Status:** Active. Extends 2026-09-04 decision 7 (awk contract) and the coreutil scope; both items
were the AWAITING entries left by P5 and were ratified by the architect verbatim: "1/ yes, 2/ build
both in-process".

**1. awk admits exactly `print`/`printf ... > "/dev/stderr"` and `> "/dev/stdout"`** (`>>` too).
Any other target, and `| cmd`, stay loud / fall-through. Rationale: the one redirection Claude
writes habitually, zero file handling, and it keeps the "extend the list deliberately" rule
intact because the list now names it.

**2. `pgrep`, `pkill`, `ps` are in-process** (`Evaluator/Builtins.Proc.cs`). Windows ships no
procps; on a Git-less PATH these were 127 and Claude Code's own `pkill` shim ends in
`command pkill`. Choices:
- **Process list via Toolhelp32, command lines via `NtQueryInformationProcess`
  (ProcessCommandLineInformation, 60).** Rejected: WMI `Win32_Process` (needs the
  `System.Management` package and costs hundreds of ms per call); reading the PEB by hand
  (more code for the same result).
- **Names match case-insensitively** (`pgrep node` finds `Node.exe`) and both the `.exe`-less
  base name and the full name are tried; `-x` is exact on either. procps is case-sensitive, but
  Windows file names are not and a miss here is a silent wrong answer.
- **The shell never matches itself** (procps behaviour; here "itself" is the whole interpreter,
  which matters because the tool is in-process and the shell's own `-c` text contains the
  pattern). **`pkill` additionally refuses to kill the shell's ancestors and says so** -- with
  `-f`, an ancestor `bash -c '... pkill -f X ...'` matches X by construction; procps would kill
  it (a known Linux foot-gun) and take the session down. Measured during the build: the first
  version killed the test harness's Git Bash. `pgrep` still lists ancestors, so Claude Code's
  PID guard (`pgrep ... | grep -qx "$CLAUDE_PID"`) keeps working.
- **Every signal is forceful** (`Process.Kill`), as for the `kill` builtin -- Windows has no
  per-signal delivery; `-9`, `-KILL`, `-SIGTERM`, `--signal` are accepted and ignored.
- **`ps` is deliberately small:** `-e/-A/-ef/aux/ax`, `-p`, `-o pid,ppid,comm,args` (and the
  `cmd`/`command` aliases), `--no-headers`; `-u`, `--sort`, `--forest`, other columns are loud.
  Claude's `ps aux | grep <name>` and `ps -p $$ -o comm=` work; nothing pretends to know
  CPU/memory/tty.
- Note for test authors: `cmd.exe /c "ping -n 30 ..."` gives `ping.exe` the command line
  `ping  -n 30 ...` (two spaces) -- match the `cmd.exe` parent or the name, not the exact text.

**Implemented by:** rev 44 -- `Builtins.Proc.cs`, `Builtins.Awk.cs` (`APrint.Dest`), `Builtins.cs`
(dispatch), `tests/cases/procs.sh`, `tests/cases/awk.sh` (+4 lines). **Affects PROJECT_CONTEXT.md**
anchor + coreutils section; **ARCHITECTURE.md** section 10; **IMPLEMENTATION.md** file map + 7;
**README.md**.

## 2026-09-04 -- pkill's self-protection cannot rely on the ppid chain alone
**Status:** Active. Hardens the `pkill` guard of the entry above (same date, "in-process pgrep /
pkill / ps"); nothing there is reversed.

**Context:** the readiness check before placing C#Bash as Claude Code's shell ran
`pkill -f no-such-process-xyz` from a nested `claude -p` session, and the pattern was in the
command line of every process relaying the prompt: the harness Git Bash, `env`, `timeout`,
`claude.exe`. The ancestor guard protected only what the ppid chain reached, and the chain
measured from inside Claude Code was `Bash -> claude -> timeout -> timeout -> <gone>`: MSYS
`env` had exec'd `timeout`, the original pid was dead, and everything above it (the harness
shells) was killed. Windows keeps no parent chain across an MSYS exec.

**Decision:** keep the ppid rule and add a ppid-free one for `-f`: a process whose command line
contains both the raw pattern and the word `pkill`/`pgrep` is a shell relaying this invocation,
never a target; it is skipped aloud ("its command line carries this pkill invocation"). A real
target (a server, a watcher) does not have `pkill` in its command line. Alternatives:
walk the chain by process start times to bridge dead pids (rejected: Windows reuses pids, and
start-time reads fail for elevated processes); refuse `-f` entirely (rejected: it is the form
Claude uses); match procps exactly and let it kill the session (rejected: loud is the rule,
but a dead session is not a message anyone reads).

**Implemented by:** rev 46 -- `Builtins.Proc.cs` (`CarriesThisInvocation`). **Affects
ARCHITECTURE.md** section 10; **PROJECT_CONTEXT.md** coreutils section; **IMPLEMENTATION.md**.

## 2026-09-04 -- Deployable = one self-contained single-file exe; benchmark assets kept; perf regression bisected
**Status:** Active.

**Deployable.** The file Claude Code is pointed at is `dotnet publish -c Release -r win-x64
-p:PublishSingleFile=true -p:DebugType=none -o dist` -> `dist/Bash.exe`, self-contained (the .NET 8
runtime inside, ~68 MB), measured startup 0.087 s against 0.083 s for the four-file build and
0.083 s for the framework-dependent single file. Rationale: one file to place, nothing to install,
no measurable startup cost; the project's premise is "no WSL, no msys2, no Cygwin" and a runtime
prerequisite would be the same kind of dependency. Rejected: framework-dependent single file
(0.6 MB) as the default -- fine where .NET 8 is installed, but it makes "copy one file" untrue on a
fresh machine; trimming -- `ConsoleMux` reaches into `Console`'s private fields by reflection and
the trimmer cannot see that. `dist/` is hg-ignored (`glob:dist/*`); the architect cuts snapshots.

**Benchmark assets.** The three ad hoc comparison scripts from the re-measurement are now
`tests/bench/{coreutils,pipeline,find}.sh`, plus `tests/bench/compare.sh` (reference bash vs
C#Bash, best of N, ratio, startup, output agreement). Wall-clock only; per-iteration A/Bs stay
with `tools/bench.csx --no-cache`.

**Regression, bisected, not yet fixed.** The architect corrected the earlier hedge: June's 356
ns/it loop figure was this machine. Building revisions in a scratch clone and running the same
harness reproduces it (rev 38: 358 ns) and attributes the rise: P1 (rev 39) 454, P2 (rev 41) 472,
P3+P4 ~490, with bytes/it slightly DOWN (1116 -> 1092). Three guesses were A/B'd and excluded
(per-node `<( )` mark/release, ERR-trap check, `ShellEnvironment.Get`'s special-name switch and
`int.TryParse`); the edits were reverted because they measured nothing -- an unmeasured
"optimisation" is not one (2026-06-14 lesson). The ~100 ns from P1 is inside the language-core
rewrite (`ExecSimpleCommand`, the `test`/`[` parser, the word-expander rewrite) and is queued as
an A/B investigation, not started: it is performance work outside the ratified P0-P5 scope and
the architect's call whether 0.36 vs 0.49 us per loop iteration (still 2.7x faster than Git
Bash wall clock) is worth the time.

**Implemented by:** rev 49 -- `tests/bench/*`, README "Using it as Claude Code's shell",
PROJECT_CONTEXT performance + Test Assets. **Affects PROJECT_CONTEXT.md** Performance, Test Assets;
**README.md**.

## 2026-09-04 -- The P1 loop regression, attributed and mostly recovered (bounded A/B pass)
**Status:** Active. Closes the "queued, not started" item of the entry above; ratified by the
architect ("see if you can figure the 100ns").

**Method:** a per-node harness (`tools/attrib.csx`, kept) times each piece of a loop iteration --
`[ $i -lt N ]`, `[ 5 -lt N ]`, `:`, `i=$((i + 1))`, `j=5`, `j=$i` -- against June's build (rev 38
in a scratch clone) and the current one. That split is what made the diff readable; the
whole-script number alone could not.

**Found (June -> before this pass, ns per call / bytes):** `[ 5 -lt N ]` 127 -> 191 / 688 -> 800;
`:` 54 -> 74 / 296 -> 368; `i=$((i+1))` 137 -> 178 / 432 -> 296; `j=$i` 63 -> 75 / 168 -> 224.
- **`test`/`[`:** P1 replaced a static evaluator with a recursive-descent `TestParser` (needed for
  `-a`/`-o`/`!`/parentheses) and `TestBracket` copied the argument list (`args[..^1]`) to drop
  the `]`. **Fix:** answer the POSIX 1/2/3-argument forms directly (bash special-cases them too;
  a 3-argument form with a binary primary in the middle is binary by the standard), pass a count
  instead of copying, and parse the integer operands once (`TestInt`). Measured: 191 -> 146 ns,
  800 -> 544 B.
- **Every simple command allocated a `RedirectScope`** (object + two empty `List`s, ~72 B, three
  allocations) even with no redirects -- the P2 scope grew when it started saving the
  per-thread `ConsoleMux` slots. **Fix:** callers create the scope only when the redirect list
  is non-empty (`using var scope = list.Count > 0 ? ApplyRedirects(list) : null`), in
  `ExecSimpleCommand` and the compound commands. Measured: `:` 74 -> 64 ns, 368 -> 232 B.
- **`ShellEnvironment.Get` fast path for lower-case names** (skip the special-parameter switch and
  `int.TryParse`): invisible in the whole-loop A/B earlier in the day, invisible per node in
  `attrib.csx`, but a 3-vs-3 alternating run of `bench.csx` at the end read 425/435/430 with it
  against 441/455/431 without -- a consistent ~10 ns (2-3 %), at the edge of the noise band.
  Kept in its minimal one-line form, labelled marginal in the code. The rule stands: it stayed
  out until a measurement showed it, and the measurement is recorded next to it.

**Result (bench.csx, best of 6):** loop 489 -> ~430 ns/it, 1092 -> 836 B/it (June: 358 / 1116);
arith 757 -> ~720; func 1303 -> ~1180. Suite 46/46, battery 217/228 unchanged.

**What remains, attributed and deliberately left:** ~27 ns in `i=$((i+1))` is the P1 redesign of
arithmetic -- identifiers are resolved through `IArithVars` (allocating the name substring and an
`ArithParserState` per evaluation) instead of textual substitution; that redesign is what makes
`x=y+1`-style values and assignments inside `(( ))` correct, and the lever for it is the parsed-
expression cache already listed under "Next levers" since June. ~10 ns is the per-node
bookkeeping added since June (`CurrentLine`, errexit/ERR checks, `<( )` mark/release) and ~12 ns
plus 56 B is in expanding a bare `$i` under the rewritten `ExpandBraceParam` -- neither chased.

**Implemented by:** rev 50 -- `Builtins.Shell.cs` (`TestExpr(args, n)`, `TestInt`), `Evaluator.cs`
(scope guards), `tools/attrib.csx`. **Affects PROJECT_CONTEXT.md** Performance.

## 2026-09-05 -- Defect report from session web-d6: two parser defects fixed, two already fixed, builds now identify themselves
**Status:** Active.

**Context:** another Claude session (`web-d6`, GUY-WINDOWS11) adopted C#Bash as its shell for a
corpus on an SMB share (a thing WSL bash cannot reach) and left `E:\CLAUDE\bash-defects-2026-09-05.md`:
four parser defects, each isolated on a three-line fixture. Its binary showed ProductVersion
`1.0.0+a65b44f...` -- which every build of this tree showed, because the SDK stamps the git
snapshot's HEAD, so "is my copy behind?" could not be answered from the file.

**Triage against the current tree (all four reproduced or not on `dist/Bash.exe`, then against Git
Bash):**
1. **`n=$(( $(wc -l < f) + 1 ))` ran `wc-l<f` and set n=1 with a clean exit -- REAL, present.**
   `ParseArithmeticExpansion` rebuilt the interior by concatenating token *values*, dropping
   every space; the nested `$( )` then lexed `wc-l`. Fix: keep the interior as source text
   (sliced from `_src` by token offsets, the way array literals already were), with a
   space-preserving token fallback when no source is available. The reporter is right that this
   is the serious one: a plausible wrong number with status 0.
2. **`"$(echo hi)"` unterminated -- not reproducible; fixed by P1 (rev 39).**
3. **`echo -e` / `printf` not expanding `\n` -- not reproducible; fixed by P1.**
4. **Backslash-newline inside a pipeline gave every tool an empty argument -- REAL, present.**
   `ConsumeWord` removed the `\<newline>` pair but still emitted the (empty) word it had
   started; `cat f \` + newline + `| sed` therefore ran `cat f ""`. Fix: a word that consisted
   only of continuations is no word (the lexer returns nothing); CRLF continuations handled too.

**Build identification (new):** `Bash.csproj` runs `hg id -n -i` before compiling and puts
`1.0.0+hg.<hash>[+] <rev>[+]` into the informational version (file properties "Product
version"); `bash --version` prints it as a third line, `C#Bash build ...`. The `+` marks an
uncommitted tree. A reporter can now say which build they have. Rejected: the git hash (only the
snapshot moves it), a build timestamp (says when, not what).

**Guards:** `tests/cases/parser2.sh` (all four items plus neighbours, expected generated under Git
Bash and identical), compat probes 229 and 230 (baseline 217 -> 219).

**Implemented by:** rev 51 -- `Parser.cs` (`ParseArithmeticExpansion`), `Lexer.cs` (`ConsumeWord`),
`Bash.csproj`, `Program.cs` (`--version`), tests. **Affects PROJECT_CONTEXT.md** anchor, Test
Assets; **README.md** (version line).

## 2026-09-05 -- `--version` no longer claims GNU bash or FSF copyright
**Status:** Active. Supersedes the banner text introduced with P1 (rev 39), which is the only
thing it changes.

**Context:** the architect asked why `bash --version` printed "Copyright (C) 2020 Free Software
Foundation, Inc." The honest answer: no reason. The P1 rewrite of `Program.cs` copied the shape
of GNU bash's banner for scripts that grep a version number out of it, and copied the copyright
line along with it. That line was false -- the FSF holds no copyright in this code, which is an
independent MIT implementation -- and "GNU bash" on the first line was a misattribution too.
Verified before changing: `claude.exe` contains no "GNU bash" string, so Claude Code does not
parse the banner; the only consumers were two suite expectations.

**Decision:** line 1 `bash, version 5.1.0-koliada(1)-release (x86_64-pc-msys)` (bash's shape,
no "GNU"); line 2 `C#Bash: a bash-compatible interpreter for Windows, MIT licensed, not GNU
bash.` plus the repository URL; line 3 the build stamp from the previous entry. `$BASH_VERSION`
and `BASH_VERSINFO` are unchanged (scripts test those, and 5.1 is the feature level implemented).
Rejected: keeping "GNU bash" for greps -- nothing measured needs it and it is untrue; dropping the
version-number shape -- `bash --version | head -1` parsing is common enough to keep.

**Implemented by:** rev 52 -- `Program.cs`; `tests/expected/{flags,pipes2}.out`.

## 2026-09-05 -- `--version` line 2 wording (architect's edit)
**Status:** Active; adjusts the wording of the entry above, nothing else. The architect changed
line 2 to `C#Bash: a bash-compatible interpreter for Windows, MIT licensed. <repo URL>` (the
"not GNU bash" clause dropped -- the absence of "GNU" on line 1 already says it). Released as
rev 53: `dist/Bash.exe` and the share copy `E:\CLAUDE\bash-dist\Bash.exe` both carry it.

## 2026-09-05 -- Second report from web-d6: one real defect, one strictness gap, two harness artefacts
**Status:** Active.

**Context:** web-d6 re-tested rev 54 and sent three items over Remote Control (the channel
works). Reproduced each from a script file and via `-c` against Git Bash before touching code.

1. **`t=$(( t + $(echo 3) ))` -> syntax error near ')' -- REAL, fixed.** The rev-51
   `ParseArithmeticExpansion` slice ended at the first `))` without tracking nesting, so a
   substitution that was not the first token closed the expression one `)` early; leading
   substitutions worked by accident. Now `(`/`$(`/`<(`/`>(` and `$((` are counted and `))` closes
   only at depth 0. Worse than reported: the parse error aborted the whole script, not one line.
2. **`echo "x (y) z"` -> `x` plus the parenthesised text executed; `printf "%s\n" x` -> `xn` --
   NOT reproducible: byte-identical to Git Bash from a script, via `-c`, and through cmd.exe
   quoting.** Both symptoms are exactly what the shell sees when the double quotes are stripped
   before it runs (`echo x (y) z`, `printf %s\n x`), i.e. the reporting harness's quoting.
   Reported back with the evidence and a quoting-safe way to re-run.
3. **The symptom exposed a real strictness gap: `echo x (y) z` ran as three commands with a
   clean-looking exit, where bash reports "syntax error near unexpected token `('".** Fixed in
   `ParseList`: after a pipeline, the next token must be a list operator, a newline, or something
   that closes the enclosing construct; a bare `(` or a stray word is a syntax error. Loud beats
   plausible output. Exit status for a syntax error in `-c` is 2 (bash 5 semantics; the Git Bash
   reference here is 4.4 and returns 1 -- the one line of `parser2.out` kept from C#Bash rather
   than the reference; [inferred from bash 5 sources, not measured against a 5.x binary]).

**Guards:** `tests/cases/parser2.sh` extended (accumulator loops, nested-in-nested, parenthesised
arithmetic with a substitution, the syntax-error case, double-quoted `printf` formats);
compat probes 231-232 (baseline 219 -> 221). Suite 47/47; battery 222/232.

**Implemented by:** rev 55 -- `Parser.cs` (`ParseArithmeticExpansion` depth, `ParseList`
terminator check), tests. Released as `dist/Bash.exe` and `E:\CLAUDE\bash-dist\Bash.exe`.

## 2026-09-05 -- head/tail -z were accepted and ignored; now real
**Status:** Active. Found while answering "how about adding tail as an internal command?" --
`tail` has been in-process since June, so the question was answered by verifying it against
Git Bash form by form; every form matched except `-z`, which both `head` and `tail` accepted as
an option and then treated the input as newline-separated anyway. That is the silent wrong
answer decision 2 exists to prevent (the option parser was strict; the implementation was not).
Fix: NUL-terminated records for both (`ZeroRecords`/`WriteZeroRecords`), an unterminated final
record written back without a NUL as GNU does. Guarded in `tests/cases/textutils.sh` (Git Bash
identical). Lesson for the option specs: an option accepted by `Opts.Parse` must be implemented
or thrown -- accepting it is a promise.

**Implemented by:** rev 57 -- `Builtins.Text.cs`. Released as `dist/Bash.exe` and the share copy.

## 2026-09-08 -- Globs on UNC paths (`//host/share/...`) matched nothing; fixed
**Status:** Active.

**Context:** web-d6's third report, every probe from a script file with a positive control:
`find //Guy-windows8/E/Claude -maxdepth 1 -name '*.facts.md'` found 4 files, `ls
//Guy-windows8/E/Claude/*.facts.md` found none, and `for f in //host/share/*.md` ran once with the
literal pattern. Reproduced here (this machine hosts the share) against Git Bash, which expands
them. `Glob.Expand` split the root as `/` and walked `\host` on the current drive.

**Decision:** a leading `//host/share/` (or `\host\share\`) is the root and the walk starts inside
the share; the typed separator is kept, so results read `//host/share/dir/file` as they do in
bash. Verified identical to Git Bash for `ls`, `for`, `echo`, `//localhost/...`, globstar across
the share (`**/*.sh`), and directory-only patterns. The "silent" half of the report is bash's own
default (a non-matching glob stays literal); `shopt -s failglob` already made it loud here and
still does. Guard: compat probe 233 (this host has the share; elsewhere both shells agree on
"no match"), baseline 221 -> 222. Not in the portable suite: there is no share every machine has.

**Also in that report, forwarded to the architect, not acted on (scope, decision 7 / coreutil
list):** awk `print > file`/`>>`/`close()`, 3-argument `match(s, re, arr)`, user-defined
functions; `df`; `awk --version`. Estimates recorded in the session summary of 2026-09-08.

**Implemented by:** rev 58 -- `Evaluator/Glob.cs` (`Expand`). Released to `dist/` and the share copy.

## 2026-09-08 -- sed `0,/re/` re-opened its range on every line; fixed
**Status:** Active.

**Context:** web-d6's fourth report, with a positive control (a no-op `sed -i` left the file
byte-identical): `sed -i '0,/^\*\*WEB-/{s/.../.../}'` changed all 20 matching records instead of
the first. Reproduced on rev 58 in every form (bare `s`, `{ }` block, `-n p`, `-i`); `1,/re/`,
`/a/,/b/` and `2,3` were correct. The report's priority argument is right: this failed silently
and by doing MORE than asked -- the worst of the three outcomes -- where the awk gaps fail loudly.

**Cause:** `SedSelect` treated the special zero start address as "matches on every line", so a
`0,/re/` range that had just closed opened again on the next line and never stopped. One
condition: the zero address matches only on line 1. Verified identical to Git Bash for all
six forms in `tests/cases/sed.sh`; compat probe 234 (baseline 222 -> 223).

**Lesson filed with the option-spec one from rev 57:** a range implementation that "works" for
the first occurrence and silently keeps going is exactly the plausible-wrong-answer class;
the sed test case now pairs every range form with a control range that must stop.

**Implemented by:** rev 59 -- `Builtins.Search.cs` (`SedSelect`). Released to `dist/` and the share copy.

## 2026-09-08 -- Build identity as shell variables; a lone `$` is literal (found by the test for the former)
**Status:** Active.

**1. `CSHARPBASH_BUILD` and `CSHARPBASH_REV`.** web-d6 asked for a build handle a script can bind
to as a VALUE rather than an output line position (it had been bitten by a position-bound check).
Two existed: the file's Product version (`(Get-Item Bash.exe).VersionInfo.ProductVersion`) and
`--version | grep -o 'hg\.[0-9a-f]*+* [0-9]*+*'`. Added a third for scripts running inside the
shell: `CSHARPBASH_BUILD` = the stamp string (`1.0.0+hg.<hash>[+] <rev>[+]`) and `CSHARPBASH_REV`
= the integer revision (0 if unstamped), so `(( CSHARPBASH_REV >= 59 ))` is a one-line check.
Plain shell variables, not exported (bash-standard variables are not), not readonly.
Rejected: putting the revision into `BASH_VERSINFO` (its slots have fixed meanings).

**2. A lone `$` glued itself to the next word.** The guard for (1), `[[ $x =~ ^[0-9]+$ ]]`, failed
to parse. `ParseWordParts` turned a `Dollar` token followed by ANY word token -- even across a
space -- into a variable expansion, a leftover from before the lexer consumed `$name` itself.
So `^[0-9]+$ ]]` swallowed the `]]`, and `[[ a$ == a$ ]]` read `==` as a variable name. A lone
`$` is now the literal it is in bash; the only non-literal case, `$"text"` locale quoting, is
kept (the `$` contributes nothing). Eight forms verified identical to Git Bash; suite lines in
`parser2.sh`, compat probe 235 (baseline 223 -> 224). This was a parse-time failure of a very
common idiom and had not been reported: the regex anchor `$` immediately before `]]`.

**Implemented by:** rev 60 -- `Program.cs` (`BuildStamp`/`BuildRev`, the two variables),
`Parser.cs` (`ParseWordParts` Dollar case), `tests/cases/{flags,parser2}.sh`. Released to
`dist/` and the share copy.

## 2026-09-08 -- `#` inside a word was a comment; `$((10#0010))` silently truncated the line
**Status:** Active.

**Context:** installer-79 (local session) sent a 13-line script that failed at parse time with
"Expected RParen but got Eof" at EOF, with every construct passing in its own file and five
bisection attempts finding no pair. It sent the reproduction rather than a guess -- the right
call. Bisecting here by dropping lines: only line 12 mattered, `( echo $((10#0010)) ) 2>&1 |
head -1`. The reporter's "line 12 alone works" was a near-miss of its own; the line fails alone.

**Cause:** the lexer treated `#` as a comment start wherever a token began, and `#` was not a
word character, so `10#0010` lexed as the number `10` followed by a comment that ate the rest of
the line. The consequences were worse than the parse error: `echo $((10#0010))` printed 10 by
COINCIDENCE (10 was what survived), `x=$((10#0010)); echo $x` printed nothing (the `; echo $x`
was inside the comment), `$((16#ff))` would have printed 16, and `echo a#b` printed `a`. The
arithmetic parser itself already understood `base#digits`; it never received it.

**Fix:** bash's rule -- `#` starts a comment only at the start of a word (after whitespace, a
newline, or an operator character); inside a word it is an ordinary character. `NextToken`
checks the preceding character; `ConsumeWord` accepts `#` after the first character, or as
the first when it follows a word character (`$x#y`). Verified identical to Git Bash: five
bases, the subshell form, the assignment form, `a#b c#d`, `x #comment`, `"$v#y" $v#y '#lit'
"#q"`. Guards: `tests/cases/parser2.sh`, compat probe 236 (baseline 224 -> 225).

**Lesson filed:** a silent-wrong-answer that a reporter could only observe as a parse error
elsewhere. The value coincidence (10 == 10) is exactly why "it printed the right number" is not
a test; `$((16#ff))` would have been the honest probe.

**Implemented by:** rev 62 -- `Lexer.cs` (`NextToken`, `ConsumeWord`). Released to `dist/` and
the share copy.

## 2026-09-11 -- Binary data through stdin was decoded as UTF-8; byte builtins now read the raw stream
**Status:** Active.

**Context:** installer-79 measured, with one file per byte value, that every byte >= 0x80 arriving
through `< file` or a pipe came out as EF BF BD (U+FFFD re-encoded), while a filename argument was
exact: `wc -c b.bin` 1000, `wc -c < b.bin` 3000, `head -c 1000 b.bin | cmp - b.bin` differs. It
had already cost a wrong size that nearly overturned a correct record, and a `dd seek=` past EOF
that made a test pass for the wrong reason. Reproduced here for cat, head/tail -c, wc -c, cmp, od,
md5sum, base64, tee through both routes.

**Cause:** P2 gave stdout a byte-faithful sink (`ConsoleMux.Raw`, `CurrentRawStdout`) but stdin
stayed a UTF-8 `StreamReader`: pipeline stages read `Console.In`, `< file` installed a
`StreamReader`, and `ReadBytes("-")` was `UTF8.GetBytes(Console.In.ReadToEnd())` -- decode then
re-encode, lossy for anything that is not valid UTF-8.

**Fix (the mirror of P2's stdout design):** `ConsoleMux.RawIn`, the byte view of the thread's
CURRENT stdin, set wherever a stdin is installed -- the pipe read end for a pipeline stage, the
`FileStream` for `< file`, `Stream.Null` for `/dev/null` and `0<&-`, the fd stream for `0<&3`,
and null for text sources (here-doc, here-string) -- saved/restored by `RedirectScope` like the
other slots and carried in `ConsoleMux.State` to job and stage threads.
`Evaluator.CurrentRawStdin()` returns it (or the process's own stdin when nothing is installed).
`ReadBytes("-")` (wc -c, head/tail -c, cmp, od, hexdump, checksums, base64), `cat -` and `tee`
read it directly; `cat`/`tee` copy bytes to the raw stdout when both exist. Text tools
(`head -n`, `grep`, `sed`, `read`) still decode as UTF-8: bash passes bytes there too, but a
line-oriented tool on binary input is not a case worth a byte-oriented text layer today.
Verified identical to Git Bash in `tests/cases/binary.sh` (three routes, chained pipes, tee,
checksums); compat probe 237 (baseline 225 -> 226).

**Found on the way, NOT fixed, AWAITING THE ARCHITECT:** `printf '\377'`, `printf '\xe9'`,
`echo -e '\xff'` and `$'\xff'` all emit the UTF-8 ENCODING of the character (c3 bf, c3 a9)
where bash emits the raw byte (ff, e9). Same class: the shell's strings are UTF-16 and every
writer encodes as UTF-8, so a byte produced by an escape cannot be told from a literal `ÿ` in the
source. The honest fix is the one Python took for filenames -- "surrogateescape": undecodable
input bytes and escape-produced bytes become U+DC80..U+DCFF in strings, and every writer the
shell creates (pipes, files, console, captures) maps those back to the bytes. That makes the
whole shell byte-transparent (`$(cat b.bin)` would round-trip too) at the cost of a custom
Encoding used everywhere the shell reads or writes text: about half a day, and a change to
every reader/writer site. The narrow alternative (printf/echo emit bytes when a raw stdout
exists, and only then) is ~40 lines but leaves `$'\xff'` and captures wrong. Recommendation:
the surrogateescape design, scheduled rather than slipped in. Fixtures in the new test use
`base64 -d` for exactly this reason.

**Implemented by:** rev 63 -- `ConsoleMux.cs`, `Evaluator.cs` (`CurrentRawStdin`, stage setup,
`ApplyRedirects`, `RedirectScope`), `Builtins.Text.cs` (`ReadBytes`, `Cat`, `Tee`),
`tests/cases/binary.sh`. Released to `dist/` and the share copy.

## 2026-09-11 -- `dotnet publish` under a UNC cwd: not a shell cost (measured); and C#Bash does not convert MSYS paths in arguments to native programs
**Status:** Active. Two findings from installer-79's performance question; the peer independently
reached conclusion 1 while this was being measured here.

**1. The 250x is the UNC path, not the shell. MEASURED here on a trivial net8.0 app, same files,
same output directory, one variable moved:**

| shell | cwd form | time |
|---|---|---|
| C#Bash | `E:\CLAUDE\perf-probe\app` (drive letter) | 2.7 s |
| C#Bash | `\Guy-windows8\E\CLAUDE\perf-probe\app` (UNC, SAME FILES) | 235.7 s |
| Git Bash | same UNC | 233.7 s |
| PowerShell | same UNC (as its location) | 233.7 s |
| C#Bash | local temp dir | 1.06 s |
| Git Bash | local temp dir | 2.67 s |

87x on identical files, and **all three shells are within 1 % of each other on the UNC path** --
C#Bash is if anything the fastest. E: is a local disk on this machine, so a UNC path to it routes
every file operation through the SMB client redirector over loopback to the SMB server and back;
MSBuild does thousands of small path operations and pays the round trip each time. Nothing in
C#Bash's child launch, console handles, job object or environment is involved. The peer's own
data already said so (a 600 KB Zig compile at 3.4 s, a 26-check install suite at 1.6 s, both
through C#Bash, both from the same UNC tree) -- few processes doing heavy work are fine; many
operations doing light work are not. Note the PowerShell row corrects the premise of the original
report: PowerShell is NOT fast on a UNC path either, so the earlier "~1 s from PowerShell" figures
must have been taken with a drive-letter location.

**Known limit worth stating (the peer's suggestion, adopted):** a UNC path to a LOCAL drive is
silently correct and catastrophically slow for any tool doing many small file operations. It is
invisible to a functional test -- exit 0, right bytes -- and shows up only as duration. Applies to
every shell; recorded here because C#Bash users on this machine reach `E:` that way.

**2. AWAITING THE ARCHITECT -- C#Bash passes MSYS-form paths to native programs verbatim.**
Found while probing the above. Git Bash's MSYS runtime rewrites `/c/x` -> `C:\x` and
`//host/share/x` -> `\host\share\x` in the ARGUMENTS of a native (non-MSYS) child; C#Bash does
not. Two shapes, one loud and one silent:
- `dotnet publish //Guy-windows8/E/.../App.csproj` -- MSBuild read the leading `//` as a switch:
  `MSBUILD : error MSB1001: Unknown switch.` Loud, exit 1, fine.
- `dotnet publish App.csproj -o /c/Users/guy/AppData/.../outP` -- dotnet resolved the MSYS path
  against the current drive and published to **`C:\c\Users\guy\AppData\...`**, exit 0. A silent
  wrong directory: verified in MSBuild's own echo of its command line
  (`--property:PublishDir=C:\c\Users\...`).
This is the plausible-wrong-answer class the project has been closing all week, and it is a design
decision rather than a bug fix: doing it means a heuristic over every argument of every external
command (which MSYS gets wrong too -- it mangled `cmd.exe /c echo //host/...` in a control run
here), with escape hatches (`MSYS2_ARG_CONV_EXCL`-style) and a blast radius across every script
that currently relies on verbatim arguments. Options: (a) convert leading-`/` arguments that look
like paths when the child is not a C#Bash script, MSYS-style; (b) convert nothing and document,
telling users to write native or relative paths to native tools (status quo); (c) convert only
`//host/...` and `/drive/...` at the START of an argument, never mid-string, and only when the
resulting path exists -- narrower than MSYS and less likely to mangle. **Recommendation: (c)**,
about half a day with a compat probe per shape. **Falsifier:** if scripts on this machine already
pass MSYS paths to native tools successfully by accident of them being relative, (b) is fine.
Not started; nothing in the tree changed for this.

**Implemented by:** rev 66 (documentation only; no code change for either finding).

## 2026-09-11 -- Sharpened: `/tmp` means TWO different places inside one C#Bash session
**Status:** Active. Strengthens the "MSYS arg conversion" item of the entry above with a worse
observation; supersedes nothing.

**Measured (one command, cwd `E:\`):**

    echo BUILTIN-WROTE-THIS > /tmp/split-probe.txt        -> C:\Users\guy\AppData\Local\Temp\split-probe.txt
    cmd.exe /c "echo NATIVE> \tmp\split-probe-native.txt" -> E:\tmp\split-probe-native.txt

`TranslatePath` maps `/tmp` to `%TEMP%` for the shell's own builtins and redirects (correct, MSYS
semantics), while a native child receives `/tmp/...` verbatim and resolves it against the CURRENT
DRIVE. So within a single script `echo x > /tmp/f` and `sometool /tmp/f` are different files, and
which file a native tool gets depends on the current drive at the time. This is not merely
"different from Git Bash" -- it is **internally inconsistent**, which the previous framing missed.

**Corroboration from installer-79, on disk right now:** `E:\c\Users\guy\` exists (nothing created
it deliberately -- it is `/c/Users/...` resolved against E:), its .NET app with `--out /tmp/probe.exe`
landed at `E:\tmp\probe.exe`, and `C:\tmp` holds 18 entries while `E:\tmp` holds 3 -- the same
`/tmp` scattered across two disks by whichever drive happened to be current. Verified here
read-only.

**Why it has no failing observable (the peer's framing, adopted):** a script that writes `/tmp/x`
and then reads `/tmp/x` through the SAME kind of command works perfectly -- both hit the same wrong
place. Only a builtin-then-external (or external-then-builtin) pair diverges, and only then does
anything look wrong. Like the UNC slowness, the failure is invisible to a functional test.

**Refined recommendation (replaces option (c) above, which was wrong for output paths -- it
required the target to exist, and `/tmp/newfile` never does).** Translate an argument to a NATIVE
child with exactly the mapping `TranslatePath` already applies to the shell's own paths, so the
two halves of the shell agree by construction, when the argument STARTS with:
  * `//host/share/...` (currently loud-fails: MSBuild read `//` as a switch)
  * `/<letter>/...` -- drive form, REQUIRING a trailing `/` and at least one more character
  * `/tmp` or `/tmp/...`
  * `~` or `~/...`
**Critical exclusion: a bare `/c`, `/d` … with nothing after it must NOT convert** -- `cmd.exe /c`
is a switch, and converting it to `C:\` breaks every `cmd /c` in every script. (MSYS gets this
class wrong: a control run here had it mangle `cmd.exe /c echo //host/...` into a broken command.)
Nothing else converts; the general Unix root mapping stays DEFERRED per 2026-09-04 decision 4.
Escape hatch: an env var to disable conversion wholesale (MSYS spells it `MSYS2_ARG_CONV_EXCL`).
Estimated half a day with a compat probe per shape plus a `cmd /c` regression.
**Falsifier:** if the architect prefers the shell to stay literal, the alternative is to make the
INCONSISTENCY loud instead -- e.g. builtins stop accepting `/tmp` too -- which is worse for
compatibility. **Priority argument:** this is the same silent-wrong-answer class as the sed range
and the stdin bytes, it is already on disk, and this machine's Claude sessions run C#Bash.

**Implemented by:** rev 67 (documentation only).

## 2026-09-11 -- `pwd` returns the forward-slash Windows form (supersedes decision 3 of 2026-09-04); unhandled file-open exceptions no longer kill the shell
**Status:** Active. **Supersedes the 2026-09-04 entry's decision 3** ("`pwd` prints native Windows
form"), which stands as the record of what was decided then and why.

**1. Paths handed back to scripts use forward slashes. Ratified by the architect 2026-09-11:**
"If Bash is handed a windows form path (using backslash), it must convert it to a forward slash
path before using - bash should be able to detect this."

Decision 3 was ratified on ONE criterion -- a P5 spike showed Claude Code's cwd read-back accepts
`F:\…`. A second criterion appeared today, raised by the architect through installer-79, and it
is decisive: **backslash is an ESCAPE CHARACTER in the shell language.** `PWD=E:\Claude\Installer`
is a value the shell's own operations misread -- `${p#pat}`, globs, `[[ ]]`, `case` and every later
expansion treat those backslashes as escapes. MSYS and Git Bash return `/e/Claude/Installer` for
exactly this reason: the value is safe in the language it is returned INTO. `E:/Claude/Installer`
satisfies both criteria at once -- every Windows API and native tool accepts forward slashes, and
there are no escape characters in it.

`ShellEnvironment.ToShellPath` (a straight `\`→`/` swap, which preserves a UNC leading pair:
`\host\share\x` -> `//host/share/x`, and keeps a root valid: `E:\` -> `E:/`) is applied to
`pwd`, `$PWD`, `$OLDPWD`, `cd -`, `dirs`/`pushd`, `mktemp`, and `realpath` -- the paths the shell
HANDS BACK. `TranslatePath` (the inbound direction) is unchanged, so `/c/…`, `/tmp`, `~` and
native forms all still work as arguments.

**Falsifier re-tested, because it is what ratified decision 3:** a nested `claude -p` session
driven by this build tracked `cd tests` across calls, reporting `F:/Koliada/Tools/bash` then
`F:/Koliada/Tools/bash/tests` twice. Claude Code's cwd read-back accepts the forward-slash form.
Suite 48/48, battery 226/237 (baseline 226) unchanged -- no expectation depended on backslashes.
Verified directly that the hazard is gone: with `R=$(pwd)`, `${R#E:/}`, `case "$R" in E:/*)` and
`[[ "$R" == E:/* ]]` all behave.
**Not converted (deliberate):** `find`/`ls`/`du` output and `stat`, which echo the operand's own
form, and anything a native child prints. Widening it further is a separate decision.

**2. A failed file open no longer kills the shell (installer-79).** It reported an unhandled
`SafeFileHandle.Open` exception taking the process down -- correctly noting that this is a defect
independent of whatever path provoked it, since a script loses every line of prior output with it.
**Reproduced here:** `split -l 1 f.txt nodir/sub/pre` (a missing output directory) ->
`Unhandled exception. System.IO.DirectoryNotFoundException`, process dead. Two guards added:
`Builtins.TryExecute` and `Evaluator.RunString` now catch `IOException`,
`UnauthorizedAccessException`, `ArgumentException`, `NotSupportedException` and
`SecurityException`, report them as ordinary shell errors and return 1.
**Critical exclusion, nearly a regression:** `BrokenPipeException` DERIVES from `IOException` and
carries the P2 SIGPIPE semantics -- the first version of the guard would have swallowed it and
broken `yes | head` (141). Both catches exclude it explicitly; re-verified 141 after the change.

**Found and NOT fixed (recorded for the next session, with repros):** parameter substitution
`${v/pat/repl}` does not process the replacement at all -- measured against Git Bash:
`${V//b/\}` gives `a\` where bash gives `a\` (escapes not removed), `${V//b/'\'}` keeps the
quote characters (no quote removal), and **`${V/$A/$B}` does not expand `$A` or `$B` at all**, so
`${path//$old/$new}` silently returns the input unchanged -- a no-failing-observable defect in a
very common idiom. Repro files: `scratchpad/sub.sh`, `sub2.sh`, `sub3.sh`. The fix needs the
replacement (and pattern) to go through expansion + quote removal; ~half a day. installer-79
reported the escape half and noted this item is downstream of (1) -- with `pwd` returning forward
slashes, most scripts never reach for `${p//\//\}` at all.

**Implemented by:** rev 68 -- `ShellEnvironment.cs` (`ToShellPath`/`ShellCwd`, startup `PWD`),
`Builtins.Shell.cs` (`Cd`, `Pwd`, `PrintDirs`), `Builtins.Files.cs` (`Mktemp`, `Realpath`),
`Builtins.cs` + `Evaluator.cs` (the guards).

**Guard added the same day (installer-79's caution, adopted):** the conversion is OUTBOUND ONLY.
Applied to arguments instead it would silently break every escape context -- `grep '\.txt'`
becoming `grep '/.txt'` still runs and still exits 0, matching the wrong set. Its three
discriminating tests are now permanent in `tests/cases/paths.sh` alongside the outbound checks
(`printf 'a\tb'` must emit a TAB; `echo Xtxt | grep -c '\.txt'` must be 0; `echo "C:\temp"`,
`find -name '\*'`, `'lit\nlit'`), and the whole case is byte-identical to Git Bash -- it asserts
the ABSENCE of backslashes and the usability of the value rather than a literal path, so it holds
on any machine.

## 2026-09-11 -- The "C#Bash crash" was Windows Defender killing a DIFFERENT process; the shell was never involved
**Status:** Active. Closes the crash thread of the entry above.

**The discriminating evidence** (the architect captured the full exception; installer-79 could see
only that something died):

    System.IO.IOException  HResult=0x800700E1
    Message=... the file contains a virus or potentially unwanted software.
            : 'E:\Claude\Installer\release\stub.exe'
       at System.IO.File.ReadAllBytes(String path)
       at Program.Main(String[] args)

`0x800700E1` is ERROR_VIRUS_INFECTED, not the ERROR_PATH_NOT_FOUND (3) the earlier decompilation
suggested -- the root-length branch read from the .NET source was the generic code path, not the
branch actually taken. **Verified here:** `Get-MpThreatDetection` shows Defender hitting
`E:\Claude\Installer\release\stub.exe` three times today (16:26, 16:28, 16:54 -- the last is the
rev-68 canary run), ThreatID 2147731250, and `stub.exe` is GONE from that directory while the
other four release files remain. The dying process is installer-79's own .NET installer app
reading its colocated stub: `Program.Main(String[] args)` is a conventional entry point, whereas
C#Bash uses top-level statements and would appear as `Program.<Main>$` (verified: zero
`static … Main` declarations in `Program.cs`), and C#Bash never reads a stub.

**Why the misattribution held for two rounds:** the failure appeared immediately after a shell
change, the only visible symptom was "the process is gone", and a plausible mechanism was
available (a bad path from the shell reaching a file open). All three are the signature of
attributing a fault to the most recently changed component. The shell had been genuinely wrong
four times the same day, which made it the likeliest suspect and, in this instance, the wrong one.
**A stack frame from the dying process settles in one line what four rounds of black-box bisection
could not** -- worth asking for before bisecting, whenever a crash can be captured at all.

**Not a C#Bash defect, and nothing here is reverted:** the rev-68 guard stands on its own merits
(it was reproduced independently with `split -l 1 f nodir/sub/pre`), and it now also means that
if C#Bash itself reads a Defender-blocked file it reports `IOException` and returns 1 rather than
dying -- which is what a shell should do while an AV product is deleting files under it.

**For the reporter (passed on, not actionable here):** a tool that appends a payload to a PE stub
is dropper-shaped by construction and will keep being flagged; the fixes are an exclusion for the
build output directory or a differently-shaped artefact, and the app should handle that
`IOException` rather than terminating on it.

**Correction, same day, within the hour:** installer-79 retracted the "crash survives rev 68"
report -- that run COMPLETED, exit 0, 273,985 ms; it had read truncated output from a command that
had exceeded its timeout and been backgrounded, and relayed "the process died". So the entry above
should not be read as saying the rev-68 canary crashed: Defender did fire during it (16:54), but
the run finished. What the dump the architect captured proves remains exactly as stated -- an
ERROR_VIRUS_INFECTED IOException inside the installer app, not the shell -- and it came from an
earlier run. Whether rev 68 changes anything about that is untested and probably moot, since the
fault was never in C#Bash.

**One conclusion of the retraction is itself wrong, and was passed back:** it attributed the
missing `release/stub.exe` to `dotnet publish -o` cleaning the output directory (an MSBuild
behaviour). Defender deleted it -- three detections on that exact path today, ThreatID 2147731250,
the last at 16:54, with the other four release files untouched. An AV product removing a build
artefact and MSBuild cleaning an output directory look identical from the script's side (the file
is simply gone), and only the AV log distinguishes them.

## 2026-09-12 -- The shell's path form is forward slash in BOTH directions; IO failures are loud everywhere
**Status:** Active. Completes the 2026-09-11 entry, on the architect's direction: "the path
separator char should catch i/o exceptions loud and not crash and backslash to forwardslash should
be symmetric across read/write operations. This should include MSYS form mapping assuming
/<d>/... as D:/..."

**1. Symmetry.** Rev 68 converted only the OUTBOUND direction, which left the shell with two path
forms: `pwd` returned `E:/x` while `TranslatePath` produced `E:\x` for the filesystem APIs.
`TranslatePath` now produces the forward-slash form too -- `/c/x` -> `C:/x` (the MSYS mapping the
architect named), `/tmp/x` -> `<temp>/x`, `~` -> the profile dir, and any backslashes in the input
normalised -- so **a path that goes out through `pwd` and comes back in as an argument is the same
string**. .NET's file APIs accept forward slashes everywhere, so nothing needs the backslash form
internally; `ShellEnvironment.FullPath` wraps `Path.GetFullPath`, which hands back backslashes
whatever it was given. Verified: `pwd` -> `cd` -> `pwd` is stable, `realpath` agrees with `pwd`,
the MSYS form reads the same file, and no outbound path carries a backslash.

**2. IO failures are loud, never fatal -- now at every entry point.** Rev 68 guarded
`Builtins.TryExecute` and `RunString`. Three holes remained, all of which would have killed the
process: the script read in `Program.cs` (outside the try -- a locked or AV-blocked script file),
`SourceFile`'s read, and the interactive loop. All three now report and continue (script read
returns 126). `BrokenPipeException` is excluded everywhere -- it derives from `IOException` and
carries SIGPIPE.

**3. A redirect failure now speaks bash, not .NET.** Measured against Git Bash while writing the
guard test: `echo x > nodir/sub/o.txt` gave `bash: nodir/sub/o.txt: Could not find a part of the
path 'F:\…\nodir\sub\o.txt'.` and status **2**, where bash gives `No such file or directory` and
status **1** (2 is reserved for syntax-level errors). New `RedirectException` carries status 1,
and `RedirectMessage` maps the exception to bash's wording, including "Is a directory" for a
directory target. Both forms are now byte-identical to Git Bash.

**Two fidelity gaps found doing this, recorded NOT fixed** (both visible in `tests/cases/paths.sh`,
which works around them):
  * bash prefixes a runtime error in a script with `<script>: line N:`; C#Bash says `bash:`.
  * C#Bash prints a failed redirect's message AFTER the enclosing redirect scope is released, so
    `( cmd > bad/path ) 2>/dev/null` does not suppress it as bash does. The message is emitted
    from `RunStatement`, outside the scope that would capture it.

**Guard:** `tests/cases/paths.sh` extended -- outbound cleanliness, the shell's own string
operations on the value, round-trip symmetry, the MSYS inbound form, four IO failures in a row
with the shell still running, and installer-79's escape-context discriminators. Byte-identical to
Git Bash. Suite 49/49, battery 226/237 (baseline 226), no regressions.

**Implemented by:** rev 71 -- `ShellEnvironment.cs` (`TranslatePath`, `FullPath`), `Program.cs`
(script read + REPL guards), `Evaluator.cs` (`SourceFile` guard, `RedirectException`,
`RedirectMessage`), `EvalException.cs`, `Builtins.Text.cs` (`IoError` made internal).

## 2026-09-12 -- One byte-transparent encoding for the whole shell (printf byte escapes fixed)
**Status:** Active. Built on the architect's direction, after he asked whether all process I/O
should simply be UTF-8: it already was, and that was the cause.

**The problem, restated.** A bash string is a BYTE SEQUENCE; a .NET string is UTF-16 and every
writer encoded UTF-8 on the way out. That is lossy in both directions. `printf '\377'` means
"the byte 0xFF" but produced the CHARACTER U+00FF and two bytes; a byte that was not valid UTF-8
decoded to U+FFFD and could never be written back. Measured before the change: five raw bytes
through `x=$(cat b.bin)` came back as TWO characters and SIX different bytes.

**The fix is one class, not a per-site judgement.** `Evaluator/ShellEncoding.cs` is UTF-8 with
Python's "surrogateescape": an undecodable byte becomes the lone surrogate U+DC00+byte, and
encoding maps that range back to the single original byte. Text is ordinary UTF-8 and encodes at
plain UTF-8 speed (runs between escapes are delegated whole); anything that was not text survives
a round trip anyway, so **the shell never has to decide whether its data is text**. The escape
handlers (`printf '\xNN'`/`'\NNN'`, `echo -e`, `$'…'`) produce the range directly via `ByteChar`;
`\u`/`\U` remain CODE POINT escapes, as in bash.

**Why a subclass and not a fallback pair:** a `DecoderFallback` can emit characters, so the decode
direction could have been a fallback — but an `EncoderFallback` may only emit characters to be
re-encoded, never raw bytes, so the encode direction cannot. Both live in the subclass.

**Three bugs found while wiring it, each of which would have shipped silently:**
  * **The escape range overlaps real surrogate pairs.** U+DC80–DCFF is also the LOW half of a
    legitimate astral character, so valid 4-byte UTF-8 was being decoded, then re-encoded as raw
    bytes. Only a LONE low surrogate is a carried byte — every encode path checks that the
    preceding character is not a high surrogate. Caught by a 64 KB random round-trip check, not
    by any targeted case.
  * **`File.ReadAllText(path, encoding)` ignores the encoding when the file starts with a BOM.**
    It defaults `detectEncodingFromByteOrderMarks: true`, so a file beginning FF FE was decoded as
    UTF-16 and the requested encoding discarded — which is exactly the binary case. Replaced by
    `ShellEncoding.ReadAllText`, which reads bytes and decodes them.
  * **`StreamWriter`'s default encoding THROWS on a lone surrogate** (`UTF8NoBOM` is constructed
    with throwOnInvalidBytes). `printf '\xff' > file` died with an encoder error. Every stream the
    shell builds now names the encoding explicitly.

**Scope:** ~58 sites in 10 files, all mechanical (`Encoding.UTF8` / `new UTF8Encoding(false)` /
unqualified stream constructions / the `File.ReadAll*` family / `Console.Output|InputEncoding`).

**A deliberate divergence from bash, in our favour:** bash cannot hold a NUL in a string (C
strings) and drops it with "warning: command substitution: ignored null byte in input". We
preserve it — 4 KB of random bytes round-trips through `$( )` exactly here and does not in bash.
Losing data to match a limitation is not worth doing; documented rather than emulated.

**Guard:** `tests/cases/bytes.sh` (escapes emit bytes, `\u` stays a code point, text stays UTF-8,
and the bytes survive a variable, a capture, a pipe, a file and a here-string) — byte-identical to
Git Bash. Suite 49 → 50, battery 226/237 unchanged.

**Implemented by:** rev 72 -- `Evaluator/ShellEncoding.cs` (new), `Printf.cs`, `Lexer.cs`
(`\xNN`/`\NNN` byte escapes, octal added), `Evaluator.cs`, `Builtins.{Text,Search,Find,Awk,Sys,Shell}.cs`,
`WordExpander.cs`, `IO/History.cs`, `Program.cs`.

## 2026-09-12 -- Ship build is ReadyToRun; Native AOT blocked on tooling; three-way benchmark added
**Status:** Active.

**Startup is the number a Claude Code user actually pays** — every Bash tool call spawns
`bash.exe -c "<wrapper>"` — so it was measured before anything else was optimised. Best of 7,
`-c 'exit 0'`:

| build | startup |
|---|---|
| single-file, plain | 0.086 s |
| single-file + ReadyToRun | **0.074 s** |
| Git Bash | 0.028 s |

Split further: `--version` (which returns before the evaluator is constructed) costs 0.059–0.061 s
in both, so **~60 ms is the .NET single-file runtime floor** and the rest is our own startup —
which ReadyToRun roughly halves (27 ms → 13 ms) by removing JIT of our code. **Decision: ship
ReadyToRun** (`-p:PublishReadyToRun=true`), 14 % off startup for 68 MB → 79 MB. Validated: all 50
suite cases pass against the R2R binary.

**Native AOT was attempted and is blocked, not rejected:** `-p:PublishAot=true` fails at the link
step (`MSB3073`, `link.rsp`) because the MSVC C++ linker is not installed here. It is the only
lever that would touch the 60 ms floor (AOT startup is typically single-digit ms), so it is worth
revisiting on a machine with the VS C++ build tools — with one known risk to test first:
`ConsoleMux` installs itself by reflecting on `Console`'s private static fields, which AOT may
not preserve.

**Not taken, and why (measured, not assumed):** `FUNCNAME`/`BASH_SOURCE` are rebuilt as
`SortedDictionary` arrays on every function entry AND exit, for a variable almost nothing reads.
A/B: removing both writes saved **376 B per call but no measurable time** (1310 vs 1290 ns,
inside noise). Real allocation, no speed — filed as a memory improvement, not taken now, because
the risk of making `FUNCNAME` lazy is not repaid by a number that does not move.

**`tests/bench/compare3.sh`** compares all three shells on the same scripts, best of N, and
checks their OUTPUTS agree so a fast wrong answer cannot read as a win. Fairness is documented in
the script rather than engineered away: WSL's Windows entry point takes only `-c`, so its scripts
are sourced into one WSL bash; WSL reaches these files over `/mnt` (9P), which is a real cost of
using it on Windows files and is marked in the table; and WSL bash is native Linux bash while Git
Bash runs on the MSYS2 emulation layer.

**Implemented by:** rev 73 -- `tests/bench/compare3.sh`, README build recipe.

## 2026-09-12 -- Three-way benchmark result (C#Bash / Git Bash / WSL bash)
**Status:** Active. The numbers the README now publishes, and what they are and are not.

**Measured** on one Windows 10 machine, best of 3, wall clock including startup, every row's
OUTPUT compared across all three shells and identical (a fast wrong answer is not a win):

| benchmark | C#Bash | Git Bash 4.4 | WSL bash 5.1 |
|---|---:|---:|---:|
| loop 200 k | 0.358 | 1.519 | 0.424 |
| arith 200 k | 0.427 | 2.124 | 0.625 |
| func 100 k | 0.486 | 2.366 | 0.577 |
| loop_big 2 M | 1.301 | 19.537 | 3.443 |
| coreutils (900 spawns) | 0.160 | 20.569 | 0.787 |
| pipeline (50 k lines, 5 tools) | 0.456 | 0.344 | 0.125 |
| find (400 files) | 0.098 | 0.194 | 0.107 |
| startup | 0.074 | 0.028 | 0.101 |

**Read it honestly.** Interpreter throughput beats native Linux bash, which is the surprising
result and the one most worth stating plainly. `coreutils` at 129× Git Bash is the project's
premise made visible — 900 `CreateProcess` calls against none. **Two rows are losses and are
published as losses:** startup (74 ms vs 28 ms; ~60 ms is the .NET single-file floor, only Native
AOT would move it) and the streaming text pipeline, where native C `grep`/`sed`/`sort` beat
managed ones per line and WSL wins outright. The shape that suits C#Bash is many small operations;
pushing 50 k lines through five real tools is not it.

**What the numbers are not:** one machine, one run, no statistical treatment. WSL reaches these
files over the 9P `/mnt` bridge — a real cost of using it on Windows files, marked in the harness
rather than engineered away. Git Bash ships bash 4.4 where WSL has 5.1.

**Implemented by:** rev 76 -- README "Performance".
