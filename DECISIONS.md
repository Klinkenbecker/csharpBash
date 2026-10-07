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
