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
