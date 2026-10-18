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

## 2026-09-13 -- Native AOT builds and is 2.5x faster to start; the Zig apphost is a 3 ms / 82 KB win on the framework-dependent path
**Status:** Active. Supersedes the "Native AOT blocked on tooling" finding of 2026-09-12, which was
wrong about the cause.

**1. Native AOT WORKS here. The 2026-09-12 conclusion was a bad diagnosis.** The link failure was
not a missing MSVC linker — VS 2022 Professional's C++ toolchain is installed. Two environmental
things were needed, and the second is the one that wasted the earlier attempt:
  * `vcvars64.bat` first, so `link.exe` has `LIB`/`INCLUDE`/`PATH`; and
  * **`vswhere.exe` ON PATH** (it lives in `…\Microsoft Visual Studio\Installer`, which vcvars does
    NOT add). The ILCompiler shells out to `vswhere` to locate the linker and, when it is missing,
    **splices its own error text into the command line it then tries to run** —
    `The command ""'vswhere.exe' is not recognized…;…\link.exe" @"…link.rsp"" exited with code 123`.
    That is why the first attempt looked like a missing linker: the real linker path was in the
    message, immediately after the error text.
Recipe kept at `scratchpad/aot.cmd`; the two `-p:` flags are just `PublishAot=true -p:DebugType=none`.

**Measured, best of 11 for startup, best of 3 per benchmark:**

| build | startup | size | loop | arith | func | coreutils | pipeline | find |
|---|---:|---:|---:|---:|---:|---:|---:|---:|
| **Native AOT** | **0.029** | **5.7 MB** | 0.172 | 0.259 | 0.229 | 0.073 | 0.266 | 0.038 |
| self-contained + R2R (shipped) | 0.073 | 79 MB | 0.360 | 0.428 | 0.489 | 0.149 | 0.472 | 0.100 |
| Git Bash (reference) | 0.028 | — | | | | | | |

**AOT startup matches Git Bash** (0.029 vs 0.028), erasing the one row the benchmark lost badly,
and every short benchmark is 1.65–2.6x faster. **Correctness is unaffected:** 50/50 suite cases and
the 237-probe battery at 226/237 with the baseline held, both identical to the managed build, and
the six subsystems most at risk were checked explicitly — the `ConsoleMux` reflection on `Console`'s
private static fields (the thing feared on 2026-09-12) SURVIVES, as do regex + `BASH_REMATCH`, the
awk parser, the Toolhelp32/NtQuery P/Invokes, job objects and `timeout`, the custom `ShellEncoding`
subclass, and UNC globs.

**The honest cost, and it is real: AOT is ~10 % SLOWER in steady state.** `loop_big` (2 M
iterations) with startup subtracted: 1.366 s of work under AOT against 1.228 s under R2R — 0.90x.
That is the expected AOT characteristic (no dynamic re-optimisation). So the crossover is about
**0.4 s of work**: AOT saves 44 ms of startup and gives back ~11 % of throughput, so it wins for
anything shorter and loses for a long batch loop. **Claude Code spawns a fresh shell per tool call
running a short command, which is the side of the crossover AOT wins**, and it is 13x smaller.
**Recommendation: ship AOT** — the architect's call, since it adds a BUILD-machine prerequisite
(MSVC + vswhere) that the R2R build does not have.

**2. The Zig apphost (E:/Claude/AppHost) is a real but small win, on one path only.** Framework-
dependent single-file, same settings: **0.080 s on the Zig host against 0.083 s on the stock host**
(~3 ms, as the architect predicted), and 82 KB smaller (597,465 vs 679,385 bytes). With R2R added:
0.068–0.070 s at 1.68 MB, which is the fastest managed configuration measured — but it needs the
.NET 8 runtime installed on the target, which the current ship deliberately does not.

**AppHost's own `limits:` note is CORRECT, and the reason is structural, not an SDK quirk** — the
architect asked this to be verified rather than repeated. A self-contained single-file publish does
not use `apphost` at all: `_CreateSingleFileHost` takes `$(SingleFileHostSourcePath)`, and the stock
`singlefilehost.exe` is **~10 MB**, not 141 KB, because it CONTAINS the runtime-loading machinery
and loads CoreCLR and every assembly from inside its own bundle. The Zig host's job is the
opposite: locate an installed .NET and hand off to `hostfxr`. Verified both ways — publishing
self-contained with `-p:AppHostSourcePath=<zig>` ignores it (68,043,853 bytes, the normal size),
and forcing `-p:SingleFileHostSourcePath=<zig>` builds a 58 MB binary that dies with
`A fatal error was encountered. The library 'hostpolicy.dll' … was not found`.

**A measurement error worth recording, because it nearly produced a false finding:** the first
Zig-apphost test showed the stock and Zig publishes byte-identical, and I briefly concluded
`AppHostSourcePath` was ignored on that path too. It was a stale `obj/`: `-v n` showed
`Skipping target "_CreateAppHost" because all output files are up-to-date`. **Any apphost
experiment must delete `obj/` first** — the intermediate patched host is cached, so changing the
host property alone does not invalidate it. Also: comparing the published exe's leading bytes
against the host template is NOT a valid check, because `CreateAppHost` patches placeholders inside
it; compare sizes (the two hosts differ by ~76 KB) or diff against a known-good publish.

**Implemented by:** rev 78 (documentation only; no code change, ship build unchanged pending the
architect's decision on AOT).

**Correction to the crossover figure in the entry above (measured 2026-09-13).** That entry put the
AOT/R2R crossover at "~0.4 s of work", derived arithmetically from 44 ms of startup saving against
an 11 % throughput penalty. That derivation was wrong, because it assumed the penalty applies at
every scale. It does not — R2R's ReadyToRun code is CONSERVATIVE, and the JIT only overtakes AOT
once it has re-compiled the hot path, which takes about a million iterations to pay back. Measured
on the same loop at four sizes (work = total minus each build's startup):

| iterations | AOT work | R2R work | winner |
|---:|---:|---:|---|
| 200 k | 0.145 | 0.286 | AOT, 2.0x |
| 500 k | 0.350 | 0.435 | AOT, 1.24x |
| 1 M | 0.688 | 0.714 | AOT, level |
| 2 M | 1.369 | 1.233 | R2R, 1.11x |

**So the crossover is ~1.2 M shell operations (~0.7 s of pure interpretation), not 0.4 s** — and
including startup AOT stays ahead to roughly 2 M. The architect's point that long scripts are rare
in Claude Code use is therefore an understatement of the case for AOT: to lose, a SINGLE shell
invocation would have to perform over a million interpreter operations. It is also worth separating
wall clock from interpreter work — installer-79's 273-second build spends nearly all of that inside
`dotnet publish`, an external process, and only milliseconds in the shell, so the penalty never
applies to it at all.

## 2026-09-13 -- RATIFIED: the ship build is Native AOT
**Status:** Active. Supersedes the ReadyToRun ship decision of 2026-09-12 (which itself superseded
nothing — it was the first). The architect: "lets settle on AOT and re-publish."

**The deliverable is now one static 5.7 MB executable** produced by `tools\publish-aot.cmd`, with
no .NET runtime required on the target and ~29 ms startup against the ReadyToRun build's ~74 ms —
the same startup as Git Bash, which erases the one benchmark row this project lost badly.

**Why the script rather than a documented `dotnet publish` line.** AOT needs two things on the
BUILD machine that are easy to get wrong, and one of them produces a message that actively
misleads: without `vswhere.exe` on PATH the ILCompiler splices its own error text into the linker
command line, so the failure reads as a missing linker while naming the real linker in the same
string. That cost a day's misdiagnosis. The script checks both prerequisites and explains the trap
in a comment, so the next person does not repeat it. It also deletes `obj/` first, because a stale
intermediate is silently reused.

**The trade accepted, stated plainly:** AOT cannot re-optimise at runtime, so past roughly 1.2 M
shell operations in a single invocation the ReadyToRun build is ~11 % faster. No Claude Code tool
call approaches that, and wall clock is not the measure — a build script that runs for minutes
inside `dotnet publish` does milliseconds of interpretation. Both managed recipes stay in the
README for anyone who wants the smallest download (1.7 MB, framework-dependent) or cannot build
AOT.

**Verified on the AOT binary before adopting it:** 50/50 suite cases and the 237-probe battery at
226/237 with the baseline held, both identical to the managed build; and the six subsystems most at
risk under AOT checked explicitly — the `ConsoleMux` reflection on `Console`'s private static
fields, regex with `BASH_REMATCH`, the awk parser, the Toolhelp32/NtQuery P/Invokes, job objects
and `timeout`, the custom `ShellEncoding` subclass, and UNC globs.

**Implemented by:** rev 80 -- `tools/publish-aot.cmd` (the ship recipe), README build section and
Performance table re-measured against the AOT build.

## 2026-09-13 -- The companion article ships inside the repo (reverses the `.gitignore` exclusion)

**Status:** Active. Reverses the standing exclusion of `moosh-all-the-way-down.md`, which existed
only as a comment in `.gitignore` ("The LinkedIn article lives elsewhere, not in the code repo")
and was never recorded here, so there is no prior entry to supersede.

**Context:** the article *It's moosh all the way down* was published on LinkedIn on 2026-09-13 and
the README now links it (commit `e1b6495`). Its source markdown had been deliberately git-ignored
since the initial public snapshot, and its hero image sat untracked in the working tree.

**Decision:** ship both the article source and its art in the repository root --
`moosh-all-the-way-down.md` and `moosh-all-the-way-down.png` (1024x559 RGB, 1.5 MB).

**Rationale:** the article is this project's provenance statement. It is the only document that
explains why a bash interpreter exists whose source no human has read, and it carries the same
three-way benchmark the README reports. LinkedIn is not an archival host: the URL can rot, the
piece can be edited or withdrawn, and it sits behind a login wall for some readers. A repository
that already ships its complete Mercurial history precisely so the record does not depend on a
third party should not then depend on a social network to preserve the one document that explains
its method.

**Alternatives considered:**
- **Keep it out (the prior position)** -- rejected: it makes the README's outbound link a single
  point of failure for the project's rationale.
- **Ship the markdown but not the image** -- rejected: the art is part of the published piece, and
  1.5 MB is immaterial beside the `mercurial-history.hg` bundle.

**Note on drift:** the repo copy and the LinkedIn copy can now diverge, since LinkedIn holds its
own rendering of the text. The repo copy is the source of truth; the published URL is the canonical
public location. House style for the article, set the same day: no em-dashes, and the close stays
flat (a closing rhetorical question was considered and rejected as selling past the close).

**Conditions to revisit:** if the article is substantially rewritten for another venue, decide then
whether the repo carries the original, the rewrite, or both.

**Affects:** `.gitignore` (exclusion removed), `PROJECT_CONTEXT.md` (root file map), `README.md`
(already links the published URL).

## 2026-09-14 -- PROPOSED (spiked, not built): a console window flashes for every external a Bash-tool call spawns; fix by attaching to a hidden console
**Status:** Proposed. AWAITING THE ARCHITECT. Nothing in `Bash/` changed.

**The defect (verified 2026-09-14, process-tree watcher + the architect's eyes):** Claude Code
(2.1.271/2.1.272) starts the Bash TOOL shell with NO console -- no conhost child, in a Windows
Terminal session and in a plain `cmd` window alike. (Statusline/hook `Bash.exe -c` spawns DO get a
`conhost 0x4`.) `ExecExternal` starts children with `UseShellExecute = false` and nothing else, so
Windows gives each console-subsystem child its OWN new console, with a visible window titled with
the exe path. Seen as "bash opens its own cmd window" on `bash --version`; confirmed on
`bash -c 'sleep 12'` (inner `bash.exe` had its own conhost and a non-zero `MainWindowHandle`).
Builtins never show it (`echo`, `whoami` stayed quiet). Git Bash did not show it [recalled,
unverified: the MSYS runtime handles a console-less parent itself].

**Spike** (`E:/Claude/csharpbash-findings-2026-09-14-hidden-console/`): a launcher starts the probe
with `DETACHED_PROCESS` (the Claude Code condition), stdio on an inheritable file. The probe applies
one variant, then runs `ping -n 3` watching for a new visible `ConsoleWindowClass` window and a
conhost under the child, then times 30 x `cmd /d /c exit 0` after 5 warm-ups (in-process Stopwatch,
Native AOT build, same toolchain recipe as `tools/publish-aot.cmd`). Positive control: the no-fix
variant DID detect the window, so the test can fail.

| variant | window | one-time | per spawn (median) |
|---|---|---|---|
| none (the defect) | YES | -- | 54.1 ms |
| `CreateNoWindow` on every child | no | -- | 15.5 ms |
| helper: `cmd /c pause` CreateNoWindow, `AttachConsole`, release | no | 22.6-26.9 ms (5 runs) | 4.00 ms |
| piggyback: first child CreateNoWindow, `AttachConsole` to IT | no | 22.0-22.3 ms (5 runs) INCLUDING that child's run | 4.05 ms |
| `AllocConsole` + hide | FLASHES (visible immediately after alloc) | 47 ms | 4.28 ms |
| reference: real inherited console | no | -- | 4.16 ms |

Also measured: redirected stdout survives attach (handle value unchanged, markers written before
and after reached the launcher); `AttachConsole` fails with error 6 until the new conhost is ready
(1.2-4.4 k spin iterations, ~17 ms), so a bounded retry is required; piggyback attached 6/6 times
even to `cmd /c exit`, the shortest child available -- a child evidently cannot finish before its
console is ready [inferred from 6 samples, not from documentation]. `AllocConsole` is hidden only
when the PARENT passed `SW_HIDE`, which we do not control. **`GetConsoleWindow() == 0` is NOT a
valid "no console" test**: the reference run had a (windowless) console and still returned 0.

**Proposal:** on the first external spawn when the shell has no console, spawn that child with
`CreateNoWindow` and `AttachConsole` to it (bounded retry, once, thread-safe -- pipeline stages
spawn concurrently); every later child inherits the hidden console at native cost. A lost race is
benign: that child is still hidden, and the next spawn tries again. Calls that spawn nothing pay
nothing. Break-even against per-child `CreateNoWindow` is the second spawn.

**Alternatives:** per-child `CreateNoWindow` (simplest; ~11 ms extra per spawn forever -- rejected
by the architect on cost, "it is so quick right now"); helper console (deterministic, ~5 ms dearer
on the first spawn, one extra process); `AllocConsole` (flashes -- rejected).

**Behaviour change to accept explicitly:** a child that reads the console directly (`CONIN$`: a
password prompt, `pause`) today gets a visible window someone could type into; under ANY hidden
fix it waits invisibly until killed. Claude Code already feeds stdin from `/dev/null`.

**Open before building:** the correct no-console test (`GetConsoleCP() == 0` is the candidate,
untested); interaction with the kill-on-close job object and with `timeout`; `ConsoleMux` and
`Console.*` probes after attach; a suite + battery run.

**Addendum, same session (appended, entry above unchanged):** the window checks above sampled only
AFTER setup. Re-run with a `SetWinEventHook(EVENT_OBJECT_SHOW)` hook for the WHOLE run, which
records any console window made visible however briefly. Positive controls fired: `alloc` 1 show
event during setup (the flash, caught although hidden microseconds later), `none` 36 (one per
child). piggyback 0 and helper 0 (2 runs each, setup included), `nowindow` 0. No initial flash.
**CONTESTED (same session):** the architect saw flashes during that run but could not attribute them
(the run also contained `alloc` and `none`, which flash by design). A desktop-wide hook (every
top-level window, any class, any process) over piggyback, helper, nowindow, alloc recorded ONE
show event, `alloc`'s. Awaiting a clean run of piggyback alone with the architect watching before
"no flash" is treated as settled.
**RESOLVED (same session, 18:31):** piggyback alone, 3 runs 5 s apart, nothing else running: the
desktop-wide hook recorded no window shown, all 3 attached, and the architect, watching, saw no
flash. The earlier sighting is attributed to the `alloc`/`none` controls in the mixed run.

## 2026-09-14 -- RATIFIED + BUILT: a console-less shell attaches to its first child's hidden console ("piggyback")
**Status:** Active. Ratifies and implements the PROPOSED entry of the same date. The architect:
"ok, yes, go - it seemed to be working", after being told a console-reading child (a password
prompt, `pause`) will now wait invisibly instead of showing a window.

**Implementation:** `HiddenConsole` in `Evaluator/ConsoleMux.cs`, two call sites in `ExecExternal`
(the ONLY process-start site in `Bash/`, verified by search). `NeedsHiding` = `GetConsoleCP() == 0`;
when true the child is started with `CreateNoWindow`, added to the kill-on-close job as before,
then `Adopt` attaches the shell to its console under a lock (a concurrent pipeline stage that
attached first is detected by re-testing `GetConsoleCP`), with a 100 ms spin budget, and sets code
page 65001 on the adopted console through `SetConsoleCP`/`SetConsoleOutputCP` -- the console
`Program.cs` gives a real one. Deliberately NOT `Console.OutputEncoding`: that setter resets .NET's
console writers, which `ConsoleMux` replaces by reflection.

**The no-console test, measured with controls:** `GetConsoleCP()` is 0 detached, 437 under a
windowless (CreateNoWindow) console, 65001 inherited; `GetConsoleWindow()` is 0 in all three.

**Verified:**
- Suite 50/50 from a console (the path unexercised), and 50/50 **detached** -- every case started
  with `DETACHED_PROCESS`, the Claude Code condition -- on both the Release build and the Native AOT
  build, with a desktop-wide show-event hook recording NO window. Positive control: the rev-80
  binary on `redirect_ext` detached flashed 2 `where.exe` console windows in the same hook.
- The attach really happens: 20 `cmd /c exit` spawns took 89.6-98.5 ms detached vs 92.4-100.5 ms
  with a real console (3 runs each) -- native cost, not the ~15 ms of a per-child console.
- Live: `dist/Bash.exe` replaced (rollback copy `dist/Bash.exe.rev80`, hg-ignored); a Bash-tool call
  in a Claude Code session ran `bash --version`, `bash -c 'sleep 5'`, `cmd.exe`, `where.exe` with no
  window recorded.
- Compat battery, rev-80 binary as reference vs the new build (both with a console): 237/237
  identical, 0 hung. The 11 "NEW PASS" it reports are artefacts of that reference; baseline untouched.

**Not verified:** the GUI-child case (a program with no console makes `Adopt` spin its 100 ms
budget each time until a console child comes along -- unmeasured); `timeout` killing a child on the
adopted console; Ctrl+C semantics (nothing can send a console event to a hidden console).

**Found on the way, not fixed:** `date +%3N` / `+%6N` ignore the width and print all nine digits
(GNU prints 3 / 6). The compat battery has no valid Git reference on this machine while Git's
`bash.exe` is renamed to `_bash.exe`: the launcher then fails every probe, and `usr/bin/_bash.exe`
alone lacks `/usr/bin` on PATH (different `wc`/`sed`), so it was run old-binary-vs-new instead.

**Revisit if:** Claude Code starts the Bash tool with a console (the path goes dormant by itself);
a console-reading child under Claude Code turns out to be common; or the GUI-child spin is measured
as noticeable.

## 2026-10-03 -- FOUND: under Claude Code, the first external in any command containing `<` loses its output silently
**Status:** Found and attributed; fix PROPOSED, AWAITING THE ARCHITECT. Nothing in `Bash/` changed.

**Defect.** Exit status 0, no output, and the child really did run (its side-effect file exists).
Reported by session msp430-a2 as "`hg status` intermittently empty". Reproduced twice by hand in
this session, then attributed by a spike. Evidence, launcher source and counts are in
`E:/Claude/csharpbash-findings-2026-10-03-empty-output/FINDINGS.md`.

**Trigger [verified, live, both polarities]:** Claude Code appends ` < /dev/null` to its
`eval '<cmd>'` only when the command text contains no `<`. Any `<` at all removes it: a
redirect, a heredoc, `<<<`, `<( )`, even a `<` inside a quoted string. Live Bash tool on rev 81:
commands containing `<` lost the first external's output 11/11; without `<`, 12/12 were delivered.

**Mechanism.** The code path is verified; the .NET handle behaviour is recalled, not read.
- An external with fully inherited stdio sets no `Redirect*` (`Evaluator.cs:1052-1103`), so .NET
  does not pass the shell's std handles.
- The console-less shell starts the child `CreateNoWindow` (`Evaluator.cs:1107-1108`). Its handles
  therefore point at its own new hidden console.
- Rev 81 then adopts that console (`Evaluator.cs:1140`), so later children inherit correctly and
  only the FIRST external is lost.

**This is NOT a rev-81 regression.** Rev 80 loses every such external (second external 0/50 vs
rev 81's 50/50), into a visible window. The hole has existed since C#Bash became Claude Code's shell
(P5, 2026-09-04); rev 81 narrowed it.

**Second hazard:** in the same condition, a first external that reads stdin reads the hidden
console and waits invisibly until killed.

**Proposed fix (Claude's position, PROVISIONAL):** in the console-less case only, and only when
all three streams would be inherited, start the child with `CreateProcess` +
`STARTF_USESTDHANDLES`, passing the shell's own three std handles. Keep `CREATE_NO_WINDOW` and the
adoption.
- Exact bash semantics: the child gets precisely the shell's handles, with no pumping.
- The stdin hang goes too [inferred]. Added latency should be zero [unmeasured].
- The change is local to one branch of `ExecExternal`.
- **Falsifier:** if that branch cannot be kept local (job object, `Exited`, wait and `timeout` all
  hang off `Process`), or if it measurably costs startup latency.

**Alternatives:**
- **Helper-first console:** acquire the hidden console before the first child. Smallest code
  change, but ~5 ms dearer on the first spawn (measured 2026-09-14, other conditions). That is
  per external-spawning call, so it is a cost against the speed floor.
- **Pump the first child's stdio:** works, but changes the child's handle types and adds threads.
- **Redirect stdin only:** stdout and stderr then pass correctly, but a pump over-reads a shared
  stdin that later commands should see.

**Before building:** measure what stdin Claude Code hands the shell when it omits `< /dev/null`;
gate the fix on the launcher matrix above (first external 50/50 delivered, both stdout modes); add
a suite case that runs detached with no `< /dev/null`.

## 2026-10-03 -- FOUND: CR is stripped by most line-reading tools, and a lone CR splits a line
**Status:** Found; fix direction = byte-transparent, CR is data. This is Claude's position, applying
the principle of the 2026-09-12 encoding entry ("the shell never has to decide whether its data is
text"; "losing data to match a limitation is not worth doing"). The architect has said Git Bash is
NOT a target. Not built.

**Verified, dist/Bash.exe:**
- On `printf 'a\r\nb\r\nc\n'`, these drop both CRs: awk, head, tail, grep, `read`, `mapfile`, cut,
  sort, and piped head and awk. `grep -c $'\r'` reads 0.
- sed, cat and `$(cat)` keep them.
- `printf 'a\rb\n' | head -n1` emits `a\n`, and `grep -c ""` counts 2 lines: a lone CR is treated
  as a line terminator. The cause is consistent with .NET `ReadLine()` semantics; there are 7 call
  sites in `Evaluator/` [cause inferred].

**Real damage, reported by msp430-a2:** an awk edit silently rewrote a mixed-EOL decision log
(590 CRLF lines) as all-LF, and the `grep -c $'\r'` check meant to catch it read 0 before and
after.

**Git Bash (4.4.23, PortableGit) for the record, not as a target:** its gawk and sed strip CR;
its head, tail, grep, read, cut and sort keep it. It is inconsistent.

**Accepted consequence:** `nativecmd | awk '{print $NF}'` will carry a trailing `\r` from CRLF
output. That is visible, not silent. Revisit if that idiom proves habitual.

**Addendum to the 2026-10-03 empty-output entry (same session; entry above unchanged):** msp430-a2
corroborated it. Every one of its empty-output cases contained a `<`: heredocs, and a `tr … < file`
placed before `hg diff --stat`. The routine case is the commit attribution trailer itself. A
`Co-Authored-By: … <noreply@anthropic.com>` inside `-m "…"` puts a `<` in the command text, so
`hg status && hg commit -m "…<noreply@…>"` loses the status output on every attributed commit.
Make that the regression test for the fix.

## 2026-10-03 -- The goal order: C#Bash for itself, then no PowerShell for non-Windows work, then a seamlessly faster Claude
**Status:** Active. Stated by the architect. It replaces the purpose line in `PROJECT_CONTEXT.md`
("drop-in replacement for msys2 bash"). The 2026-09-04 goal entry ("C#Bash as Claude Code's
Windows shell") is not superseded; it becomes goal 3 of three.

**The architect's words:** "C#Bash is primarily for itself, on windows, secondarily to eliminate
powershell for _everything_ not windows dependent (system tools, etc) and thirdly to make claude
(seamlessly) faster. The latter goal is already achieved and should be maintained at all costs."
And: "I'm not interested in git Bash as a target."

**Clarified the same day:**
- **Goal 3 against goals 1 and 2: "depends on circumstance".** Nothing takes precedence
  automatically. A change made for goal 1 or 2 that costs goal 3 (latency, a window, a silent
  failure) is surfaced with its numbers for a case-by-case call. It is never traded silently.
- **Where goal 2 stops: "inside if the _command_ requires powershell for some reason, not if
  the output is windows specific."** Claude's reading, provisional until the architect confirms
  it: tools that today force a detour into PowerShell (`df`, `free`, `uptime` and the like) are in
  scope; things whose output is inherently Windows (services, the registry, the event log) are
  out.
- **Git Bash is not a target.** The compat battery stays, as a ratchet and as a corpus of
  Claude's habits. A difference from Git Bash is no longer a defect by definition. The first
  instance is CR handling (entry above): Git's gawk and sed strip CR, and C#Bash will not.

**Claude's recommendations, NOT ratified:**
- Park the steroids investigation, since further speed is not a goal. Its findings are in
  `E:/Claude/csharpbash-findings-2026-09-13/`. Revisit if goal-1 or goal-2 work threatens goal 3.
- Scope goal 2 from evidence: scan the session transcripts for PowerShell tool calls and sort
  each into "Windows-dependent" or "C#Bash could have done it".

## 2026-10-03 -- RATIFIED + BUILT: a console-less shell starts an all-inherited child on its own std handles
**Status:** Active. Ratifies and implements the empty-output proposal above. The architect said
"1/ yes" to the explicit-handle direction.

**Implementation:** `HiddenConsole.StartInherited` in `Evaluator/ConsoleMux.cs`.
- `CreateProcessW` with `STARTF_USESTDHANDLES`, passing the shell's own three std handles as
  inheritable duplicates (stderr gets stdout under `2>&1`).
- Flags: `CREATE_NO_WINDOW | CREATE_SUSPENDED | CREATE_UNICODE_ENVIRONMENT`.
- The command line and environment block are built the way .NET builds them: PasteArguments
  quoting, and a block sorted OrdinalIgnoreCase.
- While the child is suspended, `Process.GetProcessById` and its `Handle` are taken, so
  `ExitCode` survives however fast the child exits.
- Failure throws `Win32Exception`, so the caller's "command not found" and "Permission denied"
  messages are unchanged.
- `ExecExternal` calls it only when `NeedsHiding` and no stream is redirected. Every other spawn
  is unchanged; with a console the path cannot be reached.

**Verified:**
- **Launcher matrix** (DETACHED_PROCESS, no `< /dev/null`, N=50 per cell):
  - The first external was delivered 50/50 for: a probe with pipe stdout and with file stdout, a
    C#Bash child, `hg status`, and `2>&1`.
  - `exit 7` gave 7 (20/20); command not found gave 127 (20/20).
  - Control, same trials on rev 81: 0/50 in every output cell.
  - Medians match rev 81 at whole-process resolution: 69.0/68.9, 37.6/37.6, 283.6/283.3 and
    38.9/38.0 ms. Any sub-millisecond cost is NOT measured.
- **No windows:** a probe child reported `GetConsoleWindow()` = 0, not visible, on 10/10 first and
  10/10 second externals. Control: rev 80 showed a visible window 20/20.
- **Suite:**
  - New case `inherit_ext`, plus two new runner options: `-Detached` (DETACHED_PROCESS, stdin
    NUL, stdout to a file, stderr NUL: Claude Code's launch condition) and `-Interpreter <exe>`.
  - Results: 51/51 with a console and 51/51 detached, for both the Release and the AOT build.
  - Control: rev 81 detached fails `inherit_ext` only. It passes the other 50, so the suite had no
    coverage of this defect before.

**An instrument bug the control caught:** the parameter was first named `-Exe`. PowerShell names
are case-insensitive, so it WAS the runner's `$exe`, and the runner overwrote it. The first
"rev 81 detached" run silently tested the Release build and passed. It is now `-Interpreter`, and
the runner prints the interpreter and build it is testing.

**Found on the way, NOT fixed (needs a ruling):** with a console, an external's `2>&1` does not
reach the shell's stdout when that stdout is a pipe or file and is not otherwise redirected.
- The comment in `ExecExternal` ("both inherit the console -- nothing to redirect") holds only
  when stdout IS the console.
- Verified: `echo OUT; "$BASH" -c 'echo EXT-ERR 1>&2' 2>&1; echo after`, run with stdout captured
  and stderr discarded. C#Bash gives OUT, after; Git Bash gives OUT, EXT-ERR, after.
- Under Claude Code this applies to every external after the first, because the shell has a
  console once it adopts one. It mostly does not show there, since the tool reports both streams.
- The same mechanism would fix it: `StartInherited` without `CREATE_NO_WINDOW`, for an inherited
  stdout under `2>&1`.

**Not verified:** what stdin Claude Code hands the shell when it omits `< /dev/null`; GUI children;
Ctrl+C.

**Deployed and verified live (same day, appended; the entry above is unchanged).** Rev 82 was
published AOT (`1.0.0+hg.e8446ad8170d+ 82+`). The `+` is git's README and `.gitignore` changes,
still uncommitted in hg; they are not code.
- The shipped binary was re-gated: first external delivered 50/50 for the probe with pipe stdout,
  with file stdout, and for `hg status`; no visible window 50/50; suite 51/51 detached and with a
  console.
- Deployed to `dist/Bash.exe`; rollback copy `dist/Bash.exe.rev81`.
- Live, from a Claude Code Bash tool on the new binary: `hg status` as the first external of a
  command containing `<` delivered its output (11/11 lost before), and so did `whoami.exe` ahead
  of a heredoc.

**Correction to the proposal's "the stdin hang goes too [inferred]": it does not, and it is not
C#Bash's to fix.** Measured live: in a command containing `<`, `timeout 5 sort.exe` blocked the
full 5 s (rc 124).
- When Claude Code omits `< /dev/null`, the shell's stdin is an open pipe that never reaches EOF.
- A child reading inherited stdin now waits on that pipe instead of the hidden console. Any bash
  under that parent behaves the same.
- Workaround: in a command that needs a `<` AND an external that reads stdin, give the external
  its own `< /dev/null`.

**Independent confirmation (same day, appended).** msp430-a2 checked build
`1.0.0+hg.e8446ad8170d+ 82+` on its own repository. In each of its three original failing cases,
`hg status` was the first external and its output was delivered: a heredoc later in the call, the
`<noreply@anthropic.com>` trailer later in the call, and a `$(tr -cd '\r' < file | wc -c)` later
in the call. Its control call, with no `<`, printed the same status line. It found no
counter-example.

## 2026-10-03 -- RATIFIED + BUILT: `ext 2>&1` with an inherited stdout puts the child's stderr on the shell's stdout
**Status:** Active. The architect said "1/ Yes" to fixing the misroute found in the previous entry.

**Defect.** An external under `2>&1`, whose stdout was inherited and not otherwise redirected,
kept its stderr on the shell's stderr. That was correct only when the shell's stdout IS the
console. With stdout a pipe or a file, the merge silently did not happen, console or not. Under
Claude Code this hit every external after the first: the shell has a console once it adopts one.

**Fix.** The same mechanism as the console-less fix. `ExecExternal` now uses
`HiddenConsole.StartInherited` for any all-inherited child that is console-less OR under
`2>&1`, and `CREATE_NO_WINDOW` is passed only when console-less.
- With a console, the creation flags are exactly the ones Process.Start uses, so the child
  shares the console as before [inferred from the flags; not observed on a TTY].
- What changes is that the child's stderr handle is the shell's stdout.

**Verified:**
- `inherit_ext` gained a `2>&1` line. The rev 82 control fails exactly that line, both with a
  console and `-Detached`.
- The new build passes 51/51 in both modes, Release and AOT.
- The launcher's first-external cells still deliver 50/50, with no visible window 50/50.

**Not verified:** a real TTY. This dev sandbox has none.

**Two process rulings, same day:**
- **hg/git sync happens ONLY on a push to GitHub** (the architect: "No, that _only_ happens on push
  to github"). Git's README link, `.gitignore` change and the article png therefore stay
  uncommitted in hg until then, and every build stamp shows `+` meanwhile. The `+` alone does not
  mean the code was uncommitted.
- **Steroids:** the architect did not recognise the question ("what now??"). Recorded as NOT
  ACTIVE, NO RULING. Claude stops asking. Its findings stay in
  `E:/Claude/csharpbash-findings-2026-09-13/` and memory.

**Deployed (same day, appended):** the rev 84 AOT build (`1.0.0+hg.68312ac6a08a+ 84+`) was re-gated as
shipped: suite 51/51 with a console and detached; first external 50/50; no visible window 50/50.
It is in `dist/Bash.exe`, with rollback copy `dist/Bash.exe.rev82`. Live in a Claude Code Bash
tool, `hg log` was the first external of a command containing `<`, and `ext 2>&1` both captured
and direct reached stdout.

## 2026-10-03 -- RULE: where C#Bash's behaviour is open, match what Claude assumes (GNU/Linux), not what Git for Windows happens to do
**Status:** Active. Ratified by the architect ("Yes") after he asked: "Why are we doing anything
different to what claude assumes?"

**The rule.** When a behaviour is undecided, the reference is GNU bash plus GNU coreutils on
Linux, because that is what Claude's habits were formed on and what its idioms presuppose.
Git for Windows' quirks are not the reference: its gawk and sed strip CR, a text-mode build
choice Claude neither knows nor relies on. This is "Git Bash is not a target" (the goal-order
entry) restated as a tie-breaker. The live reference is WSL's Ubuntu (`wsl.exe -e bash …`), which
is available on this machine.

**Correction, superseding a claim in the CR entry earlier today.** That entry's "Accepted
consequence: `nativecmd | awk '{print $NF}'` will carry a trailing `\r` … Revisit if that idiom
proves habitual" was framed backwards. Keeping the `\r` is what Claude assumes, so it is no cost.
The evidence is in this session: msp430-a2's report predicted "GNU: 2" (CR kept) for every tool,
including awk, and its `grep -c $'\r'` safety check presupposed it.

## 2026-10-03 -- RATIFIED + BUILT: CR is data in every line-reading tool; no BOM sniffing; child streams byte-transparent
**Status:** Active. Ratified by the architect ("Yes": build it byte-transparent across every tool).

**What changed** (a new `LfReader`, plus `ShellEncoding.ReadLine`/`Lines`/`LineReaderFor`):
- Lines end at LF ONLY: a CR stays in the line, and a lone CR ends nothing. This applies to
  head, tail, grep, awk, sort, cut, uniq, tac, rev, nl, paste, comm, split, factor, `date -f`,
  `grep -f`, xargs (default mode and `-I`; CR is not a blank to GNU xargs), `read` and
  `mapfile -t`, which each stripped a trailing CR explicitly, and the history file.
- No BOM sniffing. `StreamReader(path|stream, enc)` defaults to `detectEncodingFromByteOrderMarks`,
  so the text tools decoded a file starting `FF FE` as UTF-16 (garbage) and dropped a UTF-8 BOM.
  Verified against GNU, which passes both through.
- An external's redirected streams use `ShellEncoding`. .NET's default turned 0xFF into U+FFFD
  inside `$(ext)`.
- `ext 1>&2` is copied as it arrives, and to the CALLING thread's stderr. It used to go line by
  line, dropping CR, and to the copy thread's own stderr: the real one, so `{ ext 1>&2; } 2>&1`
  escaped a `$( )` capture. Both verified against GNU.

**`LfReader`, and why it took three measured iterations.** Speed is the goal-3 floor:
1. **Char-by-char reading** through the console multiplexer was correct but cost +12.7 % (+102 ms)
   on six tools over 200 k-line files, about 85 ns a line.
2. **An `LfReader` wrapped around a `StreamReader`** was +48.8 %. Asked for an 8 K block, a
   StreamReader keeps reading until the block is full or a read comes back short, so a pipeline
   stage waited on its producer instead of overlapping it (`head | tail` 132 -> 295 ms; JIT and
   AOT alike). Over a file it was also ~30 % slower than `StreamReader.ReadLine`, measured
   in-process.
3. **An `LfReader` over the raw stream**, ONE read and one decode per fill: lines bench -0.3 %,
   pipeline bench +1.3 %, startup 12.1 vs 12.1 ms, every per-command timing within ±3 ms of
   rev 84. In-process it is at least as fast as `StreamReader.ReadLine` (26.3 vs 26.8 ms keeping
   200 k lines).

**Shared stdin.** Every stdin the shell installs is one `LfReader` (pipeline stages, `< file`,
`<&n`, a redirected process stdin), so all the commands reading it share one buffer and none
reads past where another stopped. Verified: `{ head -n 1; sed -n p; } < f` and
`{ grep -m1 a; sed -n p; } < f` match GNU. A reader that is not an `LfReader` (a here-document,
an interactive console) is read char by char.

**Verified:**
- `tests/cases/crlf.sh`: 42 probes, expected output GENERATED BY GNU via WSL. Regenerate with
  `wsl.exe -e bash tests/cases/crlf.sh > tests/expected/crlf.out`, run from `tests/`. The new
  build matches byte for byte; rev 84 differs on 33.
- Suite 52/52 with a console and `-Detached`.
- Battery 237/237 identical to rev 84: none of Claude's habitual probes changed.

**Found on the way, NOT fixed (each pre-existing, verified on rev 84 unless marked):**
1. **Pipeline stages race on shared shell state.** Stages are threads over ONE environment, so a
   `$( )` in one stage can take another stage's positional parameters, and its output.
   - `scratchpad/cr/amplify.sh` (stage 1 runs 3000 `$( )`, stage 2 calls a function 3000 times)
     printed `LOST: got ''`, or lost the "stage2 done" line entirely, rc 0, on rev 84 and rev 80.
   - In bash, each stage is a subshell. Silent; architectural; needs a design discussion.
2. **Text then bytes on one stdin.** `{ read -r x; cat; } < f` prints nothing; GNU prints the rest.
   The text reader buffered the file, and `cat` reads the raw stream beneath it. Silent; it hits
   the skip-a-header idiom.
3. **No stateful decoder** [inferred from the code; not demonstrated]. `ShellEncoding` does not
   override `GetDecoder`, so a UTF-8 character split across two reads becomes escaped bytes
   inside the shell (wrong `${#x}` and `cut -c` at that boundary). It is written back as the same
   bytes.
4. **fold** treats `\r` as a column, where GNU resets the column.
5. **`tee /dev/null`** fails with "No such file or directory".

**Left as is, on purpose:** the lexer treats `\r` as whitespace in script text, and the shebang
read strips CR. A CRLF script still runs, where Linux bash would fail on it. That is script
loading, not data: no tool changes bytes there. Revisit if the architect wants Linux-strict.

**Deployed (same day, appended):** the rev 86 AOT build (`1.0.0+hg.b0bf88907cc5+ 86+`) was re-gated as
shipped: suite 52/52 with a console and detached; `crlf.sh` byte-identical to GNU;
first external 50/50; no visible window 50/50. It is in `dist/Bash.exe`, with rollback copy
`dist/Bash.exe.rev84`. Live in a Claude Code Bash tool, msp430-a2's original repro reads 2 CRs
for awk, head, tail, grep, `read` and `grep -c $'\r'` (it read 0).

**Independent confirmation (same day, appended).** msp430-a2 checked build
`1.0.0+hg.b0bf88907cc5+ 86+` on its real data, with no counter-example:
- Its original repro reads 2 everywhere; a lone CR survives `head -n1`.
- A committed copy of its mixed-EOL decision log (591 CRLF + 61 LF lines) passes through
  `awk '{print}'`, `head`, `grep ''` and `sed -n p` byte-identical (cmp, with a positive control
  that cmp flags a one-byte change).
- The exact awk insert that corrupted the log this morning now adds one row and touches no line
  ending; removing the row round-trips byte-identical.

## 2026-10-03 -- RATIFIED + BUILT: commands sharing one stdin; `head -c` streams exactly N bytes
**Status:** Active. The architect said "Yes" to fixing `{ read; cat; } < f` first.

**Defects:** all silent or hanging, all present on rev 86, all verified against GNU (WSL).
1. **A byte builtin after a text reader on the same stdin got nothing.** `{ read -r header; cat;
   } < f`, `{ head -n 1; cat; }`, `{ read; od; }`, `{ read; wc -c; }` and a pipe into
   `{ read; cat; }` all lost the rest. The text reader's buffer had read ahead, and byte builtins
   read the raw stream beneath it. `CurrentRawStdin`'s own comment said "read one or the other,
   never both".
2. **`yes | head -c 5` HUNG.** It printed nothing and was killed by `timeout` after 5 s; GNU takes
   6 ms. `head -c N` read ALL of its input before taking N bytes, which also left nothing for a
   following `read` on a shared stdin.

**Fix:**
- `CurrentRawStdin` hands a byte builtin the bytes this stdin's `LfReader` read ahead and did not
  consume (`LfReader.TakeBuffered`, re-encoded through `ShellEncoding`, which round-trips
  exactly), then the raw stream (`PrefixedStream`).
- `head -c N` streams exactly N bytes and stops. Only `head -c -N` ("all but the last N") still
  reads everything, because it has to.
- Hazard: `CurrentRawStdin` now has a side effect, taking the read-ahead, so a caller must call it
  ONCE per use.

**Verified:**
- New case `stdin_shared.sh`: 9 probes, expected output generated by GNU via WSL. The new build
  matches byte for byte; rev 86 fails all 9.
- Suite 53/53 with a console and `-Detached`; battery 237/237 identical to rev 86.
- An instrument error caught on the way: a `bash -c` probe resolved `bash` through PATH to the
  OLD `dist/Bash.exe`, so the first hang check "failed" on the fixed build. Probes now use `$BASH`.

**A divergence kept on purpose:** `producer | { head -n 1; sed -n p; }` gives sed the rest here.
GNU's head reads ahead on a pipe and swallows it, so GNU prints only the first line. Emulating
that would be emulating data loss, the same reasoning as keeping NULs (2026-09-12).

**Deployed (same day, appended):** the rev 88 AOT build (`1.0.0+hg.80ac45ee3f33+ 88+`) passes 53/53 with a
console and detached. Speed is level with rev 86: lines 812 vs 822 ms, pipeline 251 vs 252, startup
12.4 vs 12.0. It is in `dist/Bash.exe`, with rollback copy `dist/Bash.exe.rev86`. Live in a Claude Code
Bash tool: `{ read -r h; cat; } < f` returns the rows, and `yes | head -c 5` ends in 15 ms.

## 2026-10-03 -- SPIKE (measured) + PROPOSAL: pipeline stages on isolated shell state
**Status:** PROPOSED. AWAITING THE ARCHITECT; the design is his. The spike was greenlit ("Yes")
as measurement only, and nothing in `Bash/` changed. Spike source:
`E:/Claude/csharpbash-findings-2026-10-03-empty-output/stage-isolation-clonecost.csx`.

**The question, and why it is hard to reverse.** Should every pipeline stage run on its own copy
of shell state, as each is a subshell in bash? It reopens P2's pipeline model (2026-09-04), whose
"Known limits" recorded "stages share variables and cwd (bash: subshells)". It was accepted then;
the race it causes was not foreseen.

**Verified facts:**
- **Stages share one `ShellEnvironment` and the process cwd.** `echo x | read v` leaves `v=x`,
  and a `| while read` counter survives the pipe. GNU (WSL) leaves both unset.
- **`( )` and `$( )` "isolate" by snapshot-and-restore of that SAME environment**
  (`Evaluator.cs:1533-1537`, `WordExpander.cs:790`). That is sound on one thread and unsound
  across stage threads: stage 1's `$( )` restores its snapshot over stage 2's pushed `$1`.
  Reproduced as `LOST: got ''` and as a stage's output going missing, rc 0, on rev 80 and rev 84
  (`pipeline-stage-race-amplify.sh`).
- **Cwd is process-global.** A `cd` in any stage, including inside `( )` in a stage, moves every
  stage. `( cd sub && make ) | tee log` can open `log` in `sub` [inferred from the mechanism;
  not reproduced].

**Measured** (in-process, best of 5, null loop subtracted). Environment: Claude Code's real
58 KB snapshot sourced (72 functions, 95 exported) plus 100 variables, an array and an assoc.
Gated: populated environment, and a pipe that really piped.

| operation | µs |
|---|---:|
| deep copy of all variable state (`TakeSnapshot`) | 2.03 |
| `new Evaluator` over an existing environment | 0.31 |
| `new ShellEnvironment()` (imports the process env and probes PATH) | 1,428 |
| a builtin pipeline today, `echo x \| read v` | 130 |
| a pipeline with an external, `echo x \| cmd /c rem` | 4,376 |

So a per-stage clone costs ~2.3 µs: about 1.8 % of a builtin pipeline per stage, and noise
against any pipeline with an external. **One constraint:** a clone must NOT go through the
`ShellEnvironment` constructor (1.4 ms); it needs a dedicated copy path. Not measured: copying
functions, aliases, options and traps (dictionaries of references) [unmeasured estimate: ~1 µs].

**Options:**
- **(a) Every stage gets a clone** (bash's default). Variables, positionals, functions, options
  and traps are isolated; the parent's state is unchanged after the pipeline.
- **(b) All stages but the last** (bash's `shopt -s lastpipe`): `cmd | read v` would work, but that
  is not default bash.
- **(c) Keep sharing, add locks:** fixes the race, keeps the leak.
- **(d) Status quo:** silent race.

**Claude's recommendation (provisional): (a).**
- **Criteria:** bash semantics, which Claude assumes (rule of 2026-10-03; Claude knows the
  `| while read` subshell gotcha and writes `done < <(cmd)` for it); no silent race; within the
  speed floor.
- (b) fails the first criterion, (c) the first, (d) the second. (a) meets all three on the
  numbers above.
- **What would change my mind:** a probe or real session that depends on the leak, or a measured
  per-stage cost above ~5 % once functions, options and traps are included.

**Cwd is a SEPARATE question, and needs its own ruling.** A clone cannot isolate a process-global
cwd. The fix is a logical cwd per evaluator: externals get `WorkingDirectory`, relative paths
resolve against it, and `( )`/`$( )` stop calling `SetCurrentDirectory`. The change is mechanical
but wide (every path resolution). Claude's position: do (a) first, then the logical cwd as its
own step. Until then, cwd sharing stays a documented limit.

**Behaviour change to accept under (a):** a script that relies on the leak changes. Example:
`n=0; cmd | while read; do n=$((n+1)); done; echo $n` prints 0, as in bash.

## 2026-10-03 -- FOUND, not fixed: a function in Claude Code's git snapshot fails to parse (silently dropped)
**Status:** Found; repro saved; not isolated to one construct.
- Sourcing the 58 KB Claude Code snapshot (Git-Bash-generated, 2026-09-07) defines 72 functions in
  C#Bash and 73 in Git Bash. `__git_ps1_show_upstream` is missing.
- Git Bash's own `declare -f` of it (136 lines,
  `E:/Claude/csharpbash-findings-2026-10-03-empty-output/parse-defect-git_ps1_show_upstream.sh`),
  sourced in C#Bash, fails with `Expected 'fi' but got '' at 137:1`.
- Sourcing the whole snapshot printed that error in one harness and nothing in another: the
  snapshot itself is sourced with `2>/dev/null` by Claude Code's wrapper.
- Bisecting by prefix is useless (every cut leaves a construct open); it needs bisection by
  statement.
- Impact today: only git-prompt completion, which a non-interactive tool call never uses. But a
  parser that silently drops a function on valid bash is a goal-1 defect.

## 2026-10-03 -- RATIFIED + BUILT, NOT DEPLOYED: every pipeline stage runs on its own interpreter (a subshell)
**Status:** Built and committed. Deployment is AWAITING THE ARCHITECT, because Claude's stated
falsifier fired (below). The architect: "Ok, your recommendation; go", for option (a) of the
SPIKE entry above.

**Implementation:**
- `Evaluator(Evaluator parent)`, a private constructor, builds a stage interpreter.
  `ExecPipelineThreaded` builds one per stage, before any stage runs, and runs the stage on it.
- **What the stage gets:** `ShellEnvironment.CloneForSubshell()`, a private copy constructor
  that bypasses the 1.4 ms process-environment import, plus `ShellOptions.Clone()` (memberwise,
  plus its shopt set), functions, aliases, traps, fds 3+, the PATH hash, the FUNCNAME and source
  stacks, the errexit-suppression depth, and the `pushd` stack.
- **Fresh per stage**, as in a bash subshell: jobs, getopts position, `exec` redirects.
- **Interrupts:** a stage's `CheckInterrupt` also honours its parent's flag, so Ctrl+C and
  `timeout` still end a pipeline loop.
- **Not covered:** the cwd. It is still process-wide; that is the separate step.

**Verified:**
- New case `pipeline_isolation.sh`: 16 lines, expected output generated by GNU via WSL. The new
  build matches byte for byte; rev 88 differs on 8 (`read` in a stage, the `| while read`
  counter, functions, `export`, arrays, positionals, sibling isolation).
- **Race (`pipeline-stage-race-amplify.sh`, 20 runs x 3000 calls):** new 0 LOST, "stage2 done"
  20/20. **Control rev 88: 5 LOST, and stage 2's last output MISSING in 14 of 20 runs.** The old
  defect was worse than the first sample suggested.
- Suite 54/54 with a console and `-Detached`; battery 237/237 identical to rev 88 (no probe
  relied on the leak).

**Cost, measured. The SPIKE's estimate was WRONG.** The spike said ~2.3 µs a stage (1.8 %). It
summed the cost of parts, not of a working stage.
- **In-process A/B** (rev 90 built in a scratch clone, same harness, alternating, twice):
  - 2-stage builtin pipeline: 136/134 -> 146/144 µs, about +10 µs (+7.5 %).
  - 3-stage with a function call: 199/196 -> 250/237 µs, about +45 µs (+22 %).
- **Constructor parts:** environment clone 1.42 µs, options 0.12, evaluator 0.38; the function
  and table copies and harness reflection make up the rest of ~5-6 µs. The extra ~30 µs in the
  3-stage case is NOT attributed [inferred: allocation and GC from per-stage copies].
- **Whole process, AOT:**
  - A loop of 4000 pipelines: 538 -> 602 ms (+11.8 %, ~16 µs a pipeline).
  - Pipeline bench +1.5 %; line tools -4.3 % (noise); coreutils +3.5 % (+2 ms, at the noise
    floor); startup 12.9 vs 12.7 ms.

**Why deployment waits.** Claude's recommendation named its own falsifier: "a measured per-stage
cost above ~5 %". A pipeline-heavy loop pays 11.8 %. Under the architect's goal-3 ruling ("depends
on circumstance": surface each collision with numbers, never trade silently), that is his call.
- **Claude's position:** deploy. The defect it removes is silent: output lost in 14/20 stress
  runs, rc 0. The cost lands only on scripts that run thousands of pipelines in a loop; a tool
  call's latency is unchanged.
- **A cheaper design if the cost matters:** copy-on-write variable frames, so a stage that never
  writes copies nothing [unmeasured].

**The README performance table, re-measured (same day, appended).** Asked for by the architect
before deciding on deployment.
- Method: `compare3.sh`'s, wall clock via `time` under Git Bash, with a rev 91 column added.
- Final rev 88 vs rev 91 figures: best of 9, the two builds interleaved round by round (the
  earlier best-of-3 runs disagreed by up to 12 % on the small rows).
- Git and WSL: best of 3. WSL is now `wsl.exe -e bash`, because `System32\bash.exe`, which the
  published table used, no longer exists here.
- All outputs agree: rev91 = rev88 = Git = WSL on every row.

| benchmark | rev 88 | rev 91 | change | Git Bash | WSL |
|---|---:|---:|---:|---:|---:|
| loop | 0.174 | 0.171 | -1.7 % | 1.557 | 0.443 |
| arith | 0.257 | 0.255 | -0.8 % | 2.086 | 0.633 |
| func | 0.228 | 0.225 | -1.3 % | 2.302 | 0.593 |
| loop_big | 1.363 | 1.349 | -1.0 % | 18.947 | 3.512 |
| coreutils | 0.078 | 0.080 | +2.6 % | 21.438 | 0.885 |
| pipeline | 0.277 | 0.278 | +0.4 % | 0.411 | 0.137 |
| find | 0.043 | 0.043 | +0.0 % | 0.174 | 0.259 |
| startup | 0.035 | 0.035 | +0.0 % | 0.029 | 0.111 |

- **No published row moves beyond noise.** The one real effect is `coreutils` at +2 ms: its 300
  `$(echo | wc)` pipelines at ~7 µs a stage.
- The isolation cost shows only in a loop of thousands of pipelines (+11.8 % on 4000), which no
  published row is.
- Today's startup (0.035) is above the published 0.030 for BOTH builds. That is machine state,
  not this change.
- Two runs were discarded, and they are worth knowing about. In the first, the scratch harness's
  `#!/usr/bin/env bash` resolved `bash` to C#Bash (Git's `bash.exe` is renamed here), so C#Bash
  ran the harness and timed nothing. The second was best-of-3 noise.

## 2026-10-03 -- FOUND, not fixed: `arr[$var]=value` is not recognised as an assignment
**Status:** Found (verified against GNU on rev 88); not fixed.
- `declare -A st; k=old; st[$k]=5` prints `bash: st[old]=5: command not found` (rc 127) and
  leaves the element unset. So does `for k2 in a b; do st[$k2]=7; done`, and on an indexed array
  `ix[$i]=x`.
- A literal index (`st[new]=6`) and an arithmetic one (`ix[i+1]=y`) work.
- GNU sets all of them.
- Loud, not silent, but `map[$key]=$val` and `count[$w]=…` are common idioms, so it breaks
  ordinary scripts. Found when a scratch harness ran under C#Bash by accident.

**Deployed (same day, appended):** the architect said "Yes, deploy", having seen the re-measured
table above.
- **What is in `dist/Bash.exe`:** a build of rev 91's code, from committed rev 92 (docs only on
  top; stamp `1.0.0+hg.027164439941+ 92+`). Suite 54/54 with a console and detached. Rollback
  copy `dist/Bash.exe.rev88`.
- **Live in a Claude Code Bash tool:** `echo x | read v` leaves `v` unset, and a `| while read`
  counter reads 0, as in bash.
- **The README performance table now carries the re-measured figures.** The C#Bash column is the
  deployed build (best of 9); Git and WSL are best of 3. The prose ratios are updated (8-14x Git;
  startup within 6 ms of Git), with a caveat that Git Bash's own times vary between runs.
  `README.md` stays UNCOMMITTED in hg, under the sync-on-push ruling: it already carries git's
  article-link change, and both go out with the next GitHub push.
- **The companion article's copy of the table is NOT changed.** It is a dated publication
  (2026-09-13) that matches what LinkedIn shows; the architect's call is pending.
- **Why `System32\bash.exe` is missing, and the harness fix.** The architect moved it because it
  was pre-empting C#Bash: it ranks first on a default Windows PATH (see #94077 in memory). Git's
  `bash.exe` is likewise renamed `_bash.exe`.
  - `tests/bench/compare3.sh` now reaches WSL through `wsl.exe -e bash`, with `bash.exe` only as a
    fallback. Verified: one round finds WSL 5.1.16 and all outputs agree.
  - **Run it as `"$BASH" tests/bench/compare3.sh` from Git Bash.** Its `#!/usr/bin/env bash`
    resolves to C#Bash on this machine; that is how the first A/B run timed nothing.

**The article stays as published (same day, appended):** the architect said "Leave the article".
Its table is a dated record that matches LinkedIn; only the README carries current figures.

## 2026-10-03 -- RATIFIED + BUILT: array element assignment with an expanded index
**Status:** Active. The architect said "go for both follow-on fixes" (this one, then the logical
cwd).

**Defects, verified against GNU on rev 88-91:**
- `map[$key]=v`, `st[$k2]=7` in a loop and `ix[$i]=x` ran as a COMMAND ("command not found",
  rc 127) and left the element unset.
- `m+=(["$k"]=v [$i]=x)` set NOTHING, silently.
- Cause: the parser and the compound-assignment path only recognised an index lying wholly
  inside the first literal part of the word.

**Fix:** `Word.TrySplitIndexedAssignment`. The index ends at the first `]` followed by `=` in a
LITERAL part (an expansion cannot close the bracket); everything before is the index, whatever
expansions or quotes it spans, and everything after is the value. Both the element-assignment
parser and compound `[k]=v` elements use it. Compound elements now expand the key and the value
separately, so a key containing `]=` stays intact.

**Verified:**
- New case `array_index_assign.sh`, expected output generated by GNU via WSL. Covered: expanded,
  quoted, braced and arithmetic indexes, compound with expanded keys, and the literal forms as a
  regression guard. The new build matches; rev 91 differs on 17 lines.
- Suite 55/55 with a console and `-Detached`; battery 237/237 identical.

**Found on the way, NOT fixed (read side, rare):** `${odd["a]=b"]}`, a quoted subscript
containing `]` inside `${…}`, reads back wrong (reading through `${odd[$k]}` works). And
`declare -p` does not quote such a key. Both pre-existing. Not supported (also pre-existing): the
append form `arr[i]+=x`.

**A correction sent to a peer:** an `eval "map[\$key]=\$val"` workaround was suggested to
msp430-a2 untested, then tested and found to FAIL (eval re-parses the same construct). The
correction gave `printf -v "map[$key]" %s "$val"`, verified on both shells. Lesson recorded:
never send an untested workaround, even labelled as such, when testing it takes seconds.

## 2026-10-03 -- RATIFIED + BUILT: each shell has its own working directory
**Status:** Active. The architect said "go for both follow-on fixes" (the second, after the
array-index fix).

**Defects, measured on rev 91 (all silent; GNU 0 in every case):**
- `(cd sub && echo x) | tee out.log` put `out.log` in `sub/` in **148-165 of 200 runs**.
- `cd sub | true` moved the shell.
- An in-process script's `cd` moved its caller.
- Cause: one process-global cwd, which `cd` and subshell restores set from whichever thread ran
  them.

**Design (why not the simple version).** Resolving every relative path inside `TranslatePath`
would have touched output: tools that echo a translated path would print absolute paths. The
inventory found ~180 raw file-system calls, but the path-printing tools already keep display and
I/O apart (`find`: `Display` vs `Fs`; glob: `display` vs `dir`). So:
- `ShellEnvironment.Cwd`: each shell's own cwd. The shell itself (`OwnsProcessCwd`) keeps the
  PROCESS cwd equal to it, so nothing changes outside pipelines.
- A pipeline stage's copy never touches the process cwd.
- `TranslatePath` resolves a relative path against the running shell's cwd ONLY when that shell
  is a stage that has moved off its starting directory (`_baseCwd`). Everywhere else paths stay
  relative, exactly as before.
- `ShellEnvironment.Active` (thread-static) is set at each entry where a thread starts running
  shell code: `RunString`, pipeline stages, background jobs, `timeout`'s worker.
- Externals start in the running shell's `Cwd`.
- `cd`, `pushd`, subshell snapshot/restore and `pwd` use the shell's `Cwd`.
- An in-process script's child adopts its caller's cwd; the caller re-asserts its own afterwards.

**A regression the suite caught in the first version:** stages resolved relative paths even
before moving, and the dup-redirect code tested the TRANSLATED target. So the `1` of `2>&1` became
`<cwd>/1`: "ambiguous redirect", 2 suite cases and 2 battery probes failing. Fixed at the root
(dup targets are fd numbers: test the raw word) and by the `_baseCwd` condition. The new case
covers `2>&1` in a stage that has changed directory.

**Verified:**
- New case `cwd_isolation.sh`: 17 lines, expected output generated by GNU via WSL. Covered:
  stage `cd`, subshells, the `tee` race (10 rounds), `cat`, redirects, globs, `test -f`, `find`,
  `2>&1`, an external child and in-process scripts, all in a stage that has changed directory.
  The new build matches; rev 91 differs on 25 lines.
- Race test (200 rounds): 0 misplaced, parent never moved.
- Suite 56/56 with a console and `-Detached`; battery 237/237 identical to rev 91.

**Not covered:** background jobs still run on the parent shell (a `cd` in `… &` still moves the
parent, as before). A raw file-system call on a user path that bypasses `TranslatePath`, run
inside a stage that changed directory, would resolve against the process cwd [none known].

## 2026-10-03 -- FOUND, not fixed: pattern operators ignore expansions and quotes in the pattern
**Status:** Found (verified against GNU on rev 91); widens the 2026-09-12 open item
"`${v//$o/$n}` is a silent no-op". Claude's recommendation: the next fix.
- With `a=/x/y/z`: `${a#"$s"/}`, `${a#$s/}`, `${a//$o/$n}`, `${a%%"/z"}` and `${a%%$suf}` ALL
  return the input unchanged. Only a bare literal pattern (`${a#/x/}`) works.
- So `$var` in a pattern is never expanded, and a quoted pattern is never quote-removed.
- SILENT (a plausible wrong value, rc 0), and `${path#"$prefix"}` and `${f%"$ext"}` are everyday
  idioms.

**Performance re-test and deployment (same day, appended).** The architect asked to re-test
performance after both follow-on fixes.
- **Rev 95 (both fixes) against deployed rev 91**, `compare3.sh`'s method under Git Bash:
  - Best of 9, interleaved: every README row within noise. `loop` +5.7 % and `func` +3.1 % looked
    like a cost, so they were re-timed at best of 21: `loop` 0.170 vs 0.170 (+0.0 %), `func` +0.4 %,
    `loop_big` +0.5 %. Noise; nothing in either fix runs per loop iteration.
  - Whole process (best of 9): pipeline loop +0.9 %, line tools -0.5 %, coreutils -0.9 %,
    pipeline -1.3 %; startup 13.0 vs 12.9 ms. All outputs identical, and identical to Git and WSL.
- **So the README table stands as published today.** It describes the subshell-per-stage build,
  and swapping its figures for this run's would trade noise for noise.
- **Deployed:** rev 95 (`1.0.0+hg.588b06d2ac33+ 95+`) in `dist/Bash.exe`; rollback copy
  `dist/Bash.exe.rev91`. Suite 56/56 with a console and detached on the shipped binary.
- **Live in a Claude Code Bash tool:** `map[$key]=v` and `map+=(["beta"]=v [$key-2]=v)` set their
  elements; `(cd sub && …) | tee out.log` puts the file in the right place; `cd sub | true` does
  not move the shell.
- **For peers until the pattern-operator fix:** `${p:${#s}+1}` strips a known prefix. Verified
  live; it is standard bash.

## 2026-10-03 -- RATIFIED + BUILT: the pattern and replacement of `${x#pat}`, `${x%pat}`, `${x/pat/rep}` are expanded
**Status:** Active. The architect said "Yes" to fixing the pattern operators.

**Defect (verified against GNU on rev 91-95):** `#`, `##`, `%`, `%%`, `/` and `//` used their
operand RAW. So `${p#"$s"/}`, `${p#$s/}`, `${p//$o/$n}`, `${p%%"/z"}` and `${a%%$suf}` all
silently returned the input; only a bare literal pattern worked. The replacement had no quote
removal either (`${V//b/\\}` gave `a\\`; the 2026-09-12 open item).

**Fix:** `WordExpander.ExpandOperand(raw, pattern)`.
- **Why it scans the RAW text instead of using the word parser:** the lexer drops an unquoted
  backslash's quoting, so `\*` would turn into a live `*`.
- **In a pattern:**
  - a backslash escape is kept, for the glob engine;
  - a quoted part (`'…'`, `"…"`, `$'…'`) is expanded and then glob-escaped (`Glob.Escape`), so it
    matches literally;
  - an unquoted `$var`, `${…}`, `$(…)`, `$((…))` or backtick is expanded and STAYS a pattern, as
    in bash;
  - a leading `~` is tilde-expanded.
- **In the replacement:** the same expansions, plus quote removal.
- **The `/` that ends the pattern** is now the first one outside quotes and expansions
  (`FindPatternSlash`), so `${p//"/x"/y}` works.

**Speed, measured:** 50k iterations of three pattern operations with variables, JIT build including
its startup.
- First correct version: 0.68 s, against Git Bash 0.82 s.
- Two fixes:
  - **a parse cache per segment** (words are immutable): 0.52 s;
  - **a plain-string path** for any pattern with no live glob character (`Glob.TryLiteral`, used
    by `#`/`%` and `/`), plus **a regex cache** for replacements, which built a new Regex on
    EVERY call (pre-existing): 0.40 s.
- That is about 2x Git Bash; WSL 2.28 s.

**Verified:**
- New case `pattern_operands.sh`: 12 lines, expected output generated by GNU via WSL. Covered:
  quoted vs unquoted variables, an escaped `\*`, a quoted glob vs a live glob, backslashes,
  replacement escapes, `$( )`, `~`, arrays, spaces, a slash inside quotes. The new build matches;
  rev 95 differs on 11 of 12.
- Suite 57/57 with a console and `-Detached`; battery 237/237 identical. No battery probe uses a
  variable in a pattern: a coverage gap the new case fills.

## 2026-10-03 -- FOUND, not fixed: an escaped glob character in an argument still globs
**Status:** Found (verified against GNU on rev 95). Claude's recommendation: fix next. The architect
has not yet ruled.
- `echo \*` and `echo \?` print matching file names; GNU prints `*` and `?`. `echo "\*"` is right.
- Cause: the lexer's word reader appends the escaped character and DROPS its quoting (`\*` becomes
  a plain `*` in a `LiteralPart`), so field expansion sees a live metacharacter.
- SILENT: `find . -name \*.txt` (a common idiom) receives the list of matching files in the cwd,
  not the pattern.

## 2026-10-03 -- FOUND (msp430-a2's report), not fixed: plain `cp` preserves the source's mtime
**Status:** Found (verified against GNU on rev 95). Awaiting the architect.
- GNU `cp` without `-p` gives the copy the CURRENT time; C#Bash keeps the source's (2020 in the
  repro). `cp -p` is correct.
- Real consequence: a file restored with `cp backup file` kept the backup's OLDER mtime, so
  MSBuild's incremental build judged it up to date and silently kept the broken copy.
- Likely cause [inferred]: .NET `File.Copy` copies timestamps, and plain `cp` never resets them.
- Workaround, verified on rev 95: `cp src dst && touch dst`.

**Deployed (same day, appended):** the rev 98 AOT build (rev 97's code plus a docs-only commit;
stamp `1.0.0+hg.0736501d7dee+ 98+`).
- Suite 57/57 with a console and detached; every README row within noise (best of 9).
- The pattern loop (50k x 3) takes 0.123 s, against 0.143 s for rev 95 (which gave WRONG answers)
  and 0.789 s for Git Bash. The plain-string and regex-cache paths more than pay for the expansion.
- `dist/Bash.exe`, rollback copy `dist/Bash.exe.rev95`. Verified live in a Claude Code Bash tool:
  `${p#"$s"/}`, `${p%$suf}`, `${p//$o/$n}`, `${p%%"…"}` and `${V//b/\\}` match bash.

## 2026-10-03 -- RATIFIED + BUILT: a copy made without -p gets the current mtime
**Status:** Active. The architect said "go" (for this, then the `\*` fix). It answers the FOUND
entry above.

**Fix:** `Builtins.Files.StampNow`. After `File.Copy` (Windows' CopyFile keeps the source's
times), a copy made without `-p`/`-a`/`--preserve` gets mtime and atime = now, in both `cp` and
the recursive path. `-p` behaviour is unchanged. A read-only copy is tolerated (the attribute
came with it, as GNU keeps the mode).

**Verified:**
- New case `cp_mtime.sh`: 10 lines, expected output generated by GNU coreutils via WSL. Covered:
  plain, `-p`, `-a`, `--preserve`, over an old file, into a directory, `-r` vs `-rp`, a
  read-only source, contents identical. The new build matches; rev 98 differs on 5.
- Suite 58/58 with a console and `-Detached`; battery 237/237 identical.

## 2026-10-03 -- RATIFIED + BUILT: a backslash-escaped glob, brace or tilde character stays quoted
**Status:** Active. The architect said "go". It answers the FOUND entry on `\*`.

**Fix:** `Lexer.EscapeMark` (U+FDD0, a Unicode noncharacter, so it never occurs in text).
- The lexer's word reader marks an escaped `* ? [ { } , ~`, and `ParseWordParts` emits each
  marked character as a `SingleQuotedPart`. Every later stage already treats a quoted part as
  literal: field glob and brace eligibility, `ExpandToPattern` for `case`/`[[ ]]`, assignments.
- Other escaped characters are unchanged: their meaning was purely lexical and is already spent.
- Arithmetic, `${…}` bodies and double-quoted interiors are lexed elsewhere and are untouched.

**Verified:**
- New case `escaped_glob.sh`: 22 lines, expected output generated by GNU via WSL. Covered:
  `echo \*`, `\?`, `\*.txt`, `\[ab\]`, `find -name \*.txt` and `-path ./sub/\*`, `\{a,b\}`,
  `\~`, `a\,b`, an assignment, `case`, `[[ ]]`, `for`, and a live `*.txt` alongside. The new
  build matches; rev 98 differs on 12.
- Suite 59/59 with a console and `-Detached`; battery 237/237 identical.

**KNOWN LIMITATION, pre-existing and NOT fixed: glob and brace quoting is tracked per FIELD, not
per character** (`ExpandToFields`' `fieldGlob`). A field with any unquoted metacharacter is
brace-expanded and globbed as a whole string, so a quoted metacharacter in the SAME word acts
live: `{x\,y,z}` gives `x y z` where GNU gives `x,y z`, and `"*"*` and `\**` treat the quoted `*`
as a glob.
- A real fix needs a parallel pattern string with quoted metacharacters escaped.
- It cannot use backslash as that escape: a backslash inside an UNQUOTED expansion's value must
  survive into the output, as in bash. So it needs its own marker, honoured by the brace and glob
  engines.
- Core expansion path; a separate decision. Rare in practice.

**Deployed (same day, appended):** the rev 102 AOT build (code from revs 100-101; stamp
`1.0.0+hg.b2f76c56bf68+ 102+`).
- Suite 59/59 with a console and detached; battery 237/237.
- README rows: within noise at best of 9. `func` +4.5 % was re-timed at best of 21: +0.4 %;
  `arith` -0.8 %.
- `dist/Bash.exe`, rollback copy `dist/Bash.exe.rev98`.
- Live in a Claude Code Bash tool: `echo \*` prints `*`; `find . -name \*.txt` finds all three
  files, including the one in a subdirectory, which it missed when the pattern globbed; a plain
  `cp` of a 2020 file has an mtime 0 s old.

## 2026-10-04 -- Goal-2 scan: what Claude needed PowerShell for, and the backlog it implies
**Status:** Scan DONE (the architect said "Sure"). The backlog is PROPOSED and AWAITING THE
ARCHITECT. Report, scripts and inputs: `E:/Claude/csharpbash-findings-2026-10-04-goal2-scan/`.

**Corpus:**
- 502 transcripts (211 sessions and 291 subagent transcripts): `~/.claude/projects` plus the
  session logs archived under E:/Claude, which the architect pointed to. That is where most
  transcripts were.
- 2026-06-19 to 2026-10-04; 1,271 PowerShell tool calls against 10,215 Bash calls, de-duplicated
  by tool_use id.

**Result**, using the architect's goal-2 rule (in scope if the command needed PowerShell, out if
the output is Windows-specific):

| bucket | calls | share |
|---|---:|---:|
| needed no PowerShell | 943 | 74.2 % |
| needed it for a generic capability | 254 | 20.0 % |
| Windows-specific | 65 | 5.1 % |
| unclassified | 9 | 0.7 % |

- Of the 943, 371 are the "launcher line" (`& $env:CLAUDE_CODE_GIT_BASH_PATH script.sh`), the
  workaround for #94077. It was used only in September and stopped when C#Bash became the shell
  the Bash tool spawns.
- The PowerShell share fell from 43 % (June) to 9 % (October).
- **Real gaps, after reading the examples:**
  - serial-port I/O: 55 calls, MSP430 only;
  - the `time` keyword: 37 calls ("time: command not found");
  - a launch that outlives the call: 22 calls. Children die in the kill-on-close job object, and
    there is no `nohup`/`setsid` that breaks away;
  - `jq`: 12 calls, plus the parsing half of 10 HTTP calls;
  - `df`: 2 calls.
- Everything else in scope proved covered: `pkill`/`pgrep`, byte-transparent tools, curl.exe,
  awk/`$(( ))`, `netstat`, tar.exe.

**Claude's proposed order** (criteria: calls x number of projects x cost):
1. **`time`.** Broad, cheap, and also a goal-1 defect.
2. **A detached launch that survives the call.** Medium; a job-object breakaway design.
3. **A `jq` subset.** Medium to large.
4. **Serial I/O** (COMn as a device file, plus `stty`). Large, one project.
5. **`df`.** Small.

Not code: this repo's own test runners (`run-tests.ps1`, `run-compat.ps1`; 80 calls) are
PowerShell, which is goal 2 applied to our own tooling. The launcher-line guidance in the global
CLAUDE.md is obsolete since the rename; that is the architect's file, so suggested only.

**What would change this order:** the architect weighting the hardware work (serial) above
breadth.

**Found on the way:**
- Claude Code sets `NoDefaultCurrentDirectoryInExePath=1`, so `cmd /c x.cmd` will not find a
  script in the current directory (`./x.cmd` from C#Bash works). An environment fact, not a
  defect.
- **C#Bash defect:** `env -u VAR cmd` does not remove VAR for the command (GNU does). Not fixed.

## 2026-10-04 -- RATIFIED + BUILT: the `time` reserved word (goal-2 backlog item 1)
**Status:** Active. The architect said "Yes, then 2, 3 and 5 (commit between, stop for
ambiguities/forks)".

**Defect:** `time` was parsed as a command name: "time: command not found", rc 127. That pushed
37 transcript calls to PowerShell's `Measure-Command`.

**Build:**
- **Parser:** `time [-p] [!] pipeline` sets `Pipeline.Timed` / `TimePosix`; a bare `time` times
  an empty command.
- **`ExecTimedPipeline`:** wall clock, plus user and system CPU of this process AND of the
  external children it waited for. Windows counts only a process's own CPU, while bash counts its
  children too; `AddChildCpu` after each `WaitForExit` closes the gap.
- **Output:** written to the shell's stderr (`ConsoleMux.Err`), so `time cmd 2>/dev/null` still
  reports and `{ time cmd; } 2>&1` captures it.
- **Format:** `TIMEFORMAT` per bash:
  - `%[0-3][l]R|U|S`, `%P`, `%%`;
  - fractions truncated;
  - unset means bash's default `\nreal\t%3lR\nuser\t%3lU\nsys\t%3lS`; empty means no report;
  - `-p` gives `real %2R` / `user %2U` / `sys %2S`;
  - an unknown character gives bash's "invalid format character" error and no report.

**Verified:**
- New case `time_keyword.sh`: 22 lines, expected output generated by GNU via WSL with digits
  masked. Covered: default and `-p` formats, precision and long forms, a pipeline, a group, exit
  status kept, `!`, a command-level `2>/dev/null`, empty `TIMEFORMAT`, capture.
- Values sanity-checked: `sleep 0.25` reads 0.285 real and 0 user. An external CPU-bound child
  reads 0.718 user against 0.661 real, so children ARE counted.
- Suite 60/60 with a console and `-Detached`; battery 237/237.

## 2026-10-04 -- DEFECT FIXED: a function named after a builtin utility never ran
**Status:** Active. Reported by peer session nupkg-28 at the architect's request.

**Defect:** simple-command dispatch tried `_builtins.TryExecute` BEFORE the function table, so
`rev(){ ...; }; rev` ran the builtin `rev` (reading stdin, so it hung without `</dev/null`) while
`type -t rev` said "function". It failed silently: the reporter's `rev(){ hg log -r . ...; }`
returned "", `hg update -r ""` went to tip, and later commits landed on the wrong branch.

**Fix:** bash's lookup order, function -> builtin -> PATH, in the one dispatch site that had it
backwards (`ExecSimple`). `Classify` (`type`, `command -v`) and `RunCommand` (xargs, env, timeout)
already had the right order. Cost: one function-table lookup before each builtin. On an empty
table it is a null check; with functions defined (Claude Code's snapshot defines several) it is
one short-string hash [unmeasured estimate: tens of ns against a per-command cost in microseconds].

**Verified:** new case `function_shadows_builtin.sh` (15 lines, expected = GNU via WSL): shadowing
with and without redirects, in `$( )`, in a pipeline stage, from inside another function,
`command`/`builtin` call-through wrappers, `unset -f` restoring the builtin. Deployed rev 102
differs on 17 lines (polarity). Suite 61/61 with a console and `-Detached`; battery 237/237.

**Found on the way, NOT fixed (open):** the `diff` builtin rejects `-` as stdin ("diff: -: No such
file or directory"); GNU diff accepts it.

## 2026-10-04 -- DEFECT FIXED: functions defined in a subshell leaked into the parent
**Status:** Active. Reported by peer session nupkg-28, verifying rev 107.

**Defect:** `( )`, `$( )` and `<( )` run in-process with only the VARIABLES snapshotted
(`ShellEnvironment.TakeSnapshot`); the function table was shared, a documented limitation
("Not isolated: shell options, traps, functions, aliases"). Rev 106 (functions shadow builtins)
turned it from latent into wrong output: `y=$( rev(){ ...; }; true )` made every later `rev`
in the script run the function.

**Fix:** copy-on-write isolation of the function table. `EnterSubshell`/`LeaveSubshell` bracket
the three in-process subshells; the first define or `unset -f` inside a level saves the table,
and leaving that level puts it back. A subshell that changes no function pays a counter
increment and a stack peek [unmeasured estimate: nanoseconds]. A full copy per `$( )` was
rejected: `$( )` is the hottest construct in Claude's scripts (goal 3).

**Verified:** new case `subshell_functions.sh` (20 lines, expected = GNU via WSL): all three
subshell forms, redefining and unsetting an existing function inside one, nested levels.
Deployed rev 107 differs on 20 lines (polarity). Suite 62/62 with a console and `-Detached`;
battery 237/237.

**Same class, still open (checked against GNU, all differ):** options (`set -e/-u`, `pipefail`),
`shopt`, traps (an EXIT trap set in `( )` fires at the PARENT's exit), and aliases all leak out of
a subshell.

## 2026-10-04 -- DEFECT FIXED: options, shopts, traps and aliases leaked out of a subshell
**Status:** Active. The architect: "Yes, go" (fix the rest of the rev-109 class before the
redirect rewrite).

**Defect:** the rest of the documented limitation behind rev 109. All checked against GNU, all
differed:
- `( set -e )`, `$( set -u )`, `( set -o pipefail )` and `( shopt -s nullglob )` stayed set;
- an EXIT trap set in `( )` fired at the PARENT's exit, not the subshell's;
- `( alias x=... )` survived.

`( set -euo pipefail; ... )` is a pattern Claude writes, and the leak left -e/-u on for the rest
of the script.

**Fix:** one record per in-process subshell level (`EnterSubshell` / `SubshellExitTrap` /
`LeaveSubshell`, around `( )`, `$( )` and `<( )`):
- Options: saved BY VALUE on entry (`ShellOptions.Save`: the `set` flags packed in an int, the
  shopt set shared and copied only when the subshell changes it). Every `set`-settable flag must
  be in `PackFlags`/`UnpackFlags`, or a subshell leaks it.
- Functions, traps, aliases: copy-on-write, saved on the first change inside a level.
- An EXIT trap the subshell set for itself fires as it ends, still inside its own redirects or
  capture; an `exit` in that trap sets the subshell's status. The parent's EXIT trap is visible
  inside (`$(trap -p)`) but does not fire there, as in bash.

**Alternative rejected:** cloning options and tables on every subshell. A `$( )` that changes
nothing now costs a few field reads and no allocation. Measured on AOT, 20,000 `x=$(echo hi)`,
best of 5: 0.091 / 0.090 s new against 0.092 / 0.096 s for deployed rev 109 (no difference).

**Verified:** new case `subshell_isolation.sh` (24 lines, expected = GNU via WSL); deployed rev
109 differs on 29 lines (polarity: it also lost output after a leaked trap). Suite 63/63 with a
console and `-Detached`; battery 237/237.

## 2026-10-04 -- RATIFIED + BUILT: background children outlive the shell (goal-2 item 2, option b)
**Status:** Active. The architect chose (b), "b/ go".

**Context:**
- Measured: the Bash tool's shell is in NO job object, and its stdout is a file. So the only
  thing that killed `server &` at the end of a call was C#Bash's own kill-on-close job, which
  every child joined.
- Claude's habit is `server &` in one call and talking to it in the next. On Linux that works.

**Decision:** bash's own rule.
- External children of a background job (`cmd &`, a background pipeline or function), and those
  started under `nohup` or `setsid`, are NOT put in the kill-on-close job (`ChildOutlivesShell`).
- Foreground children stay in it, so a host that kills the shell on a timeout still takes the
  foreground work with it. `timeout`'s internal job is not a background job.
- New builtins:
  - `nohup`: GNU's terminal rules for stdin, stdout and stderr; 125 for a missing operand.
  - `setsid [-f] [-w]`: foreground in a non-interactive shell, as util-linux behaves without job
    control; `-f` starts it and returns.
  - `disown [-a] [-r] [-h] [jobspec]`.
- Pipeline stages carry the job and the detach flag (they run on their own threads).
- At shell exit, a job is a thread, not a forked process, so a job that has not yet started its
  program gets up to 1 s to do so (`SettleBackgroundJobs`). Otherwise `server &` as the last line
  would never start.
  - The wait is a monitor (`BackgroundJob.WaitSettled`), never a `Thread.Sleep` poll (the
    architect: "Never ever use thread.Sleep for any kind of sync purposes").
  - A job parked in the `sleep` builtin is not waited for. Measured: the first version cost
    1.07 s at every exit with a pending `sleep 30 &`.

**Measured (AOT, best of 11):**
- exit with a pending `sleep 30 &`: +2 ms;
- with `(sleep 5; kill 1) &`: +5 ms;
- with a background builtin-only busy loop: +1.0 s (accepted, rare).

**Not solved here (the survival spike, `scratchpad/detach/survive.sh`):**
- `server > log &` still loses its output when the shell exits: the redirect is a pipe pumped by
  a shell thread. This is the redirect rewrite, ratified separately and next.
- A nested shell (`bash -c 'server &'`) is a separate process inside the parent's job, and
  Windows children inherit job membership, so the server dies with the outermost C#Bash. OPEN.
- A builtin or function cannot outlive the shell: it is a thread.

**Verified:** new case `detach_builtins.sh` (13 lines, expected = GNU via WSL); deployed rev
111 differs on 17 lines. Suite 64/64 with a console and `-Detached`; battery 237/237.

## 2026-10-04 -- RATIFIED + BUILT: an external child gets the REAL file handle (the redirect rewrite)
**Status:** Active. The architect: "Yes" (every external child, foreground included; `/dev/null`
becomes the NUL device), then "Yes, go".

**Context:** the phase-2 survival spike. ExecExternal gave a redirected child a PIPE, and a
shell thread pumped it into a FileStream the shell opened UNSHARED. So:
- `server > log &` lost all its output when the shell exited (measured: 0 lines against 7);
- `cat log` failed while the job ran ("used by another process");
- an outer shell waited for EOF that a detached grandchild held.

**Decision:**
- **One launcher (`ChildLauncher.Start`).** `CreateProcessW` with exactly three std handles and
  PROC_THREAD_ATTRIBUTE_HANDLE_LIST, so a child inherits nothing else (Windows otherwise hands it
  every inheritable handle in the process, so one stage's child could hold another stage's pipe
  open). `Process.Start` and `HiddenConsole.StartInherited` are gone from the launch path.
- **Each fd 0-2 starts where the shell's stream points NOW, and redirects apply LEFT TO RIGHT**,
  as dup2 does (`2>&1 >f` leaves stderr on the old stdout).
- **A file is a handle the child holds.**
  - `>`: OPEN_ALWAYS + SetEndOfFile, so attributes are kept and a hidden file works.
  - `>>`: FILE_APPEND_DATA, so concurrent appenders interleave, as O_APPEND does.
  - `<`, `<>`, and `/dev/null` as NUL.
  - Every handle shares read, write and delete.
- **The shell's OWN file redirect is handed to the child as its own append handle**, not pumped.
  This covers `nohup server > log 2>&1 &`, where the redirect belongs to the builtin, plus
  `{ ext; } > f` and `exec 3>f`. The shell flushes before the launch and seeks to the end after
  the child, so `{ echo a; ext; echo b; } > f` keeps the order.
- **Only in-process ends keep a pipe and a pump:** a pipeline stage, a `$( )` capture, a here-doc,
  a non-file fd. `2>&1` into one of them is ONE pipe, so the order holds.
- **`ShellFile`:** every file the shell itself opens for a command shares read, write and delete
  (File.OpenRead / File.Create lock other processes out).

**Defects fixed on the way.** All were verified against GNU, and all were present in deployed rev 111:
1. An external inside `$( )` or `{ } > f` IN A PIPELINE STAGE wrote into the stage's pipe:
   `for ...; do x=$(git ...); done | sort` captured nothing. The newest stdout now wins.
2. Text a `read` had buffered was lost to an external that followed (`printf 'a\nb\n' | { read x;
   ext; }`).
3. `./script.sh > out 2> err` ignored both redirects, and `FOO=1 ./script.sh` passed no FOO. An
   in-process script now takes its redirects and temp assignments.
4. A redirect failure abandoned the whole statement (the rest of a `{ }` group) and was printed
   outside the enclosing `2>/dev/null`. It now fails that one command with status 1 and is
   reported where its stderr points (`ApplyRedirects` reports before undoing its partial scope).
5. noclobber, "ambiguous redirect" and a bad fd were status 2; bash gives 1.
6. `<>` was ignored for externals; `n<file` with n>0 hijacked stdin.

**Measured:**
- **Survival** (`scratchpad/detach/survive2.sh`, Release): the shell exits and the probe keeps
  writing all 7 lines for `> f 2>&1 &`, `>> f &`, `nohup ... > f 2>&1 &`, `setsid -f ... > f`, a
  background function, `cd && ... &` as the last line, and an outer shell owning the file. Also:
  `> /dev/null 2>&1 &` stays alive; `cat log` reads while the job writes; a foreground child of a
  killed shell still dies (polarity).
- **Speed** (AOT, deployed rev 111 against this): README rows -2 % to +3 % (noise). External
  launches best of 21: inherited +0.00 ms, `> f` -0.07 ms, `>> f` -0.55 ms a launch.
- **A finding worth keeping:** when the CHILD made the last close of a file it had rewritten, it
  cost +0.65 ms a launch. CREATE_ALWAYS made no difference; holding the shell's handle until the
  child exits removed it entirely. Cause [inferred]: on-close scanning in the child's teardown.

**Accepted risk:** the C runtime reports NUL as a terminal (`isatty`), so a program that checks
may act as if interactive. cmd's `> nul` and Git Bash have done the same for decades.

**Still open:**
- nested shells (`bash -c 'server &'`) die with the outermost C#Bash (job inheritance);
- stdin from an in-process `< file` is still pumped;
- found while testing, separate items: background jobs share the parent's variables (all three
  `$i` read 3); `"$@"` / an unquoted `$cmd` in COMMAND position is not word-split (every build
  back to rev 80).

**Verified:** new case `external_redirects.sh` (27 lines, expected = GNU via WSL); deployed rev
111 differs on 33 lines. Suite 65/65 with a console and `-Detached`; battery 237/237.

## 2026-10-04 -- DEFECT FIXED: "$@" and an unquoted $cmd in command position were one word
**Status:** Active. Found while writing the redirect rewrite's spike (a `ms() { ...; "$@"; }`
timing wrapper failed). Present in every build back to rev 80.

**Defect:** `ExecSimpleCommand` expanded the command word with `ExpandToString`, as ONE string,
while the arguments got full field expansion. So `f() { "$@"; }; f echo a b`,
`c="ls -l"; $c`, `"${arr[@]}"` and every `run() { ...; "$@"; }` wrapper failed with
"echo a b: command not found".

**Fix:** the command word is expanded like any word (field splitting, "$@", globs, braces), and
its first field is the command; the rest lead the arguments. If it expands to nothing, the first
argument is the command; if nothing remains at all, no command runs, but its redirects are still
made. A plain literal name (nearly every command) keeps the old single-string path, so the hot
path is unchanged.

**Verified:** new case `command_word_split.sh` (expected = GNU via WSL), covering "$@", `$cmd`,
an array, a wrapper, a quoted name with spaces (one word, 127), an empty name, "$@" with no
arguments, and `{echo,brace,expansion}`. Deployed rev 114 differs on 23 lines. Suite 66/66 with a
console and `-Detached`; battery 237/237.

## 2026-10-04 -- `sleep` and `tail -f` wait on signals; no Thread.Sleep left
**Status:** Active. The architect: "Never ever use thread.Sleep for any kind of sync purposes -
there are better ways"; converting these two older offenders was agreed with the leak-class "Yes, go".

**Change:**
- Each interpreter has an interrupt EVENT beside its flag. `RequestInterrupt` (Ctrl+C,
  `timeout`) sets it; `ClearInterrupt` / `CheckInterrupt` reset it. `InterruptHandles()` returns
  this shell's event and its pipeline parents'.
- `sleep`: one timed wait on those handles, instead of 50 ms Thread.Sleep slices polling the
  flag. An INT trap still runs and the sleep goes on, as before.
- `tail -f`: waits on the interrupt handles plus a FileSystemWatcher event. The interval (GNU's
  `-s`) remains the backstop, because NTFS reports a growing file's size lazily, so the watcher
  alone can miss growth.
- Also fixed: a `tail -f` ended by Ctrl+C or `timeout` printed "tail: cannot open 'f' for
  reading: Interrupted"; GNU prints nothing.

**Measured (Release):**

| | deployed rev 114 | new |
|---|---|---|
| `time sleep 0.3` | 0.358 s | 0.316 s |
| `timeout 0.3 sleep 5` | 0.372 s | 0.318 s |

The slicing overshoot is gone. `timeout 1 tail -f` on a growing file matches GNU (lines, then rc 124).

**Verified:** suite 66/66 with a console and `-Detached`; battery 237/237. `Thread.Sleep` now
appears in the source only in comments.

## 2026-10-04 -- CORRECTION to the rev-116 entry: its fast path made `[` 20x slower (fixed, never deployed)
**Status:** Active. Corrects the 2026-10-04 "$@ in command position" entry, which said "the hot
path is unchanged". It was not.

**What happened:** rev 116 sent any command name containing `*`, `?`, `[` or `{` down the full
expansion path, and that included `[` itself. Measured on AOT before deploy (deployed rev 114
against the rev 116 build, best of 21):
- loop.sh 0.141 -> 3.202 s;
- func.sh 0.195 -> 1.752 s;
- arith.sh 0.226 -> 3.277 s.

The suite passed: it checks output, not speed. The benchmark caught it.

**Fix:**
- `Word.IsPlainLiteral`: computed once per AST node and cached, since loops reuse the node. Only
  a real pattern takes the slow path: `*`, `?`, a `[` closed later, or a `{` closed later.
- The non-literal path (`ExpandCommandWord`) and the "nothing left" path (`NoCommandLeft`) moved
  out of line, so `ExecSimpleCommand` stays compact.

**Measured after (AOT, best of 21, deployed rev 114 shown as two control runs):**

| | rev 114 | current |
|---|---|---|
| loop.sh | 0.143 / 0.141 | 0.144 |
| func.sh | 0.195 / 0.196 | 0.202 |
| arith.sh | 0.226 / 0.226 | 0.230 |

loop is level. func and arith stay +2-3 % over three rounds. The cause is NOT isolated: by
inspection the added per-command work is a cached byte test, and rev 117 is not on these paths.
Suspected code layout [inferred]. AWAITING THE ARCHITECT: whether that residual blocks deploying
revs 116-118.

**Verified:** `command_word_split` still matches GNU; suite 66/66 with a console and `-Detached`;
battery 237/237.

## 2026-10-05 -- RATIFIED: deploy revs 116-118 despite func +3 % / arith +2 % (Q1 "yes")
**Status:** Active. The architect: "Q1/ yes", with the residual unexplained (see the rev-118
correction entry). Deployed as rev 120 (AOT suite 66/66 with a console and `-Detached`; verified
live: wrappers split, `sleep 0.2` = 0.208 s).

## 2026-10-05 -- RATIFIED + BUILT: a background job runs on its own copy of the shell (Q2 "Yes")
**Status:** Active. The same decision as the 2026-10-03 pipeline-stage interpreters, applied to
`cmd &` and `setsid -f`.

**Defect:** a job ran on the parent shell's own state. So:
- `for f in *; do gzip "$f" & done` read `$f` after the loop had moved on (measured: three jobs
  with `$i` = 1, 2, 3 all wrote "w3");
- a job's `cd`, assignment or function definition changed the parent;
- `kill %n` killed only the program the job was running, so a `while` loop went on to its next
  iteration.

**Decision:**
- At `&` the shell builds the job's interpreter (`new Evaluator(this, sharesInterrupts: false)`)
  on the CALLING thread, so the job sees the state as it was at `&`, as bash forks it.
- A job does not chain to its parent's Ctrl+C: an asynchronous job ignores the terminal's
  interrupt in bash.
- `kill %n` interrupts the job's own interpreter as well as killing its current program. An
  in-process loop then ends, with status 143 (128 + SIGTERM).
- Job numbers follow bash: one past the highest still in the table, so after `wait` the next job
  is %1 again. A counter that never reset made `kill %1` "no such job" after a loop of jobs.

**Measured (AOT, best of 11):**
- 500 x `: &` plus `wait`: 0.035 -> 0.040 s, about 10 us a job (the copy);
- README loop/func/arith level against a same-run control.

**Verified:** new case `background_isolation.sh` (15 lines, expected = GNU via WSL); deployed rev
120 differs on 18 lines. Suite 67/67 with a console and `-Detached`; battery 237/237.

## 2026-10-05 -- RATIFIED + BUILT: a nested shell's detached child leaves the outer kill job (Q3)
**Status:** Active. The architect: "Q3/ Spike", then "Yes adopt".

**Context:** `bash -c 'server &'`, or `bash start.sh` whose script backgrounds a server, is a
separate C#Bash process inside the outer shell's kill-on-close job. A Windows child inherits its
parent's job, so the server died with the OUTERMOST C#Bash even though no job of its own shell
held it.

**Decision:**
- C#Bash's job allows breakaway (JOB_OBJECT_LIMIT_BREAKAWAY_OK).
- A child that outlives the shell (`ChildOutlivesShell`) starts with CREATE_BREAKAWAY_FROM_JOB.
- Where a job above us forbids breakaway (a host's own job), CreateProcess fails with access
  denied and the launcher starts the child normally inside that job: the old behaviour, no new
  failure.

**Cost accepted:** any program in our job may now leave it by asking for breakaway itself. Only a
program written to outlive its parent does that.

**Spike, kept as manual checks** (`tests/detach/nested.sh`; `tests/detach/survive.sh` for the
top-level matrix):

| Case | Result |
|---|---|
| nested background child | survives (7/7; the deployed rev 121 control 0/7) |
| doubly nested | survives (7/7) |
| a nested FOREGROUND child, outer shell killed | still dies |
| inside a job forbidding breakaway | still starts, and dies with that job |

The top-level survival matrix is unchanged, and its nested row now survives.

**Verified:** suite 67/67 with a console and `-Detached`; battery 237/237.

**Found on the way, OPEN:** C#Bash's sed treats a `$` or `^` in the MIDDLE of a basic regex as an
anchor; GNU treats it as a literal (`echo 'S=$1' | sed 's/^S=$1/X/'` matches in GNU, not here).

## 2026-10-05 -- DEFECT FIXED (in rev 123, found by its live test): a detached child must not share a hidden console
**Status:** Active. Completes the nested-shell decision above.

**Defect:** in the LIVE cross-call test of rev 123, the nested probe survived but every `ping` in
it failed at once, after the call ended. Measured with a console-less outer shell
(`tests/detach/consoleless.ps1`): 15 of 15 pings failed, 15 ticks in under 4 s.

**Cause:** Claude Code's tool shell has no console, so it starts each child with a NEW hidden
console. That console's host (`conhost.exe`) is created inside the tool shell's kill-on-close
job. The nested shell's detached child inherited that console, so when the call ended and the job
closed, the host died. The child had broken away and lived on, but without a console every
console program it started failed. Two observations support this:
- conhost was parented to the (exited) inner shell, and gone after the call;
- the earlier spike missed it because its outermost shell already had a console that no C#Bash
  job held.

**Fix:**
- A child that outlives the shell starts with CREATE_NO_WINDOW, giving it a hidden console of
  its own, whenever this shell's console may die with a shell: no console, or a windowless one
  (`HiddenConsole.ConsoleMayDieWithAShell`).
- A real terminal, which has a window, is still shared, so `server &` there prints to the
  terminal.
- The shell no longer attaches itself (`Adopt`) to a detached child's console.

**Verified:**
- `tests/detach/consoleless.ps1`: 0 of 15 pings failed, normal pace.
- `tests/detach/nested.sh` against rev 121 as the old build: unchanged.
- `tests/detach/survive.sh`: unchanged.
- Suite 67/67 with a console and `-Detached`; battery 237/237.

**Not verified:** `server &` from an interactive C#Bash in a real terminal (no terminal here).

## 2026-10-05 -- DEFECTS FIXED: basic-regex `$`/`^`/`*`, grep -P `\K`, `diff -`
**Status:** Active. The architect: "fix first, then jq". The first two were hit by peer sessions.

1. **Basic regex (sed, grep).** A `$` or `^` in the middle of a basic regex is a literal in
   POSIX/GNU; `BreToNet` passed both through and .NET took them as anchors. daq-67's
   `s|^REV=$(hg id -n)$|...|` silently matched nothing.
   - Now `^` anchors only at the start or right after `\(` / `\|`, and `$` only at the end or
     right before `\)` / `\|`.
   - A leading `*` (also after `\(`, `\|`, `^`) is a literal; .NET rejected it ("Quantifier
     following nothing"). Extended regex: the same for a leading `*`; its `^`/`$` stay anchors,
     as in GNU.
   - Matrix measured against GNU first (15 cases), all now equal.
2. **`grep -P` `\K`** (koliada-net-dd): .NET has no `\K`. Each top-level branch `X\KY` becomes
   `X(?<csbashKeep>Y)`, and `-o` reports that group's span (`KeptSpan`). The last `\K` of a
   branch wins; a `\K` inside a group is refused loudly.
   - **Rejected: the lookbehind `(?<=X)Y`.** It does not consume X, so `\w+ \K\w+` over
     "public void Alpha()" also matched "Alpha", which GNU does not. Measured with the
     top-level-`|` case.
3. **`diff -`** now reads stdin: copied once, byte-faithfully with any read-ahead, to a temp file
   that is diffed while still labelled `-`. `diff - -` is 0.

**Verified:** new case `regex_k_diff_stdin.sh` (36 lines, expected = GNU via WSL); deployed rev
124 differs on 49 lines. Suite 68/68 with a console and `-Detached`; battery 237/237. README rows
on AOT level with rev 124 (best of 9).

## 2026-10-05 -- RATIFIED + BUILT (not deployed): the in-process jq subset (goal-2 item 3, option (c))
**Status:** Active. Built; NOT deployed until it is checked against the oracle. The architect:
"1/ c. 2/ Yes 3/ noted".

**Evidence** (`E:/Claude/csharpbash-findings-2026-10-05-jq/`, 529 transcripts):
- 23 PowerShell JSON calls: read a JSON/JSONL file or a GitHub API reply, navigate fields, iterate
  arrays, count, pick the longest, format text.
- 100 python-json calls, mostly real programs.
- 8 `jq` calls, all probes (`jq --version`).
- jq is on neither Windows nor WSL.

**Decision (c):** an in-process subset. A filter or option outside it is refused while PARSING,
before input is read, and the dispatcher hands the command to a PATH `jq` if there is one, else
fails loudly (`UnsupportedOptionException`, DECISIONS 2026-09-04 #2).

**Alternatives:**
- (a) Ship or require the real jq.exe: rejected, it adds a dependency for every C#Bash user.
- (b) A subset with no deferral: rejected, it would leave no path to the full language.

**Design:**
- Values are CLR objects (null, bool, double, string, `List<object?>`, `JObj`), and `JObj` keeps
  insertion order. Nothing is mutated; assignment copies along its path.
- Filters are generators (`IEnumerable`).
- `=`, `|=`, `op=`, `//=` and `del` work on paths (`JqInterp.Paths`), as jq's do.
- Evaluation orders follow jq 1.6: in binary operators the right operand varies slowest; in
  `{}` construction the first key varies slowest; in string interpolation the first part varies
  fastest.
- Files: `JqJson.cs` (values, reader, printer, order), `JqParser.cs`, `JqInterp.cs`,
  `Builtins.Jq.cs` (options, inputs, errors, exit codes 0/1/2/3/4/5).
- `jq --version` answers `jq-1.6 (C#Bash in-process subset)`: it was Claude's usual first jq
  call, and an error there reads as "no jq".

**Scope as ratified:**
- input: files, stdin, JSONL, BOM tolerated; options `-n -s -c -r -j -e -M --arg --argjson`;
- navigation: `.a`, `."k"`, `.[n]`, slices, `.[]`, `?`, `..`;
- `|`, `,`, construction, interpolation, arithmetic, comparison, `and`/`or`/`not`, `//`,
  `if`/`elif`/`else`, `as` (with array and object destructuring), `reduce`, `try`/`catch`,
  `= |= += -= *= /= %= //=`, `del`;
- builtins: select, empty, error, length, keys(_unsorted), values, has, map(_values),
  to/from/with_entries, add, any, all, sort(_by), group_by, unique(_by), min/max(_by),
  first/last, reverse, flatten, range, tostring, tonumber, type, tojson, fromjson, split, join,
  test, sub, gsub, startswith, endswith, ltrimstr, rtrimstr, ascii_downcase/upcase, contains,
  env/$ENV, recurse, floor, ceil, round, sqrt, fabs;
- formats: @text, @json, @csv, @tsv, @base64.

Refused (or deferred): def, foreach, label, `$__loc__`, `@fmt "..."`, `?//`, paths/getpath and
the rest. The type filters (`numbers`, `strings`, ...) are outside the agreed list: a candidate
for a small extension.

**Verified so far:**
- The evidence cases and a sweep (`tests/cases/jq_subset.sh`) agree with jq's documented
  behaviour BY INSPECTION; the non-ASCII output bytes were checked.
- 1 MB JSONL select+project: 0.104 s against python's 0.092 s (JIT build); output identical
  apart from python's CRLF.
- Suite 68/68; battery 236/237. The one difference is probe 212 (`jq --version`, not in the
  baseline): a reference shell without jq prints "ba...".

**OPEN, blocks deploy:** the oracle. Real jq 1.6 in WSL (`sudo apt-get install -y jq`, the
architect) generates `tests/expected/jq_subset.out`; then tune the `[TUNE]` items:
- number format (`-0`, `1e17`, exponents);
- `Brief` truncation in error messages;
- parse-error and syntax-error wording;
- the error location `(at <stdin>:N)`;
- `"x" * 1.5`.

## 2026-10-05 -- DEFECTS FIXED: builtins' non-ASCII output with no console; `${#s}` and substrings count characters
**Status:** Active. Found while checking the jq subset against jq 1.6 (the suite's `-Detached`
mode, which runs C#Bash as Claude Code does).

1. **Non-ASCII text from builtins reached Claude Code in the ANSI code page.** `echo 'é — ✓'`
   arrived as "� � ?" (measured live on deployed rev 127).
   - Cause: `Console.OutputEncoding = UTF-8` needs a console. Without one it throws, the failure
     was swallowed, and .NET kept cp1252 for stdout and stderr. The comment there assumed "the
     default UTF-8 writer is what we want anyway"; it was not.
   - Fix (Program.cs): when that setter fails, stdout and stderr get UTF-8 `StreamWriter`s of our
     own before ConsoleMux captures them.
   - Pipes, files and external programs were never affected.
2. **`${#s}`, `${#a[i]}` and `${s:off:len}` counted UTF-16 units,** so "😀" was 2 characters
   (bash: 1). `CharCount` / `Utf16Offset`: a surrogate PAIR is one character, and a lone
   surrogate (an undecodable byte kept by ShellEncoding) is one too. Rune enumeration would have
   turned those bytes into U+FFFD.
3. **The test harness** read expected files as ANSI (PowerShell 5.1's `Get-Content` default) and
   detached output in the console's code page. Both are UTF-8 now. Every earlier case was pure
   ASCII, which is why nothing failed before.

**Verified:** new case `non_ascii_output.sh` (expected = GNU via WSL); deployed rev 127 fails it
in `-Detached` mode ("echo: � � ? ?? ??"). Suite 71/71 with a console and `-Detached`.

## 2026-10-05 -- jq subset VERIFIED against jq 1.6 (the oracle) and tuned to it; type filters added
**Status:** Active; NOT deployed (see the startup note). The architect installed jq 1.6 in WSL
("should be installed") and approved the type filters ("2/ yes").

**Byte-identical to jq 1.6:** `jq_subset.sh` (84 lines) and `jq_messages.sh` (68 lines). The
second is the probes that tuned it, now regression tests.

**Tuned to the oracle; several of these overturned my assumptions:**
- `//` does NOT suppress an error on its left in jq 1.6.
- `from_entries` takes key/Key/name/Name (not k/K) and value/Value (not v); a non-string key is
  an error.
- Index errors read `Cannot index number with string "a"`.
- A value quoted in a message is cut to 11 characters plus "..." when its JSON exceeds 14.
- `"ab" * 1.5` is "ab". `1 / 0` between literals is a COMPILE error ("Division by zero?"); `%`
  by zero is a runtime one with "(remainder)".
- Numbers follow jvp_dtoa_fmt (12345678901234567890 prints 12345678901234567000; 1e17 prints
  1e+17; -0 prints -0).
- Input parsing is a port of jv_parse.c: its messages and line/column, "at EOF", and "parse
  error: ..." with exit 2.
- An error's location counts the lines READ when the value completed (jq reads line by line).
- Compile errors: bison token names, "(Unix shell quoting issues?)", ", expecting $end" after a
  complete filter, the line padded to the error's column, `$x` / `foo/0` "is not defined" (exit 3).
- `error(null)` prints nothing.
- stdout is buffered like stdio (4 KB), so with `2>&1` error lines precede earlier results.
- A real defect found here: a comma ran its RIGHT side eagerly (`Concat(Eval(right))`), so
  `"a", error("x")` lost "a". Now a true generator.

**A misspelling is not "unsupported":** a name/arity outside jq 1.6's own `builtins` list
(embedded) is jq's compile error; only a real builtin outside the subset defers or is refused.

**Kept on purpose:** `jq --` ends options as in jq 1.7 (jq 1.6 prints its usage).

**Measured:**
- AOT README rows level against deployed rev 127 (best of 9).
- **Startup +1 ms: 0.035 -> 0.036 s** (best of 21, interleaved, twice, Git Bash timing). The
  binary grew 6.08 -> 6.57 MB with the engine [inferred cause].
- AWAITING THE ARCHITECT: deploy with +1 ms startup, or shrink first.

**Verified:** suite 71/71 with a console and `-Detached` (the two jq cases included); battery
236/237 (probe 212 `jq --version`, not in the baseline).

## 2026-10-06 -- CORRECTION: the jq build's "+1 ms startup" does not exist (a measuring artefact)
**Status:** Active. Corrects the 2026-10-05 entry "jq subset VERIFIED...", which reported
"Startup +1 ms: 0.035 -> 0.036 s" and held the deploy on it. The architect asked "Where is the 1ms
coming from?".

**Measured properly** (AOT builds of revs 129, 130, 132 and 133 from `hg archive`; per-launch
time = the total for 200 consecutive `-c 'exit 0'` launches / 200; rounds interleaved):

| launcher | rev 129 (no jq) | rev 133 | difference |
|---|---|---|---|
| C#Bash (direct CreateProcess, as Claude Code launches a shell), best of 5 rounds | 11.11 ms | 11.16 ms (r130 11.19, r132 11.12) | noise |
| Git Bash (the README's method), mean of 10 rounds | 38.10 ms | 38.00 ms | -0.10 ms, sd 0.44, se 0.14 |

**Cause of the false reading:** a best-of-21 MINIMUM on a 1 ms clock. True startup sits at about
35.5 ms, so the minimum flips between 0.035 and 0.036 on noise. The binary did grow (6.08 -> 6.57
MB: jq +390 KB, its tuning +92 KB), but that costs nothing measurable at startup.

**Method from now on:** a startup difference under 1 ms is measured as a total over many launches,
interleaved, with the spread reported, never as a best-of-N on a 1 ms clock.

## 2026-10-06 -- The benchmark is one fixed command: `tests/bench/compare3.sh`, method frozen in it
**Status:** Active. The architect: "the benchmark should 'just run' - every time, the same way
(unless we explicitly change it). otherwise it is not a benchmark!?"; method approved ("Yes, yes,
yes") the same day.

**Context:** in one session the C#Bash builds were timed with half a dozen ad-hoc harnesses
(different launchers, round counts, statistics, a scratch root under TEMP that Git Bash maps to
/tmp, a binary on drive E:) and the numbers were reported as if interchangeable with the
benchmark. That produced the false "+1 ms startup" (see the CORRECTION entry above). The README's
method ("C#Bash best of nine, the other two best of three") was not encoded anywhere: the script
had one `[rounds]` argument for all three shells, and no defined way to time an undeployed build.

**Decision:** the method lives in the script, and the caller cannot vary it.
- Rows: C#Bash best of 9, Git Bash and WSL best of 3 (the README's documented method).
- Startup: the total of 100 consecutive `-c 'exit 0'` launches / 100, the same for all three
  shells, gated on a control (`-c 'exit 7'` must return 7). Was: best of 7 single launches.
  **This changes the startup row's method**: rows before 2026-10-06 are not comparable to it.
- One option, `--cs <exe>`, times an undeployed build; the header says so in the output
  ("NOT the deployed build"). Any other argument is refused. The `bin/Release` fallback is gone
  (it silently timed a different build when `dist/` was missing).
- Every run prints the date, build stamp, shell versions and method, and appends its full output
  to `tests/bench/results.md`, so runs compare over time.
- It refuses to run under C#Bash (there, "Git Bash" would be C#Bash and so would the `time`).
  The exact launcher for this machine is in its header.

**Alternatives considered:**
- **Keep best-of-7 startup** -- rejected: a minimum on a 1 ms clock cannot resolve the sub-ms
  differences the row gets used to judge; it invented the +1 ms.
- **Best of 3 batches of 100** -- not chosen: the approved wording is one total; a batch of 100
  already resolves 10 us. Revisit if the row proves noisy run to run (results.md will show it).
- **Leave method as caller arguments** -- rejected: that is how the runs drifted.

**Not changed:** the seven row scripts, the output-agreement check, inherited stdin.

**Revisit if:** results.md shows run-to-run spread on a row larger than the differences being
judged; a method change is the architect's call and gets its own entry.

## 2026-10-06 -- The benchmark launches every shell directly: `tools/benchtimer`
**Status:** Active. Amends the same-day entry "The benchmark is one fixed command" (its rounds,
rows, log and startup statistic stand; only the launcher and clock change). The architect: "Why
are we using Git bash to measure the startup time of C#Bash - that is stupid." / "yes, make it so!"

**Context:** `compare3.sh` timed with bash's `time` inside Git Bash, so Git Bash launched every
shell. Starting a NATIVE program from Git Bash costs about 30 ms (C#Bash read 42 ms per launch
that way, 11 ms launched directly), and Git Bash launching itself does not pay it. Every row
carried that bias against C#Bash and WSL, most visibly startup (0.6x Git Bash) and the short rows.

**Decision:** `tools/benchtimer` (C#, built with `dotnet build tools/benchtimer -c Release -o
tools/benchtimer/bin`) launches each shell straight from CreateProcessW, as Claude Code launches
a shell, and times CreateProcessW-to-exit on the high-resolution clock. NUL for stdin/stdout/
stderr, only that handle inherited, the timer's console inherited (no console created per
launch; it refuses to run with no console). One untimed launch first. Any non-zero exit fails
the run. `compare3.sh` still drives (from Git Bash) and fails loudly if the timer is not built.
Git Bash's path is configured, not searched (rev 138).

**Verified before switching:** every row's output from timer-launched Git Bash equals its
output from Git Bash as before; a launch exiting 3 fails the run; timer-launched Git Bash still
resolves `/usr/bin` tools.

**First run (rev 136 deployed):** startup C#Bash 0.0152 / Git Bash 0.0162 / WSL 0.1036; find
0.020 (was 0.047); coreutils 0.055 (was 0.091). Rows before this entry are not comparable.

**Not chosen:** CREATE_NO_WINDOW per launch (what Claude Code does today): it allocates a conhost
per launch, ~10 ms for every shell alike, and is Claude Code's defect (issue filed), not a shell
cost.

## 2026-10-08 -- BUILT: `df` in-process, GNU's format over Windows volumes (goal-2 backlog item 5)
**Status:** Active. Greenlit 2026-10-04 ("Yes, then 2, 3 and 5 ..."); the architect, 2026-10-08:
"ok, let's get df done". Plan as stated then: in-process, GNU-format output, checked against WSL.

**Context:** `df` resolved to Git's `usr/bin/df.EXE`, which lists only PortableGit (as `/`) and
`A:` here: no C:, E: or F: rows. Without Git on PATH there was no `df` at all. The 2026-10-04 scan
found PowerShell detours for it.

**Decision:** `df` is a coreutil in `Builtins.Sys.cs`. Choices, each settled by an existing rule:
- **Which volumes:** with no operand, every drive letter A-Z whose volume is ready
  (`GetLogicalDrives`, `GetVolumeInformationW`). A drive with no media, or access denied, is
  skipped silently, as GNU skips a mount it cannot access (ENOENT/EACCES) when listing; any other
  failure is reported (`df: 'X:/': <message>`, status 1). With operands: the volume holding each
  one (`GetVolumePathNameW`), so a folder mount point or a UNC share works.
- **Filesystem column:** `C:` for a drive letter (the MSYS and Cygwin convention); `//host/share`
  for a mapped network drive (`WNetGetConnectionW`), which is how a Linux cifs mount shows its
  source; the root itself for a UNC or folder-mount operand.
- **Mounted on:** the shell's forward-slash path form, `C:/` (DECISIONS 2026-09-11/12).
- **Type (`-T`, `-t`, `-x`):** the volume's file system name in lower case (`ntfs`, `fat`, `exfat`).
  For a network drive that is the server's file system, not the transport (Linux would say
  `cifs`): it is what Windows reports, nothing invented. `-t`/`-x` compare exactly, as GNU does.
- **Numbers:** `GetDiskFreeSpaceExW`: size = total, used = total - free, available = free to the
  caller (quota-aware). Use% = used / (used + available), rounded up; `-` when both are 0.
- **Rounding, headers, widths:** ported from coreutils 8.32 and gnulib, not approximated:
  `human_readable` (ceiling, one decimal under 10, `1.0` when rounding up reaches the base),
  `humblock` for `-B` / `--block-size` / `DF_BLOCK_SIZE` / `BLOCK_SIZE` / `BLOCKSIZE` /
  `POSIXLY_CORRECT`, `get_header` (the base that states the block size exactly: `1K`, `1M`,
  `1B`, `512B`, `1kB`), per-field minimum widths, no padding after a left-aligned last column.
- **Options:** `-h -H/--si -k -B -P -T -t -x -l --total --output=LIST -v --sync --no-sync`. The
  last of `-h`/`-H`/`-k`/`-B` wins (GNU's rule), so `Opts` gained `Sequence`, every short option in
  command-line order. `-l` skips a remote drive BEFORE any network I/O, as GNU does: `df -l`
  0.36 s -> 0.019 s here (9 mapped shares).
- **Loud (decision 2, so a PATH `df` takes over under `auto`):** `-i`, an `--output` list with
  inode fields, bare `--output` (all fields, inodes included), and `-a`. Windows has no inode
  counts; `-a` would mean inventing rows for drives that cannot be read.
- **Messages:** GNU's text with ASCII apostrophes (GNU under `LC_ALL=C`), as C#Bash's other tools
  print them; operands quoted only when the shell would need it (gnulib `quotef`).

**Verified:**
- `tests/df/compare-wsl.sh` (new, manual, run from Git Bash): 76 cases, each run by GNU df 8.32 in
  WSL over `/mnt/<d>` and by C#Bash over `<D>:/`, on four quiet local volumes. Covers every unit
  option and `-B` form, header modes, `--total`, `--output` orders, filters, env variables and
  every error path. **76/76 identical** (stdout, stderr, status) on the Release and the AOT build,
  after normalising only the by-design names (`G:\` / `G:`, `/mnt/g` / `G:/`, `9p` / `ntfs`).
  Polarity: it reported differences when it was mis-run, and a real one (an unnormalised type name)
  until fixed.
- Suite case `df` (shape on C:, machine-independent): 72/72 with a console and `-Detached`, on
  the Release and the AOT build. Control: the case fails on rev 136 (Git's df).

**Known limits:**
- Folder mount points without a drive letter are not listed with no operand (an operand inside
  one works).
- A full listing does network I/O for every mapped drive: 0.37 s here for 9 shares, local drives
  under 1 ms each. A dead share can block for the SMB timeout, as a dead NFS mount blocks GNU df;
  `df -l` avoids it.

**Not chosen:**
- **Enumerate volumes with `FindFirstVolumeW`** (would list folder mount points): it does not
  cover mapped network drives, so two enumerations to merge, for a rare case.
- **`/c`-style mount points** (what Git's df prints): contradicts the ratified path form.
- **Filesystem as the volume GUID path** (the closest analogue of a Linux device): unreadable,
  and the drive letter is what Windows users and tools name.

## 2026-10-08 -- DEFECT FIXED: literal text was field-split (`a\ b/x*` became two words)
**Status:** Active. The architect: "Yes" (start the bug list with this one).

**Defect:** `ExpandToFields` split LITERAL text on IFS characters, the treatment bash gives only to
the results of expansions. It showed whenever a word missed the single-literal fast path, which a
glob character does: `a\ b/x*` gave `a` and `b/x*` (GNU: the two files), with `*`, `?` and `[ ]`
alike, matched or not. Also `IFS=:; echo ab:c$v` split at the colon, and `~` split when the home
directory contains a space (POSIX: a tilde result is not field-split), which a Windows profile
such as `C:/Users/John Smith` does even with no glob. Silent wrong arguments in every case. Found
2026-10-06 as a side observation, probed 2026-10-08.

**Fix:** `AppendText` takes `split`; a `LiteralPart` is appended unsplit but stays glob- and
brace-eligible; a `TildePart` appends the home directory as quoted text (`TildeHome`) and its
suffix as literal text. Expansion results split exactly as before.

**Verified:** new case `literal_no_split` (expected = GNU bash via WSL; 16 lines fail on the
deployed rev 143 build, including all three tilde lines); suite 73/73 with a console and
`-Detached`; compat battery 227/237 against Git Bash, exit 0 (no baseline probe regressed; the
10 diffs are the known environment and by-design ones).

**Not changed (seen while testing):** a pattern like `a:b*` globs as a DRIVE-RELATIVE path
(drive A:'s current directory), which is Windows path syntax; on Linux it is a file name. Not
raised; noted because a test line hit it on this machine, where A: is a mapped share.

**Known limitation kept:** glob quoting is still per field, so a home directory containing `[ ]`
or `*` is matched as a pattern when the rest of the word globs.

## 2026-10-08 -- DEFECT FIXED: a declaration builtin's `name=$v` argument was field-split
**Status:** Active. The architect: "go" (the next bug on the list).

**Defect:** `local a=$1 b=$2` with `"x y"` set `a=x` and declared a second variable `y` (and
printed "declare: `@D': not a valid identifier" for a value like `-P @D`). The same in `declare`,
`typeset`, `export` and `readonly`, and for `name+=` and `name[i]=`. Bash expands an
assignment-shaped argument of a declaration builtin like an assignment: one field, no splitting,
no globbing. Silent wrong values; found 2026-10-08 when it broke the df comparison harness.

**Fix:** the parser, which already knew the declaration builtins (it folds `local arr=(…)`), turns
an argument that `IsAssignment` or `IsArrayElementAssign` accepts into an `AssignmentArgPart`
when the command word is literally one of them. The expander gives it one unsplit, unglobbed
field (`name=value`, `name+=value`, `name[index]=value`). As in bash, the rule is lexical:
`cmd=local; $cmd z=$1` still splits, and so does a non-assignment argument (`local $v` with
`v="p=1 q=2"` declares both). `local arr=($1)` is unchanged (it splits inside the parentheses).

**Verified:** new case `declaration_assign` (expected = GNU bash via WSL; 10 lines fail on the
deployed rev 143 build); suite 74/74 with a console and `-Detached`; compat battery 227/237 against
Git Bash, exit 0, no baseline regression.

**Found while testing, NOT fixed (separate defects, added to the list):**
- A `~` in an assignment value is never expanded: `x=~/y` stays `~/y` (GNU: `$HOME/y`; also after
  `:`, `x=a:~/b`). Every assignment, not only declaration builtins.
- `local j` with no value gives `j` an empty value; bash declares it and leaves it unset, so
  `${j-unset}` prints `unset` there and nothing here. `declare k` at the top level is right.

## 2026-10-08 -- DEFECT FIXED: the word of `${x:-word}` / `${x:+word}` lost its quoting context
**Status:** Active. The architect: "go" (the next bug on the list, reported as `"${v:+$v }"`
dropping its trailing space).

**Defect, wider than reported:** `ExpandOperatorWord` parsed the word as separate words, expanded
each into fields and re-joined them with single spaces, detached from the context of the `${ }`.
A 22-case comparison with GNU showed ten wrong:
- inside "...": trailing and leading blanks lost (`"${v:+$v }"`, `"${v:+ --flag $v}"`), a value's
  blanks collapsed (`"${u:-$w}"` with `w='p  q'`), and `'...'`, `\ ` and `~` processed where bash
  keeps them literal;
- unquoted: the word's own quotes stopped protecting: `${u:-a "b c" d}` gave four fields, not three;
  `${u:-"f*"}` globbed; `${v:+-o "$w"}` split `"$w"`, the common `${var:+--opt "$var"}` idiom, and
  `${1+"$@"}` broke arguments containing spaces. Silent wrong arguments in every case.

**Fix (bash's rule: the word is expanded in the same context as the `${ }`):**
- In a list of fields, `ExpandToFields` asks `TryOperatorWord` whether `-`/`+` selects the word
  and, if so, expands the word IN PLACE (`ProcessOperatorWord`). Inside "...": the word is parsed
  as double-quoted text (`Parser.ParseDoubleQuotedText`) after its top-level `"` are removed (they
  only toggle quoting there). Unquoted: the word is cut at its top-level blanks (`SplitAtBlanks`,
  aware of quotes, `$( )`, `${ }`, backquotes, escapes); blank runs split per IFS like any
  expansion result, and each word's parts keep their own quoting.
- In a string context (assignment values, `:=`, `?`), `ExpandOperatorWord` uses the same parsing
  and joins the pieces with the word's own blanks, nothing split or globbed.
- The expander knows it is inside "..." through `_inDoubleQuotes`, saved and restored around each
  quoted part and cleared inside `$( )` / `<( )`, whose command has its own quoting.
- Unchanged: `$@`/`$*` with operators, whole arrays (`${a[@]:-x}`), lengths, indirection.

**Verified:** new case `operator_word` (36 lines, expected = GNU bash via WSL; 16 fail on the
deployed rev 143 build); suite 75/75 with a console and `-Detached`; compat battery 227/237 against
Git Bash, exit 0, no baseline regression.

**Approximation kept:** in an unquoted operator word, an IFS character that is not a blank (with
`IFS=:`, `${u:-a:b}`) does not split it; bash would. Literal text cannot tell an escaped `\:` from a
plain `:` after parsing, and splitting it would break the escaped form. Not measured: the speed
cost of the flag save/restore on each part in the slow expansion path (the benchmark was not run).

## 2026-10-08 -- DEFECT FIXED: assignment values never expanded a tilde (`x=~/y` stayed `~/y`)
**Status:** Active. The architect: "go" (the next bug; found 2026-10-08 while fixing `local`).

**Defect:** bash expands a `~` at the start of an assignment value, and after each unquoted `:`
in it, when followed by `/`, `:` or the end of the value (`x=~/y`, `PATH=$PATH:~/bin`, `x=~`).
C#Bash expanded none of them: prefix assignments, `local`/`export`/`declare` arguments and
`a[0]=` values all kept the literal `~`. Silent wrong paths, in a shape Claude writes for PATH and
config variables. Array literals (`arr=(~/a)`) were already right: their elements are words.

**Fix:** tilde expansion is lexical, so the parser does it where it builds an assignment value
(`IsAssignment`, `IsArrayElementAssign`): `WithAssignmentTildes` turns each qualifying `~` of a
literal part into a `TildePart`. A quoted or escaped tilde is not a literal part and stays; so do
`a~b`, `~"/q"` and `~user` (unsupported, as elsewhere).

**Verified:** new case `assignment_tilde` (17 lines, expected = GNU bash via WSL; 10 fail on the
deployed rev 143 build); suite 76/76 with a console and `-Detached`; compat battery 227/237 against
Git Bash, exit 0, no baseline regression.

**Still open, next to this:** the home directory itself comes from `$HOME`, which C#Bash keeps in
the inherited backslash form (`C:\Users\guy`), so `x=~/y` gives `C:\Users\guy/y` here (the open
`$HOME` defect).

## 2026-10-08 -- DEFECT FIXED: a declaration with no value left the variable set (`local j` was "")
**Status:** Active. The architect: "go" (the next bug; found 2026-10-08 while fixing `local a=$1`).

**Defect:** `local j` stored `j=""`, so `${j-dflt}`, `${j+x}`, `[[ -v j ]]` and `declare -p j`
(`j=""`) all saw a set variable; bash declares it in the function's scope, unset, hiding an outer
`j`. Found in the same family, compared with GNU:
- `export m` on an unset `m` put `m=""` into every child's environment (bash: nothing), and
  `export -p` did not list it (bash: `declare -x m`);
- an array with no element 0 read as set: `local -a arr; ${arr-unset}`, `declare -a a1; [[ -v a1 ]]`,
  `a3=([1]=x)` (bash: `-v`, `${a-}` and `${a+}` on an array name mean element 0).
`typeset j` / `declare j` in a function were already right (they create nothing).

**Fix (ShellEnvironment):** a frame entry `DeclaredUnset`, a sentinel string compared by reference,
marks a name declared with no value: it hides outer frames, `Get` reads "" and `IsSet` false.
`DeclareLocal` adds it (keeping a value already declared in that frame, as bash does); assignment
overwrites it in place. `IsDeclared` (declared, set or not) serves `declare -p name` and `unset`'s
variable-or-function choice. `GetExportedVars` passes only exported names that have a value;
`ExportedNames` lists every exported name for `export -p`. `IsSet` on an array name checks element
0 (associative: key "0"). A temporary assignment (`x=v cmd`) on a declared-unset local restores the
declared-unset state; restoring by `Unset`, which clears every frame, would have deleted an outer x.

**Verified:** new case `declared_unset` (27 lines, expected = GNU bash via WSL; 15 fail on the
deployed rev 150 build); suite 77/77 with a console and `-Detached`; compat battery 227/237 against
Git Bash, exit 0, no baseline regression.

**Found, NOT fixed (separate):** `set -u` (nounset) is accepted and never enforced. The option is
stored (`ShellOptions.UnsetError`) but nothing reads it: `set -u; echo "$nope"` prints an empty
line and continues where bash stops with `nope: unbound variable`. Scripts relying on
`set -euo pipefail` to catch a typo run on silently.

## 2026-10-08 -- DEFECT FIXED: `set -u` (nounset) is enforced; and `${x:-word}` expanded its word eagerly
**Status:** Active. The architect: "sure, go" (Claude proposed it next: a safety check scripts rely
on, silently missing).

**Defect:** `set -u` / `set -o nounset` set `ShellOptions.UnsetError` and nothing read it, so
`set -euo pipefail` scripts expanded unset names as "" and ran on. A 35-case comparison with GNU
fixed the semantics before any code was written.

**Rule, as measured against bash 5.1:**
- Expanding an unset parameter is an error ending a non-interactive shell: `$x`, `${x}`, `$1`, `${#x}`,
  `${a[1]}`, `${m[k]}`, `${x#p}`, `${x:0:1}`, arithmetic (`$((n+1))`, `(( z > 0 ))`, `let`), a
  `for` list, a test operand. Messages: `x: unbound variable`, `$1: …` for `$1`, `10: …` for `${10}`.
- Never an error: `${x-…}` `${x:-…}` `${x+…}` `${x:+…}` `${x=…}` (and `${x?…}`, its own error), `$@`, `$*`,
  `"${a[@]}"` empty or unset, `${#a[1]}` (0), `[[ -v x ]]`.
- Only the unit fails, and the shell goes on: a `( )` subshell (status 1), a `$( )` (status 1), a
  pipeline stage, a here-document or here-string (the command fails with status 1).
- Exit status of the shell: 1 for a script file; under `bash -c`, 127, or 1 where `set -e` is IN
  FORCE (on, and not suspended inside a command followed by `&&`/`||`, an `if` condition, …).
  Claude Code's wrapper runs `eval '<cmd>' < /dev/null && pwd -P`, so there it is 127 even with
  `set -e` inside the command: measured.

**Fix:**
- `WordExpander.GetChecked` (the parameter value, or `Unbound(name)`) at every expansion site: `$x`,
  the `${x}` fast paths, `${#x}`, the arithmetic resolver (`ArithVars.Get`, whose catch-all no
  longer swallows it) and `$name` inside arithmetic text and subscripts. `ExpandBraceParam` checks
  subscripted and operator forms after the `-=+?` family; whole arrays are exempt.
- `FatalShellException.ExpansionError` marks unbound and `${x:?}` errors. `RunStatement` records
  on the `ExitException` whether errexit was in force (`_errexitSuppress`); `Program` (the `-c`
  path) turns an expansion exit into 127 unless it was. `${x:?}` follows the same rule, as in bash.
- `ExpandRedirectBody`: an expansion error in a here-document or here-string fails the command
  (`RedirectException`, status 1) instead of ending the shell.
- **Also fixed (needed, or `set -u` would raise false errors):** the word of `${x:-word}` /
  `${x:=word}` / `${x:+word}` / `${x:?word}` was expanded BEFORE the operator decided whether it
  was needed, so `${x:-$(cmd)}` ran `cmd` with x set, and under `-u` `${v:-$undefined}` failed
  with v set. It is expanded only when selected now (case lines 36-38).

**Verified:** new case `nounset` (49 lines, expected = GNU bash via WSL, each case in its own shell,
messages normalised; 41 differ on the deployed rev 150 build); suite 78/78 with a console and
`-Detached`; compat battery 227/237 against Git Bash, exit 0, no baseline regression.

**Not covered:** `$!` before any background job (bash calls it unbound; C#Bash treats it as set),
`${!x}` with x unset, and the readonly-assignment error (GNU continues a SCRIPT after it, C#Bash
exits; unchanged, separate).

## 2026-10-08 -- DEFECT FIXED: `$HOME`, `~`, `$BASH` and `command -v` / `type` / `which` / `hash` gave backslash paths
**Status:** Active. The architect: "go" (the next bug). Applies the ratified rule of DECISIONS
2026-09-11/12 ("If Bash is handed a windows form path (using backslash), it must convert it to a
forward slash path before using"), which until now covered `pwd`, `$PWD`, `$OLDPWD`, `dirs`,
`mktemp` and `realpath` only.

**Defect:** `HOME` arrived from Windows as `C:\Users\guy`, so `$HOME`, `~`, `~/x` and `x=~/y` (rev
150) gave `C:\Users\guy/y`; `$BASH` and a default `$SHELL` were `Environment.ProcessPath` as is; and
`command -v`, `command -V`, `type`, `type -p`, `type -a`, `which` and the `hash` listing printed
`F:\Tools\PortableGit\mingw64\bin\git.EXE`. Mixed or backslash paths that the shell's own string
operations misread (the reason the rule exists).

**Fix:** the shell converts the paths IT produces: `HOME` once at startup (it is exported, so
children see `C:/Users/guy`, which Windows programs accept), `BASH`/`SHELL` when it sets them, and
tilde expansion always (a `HOME` the user sets with backslashes still yields `C:/…` from `~`, while
`$HOME` itself keeps what was set). The lookup output goes through `Builtins.Shown`; the lookup
itself (`ResolveOnPath`) keeps the native form, which is what launches the program (a `.cmd` run
through cmd.exe with a forward-slash path is a risk not worth taking for display). A path the user
gives (`type -p ./x`) prints as given. Other inherited Windows variables (`USERPROFILE`, `TEMP`,
`APPDATA`, PATH's native `C:\x;…` form) are left alone: they are Windows', not the shell's.

**Verified:** new case `shell_paths` (17 lines, authored: Windows-specific values, no GNU oracle;
13 fail on the deployed rev 150 build); suite 79/79 with a console and `-Detached`; compat battery
227/237 against Git Bash, exit 0, no baseline regression.

**Found while writing the test (separate, added to the list):**
- In a `case` or `[[ == ]]` pattern, an unquoted `\\` does not match a backslash: `case 'a\b' in
  *\\*)` and `[[ 'a\b' == *\\* ]]` fail where GNU matches (`*[\\]*` and `*"\\"*` work). The first
  version of this test's check used that form and could not fail; found by running it on the old
  build as a control.
- `command -v git` prints `git.EXE` (the PATHEXT spelling) for the on-disk `git.exe`.
- `hash -t name` prints nothing; the `hash` listing is paths only, not bash's `hits command` table.

## 2026-10-08 -- DEFECT FIXED: `env`'s options did not reach the command it runs
**Status:** Active. Reported by koliada-net-f7 (2026-10-10: `env "STDOUT.OutputLevel=trace" <app>`,
the documented way to set every Koliada STDOUT program's verbosity, failed, so the workaround was
PowerShell); the architect: "yes". Includes the older open item "`env -u VAR cmd` does not remove VAR
for cmd" (2026-10-04), which lives in the same function.

**Defects (each compared with GNU env, 21 cases):**
- a NAME=VALUE whose NAME was not a shell identifier (`A.B=1`, `X-Y=2`, `A B=1`) became the COMMAND
  (`A.B=1: command not found`, 127). GNU: every leading operand containing `=` is an assignment;
  only the shell's own `NAME=value cmd` prefix is limited to identifiers (unchanged, correct);
- `-u NAME` was applied only when env printed the environment, never to a command, builtin or program;
- `-i` reached only external programs: `env -i printenv` printed the whole environment;
- `-uNAME`, `--unset=NAME` and `--unset NAME` were refused as invalid options; `-` (GNU's `-i`)
  ran `-` as the command; `-0` was accepted and ignored.

**Fix:** `Env` parses the GNU forms and treats any operand containing `=` as an assignment (`=x`,
an empty name, is accepted and sets nothing: Windows has no empty-named variable). `RunCommand`
takes the unset list. For an external program, `ExecExternal` removes the names from the child's
environment block. For a builtin or function, `env -i` / `env -u` unexport the names for the
duration (what such a command sees as its environment: printenv, env, the programs it starts) and
re-export them after; `env`'s assignments are applied on top, as before. Live check: `cmd /C` sees
`%A.B%` and `%STDOUT.OutputLevel%`, and `env -u FOO cmd` does not see FOO.

**Verified:** new case `env_options` (21 lines, expected = GNU env via WSL; 18 fail on the deployed
rev 150 build); suite 80/80 with a console and `-Detached`; compat battery 227/237 against Git Bash,
exit 0, no baseline regression.

**Known, kept:** a FUNCTION run by `env -u X f` still sees `$X` as a shell variable (only the export
is hidden); GNU env cannot run a function at all. `env` lists in the shell's internal order, not the
order variables were added (GNU's), so the test sorts.

**Incident while testing:** the first run of the comparison printed C#Bash's whole environment
(`env -i A=1 printenv`, the `-i` defect itself) into the session transcript, including an API key.
The cases now never print an environment: they count lines or ask for named variables.

## 2026-10-08 -- DEFECT FIXED: an unquoted `\\` in a pattern did not stand for a backslash
**Status:** Active. The architect: "next; go". Found 2026-10-08 when the check in `shell_paths`
(`case $v in *\\*)`) could not fail: run against the old build as a control, it passed there too.

**Defect:** the lexer drops the escaping backslash and keeps the escaped character as literal text,
except for `* ? [ { } , ~`, which it marks so the parser makes them quoted parts (2026-10-03). A
backslash was not in that set, so `\\` reached a pattern as a lone `\`, which the matcher reads as
"escape the next character". The hypothesis was tested before the fix by its sharpest consequence:
`case 'a*' in *\\*)` MATCHED in C#Bash (the pattern meant "ends in a literal `*`"); GNU does not.
Wrong in `case`, `[[ == ]]` (`*\\*`, `a\\b`, `a\\?`) and `[[ =~ \\ ]]` ("invalid regular expression");
right already in the `${p//\\//}` family, which reads the raw text, and when `\\` ended the pattern.
Silent wrong branch.

**Fix:** `\\` joins the lexer's marked set, so an escaped backslash is a quoted one-character part:
patterns escape it (`EscapeGlob`), regexes quote it, and every other use still yields one backslash.

**Verified:** new case `backslash_pattern` (29 lines, expected = GNU bash via WSL: 18 pattern cases,
6 of which fail on the deployed rev 156 build, and 11 other uses of `\\` in words, unchanged and
identical to GNU on both builds); suite 81/81 with a console and `-Detached`; compat battery 227/237
against Git Bash, exit 0, no baseline regression.

## 2026-10-08 -- DEFECT FIXED: an MSYS program wrote nothing under `>>` or under the shell's own redirect
**Status:** Active. The architect: "go all" (the remaining list). Found 2026-10-08 as "a coreutil
that defers to a PATH external loses its output into a file" (awk with `getline` falling back to
Git's awk, `> out.txt`); isolated to a wider cause.

**Defect:** every MSYS2 program (Git for Windows' `usr/bin`: sed, awk, perl, ssh, tar, find, ...)
failed every write with "Bad file descriptor", status 1, an empty file, when given a file by
`>>` (`tool >> f`) or by the shell's own redirect (`{ tool; } > f`, `{ tool; } >> f`, a builtin's
`> f` when the builtin falls back to the program). A Windows program was fine; so was a plain
`tool > f`. Those handles are opened `FILE_APPEND_DATA` without `FILE_WRITE_DATA` (DECISIONS
2026-10-04: atomic appends, so concurrent appenders interleave, as O_APPEND does), and MSYS derives
an inherited handle's mode from FILE_WRITE_DATA: without it the descriptor is read-only.

**Decision (settled from the record, not a new design):** keep the ratified append-only handle for
Windows programs, which honour it; give an MSYS program an ordinary write handle positioned at the
end (`ChildLauncher.Access.AppendAtEnd`), the most it can use. `ChildLauncher.IsMsysProgram`: the
program's directory holds `msys-2.0.dll` or `cygwin1.dll` (an MSYS program loads it from there),
cached per directory.

**Alternatives:** a write handle for every program (simple, but drops the ratified atomic append for
native concurrent appenders); leave it (MSYS output lost). Cost accepted: two MSYS programs appending
to one file at the same time share no atomicity.

**Verified:** new case `msys_redirects` (authored, needs Git for Windows; 6 of 7 lines fail on the
deployed rev 156 build, the Windows-program control line passes on both); suite 82/82 with a console
and `-Detached`; compat battery 227/237, exit 0, no baseline regression.

## 2026-10-08 -- DEFECTS FIXED: PATH lookups printed PATHEXT's spelling; `hash` ignored -t / -l / -d and had no hits
**Status:** Active. The architect: "go all".

**Defects:** `command -v git`, `type`, `which` and the `hash` listing printed `git.EXE`, the PATHEXT
spelling the lookup appended, for the file `git.exe`. `hash -t NAME` printed nothing, `-l` and `-d`
were ignored, and the listing was bare paths, not bash's `hits command` table.

**Fix:** `Builtins.Shown` (display only; the cached path that launches the program is untouched)
gives a found program its on-disk name (one directory lookup, only when something is printed). The
PATH cache counts executions (`HashHit` in `ExecExternal`); `hash` prints `hits<tab>command` with
`%4d` counts, `-t` (one name: the path; several: `name<tab>path`; not hashed: an error, status 1;
counted as a hit, as bash does), `-l` (`builtin hash -p PATH NAME`), `-d`, `-p`, `-r`; a builtin or
function name is skipped with status 0. Formats measured against GNU bash 5.1 in WSL.

**Verified:** new case `command_lookup` (authored: Windows paths; fails on the deployed build); suite
83/83 with a console and `-Detached`; compat battery 227/237, exit 0, no baseline regression.

**Known, kept:** the table also holds names resolved by `type` / `command -v` (bash hashes only what
it runs), shown with 0 hits.

## 2026-10-08 -- DEFECT FIXED: a stray `fi` / `done` / `esac` / … ran as a command
**Status:** Active. The architect: "go all".

**Defect:** with no construct open, a closing reserved word (`fi`, `done`, `esac`, `then`, `do`,
`else`, `elif`) was parsed as a simple command: `echo a; fi` printed `a`, then
`fi: command not found`, status 127, and `bash -n` passed the file. bash: "syntax error near
unexpected token `fi'", status 2 (script, `-c`, `-n`, `eval`), nothing of that line run.

**Fix:** the top-level `Parser.Parse` loop rejects an unquoted closing word before parsing a list.
Inside a construct the existing terminator logic already applied; a quoted `'fi'` stays an ordinary
command name, as in bash.

**Verified:** new case `stray_keyword` (16 lines, expected = GNU bash via WSL, messages normalised to
token and status; 12 differ on the deployed build); suite 84/84 with a console and `-Detached`;
compat battery 227/237, exit 0, no baseline regression.

**Known divergence, unchanged (separate, larger):** bash parses a script line by line and runs what
precedes a syntax error (`echo before` / `fi` prints `before`, then fails); C#Bash parses the whole
file first, so a syntax error anywhere runs nothing. The same was already true of a stray `}`.

## 2026-10-08 -- DEFECTS FIXED: `date +%3N` printed 9 digits; `date -d` refused a fractional `@epoch`
**Status:** Active. The architect: "go all".

**Defects:** `%N`'s width was applied as left padding, so `%3N` printed all nine digits (GNU: a
precision, the first 3: milliseconds). `-d @1700000000.5` was "invalid date" (whole seconds only).

**Fix, to GNU's measured rule:** `%wN` is the first w digits (default 9; zeros on the right past 9);
the trailing zeros are its padding, so `-` drops them and `_` turns them into spaces, keeping at least
one digit (`%-N` of .5 s is `5`); `0` and no flag keep them. `@[-]S[.F]` takes a fraction, negative
epochs counting back from 1970 (`@-1.5` is second -2, fraction .5, as GNU prints).

**Limit:** .NET time keeps 100 ns, so fraction digits past the 7th are dropped (`@1.123456789` prints
`%N` as 123456700; GNU 123456789). The test uses at most 7 digits.

**Verified:** new case `date_nanos` (11 lines, expected = GNU date via WSL; 9 differ on the deployed
build); suite 85/85 with a console and `-Detached`; compat battery 227/237, exit 0, no regression.

## 2026-10-08 -- DEFECTS FIXED: `fold` counted `\r`, `\b` and tabs as one column; `tee /dev/null` failed
**Status:** Active. The architect: "go all".

**Defects:** `fold` cut each line into fixed runs of characters, so a `\r` (column back to 0), `\b`
(one back) and a tab (to the next multiple of 8) each counted as one column, `-s` broke at the wrong
place, a final line without a newline got one, and UTF-8 counted characters where GNU fold counts
bytes. `tee /dev/null` (and `/dev/stdout`, `/dev/stderr`) translated the name to a file path and
failed: "No such file or directory", status 1.

**Fix:** `fold` is a port of coreutils fold.c (its `adjust_column`, the "this byte would make the
line too long" rule, `-s`'s rescan after the last blank), over bytes, streamed in 8 KB reads so
`yes | fold | head` still ends on the closed pipe (statuses 141 141 0, as GNU). `tee` takes the
three device names as the redirects do: `/dev/null` is no output, `/dev/stdout` and `/dev/stderr`
are extra copies to the shell's own streams.

**Verified:** new case `fold_tee` (20 lines, expected = GNU coreutils via WSL; 13 differ on the
deployed build); suite 86/86 with a console and `-Detached`; compat battery 227/237, exit 0.

## 2026-10-08 -- DEFECT FIXED: a Windows program's exit code reached `$?` unreduced (-1, 256, NTSTATUS)
**Status:** Active. The architect: "go meant 'still open' gogogo". Open since 2026-09-14.

**Defect:** `$?` after a native program was its raw 32-bit exit code: `-1`, `256`, `-1073741819`.
A shell status is 0-255. (The shell's own `exit`/`return` statuses were already reduced.)

**Measured first:** the record said "bash gives 255" for -1; that was never measured. Git Bash's
runtime gives 127 for -1, for Ctrl+C (0xC000013A), divide by zero and stack overflow, and maps only
an access violation (139, printing "Segmentation fault") and an illegal instruction (132). 127 reads
as "command not found".

**Decision (the ratified rule of 2026-10-03: GNU bash on Linux, not Git for Windows' quirks):** the
status bash on Linux would report for the same outcome. `Evaluator.ShellStatus`: an NTSTATUS crash
is 128 + its signal (access violation and stack overflow 139 SIGSEGV; illegal and privileged
instruction 132 SIGILL; integer and float divide by zero, integer overflow 136 SIGFPE; Ctrl+C exit
130 SIGINT; breakpoint 133 SIGTRAP; fail-fast 0xC0000409 134 SIGABRT); any other code its low byte
(-1 is 255, 256 is 0, 1000 is 232). Applied where every external's exit code is read, so pipelines,
subshells, `$( )` and `wait` follow.

**Not done:** bash also prints "Segmentation fault" (etc.) for a signalled foreground child; C#Bash
prints nothing extra.

**Verified:** new case `exit_status_range` (authored: Windows exit codes via `cmd /c exit N`; the
deployed build prints the raw codes); suite 87/87 with a console and `-Detached`; compat battery
227/237, exit 0, no baseline regression.

## 2026-10-08 -- DEFECT FIXED: an unquoted `${…${…}…}` swallowed the rest of the script, silently
**Status:** Active. The architect: "gogogo" (the open list: Git's `__git_ps1_show_upstream` "fails to
parse"); then "this is the second time ${ has been a bug!!".

**Defect:** the lexer reads an unquoted `${…}` as one raw token by counting braces. At a nested `${`
it raised the depth at the `$` and left the `{` for the next pass, which raised it again: two
openings, one closing. The depth never returned to 0, and the token took everything up to the end
of the file, with no error. `x=${1:-${HOME}}`, `${a#${b}}`, `${a%${b}c}`: nothing after them ran (a
script printed nothing, status 0). Inside double quotes a different reader is used, which is why it
went unseen. Git's git-prompt.sh (`svn_upstream=${svn_upstream#${svn_remote[$n]}}`) "failed to
parse" this way: the loop's `done` had been swallowed.

**Fix:** the nested `{` is consumed with its `$` (one level); an unclosed `${` at end of input is a
loud error (`LexException`, Incomplete for the interactive prompt) instead of the rest of the file.

**Verified:** new case `nested_brace` (expected = GNU bash via WSL; the deployed build prints nothing
at all); git-prompt.sh parses (`-n` 0), loads, and `__git_ps1 "[%s]"` prints the branch; suite 88/88
with a console and `-Detached`; compat battery 227/237, exit 0, no baseline regression.

**Why it recurs (the architect's point):** `${…}` is matched by several separate hand-written
scanners (the lexer's raw token, the double-quote interior parser, `ExpandVarsInArith`,
`ExpandSubscript`, and `SplitAtBlanks` / `RemoveTopLevelDoubleQuotes` from rev 148), each with its
own brace and quote counting. A bug class fixed in one can remain in the others. Audited next.

## 2026-10-10 -- One construct matcher (`Lexer.ConstructEnd`) for `${ }`, `$( )`, `$(( ))`, backquotes and quotes; `{` / `}` are words after the command name
**Status:** Active. The architect, after rev 165: "this is the second time ${ has been a bug!!"
(that entry's last paragraph names the cause: several hand-written scanners).

**Context:** where a `${…}`, `$(…)`, `$((…))`, `` `…` `` or quoted string ends was decided by seven
separate scanners, each with its own brace, parenthesis and quote counting: the lexer's raw `${`
token, the lexer's `"…"` scanner, the parser's `ParseDoubleQuotedInterior`, and in `WordExpander`
`SkipConstruct`, `ExpandVarsInArith`, `ExpandSubscript`, and `SplitAtBlanks` /
`RemoveTopLevelDoubleQuotes`. A 42-case audit (each case in its own shell, so one swallowing bug
cannot hide the rest; expected = GNU bash via WSL) found 13 still wrong on rev 165:
`${u:-\}}` kept the backslash, quoted and unquoted; `${a//\}/x}` and `${a/b/\}}` ended at the
escaped brace; `${a:-{x}}` dropped the `}` (a bare `{` was counted as a level); `${u:-"a\"b"}`
was an error; `${u:-$(echo })}` was an error unquoted and printed `)}` quoted; `` ${u:-`echo }`} ``
ran `}` as a command; `"${u:-'a}b'}"` and `"${u:-"a}b"}"` closed at the inner `}`;
`${u:-${a:+$(echo })}}` printed its source text.

**Decision:** one matcher, `Lexer.ConstructEnd(s, i)`, is bash's single rule (parse.y,
`parse_matched_pair`). It returns the index just past the construct at `i`, or -1 if it never
closes. A backslash skips the next character. Quotes, backquotes, `$'…'`, `$( )` and `${ }` inside
are matched as units, so a `}` or `)` in them closes nothing. Within `${…}` only a `${` opens a brace
level; within `$( )` a bare `(` does. All seven scanners call it; an unclosed construct is
`Unterminated …` in the lexer, and runs to the end of the text where the text has already been
lexed. Two related changes were needed for the audit cases:
- `{` and `}` are reserved only where a command starts, so after the command name they are
  ordinary words glued to adjacent text (`echo }`, `echo a } b`, `$(echo })`). Before, `echo }`
  was a syntax error, so `${u:-$(echo })}` could not run.
- In a double-quoted operator word, `\}` is `}`, which `RemoveTopLevelDoubleQuotes` now applies.

**Alternatives considered:**
- **Patch the 13 cases in place**, in whichever scanner each one hit. Rejected: that is how the
  defect came back after rev 165. Each scanner would keep its own counting, and the next construct
  would be wrong in the ones not patched.
- **A real recursive word parser** that keeps the parsed structure, instead of re-scanning raw
  text in the expander. This is the larger and more correct change, but it touches every
  expansion path. A shared end-matcher removes the divergence, which is the defect class, at a
  fraction of the risk. Revisit if a construct needs the parse itself, not just where it ends.

**Verified:** all 42 audit cases now match GNU. New case `brace_constructs` (the 42 cases plus a
line of `{` / `}` as words; expected = GNU bash via WSL, 52 lines; the deployed build, rev 156,
prints one line instead: a syntax error at `echo }`). git-prompt.sh still loads and `__git_ps1 "[%s]"` prints `[main]`. Suite 89/89 with a
console and with `-Detached`; compat battery 227/237, exit 0, no baseline regression.

**Correction to this log's dating:** the defect entries above for revs 146 to 165 are headed
2026-10-08, the day the defect list was opened. The commits are dated 2026-10-09 (revs 146-153)
and 2026-10-10 (revs 154-165), per `hg log`. The entries stay as written (append-only); the code
comments that say "until 2026-10-08" carry the same offset.

**Implemented by:** rev 166. Files: `Bash/Lexer/Lexer.cs`, `Bash/Parser/Parser.cs`,
`Bash/Evaluator/WordExpander.cs`, `tests/cases/brace_constructs.sh`,
`tests/expected/brace_constructs.out`, `tests/run-tests.ps1`.

**Affects `PROJECT_CONTEXT.md`:** the RESUME anchor (FIXED, NOT DEPLOYED).

## 2026-10-10 -- DEFECT FIXED: FUNCNAME / BASH_SOURCE / BASH_LINENO were two drifting lists (a `source` dropped the script; inside a function BASH_SOURCE was empty)
**Status:** Active. Found while working the open item "script errors say `bash:` where bash says
`<script>: line N:`" (the architect, 2026-10-10: "go meant 'still open' gogogo"): bash names the
file in that prefix from `BASH_SOURCE[0]`, so the prefix cannot be right until BASH_SOURCE is.

**Defect (measured against bash 5.1 via WSL):** the evaluator kept a function-name list and a
source-file list separately. The script itself was never on the source list, so after `. ./lib.sh`
BASH_SOURCE was empty. A function did not record the file it was defined in, so inside any
function BASH_SOURCE was empty (the common `$(dirname "${BASH_SOURCE[0]}")` idiom inside a
function got `.`). BASH_LINENO did not exist. FUNCNAME was `(main)` at a script's top level.
12 of the 13 lines of the new case differed from GNU.

**Decision:** one frame stack, innermost first, as in bash:
- A function call pushes (its name, the file it was defined in, the line it was called from).
- `source` pushes ("source", the file, the line).
- A script file has a bottom frame ("main", the script as named, 0). `-c` and stdin have none.
- A function records its file when its definition runs: `BASH_SOURCE[0]`, else "environment"
  under `-c` and "main" from stdin (bash's `make_function_def`).
- FUNCNAME exists only while a function runs.

The three arrays are written only when an array is next read. A call or return just marks them
stale. Rebuilding them on every call and return cost ~0.9 us a call, +34% on a bare call (20,000
calls in-process: 0.053 s to 0.071 s). Written lazily, the cost is the old build's (0.047 s vs
0.050 s, within noise).

**Alternatives considered:**
- **Patch the source list** (push the script at startup, push the file on a function call),
  keeping the separate lists. Rejected: the lists drifting apart was the defect, and BASH_LINENO
  needs the same frames.
- **Publish eagerly.** Measured above. Rejected for the per-call cost.

**Compat battery:** probe 199 (`${FUNCNAME[0]:-nofn}` at top level) now differs from Git Bash,
which is bash 4.4.23 and prints `main` (inconsistently: `${#FUNCNAME[@]}` is 0 there). GNU 5.1 on
Linux prints `nofn`, and the ruling of 2026-10-03 is GNU bash on Linux, not Git for Windows'
quirks, so 199 left the baseline (226 to 225).

**Verified:** new case `call_stack` (scripts run by relative name from a temp dir: a sourced
file, nested calls, `source` inside a function, `( )`, `$( )`, an in-process `./child.sh`, `-c`,
stdin; expected = GNU bash via WSL); the rev 166 build differs on 12 of 13 lines. Suite 90/90 with a
console and `-Detached`; compat battery 226/237, exit 0, no regression against the new baseline.

**Implemented by:** rev 167. Files: `Bash/Evaluator/Evaluator.cs`, `Bash/Evaluator/ShellEnvironment.cs`,
`Bash/Parser/Nodes.cs` (`FunctionDef.DefinedIn`), `Bash/Program.cs`, `tests/cases/call_stack.sh`,
`tests/expected/call_stack.out`, `tests/run-tests.ps1`, `tests/compat/baseline.txt`.

**Affects `PROJECT_CONTEXT.md`:** the RESUME anchor (FIXED, NOT DEPLOYED; battery baseline 225).

## 2026-10-10 -- DEFECT FIXED: every error said `bash:` where bash says `script: line N:`; `eval` and `$( )` text restarted at line 1
**Status:** Active. The open item "message placement" (recorded 2026-09-12; the architect,
2026-10-10: "go meant 'still open' gogogo"). Step 2; step 1 was the call stack (rev 167).

**Defect (measured against bash 5.1 via WSL):**
- Every shell error was printed as `bash: …`. bash prints `name: line N: …` in a
  non-interactive shell (error.c, `error_prolog`), and `printf`'s own messages had no prefix at all.
  The name is `BASH_SOURCE[0]`, else `$0`, so a function's error names the file it was defined
  in (`./lib.sh: line 2:`). `-c '…' myname` gives `myname: line N:`, stdin gives `$0: line N:`,
  and a function defined under `-c` gives `environment: line 1:`.
- A syntax error adds the input name when it is not the error name (parse.y, `parser_error`):
  `script: eval: line N:`, `name: -c: line N:`.
- Text parsed apart from its file restarted at line 1: an `eval` string, a `$( )` inside double
  quotes, a heredoc or a `${x:-word}`, and `$( )` in arithmetic. bash continues the line of the
  command that runs it (an `eval` on line 7 runs its line 2 as line 8). A `$( )` also left the
  caller's `$LINENO` at its own last line.

**Decision:**
- One prefix, `Evaluator.ErrorPrefix`: `name: line N: `, or `bash: ` when interactive. Every
  `"bash: "` site in the evaluator uses it, and builtins use it through `Builtins.Err`. The in-process utilities
  (`ls`, `sed`, …) stand in for programs and keep their bare `ls: `, as the programs do.
- An `EvalException` captures the prefix when it is raised. It is printed where it is caught, after
  the function's frame and line have unwound.
- Line numbers: a substitution body or eval string counts from 1, and `Evaluator._lineBase` is
  added while it runs: the enclosing command's line − 1. An unquoted `$( )` from the main token
  stream is rebased to match. A file runs at base 0. A function keeps the base it was defined
  under, so one defined in an `eval` still reports the eval's lines.
- Startup errors stay `bash: ` (bad option, missing script file), as in bash.

**Alternatives considered:**
- **Lex eval and substitution text at absolute file lines.** Tried first for `eval`, then
  withdrawn. A heredoc's `$( )` would report its body line (5) where bash reports the command's
  (4), and a `$( )` re-parsed at run time has no file line to start from.
- **Print the current prefix where an exception is caught.** Rejected: by then a function's
  frame is gone, so the file would be the caller's.

**Not done (each recorded in the anchor):**
- bash numbers a simple command from the line where its SECOND word ends (parser lookahead).
  So for a multi-line `x=$(…)` on lines 3-6, an error inside it is reported at 6 + offset (8); C#Bash
  reports 3 + offset (5). Not imitated: it is a parser artefact, and it would move `$LINENO`.
- Syntax-error text: bash's ``syntax error near unexpected token `fi'`` plus a second line
  echoing the source; C#Bash prints its own text (`Expected 'then' but got '' at 1:8`), and for
  `if true; then` + `fi` at line 3 it reports line 5.
- `trap 'cmd' BADSIG` and `trap - BADSIG` are accepted silently; bash prints `trap: BADSIG:
  invalid signal specification` (found by this case).
- `read -z` prints no usage line after the error; bash prints `read: usage: …`.

**Verified:** new case `error_prefix` (42 lines: sourced file, function in a sourced file, `eval`,
`$( )` unquoted / quoted / in a heredoc / in `${ }` / in arithmetic, pipeline, subshell, loop,
condition, continued line, builtin, `printf`, redirects, missing `source` file, `${u:?}`, `-c`
with a name, a `-c` function, syntax-error prefixes for `eval`, `-c` and a script; expected = GNU
bash via WSL); the rev 166 build differs on 35 of the 42. `shellbuiltins`' expected file
(authored 2026-09-04, rev 39) pinned the old `bash: nosuch_cmd_zz: …`; it now has the script's
prefix. Suite 91/91 with a console and `-Detached`. Compat battery 227/237, exit 0; probe 187
(`python --version 2>&1 | cut -d' ' -f1` where python is absent) now passes because of the prefix
and joined the baseline (225 to 226).

**Implemented by:** rev 168. Files: `Bash/Evaluator/Evaluator.cs`, `Bash/Evaluator/EvalException.cs`,
`Bash/Evaluator/ShellEnvironment.cs`, `Bash/Evaluator/Builtins.cs`, `Bash/Evaluator/Builtins.Shell.cs`,
`Bash/Evaluator/WordExpander.cs`, `Bash/Parser/Parser.cs`, `Bash/Parser/Nodes.cs`, `Bash/Program.cs`,
`tests/cases/error_prefix.sh`, `tests/expected/error_prefix.out`, `tests/expected/shellbuiltins.out`,
`tests/run-tests.ps1`, `tests/compat/baseline.txt`.

**Affects `PROJECT_CONTEXT.md`:** the RESUME anchor (FIXED, NOT DEPLOYED; OPEN DEFECTS).

## 2026-10-10 -- DEFECT FIXED: `ls dir missing` printed no `dir:` heading; directory operands were not sorted
**Status:** Active. The open item "[not isolated] `ls a\ b/ x` with a failing second operand
printed no `a b/:` heading" (found 2026-10-08 in the escaped-space probe); the architect,
2026-10-10: "go meant 'still open' gogogo".

**Isolated (against GNU ls via WSL, 14 operand forms):** the space was irrelevant. Two
defects in the in-process `ls`:
- The heading rule counted only the operands that exist. GNU ls (ls.c) drops the heading only
  when there is a single operand and it is a directory; a failed operand counts. So
  `ls plain x` and `ls x plain` printed `f2` where GNU prints `plain:` then `f2`.
- Directory operands were listed in command-line order. GNU sorts them with the listing's own
  order (name, `-r`, `-t`, ...): `ls plain 'a b'` lists `a b:` first.

**Decision:** a heading whenever there is more than one operand, or under `-R`. Directory
operands are sorted with the same comparison as the entries, except under `-U`, which keeps
command-line order.

**Not done:** `-U` within a directory still sorts by name (the comparison falls back to the name
when the sort key is 0). GNU lists directory order. Not measured against GNU here, not raised.

**Verified:** new case `ls_operands` (14 forms: failing operands before and after, two failing,
a file and a directory, `-r`, `-d`, `-R`; expected = GNU ls via WSL, 80 lines); the rev 166 build
differs on 23 lines. Suite 92/92 with a console and `-Detached`; compat battery 227/237, exit 0,
no regression.

**Implemented by:** rev 169. Files: `Bash/Evaluator/Builtins.Files.cs`, `tests/cases/ls_operands.sh`,
`tests/expected/ls_operands.out`, `tests/run-tests.ps1`.

**Affects `PROJECT_CONTEXT.md`:** the RESUME anchor (FIXED, NOT DEPLOYED; OPEN DEFECTS).

## 2026-10-10 -- DEFECTS FIXED: `trap` accepted any word as a signal (`trap x BADSIG`, `trap x 65`); `trap -p` spelling, order and operands
**Status:** Active. Found by the `error_prefix` case (DECISIONS "every error said `bash:`", Not
done); the architect, 2026-10-10: "go meant 'still open' gogogo".

**Defect (measured against bash 5.1 via WSL, 33 signal specs):**
- Any alphanumeric word was a signal, so `trap x BADSIG`, `trap - BADSIG`, `trap x 65`, `IOT`
  and `CLD` all succeeded silently. bash says `trap: BADSIG: invalid signal specification`,
  status 1.
- `trap x INT BADSIG TERM` stopped at BADSIG, leaving TERM unset; bash sets every valid one.
- `RTMIN+3` and `RTMAX-2` were refused.
- `trap -p` printed `INT` (bash: `SIGINT`), in hash order (bash: EXIT, the signals by number,
  DEBUG, ERR, RETURN), and ignored its operands.

**Decision:**
- A table of Linux's signal numbers as bash names them: 1-31 by name, 32 and 33 bare,
  RTMIN … RTMIN+15, RTMAX-14 … RTMAX. A spec is valid if it is one of those names in any case,
  with or without SIG, or `RTMIN+n` (n 0-30), `RTMAX-n` (n 0-29, bash's `decode_signal`), a number
  0-64, EXIT, DEBUG, ERR or RETURN. Windows raises none but INT, but a script may still set and list
  traps for any of them.
- Every bad spec is reported and the valid ones are set.
- `trap -p` uses bash's spelling and order. With operands it prints each in turn as given: a
  repeat prints again, an unset one prints nothing.

**Not done:** `trap -l` and `kill -l` still print the short list of nine names. bash prints the
numbered table (` 1) SIGHUP  2) SIGINT …`). Not measured here, not raised.

**Verified:** new case `trap_signals` (the 33 specs, a bad spec among good ones, `trap -p` with
and without operands, a quote in a trap command; streams merged inside the script, because
wsl.exe relays stdout and stderr separately and loses their order under an outer `2>&1`;
expected = GNU bash via WSL); the rev 166 build differs on 29 lines. Suite 93/93 with a console
and `-Detached`; compat battery 227/237, exit 0, no regression.

**Implemented by:** rev 170. Files: `Bash/Evaluator/Builtins.cs`, `tests/cases/trap_signals.sh`,
`tests/expected/trap_signals.out`, `tests/run-tests.ps1`.

**Affects `PROJECT_CONTEXT.md`:** the RESUME anchor (FIXED, NOT DEPLOYED; OPEN DEFECTS).

## 2026-10-10 -- DEFECTS FIXED: builtins took bad options as operands or ignored them, and printed no usage line (`printf -Z`, `cd -Z`, `kill -Z 1`, `trap -- 'cmd' SIG`)
**Status:** Active. Grew from the open item "`read -z` prints no usage line" (found by the
`error_prefix` case); the architect, 2026-10-10: "go meant 'still open' gogogo". Grounded in the
README's own contract: "an option … that is not implemented is never silently ignored".

**Defect (measured against bash 5.1 via WSL, every option-taking builtin):**
- No builtin printed bash's usage line after an invalid option (`read: usage: read [-ers] …`).
  `readarray`, `typeset` and `local` reported themselves as `mapfile` or `declare`.
- These took `-Z` as an operand, ignored it, or did something else with it: `printf` (printed it),
  `cd`, `pwd`, `export` and `readonly` (a bad identifier), `unset`, `alias` and `unalias` ("not
  found"), `source` (a file named `-Z`), `pushd`, `popd` and `dirs` (`pushd +3` ran `cd +3`),
  `trap`, `history`, `jobs` and `wait`. `kill -Z 1` went on to signal pid 1.
- `trap -- 'cmd' SIG`, the form `trap -p` prints, set a trap that runs `--` and reported the
  command as a signal. `source -- file` read `--` as the file. `cd a b` ignored `b`, where bash
  says "too many arguments".
- The usage lines that did exist (`printf`, `getopts`, `kill`, `trap`) carried the script prefix
  from rev 168; bash prints usage lines without it (`builtin_usage`).

**Decision:**
- One helper, `Builtins.BadOption`, prints `name: -Z: invalid option` and bash's usage line, for
  status 2. `name` is the builtin as called (`Builtins._called`, saved and restored when a
  builtin runs another). The usage texts are bash 5.1's `help -s`, in `Builtins.Usages`.
- `FlagOptions` reads leading flag letters against each builtin's set; options end at the
  first operand or `--`.
- Each builtin follows bash's own rules where they differ, all measured:
  - `pwd` ignores operands. `cd` with two operands is "too many arguments", status 1.
  - `alias -p` prints all and then processes any names. `unalias -a` clears all; `unalias` alone
    prints the usage line.
  - `source`/`.` with no file is "filename argument required" plus usage, under the called name.
    `source` also passes the file name as given, so `BASH_SOURCE` and the error prefix name it as
    bash does.
  - `pushd`/`popd`/`dirs` take a bad `+N`/`-N` as "invalid number". `dirs` takes one flag per
    word (`dirs -cv` is an invalid number) and an operand as an invalid option. `popd foo` is an
    invalid argument. `pushd a b` is "too many arguments". An empty stack is reported before
    `+N`. A good `+N`/`-N` is reported as not supported (rotation is not implemented).
  - `kill` checks its signal spec with trap's table and signals nothing on a bad one ("invalid
    signal specification", 1). `kill -s` with no spec is "option requires an argument", 1.
    `kill -l N` / `kill -l NAME` translate (N may be 128+signal).
  - `trap` reads `-l`/`-p`/`--` as options. A first operand that is a signal number, or a lone
    signal, resets (`trap 2 15`, `trap INT`). An action with no signal prints the usage line.
  - `history` checks its letters before its script-mode early return.

**Not done (recorded in the anchor):** what the accepted-but-unimplemented options select:
`jobs -l -n -p -r -s -x`, `wait -n` (does not wait for just one job; `jobs` then still lists a
finished job), `wait -p VAR`, the directory stack's `+N`/`-N` rotation, and `kill -l`/`trap -l`'s
numbered table. `umask`, `times` and `fc` are not builtins at all; `fc` reaches Windows'
`fc.exe`. Those three are scope, raised with the architect, not built.

**Verified:** new case `builtin_options` (every option-taking builtin with `-Z`, then 40 edge
forms: valid options still work, operands after options, `--`, the stack forms, `kill` and `trap`
forms; run by relative name from a temp dir, streams merged inside; expected = GNU bash via WSL,
188 lines). The rev 166 build differs on 142 lines. Git's `__git_ps1` (which uses
`printf -v VAR -- fmt`) still prints `[main *=]`. Suite 94/94 with a console and `-Detached`;
compat battery 227/237, exit 0, no regression.

**Implemented by:** rev 171. Files: `Bash/Evaluator/Builtins.cs`, `Bash/Evaluator/Builtins.Shell.cs`,
`Bash/Evaluator/Evaluator.cs` (disown's usage line), `tests/cases/builtin_options.sh`,
`tests/expected/builtin_options.out`, `tests/run-tests.ps1`.

**Affects `PROJECT_CONTEXT.md`:** the RESUME anchor (FIXED, NOT DEPLOYED; OPEN DEFECTS; AWAITING).

## 2026-10-10 -- DEFECTS FIXED: syntax errors in C#Bash's own words, often at the wrong line; grammar bash rejects was accepted (`then fi`, `a &&`, `f() echo x`, `[[ -f ]]`)
**Status:** Active. The open item recorded with the error-prefix fix (DECISIONS "every error said
`bash:`", Not done); the architect, 2026-10-10: "go meant 'still open' gogogo".

**Defect (measured against bash 5.1 via WSL, 43 scripts):**
- Every syntax error had C#Bash's text: `Expected 'fi' but got '' at 5:1`, `Expected RParen but got
  Eof`, `Unterminated double-quoted string at 1:6`. bash has three forms (parse.y,
  `report_syntax_error`, `parser_error`): ``syntax error near unexpected token `fi'`` followed by
  the source line quoted; `syntax error: unexpected end of file`; and, for an unclosed quote, `$(`,
  `${` or backquote, ``unexpected EOF while looking for matching `)'`` at its line, then the end of
  file. `[[ ]]` has its own (`cond_error`): ``unexpected argument `]]' to conditional binary
  operator`` / ``syntax error near `]]'`` / the line.
- Grammar bash rejects was accepted:
  - an empty body (`then fi`, `do done`, `{ }`, `( )`): the closing word was parsed as a command
    named `fi`, and the error came at the end of the file;
  - `a &&` or `a ||` at the end, which ran `a`;
  - `f() echo x`, where the function body must be a compound command;
  - `[[ -f ]]`, `-f` read as a string, status 0;
  - `[[ a == ]]`, which took `]]` as the operand;
  - an unclosed backquote.
- An unterminated heredoc was silent; bash warns ``here-document at line 1 delimited by
  end-of-file (wanted `EOF')``.

**Decision:**
- A `ParseException` carries the token it failed at, plus, where bash words it differently, the
  missing closer, an "unsupported" flag (`>( )` keeps its own message), the `[[ ]]` form, and the
  status (an array literal's is 1, as in bash).
- One renderer, `Parser.SyntaxMessage`, produces bash's lines, and `RunString` prints them with the
  error prefix.
- The grammar is fixed where it was wrong: a list does not start at an unquoted closing word;
  bodies need a command; a command must follow `&&`/`||`; a function body is a compound command;
  `[[ ]]` operands are words and never `]]`, and its unary operators are bash's own list. The
  lexer reports the heredoc warning before the text runs.
- An unquoted `$( )` body parsed from rebased tokens (rev 168) reports its errors at the file's
  line again.

**Alternatives considered:** keep C#Bash's messages and fix only the grammar. Rejected: the
messages are what people and agents match on, and the line was often wrong.

**Not done (raised with the architect, recorded in the anchor):**
- bash parses and runs one top-level command at a time, so the lines before a syntax error RUN
  (`echo a` then an error on line 3 prints `a`). C#Bash parses the whole text first and runs
  nothing.
- The same model makes bash report a syntax error inside `$( )` at run time, as `name: command
  substitution: line N+1:`, with that command failing (status 1) and the script going on. C#Bash
  ends the whole script before it starts.
- Interactive mode (`Program.cs`'s REPL) still prints the old text.

**Corpus check:** `-n` over Git for Windows' 24 scripts (all agree with GNU) and Ubuntu 22.04's 912
bash-completion scripts. 843 agree; the 69 that C#Bash rejects all use extglob in `[[ $x ==
@(a|b) ]]`, which bash parses with extglob off. The rev 166 build rejects the same 69, so this
change adds no false rejections. That extglob gap is the 2026-09-04 decision, not this one.

**Verified:** new case `syntax_errors` (43 scripts; commands before an error are `true`, so the
open question above is not tested; expected = GNU bash via WSL, 170 lines); the rev 166 build
differs on 92. `stray_keyword` caught a regression during the work: a quoted `'fi'` must stay a
command name. Git's `__git_ps1` prints `[main *=]`. Suite 95/95 with a console and `-Detached`;
compat battery 227/237, exit 0, no regression.

**Implemented by:** rev 172. Files: `Bash/Parser/Parser.cs`, `Bash/Parser/ParseException.cs`,
`Bash/Parser/SyntaxMessage.cs` (new), `Bash/Lexer/Lexer.cs`, `Bash/Lexer/LexException.cs`,
`Bash/Evaluator/Evaluator.cs`, `tests/cases/syntax_errors.sh`, `tests/expected/syntax_errors.out`,
`tests/run-tests.ps1`.

**Affects `PROJECT_CONTEXT.md`:** the RESUME anchor (FIXED, NOT DEPLOYED; OPEN DEFECTS; AWAITING).

## 2026-10-10 -- DEFECT FIXED: `while read -r f; do prog; done < list` ended after one line (a program run while the shell reads a file took the rest of it)
**Status:** Active. Found 2026-10-10 by Claude while running a `-n` corpus check (a loop over a file
list ran one file). It is in every build back to the deployed rev 156. The architect: "go meant
'still open' gogogo".

**Defect:** when any external program ran inside a loop reading a file, the loop got end of file
after the first line:
- with the program's stdin redirected (`prog < /dev/null`): the child's default stdin was computed
  BEFORE its redirects, and computing it took (`TakeBuffered`) what the loop's `read` had read
  ahead. The default was then discarded, and the data with it.
- with the program inheriting stdin: the rest of the file was PUMPED into the program through a
  pipe (`FeedChild` copies to the end), whether or not the program reads it.
- a C#Bash child (`"$BASH" -c 'read -r y'`) read 4 KB ahead and lost the rest to its parent at
  exit.
On Unix the child shares the file offset: it consumes only what it reads, and the shell goes on from
there. bash's `read` also seeks back what it read ahead on a seekable stdin.

**Decision:**
- A child's fd 0 default is taken only when no redirect replaces it (`<&0` takes it on demand).
- When the shell reads a seekable FILE, the child gets the file itself (`SharedFileFd`), and the
  OS file pointer is set to the shell's logical offset (the stream position minus the read-ahead).
  After the child exits, the shell reads on from where the child left the pointer.
- When the shell's own stdin is a disk file (`bash script < list`), the read-ahead is rewound and
  the handle inherited.
- At exit, a C#Bash whose stdin is a disk file seeks back over what its reader read ahead and did
  not use (`ConsoleMux.GiveBackStdin`), as bash's `read` does.

**A trap found on the way:** .NET's `FileStream.SafeFileHandle` getter moves the OS pointer to the
stream's cached position. Calling it after the child undid exactly what the child read. The handle is
taken once, before the launch.

**Not done (a known limitation, recorded in the anchor):** a PIPE into the loop
(`producer | while read; do prog; done`) is still pumped into `prog`, because pipeline stages are
in-process pipes, not OS pipes. A program that reads part of its stdin (bash's `read` takes one
line) then loses the rest of the pipe for the loop. A program that reads none of it, given
`< /dev/null`, now leaves it alone. Fixing that means OS pipes between stages, which is an
architectural change.

**Verified:** new case `stdin_sharing` (10 forms: no program, a program with `< /dev/null`, one
inheriting, a C#Bash child reading one line per pass, in-process `head -n 1`, a `{ }` group, the
shell's own stdin a file, a pipe with `< /dev/null`, `cat` consuming the rest; expected = GNU bash
via WSL); the rev 166 build differs on 7 of the 10 lines. `cmd.exe /c rem` in such a loop takes the
rest of the file under Git Bash too: cmd reads its stdin. Suite 96/96 with a console and
`-Detached`; compat battery 227/237, exit 0, no regression.

**Implemented by:** rev 173. Files: `Bash/Evaluator/Evaluator.cs` (`CurrentStdin`, `ExecExternal`,
`SharedFileFd`), `Bash/Evaluator/ChildLauncher.cs` (`SeekHandle`, `HandlePosition`, `IsDiskFile`),
`Bash/Evaluator/ConsoleMux.cs` (`GiveBackStdin`), `tests/cases/stdin_sharing.sh`,
`tests/expected/stdin_sharing.out`, `tests/run-tests.ps1`.

**Affects `PROJECT_CONTEXT.md`:** the RESUME anchor (FIXED, NOT DEPLOYED; KNOWN LIMITATIONS).

## 2026-10-10 -- DEFECT FIXED: a UTF-8 character cut by a read's 4096-byte block (or split across pipe writes) became two raw bytes
**Status:** Active. The open item "UTF-8 split at read boundaries [inferred 2026-10-03, never
reproduced]". Reproduced 2026-10-10 once rev 173's work showed the mechanism; the architect: "go
meant 'still open' gogogo".

**Defect (measured against bash 5.1 via WSL):** `LfReader` decodes stdin in 4096-byte blocks
through `ShellEncoding.Utf8.GetDecoder()`. `ShellEncoding` did not override `GetDecoder`, and
Encoding's default decoder is stateless, so a character whose bytes straddled a block became its
bytes as raw-byte characters. With `é` at bytes 4096-4097, `read`, a pipe, `mapfile` and `while read`
gave a line of 4098 characters, not 4097, and `${line: -2}` showed a raw byte. Output stayed byte
for byte (the raw-byte characters write back as the same bytes), so it showed only in lengths,
slicing and pattern matches. `$(< file)` was right: it decodes the whole file at once. The code
had assumed the opposite ("Callers that read in chunks must therefore decode whole buffers (all of
ours do)").

**Decision:** `ShellEncoding.CarryDecoder`, returned by `GetDecoder`. At a chunk's end it holds back
a lead byte followed only by continuation bytes, fewer than the lead needs (at most 3), and joins
them to the next chunk; at the final flush they decode as anywhere (raw bytes if nothing completes
them). Every `StreamReader` over `ShellEncoding` gets it too. `LfReader.TakeBuffered` hands back
the held bytes with the read-ahead, so rev 173's file-offset handoff stays exact when the
read-ahead ends mid-character.

**Verified:** new case `utf8_boundary` (2-, 3- and 4-byte characters at the boundary through
`read`, a pipe, `mapfile` and `while read`; an invalid byte at the boundary; a sequence cut by the
end of the file; a character split across two pipe writes 0.2 s apart; the offset handoff to a
child; expected = GNU bash via WSL); the rev 166 build differs on 14 of 19 lines. Suite 97/97 with
a console and `-Detached`; compat battery 227/237, exit 0, no regression.

**Implemented by:** rev 174. Files: `Bash/Evaluator/ShellEncoding.cs`, `tests/cases/utf8_boundary.sh`,
`tests/expected/utf8_boundary.out`, `tests/run-tests.ps1`.

**Affects `PROJECT_CONTEXT.md`:** the RESUME anchor (FIXED, NOT DEPLOYED; OPEN DEFECTS).

## 2026-10-10 -- DEFECTS FIXED: Linux's device files did not exist (`cat /dev/null`, `head -c 5 /dev/zero`, `diff -u /dev/null new`, `[ -e /dev/null ]`); `tr` did not stream; `od` printed nothing for empty input
**Status:** Active. Found 2026-10-10 by the UTF-8 boundary work (`head -c 4095 /dev/zero` could not
be used to build the test file); the architect: "go meant 'still open' gogogo".

**Defect (measured against bash 5.1 via WSL):**
- Only the redirect code knew `/dev/null`, `/dev/stdout` and `/dev/stderr`, and only as output
  targets (and `/dev/null` as input). Everywhere else a device path resolved against the current
  drive (`F:\dev\null`) and was missing:
  - as an operand: `cat /dev/null`, `head -c 5 /dev/zero`, `cat /dev/stdin`, `cat /dev/fd/3`,
    `diff -u /dev/null new` (how a new-file patch is made), `cp /dev/null f`, `tail`, `ls`, `stat`;
  - as a redirect: `< /dev/zero`, `< /dev/urandom`, `> /dev/fd/2`;
  - under `test`: `[ -e /dev/null ]` was false.
- `tr` read ALL its input before writing, so `tr '\0' z < /dev/zero | head -c 3` never ended, and
  a large input had to fit in memory.
- `od` printed nothing for an empty input; GNU prints the offset `0000000`.

**Decision:** one device layer in `ShellFile`:
- `Device(path)` names the device: null, zero, random (/dev/random and /dev/urandom), tty, fd:N.
  /dev/stdin, /dev/stdout and /dev/stderr are fd:0, fd:1 and fd:2.
- `OpenInput` reads files and devices. Zero and random are endless streams; fd:0 is the shell's
  current stdin, read-ahead included; fd:N comes from the evaluator (`ShellEnvironment.FdStream`).
  `Exists` is true for devices.
- `ShellFile.ReadAllBytes`, and so `ShellEncoding.ReadAllText`, go through it. That covers `sed`,
  `awk`, `diff`, `jq`, the checksum tools and script loading. `cat`, `head`, `tail`, `fold`,
  `grep` and `OpenText` open through it.
- `cp`, `ls` and `stat` know devices (`stat -c '%F %s %a %f'` gives `character special file 0 666
  21b6`).
- Redirects, in-process and for a program: `DeviceRedirect` rewrites a device redirect as what it
  amounts to on Linux:
  - `> /dev/zero` and `> /dev/urandom` are `> /dev/null`;
  - `> /dev/fd/1` and `> /dev/fd/2` are /dev/stdout and /dev/stderr;
  - `> /dev/fd/N` and `< /dev/fd/N` are `>&N` and `<&N`;
  - `< /dev/stdin` is nothing.
  A device read is opened by the layer, and a program gets zero or random through a pump that runs
  until it stops reading. `/dev/tty` is the console (CONIN$, CONOUT$); with none, it is "No such
  device or address".
- `test` (FileTests): a device exists, is readable and writable, and is a character device. A
  /dev/fd/N or /dev/std* is one only when its descriptor is the console or /dev/null.
- `tr` streams a block at a time; `od` always prints its last offset (none under `-An`).

**Not done:** `exec 3>&1` (copying a standard descriptor to a numbered one) does not work at all:
`echo >&3` is "Bad file descriptor" (found by this case, recorded as the next open item). For
`> /dev/fd/3` inside `$( )`, GNU's own result is a /proc artefact (Permission denied), so the case
leaves it out. `/dev/tty` depends on a console, so it is not in the case either. Utilities not
named above that check `File.Exists` before opening may still miss a device.

**Verified:** new case `device_files` (52 lines: 7 devices under `test`, 24 utilities with
`/dev/null`, zero and random through `head` and a redirect, `/dev/stdin`, `/dev/fd/0` and
`/dev/fd/3`, `/dev/fd/2` and `/dev/stderr` as targets, programs given `< /dev/zero`, `2> /dev/fd/1`,
`> /dev/fd/2` and `cat /dev/stdin`, `tr` from `/dev/zero`; expected = GNU bash via WSL); the rev 166
build differs on 46 lines. Suite 98/98 with a console and `-Detached`; compat battery 227/237, exit
0, no regression.

**Implemented by:** rev 175. Files: `Bash/Evaluator/ShellFile.cs`, `Bash/Evaluator/ShellEnvironment.cs`,
`Bash/Evaluator/Evaluator.cs` (`DeviceRedirect`, the redirect loops), `Bash/Evaluator/ChildLauncher.cs`
(`Access.Device`), `Bash/Evaluator/FileTests.cs`, `Bash/Evaluator/Builtins.Text.cs` (cat, head, tail,
fold, tr, od), `Bash/Evaluator/Builtins.Search.cs` (grep), `Bash/Evaluator/Builtins.Find.cs` (diff),
`Bash/Evaluator/Builtins.Files.cs` (cp, ls, stat), `tests/cases/device_files.sh`,
`tests/expected/device_files.out`, `tests/run-tests.ps1`.

**Affects `PROJECT_CONTEXT.md`:** the RESUME anchor (FIXED, NOT DEPLOYED; OPEN DEFECTS).

## 2026-10-10 -- DEFECTS FIXED: `exec 3>&1` (a standard descriptor copied to a numbered one) did nothing; closing a copied descriptor broke the other copy
**Status:** Active. Found 2026-10-10 by the `device_files` work; the architect: "go meant 'still
open' gogogo".

**Defect (measured against bash 5.1 via WSL):**
- `N>&1`, `N>&2` and `N<&0` with N ≥ 3 fell through the redirect code without making fd N. So
  `exec 3>&1; echo x >&3` said "3: Bad file descriptor", and so did the stderr-capture idiom
  `exec 3>&1; out=$(cmd 2>&1 >&3)`, `{ …; } 3>&1 1>&2 2>&3` (swap stdout and stderr), `f 4>&1`, and
  `exec 3<&0; read -u 3`.
- With fd 3 made (from a file, or by this fix), `exec 1>&3 3>&-` DISPOSED the stream fd 1 had just
  started writing through: the next `echo` failed ("The handle is invalid"), and a later `4>&1`
  threw an unhandled exception out of the shell. Closing one of two dup'd descriptors must leave
  the other open.

**Decision:**
- fd N is a stream onto where fd 1, 2 or 0 points at that moment (`StdStream`): the file or pipe
  it writes or reads, a `$( )` capture through `TextWriterStream` (decoded with the carrying
  decoder), or the process's own handle. A later `exec >f` leaves it where it was, as in bash.
- A stream that more than one descriptor uses is marked shared (`RedirectScope.MarkShared`): a
  copy, or a stream fd 1/2 writes through after `1>&N`. `N>&-` then drops fd N without disposing
  it. A stream only fd N held is still closed.
- A dup onto a stream that cannot be written is a redirect error ("N: Bad file descriptor"), never
  an exception.

**Verified:** new case `fd_copy` (13 lines: the stderr-capture idiom, the stdout/stderr swap, `exec
>/dev/null` with fd 3 kept, `exec 1>&3 3>&-`, `f 4>&1`, a program's stderr captured with stdout to
fd 3, `exec 3<&0; read -u 3`, `exec 4>&2`, a closed fd, `exec 3>&1` inside `$( )`; run by relative
name from a temp dir; expected = GNU bash via WSL); the rev 166 build differs on all 13. Suite 99/99
with a console and `-Detached`; compat battery 227/237, exit 0, no regression.

**Implemented by:** rev 176. Files: `Bash/Evaluator/Evaluator.cs` (`StdStream`, `TextWriterStream`,
the dup cases, `RedirectScope.TrackFd`/`MarkShared`), `tests/cases/fd_copy.sh`,
`tests/expected/fd_copy.out`, `tests/run-tests.ps1`.

**Affects `PROJECT_CONTEXT.md`:** the RESUME anchor (FIXED, NOT DEPLOYED; OPEN DEFECTS).

## 2026-10-10 -- DEFECT FIXED: `wait -n` did not wait (and `wait -p VAR` was ignored)
**Status:** Active. The open item "accepted options whose effect is not implemented" (DECISIONS
"builtins took bad options …", Not done); the architect: "go meant 'still open' gogogo".

**Defect (measured against bash 5.1 via WSL):** `wait` skipped every argument starting with `-`,
so `wait -n` returned at once, status 0, and left the finished job in the table (`jobs` still
listed it). A job pool (`while …; do cmd & ((++n >= 4)) && wait -n; done`) therefore ran unbounded.
`wait -p VAR` was ignored.

**Decision:** bash 5.1's semantics. `wait -n` waits for the next of the shell's jobs, or of the ids
given, to finish, removes it, and returns its status; 127 when there is none. `-p VAR` is set to
that job's id; with plain `wait`, to the last job waited for. The wait blocks on an event that
each job sets when it finishes (`BackgroundJob.Finished`, one `ManualResetEvent` per shell). The
event is reset BEFORE the jobs are checked, so a job finishing in between is not missed. The
interrupt handles wake it too, as for `tail -f`. No polling (the architect's rule).

**Verified:** new case `wait_next` (two jobs finish out of order, `wait -n` twice, a third with none
left (127), `-p`, `wait -n ID`, plain `wait`, the job table empty after; gaps of a second so the
order does not depend on a child's start-up; expected = GNU bash via WSL); the rev 166 build differs
on 4 lines. Suite 100/100 with a console and `-Detached`; compat battery 227/237, exit 0, no
regression.

**Implemented by:** rev 177. Files: `Bash/Evaluator/Evaluator.cs` (`WaitJobs`, `WaitNext`,
`WaitEach`), `Bash/Evaluator/BackgroundJob.cs` (`Finished`), `tests/cases/wait_next.sh`,
`tests/expected/wait_next.out`, `tests/run-tests.ps1`.

**Affects `PROJECT_CONTEXT.md`:** the RESUME anchor (FIXED, NOT DEPLOYED; OPEN DEFECTS).

## 2026-10-10 -- DEFECT FIXED: `jobs` ignored its options (`jobs -p` printed whole lines), used its own format, and listed nothing in a pipeline stage
**Status:** Active. The open item "accepted options whose effect is not implemented"; the architect:
"go meant 'still open' gogogo".

**Defect (measured against bash 5.1 via WSL):**
- `jobs -p` printed the full listing, so `kill $(jobs -p)` in a cleanup trap got words like
  `[1]+`. That idiom happened to work only because `kill` skips non-numbers.
- `-l`, `-r` and `-s` were ignored (`jobs -s` listed the running jobs), and so were job specs
  (`jobs %1` listed every job).
- The format was `[1]+  Running  cmd`. bash's is `[1]+  Running` with the state 24 wide, the
  command, and ` &` while it runs.
- In a pipeline stage (`jobs | sed`) the list was empty, because a stage starts a fresh table. A
  bash subshell lists its parent's jobs.

**Decision:** bash's `pretty_print_job` format. `-l` adds the pid (`%5ld`, here the synthetic job
pid that `kill` and `wait` accept). `-p` prints pids alone, `-r` only running jobs, and `-s` only
stopped ones, which is none, since jobs are never stopped here. Job specs filter the list; an
unknown spec is "no such job", status 1. A subshell keeps a snapshot of its parent's jobs
(`_parentJobs`) for `jobs` alone; `wait` there still has only its own. `jobs -x` is reported as
not supported.

**Verified:** new case `jobs_list` (plain, `-l`, `-p`, `-r`, `-s`, `%1`, `-p %2`, `kill $(jobs -p)`;
all through a pipe, so it also covers the subshell listing; pids replaced and `-l`'s padding
collapsed, since pids differ between shells; expected = GNU bash via WSL); the rev 166 build differs
on 10 of 17 lines. Suite 101/101 with a console and `-Detached`; compat battery 227/237, exit 0, no
regression.

**Implemented by:** rev 178. Files: `Bash/Evaluator/Evaluator.cs` (`ListJobs`, `FindJobIn`,
`_parentJobs`), `Bash/Evaluator/Builtins.cs` (`Jobs`), `tests/cases/jobs_list.sh`,
`tests/expected/jobs_list.out`, `tests/run-tests.ps1`.

**Affects `PROJECT_CONTEXT.md`:** the RESUME anchor (FIXED, NOT DEPLOYED; OPEN DEFECTS).

## 2026-10-10 -- DEFECT FIXED: a syntax error at the end of a text with no final newline was reported one line early
**Status:** Active. Found while checking the deploy notice's examples against GNU (the architect:
"deploy and notify"). It is part of rev 172's syntax-error work, so it is fixed before shipping.

**Defect (measured against bash 5.1 via WSL):** for `bash -c 'echo a &&'`, or a file whose last line
has no newline, bash reports `syntax error: unexpected end of file` at the line AFTER the last line
(line 2). C#Bash counted only newlines and said line 1. The `syntax_errors` case missed it because all
its scripts ended with a newline.

**Decision:** the end of file is the line after the last line, whether or not that line ends with a
newline (`SyntaxMessage`).

**Verified:** `syntax_errors` gains four scripts with no final newline and a `-c` case (expected = GNU
bash via WSL); the rev 178 build differs on 5 lines. Suite 101/101 with a console and `-Detached`;
compat battery 227/237, exit 0.

**Implemented by:** rev 179. Files: `Bash/Parser/SyntaxMessage.cs`, `tests/cases/syntax_errors.sh`,
`tests/expected/syntax_errors.out`.

## 2026-10-10 -- FINDING: the "+1.2 ms startup" of revs 158-179 was the benchmark's own noise; the startup row is one sample
**Status:** Active (a finding, not a method change). The architect, 2026-10-10: "bisect the 1.2ms
first".

**What was seen:** on a quiet machine, `compare3.sh` gave rev 179 a startup of 16.0 ms and, four
minutes later (`--cs`), rev 156 14.8 ms; Git Bash read 15.7 and 15.8. That looked like a regression of
+1.2 ms. (An earlier run while the machine was busy gave rev 179 18.2 ms.)

**The check (a diagnostic, NOT the benchmark, as the benchmark memory allows for a bisect):**
compare3's own startup measurement, `benchtimer 100 <exe> -c 'exit 0'`, run under the same Git Bash
login environment, rev 156 and rev 179 alternating, 15 rounds:
- 5 rounds: rev 156 15.34 ms, rev 179 15.26 ms on average;
- 10 rounds: rev 156 16.23 (sd 0.83), rev 179 15.79 (sd 0.50), so rev 179 was 0.44 ms FASTER;
- one binary's 100-launch samples ranged from 14.9 to 17.7 ms.
From Claude Code's own shell, with a smaller environment, both start in about 12 ms, level.

**Conclusion:** no regression; nothing to bisect. compare3's startup row is ONE 100-launch total,
so it moves by about +-1.5 ms between runs on this machine. A difference of that size between two
compare3 runs is not evidence. Confirm with an interleaved run of the same measurement before
calling a regression. (This is the sibling of 2026-10-06's "+1 ms" from a best-of-N minimum.)

**Not done:** the method itself (one startup sample) is unchanged. Changing it (several samples,
interleaved, a median) is the architect's call (DECISIONS 2026-10-06, "The benchmark is one fixed
command").

## 2026-10-10 -- METHOD CHANGE: the benchmark's startup row is the median of 5 samples
**Status:** Active. Supersedes the single-sample startup measurement of the 2026-10-06 entry "The
benchmark is one fixed command" (the rest of that method stands). The architect, 2026-10-10:
"Median of 5 sounds fair".

**Why:** one sample (the total of 100 launches / 100) moves by about +-1.5 ms between runs on this
machine (14.9 to 17.7 ms for one binary), and two single runs invented a "+1.2 ms regression"
(DECISIONS 2026-10-10, "the +1.2 ms startup … was the benchmark's own noise").

**Decision:** `compare3.sh` takes `STARTUP_SAMPLES=5` samples per shell, each still `benchtimer 100
<exe> -c 'exit 0'` with the exit-7 control first, and reports their median. A median, not a mean,
so one disturbed sample cannot move it. The rows (best of 9 / best of 3) are unchanged.

**Alternatives considered:** a mean of 5 (a single bad sample still moves it); interleaving the three
shells round by round (not asked for; the median already absorbs the drift seen); more launches per
sample (the noise is between samples, not within one).

**Verified:** the new `startup()` with a fake timer whose five totals are known: median 0.0160 where
the mean would be 0.0164; a shell that fails the control still reads FAIL. `bash -n` under Git Bash.
Not run as a benchmark (not asked). The README's table is still the 2026-10-10 single-sample run,
and its method text says so; the next push re-measures with this method.

**Implemented by:** rev 184. Files: `tests/bench/compare3.sh`.

## 2026-10-10 -- DECIDED AND BUILT: a Windows program's MSYS-form path arguments are converted (open since 2026-09-11)
**Status:** Active. Resolves the open item "MSYS paths in arguments to native programs" (raised
2026-09-11, rev 67; DECISIONS 2026-09-11 "Sharpened"). The architect, 2026-10-10, after it was laid
out ("we are now mapping MSYS internally for ourselves, but not when calling windows native
programs?" -- yes): "ok, your recommendation is good".

**Before:** the shell converted MSYS forms wherever it handled a path itself (`cd`, builtins,
`test`, redirect targets; rev 71). A Windows program's ARGUMENTS reached it verbatim, so the same
session meant two things by one path: `cd /f/repo` worked and `git -C /f/repo` failed ("cannot
change to '/f/repo'"); `/tmp/x` was `F:/TEMP/x` to the shell and `<current drive>:\tmp\x` to a
program.

**Decision (the recommendation as given, two refinements marked):**
- Only at the START of an argument: `/<letter>/…` becomes `<LETTER>:/…`, and `/tmp`, `/tmp/…` the
  temp directory. Never a bare `/c` (`cmd /c`), never inside an argument (`--file=/c/x`), and the
  rest of the argument is left as typed (no backslash rewriting). `//host/share/…` is already
  Windows form; `~` the shell expands before.
- Only for Windows programs. An MSYS program (Git's usr/bin, detected by msys-2.0.dll beside it,
  rev 159) reads `/c/x` itself and is left alone, so Git's `sed` and `awk` never see a script like
  `/a/d` rewritten.
- Off switches as in Git Bash: `MSYS_NO_PATHCONV` (any value), and `MSYS2_ARG_CONV_EXCL` (`*` for
  all, or `;`-separated prefixes to leave alone), from the environment or a `VAR=x prog` prefix.
- Refinement 1: the drive must exist, so `/a/b` with no A: stays as typed.
- Refinement 2: honouring Git Bash's own variable names means habits sessions already have keep
  working. This is the environment switch the recommendation called for.

**The risk, stated:** an argument that only LOOKS like an MSYS path, given to a Windows-native
tool, is rewritten: a regex or script text such as `/e/i`, or combined switches like `xcopy
/e/i/h` on a machine with an E: drive. Git Bash has the same behaviour; the off switches are the
remedy. The 2026-10-10 notice says so.

**Verified:** new case `native_args` (15 checks through `cmd /c echo`: drive, root, `/tmp`, bare
`/c`, inside an argument, mid-argument, UNC, a missing drive, a backslash kept, both switches and a
prefix list, an MSYS program untouched, `git -C /c/…`; expected authored, Windows-specific, values
computed at run time); the deployed rev 179 fails 7 of them. Suite 102/102 with a console and
`-Detached`; compat battery 227/237, exit 0, no regression.

**Implemented by:** rev 186. Files: `Bash/Evaluator/ShellEnvironment.cs` (`NativeArgument`),
`Bash/Evaluator/Evaluator.cs` (`ExecExternal`), `tests/cases/native_args.sh`,
`tests/expected/native_args.out`, `tests/run-tests.ps1`.

## 2026-10-10 -- DECIDED AND BUILT: UTF-8 for external programs = PYTHONIOENCODING only; HGENCODING measured harmful and dropped
**Status:** Active. Resolves the open item "UTF-8 for external programs" (koliada-net-dd,
2026-10-06). The architect, 2026-10-10: "your recommendation is good; go up to …". The
recommendation was to export PYTHONIOENCODING=utf-8 and HGENCODING=utf-8 when unset, with a recorded
doubt about HGENCODING: "test before building". The test failed that half, so it is dropped (the
stated falsifier).

**Measured (Python 3.13.2 via `py`, Mercurial 6.6.3, Windows ANSI codepage cp1252):**
- Python, piped: `print("café ✓")` crashed with `UnicodeEncodeError: 'charmap' codec can't encode
  character '\u2713'`. With PYTHONIOENCODING=utf-8 it wrote `63 61 66 c3 a9 20 e2 9c 93`. Piped UTF-8
  input was read as 6 characters. Python 3.6+ ignores the variable for an interactive console, so
  only pipes and files change.
- hg with HGENCODING=utf-8: `hg commit -m 'm1 café ✓'` ABORTS ("'utf-8' codec can't decode byte
  0xe9"): on Windows hg receives its arguments in the ANSI codepage whatever HGENCODING says.
  Messages committed with the default setting from a UTF-8 `-l` file read back as mojibake. So
  exporting it would break a working command for every session.
- hg with the default: piped output is cp1252 (the original report stands), a UTF-8 `-l` file is
  stored double-encoded, and `-m` drops `✓`. These are hg-on-Windows behaviours with no safe
  environment fix. For READING, `hg --encoding utf-8 log …` is safe per command (no argument
  decoding involved).

**Decision:** export PYTHONIOENCODING=utf-8 at startup when the environment does not set it (a
session's own value wins). Not PYTHONUTF8, which also changes `open()`'s default for user scripts
(the 2026-10-06 position). Not HGENCODING.

**Found on the way, separate:** `"caf$(printf '\xc3\xa9').txt"` makes a file whose name hg (and
Windows) sees as `caf??.txt`; Git Bash makes `café.txt`. Recorded as the next item.

**Verified:** new case `python_utf8` (exported; a set value kept; py's piped output and input;
expected authored, Windows-specific; it skips without py); the deployed rev 179 fails it. Suite
103/103 with a console and `-Detached`; compat battery 227/237, exit 0.

**Implemented by:** rev 187. Files: `Bash/Program.cs`, `tests/cases/python_utf8.sh`,
`tests/expected/python_utf8.out`, `tests/run-tests.ps1`.

## 2026-10-10 -- DEFECT FIXED: carried bytes that spell a UTF-8 character stayed bytes (`$'\xc3\xa9'` had length 2; files named with it got invalid names)
**Status:** Active. Found 2026-10-10 by the HGENCODING test (DECISIONS "UTF-8 for external programs
= PYTHONIOENCODING only"): `hg add` skipped a file C#Bash had named `"caf$(printf '\xc3\xa9').txt"`,
which hg saw as `caf??.txt`; under Git Bash it is `café.txt`.

**Defect (measured against bash 5.1 via WSL, C.UTF-8):** a shell string carries a byte that is not
text as a raw-byte character (ShellEncoding, 2026-09-12). Bytes that came from `$'\xNN'`, from
`printf '\xNN'` captured by `$( )` or `printf -v`, or from concatenating such pieces, stayed raw
bytes even when together they spelled a valid UTF-8 character. bash, in a UTF-8 locale, sees that
character:
- `${#x}` for `x=$'\xc3\xa9'` was 2 (bash: 1); `$'\xe2\x9c\x93'` was 3;
- `[[ $x == é ]]`, `case … in café)` and `[ -e café.txt ]` failed;
- `${x^^}` left `é` alone (bash: `É`, `c3 89`);
- a file named with them got two invalid UTF-16 characters, which other programs see as `??`.

**Decision:** `ShellEncoding.Canonical`: a run of raw bytes that spells a valid UTF-8 character
becomes that character; anything else stays raw. Applied where a word's expansion is finished
(`ExpandToString`, `ExpandToFields` before brace and glob, `ExpandToPattern`, `ExpandToRegex`), so
`$'\xc3'$'\xa9'` merges too, and in builtin assignments (`AssignVar`: `printf -v`, `read`,
`getopts`). Output is still byte for byte: a canonical string encodes to the same bytes. A string
with no raw byte is returned as it is (one vectorised scan).

**Not done:** a file name containing `\` (`n$'\xe2\x9c\x93'` inside double quotes is a literal
backslash name in bash) cannot exist on Windows, so the case leaves it out.

**Verified:** new case `utf8_bytes` (13 lines: the three sources and two concatenations, invalid
bytes, a mixed string, a 3-byte character with `${x:0:1}`, `case`, file names, `${x^^}`; expected =
GNU bash via WSL); the deployed rev 179 differs on 9. hg now adds the file as under Git Bash. Suite
104/104 with a console and `-Detached`, the byte-transparency case included; compat battery
227/237, exit 0.

**Implemented by:** rev 188. Files: `Bash/Evaluator/ShellEncoding.cs`, `Bash/Evaluator/WordExpander.cs`,
`Bash/Evaluator/Builtins.Shell.cs`, `tests/cases/utf8_bytes.sh`, `tests/expected/utf8_bytes.out`,
`tests/run-tests.ps1`.

## 2026-10-10 -- SUBSET EXTENDED: awk user functions, `print > file`, `getline [var] < file`, `close`, `fflush`, 3-argument `match`, `--version`
**Status:** Active. Adds to the 2026-09-04 #7 supported list. Requested by installer-79 (2026-09-08:
`print > file` and `close()`, 3-argument `match()`, user functions, `awk --version`); `getline [var] <
file` added on the architect's "1, 2, 3 go" to Claude's position (nupkg-28 used it; `close()` is
mostly for re-reading). Under the directive "go up to but not including the scrip question".

**Behaviour, as gawk 5.1 (measured via WSL):**
- Functions (`function`/`func`): parameters beyond the arguments are locals; scalars by value, arrays
  by reference; an untyped variable becomes an array in the caller when the function uses it as one,
  through any number of calls; recursion; calls before the definition. Surplus arguments are
  evaluated, then gawk's warning, at every call that runs (gawk does not warn while parsing). An
  undefined function is gawk's fatal error, exit 2, before anything runs. `return` outside a
  function, `break`/`continue` outside a loop, and a function name used as a variable (`f (x)`
  with a space) are syntax errors, as in gawk.
- `print`/`printf` `> t` and `>> t`: `t` is a concatenation (`> "f" n` is `f1`); truncated at the
  first open, open until `close()` or the end. `"-"`, `/dev/stdout`, `/dev/stderr`, `/dev/fd/N`;
  `/dev/null`, `/dev/zero`, `/dev/random` discard (as the shell's redirects). An open failure is
  gawk's `fatal: cannot redirect to `x': <reason>`, exit 2, with gawk's `cmd. line:N:` prefix.
- `getline [var] < file`: 1, 0 at the end, -1 silently when it cannot be opened (or is a
  directory); no var sets `$0` and `NF`; never NR/FNR; `"-"` is stdin. The file is the additive
  expression after `<` (gawk: `getline < "a" "b"` reads `a`).
- `close(name)`: 0 if an output or input was open, else -1. `fflush()`: everything, 0;
  `fflush(name)`: 0, or gawk's warning and -1 when not open.
- `match(s, re, arr)`: arr cleared even on no match; `arr[n]` (strnum), `arr[n,"start"]`,
  `arr[n,"length"]` for the match and each group that took part. A capturing copy of the regex;
  every other use keeps ExplicitCapture.
- `awk --version`: `awk (C#Bash in-process subset, gawk-5.1 compatible) <build stamp>`, never
  "GNU Awk", so a script that greps for gawk does not take this for it.
- Still outside (loud, falls through): `getline` from the main input, `cmd | getline`, `print |
  cmd`, `system()`, `ENVIRON`, `RS`, `printf %e/%g`, `nextfile`.

**`return`** is a flag every block and loop checks, not an exception: break/continue/next throw,
but a throw per function call is too dear.

**Recursion limit [measured]:** the interpreter recurses on the .NET stack, and a stack overflow
kills the whole shell (it cannot be caught). Each call runs `RuntimeHelpers.EnsureSufficientExecutionStack`
and stops the program with a fatal error (exit 2) when the stack is nearly spent. Depth of
`d(n){return n==0?0:1+d(n-1)}`, main thread and pipeline stage alike: AOT (the deployed build)
900-950, JIT Release 300-350 (about 2.7 KB of stack per call). gawk did 5000. A fixed depth counter
was rejected: the stack per level differs by build and expression shape. A large-stack thread for
programs with functions would lift the limit; not done, by #7's own rule ("grow the list from
measurements of what Claude actually sends, never by inference"): no session has needed it.
**Revisit** when one does.

**Verified:** new case `awk_extended` (84 lines; expected = gawk 5.1 via WSL running the same
script; `BASH_COREUTILS=builtin`, so nothing falls through); rev 179 differs on 90 lines. `awk.sh`'s
boundary block now shows main-input `getline`, `print | "sort"` and the depth limit. Suite 105/105
with a console and `-Detached`, on the Release and the AOT build; compat battery 227/237, baseline
intact.

**Found on the way (not in this rev):** awk string escapes above 0x7F made characters, not bytes:
`"\xc3\xa9"` wrote `c3 83 c2 a9` where gawk writes `é`, `"\377"` wrote `c3 bf` where gawk writes
`ff`. Rev 179 too.

**Implemented by:** rev 193. Files: `Bash/Evaluator/Builtins.Awk.cs`, `tests/cases/awk_extended.sh`,
`tests/expected/awk_extended.out`, `tests/cases/awk.sh`, `tests/expected/awk.out`, `tests/run-tests.ps1`.

## 2026-10-10 -- DEFECT FIXED: awk string escapes above 0x7F made characters, not bytes (`"\xc3\xa9"` wrote 4 bytes, `"\377"` wrote `c3 bf`)
**Status:** Active. Found at rev 193 (that entry's "Found on the way"); in rev 179 too.

**Defect (measured against gawk 5.1 via WSL):** `AwkLex` turned `\xNN` and `\NNN` into the character
U+00NN, so `printf "\xc3\xa9"` wrote `c3 83 c2 a9` (gawk: `é`, `c3 a9`), `"\377"` wrote `c3 bf` (gawk:
`ff`), `length("\xc3\xa9")` was 2 (gawk: 1) and `"\xc3\xa9" == "é"` was false.

**Decision:** the escapes make carried bytes (`ShellEncoding.ByteChar`; an octal value is taken mod
256, as gawk stores a byte), and the literal is canonicalised as the shell's words are since rev 188:
bytes that spell a UTF-8 character are that character. A concatenation re-canonicalises only when
both sides meet at a carried byte (gawk: `"\xc3" "\xa9"` is `é`, length 1); each side is already
canonical, so the seam is the only place a new character can form, and the check is O(1).

**Verified:** new case `awk_escapes` (8 lines; expected = gawk 5.1 via WSL); rev 179 differs on 7.
Suite 106/106 with a console and `-Detached`.

**Found on the way, not in this rev:** `-v name=value` (and `name=value` operands) process only
`\n \t \r \a \b \f \v \\ \" \/`: gawk also takes octal and `\x` there (`-v s='\101\303\251'` is `Aé`;
ours printed the text unchanged). `AwkInterp.Unescape`.

**Implemented by:** rev 194. Files: `Bash/Evaluator/Builtins.Awk.cs`, `tests/cases/awk_escapes.sh`,
`tests/expected/awk_escapes.out`, `tests/run-tests.ps1`.

## 2026-10-10 -- DEFECT FIXED: awk `-v name=value` ignored octal and `\x` escapes; a `name=value` operand with a backslash was read as a file
**Status:** Active. Found at rev 194 (that entry's "Found on the way"); in rev 179 too.

**Defect (measured against gawk 5.1 via WSL):** `AwkInterp.Unescape` (for `-v`, `name=value`
operands and `-F`) knew only `\n \t \r \a \b \f \v \\ \" \/`: `-v s='\101\303\251'` printed the text
(gawk: `Aé`, length 2). And `Run` took an operand containing any backslash as a file, so
`v='\303\251'` failed "cannot open file"; the identifier-then-`=` pattern already rules out paths.

**Decision:** `Unescape` takes octal and `\x` as a string literal does (bytes, then canonical), and
the backslash test is gone: an identifier then `=` is an assignment, as in gawk, whatever the value.

**Verified:** 4 lines added to case `awk_escapes` (expected = gawk 5.1 via WSL); the rev 193 build
differs on 10 of its 12 lines. Suite 106/106 with a console and `-Detached`.

**Implemented by:** rev 195. Files: `Bash/Evaluator/Builtins.Awk.cs`, `tests/cases/awk_escapes.sh`,
`tests/expected/awk_escapes.out`.

## 2026-10-10 -- BUILT: `umask`, `times` and `fc` are builtins (they were "command not found"; `fc` ran Windows' fc.exe)
**Status:** Active. AWAITING item 4 of the resume anchor, built as Claude proposed on the architect's
"1, 2, 3 go" (2026-10-10).

**Before:** each was status 127; `fc` found `C:/Windows/System32/fc.exe` ("FC: Insufficient number of
file specifications"). `umask 077` before creating files is a common script line.

**Decision (bash 5.1 measured via WSL):**
- `umask [-p] [-S] [mode]`, after bash's umask.def: octal up to 07777 (else "octal number out of
  range", 1), kept as the 9 permission bits (`umask 1777` prints `0777`); symbolic clauses
  `[ugoa]*[+-=][rwx]*` on the permitted bits, all or none (`o+w,g=u` changes nothing: "`u': invalid
  symbolic mode character"); `=r` with no class means all; extra operands ignored; with a mode only
  `-S` prints (the new mask); bad option = message, usage, 2. Default 0022. Stored in
  ShellEnvironment, copied to pipeline stages and restored after `( )` / `$( )`, so a subshell's
  mask stays there. **It does not affect file creation:** Windows files have no mode bits to clear
  (README says so). A separate C#Bash process starts at 0022 (no inherited process umask).
- `times`: the shell's user and system CPU, then its waited-for children's (the accounting `time`
  already keeps), bash's `%ldm%d.%03ds`, truncated; operands ignored; options rejected as bash.
- `fc`: bash's option check (`-e ename`, `-lnrs`, `-N` is an offset), then "history editing is not
  supported by this interpreter", status 2, the wording of the other unsupported builtin forms.
  bash in a script lists an empty history (status 0); the case accepts both.

**Alternatives:** `umask` denying writes through ACLs: a bigger, separate design, not proposed (the
falsifier stated with the proposal). `fc` emulating bash's script-mode no-op: rejected in the
proposal for a loud "not supported", since the interactive history it would edit is not editable here.

**Verified:** new case `umask_times_fc` (53 lines; expected = GNU bash via WSL; inner script by
relative name so both shells print `t.sh: line N:`); rev 179 differs throughout. Suite 107/107 with a
console and `-Detached`; compat battery 227/237, baseline intact.

**Implemented by:** rev 196. Files: `Bash/Evaluator/Builtins.cs`, `Bash/Evaluator/ShellEnvironment.cs`,
`Bash/Evaluator/Evaluator.cs`, `README.md`, `tests/cases/umask_times_fc.sh`,
`tests/expected/umask_times_fc.out`, `tests/run-tests.ps1`.

## 2026-10-10 -- BUILT: `jq -R` / `--raw-input` (each line a string) and `-Rs` (the whole input one string)
**Status:** Active. AWAITING item 5 of the resume anchor, as Claude proposed on the architect's "1, 2, 3
go": build `jq -R`; `strings` deferred until a second session needs it (one non-blocking hit, a new
tool, not on PATH here either). msp430-a2 (2026-10-10) wanted `jq -Rs '{text: .}' file`.

**Behaviour, as jq 1.6 (measured via WSL):** the inputs are ONE text: a file with no final newline
runs into the next file's first line (`d` + `e` = `"de"`); a last line with no newline still counts;
empty input gives nothing (`-Rs`: `""`); `-n` wins (`-nR .` is `null`); errors are located at the file
and newline count where the line ends (`f2:1`), `-Rs` at the last file's count.

**Not copied:** jq 1.6 drops the rest of the next file after a file that cannot be opened under `-R`
(`jq -R . nofile f1` printed only `"a"`): a jq bug; here every line of `f1` is read.

**Found while testing, NOT fixed (recorded in PROJECT_CONTEXT OPEN DEFECTS):** short options after the
filter are read as files (`jq . f -c`; jq takes options anywhere); the open error says `Could not open
nofile` where jq 1.6 says `Could not open file nofile`; invalid UTF-8 passes through as bytes where
jq 1.6 writes U+FFFD (in JSON input too; the shell's byte transparency vs jq fidelity: the
architect's call).

**Verified:** new case `jq_raw_input` (19 probes; expected = jq 1.6 via WSL; `BASH_COREUTILS=builtin`);
rev 179 misses 42 of its lines. Suite 108/108 with a console and `-Detached`.

**Implemented by:** rev 197. Files: `Bash/Evaluator/Builtins.Jq.cs`, `tests/cases/jq_raw_input.sh`,
`tests/expected/jq_raw_input.out`, `tests/run-tests.ps1`.

## 2026-10-11 -- DECIDED: C#Bash runs scripts one command at a time, as bash does; a parse-first "safe" mode only ever as an opt-in
**Status:** Decided by the architect 2026-10-11. NOT built: an architectural change to the script runner,
its plan awaits the architect's go. Settles the "known divergence" left open in the 2026-10-08 entry on
a stray `fi` and in 2026-10-10 "syntax errors in C#Bash's own words" ("Not done").

**The ruling (verbatim):** "C#Bash needs to match (and track) users 'expectation' of 'bash', otherwise
it is not 'bash'. C#Bash primary user is, now, Claude, thus C#Bash needs to match (and track) Claudes
expectations." And: "A 'safe' C#Bash script is a different tool, if it is included, it must be
'optioned in', not the default."

**As a rule for later choices:** the default is bash as its users expect it; a C#Bash-only protection
is opt-in, never the default.

**Evidence (measured 2026-10-10, bash 5.1 via WSL against rev 197):** bash parses and runs one
top-level command at a time; C#Bash parses the whole text first. A script `echo before` / `if then`:
bash prints `before`, C#Bash nothing. A stray `fi` after `rm -f marker; touch marker`: bash ran both.
`x=$(if then)`: bash reports it when it runs it and carries on (exit 0); C#Bash runs nothing (exit 2).
A script with data after `exit 0` (installer style): bash runs it, exit 0; C#Bash fails on the data,
exit 2, so valid scripts break. A one-line `-c` command: identical (bash parses a whole line first);
multi-line `-c`: bash runs the lines before the error.

**Open, Claude's position [proposal]:** no opt-in parse-first mode now. `bash -n file && bash file`
gives exactly that with standard bash, and the habit carries to real bash. Falsifier: a human user
asking for it as one switch.

## 2026-10-11 -- DEFECTS FIXED: jq read a short option after the filter as a file (`jq . f -c`); its open error lacked jq 1.6's word "file"
**Status:** Active. Found 2026-10-10 testing `jq -R` (that entry's "Found while testing"); the
architect: "go for both jq bugs".

**Defects (measured against jq 1.6 via WSL):** short options counted only before the filter, so `jq .
f -c`, `jq '1+1' -n` and `jq -c .b f -r` failed "Could not open -c" (long options already worked
anywhere). A file that cannot be opened said `Could not open nofile`; jq 1.6 says `Could not open file
nofile`.

**Decision:** short options are options anywhere before `--`, as jq parses them; the message takes
jq's words.

**Verified:** 8 lines added to case `jq_messages` (expected regenerated from jq 1.6; its earlier lines
unchanged); rev 179 fails 6 of them. Suite 108/108 with a console and `-Detached`; compat battery
227/237, baseline intact.

**Implemented by:** rev 198. Files: `Bash/Evaluator/Builtins.Jq.cs`, `tests/cases/jq_messages.sh`,
`tests/expected/jq_messages.out`.

## 2026-10-11 -- DECIDED: a feature C#Bash lacks says so with a prefilled GitHub issue link, never an automatic post; restricted mode refuses to start
**Status:** Decided by the architect 2026-10-11 ("Yes ... your version is much better than mine"), on
Claude's counter-proposal. NOT built; GREENLIT, after the item 6 build plan.

**The request:** when a user asks for a mode C#Bash lacks, say "not currently available, a build request
has been sent to the project", posting a comment on the GitHub project.

**Decision:** print "not currently available in C#Bash" and a link that opens a NEW ISSUE on
github.com/klinkenbecker/csharpBash with title and body prefilled (the feature, the C#Bash build). The
user sends it, with one click, under their own account. Nothing is posted automatically.

**Why not the automatic post:** it needs a GitHub token with write access inside an MIT binary anyone
can open: extracted within days, used to spam or deface the repo, revoked by GitHub. And a shell that
contacts a server because a command was typed breaks the user's trust (telemetry without consent);
under Claude Code the shell's network may also be sandboxed. **Revisit** if the project runs its own
collection endpoint that users switch on explicitly.

**First use: restricted mode.** `bash -r`, `--restricted` (and running as `rbash`; `set -r` not yet
checked) are accepted and IGNORED today (`Program.cs`: `case 'r': break; // restricted: not
implemented`; `--restricted` in the no-op list), so a user who asks for restrictions gets an
unrestricted shell, silently. Decision: refuse to start, with the message and the link. Refusing is the
secure default: running unrestricted would remove the protection the user asked for.

**Not "safe modes" in this sense:** `bash -n` (check without running) works; `set -euo pipefail`
("strict mode") works.

## 2026-10-11 -- BUILT (item 6, phase 1 of 5): scripts, `-c`, `source` and `eval` run one top-level command at a time
**Status:** Active. Phase 1 of the plan the architect ratified 2026-10-11 (PROJECT_CONTEXT AWAITING 6;
DECISIONS "DECIDED: C#Bash runs scripts one command at a time").

**Change:** `RunString` (every whole-text path: script, `-c`, stdin, `source`, `eval`, an in-process
child script) parses one top-level command with `Parser.ParseNext()`, runs it, then parses the next.
A lexing error is held back (`Tokenize(deferErrors: true)` ends the tokens with an Eof and keeps the
error) and raised when the parser reaches it. The here-document-at-end-of-file warning prints when
the parser reaches the end, before that last command runs, as bash.

**The unit is the LINE, measured:** bash parses a whole line before running any of it (`echo a; fi`,
`(echo p) fi`, `echo q; )` run nothing), and only `&&` / `||` carry a command over a newline. So a
top-level list now ends at a newline, also after `;` or `&` (before, a newline was a `;` everywhere,
and one top-level list held the whole script), and any other token left on the line is a syntax error
before the line runs. Inside compound commands newlines still separate commands. `ParseNext` skips
newlines BEFORE a command, never after, so a finished command runs before anything later is read.

**Found while building:** the first version ran `echo a` in `echo a; fi`: the unit had been taken as
the command, not the line. Caught by the existing cases `stray_keyword` and `syntax_errors`.

**Verified:** new case `one_at_a_time` (expected = GNU bash via WSL: lines before an error run,
including an `rm`/`touch`; an unterminated quote; a here-document at the end; a text payload after
`exit 0`; `source`; `eval`'s first line; multi-line `-c`; same-line errors; `&&` over a newline; `-n`
still checks the whole file); rev 179 misses 19 of its lines. Suite 109/109 with a console and
`-Detached`; compat battery 227/237; `-n` over Ubuntu's 912 bash-completion scripts: 843 agree with
GNU before and after, no verdict changed.

**Implemented by:** rev 201. Files: `Bash/Lexer/Lexer.cs`, `Bash/Parser/Parser.cs`,
`Bash/Evaluator/Evaluator.cs`, `tests/cases/one_at_a_time.sh`, `tests/expected/one_at_a_time.out`,
`tests/run-tests.ps1`.

## 2026-10-11 -- BUILT (item 6, phase 2 of 5): the lexer produces tokens as the parser asks for them
**Status:** Active. Phase 2 of the ratified plan.

**Change:** `Lexer.Step(tokens)` lexes one more step into the list a parser reads (a token; after a
newline, that line's here-document bodies; the Eof), holding a lexing error back as phase 1 does.
`new Parser(lexer, source)` reads through `Fill(i)`: `Peek`, `PeekAhead`, `Advance`, `AtEnd` and the
here-document lookup lex only as far as they look. `RunString` uses it, so text after a top-level `exit`
is never lexed. The other parsers (`$( )` bodies, alias text, `ParseWords`, the REPL) still tokenize
whole: short texts.

**Here-document bodies:** a body is emitted after its line's newline; the redirect lexes on until the
next body appears. The lexer records each pending delimiter as an index into the same list the parser
removes bodies from; the order makes it safe (a line's bodies are flushed before its redirects are
parsed, and removed only then), and the here-document cases pass.

**Measured (one run each, wall clock, this machine):** 20 MB after `exit 0`: base64 268 ms, binary
520 ms, both print `installing`, exit 0 (bash: 4 ms; before phase 1: 1.3 s, and the binary one failed).
What is left is, inferred and not measured apart, reading and decoding the whole file: the plan's
out-of-scope "reading the file in chunks".

**Verified:** case `one_at_a_time` gains a 300 KB binary payload after `exit 0` (expected = GNU bash
via WSL). Suite 109/109 with a console and `-Detached`; compat battery 227/237; `-n` corpus 843/912
agree with GNU, no verdict changed.

**Implemented by:** rev 202. Files: `Bash/Lexer/Lexer.cs`, `Bash/Parser/Parser.cs`,
`Bash/Evaluator/Evaluator.cs`, `tests/cases/one_at_a_time.sh`, `tests/expected/one_at_a_time.out`.

## 2026-10-11 -- BUILT (item 6, phase 3 of 5): a syntax error inside a command substitution is met when it runs
**Status:** Active. Phase 3 of the ratified plan; the plan's one line ("that top-level command stops (1),
the next runs") was measured out in full before building, and holds.

**bash 5.1, measured via WSL:** a substitution's body is parsed when it runs, and `-n` never sees it.
- `$( )` (bare, in `"…"`, in a here-document, in a loop, in a function called from the line): the error
  is printed as `name: command substitution: line N:` plus the text bash parsed (the body with its `)`;
  inside quotes, the rest of the string), and the whole top-level command is abandoned, status 1; the
  next one runs. A subshell `( )`, a pipeline stage, an enclosing `$( )` or `<( )`, a background job
  end there instead (their own processes in bash). `source` and `eval` are their own top level.
- Backquotes: parsed in the substitution's own process: message at the command's own line, the
  substitution fails with status 2, gives nothing, and the line carries on.
- N: a one-line body is the running command's line + 1 (backquotes: the line itself).

**Change:** a body that does not parse becomes a `SubstitutionSyntaxError` node in its
`CommandSubstitutionPart`, holding the error of parsing the text bash parses (lexed on demand, so a
closing `"` after the body is not met first). Expanding it prints bash's lines; for `$( )` it throws
`CommandAbortException`, caught by RunString's loop (status 1, next command), and by `( )`, pipeline
stages, `$( )`, `<( )` and the REPL (status 1). Body parsers record their line shift so `OffsetOf`
finds their tokens in the file's text (it was wrong for nested substitutions).

**Known difference, measured, kept:** a MULTI-line `$( )` body at the top level reports its line 1 to 2
lines early (bash: the command's last line + k; C#Bash: its first line + k). No single rule fitted all
measured shapes (a backslash-continued command and a function body follow first line + k), so the
matching shapes were not traded for a guess. Message text only; the behaviour matches.

**Verified:** new case `substitution_errors` (every context above, `source`, `eval`, a background job,
`-c`, `-n`; expected = GNU bash via WSL); rev 179 misses 65 of its lines. Suite 110/110 with a console and
`-Detached`; compat battery 227/237; `-n` corpus now 844/912 agree with GNU (`invoke-rc.d` joins, as GNU
does not look inside the substitution), no other verdict changed.

**Implemented by:** rev 203. Files: `Bash/Parser/Nodes.cs`, `Bash/Parser/Parser.cs`,
`Bash/Evaluator/EvalException.cs`, `Bash/Evaluator/WordExpander.cs`, `Bash/Evaluator/Evaluator.cs`,
`Bash/Program.cs`, `tests/cases/substitution_errors.sh`, `tests/expected/substitution_errors.out`,
`tests/run-tests.ps1`.

## 2026-10-11 -- BUILT (item 6, phase 4 of 5, statuses only): `eval`'s syntax errors are status 2; its line numbers are an OPEN question
**Status:** Statuses Active. The line half is NOT built: its plan rule was falsified by measurement and
is back with the architect (PROJECT_CONTEXT AWAITING 6).

**Corrects 2026-09-04 ("P1 implemented …"):** that entry says a syntax error is "1 for eval (bash)".
Measured 2026-10-11 over 15 kinds (bash 5.1 via WSL): 2 for every one (a token, a stray `fi`, a stray
`)`, an unfinished `if`/`case`/`for`/`{`/`&&`, unclosed quotes, `$(`, `${`, `[[ -f ]]`), except an
unclosed array literal (`x=(`), 1, which its ParseException already carries. `RunEval` now passes no
status of its own. 14 of the 15 were 1.

**The line half, falsified:** the plan said eval's lines count from where the eval command ENDS, from
one sample (a two-line `eval` in `-c`: bash line 3, C#Bash 2). More samples in one script: `eval "if
then"` on line 7, inside a top-level `if`, is bash's line **5**; `eval "x=("` on line 13 is **11**; a
two-line eval on lines 10-11 erring on its line 2 is **10**. C#Bash prints the physical lines 7, 13, 11.
No rule from where the command starts or ends fits, so none was built.

**Verified:** 7 status probes added to case `one_at_a_time` (expected = GNU bash via WSL). Suite 110/110
with a console and `-Detached`.

**Implemented by:** rev 204. Files: `Bash/Evaluator/Evaluator.cs`, `tests/cases/one_at_a_time.sh`,
`tests/expected/one_at_a_time.out`.

## 2026-10-11 -- VERIFIED (item 6, phase 5 of 5): no slowdown from reading one command at a time
**Status:** Active. The ratified plan's last phase.

**Method, unchanged:** `tests/bench/compare3.sh --cs <exe>` under Git Bash, both builds AOT (as
shipped): rev 200 (the code before item 6, from `hg archive`; its stamp reads a bare `1.0.0`) and rev
204, one run each, nothing else running; both tables are in `tests/bench/results.md`.

**Result (seconds, C#Bash, rev 200 → rev 204):** loop 0.165 → 0.157, arith 0.258 → 0.254, func 0.222 →
0.210, loop_big 1.441 → 1.421, coreutils 0.060 → 0.059, pipeline 0.250 → 0.253, find 0.021 → 0.023,
startup 0.0164 → 0.0166. All within the method's noise (Git Bash's own startup moved 0.0165 → 0.0171
between the two runs); outputs agree in every row. Suite 110/110 on the rev 204 AOT build, with a console
and `-Detached`.

**Item 6 stands at:** P1-P3 and P5 done, P4 done for statuses; P4's line numbers are with the architect.

## 2026-10-11 -- Compat battery: probe 145 leaves the baseline (Git Bash 4.4's `eval` status), 226 to 225
**Status:** Active. Found by re-running the battery after phase 4 (the phase 4 and 5 entries above were
written before it was run; this closes that gap).

Probe 145 ends with `eval 'echo ev'"'"'al'`, an unclosed quote. The battery's reference is Git for
Windows' bash 4.4.23, which gives 1; GNU bash 5.1 (WSL) gives 2, as for every eval syntax error but an
unclosed array literal (phase 4). The ruling of 2026-10-03 is GNU bash on Linux, not Git for Windows'
quirks; probe 199 left the baseline the same way (2026-10-10, FUNCNAME). So 145 leaves it too, edited
by hand (`-UpdateBaseline` would also add probe 172, which is kept out on purpose). Battery: 226/237
against the baseline of 225, no regression.

## 2026-10-11 -- DECIDED: error messages keep the PHYSICAL line (eval, multi-line `$( )`), not bash's drifting numbers
**Status:** Decided by the architect 2026-10-11 ("physical line"), on Claude's position. Closes the line
half of item 6 phase 4, and the multi-line `$( )` difference of phase 3.

bash 5.1 numbers some of these lines in a way no measured rule fitted (`eval "if then"` on line 7, inside
a top-level `if`, is its line 5; `eval "x=("` on line 13 is 11; a multi-line `$( )` body at the top level
is 1-2 lines later than its physical line). C#Bash prints the line the error is on: what a reader of
the message, Claude above all, looks at. A documented difference from bash, kept on purpose.
**Revisit** if matching bash's numbers is ever needed: read bash's evalstring.c first, do not guess.

## 2026-10-11 -- BUILT: restricted mode is refused with a prefilled-issue link (`-r`, `--restricted`, `rbash`, `set -r`)
**Status:** Active. Builds the 2026-10-11 decision "a feature C#Bash lacks says so … restricted mode
refuses to start", GREENLIT by the architect ("next go").

**Before:** `bash -r` and `--restricted` were accepted and ignored (`cd /` worked); `set -r` said
"invalid option" (it is valid in bash) and the script went on unrestricted.

**Now:** `-r` (alone or in a cluster such as `-cr`), `--restricted` and a program named `rbash` refuse to
start, status 2: `bash: restricted mode (-r) is not currently available in C#Bash` and `bash: to ask
for it, open: <link>`, the link opening a NEW issue on github.com/klinkenbecker/csharpBash with title
and body filled in (the feature and the build stamp, never the command line, which can hold secrets;
no final `.`, which terminals drop from links). `+r` stays harmless; `shopt restricted_shell` is off.

**`set -r`, decided by Claude while building (the decision named it "not yet checked"):** the shell
ENDS, status 2, with the same lines; a subshell ends only itself, as bash restricts only it. Failing just
the `set` and going on would run the rest of a script that asked for restrictions without them: refusing
is the secure default, the run-time form of refusing to start.

**Shared:** `NotAvailable` (message + link) for the next feature that says so; `BuildInfo.Stamp`, now the
one place the build stamp is read (Program.cs and awk's `--version` read it there).

**Seen, not changed (open, for the architect):** `--posix` is also accepted and silently ignored. Not a
safety mode; POSIX mode changes a set of behaviours.

**Verified:** new case `restricted_mode` (authored: bash has restricted mode, so there is no GNU oracle
for refusing it; the stamp is masked; it checks the command text never reaches the link); rev 179 differs
on 16 of its lines. `rbash` checked by hand (a copy of the launcher named rbash.exe: refused, status 2).
Suite 111/111 with a console and `-Detached`; compat battery 226/237 against 225.

**Implemented by:** rev 208. Files: `Bash/Evaluator/NotAvailable.cs` (new), `Bash/Program.cs`,
`Bash/Evaluator/Builtins.Shell.cs`, `Bash/Evaluator/Builtins.Awk.cs`, `tests/cases/restricted_mode.sh`,
`tests/expected/restricted_mode.out`, `tests/run-tests.ps1`.

## 2026-10-11 -- BUILT: POSIX mode says it is not available and goes on (`--posix`, `-o posix`, `set -o posix`, `shopt -so posix`)
**Status:** Active. The architect: "fix --posix", on Claude's position (notice and go on, not refuse).

**Before:** all four were accepted and silently ignored. **Now:** each prints `POSIX mode (<how>) is not
currently available in C#Bash; going on in bash's default mode` and the prefilled-issue link
(`NotAvailable.PosixMode`), and goes on, status 0; `set +o posix` says nothing. Not refused, unlike
restricted mode: POSIX mode only narrows bash's behaviour, so a script that asks for it mostly runs as
it is, and it protects nothing.

**Seen, not changed (open, for the architect):** `ShellOptions.ApplyLong` also accepts with no effect
options that change behaviour: `onecmd` (`-t`, exit after one command), `keyword` (`-k`), `functrace`
(`-T`), `physical` (`-P`), `privileged` (`-p`); and `POSIXLY_CORRECT` in the environment (bash's other way
into POSIX mode). The rest of that list (emacs, vi, history, monitor, …) has nothing to do outside an
interactive terminal.

**Verified:** 3 lines added to case `restricted_mode` (authored). Suite 111/111 with a console and
`-Detached`.

**Implemented by:** rev 209. Files: `Bash/Evaluator/NotAvailable.cs`, `Bash/Program.cs`,
`Bash/Evaluator/Builtins.Shell.cs`, `tests/cases/restricted_mode.sh`, `tests/expected/restricted_mode.out`.

## 2026-10-11 -- DEPLOYED: rev 209 (revs 180-209), notice sent
**Status:** Active. The architect: "fix --posix, then deploy, notify and publish".

`tools/publish-aot.cmd` into the scratchpad (never `dist/` directly: the script empties its output
directory, where the rollback copies live), stamp `1.0.0+hg.4fa037d780c8 209` (committed tree). Suite
111/111 on that exact binary with a console and `-Detached`; battery 226/237 against 225. The running
rev 179 was renamed to `dist/Bash.exe.rev179` (Windows renames a running image; it cannot overwrite it)
and rev 209 copied in. Live check in a Claude Code Bash call: rev 209, awk functions, umask, jq -R, the
lines before a syntax error run, -r refused, eval status 2, `git -C /f/...` (MSYS path conversion).
Notice to the six running sessions, leading with the six behaviour changes.

## 2026-10-11 -- PUBLISHED: GitHub main = git e8c2d39 = hg rev 211
**Status:** Active. The architect: "deploy, notify and publish"; the push rule of 2026-10-10 ("another
benchmark and a doc review first") followed.

Benchmark: `compare3.sh` on the deployed rev 209 (README table). Doc review: README (table, startup
method, reading model, restricted/POSIX notices, awk and jq additions, 111 cases, and the
`BASH_COREUTILS=external` crash stated as a known defect rather than left advertised as working),
ARCHITECTURE, IMPLEMENTATION. History: the table generator was first checked to reproduce the 183
published rows byte for byte (a `|` in a summary is `\|`), then run for revs 211-0; the bundle (212
changesets) unbundled into an empty repository to rev 211. git: the same 47 files hg changed in revs
183-211, plus the two history files (the `.url` left out); pushed with a one-off auth header from the
token file (no token in config afterwards); `git ls-remote` shows `e8c2d39`.

## 2026-10-11 -- DEFECT FIXED: `BASH_COREUTILS=external` killed the shell (stack overflow)
**Status:** Active. Found 2026-10-11 checking msp430-a2's deploy reply; the architect: "yes" (fix it
next).

**Defect (every build checked: revs 156, 179, 209):** `BASH_COREUTILS=external awk …` (any in-process
tool with a PATH twin: grep, sort, cat …) ended the shell process, "Process is terminating due to
StackOverflowException", rc 139, losing everything after it. The README documents the mode.

**Cause (read in the code, not inferred):** `Builtins.TryExecute`'s external branch called
`_eval.RunCommand(name, …, skipFunctions: true, …)`. `RunCommand` runs a builtin when the name is one,
through `_builtins.TryExecute`, which took the external branch again: unbounded recursion. The
unsupported-option fallback a few lines down already called `RunExternal`, the PATH program only.

**Fix:** the external branch calls `_eval.RunExternal(name, args)`. A tool with no PATH twin, and
anything that is not a coreutil, stays in-process, as before.

**Verified:** new case `coreutils_external` (authored; needs Git for Windows: GNU Awk and GNU cat answer
`--version`, grep/sort/jq work, the shell survives); deployed rev 209 dies on its first line. Suite
112/112 with a console and `-Detached`; compat battery 226/237 against 225. README: the "known
defect" note pushed with rev 211 is removed here; GitHub keeps it until the next push.

**Implemented by:** rev 213. Files: `Bash/Evaluator/Builtins.cs`, `README.md`,
`tests/cases/coreutils_external.sh`, `tests/expected/coreutils_external.out`, `tests/run-tests.ps1`.

## 2026-10-11 -- DEPLOYED: rev 213 (the `BASH_COREUTILS=external` fix), notice sent; pushed with it
**Status:** Active. The architect: "deploy, notify and push".

AOT build `1.0.0+hg.40a4296afbd6 213` (committed tree), suite 112/112 on it with a console and
`-Detached`; rev 209 renamed to `dist/Bash.exe.rev209`, rev 213 copied in; live check in a Claude Code
Bash call (rev 213, `BASH_COREUTILS=external awk --version` is Git's GNU Awk 5.0, the shell survives).
One-fix notice to the six sessions. Push rule followed: `compare3.sh` on deployed rev 213 (every row
within noise of the rev 209 run; the README table is now this run, build rev 213), doc review (the
README's known-defect note gone; nothing else changed since rev 211).

**Pushed:** GitHub `main` = git `1ab0a25` = hg rev 214 (`e8c2d39..1ab0a25`; history 215 changesets, bundle verified; no token left in config; `git ls-remote` confirmed).

## 2026-10-11 -- BUILT: `-t` (onecmd), `-k` (keyword), `-p` (privileged) act as in bash; they were accepted and ignored
**Status:** Active. The architect: "go" on the open items; implement-or-notice decided by the ratified rule
(the default is bash as its users expect it): implemented where it is sound, the notice where the
machinery is missing.

**Measured in bash 5.1 (WSL) and built:**
- `-t` / `set -t` / `-o onecmd`: the shell's own input (a script, `-c`, stdin; not `source`, `eval` or a
  trap, as bash's reader loop) runs ONE command, a whole line or compound command, then the shell exits
  with its status; `set -t` is itself that command. `RunString(…, mainLoop: true)` from Program.cs and the
  in-process child script; the REPL exits after one command too.
- `-k` / `-o keyword`: a `NAME=value` / `NAME+=value` word anywhere after the command name is a
  temporary assignment for the command, as a prefix one is, not an argument; `declare D=1` and
  `export E=2` lose theirs too (their arguments arrive as `AssignmentArgPart`). `KeywordAssignment`.
- `-p` / `-o privileged`: `BASH_ENV` is not read; `p` is in `$-`. bash's setuid effects mean nothing
  on Windows.
- `$-` now lists `k p t` in bash's places and `s` LAST (`hBs`; it stood before `u`: an older mismatch,
  measured while here). The three join the subshell pack/unpack lists.

**Still accepted with no effect, by reason:** `-b -m -H` (job notices, job control, `!` history: nothing
in a script in bash either); `-P` (`cd -P`, which an older decision treats as moot without symlinked
cwds); `-T` (it extends DEBUG and RETURN traps into functions, and those traps NEVER run here: `trap …
DEBUG` / `trap … RETURN` are accepted and silent, found 2026-10-11, OPEN with the architect).

**Verified:** new case `options_tkp` (expected = GNU bash via WSL); deployed rev 213 misses 12 of its
lines. Suite 113/113 with a console and `-Detached`; compat battery 226/237 against 225.

**Implemented by:** rev 216. Files: `Bash/Evaluator/ShellOptions.cs`, `Bash/Evaluator/Evaluator.cs`,
`Bash/Program.cs`, `tests/cases/options_tkp.sh`, `tests/expected/options_tkp.out`, `tests/run-tests.ps1`.

## 2026-10-11 -- BUILT: DEBUG and RETURN traps run, `$BASH_COMMAND` exists, `-T` and `declare -ft`; trap `$LINENO`
**Status:** Active. The architect: "Build" (Claude's position: build them, a check per command when no
trap is set; falsifier: a measured per-command cost above noise, then a notice instead).

**Before:** `trap … DEBUG` and `trap … RETURN` were accepted and never ran; `$BASH_COMMAND` was empty
everywhere (an ERR trap's `error in: $BASH_COMMAND` printed nothing); `-T` / `-o functrace` were ignored;
`declare -ft f` printed f and ignored -t; a trap's `$LINENO` was 1 (an ERR trap said line 1).

**bash 5.1, measured via WSL, and now matched:** DEBUG runs before each simple command (assignments
too), `[[ ]]`, `(( ))`, each `for` turn (`for i in a b`), an arithmetic for's init, each test and each
step (`((i<2))`), a `case` (`case $x in `), each pipeline element (in the parent, before the stage);
NOT inside functions, `( )`, `$( )`, `<( )`, pipeline stages or background jobs, unless -T or the
function's trace attribute (`declare -ft`, cleared by `declare -f +t`; in `declare +ft` the t is a
variable's: nothing), with which it also runs once at a function's entry. RETURN runs after `source`
(out of its frame) and after a function that set it, or after every function with -T (in its frame).
A trap never traces itself; `$?` survives DEBUG; RETURN keeps the function's status. `$LINENO` in DEBUG
and ERR is the command's line, in RETURN the function's definition line or the `source` line; EXIT
stays 1, as bash. `$BASH_COMMAND` is bash's rendering: words as written one space apart, `> target`
but `2>&1` and `<<EOT`, a here-document's body after it, `$'s'` as `'s'`, no `!` or `time`.

**Design:** tokens carry their source offsets (`Lexer.Make` stamps `Start`/`End`); the traced nodes
keep a `SourceSpan` (a reference and two ints, no text); `CommandText.Render` re-lexes a span only when
`$BASH_COMMAND` is read. The evaluator keeps the current command (`SetCommand`, one field store per
command; not changed inside a trap, so a trap sees the command it is for), a `_debugOff` count for the
non-inheriting scopes, a `_debugSkip` for a stage's own command (already traced by the parent), and
call ids so RETURN fires for the function that set it. `RunTrap(…, atLine)` reads a trap's text as at
the line it fires for. `shopt -s extdebug` (DEBUG's status skipping a command) stays refused, loudly.

**Not matched, known:** text inside `$( )` in `$BASH_COMMAND` has its spaces collapsed to one (bash
keeps them as written; unmeasured beyond the probes). Whether DEBUG runs inside other traps (EXIT, ERR):
here never; bash not measured.

**Cost (the falsifier, not triggered):** `compare3.sh` back to back, AOT, this build vs deployed rev
213 (seconds): loop 0.163/0.164, arith 0.248/0.246, func 0.218/0.212, loop_big 1.421/1.434, coreutils
0.058/0.056, pipeline 0.248/0.246, find 0.022/0.021, startup 0.0161/0.0158: within the run-to-run
spread (func ranged 0.210-0.217 across today's runs). `declare -ft` came after, adding one count test
per function call.

**Verified:** new case `traps_debug_return` (9 probe scripts; expected = GNU bash via WSL); deployed rev
213 misses 63+ of its lines. Suite 114/114 with a console and `-Detached`; compat battery 226/237
against 225; `-n` corpus 844/912, unchanged.

**Implemented by:** rev 217. Files: `Bash/Lexer/Token.cs`, `Bash/Lexer/Lexer.cs`, `Bash/Parser/Nodes.cs`,
`Bash/Parser/Parser.cs`, `Bash/Parser/CommandText.cs` (new), `Bash/Evaluator/Evaluator.cs`,
`Bash/Evaluator/ShellEnvironment.cs`, `Bash/Evaluator/ShellOptions.cs`, `Bash/Evaluator/Builtins.Shell.cs`,
`Bash/Evaluator/WordExpander.cs`, `tests/cases/traps_debug_return.sh`,
`tests/expected/traps_debug_return.out`, `tests/run-tests.ps1`, `tests/bench/results.md`.

**Deployed 2026-10-11 ("yup"):** rev 217 (`1.0.0+hg.64cd033c7de6 217`), revs 214-217 with this entry and the `-t -k -p` one; suite 114/114 on the AOT binary with a console and `-Detached`; live check in a Claude Code Bash call (`ERR at 3: false`, a DEBUG trace, `-t`); rollback `dist/Bash.exe.rev213`; notice to the six sessions.

## 2026-10-11 -- DEFECT FIXED: `$0` was the launcher's backslash path (every `-c` error prefix under Claude Code); a PATHEXT match's argv[0] was spelt `bash.EXE`
**Status:** Active. OPEN DEFECTS item "`$0` under -c is a full backslash path" (seen 2026-10-10, not
analysed); the architect: "go next".

**Measured (the cause, not inferred):** a launcher passes the resolved native path as argv[0]: Claude
Code `F:\…\dist\bash.exe`, Git Bash `F:\…\Bash.exe`, C#Bash itself `F:\…\bash.EXE` (the typed name plus
PATHEXT's spelling). C#Bash took `$0` from `GetCommandLineArgs()[0]` verbatim, so `$0` and every `-c` error
prefix carried backslashes (`F:\…\bash.exe: -c: line 1: …`); on the JIT build it was `Bash.dll` (the
apphost's). Git Bash shows its own path in its POSIX form (`/usr/bin/bash`).

**Decision, from the record (the shell's path form is forward slashes, 2026-09-12; `$BASH` since rev
154):** `$0` from argv[0] is the command line's first token as the launcher passed it, in the shell's
forward-slash form (`ShellEnvironment.LaunchedAs`, GetCommandLineW: right on the JIT build too). A `-c`
name argument and a script path stay as typed. A program found through PATHEXT is launched with its
on-disk name (`ShellFile.OnDiskName`, cached per name), so a child's argv[0] is `…\Bash.exe`, as Git Bash
passes; the launch keeps the native path (rev 160's decision).

**Found while testing, OPEN (next):** a temporary `PATH=…;… cmd` is not used to find `cmd` (bash
searches it); exported, it is. PATH here holds Windows entries split by `;` (documented), so a test must
use `;`.

**Verified:** new case `arg0_path` (authored: the interpreter under test launched by name through PATH;
no backslash in `$0` or the error prefix; its child's argv[0] is `Bash.exe`; a `-c` name kept); rev 217
fails 3 of its 4 lines. Suite 115/115 with a console and `-Detached`; compat battery 226/237 against 225.

**Implemented by:** rev 219. Files: `Bash/Evaluator/ShellEnvironment.cs`, `Bash/Evaluator/ShellFile.cs`,
`Bash/Evaluator/Builtins.Shell.cs`, `Bash/Evaluator/Evaluator.cs`, `tests/cases/arg0_path.sh`,
`tests/expected/arg0_path.out`, `tests/run-tests.ps1`.

## 2026-10-11 -- DEFECTS FIXED: command lookup ignored the shell's PATH (stale hash after a PATH change; temporary `PATH=` not searched; a subshell's PATH leaked to the parent)
**Status:** Active. Found 2026-10-11 testing `$0`; the architect: "go next".

**Measured (bash 5.1 with scripts; C#Bash rev 217 with .cmd files, PATH entries split by `;`):** after
`export PATH=B;…` put B before A, C#Bash still ran A's program (bash: B: "any assignment to PATH resets the
hash table"); `PATH=A;… cmd` was not searched for cmd (bash searches it, and does not hash it);
`( export PATH=C;… )` let the parent then find C's program (bash: not found).

**Cause (read in the code):** `ResolveOnPath` (and `type -a`, `which`, `source`'s sourcepath) read the
PROCESS environment's PATH, which `Set("PATH")` writes and nothing restores after a subshell; a temporary
assignment never reaches it; and its hash table was cleared only by `hash -r`.

**Fix:** lookups read the running shell's own PATH (`ShellEnvironment.CurrentPath`; a subshell's or a
stage's is its own); the hash is reset on every PATH assignment (`PathChanged`) and whenever the value is
not the one it was built for (a restored subshell); a temporary `PATH=` is searched for that command only,
not hashed. Child programs were already fine: their environment comes from the shell's exported variables
(checked: the leaked PATH never reached a child).

**Verified:** new case `path_lookup` (authored; the bash-measured sequence from-A, from-B, from-A, from-B,
not found; `hash` empty after `PATH=$PATH`); rev 217 fails 5 of its lines. Suite 116/116 with a console and
`-Detached`; compat battery 226/237 against 225.

**Implemented by:** rev 220. Files: `Bash/Evaluator/ShellEnvironment.cs`, `Bash/Evaluator/Evaluator.cs`,
`Bash/Evaluator/Builtins.cs`, `Bash/Evaluator/Builtins.Shell.cs`, `tests/cases/path_lookup.sh`,
`tests/expected/path_lookup.out`, `tests/run-tests.ps1`.

## 2026-10-12 -- DEFECT FIXED: a `case` inside `$( )` ended the substitution at its first pattern's `)`
**Status:** Active. The next OPEN DEFECT after the PATH lookup; the architect: "go next".

**Measured (GNU bash 5.1 via WSL; C#Bash rev 217):** `x=$(case a in a) echo yes;; esac)` is a syntax error in
C#Bash (`near unexpected token ';;'`) that loses the whole line; bash runs it. The same in `"$( )"` and in
backquotes, with `(pat)` patterns, nested in loops, `if`, a subshell in a branch, and a `case` in a `case`'s
branch. `$(echo case in; …)` is only words in both.

**Cause:** both readers of `$( )` (the lexer's text scan `ConstructEnd`, used for `"$( )"` and backquotes,
and the parser's token scan `ParseCommandSubstitution`) counted every `(` and `)`. bash's `parse_comsub`
knows where a `case` is and does not count its patterns' parentheses.

**Fix:** `Lexer.CaseTracker`, shared by both scans: `case` is a keyword only at a command start (after a
separator or `then`/`do`/`else`/`elif`/`if`/`while`/`until`/`!`/`{`/`time`); after `case WORD in` each
pattern runs to its `)` (an opening `(` optional) and its parentheses do not change the depth; the branch body
counts as usual up to `;;`/`;&`/`;;&`; `esac` closes it. Quoted or `$`-bearing words are never keywords.

**Found in testing, never shipped:** the first text-scan version did not move past a bare `$name`
(`"$(echo $HOME)"` hung the shell; the suite stopped on `array_index_assign`). A zero-length word now falls
through to the ordinary character step; case lines 13-14 cover it.

**Verified:** new case `case_in_substitution` (14 lines, expected = GNU bash via WSL; rev 217 fails all of
them on its line-5 syntax error). Suite 117/117 with a console and `-Detached`; compat battery 226/237
against 225 (Git Bash reference; 172 environment-dependent, left out); `-n` corpus 844/912, unchanged.
Cost not measured (the tracker runs only while scanning a `$( )` body) [unmeasured estimate: within noise].

**Implemented by:** rev 221. Files: `Bash/Lexer/Lexer.cs`, `Bash/Parser/Parser.cs`,
`tests/cases/case_in_substitution.sh`, `tests/expected/case_in_substitution.out`, `tests/run-tests.ps1`.

## 2026-10-12 -- DEFECT FIXED: `kill -l` / `trap -l` printed nine names, not bash's numbered table; `kill`'s options and `SIG` names drifted from bash
**Status:** Active. The next OPEN DEFECT (found by the `builtin_options` work, 2026-10-10 entry "Not done");
the architect: "go next". Closes that entry's `kill -l`/`trap -l` item.

**Measured (GNU bash 5.1 via WSL; C#Bash rev 217):** `kill -l`, `kill -L` and `trap -l` print the 62-signal
Linux table, `%2d) SIGNAME` five to a row joined by tabs, the last row keeping its tab; C#Bash printed one
line of nine names. `trap -l INT -p` ignores its operands and `-p`. `kill -l 0` is `EXIT`; `kill -l 32`/`33`
print nothing (status 0); `kill -l EXIT|DEBUG|ERR|RETURN` give 0/65/66/67. bash's kill.def takes the FIRST
`-WORD` as the signal and checks it only when killing: `kill -l -9` lists, `kill -l -1 2` prints `INT`;
`kill -sINT` / `kill -n9` are attached forms; `kill -sINT` with no pid is the usage line; `kill -` is
`: invalid signal specification`. `SIGEXIT`, `SIGDEBUG`, `SIGERR`, `SIGRETURN`, `SIG2`, `SIGSIGINT` are
invalid in `kill -l` and in `trap` (C#Bash accepted every one in `trap`).

**Fix:** one `PrintSignalTable` (from the existing `LinuxSignals`) for both builtins; `kill`'s option loop
follows kill.def; `CanonSignal` takes `SIG` only before a real signal's name (bash's decode_signal).
`SignalNames` (the nine) is gone.

**Not done:** POSIX mode's `kill -l` (measured: `bash --posix -c 'kill -l'` prints `HUP INT QUIT …` on one
line, space-separated; `trap -l` keeps the table): C#Bash's `--posix` is a notice only (2026-10-11), so
there is nothing to switch on.

**Verified:** new case `signal_list` (82 lines, expected = GNU bash via WSL; rev 217 misses 91 diff lines).
Suite 118/118 with a console and `-Detached`; compat battery 226/237 against 225 (Git Bash reference).

**Implemented by:** rev 222. Files: `Bash/Evaluator/Builtins.cs`, `Bash/Evaluator/Builtins.Shell.cs`,
`tests/cases/signal_list.sh`, `tests/expected/signal_list.out`, `tests/run-tests.ps1`.

## 2026-10-12 -- DEFECT FIXED: pushd / popd / dirs refused `+N` / `-N`, and took `-n` to mean "print nothing"
**Status:** Active. The next OPEN DEFECT (the 2026-10-10 `builtin_options` entry, "Not done"); the architect:
"go next". Measuring the rotation showed the rest of the stack drifting too.

**Measured (GNU bash 5.1 via WSL; C#Bash rev 217):** `+N` / `-N` rotate (pushd), remove (popd) and show
(dirs) entry N counted from the left / the right of `dirs`; C#Bash refused them. `-n` means "no cd", not
"quiet": `pushd -n DIR` saves DIR, as written, as entry 1 and prints the stack; `popd -n` removes entry 1 and
prints. `pushd -n +N` rotates without changing directory and DROPS the entry it would have changed to (bash
rotates first, then would cd), printing nothing; `pushd +0` cds to `.` and prints, even on an empty stack.
`dirs -p` (one per line) and `-l` (no `~`) did nothing in C#Bash; `-v -l` combine; `dirs -lv` is an invalid
number. Out of range: `dirs: 9: …` (no sign), `pushd: +9: …`; an empty stack says `directory stack empty`
first. `~` replaces HOME only at a `/` boundary and only when HOME is longer than `/` (C#Bash turned
`/home/ux` into `~x` under HOME=/home/u). cd's errors under pushd say `pushd:`; C#Bash said `cd:`.

**Fix:** Pushd, Popd and Dirs rewritten from bash's pushd.def logic over C#Bash's stack (entry 0 is the
current directory, `_dirStack` holds 1…); `Cd`'s messages carry the calling builtin's name.

**Not done (found, not raised for action):** `DIRSTACK`, bash's array view of the stack (assignable by
index); C#Bash has no such variable. No script in the suite or the battery reads it.

**Verified:** new case `dir_stack` (209 lines, expected = GNU bash via WSL; rev 217 misses 293 diff lines).
Suite 119/119 with a console and `-Detached`; compat battery 226/237 against 225 (Git Bash reference).

**Implemented by:** rev 223. Files: `Bash/Evaluator/Builtins.Shell.cs`, `tests/cases/dir_stack.sh`,
`tests/expected/dir_stack.out`, `tests/run-tests.ps1`.

## 2026-10-12 -- DEFECT FIXED: `jobs -x` was refused; it runs a command with jobspecs replaced, as bash
**Status:** Active. The last item of the 2026-10-10 `builtin_options` "Not done" list; the architect: "go next".

**Measured (GNU bash 5.1 via WSL; C#Bash rev 217 refused `-x`, status 2):** `jobs -x cmd args` replaces each
word that STARTS with `%` and names a job by the job's process group (`pre%1` stays; `%9`, no such job, stays
silently) and runs the words as one simple command with no further expansion (builtin, function or program;
`nosuchcmd` is 127; no words is status 0). `-x` must come before `-l`/`-p`/`-n` (`jobs -lx`: "no other
options allowed with `-x'", 1; `jobs -xl` runs). The group in a script is the shell's own (568 = `$$` in
the measurement), not `$!`; under job control (`bash -i`, `set -m`) it is the job's leader (`$!`; a
pipeline's first process).

**Decision (Claude's call, stated):** Windows has no process groups. C#Bash substitutes the job's pid when
interactive (what `jobs -p` prints) and `$$` in a script, following bash's rule with the shell's own group
read as `$$` (true when the shell leads its group, as a script run from a terminal does). Falsifier: a
session needing `jobs -x` in a script to name the job, which bash itself does not do there. C#Bash ties job
control to `Interactive` as its `$-` already does (`set -m` in a script changes nothing).

**Found, recorded as an OPEN DEFECT:** C#Bash's job lookup has no `%name`, `%?text` or bare `%` (bash
resolves all three, or says "ambiguous job spec"); `jobs`, `kill`, `wait`, `fg`, `disown` and `jobs -x` share
it. Measured: bash's `kill %?4` kills the job; C#Bash's says "no such job".

**Verified:** new case `jobs_x` (expected = GNU bash via WSL, `$$` normalised; the interactive line checked to
vanish without `-i`; rev 217 misses 53 diff lines). Suite 120/120 with a console and `-Detached`; compat
battery 226/237 against 225 (Git Bash reference).

**Implemented by:** rev 224. Files: `Bash/Evaluator/Builtins.cs`, `Bash/Evaluator/Evaluator.cs`,
`tests/cases/jobs_x.sh`, `tests/expected/jobs_x.out`, `tests/run-tests.ps1`.

## 2026-10-12 -- DEFECT FIXED: jobspecs `%name`, `%?text` and a bare `%` found no job
**Status:** Active. Found 2026-10-12 making `jobs -x` (entry above); fixed next, as the PATH lookup was.

**Measured (GNU bash 5.1 via WSL; C#Bash rev 224):** bash's get_job_spec takes `%name` (a job one of whose
commands STARTS with name, case kept), `%?text` (CONTAINS text, case ignored: strcasestr) and `%` alone (the
current job). Two matching jobs: "NAME: ambiguous job spec" from the builtin, then its usual "no such job".
A match on a pipeline's later command (`%?cat` against `sleep 4 | cat`) is ALWAYS ambiguous: get_job_by_name
does not step past a match there and meets it again (read in bash 5.1's jobs.c, agreeing with both measured
cases). C#Bash found none of the three.

**Fix:** `FindJobIn` follows get_job_spec; a job keeps each pipeline stage's command (`BackgroundJob.Stages`)
to match per process as bash does; the ambiguous message names the calling builtin (`fg` reports as `wait`,
as its other messages already did).

**Found, recorded as OPEN DEFECTS (measured):** (1) `a && b &` / `a || b &` background only the LAST pipeline:
`sleep 1 && echo A &` sleeps in the foreground (bash backgrounds the whole and-or list); rev 217, deployed, does
it too. (2) A compound job's text is its node type: `jobs` shows `BraceGroup`, `Subshell`, `ForCommand`
where bash shows `{ sleep 1; echo hi; } > /dev/null`, `( sleep 1 )`, its re-printed `for`.

**Verified:** new case `jobspec_names` (expected = GNU bash via WSL, identical on two runs; rev 217 misses 39
diff lines; a `wait` after `kill` left out: whether bash has reaped the job by then is a race). Suite 121/121
with a console and `-Detached`; compat battery 226/237 against 225 (Git Bash reference).

**Implemented by:** rev 225. Files: `Bash/Evaluator/Evaluator.cs`, `Bash/Evaluator/BackgroundJob.cs`,
`tests/cases/jobspec_names.sh`, `tests/expected/jobspec_names.out`, `tests/run-tests.ps1`.

## 2026-10-12 -- DEFECT FIXED: `a && b &` backgrounded only `b`; the and-or list ran its head in the foreground
**Status:** Active. Found 2026-10-12 testing jobspecs (entry above); fixed next. In every build to rev 225,
the deployed rev 217 included.

**Measured (GNU bash 5.1 via WSL):** `sleep 1 && echo A &` returns at once and `jobs` shows `sleep 1 &&
echo A &`; C#Bash slept 1 s in the foreground and listed `echo A`. As one job, the list's `cd` and
assignments stay in it: `cd / && true &` leaves the shell where it was, `v=1 && true &` leaves v unset;
C#Bash moved the shell and set v. Same for `||` and mixed chains (`true && u=3 || true &`).

**Cause:** `ExecList` applied each item's `&` to that item alone; `&&` / `||` bind tighter than `&` in the
grammar, so the job is the whole chain back to the previous `;`, `&` or newline.

**Fix:** at the start of an and-or chain whose last item ends in `&`, the chain becomes one sub-list
(`l with { Items = … }`, keeping its line and span) and one background job; `$?` is 0 after starting it.
`DescribeNode` renders a list with its own operators (` && `, ` || `), not ` ; `.

**Verified:** new case `and_or_background` (expected = GNU bash via WSL, identical on two runs; rev 217 misses
15 diff lines; `jobs` is shown first, while bash's table holds no finished jobs: bash keeps those until it
reports them, so later job numbers differ, a separate behaviour). Suite 122/122 with a console and
`-Detached`; compat battery 226/237 against 225 (Git Bash reference).

**Implemented by:** rev 226. Files: `Bash/Evaluator/Evaluator.cs`, `tests/cases/and_or_background.sh`,
`tests/expected/and_or_background.out`, `tests/run-tests.ps1`.

## 2026-10-12 -- DEFECT FIXED: field splitting and `$*` ignored what IFS held (IFS='' split anyway; IFS=, lost empty fields)
**Status:** Active. Found 2026-10-12 verifying a workaround for daq-d1 (who reported `compgen` missing):
`local IFS= f; for f in $1` split a path at its space in C#Bash, so the workaround failed there.

**Measured (GNU bash 5.1 via WSL; C#Bash rev 217; 64 cases):** IFS set to "" means no splitting at all; C#Bash
took it as unset. Splitting is POSIX 2.6.5: IFS white space (space, tab, newline in IFS) runs together and is
dropped at the ends; any other IFS character ends a field, an empty one too, with adjacent IFS white space:
IFS=, makes `a,,b` three fields and `,a,` two (`` and `a`); IFS=' ,' makes ` a , b ,, c ` four. C#Bash
treated every IFS character as white space. Unquoted `$@`, `$*`, `${a[@]}`, `${a[*]}` are joined with IFS's
first character and then split (IFS=, makes `"a,b" "" c` four fields, `"a b" c` two); with IFS empty each
non-empty element is its own field; C#Bash joined with a space. Where `$*` / `${a[*]}` make one string
("$*", an assignment, `[[ ]]`, a here-string, `case`, `${*:2}`, `${a[*]:1}`, `${!a[*]}`, `${!p*}`) they join
with IFS's first character, nothing when IFS is empty, a space when unset; C#Bash always used a space.
`$@` there joins with a space in both.

**Fix:** `WordExpander.ExpandToFields` reads IFS as set-or-unset and splits by the POSIX rule (one state bit:
white space just ended a field, so a following `,` is the same delimiter); `AppendArray` joins with IFS's
first character and splits, or keeps elements apart when IFS is empty; `ShellEnvironment.StarSeparator`
joins `$*` everywhere it makes one string. The element-wise operator path (`${a[*]/x/y}`) still joins with
spaces internally to split back into elements (its documented approximation), then applies `*`'s joining.
Cost: IFS is still one lookup per word when it is non-empty [unmeasured estimate: within noise; compare3
runs before the next push].

**Verified:** new case `ifs_splitting` (64 lines, expected = GNU bash via WSL; rev 217 misses 95 diff lines).
Suite 123/123 with a console and `-Detached`; compat battery 226/237 against 225 (Git Bash reference).

**Implemented by:** rev 228. Files: `Bash/Evaluator/WordExpander.cs`, `Bash/Evaluator/ShellEnvironment.cs`,
`tests/cases/ifs_splitting.sh`, `tests/expected/ifs_splitting.out`, `tests/run-tests.ps1`.

## 2026-10-12 -- METHOD: repetitive tools are contracts; the benchmark builds what it measures, the `-n` corpus and the battery's reference are fixed
**Status:** Active. The architect, 2026-10-12: "yes, do that - repetitive tools here need to be contracts,
period." (Asked why running the benchmark had needed anything "recovered" from a transcript.)

**Context:** `compare3.sh` itself ran unmodified, but around it three things were improvised: (1) the
build under test for an undeployed rev was published by hand into a scratch directory (revs 200, 204, the
traps build, rev 229), against the benchmark's own rule ("never copy builds into scratch roots"); (2) the
`-n` corpus check lived only in a scratch directory and was re-run from a command found in the transcript;
(3) the battery, run without `-Reference`, took `CLAUDE_CODE_GIT_BASH_PATH`, which on this machine names
the deployed C#Bash, so it compared C#Bash with itself and could not fail.

**Decision:** a check run more than once lives in the repository, runs one way, and is invoked as its
header says.
- `tests/bench/compare3.sh --build` makes the build it measures: a dirty tree is refused, the tree is
  published with `tools/publish-aot.cmd` (as shipped) to `tests/bench/aot` (ignored), the stamp must carry
  the tree's node, and each C#Bash row times the build and `dist/Bash.exe` in alternating single launches
  (best of 9 each; startup samples alternate, median of 5 each), adding `deployed` and `change` columns.
  Alternating answers the 2026-10-12 run, where Git Bash itself drifted 1-2% between two back-to-back
  runs, as much as the differences read. The default and `--cs` modes are unchanged.
- `tests/corpus/run-n.sh`: the corpus is WSL's `/usr/share/bash-completion/completions` (bash-completion
  1:2.11-5ubuntu1, 912 files), fetched each run (GPL files are not committed to this MIT repository);
  `manifest.txt` pins sha256 and GNU bash 5.1.16's verdict per file, and any drift stops the run;
  `baseline.txt` is a ratchet like the battery's. It runs in Git Bash, never in the candidate.
- `tests/compat/run-compat.ps1` runs each reference candidate and skips C#Bash (BASH_VERSION has
  "koliada") and anything that does not start; an explicit `-Reference` naming C#Bash is an error. It
  also finds the Git root from `mingw64\bin\git.exe` (two levels up was `mingw64`) and accepts the renamed
  `usr\bin\_bash.exe`.

**Alternatives considered:** keeping the scratch habits with better notes (rejected: a note is not a
contract, and this is the third time the method was rebuilt by hand); committing the corpus files
(rejected: third-party GPL files in an MIT repository mirrored to GitHub); configuring the battery's
reference as compare3 configures Git Bash (rejected for now: the battery is published and its search
keeps it portable; identifying the candidate by running it closes the hole).

**Verified:** `--build` refuses a dirty tree and `--build --cs` (exit 2). `run-n.sh`: 844 of 912 agree,
the same 68 disagreements as the scratch harness on the same bytes (identical hash multiset); a tampered
manifest line stops it as DRIFT (exit 2); a disagreeing file put in the baseline fails it as a
REGRESSION (exit 1). The battery without `-Reference` now picks `F:\Tools\PortableGit\usr\bin\_bash.exe`
(bash 4.4.23) and gives 226/237 against 225; `-Reference dist/Bash.exe` is refused (exit 2). The first
`--build` run is the next commit's measurement.

**Implemented by:** rev 231. Files: `tests/bench/compare3.sh`, `tests/corpus/run-n.sh`,
`tests/corpus/manifest.txt`, `tests/corpus/baseline.txt`, `tests/compat/run-compat.ps1`, `.hgignore`,
`.gitignore`, `PROJECT_CONTEXT.md`.

## 2026-10-12 -- VERIFIED: revs 218-231 cost nothing measurable (the first `compare3.sh --build`)
**Status:** Active. Push-gate evidence for revs 218-231.

**Result (`compare3.sh --build`, rev 231 AOT against deployed rev 217, alternating rounds; seconds):**
loop 0.159 / 0.163 (-2.5%), arith 0.248 / 0.245 (+1.2%), func 0.229 / 0.230 (-0.4%), loop_big 1.404 /
1.437 (-2.3%), coreutils 0.058 / 0.059, pipeline 0.253 / 0.251 (+0.8%), find 0.023 / 0.022 (1 ms on a
1 ms clock), startup 0.0163 / 0.0165. Outputs agree with Git Bash, WSL and the deployed build in every row.
The "+8% func" of the morning's two separate runs (rev 229 then rev 217) was drift between runs, not a
cost: alternated, the two builds are level. The table is in `tests/bench/results.md`.

## 2026-10-12 -- METHOD: deploy and push are contract tools too (`tools/deploy.sh`, `tools/publish.sh`)
**Status:** Active. The architect: "deploy, notify and push", the same day as "repetitive tools here need
to be contracts, period". Deploys and pushes had been done by hand from DECISIONS' notes six times; doing a
seventh by hand would break the rule just made.

**Decision:** `tools/deploy.sh` (from Git Bash) refuses a dirty tree or an already-deployed rev, publishes
with `tools/publish-aot.cmd` to `dist.staging` (never `dist/`, which the publish would empty), checks the
stamp, runs the suite on that exact binary with a console and `-Detached` and the battery (any failure or
regression stops it), renames the running `dist/Bash.exe` to `dist/Bash.exe.rev<old>` (Windows renames a
running image and cannot overwrite it), copies the build in, re-checks the stamp, and prints the revisions
now deployed. The live check, the notice (every live local session, behaviour changes first) and the
records stay Claude's, each the same way every time. `tools/publish.sh <message-file> <token-file>`
requires a clean tree, a message whose first line is `C#Bash at hg rev <tip>: …`, and a compare3 table of
the DEPLOYED build carrying `dist/Bash.exe`'s stamp (the push rule of 2026-10-10); it regenerates
`MERCURIAL_HISTORY.md` and `mercurial-history.hg` (verified by unbundling into a fresh repository),
stages exactly hg's tracked files (a blanket `git add -A` would have published `The Windows Shell
Tax.url`, which is not the project's), commits, pushes with the token as a command-line extraHeader
(masked), and verifies the header is not in git config and the remote main is HEAD. The doc review stays a
judgment, committed in hg before the script runs.

**Alternatives considered:** a checklist in PROJECT_CONTEXT (rejected: that is what existed, and steps
were still reconstructed each time); one combined deploy-and-push script (rejected: they happen on
different words from the architect, and a push needs a benchmark of what was deployed).

**Implemented by:** rev 233. Files: `tools/deploy.sh`, `tools/publish.sh`, `.hgignore`, `.gitignore`,
`PROJECT_CONTEXT.md`.

## 2026-10-12 -- DEPLOYED: rev 233 (revs 218-233), notice sent; the first deploy by `tools/deploy.sh`
**Status:** Active. The architect: "deploy, notify and push".

`tools/deploy.sh`, as written: rev 233 (`1.0.0+hg.2a0678c462c5 233`) published to `dist.staging`; suite
123/123 on that exact binary with a console and `-Detached`; battery 228/237 against 225, no regression
(run from Git Bash's environment, probes 55 and 196 pass as well as 172: environment-dependent, the same
reference `F:\Tools\PortableGit\usr\bin\_bash.exe`, bash 4.4.23; 226 from Claude Code's environment);
rev 217 renamed to `dist/Bash.exe.rev217`, rev 233 copied in, stamp re-checked. Live check in a Claude
Code Bash call: stamp rev 233, `sleep 1 && echo x &` returns at once, `IFS=` keeps `a b` whole, `case`
inside `$( )`, `kill -l`'s table, `dirs +0`. Notice to the six live sessions (daq-d1, adc-framework-d8,
msp430-a2, nupkg-28, errorlogs-42, koliada-net-6a), leading with the seven behaviour changes; daq-d1 told
its `compgen` workaround stands.

## 2026-10-12 -- PUBLISHED: GitHub main = git 02f95bc = hg rev 235; the first push by `tools/publish.sh`
**Status:** Active. The architect: "deploy, notify and push"; the push rule followed (compare3 on the
deployed rev 233, 2026-10-12 07:41: every row within noise of the README's previous table; the doc review
committed as rev 235: README table, traps, IFS, jobs, 123 cases, compgen not yet, the tools in Layout;
ARCHITECTURE field splitting, traps, jobs; IMPLEMENTATION CaseTracker, FindJobIn, RunWithJobGroups).

`tools/publish.sh` as written: MERCURIAL_HISTORY.md and the bundle (236 changesets) regenerated, the bundle
verified by unbundling; exactly hg's tracked files staged (`The Windows Shell Tax.url` listed and left
out); pushed 1ab0a25..02f95bc; no extraHeader left in git config; remote main = HEAD. Git warned it
stores `.hgignore` and `tests/expected/path_lookup.out` with LF (`* text=auto eol=lf`); checked that it
does not matter: with GitHub's LF-only copies of `path_lookup.out` and `fieldutils.out` (the two expected
files with CRLF lines) the suite passes 123/123, as the runner splits both sides on `\r?\n`.

## 2026-10-12 -- RULING: Git Bash is a reference only as far as Claude is concerned; GNU bash is the strategic reference
**Status:** Active. The architect, 2026-10-12: "I think I said earlier that git bash is a reference. Slight
correction - only as far as Claude is concerned. GNU Bash is prolly a better 'strategic' reference going
forward." Refines the 2026-10-03 ruling (GNU bash on Linux is the target; Git Bash is not a target).

**Meaning:** where C#Bash's behaviour is open, GNU bash (5.1, via WSL) decides. Git Bash's behaviour counts
only as evidence of what Claude expects on Windows (it is Claude Code's default shell there), which matters
for Windows-specific forms (paths, `.exe` names, Claude Code's wrapper) that GNU bash on Linux never sees.
The compat battery still uses Git Bash as its reference; moving it is proposed, not decided (anchor).

**Also greenlit the same message:** the command printer (AWAITING item 0, option (b), port bash's
print_cmd.c): "printer, yes". Its plan goes to the architect for ratification before building.

## 2026-10-12 -- DEFECT FIXED: case's `;&` and `;;&` were syntax errors; found: `select` and `coproc` do not exist
**Status:** Active. Found in printer P1 (the ratified plan): `CaseItem` had no terminator to print.

**Measured (GNU bash 5.1 via WSL):** `;&` runs the next branch's commands without testing it, `;;&` goes on
testing the branches after it; the status is the last command run's (0 when nothing matched). Outside
`case` both are a syntax error, `near unexpected token ';&'` (bash lexes them as tokens everywhere). C#Bash
rejected both inside `case` too.

**Fix:** the lexer makes `;&` and `;;&` tokens; `CaseItem.Terminator` (`Break`, `FallThrough`, `TestNext`)
records how a branch ends; `ExecCase` follows it, patterns still expanding lazily up to the first match;
the `$( )` case tracker counts all three as a branch's end.

**Found, not raised for action (measured):** `select` is a syntax error and `coproc` is "command not
found" in C#Bash; bash runs both. Neither has an AST node, so the printer leaves them out until one exists.

**Verified:** new case `case_fallthrough` (expected = GNU bash via WSL; rev 233 misses 22 diff lines). Suite
124/124 with a console and `-Detached`; battery 226/237 against 225; `-n` corpus 846/912 (was 844: the
`export` and `slackpkg` completions use the terminators), baseline advanced.

**Implemented by:** rev 238. Files: `Bash/Lexer/TokenType.cs`, `Bash/Lexer/Lexer.cs`, `Bash/Parser/Nodes.cs`,
`Bash/Parser/Parser.cs`, `Bash/Evaluator/Evaluator.cs`, `tests/cases/case_fallthrough.sh`,
`tests/expected/case_fallthrough.out`, `tests/run-tests.ps1`, `tests/corpus/baseline.txt`.

## 2026-10-12 -- PRINTER P1 (ratified plan): bash 5.1's command-printing rules, measured
**Status:** Active. The plan (P1-P5, anchor GREENLIT 2026-10-12) was ratified ("Yes"). P1's criterion: every
rule written here with a probe that shows it. The probes are `tests/cases/command_printing.sh` (53 functions;
expected = GNU bash 5.1 via WSL, 440 lines; C#Bash today: 561 diff lines, it prints the source as written).
Not yet in the suite: it joins when P2 passes it.

**Rules (bash 5.1 `declare -f`, `type`):**
1. A function: `NAME () ` + newline + `{ ` (trailing space), the body indented 4, `}`. At top level the
   `function` keyword is dropped; a definition nested in a body prints `function NAME () `. A subshell body
   (`g() ( … )`) is wrapped: `{` / `( … )` / `}`. The function's redirections follow `}`: `} 2> /dev/null`.
   `declare -f` lists functions in name order (byte order: f1, f10, f2); comments are dropped.
2. Lists: one command per line at the current indent, `;` after every command but a group's last; `&` ends a
   backgrounded command instead of `;`; `&&` / `||` / `|` stay on one line with single spaces (a pipeline or
   and-or list written across lines is joined); `|&` prints as `2>&1 |`.
3. `if C; then` / body (+4, last command `;`) / `else` / body / `fi`. `elif` prints as `else` + a nested
   `if … fi;` one level deeper.
4. `while C; do` and `until C; do` / body (+4, every command `;`, the last too) / `done`.
5. `for V in WORDS;` / `do` / body (+4, every command `;`) / `done`; `for V` (no `in`) prints `in "$@"`;
   `for ((A; B; C))` (heads joined by `; `) / `do` / … / `done`.
6. `case WORD in ` (trailing space) / each branch at +4: patterns joined by ` | ` then `)` (a leading `(` is
   dropped) / body at +8 (commands `;`-joined, the last bare) / the terminator `;;`, `;&` or `;;&` at +4 /
   `esac`. An empty body prints an empty line.
7. `{ ` / body (+4, last bare) / `}` + redirections; `( A;` / later commands at the CURRENT indent / `… )`
   (`( a; b; c ) > out` prints `( a;`, `    b;`, `    c ) > out`); `time { a; }` and `( a ) && { b; }`
   follow the same rules.
8. A simple command: assignments and words as written, one space apart (`$(…)`, backquotes, `$((…))`,
   `$[…]`, `${…}` and quotes kept verbatim); `$'…'` prints decoded in single quotes (`$'it\'s'` →
   `'it'\''s'`). Redirections: `> f`, `2> f`, `>> f`, `< f`, `3<> f`, `>| f`, `&> f` (a space before the
   target); a descriptor copy has no space and an explicit fd (`>&2` → `1>&2`, `<&3` → `0<&3`, `2>&1`,
   `3>&-`); `<<< w`.
9. A here-document: `<<DELIM` / `<<'Q'` / `<<-EOT` with no space, other redirections after it; the body
   and delimiter follow the command line verbatim (`<<-` bodies and delimiter with their tabs stripped);
   then an EMPTY line; the command takes no `;`. In a pipeline the next stage follows the body indented
   two spaces (`cat <<EOT |` / body / `EOT` / `  sort`).
10. `[[ … ]]` and `(( … ))` as written with single spaces between tokens.
11. `! P` and `time [-p] P` prefix the pipeline.
12. Not printed by C#Bash (no AST node): `select`, `coproc` (bash prints `coproc COPROC cmd` for an
    unnamed one).

## 2026-10-12 -- PRINTER P2: `CommandPrinter` prints functions as bash does (`declare -f`, `type`)
**Status:** Active. The ratified plan's P2; criterion: `command_printing` byte-identical to GNU bash 5.1.

**Built:** `Bash/Parser/CommandPrinter.cs` walks the AST with bash's layout (P1's rules: 4-space indents, `;`
placement, `elif` as a nested `if`, `for … in "$@"`, `case` branches and terminators, a subshell's later
commands at the current indent, `function` for a nested definition, a brace body's redirections after `}`);
here-document bodies wait for the next newline, as bash's print_deferred_heredocs, so `cat <<EOT | sort`
prints `cat <<EOT |`, the body, `  sort`. Simple commands, assignments, `(( ))` and `[[ ]]` come from
`CommandText.RenderParts` (their source text, as `$BASH_COMMAND` already rendered them), which now (1)
returns the line and the bodies apart, (2) stops at a top-level operator, so a command whose span runs to
its here-document's end does not take in the rest of its line (`[[ a && b ]]` excepted), (3) copies a
`$( )`, `<( )`, `>( )`, `( )` or backquoted part verbatim to its matching close (it re-joined tokens and
lost a here-document inside `$( )`), (4) gives a descriptor copy bash's explicit fd (`>&2` → `1>&2`,
`<&0` → `0<&0`), (5) writes a `'` inside a decoded `$'…'` as `'\''`. (4) and (5) are bash's
`$BASH_COMMAND` too: measured identical for `echo x >&2`, `read y <&0`, a here-document in `$( )`,
`$'it\x27s'`. Words keep their source span (`Word.RawSpan`, no allocation while parsing) for patterns,
`for` lists and compound redirections; the two array-assignment nodes now have spans and lines.

**Verified:** `command_printing` (53 functions, `type`, `eval "$(declare -f f)"` round trip) byte-identical
to GNU (rev 239's C#Bash: 561 diff lines); joined the suite. Suite 125/125 with a console and
`-Detached`; battery 226/237 against 225; `-n` corpus 846/912 (= baseline).

**Implemented by:** rev 240. Files: `Bash/Parser/CommandPrinter.cs` (new), `Bash/Parser/CommandText.cs`,
`Bash/Parser/Nodes.cs`, `Bash/Parser/Parser.cs`, `Bash/Evaluator/Builtins.Shell.cs`, `tests/run-tests.ps1`.

## 2026-10-12 -- PRINTER P3: `jobs` prints a job's command as bash does (closes "compound job text")
**Status:** Active. The ratified plan's P3; criteria: compound jobs as bash shows them; the `eval "$(declare -f
f)"` round trip (met in P2's case).

**Measured (GNU bash 5.1):** `jobs` uses the same printer in another mode: a brace group or a subshell stays
on one line (`{ sleep 5; echo hi; } > /dev/null`, `( sleep 5; true )`), while `for`, `if` and `case` take
their lines at indent 0, also inside a group (`{ if true; then` / `    sleep 5;` / `fi; }`); assignments and
redirections show (`x=1 sleep 5`, `sleep 5 1>&2 2> /dev/null`); a pipeline's `!` does not; `time` does.

**Built:** `CommandPrinter.Command` (job mode); `DescribeNode` uses it, and its old reconstruction (node
type names for compound commands, no assignments, no redirections) is gone. Per-stage texts for `%name`
matching come from it too.

**Verified:** new case `jobs_text` (14 job shapes, expected = GNU bash via WSL, identical on two runs; rev 233
misses 37 diff lines). Suite 126/126 with a console and `-Detached`; battery 226/237 against 225.

**Implemented by:** rev 241. Files: `Bash/Parser/CommandPrinter.cs`, `Bash/Evaluator/Evaluator.cs`,
`tests/cases/jobs_text.sh`, `tests/expected/jobs_text.out`, `tests/run-tests.ps1`.

## 2026-10-12 -- PRINTER P4: the corpus check; 845 of 912 files print as GNU, every other one has a cause
**Status:** Active. The ratified plan's P4; criterion: every corpus function C#Bash defines prints as GNU's
`declare -f`, or is listed with its cause.

**Tool:** `tests/corpus/run-n.sh --printer` (contract): sources each corpus file in `bash --norc` (GNU in WSL,
then C#Bash), compares the `declare -f` output byte for byte, ratchet `printer-baseline.txt`; both outputs
kept in `tests/corpus/printer/` (ignored). First run 445/912.

**Printer rules found in the corpus (each confirmed by the files it fixed):** `[[ ]]` is printed from its
tree: a bare word is `-n WORD` (`[[ $x ]]` → `[[ -n $x ]]`), parentheses where written (new `CondGroup` node;
the `=~` operand keeps its span); a line continuation (`\` + newline) is removed from every word and
substitution, except inside single quotes, with quoting starting afresh inside each `$( )`; `$'…'` is decoded
into single quotes everywhere outside double quotes, a substitution's text included, and a NUL ends it
(`IFS=$'\0'` → `IFS=''`); an array literal prints its elements one space apart (`a=( $(x) )` → `a=($(x))`);
an arithmetic `for`'s parts keep their written ends and an empty part is `1` (`for ((1; 1; 1))`); after a
here-document's body the next `;` is left out (bash's was_heredoc: `fi` bare).

**Result: 845/912 identical.** The 67 others: 62 files C#Bash cannot parse (the `-n` corpus's own list,
chiefly extglob patterns in `[[ ]]`, a recorded gap); `smartctl` (the same, `@(…)` in `[[ ]]`, partway);
`_su`, `_mount`, `_umount`, `route` define their functions only when `$OSTYPE` is Linux (C#Bash's is the
Windows one): a platform difference, not the printer's.

**Defects found and fixed on the way (measured against GNU bash 5.1):**
- `local p=($(cmd))`, `declare -a q=($(…))`, `export v=($(…))`: a syntax error (`CollectParenText` counted
  `(` but not the one inside `$(`, `<(`, `>(`), in every build. The `-n` corpus rises to 850/912 (gdb,
  openssl, rdesktop, sysbench), baseline advanced.
- `export v=(a b)` stored the text `(a b)`; `readonly r=(x y)` without `-a` set nothing: both assign arrays.
- a quoted value that looks like `(…)` became an array in `declare`/`local` (`local s="(x y)"`): bash makes
  an array only of the unquoted literal, or of any `(…)` value under `-a`/`-A` (measured: `declare -a
  x='(a b)'` is an array). The parser now marks the literal it folds (`Lexer.ArrayLiteralMark`, U+FDD1, never
  shown: `set -x` strips it); `ArrayLiteralValue` applies the rule.
- `${#x[@]}` of a set scalar was 0; it is 1 (unset: 0).

**Found, NOT fixed, raised to the architect:** arrays are not local. `ShellEnvironment` keeps indexed and
associative arrays in two shell-wide dictionaries, outside the frames that hold scalars, so `local a=(x)` or
`declare a=(x)` in a function creates or overwrites the GLOBAL array (measured: after `f() { local t=(a b);
}; f`, bash has no `t`, C#Bash has `t=(a b)`), in every build. A fix makes array storage follow frames:
a change to the variable model.

**Verified:** new case `declaration_arrays` (expected = GNU via WSL; rev 233 misses 28 diff lines);
`command_printing` and `jobs_text` still identical. Suite 127/127 with a console and `-Detached`; battery
226/237 against 225; `-n` corpus 850/912; printer corpus 845/912 (baseline recorded).

**Implemented by:** rev 242. Files: `Bash/Parser/CommandPrinter.cs`, `Bash/Parser/CommandText.cs`,
`Bash/Parser/Nodes.cs`, `Bash/Parser/Parser.cs`, `Bash/Lexer/Lexer.cs`, `Bash/Evaluator/Evaluator.cs`,
`Bash/Evaluator/Builtins.Shell.cs`, `Bash/Evaluator/WordExpander.cs`, `Bash/AstPrinter.cs`,
`tests/corpus/run-n.sh`, `tests/corpus/baseline.txt`, `tests/corpus/printer-baseline.txt`,
`tests/cases/declaration_arrays.sh`, `tests/expected/declaration_arrays.out`, `tests/run-tests.ps1`,
`.hgignore`, `.gitignore`.

## 2026-10-12 -- PRINTER P5: no regression; the printer plan is complete
**Status:** Active. The ratified plan's P5; criterion: suite, battery, `-n` corpus and `compare3 --build` show
no regression.

`compare3.sh --build`, rev 242 against the deployed rev 233, alternating rounds (seconds): loop 0.161 /
0.157 (+2.5%), arith 0.242 / 0.245, func 0.212 / 0.214, loop_big 1.387 / 1.390, coreutils 0.057 / 0.059,
pipeline 0.245 / 0.246, find 0.022 / 0.021 (1 ms on a 1 ms clock), startup 0.0158 / 0.0154 (0.4 ms, inside
the row's ±1.5 ms spread); outputs agree with Git Bash, WSL and the deployed build. Suite 127/127 both modes;
battery 226/237 against 225; `-n` corpus 850/912; printer corpus 845/912. P1-P5 done, revs 239-242.

## 2026-10-12 -- DEFECT FIXED (adc-framework-d8): `diff -u` ignored `--label`
**Status:** Active. Reported by adc-framework-d8 (a patch made with `--label` could not be applied with
`patch -p1`); verified, then fixed after the printer as promised to the reporter.

**Measured (GNU diff via WSL):** `--label L` replaces a header's file name and drops its timestamp; the first
names the old file, the second the new (`--- X1` / `+++ Y1`; one label: `--- only` / `+++ l2.txt<TAB>stamp`).
C#Bash's in-process `diff -u` accepted the option and printed the names and times. `-c` looked right only
because the in-process diff has no context format and hands it to Git's GNU diff.

**Fix:** `Opts.AllLong` keeps every value of a repeated long option (`_longs` kept the last); `diff` passes
the labels to the unified header.

**Verified:** new case `diff_label` (expected = GNU diff via WSL; rev 233 misses 20 diff lines). Suite 128/128
with a console and `-Detached`; battery 226/237 against 225.

**Implemented by:** rev 244. Files: `Bash/Evaluator/Opts.cs`, `Bash/Evaluator/Builtins.Find.cs`,
`tests/cases/diff_label.sh`, `tests/expected/diff_label.out`, `tests/run-tests.ps1`.

## 2026-10-12 -- VARIABLE SCOPE V1 (ratified plan): the measured contract, and C#Bash's baseline
**Status:** Active. The architect ratified the variable-scope plan V1-V4 ("ratified, go"; anchor item V).
V1's criterion: the case written, expected = GNU, C#Bash's failure recorded.

**The contract (GNU bash 5.1, `tests/cases/variable_scope.sh`, 59 lines):** an assignment to a name no scope
holds goes to the global scope, however it is made (`x=`, `read`, `for`, `printf -v`, `getopts`, `(( ))`,
`let`, `${x:=}`, `+=`, `arr[i]=`, `export`, `readonly`); `local` and `declare` (and `typeset`) in a function
hold the variable, scalar or array or associative, with its attributes, for that call only (`-i` evaluates,
`-r` and `-x` end with it; `declare -g` is global); a callee sees and changes its caller's locals (dynamic
scope); `local x` with no value hides an outer x until it is set, and an array subscript then makes it a local
array; `local a=("${a[@]}" new)` reads the outer `a` on the right; recursion keeps each call's arrays apart;
`unset` of a local in its own scope leaves it local and unset, while a callee unsetting its caller's local
uncovers the outer value; subshells and `$( )` get copies; `local` outside a function: "local: can only be
used in a function", status 1.

**Baseline, C#Bash rev 245 (and every build before it):** 20 lines, then the script aborts (`local -r` left
the variable readonly globally); 67 diff lines against GNU's 59.

## 2026-10-12 -- VARIABLE SCOPE V2+V3: one store per scope, bash's dynamic scoping for scalars, arrays and attributes
**Status:** Active. The ratified plan's V2 (model) and V3 (callers), built together; criteria: `variable_scope`
byte-identical to GNU; suite green both modes; battery, `-n` corpus, printer corpus without regression.

**Model (`ShellEnvironment`):** the scopes are a list of dictionaries, global first, from a name to one `Var`:
a scalar `Value`, or an `Indexed` or `Assoc` array, with its `Exported` / `Readonly` / `Integer` attributes;
declared with no value (`local x`) is a scalar with a null value. `Find` looks a name up innermost-out
(dynamic scope); `Target` is what an assignment changes: the innermost variable, else a NEW GLOBAL one (the
heart of the fix: a function's `x=1`, `read x`, `for x`, `printf -v x`, `getopts`, `(( ))`, `let`, `${x:=}`,
`x+=`, `a[i]=` now outlive the call). `DeclareLocal` / `SetLocal` work in the innermost scope (a readonly
outer one cannot be shadowed), `SetGlobal` in the global one. Scalar and array are one variable: `$a` is an
array's element 0; `a[1]=x` on a scalar makes it an array with the scalar as element 0; `a=x` on an array sets
element 0 (bash). Attributes belong to the variable, so a local `-r`, `-i`, `-x` ends with the call; a local
`-x` reaches children while its function runs (`GetExportedVars` takes each name's visible variable).
`Unset`: in the function scope that declared it the variable stays declared and unset (still hiding an outer
one); a callee unsetting its caller's local, or anything at top level, removes it, uncovering the next one
out (measured). FUNCNAME / BASH_SOURCE / BASH_LINENO keep their lazy refresh (on reading one of them, a
snapshot, a listing). Subshells, `$( )` and pipeline stages deep-copy the scopes (`CopyFrames`).

**Callers:** `local`, and `declare` / `typeset` inside a function unless `-g`, declare the name in the current
scope BEFORE its attributes and value; an array literal's words are expanded first (`PrepareArrayLiteral`),
so `local a=("${a[@]}" x)` reads the outer `a` (bash). `local -i` now evaluates (`Assign` honours the
variable's integer attribute; `SetLocal` used to bypass it). Found and fixed on the way: a subshell's fatal
error (`( r=2 ) 2>/dev/null`, r readonly) was printed after its redirects were gone; it is reported inside
them (bash is silent there), in every build.

**Verified:** `variable_scope` byte-identical to GNU (rev 246's C#Bash: 20 lines, then an abort; 67 diff
lines); joined the suite. Suite 129/129 with a console and `-Detached`; battery 226/237 against 225; `-n`
corpus 850/912; printer corpus 845/912. V4 (the benchmark) next.

**Implemented by:** rev 247. Files: `Bash/Evaluator/ShellEnvironment.cs`, `Bash/Evaluator/Builtins.Shell.cs`,
`Bash/Evaluator/Evaluator.cs`, `tests/run-tests.ps1`.

## 2026-10-12 -- VARIABLE SCOPE V4: the benchmark; one row is a real, small cost, brought to the architect
**Status:** AWAITING THE ARCHITECT (the ratified plan: "a row beyond noise is measured in-process and
brought to the architect before going on").

**`compare3.sh --build` (alternating rounds against deployed rev 233):** rev 247: coreutils +10.5% (6 ms),
the rest within noise. In-process diagnostic (coreutils.sh, rev 246 vs 247): allocation per `$( )` 87.0 vs
87.8 KB, time 70.6-71.8 vs 72.9-82.3 ms. A snapshot is restored once, so `RestoreSnapshot` now takes over
its copy instead of copying again (rev 248): in-process 68.9-70.2 ms against rev 246's 69.5-73.6, 78 KB per
substitution against 87. rev 249 run: loop +3.1%, arith -1.6%, func +4.2%, loop_big +1.0%, coreutils +5.3%,
pipeline -0.8%, find 0, startup +0.6%, outputs agree (the machine ran slower: Git Bash's startup 17.3 ms
against 16.4 earlier).

**In-process (`tools/bench.csx`, interleaved, rev 246 = old model):** loop 456-476 vs 456-462 ns/it, arith
739-770 vs 766-797: level. func (100k calls, each `local r=$((…))`): 1404-1410 ns/call against 1332-1415,
+48 B per call: a REAL ~5% (~70 ns) per call with a local, the `Var` object the correct model gives each
local. `SetLocal` now declares and assigns in one lookup (`local x=v` took two); that removed variance, not
the object.

**Options put to the architect:** accept the ~70 ns per `local` (Claude's recommendation: it buys bash's
scoping in every script with functions, the absolute cost is tens of nanoseconds per call); or store
scalars as structs inside the scope dictionaries (no allocation for a scalar local) [unmeasured estimate:
recovers most of it], at the price of a harder-to-read model.

## 2026-10-12 -- DECIDED: the variable-scope cost is accepted; a performance review will follow "shortly"
**Status:** Active. The architect, on V4's question (accept ~5% / ~70 ns per function call with a `local`, and
deploy revs 234-251): "Yes to both - note the creeping drop in performance will prompt a performance review
'shortly'!"

**Meaning:** the variable-scope plan is complete (V1-V4); the struct-storage alternative is not built now.
The architect expects to call a PERFORMANCE REVIEW soon, prompted by the accumulating small costs. Its record
so far, from `compare3.sh --build` (alternating rounds against the deployed build) and in-process
`tools/bench.csx`: rev 242 (printer) every row within noise; rev 249 (variable scope) loop +3.1%, func +4.2%,
coreutils +5.3% in the benchmark, func a real ~5% in-process (+48 B per call with a local), loop and arith
level. Do NOT start the review unasked: it is the architect's to call.

## 2026-10-12 -- DEPLOYED: rev 252 (revs 234-252), notice sent
**Status:** Active. The architect: "Yes to both" (the variable-scope cost accepted; deploy).

`tools/deploy.sh`: rev 252 (`1.0.0+hg.e7a1f2717689 252`); suite 129/129 on that exact binary with a console
and `-Detached`; battery 228/237 against 225 (Git Bash's environment, as at rev 233's deploy); rev 233 renamed
to `dist/Bash.exe.rev233`, rev 252 copied in. Live check in a Claude Code Bash call: stamp rev 252, a
function's new variable persists, a local array and a local `-r` end with the call, `local p=($(…))`,
`declare -f` in bash's layout, `case ;&`, `diff -u --label`. Notice to the six live sessions (daq-d1,
adc-framework-d8 told its `diff --label` report is in, msp430-a2 told its two reports are next, nupkg-28,
errorlogs-42, koliada-net-6a told `strings` is queued), leading with the variable-scope change.

## 2026-10-12 -- DEFECT FIXED (msp430-a2, data loss): an awk program containing `getline` lost every CR
**Status:** Active. Reported by msp430-a2 (a CRLF PROJECT_CONTEXT.md committed as LF), who also measured the
mechanism: Git's gawk strips CR in text mode (`gawk '{print}'` 0 CR, `gawk -v BINMODE=3` 3 CR).

**Cause:** the in-process awk refused main-input `getline` / `getline var` at PARSE time (even inside
`if (0)`), and the dispatcher re-ran the whole program with the PATH awk, Git's gawk.

**Fix:** (1) main-input `getline` and `getline var` in-process: the rule loop and a plain `getline` share one
reader (`NextMainLine`): operands in order, `v=x` operands assigned as reached, standard input when there is no
file, FILENAME and FNR following across files, NR/FNR counted, 0 at the end, in BEGIN too. (2) when awk still
hands a program to the PATH awk (`cmd | getline`, other gaps) or `BASH_COREUTILS=external` asks for it, the
arguments start `-v BINMODE=3`, so Git's gawk keeps CR as GNU awk on Linux does (a script's own `-v BINMODE`
comes later and wins).

**Verified:** new case `awk_getline` (15 lines: msp430-a2's three repros, getline across files, in BEGIN and
END, with assignment operands, and both fall-through paths; expected = GNU awk via WSL; rev 252 misses 18 diff
lines). The `awk` case's line asserting "unsupported: getline from the main input" now prints GNU's empty line.
Suite 130/130 with a console and `-Detached`; battery 226/237 against 225.

**Implemented by:** rev 254. Files: `Bash/Evaluator/Builtins.Awk.cs`, `Bash/Evaluator/Builtins.cs`,
`tests/cases/awk_getline.sh`, `tests/expected/awk_getline.out`, `tests/expected/awk.out`, `tests/run-tests.ps1`.

## 2026-10-12 -- DEFECT FIXED (msp430-a2): arguments to MSYS programs were re-parsed (braces, globs, quotes, backslashes)
**Status:** Active. Reported by msp430-a2 (`gawk '{print}'` arrived as `print`).

**Measured (Git's usr/bin printf.exe echoing its argv, C#Bash rev 254):** an MSYS program parses an UNQUOTED
word of its Windows command line like a shell: `'[%s]\n'` lost the backslash, `'{print}'` became `print`,
`'*.txt'` the matching files, and the `'` of `"it's"` opened a quote that swallowed every argument after it
(so `sed "s/it's/X/"` failed). `ChildLauncher.AppendArgument` quoted only on white space or `"`. Inside
double quotes the MSYS parser also takes `\\` as one backslash (`'\\srv\share'` arrived as `\srv\share`).

**Fix:** for an MSYS program (`ChildLauncher.IsMsysProgram`: it sits beside msys-2.0.dll) an argument goes
bare only if it is letters, digits and `-_./:=@%+,`; otherwise it is double-quoted with every backslash
doubled and `"` as `\"`. Native programs keep .NET's quoting exactly (cmd.exe and friends parse their raw
command line, so nothing changes for them).

**Verified:** new case `msys_arguments` (20 argument shapes through the PATH printf, plus gawk `'{print}'`,
sed with `'` and braces, find `-name '*.txt'`; expected = GNU bash via WSL with the native tools; rev 252
misses 32 diff lines). Suite 131/131 with a console and `-Detached`; battery 226/237 against 225.

**Implemented by:** rev 255. Files: `Bash/Evaluator/ChildLauncher.cs`, `tests/cases/msys_arguments.sh`,
`tests/expected/msys_arguments.out`, `tests/run-tests.ps1`.

## 2026-10-12 -- DEFECTS FIXED: sed added a missing final newline; `D` autoprinted (N;P;D doubled lines)
**Status:** Active. Found testing adc-framework-d8's rm report: its case pipes rm's prompts through sed, and
an unterminated prompt line came back with a newline.

**Measured (GNU sed 4.8 via WSL against rev 252):** (1) GNU writes a last input line that has no newline
without one, by every command that writes the pattern space (`p`, `P`, `n`, `N` at the end, `s///p`, autoprint,
`q`, `-i`, several files: only the last file's last line counts); C#Bash always added it. (2) `D` with a newline
in the pattern space restarts the cycle WITHOUT autoprinting, as `d` does, while text queued by `a` is still
flushed (`1{N;a X};D` prints `X` alone); C#Bash autoprinted the rest, so `N;P;D` printed every line after the
first twice, with or without a final newline. (2) was in every deployed build.

**Fix:** `SedRun` writes through two helpers: `Out` (any text) and `PS` (the pattern space: on the last line
of unterminated input it holds the newline back, written only if more output follows). The readers report
`missingNl`. `D` sets `deleted` as well as `restartNoRead`.

**Runner defect found on the way:** `tests/run-tests.ps1 -Detached` stripped the trailing newline with
`-replace "\r?\n$"`; .NET's `$` also matches before a final newline, so output ending in an empty line lost it
(PowerShell's own capture, which the runner imitates, keeps it: measured). Now `\z`. No earlier case ended in
an empty line, so nothing passed because of it (the whole suite passes with the fix).

**Verified:** new case `sed_final_newline` (24 lines; expected = GNU sed via WSL; rev 252 misses 36 diff lines).
Suite 133/133 with a console and `-Detached`, run on the tree holding rev 257's rm too; battery 226/237
against 225.

**Implemented by:** rev 256. Files: `Bash/Evaluator/Builtins.Search.cs`, `tests/cases/sed_final_newline.sh`,
`tests/expected/sed_final_newline.out`, `tests/run-tests.ps1`.

## 2026-10-12 -- DEFECTS FIXED (adc-framework-d8): rm refused a read-only file; -i/-I never asked; `rm -r` on a directory link emptied its target
**Status:** Active. Reported by adc-framework-d8 (measured on rev 252): `rm` without `-f` refuses a file with
the Windows ReadOnly attribute when standard input is not a terminal; GNU rm deletes it. Measuring rm against
GNU rm (via WSL) for the fix found two more defects, the second DATA LOSS in every deployed build.

**Measured (rev 252 against GNU rm):** (1) a write-protected file (`chmod a-w`, which C#Bash maps to ReadOnly)
was refused without `-f`; GNU removes it when standard input is not a terminal and asks when it is. (2) `-i`
and `-I` removed without asking. (3) `rm -r` on a directory link (an `ln -s` link or a junction) descended
into it and deleted the TARGET's contents; GNU removes the link only. Plain `rm` on one said "Is a directory".

**Fix (`Builtins.Files.cs` `Rm`):** `-f`, `-i`, `-I` override each other, the last given winning
(`--interactive` is `-i`). `-i` asks before each removal (with `-r`: descend, each entry, the directory, in
GNU's order and wording); `-I` asks once for more than three operands or `-r`. The question goes to standard
error and the answer is a line of the shell's standard input (`y`/`Y` yes, end of input no). A write-protected
file is asked about only when standard input is a terminal and neither `-f` nor `-i` is given, else removed.
A directory link is removed as a link, with or without `-r`, never descended into.

**Not covered by the case:** a terminal stdin (the case cannot have one; the write-protected question there
is GNU's documented behaviour, not measured here); `rm -r link/` with a trailing slash.

**Verified:** new case `rm_interactive` (18 cases incl. `echo y | rm -i`, `-ri`, `-rI`, `-di`, both link
cases; expected = GNU rm via WSL; rev 252 misses 77 diff lines; the junction itself spiked separately: rev 252
deleted the target file, the fix keeps it). Suite 133/133 with a console and `-Detached`; battery 226/237
against 225.

**Implemented by:** rev 257. Files: `Bash/Evaluator/Builtins.Files.cs`, `tests/cases/rm_interactive.sh`,
`tests/expected/rm_interactive.out`, `tests/run-tests.ps1`.

## 2026-10-13 -- DEPLOYED: rev 257 (revs 254-257), notice sent
**Status:** Active. The architect: "yes" (deploy now: two data-loss fixes waiting), then "deploy, notify
and publish". The first attempt was refused by Claude Code's auto-mode classifier ("Production Deploy");
it ran on the architect's explicit word.

`tools/deploy.sh`: rev 257 (`1.0.0+hg.099fd70fc65b 257`) over rev 252; suite 133/133 on that exact binary
with a console and `-Detached`; battery 228/237 against 225 (Git Bash's environment, as on 2026-10-12);
rev 252 renamed to `dist/Bash.exe.rev252`. Live check in a Claude Code Bash call: stamp rev 257, `rm -r` on a
directory link keeps the target, a read-only file removed, `N;P;D` once per line, a missing final newline
kept missing, awk `getline` keeps CR, `'{print}'` reaches Git's gawk intact. Notice to the six live sessions
(daq-d1, adc-framework-d8, msp430-a2, nupkg-28, errorlogs-42, and koliada-net-7c, new: koliada-net-6a has
gone), leading with the data-loss fix and with `rm -i` now keeping files when stdin is empty; `cmd //c`
named as known with the verified `cmd /c` workaround.

**Open (raised 2026-10-13, the architect asked "why does the deploy script use git bash?"):** no recorded
reason. The one recorded reason is the benchmark's (2026-09-13: its `#!/usr/bin/env bash` found C#Bash and
timed nothing; and it times Git Bash itself). `deploy.sh` and `publish.sh` copied the guard the day they
were written. Claude's position: drop the guard from those two and keep it for `compare3.sh`; falsifier: a
step in either that measurably behaves differently under C#Bash. AWAITING THE ARCHITECT.

## 2026-10-13 -- PUBLISHED: GitHub main = git 0b3547d = hg rev 259
**Status:** Active. The architect: "deploy, notify and publish". Before it, by the push rule: `compare3.sh`
on the DEPLOYED build (rev 257; every row within noise of the rev 233 table, all outputs agree with Git Bash
and WSL; results.md) and the doc review (rev 259). The review found two passages stale since before this
session's work and fixed them: ARCHITECTURE's "arguments are never rewritten" (false since rev 186's
Windows-program conversion) and IMPLEMENTATION's `FunctionDef.Source` for `declare -f` (gone since rev 240's
printer); section 11 rewritten for the variable model of revs 245-250. `tools/publish.sh`: MERCURIAL_HISTORY.md
and the bundle (260 changesets, verified), `The Windows Shell Tax.url` listed and not added, pushed
02f95bc..0b3547d, no header left in git config, remote main = HEAD, no credential in the log.

## 2026-10-14 -- DEFECT FIXED: `cmd //c` started an interactive cmd (Git Bash's `//` switch escape)
**Status:** Active. Found 2026-10-12 (a spike's `cmd //c "mklink /J …"` hung a Claude Code call to its timeout);
the architect: "2/ yes" (fix it before `strings`), then "go". Extends DECISIONS 2026-10-10 "a Windows program's
MSYS-form path arguments are converted", whose frame it keeps: the start of an argument only, Windows programs
only, `MSYS_NO_PATHCONV` / `MSYS2_ARG_CONV_EXCL` honoured.

**Measured (Git Bash 4.4's MSYS runtime, a native program printing its argv, 63 arguments in four probes):** an argument starting
`//` loses one leading `/` and every `\` becomes `/` (`//c` → `/c`, `//a\b\c` → `/a/b/c`, `///x` → `//x`,
`//` → `/`); exactly two slashes with another `/` later is UNC and keeps both (`//host/share/x`, `//c/x`;
`//a\b/c` → `//a/b/c`); any of `* ? [ ] " '` or a backquote leaves it alone; every other character converts
(space, tab, newline, `\`, `!#$%&()+,.:;<=>@^_{|}~`, non-ASCII). C#Bash passed every such argument as typed.

**Decision:** `ShellEnvironment.DoubleSlash`, reached from `NativeArgument`, exactly as measured. Kept different
from Git Bash on purpose (DECISIONS 2026-10-10): a bare `/c` (Git Bash makes it `C:/`, which is why its users
write `cmd //c`; C#Bash keeps `cmd /c` working too) and a `//` inside an argument (`-o//x`, Git Bash `-o/x`).
Consequence, stated to the architect before building: `//host\share` becomes `/host/share`, as in Git Bash;
the forward-slash UNC form is untouched.

**Verified:** the four probe scripts (scratchpad slash*.sh) identical to Git Bash except bare `/c`, `-o//x` and
`//*` (the glob, not this conversion: Git Bash lists network hosts, C#Bash the current drive's root; the known
UNC-glob limitation). `native_args` +7 checks (`cmd //c echo hi`, the shapes, UNC, alone, both switches; deployed
rev 257 fails 5). Suite 133/133 with a console and `-Detached`; battery 226/237 against 225.

**Implemented by:** rev 261. Files: `Bash/Evaluator/ShellEnvironment.cs`, `tests/cases/native_args.sh`,
`tests/expected/native_args.out`.

## 2026-10-14 -- METHOD: `tools/deploy.sh` and `tools/publish.sh` run under any bash (the Git Bash guard dropped)
**Status:** Active. Resolves the open item in "DEPLOYED: rev 257" (the architect had asked "why does the deploy
script use git bash?"). The architect: "drop it, go". Amends the 2026-10-12 METHOD entry's "(from Git Bash)".

**Context:** both tools refused C#Bash with a guard that gave no reason. The only recorded reason belongs to
`compare3.sh` (2026-09-13: its `#!/usr/bin/env bash` found C#Bash and timed nothing; it also times Git Bash
itself), and both tools copied its guard the day they were written. Claude's falsifier for dropping it: a step
that measurably behaves differently under C#Bash.

**Measured (2026-10-14, every command form of both tools, read-only, under Git Bash, the deployed C#Bash rev 257
and rev 261):** identical results for `hg status`/`id`, `cygpath -w`, `cmd.exe /c` with `MSYS2_ARG_CONV_EXCL='*'`,
`powershell.exe -File` with the suite's arguments, `[[ ]]` patterns, here-strings, the stamp-vs-results.md check,
the MERCURIAL_HISTORY.md table (same md5 over 262 rows), the staging list (382 files, same md5), `base64`, the
token mask, and renaming a running image. One cosmetic difference seen: `sed`'s error column ("char 0", GNU
"char 12"), not raised.

**Decision:** the guard is removed from both; their headers say any bash runs them and why. `compare3.sh` keeps
its guard (its reason is measured). Polarity: under C#Bash the old `deploy.sh` stops at the guard; the new one
reaches its own clean-tree check, and `publish.sh` its usage check.

**Also decided (the architect, "1/ next"):** rev 261 (`cmd //c`) goes out with the next deploy, not on its own.

**Implemented by:** rev 262. Files: `tools/deploy.sh`, `tools/publish.sh`.

## 2026-10-14 -- BUILT: `strings` as GNU strings 2.38 (scope ratified)
**Status:** Active. Queued 2026-10-12 when a second session needed it (koliada-net hit it inspecting a .NET exe,
2026-10-10). Scope proposed against GNU strings 2.38 (Ubuntu, via WSL) and RATIFIED by the architect 2026-10-14
("ratified"), with its three-step plan. Until then `strings` was not a command here at all: Git for Windows ships
no binutils.

**Behaviour (GNU's, measured):** files or standard input (`{standard input}` for `-f`); the whole file; runs of at
least 4 printable characters (ASCII 32-126 and tab), each followed by a newline or the `-s` separator. `-n` /
`--bytes` read as C's strtoul (`010` is 8, `0x5` is 5, trailing text "invalid integer argument", 0 "invalid
minimum string length 0", a negative wraps to a huge minimum: nothing printed); `-N` the digits alone; `-t o|d|x`
and `-o`, the last winning, offsets 7 wide; `-f`; `-w` (all white space, CR and newlines kept inside a run);
`-e s|S|b|l|B|L` (8-bit, 16- and 32-bit big/little-endian; a character above 255 never printable). After a
character that is not printable the scan resumes ONE BYTE past its start (GNU's unget_part_char), so odd-aligned
UTF-16 is found: measured, against Claude's recollection of the C, which said otherwise. `-` is GNU's alias for
`-a`, not standard input (`strings -` alone is a usage error). A missing file or a directory reports, carries on,
exits 1, with standard output flushed first.

**C#Bash's own:** the default stays `-e s` (ASCII only) as GNU, though Windows binaries keep most text in UTF-16:
the ruling is bash as Claude expects it, and a C#Bash-only default would break it. `--version` never claims GNU
(awk's precedent). `--help` / a usage error print GNU's usage text with its last line C#Bash's (GNU's lists BFD
targets). An option GNU lacks (`-q`) follows DECISIONS 2026-09-04 #2. `-d`, `-T`, `-U` / `--unicode`, `@file`:
a PATH `strings` takes the command when one exists (2026-09-04 #2), else the not-available notice and link
(2026-10-11), exit 2.

**Verified:** `strings_gnu` (197 lines: synthetic bytes, every option, both 16- and 32-bit orders, odd alignment,
stdin, errors; expected = GNU via WSL; identical; rev 257 fails it, `command not found`); `strings_local`
(authored: version, help, usage errors, the policy, the four notices with their links). On the real 6.9 MB
`dist/Bash.exe`, `strings`, `-e l`, `-t x -n 8` and `-e S` give GNU's line counts and output md5 exactly
(90449 lines for plain `strings`). One run, managed Release build including ~70 ms startup: 192 ms (`-e l` 185 ms).
Suite 135/135 with a console and `-Detached`; battery 226/237 against 225.

**Implemented by:** rev 264. Files: `Bash/Evaluator/Builtins.Text.cs` (`Strings`, `StringsScan`, `StrToUl`,
`StringsOutput`), `Bash/Evaluator/Builtins.cs`, `tests/cases/strings_gnu.sh`, `tests/expected/strings_gnu.out`,
`tests/cases/strings_local.sh`, `tests/expected/strings_local.out`, `tests/run-tests.ps1`, `README.md`,
`IMPLEMENTATION.md`.

## 2026-10-15 -- BUILT: `compgen` (describing THIS shell); `complete` / `compopt` say not available; PATH scan deferred
**Status:** Active. Reported by daq-d1 2026-10-12 (`compgen` was not a builtin; it runs on a workaround). Scope
GREENLIT 2026-10-12 ("Yes": generating actions + -G -W -X -P -S + word filter + status; complete/compopt/-F/-C/
-o/-u/-g/hostname/service/binding the not-available notice; `-c` names as Git Bash). Built on "next; go".

**Ruling on the fork found while building (the architect, 2026-10-15):** "C#Bash has no formal bin dir. Whereas
some of the commands may or may not exist externally, C#Bash is really _only_ about its builtins. It is probably
worth building a list of externally available linux commands (from the path), but I would defer that as a
possible enhancement." So compgen describes THIS shell: `-b` / `-A builtin` / `enabled` list every C#Bash
builtin, the in-process tools included (as `type -t` and the interactive completion already did); `-c` lists
aliases, keywords, functions and builtins, and scans no PATH; `-A helptopic` gets the notice (no `help` here).
The fork as put: bash 5.1's 61 builtins minus the 10 C#Bash lacks, or all of C#Bash's; Git Bash's PATH rule
(`.exe`/`.com`/`#!`, measured) or what C#Bash runs (PATHEXT incl. `.cmd`/`.bat`); help topics or the notice.

**DEFERRED (the architect's word: "a possible enhancement"):** listing the externally available commands on PATH
in `compgen -c`. Measured for it: Git Bash lists PATH files by on-disk name (`zzt1.exe`, `zzt9.sh`), duplicates
across PATH entries kept, only `.exe`/`.com` and `#!` scripts (not `.cmd`/`.bat`). Revisit: on the architect's
word, or when a session needs `compgen -c` to find an external program.

**Behaviour, measured against GNU bash 5.1:** the actions in bash's order (pcomplete.c's CA_ bits), then `-G`,
then `-W`, no duplicates removed; each keeps the names starting with the word; `-W` split at IFS outside quotes,
each piece expanded (braces, tilde, parameters, command substitution, arithmetic, word splitting) but never
globbed; `-X` with `&` as the word and `!` to keep; `-P`/`-S` last; `-f`/`-d` complete the word as a path, dot
files included, directory order (unsorted in bash too); `-G` ignores the word; names sorted for aliases,
functions and variables, bash's own order for keywords, signals (EXIT, 64 Linux names with SIGJUNK(32)/(33),
DEBUG, ERR, RETURN), set -o and shopt names (this shell's lists); jobs newest first by first word; a variable
with no value or an empty array is not listed; status 1 for no output, 0 when nothing was asked, 2 (with bash's
messages and usage line) for a bad option, a missing argument or a bad action name.

**Verified:** `compgen` (246 lines, GNU-expected; rev 257 misses 306 diff lines) and `compgen_local` (authored:
the builtin list, `-c` order and no PATH scan, the twelve notices, complete/compopt). Suite 137/137 with a
console and `-Detached`; battery 226/237 against 225.

**Implemented by:** rev 265. Files: `Bash/Evaluator/Builtins.Shell.cs` (`Compgen` and helpers,
`CompletionNotAvailable`), `Bash/Evaluator/Builtins.cs`, `Bash/Evaluator/ShellEnvironment.cs`
(`VariablesWithValues`), `Bash/Evaluator/Evaluator.cs` (`JobTable`), `tests/cases/compgen.sh`,
`tests/expected/compgen.out`, `tests/cases/compgen_local.sh`, `tests/expected/compgen_local.out`,
`tests/run-tests.ps1`, `README.md`, `IMPLEMENTATION.md`.

## 2026-10-15 -- DEFECT TRACED AND FIXED: git's credential prompt hung under Claude Code (`/dev/tty` in a windowless console)
**Status:** Active. The open item "Inferred, not traced (2026-09-15): git's credential prompt runs through C#Bash
and fails on /dev/tty", taken on the architect's "go next" (the last of the open-defects run).

**Traced (2026-10-15, git 2.28.0.windows.1, `GIT_TRACE`):** with no helper answering (credential.helper=helper-
selector is set in PortableGit's etc/gitconfig; the trace disabled it to keep a GUI off the desktop) and no
askpass set, Git for Windows prompts by running `bash -c 'cat >/dev/tty && read -r line </dev/tty && echo
"$line"'`, and `bash` on PATH is C#Bash (dist/Bash.exe). Claude Code gives its shell a console with NO WINDOW
(CREATE_NO_WINDOW; GetConsoleWindow() NULL there and in git's child, measured), C#Bash opened it as /dev/tty, and
the read waited for typing that cannot come: the call hung to its timeout, silently. The inference was half
right: it does run through C#Bash, and it hangs rather than fails.

**References, measured in the same place:** Git Bash's /dev/tty there is "No such device or address" at once;
GNU bash with no controlling terminal (`setsid -w`) the same. git with no `bash` at all fails at once ("could
not read Username"), so git's own fallback does not hang: C#Bash's read was the whole hang. Under Git Bash's
login profile `etc/profile.d/env.sh` exports `SSH_ASKPASS=git-gui--askpass`, so there git shows a GUI password
dialog instead (seen when the trace ran it: it waited 20 s for an answer on the desktop).

**Fix:** `ShellFile.OpenTty` (every /dev/tty open: a program's redirect, an in-process redirect, `cat`/`read`):
the console only when it has a window (`GetConsoleWindow() != NULL`), else bash's "No such device or address".
Measured with a throwaway ConPTY probe: Windows Terminal's ConPTY and a classic console (even hidden) have a
window handle, so interactive /dev/tty prompts keep working. Refines DECISIONS' 2026-10 device-file rule ("with
none [console], it is 'No such device or address'") to "with no window".

**Verified:** new case `dev_tty_windowless` (authored, GNU's messages): via the new fixture
`tests/fixtures/console-kind.ps1` (a windowless console, or one with a hidden window, the same in both suite
modes, a hang killed at 15 s), write/read/cat on /dev/tty fail at once and git's prompt fails ("could not read
Username", 128) in about a second; with a window /dev/tty still opens. Rev 257 writes, then hangs (TIMEOUT).
Suite 138/138 with a console and `-Detached`; battery 226/237 against 225.

**Open (raised, AWAITING THE ARCHITECT, not built):** should C#Bash export `SSH_ASKPASS=git-gui--askpass` when
unset, as Git Bash's profile does, so git asks with a dialog instead of failing? Claude's position: no. GNU
bash (the strategic reference) fails fast; a dialog popping up from Claude's commands is surprising, and git's
own credential helper (helper-selector / GCM) already offers the GUI route before any prompt. Falsifier: sessions
that need interactive credentials and have no helper.

**Implemented by:** rev 266. Files: `Bash/Evaluator/ShellFile.cs`, `Bash/Evaluator/Evaluator.cs`,
`tests/fixtures/console-kind.ps1`, `tests/cases/dev_tty_windowless.sh`, `tests/expected/dev_tty_windowless.out`,
`tests/run-tests.ps1`, `README.md`, `IMPLEMENTATION.md`.

## 2026-10-16 -- DEPLOYED: rev 266 (revs 261-266), notice sent; the first deploy run by C#Bash itself
**Status:** Active. The architect: "1/ deploy, notify and publish". `bash tools/deploy.sh` from a Claude Code
Bash call, i.e. under the deployed C#Bash rev 257 (the guard was dropped in rev 262): rev 266
(`1.0.0+hg.a160167e4eb9 266`) over rev 257; suite 138/138 on that exact binary with a console and `-Detached`;
battery 226/237 against 225; rev 257 renamed to `dist/Bash.exe.rev257` while it was running the script, rev 266
copied in, stamp re-checked. Live check in a Claude Code Bash call: stamp rev 266, `cmd //c echo hi` prints
`hi`, `strings` and `strings -e l`, `compgen -W` and `-b`, `read </dev/tty` fails at once, git's credential
prompt fails in 0 s (128). Notice to the eight live sessions (daq-fe, the successor of daq-d1, told its compgen
report is in; koliada-net-7c told `strings` is in; gps-time-2c and guy-b3, new, told what their shell is),
leading with the git hang.

## 2026-10-16 -- DECIDED: C#Bash does not export SSH_ASKPASS (git fails fast without a terminal)
**Status:** Active. The architect: "2/ No", on the question raised with the /dev/tty fix (rev 266): should
C#Bash export `SSH_ASKPASS=git-gui--askpass` when unset, as Git Bash's `etc/profile.d/env.sh` does, so git asks
for credentials with a dialog? No: without a terminal git fails at once, as under GNU bash; git's credential
helper remains the GUI route. Revisit only on the architect's word.

## 2026-10-16 -- PUBLISHED: GitHub main = git 94a3ed2 = hg rev 268; the first publish run by C#Bash itself
**Status:** Active. The architect: "1/ deploy, notify and publish". Before it, by the push rule: `compare3.sh` on
the DEPLOYED build (rev 266, from Git Bash, as its guard requires; all outputs agree with Git Bash and WSL; C#Bash's
rows within noise of the rev 257 table; startup 18.1 ms against 16.6 ms, with Git Bash and WSL slower in the same
run, so the machine, not the build) and the doc review (rev 268: ARCHITECTURE's path model gains the `//` rule and
its redirect model `/dev/tty` with a window only; README's table). `bash tools/publish.sh` under C#Bash:
MERCURIAL_HISTORY.md and the bundle (269 changesets, verified), `The Windows Shell Tax.url` listed and not added,
pushed 0b3547d..94a3ed2 (30 files), no header left in git config, remote main = HEAD, no credential in the log.

## 2026-10-16 -- RATIFIED: the performance review plan (R1-R4), with a timing switch and a "Claude call" contract tool
**Status:** Active. The architect called the review flagged 2026-10-12 ("Lets take the opportunity to do a
performance review to see where we can optimize performance?"), then on the plan: "ratified, two additions sound
good".

**Context:** `compare3.sh` (unchanged, still THE benchmark) times long scripts and `bash -c 'exit 0'`; nothing
measures what a Claude Code call costs (the wrapper, the live snapshot, a command, the cwd write-back). The
2026-09-13 finding that 38% of a call was the snapshot was measured on a 58 KB snapshot; today's are ~5 KB.

**Plan (each phase's criterion written before it starts):**
- R1 anatomy of a real call: the exact wrapper with a snapshot and typical commands, split into named segments
  (process start, environment/PATH, snapshot, wrapper builtins, the command, exit). Done when the segments sum to
  within 10% of the measured whole-process time.
- R2 throughput hot spots: `tools/bench.csx` / `tools/attrib.csx` (as their headers say) on the current build. Done
  when each benchmark row has time and allocation per iteration and its top three contributors in %.
- R3 parse speed: the 2026-09-13 per-byte / per-token figures re-measured, plus the parse share of a real call.
- R4 report: ranked table (measured cost, predicted saving measured or marked, effort, risk, main or steroids)
  and a recommendation. No optimisation code before the architect picks.

**The two additions (approved):** an opt-in timing switch in the shell (off by default; its cost when off to be
measured, not assumed) and a contract tool for the Claude-call measurement beside compare3.sh (a method addition,
hence the architect's). Do NOT derive: any change to compare3.sh itself; any architectural (steroids) work.

## 2026-10-16 -- BUILT (review R1 instruments): the timing switch `BASH_TIMING_LOG` and `tests/bench/claudecall.sh`
**Status:** Active. The two additions the architect approved with the review plan ("two additions sound good").

**The switch (`Bash/Evaluator/Timing.cs`):** with BASH_TIMING_LOG naming a file, the shell keeps marks in memory
and appends them to that file when the process exits: ms since the process was created (the OS creation time),
then the event: main, console, evaluator, ready, done, exit, and the regions `source+ file`/`source-`,
`eval+`/`eval-`, `ext+ name`/`ext- name`. Off: one environment read in Main and a false bool tested at each mark;
no closures on hot paths (a lambda capturing `args` in the dispatcher would allocate on every call), string work
only when on. Its cost when off is for compare3 --build to show (startup row).

**The tool (`tests/bench/claudecall.sh`, a contract tool):** the exact Claude Code call, re-read from this
session's own command line (2026-10-16: the wrapper matches the 2026-09-03 record except that `eval '…'` no longer
gets `< /dev/null`); the snapshot fixture `tests/bench/claudecall/snapshot.sh` (a live one, user name scrubbed,
PATH filled at run time in each shell's form); six commands (`true`, `ls`, `cat | head`, `grep -rn`,
`git status --short`, `cd && pwd`) plus `bare` (`-c 'exit 0'`); launched by tools/benchtimer; median of 5 samples
of 20 launches; the build under test, the deployed build (with --build) and Git Bash in alternating rounds; then
the anatomy from 20 launches with the switch on, judged against R1's criterion. Results to
`tests/bench/claudecall-results.md`. compare3.sh is untouched.

**Implemented by:** rev 271. Files: `Bash/Evaluator/Timing.cs`, `Bash/Program.cs`, `Bash/Evaluator/Builtins.cs`,
`Bash/Evaluator/Builtins.Shell.cs`, `Bash/Evaluator/Evaluator.cs`, `tests/bench/claudecall.sh`,
`tests/bench/claudecall/snapshot.sh`, `README.md`, `IMPLEMENTATION.md`.

## 2026-10-16 -- REVIEW R1 RESULT: where a Claude Code call's ~31 ms goes (the snapshot's failed `hash rg` is half of it)
**Status:** Active (a finding; no code changed by it). Phase R1 of the ratified review.

**Measured (`tests/bench/claudecall.sh --build`, rev 273, AOT; the anatomy with the timing switch; diagnostics
labelled as such, not the benchmark):** a trivial call (`true`, `ls`, `cat | head`, `cd && pwd`) is ~31 ms; `bare`
(`-c 'exit 0'`) 15 ms; `grep -rn` adds ~7 ms, `git status` ~18 ms (16 inside git.exe). The timing switch costs
nothing measurable when off (rev 273 vs deployed rev 266: -0.5% to +1.8%, noise). One `ls` call, ms:
- runtime start (process creation to Main) 7.1; console 0.7; evaluator (environment, PATH) 1.6; options 0.2;
  parsing the call 0.2;
- the snapshot (`source`, 110 lines) 15.2, of which the `rg` block is 14.3 (cumulative prefixes): its
  `hash rg` misses (rg is not on PATH), searching 51 PATH entries x 12 names (bare + 11 PATHEXT) = ~600 probes.
  Measured as whole launches: failed `hash rg` +15.4 ms (`type -P rg` +17.1), a found `hash git` +0, the same
  miss with PATH = System32 +0. Two entries on drive X: (`X:\CC2541 …\bin`, `…\bin\targets\CC8051`) cost 8.8 ms
  of it (the miss without them: +6.0 ms); the other 49 ~10 us per probe. The other 109 lines: ~1 ms;
- the command, wrapper builtins, the cwd write-back 0.8; process exit after the last mark 6.9.
**The criterion** (segments within 10%) failed on the first reading: 21% fell after the exit mark. Split with a
minimal Native AOT program built and measured the same way: ANY AOT process here costs ~11 ms to start and end
(creation to Main 5.8-7.0, after its mark 2-3.5); C#Bash `bare` 15.8-16.2 (7.4-7.7 and 5-6). So: the platform floor
~11 ms; C#Bash's own start +~1 ms and exit +~3 ms (it loads 5 DLLs the minimal program does not: icu, ws2_32,
iphlpapi, secur32, sspicli; the rest, user32/gdi32/COM included, the runtime loads anyway). With that split the
call is accounted for; the C#Bash part of exit (~3 ms) is attributed by subtraction, not decomposed.

**Found on the way (recorded, not fixed):** (1) C#Bash does not expand PS4 (`PS4='+[$x] '` traces `+[$x]`, GNU
`+[val]`). (2) `$EPOCHREALTIME` has millisecond steps (GNU: microseconds). (3) Claude Code's snapshot generator
writes `set -o monitor` and `set -o onecmd` (its `set -o | grep on` matches the option names; all three shells
print them off); C#Bash ignores them. (4) Git Bash 4.4's `git status` hangs when this C#Bash-made snapshot is
sourced with a second wrapper part (bisected; not C#Bash; claudecall.sh is C#Bash-only since rev 273).

## 2026-10-16 -- FINDING (review R1 follow-up): ws2_32, iphlpapi and secur32 load at process creation because Native AOT binds them directly; icu.dll is a runtime load
**Status:** Active (a finding; no code changed). The architect asked why C#Bash loads these DLLs, whether they could
load on demand, and why the AOT build does not include them.

**Verified (dumpbin 14.44 on deployed rev 266; ILCompiler 8.0.25's targets):**
- `dumpbin -dependents dist/Bash.exe`: ADVAPI32, bcrypt, IPHLPAPI, KERNEL32, ole32, Secur32, USER32, WS2_32 and
  seven api-ms-win-crt. These are static imports: the Windows loader maps them before Main, used or not.
- Imported: WS2_32 GetAddrInfoW, GetNameInfoW, WSASocketW, FreeAddrInfoW; IPHLPAPI if_nametoindex; Secur32
  GetUserNameExW, which secur32.dll forwards to SSPICLI (hence sspicli).
- Why static: `Microsoft.NETCore.Native.Windows.targets` adds `WindowsAPIs.txt` ("ubiquitous Windows APIs that are
  safe to emit direct calls for") as a DirectPInvokeList, and all six functions are in it. A P/Invoke not on the
  list is bound lazily, at its first call.
- The only System.Net use in the source is `hostname -I` / `-f` (Builtins.Sys.cs:182, 189): ws2_32 and iphlpapi
  load on every start for two rarely used options.
- Why not "in the AOT": they are Windows system DLLs, the OS's API surface, with no static form to link; AOT
  decides only WHEN they load. icu.dll is not an import at all: .NET loads Windows' own ICU (System32) at the first
  culture-sensitive operation.

**Not checked:** which call brings GetUserNameExW (Environment.UserName and WindowsIdentity.GetCurrent are the
candidates); which startup operation loads icu.dll; whether these loads ARE the ~1 ms start / ~3 ms exit R1
attributed to C#Bash by subtraction (a correlation, not a measurement).

**Levers for R4 (not built; the architect picks):** lazy binding for those entries (a build setting; the MSBuild
hook is untested) or no System.Net in the binary; for ICU, find and remove the startup trigger, or
`InvariantGlobalization` / `UseNls` (behaviour changes: collation, and the ICU `StartsWith` gotchas go away).

## 2026-10-16 -- DECIDED (the architect, "yes, lazy load"): ws2_32, iphlpapi and secur32 bound lazily in the AOT build
**Status:** Active. Follows the FINDING entry above; the architect picked it ahead of R2.

**Decision:** `Bash/Bash.csproj` gains a target that runs after the ILCompiler's `SetupOSSpecificProps` and swaps
the SDK's `WindowsAPIs.txt` direct-call list for a copy written to obj/ without the `ws2_32!`, `iphlpapi!` and
`secur32!` lines (462 of 2368). The P/Invokes still link; they resolve at their first call. If the SDK's list is not
found (a renamed file or target in a later SDK) the build FAILS, so the setting cannot silently lapse.

**Alternatives:** no System.Net in `hostname -I` / `-f` (a code change, and secur32 stays); a committed filtered
copy of the list (drifts from the SDK it came from); ILC's `--directpinvoke` has no negative form.

**Criteria, written before building:** (1) `dumpbin -dependents` of the AOT build no longer lists the three (rev 266
does: the polarity check); (2) the suite green on the AOT binary in both modes, and `hostname -I`, `hostname -f`,
`whoami` still answer; (3) the saving measured with `compare3.sh --build` (startup row) and `claudecall.sh --build`
(`bare`); within noise, it is reported as such and keeping it is the architect's call.

**Run conditions (stated, not hidden):** this session's PATH is one entry (`dist`, the architect's choice), so the
build and the benchmarks run with the PATH of Claude's 2026-10-15 snapshot (50 entries) given to the tools only.

## 2026-10-16 -- RESULT: lazy binding (rev 277) saves ~0.3 ms at start and nothing at exit; C#Bash's extra exit cost is not these DLLs
**Status:** Active (a measurement). Criterion 3 of the entry above.

**Measured (2026-10-16, the 2026-10-15 snapshot's PATH given to the tools):**
- `compare3.sh --build` (rev 277 vs deployed rev 266): startup 18.1 ms vs 18.5 (-2.2%); Git Bash 18.8, WSL 135.7.
  Also loop +6.3%, loop_big +3.8%: NOT attributable to rev 277 by this comparison (revs 267-276 differ too).
- `claudecall.sh --build` (rev 278 = 277 + results; vs 266): bare 15.0 vs 14.6 (+2.8%); the call rows -4.8% to
  +1.6%, both signs: within noise.
- The anatomy against rev 273's (same tool, same fixture): runtime start (creation to Main) 6.74-7.06 vs 7.13-7.62,
  ~-0.3 ms in every row; process exit after the mark 7.01-7.68 vs 6.94-7.38, unchanged.

**Reading:** the loader maps four fewer DLLs (~0.3 ms, consistent across rows, below compare3's resolution). R1's
~3 ms "C#Bash exit beyond the AOT floor" does NOT come from ws2_32/iphlpapi/secur32/sspicli. icu.dll is now the only
loaded-DLL difference from the minimal AOT program; whether it (or anything else) is the exit cost is untraced.
Keeping rev 277 is the architect's call (Claude: keep; no behaviour change, the build guards it).

## 2026-10-16 -- DECIDED: keep rev 277 (lazy binding); tool lookup = a profile PATH plus a self-maintained C#Bash command map whose misses are permanent
**Status:** Active. The architect: "1/ keep it" (rev 277, after the explanation); "2/ good catch, record misses but
record them as 'fail once' if it is not there it is never there".

**Context:** Claude Code's PATH is one entry (`dist`) by the architect's choice; a long PATH costs every call through
Claude Code's snapshot (`hash rg` misses, ~15 ms, DECISIONS "REVIEW R1 RESULT"). The architect proposed a
`~/.bash_profile` PATH that C#Bash's own command map keeps fast; Claude added that misses must be remembered too.

**Decided:** (1) rev 277 stays. (2) The map records hits AND misses; a recorded miss is never searched again ("fail
once"). This drops Claude's proposed folder-time check on misses, and with it the spike that was to measure it.

**Plan (written 2026-10-16, AWAITING the architect's ratification; nothing built):**
- T1 profile: `~/.bash_profile`, only when `$CLAUDECODE` is set, sets PATH from the Windows PATH (HKLM then HKCU,
  `%VAR%` expanded, via System32's `reg.exe` by full path). It runs once per session, when Claude Code builds its
  snapshot; children inherit it. The architect's file: Claude drafts, the architect installs.
- T2 map: a per-user file, one map per PATH value (bash's own rule: assigning PATH clears the hash, so a new PATH
  folder starts fresh); a hit is used after one existence probe (gone: search again); a miss is recorded and
  final; `hash -r` clears it (memory and file), `hash -d name` one name; a temporary `PATH=… cmd` bypasses it, as
  today; written atomically (temp + rename), a lost concurrent update only costs one re-search.
- T3 measure: `claudecall.sh --build` with the full Windows PATH: the snapshot segment from ~15 ms to ~1 ms on calls
  after the first; `compare3.sh --build` no regression; suite green both modes plus a new case (hit, remembered
  miss, `hash -r`, PATH change, two shells writing at once).

**The risk, put to the architect:** a tool installed into a folder ALREADY on PATH (pip, `dotnet tool install`,
`npm -g`) after its name was recorded as a miss stays "command not found" until `hash -r`. Claude's position: keep
the rule as ruled, and say so in the message (`bash: x: command not found (remembered miss: hash -r searches
again)`), since a silent false "not found" sends Claude chasing a tool that is there. Falsifier: a test or battery
case that compares that message byte for byte with GNU; then bash's text stays and the README documents it.

## 2026-10-16 -- RATIFIED: the tool-lookup plan, T1 amended to `$BASH_PATH`; the remembered-miss note in the error
**Status:** Active. The architect: "Use $BASH_PATH instead of $CLAUDECODE - this has generic utility to any use of
BASH. Yes, keep the rule, but say so in the error. Ratified", and "Yes" to: `$BASH_PATH` holds the PATH list, set by
the launcher. Amends the plan in the entry above.

**T1 (replaces the profile and the reg.exe read):** at startup, if `BASH_PATH` is set and not empty, C#Bash sets
PATH from it (the process environment too, so children inherit it) and then UNSETS it (Claude's call, stated:
otherwise a nested `bash` would reset PATH and drop its parent's `export PATH=/x:$PATH`; PATH carries the list from
there on). A launcher narrows its own PATH and hands the list on, e.g. `set BASH_PATH=%PATH%` then `set PATH=…dist`.
`claude.exe` then carries the list under a name it never searches; no file to keep; any launcher can use it.

**T2 (as written, plus):** the map is read on the first lookup, not at startup; one file per PATH value (with
PATHEXT), a stable hash in the name and the PATH in the file; not kept when PATH has a relative entry (the answer
would depend on the directory); the oldest files pruned beyond a small number; under `%LOCALAPPDATA%` (so a test can
point it elsewhere). A remembered miss prints `bash: x: command not found (remembered miss: hash -r searches
again)`; a miss found by searching prints bash's own message.

**T3:** as written.

## 2026-10-16 -- DECIDED (Claude's call, delegated): only `hash` records and obeys misses; every other lookup is bash's
**Status:** Active. Amends T2 of "RATIFIED: the tool-lookup plan". The architect, asked which rule to build: "You tell
me - come on, you can see further into this crud than I can?"

**Context:** the first build applied "fail once" to every lookup through `ResolveOnPath`. The suite then failed two
GNU-compared cases (`shellbuiltins`, `error_prefix`): a script that runs a missing command twice in one process got
the note on the second run where bash prints its plain message. Measured on that build: a script that misses a
command, creates it and runs it again fails for a `.cmd` (Windows' own search tries only `.exe`), and for an `.exe` it
runs while `command -v` still says it does not exist. The architect asked why a query (`command -v`) should record a
miss at all.

**Decision:** a miss is recorded only by `hash name`, and obeyed only by `hash name` when an EARLIER process recorded
it (a process's own miss is searched again, as bash). Running a program, `command -v`, `type` and the
unsupported-option fallback search past a remembered miss and never write one. Every lookup remembers hits (one
existence probe before use) and a found name replaces a remembered miss. The note is `hash`'s alone:
`hash: x: not found (remembered miss: hash -r searches again)`.

**Why:** the measured per-call cost is one probe, the snapshot's `hash rg` (its only PATH probe; `command rg` inside
its function runs only when claude.exe is missing), so this keeps all of the saving. Elsewhere a remembered miss buys
one search on a path that is failing anyway (~15 ms) and costs a false "not found" (a confused Claude); bash
searches every time. **Falsifier:** a measured per-call miss other than `hash rg` (Claude Code probing with `command
-v`, or Claude re-running a missing tool often enough to show): then that lookup records misses too.

## 2026-10-16 -- BUILT (T1, T2): `BASH_PATH` and the command map; two older defects found on the way (open)
**Status:** Active. The plan's T1 and T2 as ratified and amended above.

**Built:** `ShellEnvironment` takes a non-empty `BASH_PATH` as PATH at startup and removes it (frames and process
environment); `Evaluator/CommandMap.cs` (per-PATH files under `%LOCALAPPDATA%/CSharpBash/commands`); `ResolveOnPath`
uses remembered hits for every lookup and remembered misses for `hash name` only (`probe: true`); `hash -r` clears
the map (`HashReset`), `hash -d` forgets a name in it; `hash`'s "not found" carries the note when an earlier
process's miss answered. Case `command_map` (deployed rev 266 fails it); the process-environment removal proven by
disabling it (the nested `"$BASH" -c` check then fails). Suite 139/139 both modes on the AOT build, run twice against
the real map; battery 226/237 against 225.

**Found on the way, in deployed rev 266, NOT fixed (open, the architect's to schedule):** (1) with PATH holding only
`dist`, a relative path with a slash and no `./` (`Bash/bin/Release/net8.0/Bash.exe`) gives "command not found"
while `./Bash/…`, the absolute path, and the same relative path under the full PATH work; cause not traced.
(2) An extensionless `#!` script found on PATH gives "Permission denied" (Windows cannot run it; bash runs it by its
interpreter line); a `./script` or a `.sh` file works.

## 2026-10-16 -- RESULT (T3): with the full PATH, a Claude Code call is ~16 ms, not ~31 (the snapshot 15 ms -> 0.6 ms)
**Status:** Active (a measurement). T3 of the tool-lookup plan; prediction (16-17 ms) met.

**Measured (`claudecall.sh --build`, rev 282 vs deployed rev 266, the snapshot fixture's PATH = the 50-entry PATH of
2026-10-15, so `hash rg` misses):** true 16.4 vs 31.1 ms (-47%), ls 16.1 vs 31.5, cat-head 16.4 vs 31.5, cd-pwd 16.0
vs 31.8 (-50%), grep-rn 24.2 vs 39.1, git-status 33.4 vs 49.6; bare 14.5 vs 15.0 (noise). Anatomy: the snapshot
segment 0.55-0.60 ms (rev 278: 14.6-16.9); every other segment unchanged. So a call costs what the platform floor
plus C#Bash's start and exit cost, whatever the PATH length: the architect's PATH need no longer be kept short.

## 2026-10-16 -- RESULT (exit trace): C#Bash's exit costs ~0.25 ms, as any AOT program's; R1's "+3 ms exit" was misplaced
**Status:** Active (a measurement; corrects the attribution in "REVIEW R1 RESULT", which stands as written). The
architect: "let's trace that 3ms of exit time just to be sure we covered all the exits".

**Method (a diagnostic, not the benchmark):** whole launches by tools/benchtimer, 100-launch totals, median of 5
rounds in rotating order, timing switch OFF. A scratch minimal AOT program, and a scratch C#Bash build (rev 283 plus
an uncommitted `BASH_PROBE_EXIT` switch, reverted after the build), each leaving three ways: normal return;
kernel32 `ExitProcess` (skips .NET's shutdown, DLLs still unload); `TerminateProcess` on itself (skips both).

**Measured (ms per launch):** minimal 10.60 / 10.62 / 10.43; minimal with ICU loaded 11.32 / 11.23 / 11.10; C#Bash
`-c 'exit 0'` 14.39 / 14.44 / 14.14. So C#Bash's whole exit path (.NET shutdown + DLL unloading) is ~0.25 ms, the
minimal+ICU program's ~0.22: no exit cost of its own. Its ~3 ms over minimal+ICU stays when exit is skipped
entirely (14.14 vs 11.10), so it is before exit: start-up, which R1's anatomy measured (~2.6 ms after Main: console
0.7, environment 1.6, options and parse 0.35; the rest creation to Main for a larger image). Where the "+3 ms exit"
came from: the timing switch, ON, costs ~1.85 ms a launch (16.08 vs 14.23 ms) and its exit work (the process start
time, the log append) runs after its own `exit` mark. ICU costs ~0.7 ms a launch (~0.5 load, ~0.2 unload): the one
DLL lever left, and a behaviour change (`InvariantGlobalization`), so an R4 candidate.

**Not separable without ETW (the session is not elevated):** the kernel's share of process teardown; that the 3 ms
is start-up rather than a larger teardown is inferred from R1's measured init, not traced.

**Found on the way (open, the architect's to schedule):** (1) `name[subscript]+=value` is not an assignment: C#Bash
runs it as a command ("r[k]+=a: command not found") for indexed and associative arrays, literal or variable
subscripts (rev 266 and the current code; GNU appends). `name[sub]=v` and `name+=v` work. No test runs the form
(`command_printing` only prints it). (2) The command map prunes by last WRITE: each test's temporary PATH makes a
map file (18 after today's runs), and a settled map is rarely rewritten, so two suite runs can push out the real
one (costs one search, never a wrong answer). Claude's position: touch a map when it is read, at most hourly, so
pruning keeps the ones in use; or give the suite its own LOCALAPPDATA.

## 2026-10-16 -- FIXED: `name[subscript]+=value` appends to the element (it ran as a command)
**Status:** Active. The architect: "Yes, fix" (found during the exit trace, entry above).

**Cause (read, then reproduced):** `Word.TrySplitIndexedAssignment` accepted only `]` followed by `=`, so `a[k]+=v`
was no assignment and became a command name ("a[k]+=v: command not found"), the value lost; `name+=v` takes a
different path and worked. Separately, `declare a[k]+=v` parsed the `+` but assigned instead of appending.

**Fix:** the helper takes `]+=` when the caller allows it (the element-assignment parse; the `(...)` literal form
is unchanged); `ArrayElementAssign.Append`; `ShellEnvironment.AppendArrayElement` / `AppendAssocElement` shared
by the statement and `declare`/`local` (joined, or added under `declare -i`; a scalar is element 0, as bash:
`s=abc; s[0]+=x` is abcx); a declaration argument `a[k]+=v` prints back as written.

**Verified:** case `array_element_append` (expected = GNU 5.1 via WSL; byte-identical; deployed rev 266 fails it);
suite 140/140 both modes on the AOT build; battery 226/237; `-n` corpus 850/912 and printer corpus 845/912, both
at baseline.

## 2026-10-16 -- DECIDED: the command map lives in `~/.bash/commands`; maps are pruned by age, touched when read
**Status:** Active. Amends where "BUILT (T1, T2)" put the map. The architect: "I know 'Local' is the normal windows
place to put app data, but bash is not 'normal' and I think the bash persistent command data should be in a folder
/c/users/guy/.bash/commands", and "Prune" (Claude's recommendation: touch a map when it is read, at most hourly).

**Decided:** the folder is `$HOME/.bash/commands` (HOME as the process has it, MSYS or Windows form, else the
Windows profile), beside bash's other files. Pruning: a map read is touched when its time is over an hour old, and
when a new map is made every map unused for 30 days goes (Claude's call, stated: under the earlier "keep the newest
32", a burst of new PATH values, one suite run makes ~18, could still push out a map in use however recently
touched; by age it cannot, and a map is ~2 KB). Tests isolate with HOME (was LOCALAPPDATA). The old folder,
`%LOCALAPPDATA%/CSharpBash`, held only today's test maps (no deployed build wrote there): removed.

**Verified:** `command_map` extended (an in-use map touched on read; a 40-day-old map pruned when a new PATH value
makes a map); the concurrency check finds its entries under `$HOME/.bash/commands`.

## 2026-10-16 -- DEPLOYED rev 286 (revs 267-286); notice sent to the eight sessions
**Status:** Active. The architect: "Prune, commit, deploy notify, publish?".

**Deployed by `tools/deploy.sh`:** `dist/Bash.exe` = `1.0.0+hg.0cc3c25dbada 286`; suite 140/140 on that binary
with a console and `-Detached`; battery 226/237 against 225; rollback `dist/Bash.exe.rev266`. Live check from a
Claude Code call: rev 286 runs; `r[k]+=a; r[k]+=b` gives `ab`; `hash x` records a miss and a later process
reports it with the note; maps in `~/.bash/commands`; `BASH_PATH` becomes PATH. Notice (behaviour changes first:
element append, remembered lookups and `hash -r`, `BASH_PATH`, ~16 ms calls) delivered to gps-time-2c,
adc-framework-d8, msp430-a2, nupkg-28, errorlogs-42, guy-b3, koliada-net-7c and daq-fe.

**Found in the live check (open, minor):** a long-lived shell's `hash -d name` does not see an entry another
process wrote after this shell first read the map (`Forget` trusts the in-memory copy when the name is absent from
it): it says "not found" and the entry stays. Each Claude Code call is a new process, so it bites only a long
interactive shell; the fix is to re-read the file in `Forget` (cheap: `hash -d` is rare).

## 2026-10-16 -- PUBLISHED: GitHub main = git 5cf4d27 = hg rev 287
**Status:** Active. By `tools/publish.sh` (the benchmark of deployed rev 286 found; doc review rev 287; 288 changesets;
bundle verified; remote main = HEAD). Git's "CRLF will be replaced by LF" for `tests/expected/command_map.out`
(and, already, `path_lookup.out`): `.gitattributes` normalises every text file to LF, so the CRs that `cmd`'s
`echo` puts in those expected lines are not in git. Harmless: `run-tests.ps1` compares lines, and both
`Get-Content` and PowerShell's capture of the shell's output take CRLF and LF alike as the line end.

## 2026-10-16 -- Housekeeping before the restart as a full C#Bash session (`BASH_PATH`): anchor rewritten, stale records fixed
**Status:** Active. The architect: "save _everything_ not already saved, check for ambiguities/stales/brokens and
restart as a full c#bash session using BASH_PATH".

- **RESUME anchor head rewritten** (the 2026-10-08 head had become one run-on START HERE paragraph that still listed
  resolved items as open: map pruning, the `loop` +6.3%). The old head is in hg rev 290's `PROJECT_CONTEXT.md`;
  every item in it is resolved with a DECISIONS entry or carried into the new head (the open defects, the review,
  the parked items, the "not raised" jobs-table note, the do-not-derive guards). The tail (KNOWN LIMITATIONS,
  SETTLED, DO NOT undo, machine facts) is byte-identical; the replacement was made between two marker lines.
- **Verified and recorded:** Git's `PortableGit/bin/_bash.exe` stub is broken (it looks for the renamed
  `usr/bin/bash.exe`); Git Bash is `usr/bin/_bash.exe`, as compare3 uses. The architect's 2026-10-16 pointer to
  `bin\_bash.exe` names the broken stub.
- **Told the architect (his global CLAUDE.md, suggested only):** two "Known limits of C#Bash" lines are stale on
  deployed rev 286: a UNC glob expands, and awk's 3-argument `match()` works.
- **Fixed:** ARCHITECTURE §10 said command lookup reads the process environment (stale since 2026-10-11: it follows
  the shell's own PATH). The Claude Code contract memory: the wrapper's `eval` no longer gets `< /dev/null`; the
  `BASH_PATH` launch added.

## 2026-10-17 -- RATIFIED: the defects first, then the review; the `BASH_PATH` launch verified from `claude.exe`'s environment
**Status:** Active. The architect: "Yes, go on the defects!" (Claude's recommendation, re-stated at the resume: the
goal order puts a correct shell before a faster Claude, and the review only measures, so it loses nothing by
waiting). Order: (a) a script found on PATH; (b) `hash -d` in a long-lived shell; (c) a relative `a/b.exe` with
PATH = `dist`; (d) `PS4`, `$EPOCHREALTIME`. Then R2-R4.

**Verified at the session start** (the architect: "Can you look at claudes own environment?"): each ancestor's live
environment block read from its PEB by a scratch reader (control: a probe variable it set in itself, read back).
Windows Terminal: no `BASH_PATH`, PATH 48 entries. `cmd /k kes-term` (the start file) and `claude.exe`: `BASH_PATH`
48 entries, PATH = `F:/Koliada/Tools/bash/dist`. The shell (`Bash.exe` 286) and its child: no `BASH_PATH`, PATH 50
entries (Git's `usr/bin` and `mingw64/bin` put first by `AugmentPathWithGitTools`). So the start file works and the
shell removes `BASH_PATH` from its process environment as designed.

## 2026-10-17 -- CAUSE + PLAN (defect a): a script found on PATH does not run; a bare name runs a script from the current directory
**Status:** Active. GREENLIT defect (a). Supersedes, in part, the 2026-06-14 "Shebang dispatch" entry's
"PATH-resident extensionless scripts aren't auto-detected yet (only explicit paths / cwd files)".

**Reproduced (deployed 286 against GNU 5.1 in WSL; case `path_script`, red on 286):**
- A `#!` script found on PATH, and a `.sh` file found on PATH: "Permission denied", 126. GNU runs both; `$0` is
  the path found.
- A PATH match that cannot run (`p1/tool`, data) ahead of a `#!` script (`p2/tool`): "Permission denied"; GNU
  passes over it. When the only match cannot run, both say "Permission denied", 126; GNU names the path, C#Bash
  the name. GNU still hashes it, and `type -t` says `file` (measured).
- A bare name runs a `#!` or `.sh` file from the current directory, before PATH: a planted `#!` file named `git`
  ran instead of `git.exe`; GNU runs git. Any directory one `cd`s into (a clone, a download) could replace any
  external tool, in a terminal and in every Claude Code call. Builtins and in-process tools are not reachable so.

**Cause (read):** the shebang dispatch in `ExecExternal` called `TryResolveScriptFile(name)`, which resolves the
name against the current directory (`TranslatePath`), never against PATH; a PATH match then went to
`CreateProcess`, which cannot run a script (error 193, reported as "Permission denied"). `ResolveOnPath` took the
first file whose name matched, whether it could run or not.

**Plan (Claude's call):**
- `ResolveOnPath`: a bare match counts only if it can run by C#Bash's own `-x` (`FileTests.IsExecutable`: a
  PATHEXT extension, `.sh`, or `#!`). With none, the first match that cannot run is the answer, hashed as bash
  hashes it but not written to the command map (the map holds programs; a remembered hit is checked the same way).
- `ExecExternal`: a bare name is a script only at its PATH match, never in the current directory; `$0` is that path
  in shell form (as `command -v` shows it). A match with a PATHEXT extension is still never opened for its first
  line (the 2026-06-14 property). "Permission denied" after a PATH lookup names the path, as bash.
- The current-directory part is a behaviour change, made under the SETTLED "match GNU where behaviour is open" and
  as the secure default: the 2026-06-14 entry describes cwd files running and gives no reason for it. Falsifier:
  the architect wants a bare `script.sh` in the current directory to keep running; the cost is the shadowing above.

**Found, not in this fix (open, the architect's):** a bare name NOT on PATH still goes to `CreateProcess`'s own
search (`ChildLauncher` passes no application name), which looks in C#Bash's folder and the current directory: a
`zzplant.exe` there ran by `zzplant` (rc 0) in an ordinary terminal, and was 127 under Claude Code (it sets
`NoDefaultCurrentDirectoryInExePath=1`). It cannot shadow a PATH tool (PATH is searched first) but can supply a
missing one (a planted `rg.exe` where ripgrep is not installed). The fallback is the 2026-06 hash entry's "the
cache can only speed things up, never break resolution that previously worked". Claude's position: drop it (a bare
name not found on PATH is "command not found", as bash) once it is checked what the OS search finds that
`ResolveOnPath` does not (App Execution Aliases; C#Bash's own folder). The same search makes a bare `x.sh` in the
current directory, not on PATH, "Permission denied" (126) in an ordinary terminal; 127 under Claude Code.

## 2026-10-17 -- FIXED (defect a): a script found on PATH runs; a bare name never runs a script from the current directory
**Status:** Active. Implements "CAUSE + PLAN (defect a)" above, plus two fixes from the red team.

**Fix:** as planned: `ResolveOnPath` counts a bare match only if it passes `-x` (`FileTests.IsExecutable`), else the
first match that cannot run (hashed, not in the map), and re-checks a remembered hit the same way; `ExecExternal`
resolves first, takes a bare name as a script at its PATH match only, gives `$0` the path found, never reads a
PATHEXT match's first line, and names the path in "Permission denied". From the red team:
- **The interpreter chain is capped at 8** (`MaxInterpreterDepth`; Claude's call, real chains are one or two):
  `#!/usr/bin/env self` re-entered `ExecExternal` until the stack overflowed and the process died. Newly reachable
  by a bare name; `./self` already crashed 286 so. Now "NAME: SHEBANG: bad interpreter: too many levels of
  interpreters", 126, the stderr redirect honoured; the shell goes on (GNU execs forever).
- **A name with a drive colon is a path** (`IsPathName`: slash, backslash, colon). `F:x` counted as bare,
  `Path.Combine` dropped the PATH entry so it resolved in F:'s current directory, and with the lookup now ahead of
  the script check it was written to the map (`F:x<TAB>F:x`). It still runs, as the explicit path it is.

**Verified:** case `path_script` (expected = GNU 5.1 via WSL, byte-identical), case `interpreter_loop` (authored),
`command_map` extended (the drive-relative name: runs, kept 0). Each red first: `path_script` fails on deployed 286;
on the pre-fix build the loop crashed and the drive name was kept. Suite 142/142 with a console and `-Detached`
(Release build); battery 226/237 against 225. Not run: the `-n` corpus (parser untouched), the suite on the AOT
build (deploy does), compare3 (per lookup: one `-x` test on a remembered hit, one cwd existence probe fewer per
launch [unmeasured]).

**Red team (an independent agent; its repros run on the build, GNU via WSL):** held: no bare name ran a cwd file
through the script path, from a plain command, a pipeline, `&`, `command`, `env`, `exec`, `xargs`, `find -exec`,
`nohup`, `timeout`, `time`, `$( )`, `PATH=x cmd`, `setsid -w`, a case variant or the env-interpreter re-entry
(286 ran the planted file in every one); a PATHEXT file is never read as a script (a `.bat` starting `#!` ran under
cmd); `$0`, arguments, exit code, redirects, `VAR=x`, `env -i` match GNU. On this machine PortableGit's `#!` scripts
`egrep`, `fgrep`, `gunzip`, `zcat` now run (were "Permission denied"), and `F:/KoliadaES/bin/targets/CC8051/buildLib`
now runs ahead of `buildLib.bat` beside it, as in Git Bash; the `.bat` only runs that script under MSYS bash.

**Found, in 286 too, not fixed (open, the architect's to schedule):**
1. `.`/`source NAME` reads a file in the current directory before PATH; GNU searches PATH first and the current
   directory only when nothing is found. A planted file there wins over the intended one.
2. `hash NAME` and `type -a NAME` accept a PATH match that cannot run; GNU: `hash: NAME: not found` (rc 1, entry
   removed) and `type -a` "not found". `type`, `command -v`, `type -P` match GNU.
3. A PATHEXT match that CreateProcess cannot start (`zz.js`: Windows' PATHEXT lists `.JS`) stops the search
   ("Permission denied", 126) and is kept in the map; a later `zz.exe` is never reached.
4. Cosmetic: the env-interpreter re-entry hashes the interpreter too (`nb`, one hit; GNU lists the script only); a
   trailing dot (`s1.`) finds `s1` (Windows drops it); a relative PATH entry is searched from the process cwd, not
   a pipeline stage's; `export HOME` in a script does not move the command map's folder (it reads the process
   environment, so tests' in-process lookups write maps into the real folder, as `path_lookup`'s always have).

**The OS fallback (open, above), position refined:** README "Finding a command" documents it on purpose (`cmd` runs
on an empty PATH). Claude's position: keep Windows' own folders and drop the current directory (search C#Bash's
folder, System32 and the Windows folder explicitly), so a planted `rg.exe` cannot answer a missing `rg`. Falsifier:
anything that relies on the launcher finding a program in the current directory by a bare name.

## 2026-10-17 -- CORRECTION (defect c): the cause is `NoDefaultCurrentDirectoryInExePath`, not a PATH holding only `dist`
**Status:** Active. Corrects the cause in "BUILT (T1, T2)" (2026-10-16), the anchor, README's known gap and the
global CLAUDE.md limit line ("with PATH holding only C#Bash's folder").

**Measured (deployed 286; a copy of whoami.exe at `sub/w.exe`):** `sub/w.exe` is 127 under
`NoDefaultCurrentDirectoryInExePath=1` (Claude Code sets it) with the full PATH, with `dist` only and with System32
only, and runs with the variable unset whatever the PATH; `./sub/w.exe` always runs. Cause: a name with a slash
reaches `CreateProcessW` relative (no application name), and Windows SEARCHES for it (C#Bash's folder, the current
directory unless that variable says no, the system folders, PATH); bash never searches for a name with a slash. The
same search would also take `<PATH dir>/sub/w.exe` [inferred from the search order, not run]. Fix direction (match
GNU): resolve it against the shell's own current directory before launching.

## 2026-10-17 -- FIXED (defect b): a long-lived shell's `hash -d` forgets a name another shell wrote since it read the map
**Status:** Active. GREENLIT defect (b) ("DEPLOYED rev 286", found in the live check).

**Cause (read, then reproduced):** `CommandMap.Change` returned early on a forget when the name was not in this
process's copy of the map and never read the file, so `hash -d` said "not found" and the entry stayed.
**Fix:** a forget reads the file and removes the name there, writing only when it was there; it reports the name
as there when either copy had it.
**Verified:** `command_map` extended (a shell reads the map, its child records `zzl`, the first shell's
`hash -d zzl`: rc 0, the map keeps none); red on 286 ("hash: zzl: not found", rc 1, kept 1). Cost: one file read per
forget, which only `hash -d` and a remembered program that has gone (or no longer runs) cause.

## 2026-10-17 -- PLAN (defect c): a name with a slash is a path from the shell's own directory, never searched for
**Status:** Active. GREENLIT defect (c); cause in "CORRECTION (defect c)" above.

**Measured further (deployed 286):** the search is also a shadowing path: with no `sub/` in the current directory,
`sub/w.exe` ran `<a PATH dir>/sub/w.exe` (rc 0). A missing path says "command not found" (127); GNU says
"No such file or directory" (127).
**Plan (match GNU):** `ExecExternal` resolves a relative path name (after `TranslatePath`, which already makes it
absolute in a pipeline stage that changed directory) against the shell's cwd, so `CreateProcess` gets a full path
and searches nothing; a rooted or drive-relative name is left as it is. Error 2/3 for a path name is "No such file
or directory". The child's argv[0] becomes the full path, as it already is for a PATH match.

## 2026-10-17 -- FIXED (defect c): a name with a slash runs from the shell's own directory; a missing one is "No such file or directory"
**Status:** Active. Implements "PLAN (defect c)".

**Fix:** `ExecExternal` hands `CreateProcess` a full path for a path name (`FromShellCwd`: a relative path joined
to the shell's cwd; a rooted or drive-relative one as it is), so Windows searches nothing; error 2/3 for a path
name says "No such file or directory".
**Verified:** case `relative_path_launch` (authored: a copy of whoami.exe): `sub/w.exe` runs with
`NoDefaultCurrentDirectoryInExePath=1` and without; a `sub/v.exe` only under a PATH directory is "No such file or
directory", 127; `./nope`, `sub/nope` likewise; `../sub/w.exe` and a pipeline stage that changed directory still
run. Red on 286 (127 "command not found"; the PATH directory's `sub/v.exe` ran). Suite 143/143 with a console and
`-Detached`; battery 226/237.

## 2026-10-17 -- FIXED (defect d): PS4 is expanded for each trace line; `$EPOCHREALTIME` has microseconds
**Status:** Active. GREENLIT defect (d) (found in "REVIEW R1 RESULT", 2026-10-16).

**Cause (read, then reproduced against GNU 5.1):** the xtrace line printed `_env.Get("PS4")` raw
(`+ $LINENO: echo hi`); `$EPOCHREALTIME` was `ToUnixTimeMilliseconds() / 1000.0`, so its last three digits were 000.
**Fix:** `XtracePrefix` expands PS4 as the inside of "…" (`Parser.ParseDoubleQuotedText`, then the expander):
parameters, arithmetic, command substitution, quotes literal, `\$` a dollar, as GNU prints them. During the
expansion xtrace is off (a `$( )` in PS4 would trace itself and expand PS4 again) and `$?` is restored afterwards
(GNU: with `PS4='+$(exit 7) '`, `false; echo "status=$?"` prints 1). A PS4 with no `$`, `` ` `` or `\` is used as it
is (the default `+ `: no cost). `$EPOCHREALTIME` = UTC ticks since the epoch / 10, six digits.
**Not done (known gaps):** PS4's prompt escapes (`\u`, `\w`; bash decodes them "as with PS1"); a failed expansion
leaves PS4 as written.
**Verified:** case `ps4_epochrealtime` (expected = GNU 5.1 via WSL, byte-identical; red on 286 on every PS4 line and
"microseconds: no"). Suite 144/144 with a console and `-Detached`; battery 226/237.

**Found, not fixed (open, xtrace fidelity, the architect's to schedule):** GNU traces assignments (`+ x=sub`, and
`PS4=…` itself), repeats PS4's first character per level of indirection (`++ echo sub` inside `$( )`), and quotes a
traced argument that needs it (`+ echo 'a b'`); C#Bash does none of the three.

## 2026-10-17 -- RED TEAM (defect c): a colon name ran an NTFS stream (fixed); a background job works in the PARENT's directory (open, HIGH)
**Status:** Active. An independent read-only agent attacked rev 294; each item below it reproduced, the first two
also reproduced by Claude.

**Fixed (a regression of rev 293's colon rule):** `IsPathName` took any colon as a path, so `ads:bar.exe` (an NTFS
stream of the file `ads`) was joined to the cwd and ran; 286 said "command not found" under Claude Code, but ran it
too with `NoDefaultCurrentDirectoryInExePath` unset (Windows' launcher searched the cwd). Now only a drive (`F:x`)
makes a path; a bare name with any other colon is never searched (`ResolveOnPath`) and is "command not found",
127, before the launcher sees it (GNU: the same). `relative_path_launch` extended (red on the rev 295 build: the
stream ran; on 286 without the variable: ran). Suite 144/144 both modes; battery 226/237.

**Open, HIGH (data loss; in 286; NOT in the greenlit list; the architect's to schedule):** a background job resolves
relative paths against the PARENT shell's directory once the parent has moved: `cd r1; ( sleep 1; echo w > out.txt;
rm -f victim ) & cd ../r2; wait` deletes `r2/victim` and writes `r2/out.txt` (286 and the new build alike; GNU: both
in `r1`). The same runs `q/x.sh` for `( sleep 1; ./x.sh ) & cd ../q`. The red team's reading of the cause [not yet
verified by Claude]: `ShellEnvironment.AgainstActiveCwd` leaves a path relative when the shell's cwd equals its
`_baseCwd`, assuming the process cwd still equals `_baseCwd`, but the owning shell has moved it; `TryResolveScriptFile`
and `Classify` go through the same `TranslatePath`. Claude's position: fix it next, before review R2; it touches the
2026-10-03 "each shell's own cwd" design, whose stated invariant (a relative path stays relative so output that
echoes it does not change) the fix must keep.

**Open, low (in 286 unless said):** a root-relative (`\x`) or drive-relative (`C:x`) name in a pipeline stage that
changed directory resolves against the process cwd, not the stage's; `..` across a junction is lexical (GNU: the
target's parent); `find -execdir` is `-exec` (read in `Builtins.Find.cs`, not run); `sub/w.exe/x` is 127 "No such
file or directory" (GNU 126 "Not a directory") and `./sub` (a directory) "Permission denied" (GNU "Is a
directory"); an exe path over 260 characters fails, relative or absolute, though LongPathsEnabled=1.
**Noted, no action:** a path name's argv[0] is now the full Windows path, quoted (Git Bash: the same, unquoted;
GNU: as typed); nothing found that it breaks. Held: UNC cwd and MSYS tools by relative path now work (286: "not
found"); every entry point in a stage that changed directory resolves correctly.

## 2026-10-17 -- DEPLOYED rev 296 (revs 287-296); notice sent to the nine live sessions
**Status:** Active. The architect: "deploy, notify and next; go".

**Deployed by `tools/deploy.sh`:** `dist/Bash.exe` = `1.0.0+hg.82d59ea69d63 296`; suite 144/144 on that AOT binary
with a console and `-Detached`; battery 226/237 against 225; rollback `dist/Bash.exe.rev286`. Live check from a
Claude Code call: rev 296 runs; a `#!` script on PATH runs (`$0` its name); a planted `#!` `git` in the cwd is
ignored (git answers); `sub/w.exe` runs under `NoDefaultCurrentDirectoryInExePath=1`; `PS4='+ $LINENO: '` traces
`+ 4: true`; `$EPOCHREALTIME` has six live digits. Notice (behaviour changes first: no cwd scripts by bare name, PATH
scripts run, relative paths from the shell's cwd and their argv[0], colon names, PATH passes over files that cannot
run; then the fixes; then the open background-job defect with its workaround) delivered to gps-time-2c,
adc-framework-d8, msp430-a2, nupkg-28, errorlogs-42, guy-b3, koliada-net-7c, daq-fe and ananta-f5 (new since 286).
Global CLAUDE.md (git `d1dcadd`): the two fixed limit lines replaced by the cwd-script change and the open
background-job defect.

## 2026-10-17 -- CAUSE + PLAN: a background job resolves relative paths in the PARENT's directory (HIGH, data loss)
**Status:** Active. The architect: "deploy, notify and next; go" (next = this, Claude's recommendation). Extends the
2026-10-03 "each shell has its own working directory" design, whose "Not covered" names background jobs.

**Reproduced (deployed 296 against GNU 5.1; case `background_cwd`, red):** a job started in `r1`, run after the
parent's `cd ../r2`, wrote `out.txt` into r2, deleted r2's `victim`, globbed r2's files, `test -f` and a pipeline
looked in r2, `./s.sh` ran r2's script, and its own `cd sub` landed in r2/sub. GNU: all in r1.
**Cause (read, the red team's reading confirmed):** a copied shell (`CloneForSubshell`) never owns the process cwd
and starts with `_baseCwd` = its parent's directory; `AgainstActiveCwd` leaves a relative path relative while the
shell has not moved (`Cwd == _baseCwd`), trusting the process cwd to equal it. That holds for a pipeline stage (its
parent waits for the pipeline) and fails for a job (`&`, `setsid -f`), whose parent goes on and `cd`s. The job's own
`cd sub` then resolves `sub` against the moved process cwd too.
**Plan (Claude's call):** a shell whose owner keeps running is `Detached` (set on the `&` job's and the `setsid -f`
fork's environment; inherited by `CloneForSubshell` and `AdoptCwd`, so its stages, subshells, `$( )` and in-process
scripts are detached too); a detached shell ALWAYS resolves a relative path against its own `Cwd`. Race-free: no
"is the process cwd still mine?" test, which a parent's `cd` could falsify between the test and the file operation.
Pipelines and the shell itself keep the 2026-10-03 rule (relative stays relative, output unchanged). Cost to watch:
a tool that prints a translated path would print it absolute inside a job; the GNU comparison and the suite say.

**Peer report (msp430-a2, on 296; open item "the OS launcher"):** a bare `probe3.sh` in the cwd, not on PATH, is 126
"Permission denied" in its direct Claude Code calls (GNU 127); in this session the same command is 127. All ten
claude.exe processes have `NoDefaultCurrentDirectoryInExePath=1` in their live environment (read from their PEB);
that session's claude.exe started 2026-10-03, before the start file, and launches the shell as `.../dist/bash.exe`.
Which environment its shell process has is asked of the peer (a read-only ancestor reader); not concluded.

## 2026-10-17 -- FIXED: a background job works in its own directory (red team: two more paths, two regressions fixed)
**Status:** Active. Implements "CAUSE + PLAN: a background job resolves relative paths in the PARENT's directory".

**Fix:** as planned (`ShellEnvironment.Detached`: set by `Detach()` on the `&` job's and `setsid -f`'s copy,
inherited by `CloneForSubshell` and `AdoptCwd`; `AgainstActiveCwd` always resolves for a detached shell). From an
independent red-team pass (read-only, each item reproduced against GNU 5.1):
- **A relative PATH entry** (`PATH=bin;…`) was searched from the process cwd (`ResolveOnPath`, `which`), so a job ran
  its parent's program, or said "not found" for its own (in 296 too). Now a relative entry is looked in from the
  shell's own directory (`TranslatePath` of the entry), answered relative as bash prints it, never cached (the
  answer moves with the cwd; the map already skipped such PATHs), and launched by its full path (`FromShellCwd`).
- **`cd -` with a relative OLDPWD** used it untranslated (in 296 too): translated now.
- **Regression of the fix, fixed:** `ln -s d sub/l` made a FILE link inside a job: the directory test resolved the
  link target with `P(s)` (now absolute in a job) instead of against the link's folder; it now takes the target's
  form only (`TranslateForm`, made internal). The same was already wrong in a pipeline stage that had moved.
- **Regression of the fix, fixed:** `cp -v`, `ln -v`, and `mv`/`ln` messages printed the resolved (absolute)
  destination inside a job; they print it as typed (`shown`), as GNU, which also turns the main shell's
  `'b' -> 'sub\b'` into `'b' -> 'sub/b'`.
**Verified:** case `background_cwd` extended (relative PATH entry with commands only in the job's, only in the
parent's, and in both `bin`; `cd -`; a directory symlink; `cp -v`/`ln -v`), byte-identical to GNU three runs in
three; red on 296 and on the first fix. Suite 145/145 both modes; battery 226/237. Error messages inside a job
checked against GNU (rm, ls, cat, cd, cp, mv, mkdir, rmdir, touch, head, redirect): relative, as GNU. Red team held:
every redirection form, `source`, `pushd`/`popd`, file tools from `cp -r` to `awk` and `grep -r`, globs, tests,
externals with relative arguments, jobs from functions/scripts/jobs/stages, `setsid -f`, `nohup`, `timeout`, `eval`,
`trap EXIT`; 40-round race test of `cmd > f & cd ..` in six forms: 296 misplaced 11-16 of 40, the fix none.

**Found by the red team, not fixed (in 296, unrelated to the job's directory; open, the architect's):**
`( exec > f; echo in ); echo after` puts "after" in f too (a subshell's `exec` redirect outlives it; the main
shell as well as jobs); `mktemp mk.XXXX` creates in TEMP (GNU: the cwd); `readlink -f` prints backslashes;
`exec 5<> f; read -u 5` is "invalid file descriptor"; an MSYS tool (join.exe, cat.exe) inside `Bash.exe s.sh > out`
started from C#Bash fails "write error: Bad file descriptor" (through cmd it works). The red team also ran
`rm -f F:/TEMP/mk.*` for its own leftovers without listing the folder first (anything else named so would have gone).

## 2026-10-17 -- FINDING (the open "OS launcher" item, now LIVE under Claude Code): an empty PATH entry makes Windows search the cwd
**Status:** Active (a finding; no code changed). Resolves msp430-a2's unexplained 126.

**Measured:** Windows' launcher (`CreateProcessW` with no application name, which `ChildLauncher` uses for a bare
name C#Bash did not find) searches the PATH of C#Bash's PROCESS environment, not the shell's PATH variable, and
takes an EMPTY entry there as the current directory, `NoDefaultCurrentDirectoryInExePath=1` notwithstanding. A child
shell with process PATH `C:/Windows/System32;;C:/Windows` and the variable set: a bare `probe3.sh` in the cwd is 126
(the peer's result) and a planted `zzplant.exe` in the cwd RUNS (rc 0) while `command -v zzplant` finds nothing;
without the empty entry both are 127. Read from the live environment of all ten claude.exe processes: the eight
started 2026-10-03 to 10-15 carry the architect's 49-entry PATH with entry #46 EMPTY (a `;;`); the two started
today through the start file have PATH = `dist` (one entry), so their shells' launcher search is C#Bash's folder,
System32, the Windows folder. So in eight of ten live sessions a `.exe` planted in a cloned repo answers any bare
name missing from the shell's PATH (a missing `rg`). The shell's own PATH (from the snapshot) drops empty entries.
**Claude's position (raised, the architect's call: it supersedes the 2026-06 "the OS launcher's own search" hash
entry):** C#Bash's lookup is the authority. A bare name it does not find on the shell's PATH is looked for only in
C#Bash's own folder, System32 and the Windows folder (what keeps `cmd` working on an empty PATH, README), launched by
full path, else "command not found" (127) without the launcher; neither the process PATH nor the cwd is searched.
Falsifier: something relies on the launcher finding a program outside the shell's PATH and those three folders.
Mitigation outside the code: remove the empty entry from the PATH the architect's terminals inherit; sessions
started through the start file are unaffected either way.

## 2026-10-17 -- FINDING (addendum): the empty PATH entry comes from the user Path's trailing `;`
**Status:** Active (a finding). msp430-a2's registry read, re-checked by Claude: `HKCU\Environment` Path ends with
`;` (one empty entry); the system Path (HKLM) has none. A process PATH is system;user, so the trailing `;` becomes an
empty entry once anything is appended after it. Its position in the old sessions (#46 of 49) does not match exactly,
so something in the launch chain also edits PATH [inferred]. Mitigation (the architect's): delete that trailing `;`;
it reaches only sessions started afterwards. The C#Bash fix proposed above covers every case either way.

## 2026-10-17 -- PLAN (fix b): an empty PATH entry never reaches Windows
**Status:** Active. The architect: "Now, the fix b) go, then deploy and notify" (fix (a), C#Bash's lookup as the
only authority, stays open: not greenlit). The user Path's trailing `;` is the architect's to remove (not done).

**Plan:** `ShellEnvironment.WithoutEmptyPathEntries` drops empty `;`-separated entries (a `;;`, a leading or
trailing `;`), order kept, the string untouched when there are none. Applied (1) at startup to C#Bash's own
PROCESS PATH, which Windows' launcher searches for a name C#Bash did not find (the measured hole: a planted `.exe`
ran in the eight sessions whose claude.exe carries a `;;`); (2) to the PATH in every child's environment
(`ExecExternal`, the only launch site), so a program C#Bash starts cannot inherit one either. The shell's `$PATH`
variable stays as written (bash: an empty entry means `.`; C#Bash's own lookup already skips empty entries).
Falsifier: anything that needs a child to inherit an empty PATH entry.

## 2026-10-17 -- FIXED (fix b): no empty PATH entry reaches Windows; CORRECTION: a trailing `;` alone is harmless
**Status:** Active. Implements "PLAN (fix b)". CORRECTS this session's earlier claim (told to the architect) that a
trailing `;` is "just as dangerous" as `;;`: that measurement was C#Bash's own doing.

**Measured (deployed 296, `NoDefaultCurrentDirectoryInExePath=1`, a planted `zzplant.exe` in the cwd, the child's
live PATH read from its PEB):** process PATH ending in `;` with nothing appended: 127 (harmless); a `;;` inside the
list: 0 (the hole). The earlier "trailing `;`: ran" came from `AppendOwnDirectoryToPath`, which joined
`path + ";" + dir`, so `…;` became `…;;dir`; its presence test also missed C#Bash's folder spelled with `/`, so it
appended it again. And `Set("PATH")` copies the shell's PATH into the process environment, so a startup clean
placed before the appends was overwritten (the first version of this fix failed its own test so).
**Fix:** `WithoutEmptyPathEntries` applied where `Set("PATH")` copies into the process environment, once at startup
after `AugmentPathWithGitTools`/`AppendOwnDirectoryToPath` (the PATH as inherited, when nothing is set), and to every
child's PATH (`ExecExternal`). Both joins no longer create an empty entry; the own-folder test compares with `/` and
`\` alike. The shell's `$PATH` keeps what it was given.
**Verified:** case `path_empty_entries` (authored): C#Bash started by cmd with `;;`, and with a trailing `;` and no
own folder: the planted `.exe` is 127 (296: ran, both); the child's own `$PATH` has the empty entry only when given
one; `cmd` started from a shell whose PATH has `;;` and a trailing `;` sees neither. Red on 296. Suite 146/146 both
modes; battery 226/237.
**The user Path's trailing `;`** (the architect: "not my doing"): it is in the stored registry value; Windows copies
the value as stored; what wrote it is not determined. It is harmless on its own, so removing it is tidiness, not a
fix. **Found, not fixed (in 296):** `cmd.exe` started by a full forward-slash path (`C:/Windows/System32/cmd.exe /c
…`) misreads its command line ("The system cannot find the path specified"); `cmd` by name works [cause inferred].

## 2026-10-17 -- DEPLOYED rev 302 (revs 297-302); notice sent to the nine live sessions
**Status:** Active. The architect: "Now, the fix b) go, then deploy and notify".

**Deployed by `tools/deploy.sh`:** `dist/Bash.exe` = `1.0.0+hg.c815b7a0b05c 302`; suite 146/146 on that AOT binary
with a console and `-Detached`; battery 226/237 against 225; rollback `dist/Bash.exe.rev296`. Live check from a
Claude Code call: rev 302 runs; a background job's `rm -f victim` after the parent's `cd` removed the job's own
file; C#Bash started by cmd with a `;;` in its PATH does not run a planted `.exe` (127). Notice (behaviour changes
first: jobs in their own directory incl. relative PATH entries and `cd -`; no empty PATH entry reaches Windows, which
closes the planted-`.exe` hole in the sessions started before 10-17; `-v` paths as typed; then fix (a) still open)
delivered to gps-time-2c, adc-framework-d8, msp430-a2, nupkg-28, errorlogs-42, guy-b3, koliada-net-7c, daq-fe,
ananta-f5. Global CLAUDE.md (`~/.claude` git `f6c9c87`): the background-job limit line removed.

## 2026-10-17 -- RULED: the shell's `$PATH` keeps empty entries as given; only what reaches Windows is cleaned
**Status:** Active. The architect: "ok, leave as is" (after Claude's verification of fix (b) on rev 302: given
`abc\def;;fgh\ijk`, from the launcher or by `export`, the shell's `$PATH` keeps the `;;`; C#Bash's own process PATH and
every child's carry none; C#Bash's own PATH additions never create one; polarity: the 296 rollback copy's process
PATH keeps the `;;`). Unverified, the architect's reading: the user Path's trailing `;` was written by the System
Properties environment editor (check: edit an entry there, then `reg query HKCU\Environment /v Path`).

## 2026-10-17 -- FIXED: C#Bash's lookup is the only authority; Windows' launcher never searches for a bare name
**Status:** Active. The architect: "1/ yes" (to stopping Windows' launcher searching the current directory).
Supersedes the 2026-06 hash entry's "a cache miss falls back to the bare name so the OS launcher still does its own
PATH search" (its reason, "never break resolution that previously worked", is kept by the three folders below).

**Fix:** in `ExecExternal`, a bare name `ResolveOnPath` does not find is looked for in C#Bash's own folder, System32
and the Windows folder (`FallbackProgram`; `.exe` appended to a name without an extension, as Windows' launcher did;
a name with a non-PATHEXT extension: none), launched by its full path; else "command not found", 127, without
calling the launcher (`NotFound`, which makes the redirects first, as bash's child does: `nope > f` creates f, GNU
measured). So neither the current directory nor the process PATH is ever searched by Windows for C#Bash.
**Verified:** case `launcher_fallback` (authored): with `NoDefaultCurrentDirectoryInExePath` unset, a planted
`zzplant.exe` is "command not found" (302: ran) and a bare cwd `zzcwd.sh` not on PATH is 127 (302: 126 "Permission
denied"); `PATH=; cmd /d /c echo` still runs and `command -v cmd` reports nothing (README's documented behaviour);
a redirect on a missing command is made. Suite 147/147 both modes; battery 226/237.

## 2026-10-17 -- FIXED: a subshell's `exec` redirect ends with it (it took every later line of the shell)
**Status:** Active. The architect: "2/ yes".

**Reproduced (GNU 5.1 against deployed 302):** `( exec > f; echo in ); echo after` put "after", and every later line
of the script, into f (the whole case printed nothing); likewise `exec 2>`, `exec 3>` and inside `$( )`.
**Cause (read):** `exec` with only redirects adds a permanent `RedirectScope` to `_permanentScopes`; `ExecSubshell`,
`RunSubstitution` and `RunProcessSubstitution` restored variables, cwd, options and traps but never the scopes made
inside them.
**Fix:** `SubshellMark` records the count at `EnterSubshell`; `DropSubshellRedirects(mark)` disposes the newer ones,
newest first, after the subshell's EXIT trap (which still writes inside them, as bash) and before its own
redirects come off; all three call it. A `{ }` group is the shell itself: its `exec` stays.
**Verified:** case `subshell_exec_redirect` (expected = GNU via WSL, byte-identical: stdout, stderr, fd 3, `$( )`, a
pipeline stage, an EXIT trap writing into the subshell's redirect, `exit 3` inside, a group's `exec 4>` kept); red
on 302. Suite 148/148 both modes; battery 226/237.
**Test fixed on the way:** `native_args` picked its "no such drive" letter with `[ -d L:/ ]`, false for an empty
card reader; Q: (removable, not ready) appeared on this machine and the case failed, though the conversion rightly
treats Q: as a drive. It now takes a letter `fsutil fsinfo drives` does not list.

## 2026-10-17 -- FIXED: eight smaller differences from GNU, and a `hash` crash from rev 298 (live in deployed 302)
**Status:** Active. The architect: "Fix all the wrong results and smaller diffs from GNU".

**Each measured against GNU 5.1 first (case `gnu_differences`, expected = GNU via WSL, byte-identical):**
- `source name` / `. name`: PATH first, then the current directory (not in POSIX mode), as bash; the cwd's file came
  first, so a planted one was sourced instead.
- `hash name` with only a PATH match that cannot run: "not found", rc 1, not kept (it hashed it); `type -a name`
  lists every runnable match on PATH in order (`Evaluator.AllOnPath`), "not found" when none can run (it listed one,
  even one that could not run). Plain `type` and `type -P` already matched GNU.
- `mktemp x.XXXX`: in the current directory, the name printed as built (a template without `/` went to TEMP and
  printed its full path); `-t`, `-p`, `--tmpdir` and no template still use the temp directory.
- `readlink -f`: the shell form, as `realpath` (it printed backslashes).
- A path name through a file (`sub/s.sh/x`): "Not a directory", 126 (127 "No such file or directory"); a directory
  (`./sub`): "Is a directory", 126 ("Permission denied").
- `s1.` / `./s1.`: no such command / file (Win32 drops a trailing dot or space, so they ran `s1`). Only command lookup:
  `cat s1.` still opens s1 (a Win32 property of every file operation; recorded as a known limitation).
- `find -execdir`: from the file's own directory, `{}` = `./name`, a `+` batch per directory (it ran as `-exec`).
- `exec 5<> f` then `read -u 5`: `<>` had no in-process case (only for externals), so fd 5 never opened.
- **A crash introduced by rev 298, live in deployed 302 (red team):** `hash name` for a name found through a relative
  PATH entry indexed the hash table for a key rev 298 no longer stores (relative matches are not cached): an unhandled
  KeyNotFoundException killed the shell. Guarded; `hash` succeeds (rc 0), as GNU.
**Verified:** red on 302 (each item, and the crash per the red team: exit 82); suite 149/149 both modes; battery
226/237.

## 2026-10-17 -- FIXED (red team on the lookup changes, revs 298-306): `~` PATH entries, MSYS/root/drive-relative paths, a child's environment, missing interpreters, cmd.exe by a `/` path
**Status:** Active. An independent read-only agent attacked revs 298-306 (on a frozen copy of the build); each item it
reproduced, the ones below fixed (also in 296/302 unless said).
- **`~` PATH entry (security, medium):** looked up in the profile (`TranslatePath`'s `~`) but answered `~/x`, which
  the launcher joined to the cwd: a copy planted at `<cwd>/~/...` ran. `Evaluator.PathEntry` now decides every PATH
  entry once (lookup, `type -a`, `which`): `~` is `$HOME` (bash expands it at search time), the form translated
  (`/f/x` was looked up as F:\f\x), and only an entry whose form is not fully qualified (relative, `\x`, `C:x`)
  "moves with the cwd": answered as written, never cached. `TranslatePath`'s own `~` (the profile) is unchanged.
- **`\x` and `C:x` in a pipeline stage or job that changed directory** took the PROCESS cwd's drive (my list's item):
  `ShellEnvironment.FullAgainst` resolves them against the shell's own directory (`\x` on its drive, `C:x` in it when it
  is on C:, else C:'s own current directory), used by `AgainstActiveCwd` and the launcher's `FromShellCwd`.
- **A child's environment** began as C#Bash's process environment with the exported variables over it, so `unset X` and
  `export -n X` of an inherited variable (PATH included) still reached children, and an exported `path` replaced PATH
  (Windows names are case-blind). Now it is exactly the exported variables (every inherited variable is imported
  exported at startup), the shell's PATH winning over any other casing. Children now see the name `PATH` (was `Path`).
- **A missing interpreter:** `#!zzmissing` is "bad interpreter: No such file or directory" (126); `#!/usr/bin/env
  zzmissing` gets env's own message (127); both were "command not found".
- **cmd.exe by a full forward-slash path** (`C:/Windows/System32/cmd.exe /c …`) read its own path's `/`s as switches:
  `ChildLauncher` puts the program path in the command line with backslashes, as Git Bash does.
**Kept, deliberate safe divergences (not fixed):** an empty or unset PATH, or an empty entry, never searches the cwd
(GNU's does); after `PATH=x`, `cmd` still runs through the fallback while `type`/`command -v` find nothing (README).
**Verified:** cases `lookup_env` (expected = GNU via WSL) and `lookup_env_windows` (authored), red on 302 in every
section; `path_empty_entries` matches the child's PATH name case-blind. Suite 151/151 both modes; battery 226/237.

## 2026-10-17 -- FIXED: an MSYS program inside `Bash.exe s.sh >> out` can write (an inherited append-only handle)
**Status:** Active. The architect: "Fix all the wrong results".

**Reproduced:** inside `Bash.exe s.sh >> out` (started from C#Bash), Git's `cat.exe` said "write error: Bad file
descriptor" and its line was lost; with `> out` it worked; started directly (`cat.exe >> out`) it worked.
**Cause:** C#Bash's `>>` hands a child an APPEND-ONLY handle (FILE_APPEND_DATA, so concurrent appenders interleave,
DECISIONS 2026-10-04); an MSYS program takes such a handle as unwritable, which is why a direct MSYS child already
gets an ordinary write handle at the end (`Access.AppendAtEnd`). A nested shell INHERITS the append-only handle and
passed it on unchanged (`InheritFd`) to its MSYS child.
**Fix:** `ChildLauncher.WritableForMsys`: for an MSYS child, an inherited standard handle that is a disk file without
write-data access (`NtQueryObject` GrantedAccess) is reopened (`ReOpenFile`, GENERIC_WRITE) at the end, once per
original handle so a `2>&1` stdout and stderr keep sharing one; closed once the child holds its own.
**Verified:** case `msys_inherited_append` (authored, needs Git; red on the build before): the line arrives; with 2>&1
the output and cat's own error keep their order. Suite 152/152 both modes; battery 226/237.

## 2026-10-17 -- FIXED: set -x prints what bash prints (assignments, `++` levels, quoting, PS4 prompt escapes)
**Status:** Active. The architect: "Fix all ... smaller diffs from GNU" (the xtrace items of "FIXED (defect d)").

**Measured against GNU 5.1 first, then matched byte for byte (case `xtrace_fidelity`):**
- Every assignment is traced as it is made: a bare one (`+ x=1`, `+ y=sub` after its `++ echo sub`), a prefix one on its
  own line before its command (`+ FOO=bar`, `+ true`), each of several (`+ x=3`, `+ z=two`), an element (`a[i]=v`), a
  compound one as written (`+ a=(1 "2 3")`, from the node's source span). Values quoted as bash's
  xtrace_print_assignment (empty stays empty).
- PS4's first character once more per level of command (and process) substitution (`++`, `+++`): `Evaluator
  .XtraceDepth`, raised in `RunSubstitution`/`RunProcessSubstitution`, inherited by pipeline stages and jobs; a `( )`
  subshell stays at one level, as bash.
- Words quoted as bash's xtrace: '' when empty, single quotes when the word holds a shell metacharacter (bash's
  sh_contains_shell_metas, `'` as '\''), $'…' for other control characters (`PrintfFormatter.ShellQuote`).
- PS4's prompt escapes decoded before its expansion (`PromptExpander.DecodeForExpansion`: decoded values and `\$`
  protected; `\\` is one backslash the expansion may still use, so `PS4='[\\$] '` is `[$] `, as GNU).
**Verified:** `xtrace_fidelity` (expected = GNU via WSL; red on 302); `ps4_epochrealtime` unchanged. Suite 153/153
both modes; battery 226/237.

## 2026-10-17 -- FIXED: set -x traces compound commands as bash does (`[[ ]]`, `(( ))`, for, arithmetic for, case)
**Status:** Active. Found while closing the xtrace items: GNU traces these, C#Bash traced none.

**Measured against GNU 5.1, then matched (case `xtrace_compound`):** each `[[ ]]` primary as it is evaluated, operands
expanded and unquoted (`+ [[ a b == a* ]]`, `+ [[ -n a b ]]`), a lone word as `-n`, a `!` printed with the primary
it negates, a quoted pattern character backslashed (`"x*"` is `\x\*`: `ExpandToPattern` builds that form in the same
pass, so a `$( )` in the operand runs once); `(( expr ))` with its text as written (`((  1 + 2  ))`); a `for` head as
written on every turn; an arithmetic `for`'s init, each test and each step (`(( j++  ))`, the part with its front
trimmed, from the parser's existing `Heads`); `case WORD in`. `while (( … ))` traces through its `(( ))`.
**Verified:** red on 302; suite 154/154 both modes; battery 226/237.

## 2026-10-17 -- FIXED (in part): paths over 260 characters; the rest is a Windows limit, now said plainly
**Status:** Active. The red team's "an exe path over 260 characters fails; `cd` into one fails" (LongPathsEnabled=1).

**Measured:** C#Bash's file operations already worked on such paths (.NET); `cd` into a 386-character directory failed
("path is too long": SetCurrentDirectory without a long-path-aware manifest; it seemed to work only in a pipeline
stage, which never moves the process cwd). Starting a program whose own path is 391 characters failed in C#Bash AND in
cmd (error 206), the `\\?\` prefix too; starting any program from the long directory failed (error 267).
**Fix:** `Bash/app.manifest` (`<ApplicationManifest>` in the csproj) declares longPathAware, with the asInvoker and
Windows 10 supportedOS entries .NET's default manifest gives; the AOT publish embeds it (measured: the AOT build's `cd`
enters the long directory). Errors 206 and 267 (the latter with a cwd of 260+ characters) say "cannot start a program
whose path is / in a directory over 260 characters (a Windows limit)", 126 (were 127 with Windows' text). README and
the anchor's known limitations record what stays; also there: Win32 drops a trailing dot or space in EVERY file
operation (`cat s1.` reads s1), command lookup alone refuses it.
**Verified:** case `long_paths` (authored; red on 302); suite 155/155 both modes; battery 226/237.
**Found on the way (not C#Bash):** the no-windows hook flagged `Start-Process … -NoNewWindow` as lacking -NoNewWindow
when the flag came after a `$( )` substitution in the same argument string (a text-match false positive; the probe was
dropped as not needed).

## 2026-10-17 -- RAISED (two forks, the architect's): PATHEXT matches Windows cannot start; `..` after a junction
**Status:** Open, AWAITING THE ARCHITECT. The fix-everything run ends here: every other recorded item is fixed above.

1. **A PATHEXT match CreateProcess cannot start** (`.js`, `.vbs`, `.wsf`, `.msc`, … in Windows' PATHEXT). Measured: with
   `p7/zz.js` before `p8/zz.exe` on PATH, C#Bash stops at zz.js ("Permission denied", 126) and `[ -x zz.js ]` is true;
   Git Bash runs `p8/zz.exe` (`type -a`: only that) and `-x zz.js` is false; cmd runs zz.js through Windows Script Host
   [recalled, not run: it can open a window]. Choices: pass over such matches (a program is what CreateProcess can
   start: .COM .EXE .BAT .CMD, plus `.sh` and `#!`), so the later zz.exe runs and `-x` agrees; or run them through
   their file association, as cmd. Claude: pass over them (Git Bash's and bash's answer, and the secure default:
   running any `.js`/`.vbs` on PATH by bare name through WSH is a classic malware route, and WSH can open windows).
   Falsifier: the architect runs `.js`/`.vbs`/`.msc` tools by bare name from the shell.
2. **`..` after `cd` into a junction or symlink.** C#Bash resolves `..` lexically (`cd J; cat ../x` reads J's parent's
   x), as Win32 does for every Windows program; GNU and Git Bash resolve it physically (the target's parent). bash's own
   `cd ..` is logical (-L) either way. Claude: keep lexical, because it agrees with every Windows program the shell
   starts (handing them a path that means something else would be worse), and physical resolution would mean
   resolving reparse points on every path with a `..`. Falsifier: scripts ported from Linux that rely on physical `..`.

## 2026-10-18 -- RULED: `..` after a junction stays lexical (Windows' resolution), as Claude proposed
**Status:** Active. The architect: "2/ ok" (to "RAISED (two forks)" item 2). C#Bash keeps resolving `..` lexically,
as every Windows program it starts does; GNU/Git Bash's physical resolution is a recorded, deliberate difference.
The PATHEXT fork (item 1) stays open.

## 2026-10-18 -- RULED + FIXED: only what Windows' launcher starts is a program (.com .exe .bat .cmd, .sh, #!)
**Status:** Active. Resolves "RAISED (two forks)" item 1. Claude, asked what GNU and Claude expect: bash matches only the
exact name and passes over any match it cannot execute; Claude Code is built around Git Bash, which runs the later
`zz.exe`; running `zz.js` through Windows Script Host would both run something other than what `type -a` reports and
can open a dialog that hangs a Claude Code call. The architect: "Yes make the change, then deploy, notify and publish".

**Fix:** `FileTests.IsProgramExtension` (.com .exe .bat .cmd) replaces "any PATHEXT extension" in `IsExecutable` (so
`[ -x ]`, `ls -F`, `find -executable` agree), in the launcher (`IsProgramFile`: a `.js` with a `#!` line is now a
script, not a refused program) and in the PATHEXT loops of `ResolveOnPath`, `AllOnPath` and `which` (PATHEXT still
decides which extensions a bare name is tried with). A name only a `.js` answers is "command not found" (127); naming
the `.js` in full is "Permission denied" (126), as bash with a file it cannot execute.
**Verified:** case `pathext_programs` (authored; red on 302 in every section); suite 156/156 both modes; battery 226/237.

## 2026-10-18 -- DEPLOYED rev 316 (revs 303-316); notice sent to the nine live sessions
**Status:** Active. The architect: "Yes make the change, then deploy, notify and publish (push)".

**Deployed by `tools/deploy.sh`:** `dist/Bash.exe` = `1.0.0+hg.32784b3827fd 316`; suite 156/156 on that AOT binary
with a console and `-Detached`; battery 226/237 against 225; rollback `dist/Bash.exe.rev302`. Live check from a
Claude Code call: rev 316 runs; `( exec > f; echo in )` keeps the later line on stdout; `hash` of a relative entry's
match returns 0 (302 crashed); a planted `.exe` in an ordinary terminal is 127; `zz.js` before `zz.exe` runs the
exe; `set -x` prints `+ echo 'a b'`. Notice (behaviour changes first: the launcher never searches the cwd, only
.com .exe .bat .cmd are programs, a child gets exactly the exported variables and the name PATH, `mktemp x.XXXX` in
the cwd, `source` PATH-first, set -x in bash's format, bash's messages; then the fixes, the 302 crash among them)
delivered to gps-time-2c, adc-framework-d8, msp430-a2, nupkg-28, guy-b3, koliada-net-7c, daq-fe, guy-97 (new),
ananta-f5 (errorlogs-42 has ended).

## 2026-10-18 -- BENCHMARK of deployed rev 316 (for the publish); a possible slowdown carried to the performance review
**Status:** Active (a measurement). `tests/bench/compare3.sh` on dist/Bash.exe (rev 316), run unmodified from Git Bash;
every row's output identical in all three shells. C#Bash: loop 0.185, arith 0.254, func 0.229, loop_big 1.491,
coreutils 0.066, pipeline 0.266, find 0.024 s, startup 18.3 ms (Git Bash 16.4 ms, WSL 111 ms). README's table and its
sentences updated from this run (the publish rule: the benchmark of the deployed build and a doc review).
**Against the rev 286 run (2026-10-16):** loop +9.5%, func +5.5%, loop_big +5.4% (C#Bash only: Git Bash's and WSL's
columns moved under 2%), the others within ±5%. Not established as a regression (one run each; `loop` moved +6.3% at
rev 277 and was judged noise): carried to review R2, to be measured with alternating rounds (`compare3 --cs` against
the rev 286/302 rollback copies, or the R1 timing switch). These rows run `while [ $i -lt N ]` loops, so the one
waste found by reading does NOT explain them: the `[[ ]]` trace strings of rev 312 are built before the xtrace check
on every `[[ ]]` test (`CondTrace($"…")`, `op + val`), an allocation per test with set -x off. Listed for R4.
