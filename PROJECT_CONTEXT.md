# Project Context: Bash

> ## RESUME ANCHOR (rewritten 2026-10-08) — read this before anything else
> Replaces the anchor kept from 2026-09-04 to 2026-10-07, whose dated blocks had piled up: resolved
> items were still listed as open, and the 2026-10-07 handoff missed one open item it carried. The
> old text is in hg rev 140's `PROJECT_CONTEXT.md`; every resolved item in it has a DECISIONS entry.
> Checked against the session transcripts of 2026-09-03 to 2026-10-07 on 2026-10-08.
>
> **NEXT SESSION, START HERE (updated 2026-10-10, the session after the third compaction):** the
> architect's steer, "1, 2, 3 go": all three BUILT (awk rev 193, `umask`/`times`/`fc` rev 196,
> `jq -R` rev 197; awk escape defects found on the way, revs 194-195). The architect: "did we loose
> track of [one command at a time]? We should discuss after 1, 2, 3" and, mid-work, "are you
> converging on outcomes?" (answer: stop collecting found-on-the-way fixes). 2026-10-11: item 6
> DECIDED (match bash; see AWAITING 6); "go for both jq bugs": fixed, rev 198. Then DECIDED: a
> missing feature prints a prefilled GitHub issue link, never an automatic post; `bash -r` /
> `--restricted` refuse to start (they are silently ignored today) — GREENLIT, after the plan
> (DECISIONS "a feature C#Bash lacks says so …"). The architect: "I don't think [of] everything;
> that is explicitly, your job!" — push back unasked. Item 6 (ratified plan) BUILT revs 201-207; its
> eval line numbers DECIDED "physical line" (the architect). Restricted mode BUILT rev 208 (`set -r`
> ends the shell: Claude's call, stated). `--posix` a notice, rev 209. The architect: "fix --posix, then
> deploy, notify and publish": ALL DONE 2026-10-11 (rev 209 deployed, six sessions told, GitHub at hg
> rev 211). Then "yes": the `BASH_COREUTILS=external` crash FIXED rev 213; "deploy, notify and push":
> rev 213 DEPLOYED, the six sessions told, and PUSHED (GitHub at hg rev 214), all 2026-10-11.
> Then "go" (the open items): `-t -k -p` BUILT rev 216. "Build": DEBUG and RETURN traps, `$BASH_COMMAND`,
> `-T`, `declare -ft`, trap `$LINENO` BUILT rev 217 (cost within noise, measured). "yup": rev 217
> DEPLOYED and the six sessions told (2026-10-11). Then "go next": **IN FLIGHT: the OPEN DEFECTS, in
> order** (`$0` FIXED rev 219; PATH lookup FIXED rev 220; a `case` inside `$( )` FIXED rev 221; `kill -l`/`trap -l` table FIXED rev 222; dir stack `+N`/`-N` FIXED rev 223; `jobs -x` FIXED rev 224; `%name`/`%?text` jobspecs FIXED rev 225; `a && b &` backgrounding only `b` FIXED rev 226 (in deployed rev 217 too); IFS splitting FIXED rev 228; case ;& ;;& FIXED rev 238; PRINTER P1-P5 DONE revs 239-243 (declare -f, type, jobs as bash; corpus 845/912); diff -u --label FIXED rev 244; RAISED: arrays are not local (serious, variable model); next per GREENLIT: compgen; then git's credential prompt on `/dev/tty` (inferred, untraced)). Revs 218-252 DEPLOYED 2026-10-12 (rev 252). **Revs 254-257 DEPLOYED 2026-10-13 (rev 257: awk getline, MSYS arguments, sed, rm incl. the junction DATA LOSS fix; rollback `dist/Bash.exe.rev252`; six sessions told).** PUBLISHED 2026-10-13: GitHub main = git `0b3547d` = hg rev 259 (by `tools/publish.sh`). `cmd //c` FIXED rev 261 (2026-10-14, "go"; NOT deployed: it goes with the next deploy, "1/ next"). The Git Bash guard DROPPED from deploy.sh/publish.sh, rev 262 ("drop it, go"; both run under C#Bash now). `strings` BUILT rev 264 (scope ratified 2026-10-14). compgen BUILT rev 265 (2026-10-15; `-c` PATH scan DEFERRED). NOT deployed: revs 261-265 (cmd //c, the tools, strings, compgen). The open-defects run is DONE: git's credential prompt traced and FIXED rev 266 (2026-10-15). **Revs 261-266 DEPLOYED 2026-10-16 (rev 266, by C#Bash itself; rollback `dist/Bash.exe.rev257`; eight sessions told).** **PUBLISHED 2026-10-16: GitHub main = git `94a3ed2` = hg rev 268** (by `tools/publish.sh` under C#Bash). **GREENLIT 2026-10-16 ("ratified, two additions sound good"): the PERFORMANCE REVIEW R1-R4** (DECISIONS "RATIFIED: the performance review plan"): R1 DONE 2026-10-16 (DECISIONS "REVIEW R1 RESULT": a ~31 ms call = ~11 ms platform floor + 15 ms snapshot, of which the failed `hash rg` PATH search is ~14-15 ms; the architect: the probe is pointless since rg is inside claude.exe, Claude Code's to fix, reporting upstream is his call). **2026-10-16 (the next session): Claude Code's PATH is now ONE entry, `dist`, by the architect's choice ("there is no PATH"; calls are faster for it, the architect), so the planned claudecall re-run with a trimmed PATH was moot. "yes, lazy load": BUILT rev 277 (ws2_32, iphlpapi, secur32 off the AOT direct-call list; an idle shell loads 20 modules, not 24; suite 138/138 both modes) and MEASURED (DECISIONS "RESULT: lazy binding"): start ~-0.3 ms, exit unchanged, startup rows within noise; R1's ~3 ms extra C#Bash exit is NOT those DLLs (icu.dll, untraced, is the only DLL difference left). Tools needing dotnet/hg/cygpath ran with the 2026-10-15 snapshot's PATH given to them only (a stopgap). DECIDED 2026-10-16: (a) rev 277 stays ("keep it"); (b) tool lookup = a `~/.bash_profile` PATH plus a C#Bash command map C#Bash maintains itself, recording hits AND misses, a miss final ("fail once": "if it is not there it is never there"; no folder-time check, no spike). RATIFIED, with T1 amended to `$BASH_PATH` (a launcher's PATH for the shell, taken once and removed; the architect: "generic utility to any use of BASH"). Then DECIDED (Claude's call, delegated: "You tell me"): only `hash name` records and obeys misses (an earlier process's; its own process searches again); running a program, `command -v`, `type` search as bash; every lookup remembers hits. T1 + T2 BUILT (DECISIONS "BUILT (T1, T2)"; case `command_map`; suite 139/139 both modes; battery 226/237). T3 MEASURED (DECISIONS "RESULT (T3)"): with the full 50-entry PATH a call is ~16 ms, not ~31 (snapshot 15 ms → 0.6 ms; rev 282 vs 266). DEPLOYED 2026-10-16: rev 286 (revs 267-286: lazy binding, BASH_PATH, the command map in ~/.bash/commands pruned by age, `a[k]+=v`; DECISIONS "DEPLOYED rev 286"); README reworked (table in the intro, startup and lookup, Claude Code use with the start file, call cost). EXIT TRACED (DECISIONS "RESULT (exit trace)"): C#Bash's exit path is ~0.25 ms like any AOT program's; its ~3 ms extra is start-up; R1's "+3 ms exit" was the timing switch's own cost (~1.85 ms when on); ICU ~0.7 ms a launch is the last DLL lever (behaviour change: R4). FIXED (found): `name[sub]+=value` was run as a command (DECISIONS "FIXED: `name[subscript]+=value`"). OPEN: map pruning by last write lets test runs push out the real map (Claude: touch on read, hourly). The launcher the architect needs (given to him): `set "BASH_PATH=%PATH%"`, `set "CLAUDE_CODE_GIT_BASH_PATH=F:/Koliada/Tools/bash/dist/Bash.exe"`, `set "PATH=F:/Koliada/Tools/bash/dist"`, then claude.exe. OPEN (found on the way, rev 266, not fixed): a relative `a/b.exe` with PATH=dist is "command not found"; an extensionless `#!` script on PATH is "Permission denied". (c) DONE 2026-10-16: the statusLine runs `C:/Windows/py.exe` (the architect: "fine"; checked with no PATH). Unattributed: compare3 loop +6.3%, loop_big +3.8% rev 277 vs 266 (revs 267-276 differ; needs 276 vs 277).** Then R2 throughput, R3 parse, R4 ranked report; NO optimisation code before the architect picks. Deferred: the `compgen -c` PATH scan. Deploy only on "deploy and notify". The cold-resume briefing
> still comes first.
>
> **NOW (2026-10-16): `dist/Bash.exe` = rev 286 (`1.0.0+hg.0cc3c25dbada 286`, deployed by tools/deploy.sh; rollback
> `dist/Bash.exe.rev266`; eight sessions told). The TRUE STATE bullets below are the 2026-10-11 record, kept as history.**
>
> **TRUE STATE (verified 2026-10-11):**
> - **DEPLOYED 2026-10-12 (later): `dist/Bash.exe` is rev 252** (`1.0.0+hg.e7a1f2717689 252`, by `tools/deploy.sh`: printer, case ;& ;;&, declaration arrays, diff --label, VARIABLE SCOPE; rollback `dist/Bash.exe.rev233`; six sessions told). **PUBLISHED 2026-10-12: GitHub main = git 02f95bc = hg rev 235** (by `tools/publish.sh`). Earlier the same day: **`dist/Bash.exe` was rev 233** (`1.0.0+hg.2a0678c462c5 233`), by `tools/deploy.sh`: revs 218-233 (the open defects, IFS, `a && b &`, the contract tools); suite 123/123 on the AOT binary both modes; live-verified; rollback `dist/Bash.exe.rev217`; six sessions told. Before it, rev 217 (`1.0.0+hg.64cd033c7de6 217`): revs 214-217 (`-t -k -p`, DEBUG/RETURN traps, `$BASH_COMMAND`, `-T`, `declare -ft`); suite 114/114 on the AOT binary both modes; live check (ERR at 3: false, DEBUG, -t); rollback `dist/Bash.exe.rev213`; six sessions told. Before it, rev 213 (`1.0.0+hg.40a4296afbd6 213`): the
>   `BASH_COREUTILS=external` fix over rev 209. Suite 112/112 on that AOT binary, both modes; live
>   check (rev 213, `=external awk` is Git's GNU Awk, the shell survives). Rollback
>   `dist/Bash.exe.rev209`. One-fix notice to the six sessions (msp430-a2: its caution lifted).
> - hg: branch `default`; the CODE is at rev 220 (rev 217 deployed); working tree clean apart from the
>   `.url` below. **GitHub `main` = git `1ab0a25` = hg rev 214** (pushed 2026-10-11; before it `e8c2d39` = hg rev 211, after the
>   benchmark on deployed rev 209 and a doc review; `git ls-remote` confirmed). History files
>   regenerated (215 changesets; the bundle unbundled into an empty repo to rev 214 `d86411e95a77`).
>   Before: git `694da54` = rev 182, `738ec92` = rev 140. `git status` lists files as modified after a
>   push only for hg revs after 214: not stray work.
> - Previously DEPLOYED 2026-10-11: `dist/Bash.exe` was rev 209 (`--version`: `1.0.0+hg.4fa037d780c8 209`,
>   a clean tree): revs 180-209 (the architect: "fix --posix, then deploy, notify and publish"). Suite
>   111/111 on that exact AOT binary, with a console and `-Detached`; battery 226/237 against 225;
>   live-verified in a Claude Code Bash call (rev, awk functions, umask, jq -R, the lines before a
>   syntax error run, -r refused, eval status 2, `git -C /f/...`). Rollbacks: `dist/Bash.exe.rev179`
>   (the previous deploy), `.rev156`, `.rev178`, `.rev150`, `.rev143`, back to `.rev80`.
>   Notice sent 2026-10-11 to daq-d1, adc-framework-d8, msp430-a2 (+ jq -Rs is in), nupkg-28 (+ awk
>   getline < file), errorlogs-42, koliada-net-6a (+ `strings` deferred), leading with six behaviour
>   changes (one line at a time; eval 2; MSYS path args; PYTHONIOENCODING; carried bytes are
>   characters; -r refused / --posix notice). Delivered, unread at the time of writing.
>   Previously DEPLOYED 2026-10-10: rev 179 (revs 158-179).
> - Running sessions told: 2026-10-08 `df`; 2026-10-09 revs 146-150; 2026-10-10 revs 152-156 (to
>   daq-d1, adc-framework-d8, msp430-a2, nupkg-28, koliada-net-f7); 2026-10-10 revs 158-179 to daq-d1,
>   adc-framework-d8, msp430-a2, nupkg-28, errorlogs-42 and koliada-net-6a, leading with three
>   behaviour changes (`while read` loops, the error prefix, the stricter parser). nupkg-28 confirmed
>   its awk report fixed on rev 179; adc-framework-d8 re-tested clean (loops, stderr capture); neither
>   had a workaround to drop. The architect, 2026-10-10: "koliada-net
>   is a new session" (koliada-net-6a, koliada-net's successor; the architect: "why would they not?"):
>   koliada-net-f7's items handed over 2026-10-10 (the `env` fix, deployed; `strings`, awaiting).
> - Benchmark: `tests/bench/compare3.sh`, method frozen, every launch made and timed by
>   `tools/benchtimer` (DECISIONS 2026-10-06, both entries). 2026-10-10, quiet machine, rev 179: startup
>   C#Bash 0.0160 s / Git Bash 0.0157 / WSL 0.1098. A "+1.2 ms vs rev 156" from two single runs was
>   NOISE: an interleaved diagnostic put rev 179 0.44 ms faster (DECISIONS "the +1.2 ms startup …
>   was the benchmark's own noise"). That run's startup row was one sample, +-1.5 ms between runs.
>   **Since rev 184 the startup row is the median of 5 samples** (ratified by the architect;
>   DECISIONS "METHOD CHANGE: the benchmark's startup row is the median of 5 samples"; logic checked
>   with a fake timer, NOT yet run for real). The next push re-measures README with it. Before:
>   rev 140, C#Bash 0.0152 s /
>   Git Bash 0.0162 / WSL 0.1036. The README table is now the 2026-10-10 rev 179 run (pushed).
> - Other installs, not checked since: GUY-WINDOWS11 `C:/tools/bin/Bash.exe` was rev 60 (2026-09-08);
>   a copy went to the share at rev 76 (2026-09-12).
> - Untracked `The Windows Shell Tax.url` (a claude.ai artifact link, there since 2026-09-14): in
>   neither VCS, not ours, leave it.
> - In flight: see START HERE. Rev 209 is DEPLOYED (2026-10-11); hg rev 214 pushed to GitHub (git `1ab0a25`, 2026-10-11). daq-d1 and koliada-net-6a
>   never replied to the rev 179 deploy notice; the architect, 2026-10-10: both sessions show
>   "nothing needs action from me". Closed: do not chase them.
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
> **GREENLIT 2026-10-12 ("Yes"): the PRINTER plan, then COMPGEN, in that order** (DECISIONS 2026-10-12
> "RULING: Git Bash is a reference only as far as Claude is concerned" and the plan entry). Printer:
> P1 measure bash 5.1's format (rules + probes into DECISIONS); P2 `CommandPrinter` from the AST, words as
> written (case `command_printing` byte-identical to GNU); P3 wire into `declare -f`, `type`, `jobs`
> (compound jobs as bash; `eval "$(declare -f f)"` round-trips); P4 `run-n.sh` corpus mode comparing
> `declare -f` of the 945 corpus functions with GNU (ratchet; every difference listed with its cause);
> P5 no regression. Do NOT derive: a change to `$BASH_COMMAND` for compound commands (raise it).
> compgen: generating actions + -G -W -X -P -S + word filter + status; complete/compopt/-F/-C/-o/-u/-g/
> hostname/service/binding answer with the NotAvailable notice; `compgen -c` names follow Git Bash
> (Windows-specific form, per the ruling). Battery reference move (GNU for non-Windows probes): proposed,
> after these two, NOT greenlit.
>
> **AWAITING THE ARCHITECT (do NOT derive; Claude's positions are proposals, not rulings):**
> V. (2026-10-12) **VARIABLE-SCOPE PLAN: DONE V1-V4 (revs 246-250), cost accepted ("Yes to both"); a PERFORMANCE REVIEW is expected "shortly", the architect's to call** (the defect in OPEN DEFECTS, "MOST SERIOUS"):
>    V1 measure: `tests/cases/variable_scope.sh` from the 29 probe cases plus export/-p, assoc, subshells;
>    expected = GNU; criterion: written, and C#Bash's failing-line count recorded.
>    V2 model: in ShellEnvironment each frame maps a name to one variable (scalar, indexed or associative,
>    with its readonly/integer/exported attributes, or declared-unset); lookups innermost-out; an
>    assignment to a name no frame has goes to the GLOBAL frame; local / declare in a function to the
>    current frame; declare -g to the global; unset as bash (in the current scope it stays local and unset;
>    a callee unsetting a caller's local uncovers the outer one); public API kept; FUNCNAME's lazy refresh
>    and subshell snapshots kept. Criterion: `variable_scope` byte-identical to GNU, suite green both modes.
>    V3 callers: declare/local/typeset/export/readonly/read/mapfile/printf -v/getopts/for/arith/let/${:=}
>    through the model's rules (`local -i` evaluates, `local -r`/`-x` end with the function). Criterion: the
>    same, plus battery, -n corpus and printer corpus without regression.
>    V4 performance: `compare3.sh --build`, every row within noise (variable access is on every row's path);
>    a row beyond noise is measured in-process and brought to the architect before going on.
>    Do NOT derive: namerefs (`local -n`, not implemented: out of scope); shopts localvar_inherit /
>    localvar_unset (out of scope); deploying after V4 (the architect's word).
> 0. (2026-10-12; GREENLIT above, kept for the record) **Command re-printing (bash's print_cmd.c).** C#Bash has no re-printer: `jobs` shows a
>    compound job as its node type (`BraceGroup`), and `declare -f` / `type` print a function as written
>    where bash re-prints it (one command per line, 4-space indent; measured). Fork: (a) job text from
>    the source span (a few lines; exact for one-line commands in bash's own style), or (b) port
>    print_cmd.c once for all three. Claude's position: (b), with (a) only as a stopgap if (b) waits;
>    falsifier: no session depends on `declare -f`'s exact text (Claude Code's snapshot re-sources it,
>    and either form sources). Revs 219-226 are NOT deployed; rev 226 fixes `a && b &` in deployed 217.
> 1. (MSYS paths in arguments: DECIDED 2026-10-10 and built, rev 186.)
> 2. (UTF-8 for external programs: DECIDED 2026-10-10, rev 187: PYTHONIOENCODING only; HGENCODING
>    measured harmful, dropped. The old text follows for the record.) Piped `hg` and `python` write
>    cp1252; python crashes on `✓`.
>    - Claude's position: export `PYTHONIOENCODING=utf-8` and `HGENCODING=utf-8` at startup when
>      unset; not `PYTHONUTF8`, which also changes `open()`'s default for user scripts.
>    - koliada-net-dd withdrew its `PYTHONUTF8` suggestion and agrees with that position (peer
>      message 2026-10-06, which asked for its agreement to be recorded here).
>    - Doubt raised 2026-10-08 [recalled, not verified]: the 2026-10-06 position called both
>      variables stdio-only. `HGENCODING` probably also decodes commit messages, usernames and branch
>      names, including `hg commit -l` files. Test before building: a non-ASCII message via `-l` and a
>      non-ASCII filename, under both settings, in a scratch repo.
> 3. (awk scope requests: DECIDED 2026-10-10 and built, rev 193, with `getline [var] < file` added
>    on the architect's word; DECISIONS "SUBSET EXTENDED: awk user functions …". Recursion limit
>    ~900 deep on AOT, kept by #7's rule; revisit when a session hits it.)
> 4. (`umask`, `times`, `fc`: DECIDED 2026-10-10 as Claude proposed and built, rev 196; DECISIONS
>    "BUILT: `umask`, `times` and `fc` are builtins".)
> 5. (`jq -R` / `-Rs`: DECIDED 2026-10-10 as Claude proposed and built, rev 197; DECISIONS "BUILT:
>    `jq -R`". `strings` NOT built: revisit when a second session needs it.)
> 6. **One command at a time: DECIDED 2026-10-11, match bash** (DECISIONS "DECIDED: C#Bash runs
>    scripts one command at a time"; the architect: C#Bash must match Claude's expectations of bash; a
>    "safe" parse-first mode only ever opt-in: not now, `bash -n f && bash f` does it).
>    **PLAN, written 2026-10-11, RATIFIED by the architect 2026-10-11 ("ratified"); build P1-P5 in order, commit between, stop for forks:**
>    - Measured 2026-10-11: 20 MB after `exit 0` = bash 4 ms; C#Bash 1.3 s (base64) or FAILS (binary:
>      a here-doc warning, then a syntax error, nothing runs). `source` / `eval` also run nothing before
>      an error. Aliases expand at run time (`Evaluator.cs` ~1283): no parse dependency.
>    - Reframe tested: "parse all, run the parsed prefix on error" fails the payload cases.
>    - Recommended: tokens on demand + `Parser.ParseNext()` per top-level command (bash's model);
>      built as phase 1 then 2, so phase 1 alone is a usable fallback. Falsifier: the lazy lexer needs
>      parser state (heredocs, lookahead) — then stop after phase 1, payload cost documented.
>    - P1 DONE rev 201 (DECISIONS "BUILT (item 6, phase 1 of 5)"; the unit is the LINE). `ParseNext` + run each command as parsed (scripts, `-c`, stdin, `source`, `eval`); lexer
>      errors and warnings raised where reached. ACCEPT: new case vs bash: lines before an error run (the
>      rm/touch one too), multi-line `-c`, `source`, `eval` prefixes, a text payload after `exit`;
>      suite green both modes; `bash -n` still rejects the whole file.
>    - P2 DONE rev 202 (268 ms / 520 ms vs bash 4 ms: the whole-file read is left, out of scope). Lazy lexer. ACCEPT: the 20 MB base64 AND binary installers print `installing`, exit 0.
>    - P3 DONE rev 203 (multi-line body at top level: message line 1-2 early, measured, kept). `$( )` syntax error at run time: `name: command substitution: line N:`, that top-level
>      command stops (1), the next runs. ACCEPT: script and `-c` forms match bash.
>    - P4 STATUSES DONE rev 204: every eval syntax error is 2 (only `x=(` is 1; 14 of 15 kinds were 1).
>      P4 LINES DECIDED 2026-10-11: physical line (the architect). Was: the plan's "counts from where the eval
>      ENDS" was falsified (bash: line 7 → 5, line 13 → 11 inside one script; C#Bash prints the
>      physical line). Claude's position: keep the physical lines, document bash's drift; falsifier:
>      the architect wants bash's numbers, then read bash's source (evalstring.c) before building.
>    - P5 DONE rev 205 (rev 200 vs 204 AOT: every row within noise). `tests/bench/compare3.sh` unmodified before/after: no regression beyond its noise; battery
>      no regression.
>    - DO NOT DERIVE (out of scope): a script editing itself while it runs (bash re-reads the file);
>      reading the file in chunks; aliases that expand to syntax; the REPL (already per line).
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
> **FIXED AND DEPLOYED (2026-10-10, in rev 179; suite 101/101 both modes on the Release and the AOT build, battery 227/237 against baseline 226 (199 left it, rev 167; 187 joined, rev 168); 145 left it 2026-10-11 (eval status, Git Bash 4.4), no regression):**
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
> **AFTER THE REV 179 DEPLOY, NOT DEPLOYED (the architect, 2026-10-10: "your recommendation is good; go
> up to but not including the scrip question, explain that one to me when you get there" -- the
> AWAITING items 1-5 by Claude's recommendations, then STOP at 6 and explain it):**
> - rev 186: a Windows program's MSYS-form path arguments are converted (`/c/x` → `C:/x`, `/tmp/x`;
>   start of an argument, existing drive, not for MSYS programs; off with MSYS_NO_PATHCONV /
>   MSYS2_ARG_CONV_EXCL). DECISIONS "a Windows program's MSYS-form path arguments are converted";
>   case `native_args`. Suite 102/102 both modes, battery 227/237.
> - rev 187: PYTHONIOENCODING=utf-8 exported unless set (piped py crashed on `✓`); HGENCODING NOT set:
>   it makes `hg commit -m 'café'` abort on Windows. DECISIONS "UTF-8 for external programs = PYTHONIOENCODING
>   only"; case `python_utf8`.
> - rev 188: carried bytes that spell a UTF-8 character are that character (`$'\xc3\xa9'` had length 2,
>   failed `== é`, named files `caf??.txt` for hg). DECISIONS "carried bytes that spell a UTF-8 character
>   stayed bytes"; case `utf8_bytes`.
> - rev 193: awk user functions, `print`/`printf > file` / `>> file`, `getline [var] < file`, `close`,
>   `fflush`, 3-argument `match`, `--version`, as gawk 5.1. DECISIONS "SUBSET EXTENDED: awk user
>   functions …"; case `awk_extended`. Suite 105/105 both modes, Release and AOT; battery 227/237.
> - rev 194: awk `"\xNN"` / `"\NNN"` above 0x7F are bytes (`"\xc3\xa9"` wrote 4 bytes, `"\377"` wrote
>   `c3 bf`); bytes spelling a UTF-8 character are that character, across a concatenation too.
>   DECISIONS "awk string escapes above 0x7F made characters"; case `awk_escapes`. Suite 106/106.
> - rev 195: awk `-v name=value` and `name=value` operands take octal and `\x` escapes; an operand
>   with a backslash in its value is an assignment, not a file. DECISIONS "awk `-v name=value`
>   ignored octal …"; case `awk_escapes`. Suite 106/106.
> - rev 196: `umask` (stored and printed, no effect on files), `times`, `fc` (not supported) are
>   builtins; they were 127 and `fc` ran fc.exe. DECISIONS "BUILT: `umask`, `times` and `fc` …"; case
>   `umask_times_fc`. Suite 107/107 both modes; battery 227/237.
> - rev 197: `jq -R` / `--raw-input` and `-Rs`, as jq 1.6. DECISIONS "BUILT: `jq -R`"; case
>   `jq_raw_input`. Suite 108/108 both modes.
> - rev 198: jq short options after the filter (`jq . f -c`) were read as files; the open error now
>   says `Could not open file x` as jq 1.6. DECISIONS "jq read a short option after the filter …";
>   case `jq_messages`. Suite 108/108 both modes; battery 227/237.
>
> **OPEN DEFECTS (the architect, 2026-10-10: "go meant 'still open' gogogo": work through them in turn, commit between, stop for ambiguities/forks):**
> - (The three found 2026-10-10 while fixing the error prefix are all fixed: `trap BADSIG` rev 170,
>   `read -z`'s usage line rev 171 (re-checked against GNU 2026-10-10), syntax-error text rev 172.)
> - (The two jq slips found at rev 197 are fixed, rev 198.) A question, not a defect until ruled:
>   invalid UTF-8 passes through jq as bytes where jq 1.6 writes U+FFFD (shell byte transparency vs
>   jq fidelity). Claude's position: keep the bytes; falsifier: a session needing jq's exact output.
> - (FIXED rev 219, not deployed: `$0` was the launcher's backslash path; a PATHEXT match's argv[0] was
>   `bash.EXE`. DECISIONS "`$0` was the launcher's backslash path".)
> - (FIXED rev 220, not deployed: lookups follow the shell's PATH: hash reset on assignment, temporary
>   `PATH=` searched, a subshell's PATH kept in it. DECISIONS "command lookup ignored the shell's PATH".)
> - (FIXED rev 221, not deployed: a `case` inside `$( )`, `"$( )"` or backquotes ended the substitution at
>   its first pattern's `)`. DECISIONS "a `case` inside `$( )` ended the substitution".)
> - (The 2026-10-10 `builtin_options` "Not done" list is all FIXED, not deployed: `kill -l`/`trap -l`'s
>   numbered table rev 222, with `kill`'s option loop and `SIG`-prefixed names (DECISIONS "`kill -l` /
>   `trap -l` printed nine names"); the directory stack's `+N`/`-N` rev 223, with `-n` = no cd, `dirs
>   -p`/`-l`, `~` boundary (DECISIONS "pushd / popd / dirs refused `+N` / `-N`"; found, not raised: no
>   `DIRSTACK` variable); `jobs -x` rev 224, `$$` in a script as bash (DECISIONS "`jobs -x` was refused").)
> - (FIXED rev 225, not deployed: jobspecs `%name`, `%?text`, bare `%`. DECISIONS "jobspecs `%name` …".)
> - (FIXED rev 226, not deployed: `a && b &` backgrounded only `b`; in every build incl. deployed rev 217.
>   DECISIONS "`a && b &` backgrounded only `b`".)
> - (FIXED rev 228, not deployed: IFS='' still split; non-white IFS lost empty fields; `$*` joined with a
>   space. Found verifying daq-d1's workaround. DECISIONS "field splitting and `$*` ignored what IFS held".)
> - **Found 2026-10-12 (measured, every build incl. deployed rev 233), MOST SERIOUS: the variable model
>   does not follow bash's scoping.** (1) A NEW variable created in a function is lost on return
>   (`f() { x=1; }; f; echo $x` prints nothing; also read, for, printf -v, getopts, (( )), let, ${x:=}, +=):
>   `Set` writes an undeclared name into the innermost frame, bash into the global scope. (2) Arrays are not
>   local at all (shell-wide `_arrays`/`_assocs`). (3) Attributes are shell-wide: `local -r` stays readonly
>   after return (the script then aborts on a later assignment), `local -i` does no arithmetic. The
>   architect: "Yes, go" (2026-10-12) = plan this fix next, ratify before building. Probes:
>   scratchpad locarr/probe.sh, probe2.sh (29 cases, GNU vs C#Bash). PLAN below, AWAITING ratification.
> - (FIXED rev 244, not deployed, adc-framework-d8: `diff -u` ignored `--label`. DECISIONS "`diff -u` ignored")
> - (FIXED rev 254, not deployed, msp430-a2 DATA LOSS: awk with `getline` went to Git's gawk and lost CR. DECISIONS "awk program containing `getline`".) WAS: an awk program that merely
>   CONTAINS main-input `getline` strips CR from every line (a CRLF file committed as LF). Code: the
>   in-process awk refuses main-input getline at parse time (Builtins.Awk.cs:977) and the program falls
>   through to Git's gawk, which strips CR in text mode (msp430-a2 MEASURED: `gawk '{print}'` 0 CR,
>   `gawk -v BINMODE=3` 3 CR). Fix: implement `getline` / `getline var` on the main input; pass
>   BINMODE=3 when awk does fall through to gawk. NEXT after V4.
> - QUEUED 2026-10-12 (the architect: "We just had a second session try to use the strings utility - add it
>   to the queue?"): `strings` (binutils). Deferred 2026-10-10 until a second session needed it; that
>   condition is now met. Order: after the two msp430-a2 data-loss fixes, before compgen.
>   BUILT rev 264 (scope RATIFIED 2026-10-14, "ratified"; DECISIONS "BUILT: `strings`"). The scope: an in-process `strings` as GNU
>   strings 2.38 (WSL): files or stdin, whole file, min 4, `-e s` default; `-n`/`--bytes`, `-t o|d|x`/`-o`,
>   `-f`, `-w`, `-s`, `-e s|S|b|l|B|L`, `--help`, `--version`; `-d`, `-T`, `-U`/`--unicode`, `@file` answer
>   with the NotAvailable notice. Evidence: koliada-net hit it inspecting a .NET exe (2026-10-10); the
>   option list in the compacted summary was Claude's own answer to "what does strings do", never ratified.
> - (FIXED rev 255, not deployed, msp430-a2: arguments to MSYS programs re-parsed. DECISIONS "arguments to MSYS programs".) WAS: an argument with braces and no
>   spaces loses them when C#Bash runs an MSYS program (`gawk '{print}'` arrives as `print`):
>   ChildLauncher.AppendArgument quotes only on whitespace or `"`, and the MSYS runtime globs/brace-
>   expands an unquoted word (a `'` is likely mangled too). Fix: double-quote arguments with `{ } * ? [ '`.
>   NEXT after V4, with the awk fix.
> - (FIXED rev 257, not deployed, adc-framework-d8: `rm` refused a ReadOnly file; `-i`/`-I` never asked;
>   DATA LOSS in every deployed build: `rm -r` on a junction or directory symlink deleted the TARGET's
>   contents. DECISIONS "rm refused a read-only file". Reply to adc-framework-d8 sent with the fix.)
> - (FIXED rev 256, not deployed, found testing rm: sed added a missing final newline; `D` autoprinted, so
>   `N;P;D` doubled lines in every deployed build. DECISIONS "sed added a missing final newline".)
> - (FIXED rev 261, not deployed: `cmd //c` as Git Bash, `ShellEnvironment.DoubleSlash`. DECISIONS 2026-10-14
>   "`cmd //c` started an interactive cmd".) WAS: found 2026-10-12: `cmd //c echo hi` starts an INTERACTIVE cmd under C#Bash
>   (cmd receives `//c`; banner and prompt, then waits on stdin; a Claude Code call would hang to its
>   timeout); Git Bash prints `hi` (the MSYS runtime turns a leading `//x` into `/x` for a native program).
>   A common idiom of Claude's on Windows. GREENLIT 2026-10-13 ("2/ yes"): fix next, before `strings`.
>   MEASURED Git Bash rule (native program, `py` printing argv), to build exactly: an argument STARTING
>   `//` loses one leading `/` and its `\` become `/` (`//c`→`/c`, `//a\b\c`→`/a/b/c`, `///x`→`//x`,
>   `//`→`/`, `//c=x`→`/c=x`); exactly two slashes with a later `/` is UNC, kept (`//srv/share/f`,
>   `//c/x`; `//a\b/c`→`//a/b/c`); any of `* ? [ " '` leaves it alone; `MSYS_NO_PATHCONV` /
>   `MSYS2_ARG_CONV_EXCL` switch it off. Mid-argument (`-o//x`, Git Bash `-o/x`) stays untouched per
>   DECISIONS 2026-10-10 "never inside an argument". Consequence stated to the architect: `//host\share`
>   becomes `/host/share`, as in Git Bash. Probes: scratchpad slash.sh, slash2.sh, slash3.sh.
> - **compgen BUILT rev 265 (not deployed; DECISIONS "BUILT: `compgen`"). DEFERRED enhancement: the PATH scan for `compgen -c`. FORK DECIDED 2026-10-15 (the architect: "C#Bash has no formal
>   bin dir ... C#Bash is really _only_ about its builtins ... a list of externally available linux commands (from
>   the path) ... I would defer that as a possible enhancement"): compgen describes THIS shell; `-b` = every C#Bash
>   builtin; `-c` = aliases, keywords, functions, builtins, NO PATH scan (DEFERRED enhancement); helptopic = the
>   notice.** The question as it was put: does compgen describe THIS shell or bash's lists? (1) `-b` / `-A builtin` / `enabled`: bash 5.1's builtins that
>   C#Bash has (51; GNU-like) or every C#Bash builtin incl. the in-process tools (`type -t cat` says builtin here,
>   and the interactive CompletionEngine offers them). (2) `-c` PATH entries: Git Bash's rule (`.exe`/`.com` and
>   `#!` scripts) or what C#Bash runs (PATHEXT incl. `.cmd`/`.bat`, plus `#!` scripts); on-disk names either way
>   (greenlit). (3) `-A helptopic`: C#Bash has no `help`: the notice, or bash's 78 topics. Claude's position:
>   THIS shell for all three. Done meanwhile: tests/cases/compgen.sh (GNU-expected, fork-free parts).
> - Reported 2026-10-12 by daq-d1 (verified): `compgen` is not a builtin (nor `complete`, `compopt`), in
>   every build. daq-d1 runs on a verified workaround (`for f in "$1"/$2; do [ -e "$f" ] …`), needs
>   nothing more. (SETTLED 2026-10-12: the architect's "Yes" GREENLIT this position, see the GREENLIT
>   "PRINTER plan, then COMPGEN" block; the text below is the proposal as it stood.) Scope: compgen's generating actions
>   (-G -W -f -d -c -b -k -a -v -e -A …, -X -P -S, word prefix) vs programmable completion
>   (complete/compopt, -F/-C). Claude's position: build compgen's generating actions now; `complete`,
>   `compopt`, `-F`, `-C` answer with the NotAvailable notice (2026-10-11 pattern). Falsifier: a session
>   that registers completions (`complete -F`), which only an interactive shell uses.
> - Found 2026-10-12 (measured, not raised): bash keeps finished jobs in its table until it reports them
>   (`jobs` lists them as Done, job numbers keep counting); C#Bash's `wait` removes them.
> - (FIXED rev 241, not deployed: a compound job's text was its node type; `jobs` now prints as bash.
>   PRINTER P2 rev 240: `declare -f` / `type` as bash. DECISIONS "PRINTER P2", "PRINTER P3".)
> - (FIXED rev 266, not deployed: git's credential prompt HUNG under Claude Code: `/dev/tty` in a windowless console. DECISIONS 2026-10-15 "git's credential prompt hung". SSH_ASKPASS: DECIDED no, 2026-10-16, "2/ No".)
> - (FIXED and DEPLOYED rev 213: `BASH_COREUTILS=external` killed the shell with a stack overflow.)
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
> `F:/Tools/PortableGit/usr/bin/_bash.exe`; the `bin/` stub is broken by the rename), and
> `System32\bash.exe` (WSL) was moved away, both so nothing pre-empts C#Bash. Reach WSL with
> `wsl.exe -e bash`. Run Git Bash scripts as `"$BASH" script` from `_bash.exe -l`. Claude Code sets
> `NoDefaultCurrentDirectoryInExePath=1`.
>
> **For the architect's own files (suggested only):** the global CLAUDE.md's launcher line is
> obsolete since the rename (2026-10-04 scan), and its "Known limits of C#Bash" says UNC globs do
> not expand, fixed in rev 58 [recorded, not re-verified]. Its awk 3-argument `match()` line is true
> of the deployed rev 179 and stale from rev 193 on: tell the architect when rev 193+ is deployed. Its
> `df` line was corrected on 2026-10-08 at the architect's request ("You should fix it").

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
