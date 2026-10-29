# Project Context: Bash

> ## RESUME ANCHOR (rewritten 2026-10-18) — read this before anything else
> Replaces the 2026-10-16 head, which had filled with resolved items; that text is in hg rev 316's
> `PROJECT_CONTEXT.md`, and every item in it is resolved with a DECISIONS entry or carried below. The tail (KNOWN
> LIMITATIONS, SETTLED, DO NOT undo, machine facts) is kept.
>
> **2026-10-27 (session 988ee3e2) — RESUME HERE** (rewritten when the greenlit list finished; the list's working notes
> are in hg rev 424's PROJECT_CONTEXT.md). It SUPERSEDES the TRUE STATE block below wherever they differ.
> - **TRUE STATE:** hg tip = rev 450 (this anchor + DECISIONS "DEPLOYED rev 448 ... PUBLISHED ... hg rev 449").
>   **PUBLISHED: GitHub main = git 2876ef9 = hg rev 449** (2026-10-28; compare3 on deployed 448: pipeline 0.080 s, 1.3x
>   WSL, no row loses). **DEPLOYED rev 448** (`1.0.0+hg.a65944c9ec7c 448`, AOT suite 198/0 both modes, battery 216 =
>   baseline; rollback dist/Bash.exe.rev445; 445 = step (c), 442 = step (b), 437 = (g2), 435 = (g1), 433 = step (a)),
>   NOTIFIED. Two untracked files in the root are not ours: BENCHMARK-PROVENANCE.md, "The Windows Shell Tax.url"; leave
>   them. The help check stopped at 41/87 (curl 000 from gnu.org) under rev 440's script: NO cache was kept
>   (DECISIONS "CORRECTION: the help check's first run left NO cache"), so a rerun fetches all 87 (~1 h, unmeasured).
>   GREENLIT 2026-10-28 ("ok, go"): IN BATCHES, `HELP_CHECK_PAGES=15` a run (rev 452), one run every few hours, the
>   first late on 2026-10-28; the cache carries each run on. If a batch of 15 is refused too: STOP, back to the architect.
>   Batch 1 (16:17-16:31): 15 fetched, no refusal; 14 answered 200; FAIL 404 `xargs` -> findutils `.../find_html/Invoking-xargs.html`
>   (WSL's find.info has node "Invoking xargs": the PATH is wrong, not the name; `find`'s Invoking-find.html, same folder, is
>   not fetched yet: it tells whether the folder is wrong). A 404 is not cached, so each batch fetches it again. Do NOT
>   guess a new URL; fix help.json from what the batches show, after the architect's word (a code change + deploy).
>   Batch 2 (19:47-19:58): 15 fetched, no refusal, 0 FAIL; 29 of 87 pages cached, their 53 URLs pass (anchors too);
>   87 URLs on the 58 pages left not checked yet (xargs's not refetched this time). Batch 3 scheduled 22:53.
>   Batch 3 (22:53-23:04): 15 fetched, no refusal, 0 FAIL; 44 of 87 cached; 66 URLs on 43 pages left. It got
>   Job-Control-Builtins.html, the page refused (000) at 10:24 this morning: so not that page. Batch 4 scheduled 02:03.
>   Batch 4 (2026-10-29 02:03-02:04): STOPPED at its FIRST page, 000 (no answer) from sha1sum-invocation.html, rc 3;
>   nothing fetched; still 44 of 87 cached, 66 URLs on 43 pages left. NOTHING SCHEDULED. Cause NOT concluded: a 000 at
>   the first page fits the VPN being off (the architect, 2026-10-28: without it gnu.org does not answer) as well as a
>   block; batches 2-3 (30 pages, 3 h apart) were not refused. The architect: VPN live (not browser-based), "try again?".
>   Batch 4 retry (03:32-03:44): 15 fetched, no refusal, 0 FAIL, sha1sum-invocation.html among them: the 02:03 000 did not
>   repeat (cause unknown; twice now a single 000 that a later run did not see). 59 of 87 cached; 31 URLs on 28 pages
>   left. curl declares Chrome 129 / Windows (UA + Accept only; Schannel TLS): unchanged, it is getting through. Batch 5
>   scheduled 06:47. Rev 459 (the architect: "ok, sounds good"): a no-answer (000) is retried ONCE after 30-90 s; a stop
>   names curl's exit code. AWAITING THE ARCHITECT: his "this should become a real tool for generic use locally?" (a
>   paced, polite fetcher any local session can use); Claude's position and questions in DECISIONS "no answer retried once".
>   2026-10-29 04:4x, the architect: "switch to the packages" + "Go on the plan": batch 5 CANCELLED; coreutils' index
>   lists coreutils.html_node.tar.gz (1 request, 04:41; then the archive, 04:42: all 55 coreutils links + anchors pass
>   offline). Rev 460: the check makes ONE download per GNU manual (~16 downloads in all, not 87 pages), tested locally.
>   The real run WAITS for the architect's word that the VPN is back on (he turned it off at ~04:45 to read his ISP
>   address for the gnu.org email; draft in the session: VPN paragraph left out, his call). VPN back ~04:55.
>   Run (04:56-05:04, 9 downloads): 140/140 passed, BUT a false pass: xargs (404 online in batch 1) passed against the
>   findutils archive. findutils' index (05:05) links `html_node/index.html` (pages flat, NO find_html/ folder) and
>   `find.html_node.tar.gz`. Rev 461: the archive name is READ FROM THE INDEX (kept), a page must be at its own path in
>   the archive. Re-run (05:09-05:11, 2 downloads): 138 pass, FAIL find + xargs (`html_node/find_html/...`: not in the
>   manual). The architect: "yes" -> rev 462: help.json's find + xargs = `.../findutils/manual/html_node/Invoking-find.html`
>   and `.../Invoking-xargs.html`, in Bash/help.json AND this machine's `~/.bash/help.json` (byte-identical now; a
>   written file answers for every name it has, Builtins.Help.cs:7-8, so OTHER machines' files keep the old two).
>   Check from the cache: 140/140, 0 downloads. **DEPLOYED rev 462** (`1.0.0+hg.c26ff088761b 462`, suite 198/0 both
>   modes, battery 216 = baseline; rollback dist/Bash.exe.rev448), NOTIFIED (koliada-net-28, kes-term-a1).
>   AWAITING THE ARCHITECT (raised 2026-10-29): an INSTALLER for 1.x. Ruled 2026-10-20 (DECISIONS: InstallGen, per-user,
>   everything under ~/.bash, ~/.bash/bin/Bash.exe, Authenticode on traction) but as phase 5 of the 2.0 plan, now
>   PortableBash's. RULED 2026-10-29: C#Bash owns ITS installer; InstallGen = E:/Claude/Installer/release (DECISIONS
>   "RULED: C#Bash owns its installer": help.json NOT payload; a plan to ratify first). RULED 2026-10-29: InstallGen
>   learns to REPLACE A FILE IN USE (requested: Installer REGISTER INST-Q-046, its rev 26, with measurements and the
>   trick); C#Bash's installer is BLOCKED on it: plan `docs/installer-plan.md`, not ratified, KEEP IT CURRENT.
>   AWAITING THE ARCHITECT: phases 1 (release asset) and 2 (help.json refresh in Bash.exe) do not need InstallGen:
>   do they wait too? INST-Q-046 now carries his refinement (Installer rev 27): set-aside files go to ONE `.deleted`
>   folder on the target's drive (a running EXE moves within a drive; across drives it is COPIED and the move says
>   success), swept at the start of every installer run.
>   GREENLIT 2026-10-29 ("fix mv first and then P1 & 2 of the plan"): (1) mv FIXED rev 467 (DECISIONS "FIXED: `mv` to
>   another drive"; NOT deployed: deploy only on his word); (2) P1 release asset: tools/release.sh + publish.sh step 6
>   (dry-run tested; the real test is the next publish); (3) P2 help.json refresh in Bash.exe: BUILT rev 469 (an
>   unedited earlier built-in table is rewritten when help reads it; NOT deployed). INVARIANT: when Bash/help.json
>   changes, add the outgoing table's SHA-256 to `EarlierHelpTables` (Builtins.Help.cs). Claude's proposal, NOT
>   built (deploy.sh is a contract tool): deploy.sh refuses when help.json changed since the deployed rev and the
>   outgoing hash is missing. **DEPLOYED rev 472** (suite 200/0 both modes, battery 216; rollback
>   dist/Bash.exe.rev462). **PUBLISHED 2026-10-29: GitHub main = git ec718e1 = hg rev 473, and the FIRST RELEASE
>   hg-473** (Bash.exe build 472 + SHA-256; checked through the README's latest link). Plan phases 1-2 DONE; 3-5
>   BLOCKED on Installer INST-Q-046.
>   README RESTRUCTURE (2026-10-29): Claude's draft, edited by the architect, Architecture kept in the README at his
>   word; committed and published (DECISIONS "the README is the highlights"; "Documentation set" below).
>   LOGGED, not fixed (found 2026-10-29): `mv file nodir/` renames to `nodir`, rc 0 (GNU: Not a directory, rc 1);
>   `exec 4< <(PROGRAM)` may run PROGRAM to completion first [inferred, not investigated].
>   Claude's position: first a GitHub Release asset per publish (Bash.exe + SHA-256), then the installer (rename the
>   running exe aside; write the claude.cmd start file, not a global CLAUDE_CODE_GIT_BASH_PATH: #94077; never
>   overwrite the user's help.json; uninstall).
>   Batches are one-shot CronCreate jobs in THIS session only (gone if it ends): a new session re-schedules by hand. NOTHING GREENLIT IS LEFT: the next step is the architect's steer.
> - **Scope (the architect):** PortableBash is a separate session; here C#Bash user support only.
> - **DONE this session (DECISIONS 2026-10-27 has each, with measurements; do NOT re-open):** before 404: tail/head over
>   2 GiB, help/--help -> GNU pages (help.json), enable, the unserviced log, wc + checksums, THE SHARED BYTE LAYER
>   (Builtins.Bytes.cs) with cmp/od/hexdump/base64/split/tac on it. The greenlit list, each its own commit + deploy:
>   406 split's suffix widens/stops (was DATA LOSS); 408 the output encoder carries surrogate pairs (CarryEncoder;
>   emoji broke past 8192 chars); 410 sed streams (SedInput/SedOut/SedShared; GNU's per-line newline flags; q ends the
>   run; w files once; -i = temp file + File.Replace); 412 uniq (coreutils' -d/-u/-D); 414 comm (--total, -z, the order
>   check); 416 paste (`paste - -`); 418 tail -f (it DUPLICATED a busy writer's lines); 420 tee (here-doc pairs; 8 KiB
>   pipe reads, measured); 422 fold + strings pooled (tr kept, measured); 424 head -n N's missing newline (shared line
>   reader kept: plan revised, measured). Shared helpers added: ShellEncoding.ReadRecord / LfReader.ReadRecord.
> - **Deliberate differences from GNU (stated in DECISIONS; do not "fix"):** on a PIPE, sed q / head -n leave the next
>   command exactly the next line (GNU: whatever its block read left); sed `$F` after a lookahead names the line's
>   file; comm -i kept (C#Bash's own); `tee /dev/stdout` with stdout a file writes two copies (Linux reopens it).
> - **DONE (was GREENLIT 2026-10-28; (1) rev 441, (2) 442, (3) 445 + 447, published hg 449) (the architect: "I'd do 1 first ... then 2.'; Yes and 3 then publish?")**, in order, each its own
>   test + commit + deploy (`bash tools/deploy.sh < /dev/null`) + notice: (1) the runner gives each case stdin NUL in
>   console mode too (item (h)); (2) buffering step (b): pipeline-stage pipes; (3) step (c): C#Bash's own stdout when not
>   a console (flush before any child inheriting it); then PUBLISH (compare3 on the deployed build when ListAgents shows
>   the peers idle, or after asking them for a clear slot; never ask the architect; memory publish-at-end-of-run).
> - **AWAITING THE ARCHITECT (do NOT start; each with Claude's position).** The architect, 2026-10-28: "Lets do 2 & 3 and then 6" = (c) the output buffering (design talk first), (d) the pipe read size (measure), then (g) the two held bugs.
>   (a) PUBLISH: DONE 2026-10-28 (git 9a5a95c = hg 426; DECISIONS "PUBLISHED ... hg rev 426"; a clean benchmark, the
>   architect confirms). Three expected files are CRLF in hg (fieldutils, path_lookup, unserviced_log): harmless, the runner
>   ignores line endings (DECISIONS "CORRECTION ... CR expected files"); converted to LF in hg, rev 429 (no push).
>   (b) the ruled, unstarted **"match GNU" item**: wc -w printable runs, -m invalid bytes, -L display width, padding;
>   cmp's EOF text and -l layout; od -j addresses; base64 -w 0's newline, -d dropping spaces/CR, -i ignored; printf %q
>   escaping '='; sed `l` not wrapped at 70; split -l/-b x|0|-1|'' accepted (one piece, rc 0; -b x falls through to the
>   PATH split); `Opts.GetInt` takes a bad value as the default (other tools unsurveyed); `$( )` keeps NULs (bash drops
>   them); sort + comm order is UTF-16 units, GNU's C locale bytes (change BOTH or neither).
>   (c) AGREED + GO 2026-10-28 (DECISIONS "buffered output to redirected files"): step (a) file redirects BUILT + DEPLOYED rev 433 (sed 20 MB to a file 5.8x faster; DECISIONS "BUILT step (a)"); step (b) pipes BUILT + DEPLOYED rev 442 (2x; DECISIONS "BUILT step (b)"); step (c) own stdout BUILT + DEPLOYED rev 445 with the stderr-never-buffered fix (433-442 wrote a byte tool's error late through 2>&1); then (b) pipes, (c) own stdout. Was RAISED: line output to a redirected FILE is ~5.8 MB/s vs ~23 MB/s to a pipe (the shell's file writer flushes
>   each write; every line tool; uniq -D in-process is slower than the PATH uniq.exe was for this reason). Position:
>   buffer non-terminal output and flush at command end; needs a design talk (interleaving with children).
>   (d) DONE 2026-10-28, measured, no change (DECISIONS "cat, wc and the checksums keep 64 KiB reads"): 8 KiB is slower for them. Was: the byte tools read whole 64 KiB blocks from pipes; tee measured 8 KiB reads 22%
>   faster on `cat f | tee g > h`. Position: measure cat/wc at 8 KiB before changing anything.
>   (e) xargs streaming (reads all input first; its own change). (f) `bash tools/check-help-urls.sh`: gnu.org SILENTLY BLOCKS it (most likely bot detection; the architect, 2026-10-28: his to find a way round; not ours). Was: once gnu.org
>   answers.
>   (g) item 6: (g1) BUILT + DEPLOYED rev 435 (DECISIONS "`<<` inside an unquoted `$(( ))`"); (g2) sed BUILT + DEPLOYED rev 437 (DECISIONS "sed's empty regex"). Item 6 DONE. Findings (GNU vs 424, measured 2026-10-28): (g1) an UNQUOTED
>   `$(( a << b ))` (assignment, argument, declare; with or without parentheses) computes right but makes the NEXT line
>   a syntax error; quoted "$(( ))" and `>>` are fine (the lexer queues a HeredocPending for `<<` inside an unquoted
>   `$(( ))`: Lexer.cs, line 8's rule). (g2) sed's empty regex is not implemented: every `s//x/`, `/re/s//x/` fails "no
>   previous regular expression"; GNU uses the last regex EXECUTED (address or s, dynamic, kept across files), and only
>   with none fails so, rc 1. Also found: `(( y = (y) <<= 1 )) 2>/dev/null` prints its error anyway (parse time) and rc 2
>   (GNU: silent, rc 1) -> the "match GNU" item.
>   (h) DONE rev 441 (DECISIONS "the console-mode test runner gives each case an empty, closed stdin"). Was RAISED: the console-mode suite hands each case
>   the runner's own stdin; a run whose stdin never ends hangs on a case that reads it (awk.sh: 37 min). Position: NUL
>   stdin per case in console mode (contract runner: his call); meanwhile start the suite and deploys `< /dev/null`.
>   Also unexplained: in this session's shell a child inside `$( )` saw a stdin that never ended.
> - **NOT moving (stated; do not re-litigate):** sort, diff, jq, mapfile (need all input).
> - **How tests were made here (keep doing it):** GNU expected via `wsl.exe -e bash /mnt/f/.../tests/cases/X.sh
>   < /dev/null 2>/dev/null > tests/expected/X.out`; C#Bash run as the runner runs it (`BASH_UNSERVICED_LOG=/dev/null
>   $exe 'cases\X.sh'` from tests/); 2 GiB inputs as SPARSE files (fsutil / truncate), never real ones in the suite;
>   a guard case proven to fail once by a deliberate mutation; `hg add` new cases; check the case NAME is free first.
>
> **TRUE STATE (rewritten 2026-10-20, end of session f1b9f797, context nearly exhausted):**
> - **hg tip = rev 394 (this anchor), working tree clean. DEPLOYED rev 373** (`1.0.0+hg.ad15d9441fb0 373`, rollback
>   `dist/Bash.exe.rev324`) **and NOTIFIED** (the architect's "yes": all nine sessions, guy-85 included).
>   **PUBLISHED: GitHub csharpBash main = git `0a8eef7` = hg rev 366**; revs 367-394 NOT published (the deploy.sh and
>   test fixes, the docs on WSL / Git / MSYS, the module decisions, the PortableBash move): publish only on his word.
>   This session's C#Bash work: deploy.sh no longer needs cygpath (369); the tests no longer need Git (373: a case whose
>   program is missing prints `SKIP: <reason>`, the runner names it; battery baseline 216); README "What the build and
>   the tests use" and IMPLEMENTATION §11 Tooling (375). DECISIONS 2026-10-20 has each.
> - **THE MODULE WORK MOVED TO PortableBash (F:/Koliada/Tools/PortableBash; read ITS anchor for it).** Here, what stays:
>   C#Bash becomes a Windows HOST of the module contract on its hg branch `2.0` (PortableBash's plan step 4; not
>   started; waits for the architect's ratification of PortableBash's plan and spec), and legacy once xpBash (the
>   PortableBash shell) exists. docs/module-contract.md, docs/module-spec.md and docs/v2-plan.md here are FROZEN copies
>   (rev 391), kept because DECISIONS entries cite them; the live ones are PortableBash's.
>   Outside the repo: the global
>   `C:/Users/guy/.claude/CLAUDE.md`'s C#Bash `hash -r` line (now "after installing any tool"), committed in its git
>   repository as `65ecb3b` (the architect allowed git there, 2026-10-19; three other changes in that repository are
>   not ours, left alone). Untracked, leave both: `BENCHMARK-PROVENANCE.md` (not to be committed),
>   `The Windows Shell Tax.url` (not ours).
> - DONE 2026-10-19 before the red team (the architect directing each step): the benchmark's provenance (README
>   "Benchmark provenance", rev 333); rev 335 (Git reached only by compare3 and publish.sh: the battery against WSL's
>   GNU bash, the `-n` corpus and the df check run in WSL's bash); outside the repo: the no-windows hook's false
>   positives (`~/.claude/hooks/no-windows`, its own hg rev 4; RULED "hook - keep it": an exe path handed to a SCRIPT
>   stays flagged, `headless-exe` vouches) and the user Path's trailing `;` (removed; the old value is in the old
>   session's scratchpad, `userpath-before.txt`). DECISIONS has each.
> - **THE RED TEAM ON REVS 307-316** (DECISIONS 2026-10-19 "RED TEAM (revs 307-316…)": findings, repros; probe scripts
>   in the old session's scratchpad `F:/TEMP/claude/F--Koliada-Tools-bash/a6218fb2-86a7-48c3-bedd-2cb7a1a89778/scratchpad/redteam/work/`).
>   RULED (the architect): "work through the HIGH list (in your order), then the rest. each fix and commit in turn; go".
>   DONE, one commit and one suite case each (do NOT re-open; DECISIONS has each): HIGH F4 `.bat` arguments 337, F5
>   quoted `~` 338, F1 a subshell's `n>&-` 339, F2 `read -u` read-ahead 340, concurrent `>>` 342, F3 MSYS `>> f 2>> f`
>   343, `find` falling back to find.exe 344; MED F6 readlink/realpath 346, F7 find -execdir 347, F8 which 348, F9
>   env -S and `#!` options 349, F10 cwd of 259 350, source devices 351, export -f 352, `$((x))` with x='1+' 353; LOW
>   F13 path through a file 355, F16 mktemp suffix 356, F11 set -x details 358, F12 set -x compound 360. Also: 341
>   (case files 337-340 had missed `hg add`), 357 (flaky `traps_debug_return`: CPU-time figures), 359 (sed: a `[` in
>   a replacement swallowed the delimiter; found beside F11).
> - **REMAINING of that list (greenlit as "the rest", but the architect PAUSED at "1/" on 2026-10-19 and will steer
>   after the restart: do NOT resume without that steer):** F14 `hash -t`/`-d` against a relative PATH entry; F15 a
>   directory as `#!` interpreter ("No such file", GNU "Permission denied"); the older LOWs: `hash -p /abs name` not
>   obeyed; `{fd}>` and `4>&3-` unsupported; a text file named `.exe` gives 127 with a literal `%1` (bash 126).
> - **AWAITING THE ARCHITECT (do NOT derive, do NOT start):**
>   (a) **the `awk` suite HANG** (DECISIONS rev 360 entry, "HANG, OPEN"): one console-mode suite run hung ~2.5 h on
>   `cases\awk.sh` (no child process); 80 direct runs (this build, 324) never hung. Put to the architect, who took only
>   "1/" (commit F12). Open: a console-mode per-case timeout in run-tests.ps1 (a contract tool: the architect's call);
>   a reproduction loop under the runner's conditions with a stack dump (dotnet-dump is not installed: ask first).
>   (b) **arithmetic error parity** (DECISIONS rev 353 entry): the message wording, GNU abandoning the rest of the line,
>   `(( 1+ ))` 2 here against 1, errors escaping a redirected subshell or group. Claude: fix as one item.
>   (c) DONE 2026-10-20 (rev 373, deployed): the Git-dependent tests (DECISIONS "FIXED: the tests no longer need Git").
>   (d) **Utility modules: see PortableBash** (F:/Koliada/Tools/PortableBash, its anchor). In short: RULED here on
>   2026-10-20 and carried there (DECISIONS here up to "RULED: PortableBash"; there, imported verbatim and continued):
>   PortableBash = the contract (`csb_` names) + Zig modules + xpBash (the shell, executable `bash`; once "zigBash");
>   C#Bash a Windows host, legacy once xpBash exists; built-ins first, modules after; MSYS a bridge module; runtime
>   sharing PARKED (the architect's alternate-runtime project). C#Bash's part, NOT started: the host on branch `2.0`,
>   after PortableBash's design record, spec and architecture are ratified, together (2026-10-20: the first two rebuilt
>   and an initial architecture drafted in PortableBash's working tree; ruled there: PortableBash's own standard
>   library, one shared runtime in Zig; PortableBash's anchor has the state).
>   (e) Raised, no position: should C#Bash answer to a bare `sh` (POSIX mode is not available)?
>   (f) Raised: the battery in deploy.sh tests the managed Release build, not the shipped AOT one; Claude: give
>   run-compat.ps1 `-Interpreter` and run it on the staged binary (falsifier in DECISIONS "DEPLOY REFUSED (rev 369)").
>   NOT MSYS in C#Bash, NOT msys64 as a test reference (the architect, 2026-10-20; do NOT re-propose).
>   (g) Found 2026-10-20: `tests/bench/claudecall.sh` (a contract tool) still needs `cygpath` (Git's), so it runs only
>   from Git Bash now; deploy.sh's was removed (rev 369). Documented (README, IMPLEMENTATION §11); not changed.
> - **Verification, as done for every fix (keep it):** a case, red on deployed 373, GNU bash via WSL as the expected
>   where possible; check the case NAME IS FREE first (a Write overwrote `xtrace_compound.sh` once; restored) and `hg
>   add` it; `dotnet build -c Release Bash`; suite `powershell.exe -NoProfile -ExecutionPolicy Bypass -File
>   tests/run-tests.ps1 -NoBuild` and again with `-Detached`; battery `tests/compat/run-compat.ps1 -NoBuild`; a DECISIONS
>   entry; commit. Expected now: suite 175 passed, 0 failed, 9 skipped (8 for a missing program, icu_lazy AOT only) of
>   184 on the Release build (176 passed, 8 skipped on AOT); battery 216/237, baseline 216.
> - **After the restart, check first:** `type -a bash` names dist/Bash.exe first and no PortableGit entry is on PATH.
>   The old session began before Git left the Windows PATH: its bare `bash` was Git Bash 4.4 and `perl`/`tar` Git's,
>   so it stripped PortableGit from PATH in every call; a session started after needs none of that.
> - `dist/Bash.exe` (rev 373) was told to (2026-10-20): gps-time-2c, adc-framework-d8, msp430-a2, nupkg-28,
>   koliada-net-7c, daq-fe, guy-97, guy-85, ananta-f5. A Claude call ~11 ms; compare3 startup 8.9 ms against Git Bash's
>   23.6 ms (2026-10-19 18:45, deployed 324).
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
> **GREENLIT (the architect, 2026-10-19: "your next, go"; "add 'Recorded' and 'finished jobs' to list"), in order:**
> 1. DONE (documented, 2026-10-19): `type -a bash` listed Git's `usr/bin/bash.exe` first while `bash` ran C#Bash: a
>    remembered hit hides a same-named program added later earlier on PATH until `hash -r`. README says so; no code
>    fix unless documenting proves insufficient (DECISIONS 2026-10-19 "DOCUMENTED: a remembered hit").
> 2. The recorded items (item 4 below), the architect's order "commit, no-windows hook, trailing ';' and then
>    red-team - go": DONE the hook (its hg rev 4; DECISIONS 2026-10-19 "FIXED (outside the repo)"); DONE the user
>    Path's trailing `;`; DONE the red-team pass on revs 307-316 (its findings: TRUE STATE above). RULED (the
>    architect, "hook - keep it"): an exe path given to a SCRIPT stays flagged by the hook; `headless-exe` vouches.
> 3. Finished jobs kept in the table until reported, as bash (item 3 below).
> Settled 2026-10-19 (the architect): Git Bash's own oddities are not ours; transient slowdowns are most likely other
> load on the machine; the LinkedIn article stays as published (it links to the code).
>
> **AWAITING THE ARCHITECT (do NOT derive; Claude's positions are proposals):**
> 1. The performance review is DONE (R2, R3, R4 in DECISIONS 2026-10-18). The architect picked R4's 1-3 ("1, 2, 3
>    concur; go, deploy, notify, publish"): BUILT (DECISIONS "BUILT (R4 picks 1-3)"): icu.dll loads only when needed,
>    the `[[ ]]` trace text only under set -x, the start-up split; deployed rev 320, published rev 321. Then ("ok,
>    your recommendations; go") BUILT and deployed in rev 324 (DECISIONS "BUILT (the split's candidates…)"): the Git-tools probe
>    (mingw64/bin, slash-blind compare), PPID's pseudo-handle, the invariant culture (U+2212), printf's inf/nan.
>    Then ("Awk - your way") BUILT: the in-process awk never runs a program twice (literal formats and RS refused at
>    parse time, a refusal while running loud, 2) and out-of-range numbers as gawk 5.1 (DECISIONS "RULED + BUILT: the
>    in-process awk never runs a program twice"). AWAITING the architect: R4's 4-9 (held: calls are start-up bound)
>    and OutputEncoding 0.25 ms. NO optimisation code before a pick.
> 2. Parked, not greenlit: serial I/O (COMn as a device file + `stty`, "to discuss"); `compgen -c` scanning PATH.
>    (The battery's reference moved to GNU: DONE 2026-10-19, WSL's bash.)
> 3. Not raised (measured 2026-10-12): bash keeps finished jobs in its table until it reports them (`jobs` lists
>    them as Done, numbers keep counting); C#Bash's `wait` removes them.
>
> 4. DONE 2026-10-19: the independent red-team pass on revs 307-316 (its findings and their fixes: TRUE STATE above);
>    the no-windows hook's false positive (`Start-Process … -NoNewWindow` flagged when the flag follows a `$( )`) and
>    the user Path's trailing `;`.
>
> **RULES:** deploy only on "deploy and notify"; publish only on "publish" / "push".
>
> **KNOWN LIMITATIONS (documented, not raised for action):**
> - Windows starts no program whose path is over 260 characters, or whose working directory is over 258 (cmd
>   neither): C#Bash says so, 126 (the 259 case fixed 2026-10-19, rev 350); the shell itself can `cd` there
>   (longPathAware manifest, 2026-10-17). Win32 drops a trailing dot or
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
> - The compat battery's reference is WSL's GNU bash (`wsl.exe -e bash`) since 2026-10-19 (the architect: Git is
>   reached nowhere but the benchmark and `tools/publish.sh`): 222 pass, 15 differ of 237, new baseline 222. The 15
>   are Windows against Linux (`$OSTYPE`, drive and UNC paths, `dotnet`, TEMP, a CRLF `bc.exe` on PATH) or C#Bash
>   gaps; the probes about Windows itself (1, 125-127, 131-133, 161, 227, 233) can no longer pass by construction.
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
>   verifies). All run as their headers say: run-n.sh and tests/df/compare-wsl.sh in WSL's bash
>   (`wsl.exe -e bash <script>`), the .ps1 tools in PowerShell, compare3.sh in Git Bash (the one place
>   Git's bash is reached), deploy.sh and publish.sh in any bash.
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
> **Machine facts (do NOT "fix"):** Since 2026-10-19 Git's two `bash.exe` files have their real names again
> (`F:/Tools/PortableGit/bin/bash.exe`, the redirect Claude Code starts, and `usr/bin/bash.exe`), and Git
> (`F:\Tools\PortableGit\bin`) is off the Windows PATH (the architect), so nothing pre-empts C#Bash by name in a
> process started since; a session started before keeps Git's folders on its PATH. From 2026-09-13/14 to 2026-10-19
> both were renamed `_bash.exe` (the workaround for Claude Code issue #94077). `System32\bash.exe` (WSL) was moved
> away: reach WSL with `wsl.exe -e bash`. Run Git Bash scripts through the redirect:
> `F:/Tools/PortableGit/bin/bash.exe script`. Claude Code sets `NoDefaultCurrentDirectoryInExePath=1`.
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

**For readers of the repository** (2026-10-29, the architect: the README "should be highlights that are easily
digested (by a human). Details into (an)other doc(s)"): `README.md` is the highlights (what it is, download, the
benchmark table, architecture, Claude Code in three steps, what you get, what it won't do, testing); the detail is
in `docs/USING.md` (setup, start file, tips, startup and lookup, help, the request log), `docs/FEATURES.md` (the
language, every built-in command), `docs/PERFORMANCE.md` (method, caveats, a call's cost, benchmark provenance) and
`docs/DEVELOPING.md` (build, test, tooling, layout, Mercurial). Publish's doc review updates the README's table AND
docs/PERFORMANCE.md's prose (tools/publish.sh header).

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
  folded in with `-UpdateBaseline`. Reference bash: WSL's GNU bash (`wsl.exe -e bash`, each probe
  started in its fixture directory, seen as `/mnt/<drive>/...`) since 2026-10-19, or `-Reference <bash.exe>`.
  It is RUN to identify it, and C#Bash (any build) is refused (2026-10-12: with
  `CLAUDE_CODE_GIT_BASH_PATH` naming the deployed C#Bash the battery had compared C#Bash with itself).
  Until 2026-10-19 the reference was Git for Windows' bash, found through `git.exe` on PATH, and WSL was
  refused as a different OS; the move rebuilt the baseline (DECISIONS 2026-10-19 "the battery's reference").
  `-UpdateBaseline` writes the file with CRLF line endings; it is kept LF (convert before committing).
- **`tests/corpus/run-n.sh`** (in WSL's bash: `wsl.exe -e bash tests/corpus/run-n.sh`) — the `-n` corpus: C#Bash's `bash -n` verdict against GNU's
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
