# Development history (Mercurial)

This project was built and version-controlled with **[Mercurial](https://www.mercurial-scm.org/)**, not git â€” by preference. The git repository you're reading is a published *snapshot*; the genuine, commit-by-commit record lived in hg.

The full history is preserved two ways:

- **Readable** â€” the table below (`hg log`, newest first).
- **Complete** â€” `mercurial-history.hg`, an `hg bundle --all` of the entire repository. Recover it with:
  ```sh
  hg init csharpBash && cd csharpBash
  hg unbundle ../mercurial-history.hg
  hg update
  ```

Authored by `Guy <guy@koliada.com>`, co-authored by Claude (Anthropic). 334 changesets.


| rev | date | summary |
|----:|------------|---------|
| 333 | 2026-10-19 | Benchmark provenance: compare3 times Git Bash through <Git>/bin/bash.exe as Claude Code starts it (no LANG, rev 332); README "Benchmark provenance" (rev 139 carried two changes; the article's Git Bash was started by another Git Bash); table from the 2026-10-19 run; deploy.sh's battery takes that Git Bash as reference; stale _bash.exe launch lines fixed |
| 332 | 2026-10-18 | compare3: no LANG/LC_* reaches any shell (the architect: time Git Bash as Claude Code runs it, a non-login bash -c without LANG); the result no longer depends on how the script is started |
| 331 | 2026-10-18 | Correction: Claude Code's per-call Git Bash is not a login shell and has no LANG (README's claim at rev 329 was unverified); the benchmark's Git Bash form is the architect's call |
| 330 | 2026-10-18 | Published: GitHub main = git b9067c8 = hg rev 329 by tools/publish.sh |
| 329 | 2026-10-18 | Correction: Git Bash's pipeline row moved with the launch form (no LANG in a non-login Git Bash), not a cache; README's table from a run in compare3's documented login form |
| 328 | 2026-10-18 | Published: GitHub main = git a8a90e2 = hg rev 327 by tools/publish.sh; on a break |
| 327 | 2026-10-18 | Resume note: on a break until a bug is filed (the architect); nothing greenlit |
| 326 | 2026-10-18 | Published: GitHub main = git 9943884 = hg rev 325 by tools/publish.sh |
| 325 | 2026-10-18 | Deployed rev 324 (notified); measured back to back with rev 320 (a call ~11 ms, compare3 startup 8.5 ms; the first run after the deploy slower again); doc review for the publish |
| 324 | 2026-10-18 | awk never runs a program twice: literal printf/sprintf formats and RS refused at parse time (the PATH awk runs it once), a refusal while running loud (2); out-of-range numbers as gawk 5.1 (+inf, -nan); case awk_unsupported_once |
| 323 | 2026-10-18 | The split's candidates: a mingw64/bin entry names Git's root and the PATH compare ignores slash direction (probe 0.66 -> 0.08 ms); PPID from the pseudo-handle (0.47 -> 0.02 ms); the invariant culture as every thread's default ($((-5)) printed U+2212 under sv-SE); printf inf/nan as C (%g of inf threw); cases startup_env, printf_nonfinite; awk re-run after output RAISED |
| 322 | 2026-10-18 | Published: GitHub main = git 32ad493 = hg rev 321 by tools/publish.sh |
| 321 | 2026-10-18 | Deployed rev 320 (notified); measured back to back: a Claude call ~1.6 ms faster, compare3 startup 9.5 ms; doc review for the publish (README table and figures from the deployed run, ARCHITECTURE 9.4 no culture unless it is the point) |
| 320 | 2026-10-18 | R4 picks 1-3: icu.dll loads only when needed (ordinal prefix tests, invariant integer parses, the command map's file time, the SID's RID, $( )'s writer); the [[ ]] trace text only under set -x; start-up split marks; case icu_lazy (AotOnly) |
| 319 | 2026-10-18 | Performance review R2-R4 recorded (measurement only): rev 316 not slower than 286, README's table from the quiet run; R4's ranked candidates await the architect |
| 318 | 2026-10-18 | Published: GitHub main = git e13485f = hg rev 317 by tools/publish.sh |
| 317 | 2026-10-18 | Doc review before publishing rev 316: README's benchmark table and figures from the deployed build's compare3 run |
| 316 | 2026-10-18 | Only what Windows' launcher starts is a program: a zz.js before zz.exe on PATH is passed over (as Git Bash) |
| 315 | 2026-10-18 | Ruled: .. after a junction stays lexical (the architect: "2/ ok") |
| 314 | 2026-10-18 | Raised two forks for the architect (PATHEXT matches Windows cannot start; .. after a junction); anchor: run done |
| 313 | 2026-10-18 | Paths over 260 characters: cd enters them (longPathAware manifest); starting a program there says the Windows limit |
| 312 | 2026-10-18 | set -x traces compound commands as bash does: [[ ]], (( )), for heads, arithmetic for, case |
| 311 | 2026-10-18 | set -x prints what bash prints: assignments, ++ levels, quoted words, PS4 prompt escapes |
| 310 | 2026-10-18 | An MSYS program inside Bash.exe s.sh >> out can write: the inherited append-only handle is reopened for it |
| 309 | 2026-10-18 | Red team on the lookup changes: ~ PATH entries, MSYS/root/drive-relative paths, a child's environment, missing interpreters |
| 308 | 2026-10-17 | Eight GNU differences fixed, and a hash crash from rev 298 (live in deployed 302) |
| 307 | 2026-10-17 | A subshell's exec redirect ends with it: ( exec > f; echo in ); echo after put every later line into f |
| 306 | 2026-10-17 | C#Bash's lookup is the only authority: a bare name not on PATH is never handed to Windows' launcher to search |
| 305 | 2026-10-17 | Anchor: the fix-everything run (the architect: "Fix all the wrong results and smaller diffs from GNU ... go"), its order and fork rule |
| 304 | 2026-10-17 | Ruled: the shell's $PATH keeps empty entries as given; only what reaches Windows is cleaned |
| 303 | 2026-10-17 | Deployed rev 302 (revs 297-302) by tools/deploy.sh; notice to the nine live sessions; anchor updated |
| 302 | 2026-10-17 | Fix b: no empty PATH entry reaches Windows (C#Bash's process PATH and every child's) |
| 301 | 2026-10-17 | Finding addendum: the empty PATH entry is the user Path's trailing ";" (HKCU), re-checked |
| 300 | 2026-10-17 | Anchor: the OS-launcher item is live under Claude Code (empty process-PATH entry); the architect's call |
| 299 | 2026-10-17 | Finding: an empty process-PATH entry makes Windows search the cwd under Claude Code (8 of 10 live sessions) |
| 298 | 2026-10-17 | A background job works in its own directory: after the parent's cd, its relative paths hit the parent's |
| 297 | 2026-10-17 | Deployed rev 296 (revs 287-296) by tools/deploy.sh; notice to the nine live sessions |
| 296 | 2026-10-17 | A colon name is a path only with a drive: `ads:bar.exe` (an NTFS stream) ran from the cwd |
| 295 | 2026-10-17 | Defect (d): PS4 is expanded for each trace line; $EPOCHREALTIME has microseconds |
| 294 | 2026-10-17 | Defect (c): a command name with a slash runs from the shell's own directory, never searched for |
| 293 | 2026-10-17 | Defects (a) and (b): a script found on PATH runs; a bare name never runs a script from the cwd; hash -d reads the map |
| 292 | 2026-10-17 | Anchor: the global CLAUDE.md is Claude's file (the architect, 2026-10-17): its stale C#Bash limits fixed and committed in ~/.claude (git 4ef2f2e), not "suggested only" |
| 291 | 2026-10-16 | Before the restart as a full C#Bash session (BASH_PATH): RESUME anchor head rewritten (the old run-on START HERE is in rev 290; open items carried: defects a-d, review R2-R4, parked items, do-not-derive guards; tail byte-identical); machine fact re-verified (PortableGit bin/_bash.exe stub broken); ARCHITECTURE lookup wording fixed; stale CLAUDE.md lines told to the architect |
| 290 | 2026-10-16 | Published: GitHub main = git 0b67afa = hg rev 289 by tools/publish.sh (the README lookup-section intro; benchmark of deployed rev 286; 290 changesets; bundle verified) |
| 289 | 2026-10-16 | README: Startup and command lookup opens with why it matters (a PATH lookup probes every entry x every PATHEXT name, a miss probes them all: ~600 probes, ~15 ms here, half of every Claude Code call via the snapshot's hash rg), pointing to Performance and DECISIONS "REVIEW R1 RESULT" (the architect's request) |
| 288 | 2026-10-16 | Published: GitHub main = git 5cf4d27 = hg rev 287 by tools/publish.sh (benchmark of deployed rev 286, doc review rev 287, 288 changesets, bundle verified, remote main = HEAD); the CRLF-to-LF warning for expected files with CRs is harmless (the runner compares lines) |
| 287 | 2026-10-16 | Doc review before publishing (deployed rev 286): README reworked: the benchmark table (compare3 on deployed rev 286, all outputs agree; startup 19.3 vs Git Bash 19.0 ms) is the last item of the intro under "Bullshit walks, money talks:"; new sections Startup and command lookup (BASH_PATH, Git usr/bin, own dir, startup files, lookup order, remembered lookups in ~/.bash/commands, hash misses, Windows fallback, known gaps) and Using it as Claude Code's shell (the start file, getting the best out of it); Performance refers back and adds what a Claude Code call costs (~16 ms: ~11 floor, ~3 start-up, snapshot <1, exit ~0.25); commands list gains disown/nohup/setsid/yes and verified options; stale compgen line fixed; 140 cases. DECISIONS: the deploy; PROJECT_CONTEXT: current state, stale trap note fixed |
| 286 | 2026-10-16 | The command map moves to ~/.bash/commands (the architect: bash is not a "normal" Windows app; HOME, MSYS or Windows form, else the profile) and is pruned by age: a map read is touched at most hourly, one unused for 30 days goes when a new map is made (a burst of test PATH values can no longer push out the one in use); command_map extended (touch on read, 40-day prune), isolated by HOME; the old %LOCALAPPDATA%/CSharpBash test-only folder removed |
| 285 | 2026-10-16 | Fix: name[subscript]+=value appends to the element (it ran as a command, "a[k]+=v: command not found", the value lost): the indexed-assignment split takes ]+= for element assignments, ArrayElementAssign.Append, ShellEnvironment.AppendArrayElement/AppendAssocElement shared with declare/local (joined, or added under declare -i; a scalar is element 0), and declare a[k]+=v now appends; case array_element_append byte-identical to GNU 5.1 (deployed 266 fails it); suite 140/140 both modes on AOT; battery 226/237; -n corpus 850 and printer corpus 845 at baseline |
| 284 | 2026-10-16 | Exit traced: C#Bash's exit path (.NET shutdown + DLL unloading) is ~0.25 ms, as a minimal AOT program with ICU (ExitProcess/TerminateProcess variants, 100-launch medians); its ~3 ms extra stays with exit skipped, so it is start-up; R1's "+3 ms exit" was the timing switch (~1.85 ms a launch when on, its log work after its exit mark); ICU ~0.7 ms a launch. Found, open: name[sub]+=value is run as a command; map pruning by last write |
| 283 | 2026-10-16 | T3 measured: with the full 50-entry PATH a Claude Code call is ~16 ms on rev 282 vs ~31 on deployed 266 (true -47%, ls -49%, cd-pwd -50%, git-status 49.6 -> 33.4 ms); the snapshot segment 15 ms -> 0.6 ms, the rest unchanged; anchor: revs 277-282 not deployed |
| 282 | 2026-10-16 | BASH_PATH and the persistent command map (the tool-lookup plan, T1 + T2): a non-empty BASH_PATH becomes PATH at startup and is removed (process environment too, so a nested shell keeps its parent's PATH); CommandMap keeps lookups across processes per PATH value under %LOCALAPPDATA%/CSharpBash/commands: hits from every lookup (one existence probe), misses recorded and obeyed by `hash name` alone (an earlier process's; its own searched again; the note "remembered miss: hash -r searches again"), running/command -v/type search as bash (Claude's call, delegated); hash -r / -d forget; not kept for a relative PATH; atomic merged writes. Case command_map (deployed 266 fails it); suite 139/139 both modes on the AOT build; battery 226/237. Two older defects recorded open: relative a/b.exe with PATH=dist, extensionless #! script on PATH |
| 281 | 2026-10-16 | Decided: rev 277 (lazy binding) stays; tool lookup = a .bash_profile PATH plus a self-maintained C#Bash command map recording hits and misses, a miss final ("fail once"); plan T1-T3 (profile from the Windows PATH, one map per PATH value, hash -r, atomic writes, measure) written for ratification, with the installed-after-a-miss risk |
| 280 | 2026-10-16 | Anchor: the statusLine runs C:/Windows/py.exe (done, checked with no PATH); the architect's tool-lookup design (a .bash_profile PATH plus a self-maintained C#Bash command map) and Claude's refinements recorded as open |
| 279 | 2026-10-16 | Lazy binding measured: claudecall --build rev 278 vs 266 within noise (bare +2.8%, calls -4.8% to +1.6%); anatomy vs rev 273: runtime start ~-0.3 ms in every row, process exit unchanged, so R1's ~3 ms extra C#Bash exit is not ws2_32/iphlpapi/secur32/sspicli (icu.dll, untraced, is the only DLL difference left); anchor: the one-entry PATH is the architect's choice, open: keep rev 277, how Claude's shell finds tools without claude.exe having a PATH, the statusLine's bare py |
| 278 | 2026-10-16 | compare3 --build, rev 277 (lazy binding) against deployed rev 266: startup 18.1 ms vs 18.5 (-2.2%), Git Bash 18.8, WSL 135.7; loop +6.3%, loop_big +3.8% (not attributable to rev 277 by this comparison: revs 267-276 differ too); all outputs agree |
| 277 | 2026-10-16 | Lazy binding for ws2_32, iphlpapi and secur32 in the AOT build (the architect: "yes, lazy load"): a target after the ILCompiler's SetupOSSpecificProps swaps the SDK's WindowsAPIs.txt direct-call list for a copy without those 462 lines, so the three leave the import table and load at first use; an idle shell loads 20 modules instead of 24 (ws2_32, iphlpapi, secur32, sspicli gone; icu.dll unchanged); suite 138/138 both modes on the AOT binary; hostname -I/-f, whoami unchanged; the build fails if the SDK list is not found. DECISIONS: the finding and the decision, with the criteria written before building |
| 276 | 2026-10-16 | Anchor: the review's next step after the session ends: re-run claudecall.sh --build once the architect has removed PATH entries, compare with the rev 273 table, then R2-R4 |
| 275 | 2026-10-16 | Review R1 result recorded: a ~31 ms Claude Code call = ~11 ms platform floor (any AOT process here), C#Bash start/exit extras ~4 ms, init ~2.6 ms, the snapshot 15 ms of which the failed `hash rg` PATH search is ~14-15 ms (two X: entries 8.8 ms, the other 49 ~6 ms); found: PS4 not expanded, EPOCHREALTIME in ms steps, Claude Code's generator adds set -o monitor/onecmd, Git Bash hang with this snapshot |
| 274 | 2026-10-16 | Review R1, first results (claudecall.sh --build, rev 273): a trivial Claude Code call is ~31 ms: runtime start 7.1, init 2.6, snapshot 15.2, command+builtins+cwd 0.8, process exit after the last mark 6.9; the timing switch costs nothing measurable when off; R1 criterion NOT met (21% after the exit mark): exit to be split against a minimal AOT baseline |
| 273 | 2026-10-16 | claudecall.sh: C#Bash only (the build against the deployed build, plus the anatomy). The Git Bash column hung on both runs: its git status blocks when the C#Bash-made snapshot is sourced with any second wrapper part (bisected; not C#Bash), and Git Bash in Claude Code sources its own snapshot anyway, so the column was never like for like |
| 272 | 2026-10-16 | claudecall.sh: Git Bash's snapshot copy drops set -o monitor and set -o onecmd (Claude Code's generator adds them because `set -o \| grep on` matches the option names; together they hang Git Bash's git status here, measured); every launch round has a 300 s limit (the first run hung in the Git Bash column) |
| 271 | 2026-10-16 | Review R1 instruments: the opt-in timing switch BASH_TIMING_LOG (marks for Main, console, evaluator, ready, done, exit and source/eval/external regions, written at exit; off = one environment read) and the contract tool tests/bench/claudecall.sh (Claude Code's exact wrapper, re-read from a live call, with a scrubbed snapshot fixture; whole-call times for the build, the deployed build and Git Bash; the anatomy with the switch on); suite 138/138 both modes |
| 270 | 2026-10-16 | Ratified: the performance review plan R1-R4 (call anatomy with an opt-in timing switch and a Claude-call contract tool, throughput, parse, ranked report; no optimisation code before the architect picks) |
| 269 | 2026-10-16 | Published: GitHub main = git 94a3ed2 = hg rev 268 by tools/publish.sh run under C#Bash (benchmark of deployed rev 266, doc review rev 268, 269 changesets, bundle verified, no header left, remote main = HEAD) |
| 268 | 2026-10-16 | Doc review before publishing: ARCHITECTURE: the // argument rule (cmd //c) in the path model, /dev/tty only with a console window in the redirect model; README: the benchmark of deployed rev 266 (compare3.sh, all outputs agree; every shell a little slower this run, startup 18 ms vs Git Bash 19 ms) |
| 267 | 2026-10-16 | DEPLOYED rev 266 by tools/deploy.sh run under C#Bash (revs 261-266: cmd //c, any-bash tools, strings, compgen, the git credential hang); suite 138/138 on the AOT binary both modes; battery 226/237; live-verified; notice sent to the eight sessions; decided: no SSH_ASKPASS (the architect: "No") |
| 266 | 2026-10-15 | git's credential prompt no longer hangs under Claude Code (traced: Git for Windows prompts via `bash -c 'cat >/dev/tty && read -r line </dev/tty ...'`, bash is C#Bash, and Claude Code's console has no window): /dev/tty is the console only when it has a window (ConPTY and classic consoles do, measured), else "No such device or address" at once, as GNU bash without a terminal and Git Bash there; new fixture tests/fixtures/console-kind.ps1; dev_tty_windowless authored (rev 257 hangs); suite 138/138 both modes; battery 226/237; raised: SSH_ASKPASS as Git Bash's profile sets it (Claude: no); not deployed |
| 265 | 2026-10-15 | compgen (daq-d1), describing THIS shell (the architect: "C#Bash is really _only_ about its builtins"): actions in bash's order then -G then -W, word filter, -W split at IFS and expanded without globbing, -X with & and !, -P/-S, -f/-d as paths, names/keywords/signals/set -o/shopt/jobs; -b every C#Bash builtin; -c without a PATH scan (DEFERRED enhancement); -o/-F/-C/-u/-g/-s/binding/helptopic/hostname/service/user and complete/compopt the not-available notice; compgen identical to GNU bash (246 lines), compgen_local authored; suite 137/137 both modes; battery 226/237; not deployed |
| 264 | 2026-10-14 | strings as GNU strings 2.38 (scope ratified): files or stdin, whole file, -n/--bytes (strtoul), -N, -t o\|d\|x and -o, -f, -w, -s, -e s\|S\|b\|l\|B\|L with GNU's one-byte resync; --version never GNU; -d/-T/-U/@file go to a PATH strings, else the not-available notice; strings_gnu identical to GNU (197 lines), strings_local authored; the real 6.9 MB dist/Bash.exe gives GNU's output exactly under four option sets; suite 135/135 both modes; battery 226/237; anchor: compgen's scope was greenlit 2026-10-12 (a stale AWAITING note corrected); not deployed |
| 263 | 2026-10-14 | Anchor: strings scope proposed against GNU strings 2.38 (WSL), awaiting ratification |
| 262 | 2026-10-14 | deploy.sh and publish.sh run under any bash (the architect: "drop it, go"): the Git Bash guard, copied from compare3.sh with no reason of its own, removed; every command form of both run under Git Bash, deployed rev 257 and rev 261 with identical results (history table and staging list byte-identical, base64, token mask, renaming a running image); compare3.sh keeps its measured guard; rev 261 goes with the next deploy |
| 261 | 2026-10-14 | cmd //c as Git Bash: an argument to a Windows program starting // loses one leading / and its backslashes become / (//c -> /c, ///x -> //x); exactly two slashes with a later / is UNC and kept; a glob or quote character leaves it alone; MSYS_NO_PATHCONV / MSYS2_ARG_CONV_EXCL honoured (cmd //c started an interactive cmd that waited on stdin); 63 argument shapes identical to Git Bash bar the decided bare /c and mid-argument //; native_args +7 checks; suite 133/133 both modes; battery 226/237; not deployed |
| 260 | 2026-10-13 | Published: GitHub main = git 0b3547d = hg rev 259 by tools/publish.sh (benchmark of deployed rev 257, doc review rev 259, 260 changesets, bundle verified, no header left, remote main = HEAD) |
| 259 | 2026-10-13 | Doc review before publishing: ARCHITECTURE section 11 rewritten for bash's dynamic scoping (Var frames), awk main-input getline and BINMODE, the path model (Windows-program argument conversion, MSYS-program quoting; "arguments are never rewritten" was stale since rev 186); IMPLEMENTATION: CommandText/CommandPrinter rows, spans replace FunctionDef.Source (stale since rev 240), ShellEnvironment row; README: case ;& ;;&, dynamic scoping, awk getline, 133 cases, the benchmark of deployed rev 257 (compare3.sh, all outputs agree) |
| 258 | 2026-10-13 | DEPLOYED rev 257 by tools/deploy.sh (revs 254-257: awk getline keeps CR, MSYS arguments intact, sed final newline and D, rm as GNU incl. the junction DATA LOSS fix; rollback dist/Bash.exe.rev252); suite 133/133 on the AOT binary both modes; battery 228/237; live-verified; notice sent to the six sessions; cmd //c greenlit next (measured rule recorded); open: the Git Bash guard in deploy.sh/publish.sh |
| 257 | 2026-10-12 | rm as GNU rm (adc-framework-d8): a write-protected (ReadOnly) file is removed when stdin is not a terminal (was refused without -f); -i and -I ask, the answer a line of stdin (they removed without asking); the last of -f/-i/-I wins; DATA LOSS fixed: rm -r on a directory link (ln -s, junction) deleted the target's contents, now the link alone is removed; rm_interactive identical to GNU rm; suite 133/133 both modes; battery 226/237; raised: cmd //c starts an interactive cmd; not deployed |
| 256 | 2026-10-12 | sed as GNU sed: a last line without a newline is written without one (C#Bash added it); D restarts the cycle without autoprinting, the append queue still flushed (N;P;D printed every line after the first twice, in every deployed build); run-tests.ps1 -Detached kept a last empty line (\z, not .NET's $); sed_final_newline identical to GNU sed; suite 133/133 both modes; battery 226/237; not deployed |
| 255 | 2026-10-12 | Arguments reach MSYS programs intact (msp430-a2): an argument with more than letters, digits and -_./:=@%+, is double-quoted with backslashes doubled for an MSYS program, which re-parsed unquoted words (braces expanded, globs matched, quotes and backslashes taken); native programs unchanged; msys_arguments identical to GNU; suite 131/131 both modes; battery 226/237; not deployed |
| 254 | 2026-10-12 | awk main-input getline / getline var in-process (msp430-a2, data loss: a program containing getline went to Git's gawk, which strips every CR); awk handed to the PATH awk gets -v BINMODE=3; awk_getline identical to GNU awk; suite 130/130 both modes; battery 226/237; not deployed |
| 253 | 2026-10-12 | DEPLOYED rev 252 by tools/deploy.sh (revs 234-252: printer, case fall-through, declaration arrays, diff --label, variable scope; rollback dist/Bash.exe.rev233); suite 129/129 on the AOT binary both modes; battery no regression; live-verified; notice sent to the six sessions |
| 252 | 2026-10-12 | Decided: the variable-scope cost (~5% per call with a local) accepted, deploy of revs 234-251 approved; a performance review is expected shortly (the architect's to call) |
| 251 | 2026-10-12 | Anchor: strings queued (its 2026-10-10 revisit condition, a second session needing it, is met), after the msp430-a2 fixes and before compgen |
| 250 | 2026-10-12 | Variable scope V4: SetLocal declares and assigns in one lookup; benchmark record (rev 249: func +4.2%, coreutils +5.3%, rest within noise); in-process: loop/arith level, func a real ~5% (~70 ns, +48 B per call with a local); awaiting the architect; anchor: two msp430-a2 defects queued (awk getline falls through to gawk which strips CR; MSYS args with braces unquoted) |
| 249 | 2026-10-12 | Benchmark record: compare3 --build rev 247 against deployed rev 233 (coreutils +10.5%, the row V4 investigated) |
| 248 | 2026-10-12 | Variable scope V4: a snapshot is restored once, so RestoreSnapshot takes over its copy (one deep copy per $( ), not two); in-process diagnostic on coreutils.sh: 68.9-70.2 ms against rev 246's 69.5-73.6 ms, 78 KB per substitution against 87 KB; suite 129/129 both modes (one -Detached run failed traps_debug_return once, three reruns passed); battery 226/237 |
| 247 | 2026-10-12 | Variable scope V2+V3: one store per scope (Var: scalar, indexed or assoc, with its attributes); an assignment to an unknown name goes GLOBAL (a function's x=1 vanished on return in every build); local/declare-in-function hold arrays and attributes for the call; unset as bash; local -i evaluates; a subshell's fatal error reported inside its redirects; variable_scope identical to GNU; suite 129/129 both modes; battery 226/237; -n 850; printer 845; not deployed |
| 246 | 2026-10-12 | Variable scope V1: tests/cases/variable_scope.sh (bash's scoping contract, 59 lines, expected = GNU); C#Bash baseline: 20 lines then an abort, 67 diff lines; not yet in the suite |
| 245 | 2026-10-12 | RESUME anchor: the variable model does not follow bash scoping (new variables in functions lost, arrays not local, attributes shell-wide; every build, measured on 29 cases); plan V1-V4 proposed, awaiting ratification |
| 244 | 2026-10-12 | diff -u --label as GNU (adc-framework-d8): the labels replace the header names and timestamps (accepted and ignored before); Opts.AllLong keeps every value of a repeated long option; suite 128/128 both modes; battery 226/237; not deployed |
| 243 | 2026-10-12 | Printer P5: compare3 --build rev 242 against deployed rev 233, every row within noise, outputs agree; the printer plan (P1-P5) is complete |
| 242 | 2026-10-12 | Printer P4: run-n.sh --printer compares declare -f with GNU on the 912-file corpus: 845 identical (was 445), every other file has a cause (62 unparseable, 4 OSTYPE, 1 extglob); new rules: [[ ]] from its tree (-n, CondGroup), continuations removed, $'' decoded in substitutions, array literals normalised, arith-for 1, was_heredoc; fixed on the way: local p=($(cmd)) syntax error, export/readonly arrays, quoted (..) is a string, ${#scalar[@]} is 1; found: arrays are not local (raised); suite 127/127 both modes; battery 226/237; -n corpus 850/912 |
| 241 | 2026-10-12 | Printer P3: jobs prints a job's command as bash 5.1 (the printer's job mode: groups and subshells on one line, loops and if/case on several, assignments and redirections kept, no !); DescribeNode's node-type names gone; jobs_text identical to GNU (14 shapes); suite 126/126 both modes; battery 226/237 |
| 240 | 2026-10-12 | Printer P2: CommandPrinter prints functions as bash 5.1 (declare -f, type): bash's layout from the AST, here-documents deferred to the newline; CommandText renders substitutions verbatim, stops at a top-level operator, gives dups an explicit fd and escapes ' in $'...' ($BASH_COMMAND matches bash on those too); command_printing byte-identical to GNU (was 561 diff lines); suite 125/125 both modes; battery 226/237; -n corpus 846/912 |
| 239 | 2026-10-12 | Printer P1: bash 5.1's command-printing rules measured and written down (12 rules), probes as tests/cases/command_printing.sh (53 functions, expected = GNU, not yet in the suite: C#Bash misses 561 diff lines) |
| 238 | 2026-10-12 | case's ;& and ;;& as bash (they were syntax errors): the lexer makes them tokens, CaseItem keeps the terminator, ExecCase falls through or goes on testing; found: select and coproc do not exist (recorded); suite 124/124 both modes; battery 226/237; -n corpus 846/912 (baseline advanced); not deployed |
| 237 | 2026-10-12 | Ruling recorded: Git Bash is a reference only as far as Claude is concerned, GNU bash the strategic reference; the command printer greenlit (plan to be ratified) |
| 236 | 2026-10-12 | RESUME anchor: published, GitHub main = git 02f95bc = hg rev 235 (236 changesets, bundle verified) by tools/publish.sh; deployed rev 233 by tools/deploy.sh; six sessions told; awaiting: command re-printing, compgen scope |
| 235 | 2026-10-12 | GitHub sync at rev 235: doc review for the push: README (2026-10-12 table from compare3 on deployed rev 233; traps ERR/DEBUG/RETURN and $BASH_COMMAND; IFS splitting; and-or jobs and jobspecs; 123 cases; compgen not yet; the contract tools in Layout), ARCHITECTURE (field splitting, traps, jobs), IMPLEMENTATION (CaseTracker, FindJobIn/RunWithJobGroups/Stages, Kill's file); benchmark results |
| 234 | 2026-10-12 | DEPLOYED rev 233 by tools/deploy.sh (revs 218-233; rollback dist/Bash.exe.rev217); suite 123/123 on the AOT binary both modes; battery no regression; live-verified; notice sent to the six sessions |
| 233 | 2026-10-12 | Deploy and push are contract tools (the architect: repetitive tools are contracts): tools/deploy.sh (staging publish, suite both modes + battery on the binary, rename-and-swap, rollback copy) and tools/publish.sh (benchmark-of-deployed check, history regenerated and bundle verified, exactly hg's files staged, masked token push, verified) |
| 232 | 2026-10-12 | First compare3.sh --build: rev 231 against deployed rev 217 in alternating rounds, every row within 2.5% (func -0.4%, the morning's +8% was drift between separate runs); outputs agree |
| 231 | 2026-10-12 | Repetitive tools are contracts (the architect): compare3.sh --build publishes the committed tree AOT itself and times it against the deployed build in alternating rounds; tests/corpus/run-n.sh is the -n corpus (912 WSL bash-completion scripts fetched each run, pinned by manifest, ratcheted by baseline: 844/912); run-compat.ps1 never takes C#Bash as its reference (it had compared C#Bash with itself when CLAUDE_CODE_GIT_BASH_PATH named dist) |
| 230 | 2026-10-12 | Benchmark (compare3.sh, unmodified): rev 229 AOT vs deployed rev 217, back to back; suite 123/123 on the rev 229 AOT binary both modes |
| 229 | 2026-10-12 | RESUME anchor: compgen (daq-d1 report, verified) awaiting the architect with Claude's scope position; revs 219-228 not deployed |
| 228 | 2026-10-12 | Field splitting and $* follow IFS as bash: IFS='' does not split (it split anyway: daq-d1's glob workaround broke on a path with a space), non-white IFS characters end fields and make empty ones, unquoted $@/$* join with IFS[0] then split, $* and ${a[*]} join with IFS[0] wherever they make one string; recorded: compgen missing (daq-d1); suite 123/123 both modes; battery 226/237; not deployed |
| 227 | 2026-10-12 | RESUME anchor: open defects worked to rev 226 (case in $( ), kill -l, dir stack, jobs -x, jobspec names, a && b &); awaiting the architect: command re-printing (print_cmd.c) for compound job text and declare -f; revs 219-226 not deployed |
| 226 | 2026-10-12 | a && b & and a \|\| b & background the whole and-or list as one job, as bash (only the last pipeline went to the background: sleep 1 && echo A & slept in the foreground, cd / && true & moved the shell; every build incl. deployed rev 217); jobs shows the list with its own operators; suite 122/122 both modes; battery 226/237; not deployed |
| 225 | 2026-10-12 | Jobspecs as bash 5.1's get_job_spec: %name (prefix), %?text (substring, case ignored), bare % (current); "ambiguous job spec", a later pipeline command always ambiguous as bash; jobs keep per-stage commands; found: a && b & backgrounds only b (every build), compound job text is its node type (recorded, next); suite 121/121 both modes; battery 226/237; not deployed |
| 224 | 2026-10-12 | jobs -x runs a command with each jobspec word replaced by the job's process group, as bash 5.1 (refused before): $$ in a script, the job's pid interactive; -x before -l/-p/-n; found: no %name / %?text / bare % jobspecs (recorded, next); suite 120/120 both modes; battery 226/237; not deployed |
| 223 | 2026-10-12 | pushd/popd/dirs as bash 5.1's pushd.def: +N/-N rotate, remove and show entries (refused before); -n means no cd, not quiet; a -n rotation drops its entry as bash does; dirs -p/-l work; ~ only at a / boundary of a HOME longer than /; cd's errors under pushd say pushd:; suite 119/119 both modes; battery 226/237; not deployed |
| 222 | 2026-10-12 | kill -l, kill -L and trap -l print bash's numbered signal table (a line of nine names before); kill's options follow kill.def (first -WORD is the signal, unchecked when listing; -sNAME/-nNUM; kill -l 0 is EXIT, 32/33 print nothing, EXIT/DEBUG/ERR/RETURN number 0/65/66/67); SIG only before a real signal's name (trap accepted SIGEXIT, SIGERR, SIG2); suite 118/118 both modes; battery 226/237; not deployed |
| 221 | 2026-10-12 | A case inside $( ), "$( )" or backquotes no longer ends the substitution at its first pattern's ) (bash's parse_comsub): one CaseTracker for the lexer's text scan and the parser's token scan; a bare $name hang in the first version caught by the suite, never shipped; suite 117/117 both modes; battery 226/237; -n corpus 844/912 unchanged; not deployed |
| 220 | 2026-10-11 | Command lookup follows the shell's own PATH: any PATH assignment resets the hash (a stale one ran the old program), a temporary PATH= is searched for that command, a subshell's PATH no longer leaks to the parent; suite 116/116 both modes; battery 226/237; not deployed |
| 219 | 2026-10-11 | $0 is argv[0] as the launcher passed it, in the shell's forward-slash form (it was the native backslash path: every -c error prefix under Claude Code; Bash.dll on JIT); a PATHEXT match is launched with its on-disk name (bash.EXE before); found: a temporary PATH= is not used for the lookup (next); suite 115/115 both modes; battery 226/237; not deployed |
| 218 | 2026-10-11 | DEPLOYED rev 217 (-t -k -p, DEBUG/RETURN traps, $BASH_COMMAND, -T, declare -ft; rollback dist/Bash.exe.rev213); suite 114/114 on the AOT binary both modes; live-verified; six sessions told; anchor: open defects next |
| 217 | 2026-10-11 | DEBUG and RETURN traps run as bash 5.1 (accepted and silent before), $BASH_COMMAND exists (rendered when read; it was empty, in ERR traps too), -T functrace and declare -ft extend them, a trap's $LINENO is the line it fires for (ERR said 1); per-command cost within benchmark noise; expected = GNU via WSL; suite 114/114 both modes; battery 226/237; -n corpus 844/912; not deployed |
| 216 | 2026-10-11 | -t (onecmd), -k (keyword) and -p (privileged: BASH_ENV not read) act as bash 5.1 (accepted and ignored before); $- lists k p t and puts s last (hBs); found: trap DEBUG/RETURN accepted and never run (with the architect); expected = GNU via WSL; suite 113/113 both modes; battery 226/237; not deployed |
| 215 | 2026-10-11 | RESUME anchor: pushed, GitHub main = git 1ab0a25 = hg rev 214 (215 changesets, bundle verified); deploy, notify and push of rev 213 all done; nothing greenlit |
| 214 | 2026-10-11 | GitHub sync at rev 214: DEPLOYED rev 213 (BASH_COREUTILS=external fix; rollback dist/Bash.exe.rev209), notice sent; README table from compare3 on deployed rev 213; anchor |
| 213 | 2026-10-11 | BASH_COREUTILS=external runs the PATH program (it called RunCommand, which ran the builtin, which came back: a stack overflow killed the shell, every build to rev 209); README's known-defect note removed; suite 112/112 both modes; battery 226/237; not deployed |
| 212 | 2026-10-11 | RESUME anchor: published, GitHub main = git e8c2d39 = hg rev 211 (212 changesets, bundle verified); deploy, notify and publish all done; nothing greenlit; raise BASH_COREUTILS=external first |
| 211 | 2026-10-11 | GitHub sync at rev 211: doc review for the push: README (2026-10-11 table from compare3 on deployed rev 209, startup median of 5, one line at a time, restricted/POSIX notices, awk and jq additions, 111 cases, BASH_COREUTILS=external known crash), ARCHITECTURE (reading model, awk extension), IMPLEMENTATION (Step, ParseNext, DeferredSubstitution, RunString, NotAvailable.cs); benchmark results; anchor: BASH_COREUTILS=external stack overflow found (rev 156-209), not analysed |
| 210 | 2026-10-11 | DEPLOYED rev 209 (revs 180-209; rollback dist/Bash.exe.rev179); suite 111/111 on the AOT binary both modes; live-verified; notice sent to the six sessions; RESUME anchor: publish in flight |
| 209 | 2026-10-11 | POSIX mode (--posix, -o posix, set -o posix, shopt -so posix) says it is not available, with the prefilled-issue link, and goes on in bash's default mode (it was silently ignored); suite 111/111 both modes; not deployed |
| 208 | 2026-10-11 | Restricted mode is refused instead of silently ignored: -r, --restricted and rbash refuse to start, set -r ends the shell (a subshell only itself), status 2, with a link to a prefilled GitHub issue (feature and build only, never the command); eval/$( ) error lines stay physical (the architect); suite 111/111 both modes; battery 226/237; not deployed |
| 207 | 2026-10-11 | Compat battery: probe 145 leaves the baseline (Git Bash 4.4 gives eval an unclosed quote status 1; GNU 5.1 gives 2, the 2026-10-03 target); 226/237 against 225, no regression |
| 206 | 2026-10-11 | RESUME anchor: mark item 6 P5 done (the rev 205 edit missed its line) |
| 205 | 2026-10-11 | Item 6 phase 5: the frozen benchmark, rev 200 vs rev 204 AOT, every row within noise (both tables in results.md); suite 110/110 on the rev 204 AOT build both modes; item 6 done except eval line numbers, which are with the architect; RESUME anchor: next is restricted mode |
| 204 | 2026-10-11 | Item 6 phase 4 (statuses): every eval syntax error is status 2 as bash 5.1 (an unclosed array literal 1; 14 of 15 kinds were 1), correcting the 2026-09-04 record; the line-number half was falsified by measurement and is back with the architect; suite 110/110 both modes; not deployed |
| 203 | 2026-10-11 | Item 6 phase 3: a syntax error inside $( ) or backquotes is met when the substitution runs, as bash 5.1: $( ) abandons the top-level command (1) and the next runs, contained by ( ), pipeline stages, $( ), <( ), background jobs; backquotes fail the substitution (2); source/eval are their own top level; -n no longer looks inside; expected = GNU via WSL; suite 110/110 both modes; battery 227/237; -n corpus 844/912; not deployed |
| 202 | 2026-10-11 | Item 6 phase 2: the lexer produces tokens as the parser asks for them, so text after a top-level exit is never lexed (20 MB installers: 268/520 ms and working, were 1.3 s or failing; bash 4 ms); suite 109/109 both modes; battery 227/237; -n corpus 843/912 unchanged; not deployed |
| 201 | 2026-10-11 | Item 6 phase 1: scripts, -c, source and eval run one top-level command (one line) at a time, as bash: the commands before a syntax error run, a lexing error waits until reached, nothing after exit is parsed; a top-level list ends at a newline; expected = GNU via WSL; suite 109/109 both modes; battery 227/237; -n corpus 843/912 unchanged; not deployed |
| 200 | 2026-10-11 | RESUME anchor: item 6 build plan (one command at a time) written for ratification: measured 20 MB installer payloads (bash 4 ms; C#Bash 1.3 s or fails), eval statuses (bash 2 for a token error, ours 1); five phases with acceptance criteria, out-of-scope items named; not built |
| 199 | 2026-10-11 | DECISIONS: a feature C#Bash lacks prints a prefilled GitHub issue link, never an automatic post (a write token in an MIT binary; telemetry without consent); bash -r / --restricted refuse to start (silently ignored today); both decided by the architect 2026-10-11, not built; RESUME anchor updated |
| 198 | 2026-10-11 | jq takes short options after the filter and files (jq . f -c read -c as a file), and says "Could not open file x" as jq 1.6; item 6 ruled by the architect (match bash: one command at a time; a parse-first mode only opt-in), recorded, not built; expected = jq 1.6 via WSL; suite 108/108 both modes; battery 227/237; not deployed |
| 197 | 2026-10-10 | jq -R / --raw-input (each line a string) and -Rs (the whole input one string), as jq 1.6: inputs are one text, errors at file:line; strings deferred; two jq slips and the U+FFFD question recorded, not fixed; expected = jq 1.6 via WSL; suite 108/108 both modes; not deployed |
| 196 | 2026-10-10 | umask, times and fc are builtins (they were 127; fc ran Windows' fc.exe): umask as bash 5.1's umask.def (octal, symbolic, -p, -S, all-or-none, per shell), stored and printed only (no mode bits on Windows; README says so); times from the shell's and its children's CPU; fc checks options then says not supported; expected = GNU via WSL; suite 107/107 both modes; battery 227/237; not deployed |
| 195 | 2026-10-10 | awk -v name=value and name=value operands take octal and \x escapes, as gawk (-v s='\101\303\251' is Ae-acute); an operand with a backslash in its value is an assignment, not a file; found at rev 194; expected = gawk 5.1 via WSL; suite 106/106 both modes; not deployed |
| 194 | 2026-10-10 | awk string escapes above 0x7F are bytes, as in gawk ("\xc3\xa9" wrote c3 83 c2 a9, "\377" wrote c3 bf); bytes spelling a UTF-8 character are that character, across a concatenation too; found at rev 193; expected = gawk 5.1 via WSL; suite 106/106 both modes; not deployed |
| 193 | 2026-10-10 | awk subset extended, as gawk 5.1: user functions (locals, arrays by reference, untyped args become caller arrays, recursion with a stack guard: ~900 deep on AOT, fatal not a crash), print/printf > file and >> file, getline [var] < file, close, fflush, 3-argument match, --version (never "GNU Awk"); requested by installer-79, getline on the architect's word; expected = gawk 5.1 via WSL; suite 105/105 both modes, Release and AOT; battery 227/237; not deployed |
| 192 | 2026-10-10 | RESUME anchor: daq-d1 and koliada-net-6a show 'nothing needs action from me' (the architect); deploy-notice replies closed |
| 191 | 2026-10-10 | RESUME anchor: TRUE STATE names the code rev (188) instead of the anchor's own rev, which every anchor commit made stale |
| 190 | 2026-10-10 | RESUME anchor, second hand-off sweep: emptied open-defects heading now says its three items were fixed (read -z re-checked against GNU); new unanalysed observation: $0 under -c is a full backslash path when started by name through PATH; START HERE names AWAITING items unambiguously (GREENLIT also has an 'item 4'); awk probe recorded |
| 189 | 2026-10-10 | RESUME anchor: session cut after its third compaction; START HERE block; awk (item 3) plan written down, not built (a 9-line AST edit reverted); item 5 has no position yet; benchmark method line updated for rev 184; TRUE STATE to rev 189 |
| 188 | 2026-10-10 | Carried bytes that spell a UTF-8 character are that character, as bash in a UTF-8 locale ($'\xc3\xa9' had length 2, failed == e-acute, named files caf??.txt); found by the HGENCODING test; expected = GNU via WSL; suite 104/104 both modes; not deployed |
| 187 | 2026-10-10 | PYTHONIOENCODING=utf-8 exported unless set (piped py crashed on non-cp1252 text); HGENCODING measured harmful (hg commit -m with non-ASCII aborts) and dropped; suite 103/103 both modes; not deployed |
| 186 | 2026-10-10 | A Windows program gets MSYS-form path arguments in Windows form (/c/x -> C:/x, /tmp/x -> temp; start of an argument, existing drive, not MSYS programs; off with MSYS_NO_PATHCONV / MSYS2_ARG_CONV_EXCL) as Git Bash does; decided by the architect; suite 102/102 both modes; not deployed |
| 185 | 2026-10-10 | RESUME anchor: koliada-net-6a given koliada-net-f7 items (env fix deployed, strings awaiting) |
| 184 | 2026-10-10 | Benchmark method: the startup row is the median of 5 samples (each 100 launches); one sample moved ~1.5 ms between runs; ratified by the architect; logic verified with a fake timer, not run |
| 183 | 2026-10-10 | RESUME anchor: pushed to GitHub (git 694da54 = hg rev 182); README table is the 2026-10-10 rev 179 run |
| 182 | 2026-10-10 | GitHub sync at rev 182: README (2026-10-10 table from compare3 on rev 179; feature list, 101 cases, 6.8 MB, layout), DECISIONS (the +1.2 ms startup was noise), anchor |
| 181 | 2026-10-10 | Doc review for the push: IMPLEMENTATION file map (SyntaxMessage, ShellFile, ChildLauncher, jq files were missing; roles updated), ARCHITECTURE (error prefix, ChildLauncher handles, shared file offset); benchmark results 2026-10-10; anchor: startup +1.2 ms (awaiting), jq -R request |
| 180 | 2026-10-10 | RESUME anchor: rev 179 deployed (revs 158-179; rollback .rev156); six sessions told; nupkg-28 and adc-framework-d8 confirmed clean |
| 179 | 2026-10-10 | A syntax error at the end of a text with no final newline is reported at the line after the last (bash -c 'echo a &&' said line 1, bash line 2); found verifying the deploy notice; expected = GNU via WSL; suite 101/101 both modes |
| 178 | 2026-10-10 | jobs in bash format with -l -p -r -s and job specs (all ignored; jobs -p printed whole lines); a pipeline stage lists the parent jobs (was empty); expected = GNU via WSL; suite 101/101 both modes; not deployed |
| 177 | 2026-10-10 | wait -n waits for the next job and returns its status (127 with none), wait -n ID, wait -p VAR (were ignored: job pools ran unbounded); event-driven, no polling; expected = GNU via WSL; suite 100/100 both modes; not deployed |
| 176 | 2026-10-10 | exec 3>&1 / 4>&2 / 3<&0 copy a standard fd (>&3 was Bad file descriptor; out=$(cmd 2>&1 >&3) failed); closing a copy leaves the other open (exec 1>&3 3>&- disposed fd 1 and a later 4>&1 crashed the shell); expected = GNU via WSL; suite 99/99 both modes; not deployed |
| 175 | 2026-10-10 | Linux device files exist (/dev/null zero random urandom stdin stdout stderr fd/N tty) as operands, redirects and under test: cat /dev/null, head -c 5 /dev/zero, diff -u /dev/null new, cp /dev/null f, [ -e /dev/null ] failed; tr streams; od offset for empty input; expected = GNU via WSL; suite 98/98 both modes; not deployed |
| 174 | 2026-10-10 | A UTF-8 char cut by a 4096-byte read block or split pipe writes is one char (read, pipe, mapfile, while read gave two raw bytes): ShellEncoding gets a carrying decoder; expected = GNU via WSL; suite 97/97 both modes; not deployed |
| 173 | 2026-10-10 | A program run while the shell reads a file shares the offset: while read -r f; do prog; done < list ended after one line (rest pumped into prog, or lost to its own < /dev/null); a C#Bash child gives back its read-ahead at exit; expected = GNU via WSL; suite 96/96 both modes; not deployed |
| 172 | 2026-10-10 | Syntax errors in bash words at bash lines (near unexpected token, unexpected end of file, looking for matching, [[ ]] forms); grammar bash rejects is rejected (then fi, { }, a && at EOF, f() echo x, [[ -f ]], unclosed backquote); heredoc EOF warning; expected = GNU via WSL; suite 95/95 both modes; not deployed |
| 171 | 2026-10-10 | Builtins check options as bash does: invalid option + bash usage line (2), options end at the first operand; printf/cd/pwd/export/readonly/unset/alias/unalias/source/pushd/popd/dirs/trap/history/jobs/wait took -Z as operand or ignored it; kill -Z signalled; trap -- cmd SIG and source -- f failed; expected = GNU via WSL; suite 94/94 both modes; not deployed |
| 170 | 2026-10-10 | trap: bash Linux signal specs only (BADSIG and 65 were accepted), every valid one set past a bad one, RTMIN+n/RTMAX-n; trap -p in bash spelling (SIGINT), order and operands; expected = GNU via WSL; suite 93/93 both modes; not deployed |
| 169 | 2026-10-10 | ls: a heading for each directory whenever there is more than one operand, a failing one included (ls dir missing printed none); directory operands in sort order; expected = GNU via WSL; suite 92/92 both modes; not deployed |
| 168 | 2026-10-10 | Errors carry bash prefix script: line N: (BASH_SOURCE[0] or $0; eval/-c input names on syntax errors) where all said bash:; eval and $( ) text continues the caller line numbers (was 1); expected = GNU via WSL; suite 91/91 both modes; not deployed |
| 167 | 2026-10-10 | FUNCNAME / BASH_SOURCE / BASH_LINENO are one frame stack as in bash (source dropped the script; a function had no file; no BASH_LINENO; FUNCNAME was (main) at top level), written lazily; expected = GNU via WSL; probe 199 leaves the battery baseline (Git Bash 4.4 quirk); suite 90/90 both modes; not deployed |
| 166 | 2026-10-10 | One construct matcher (Lexer.ConstructEnd, bash parse_matched_pair) replaces seven scanners of ${ } $( ) $(( )) backquotes and quotes; 13 of 42 audit cases were wrong; { and } are words after the command name (expected = GNU via WSL); suite 89/89 both modes; not deployed |
| 165 | 2026-10-10 | Lexer: a nested ${ inside an unquoted ${…} was counted twice, so the token swallowed the rest of the script silently (git-prompt.sh); unclosed ${ is an error (expected = GNU via WSL); suite 88/88 both modes; not deployed |
| 164 | 2026-10-10 | A Windows exit code becomes a 0-255 status as on Linux (low byte; NTSTATUS crash = 128+signal); $? was the raw code; suite 87/87 both modes; not deployed |
| 163 | 2026-10-10 | fold is a port of GNU fold.c (\r \b tab columns, bytes, -s, no added newline); tee takes /dev/null /dev/stdout /dev/stderr (expected = GNU via WSL); suite 86/86 both modes; not deployed |
| 162 | 2026-10-10 | date: %N width is a precision (%3N printed 9 digits), - and _ flags as GNU; -d @epoch.fraction (expected = GNU via WSL); suite 85/85 both modes; not deployed |
| 161 | 2026-10-10 | A stray fi/done/esac/then/do/else/elif is a syntax error, status 2 (ran as a command, 127; bash -n passed it) (expected = GNU via WSL); suite 84/84 both modes; not deployed |
| 160 | 2026-10-10 | PATH lookups print the on-disk name (cmd.exe, not cmd.EXE); hash: bash's hits table, -t -l -d -p; suite 83/83 both modes; not deployed |
| 159 | 2026-10-10 | MSYS programs (Git usr/bin) under >> and the shell's own redirects get a write handle at the end (the append-only handle made every write fail); Windows programs keep atomic append; suite 82/82 both modes; not deployed |
| 158 | 2026-10-10 | An unquoted \\ in a case / [[ == ]] / =~ pattern is a literal backslash (*\\* matched a*); other \\ uses unchanged (expected = GNU via WSL); suite 81/81 both modes; not deployed |
| 157 | 2026-10-10 | RESUME anchor: rev 156 deployed (four more fixes; rollback .rev150); five running sessions notified, behaviour changes first |
| 156 | 2026-10-10 | env: any NAME=VALUE is an assignment (STDOUT.OutputLevel=trace); -u and -i reach builtins and programs; -uNAME, --unset=, -, -0 (koliada-net-f7's report; expected = GNU via WSL); suite 80/80 both modes; not deployed |
| 155 | 2026-10-10 | RESUME anchor: koliada-net-f7's env report verified (non-identifier NAME=VALUE run as the command; same function as env -u); strings asked for |
| 154 | 2026-10-10 | $HOME, ~, $BASH and command -v/type/which/hash output are forward-slash paths (were backslash), per the 2026-09-11/12 path-form rule; suite 79/79 both modes; not deployed |
| 153 | 2026-10-09 | set -u is enforced (was accepted, never applied), with bash's exit statuses; ${x:-word} expands its word only when selected (expected = GNU via WSL); suite 78/78 both modes; not deployed |
| 152 | 2026-10-09 | A declaration with no value leaves the variable unset: local j was ""; export of an unset name gave children ""; arrays without element 0 read as set (expected = GNU via WSL); suite 77/77 both modes; not deployed |
| 151 | 2026-10-09 | RESUME anchor: rev 150 deployed (df + four defect fixes; rollback .rev143); five running sessions notified |
| 150 | 2026-10-09 | Assignment values expand a tilde after = and : (x=~/y, PATH=$PATH:~/bin, local/export/a[0]= values; was never expanded) (expected = GNU via WSL); suite 76/76 both modes; not deployed |
| 149 | 2026-10-09 | RESUME anchor: hg revision line |
| 148 | 2026-10-09 | ${x:-word}/${x:+word}: the word is expanded in the ${ }'s own context ("${v:+$v }" lost its space; ${v:+-o "$w"} split "$w"; ${1+"$@"}) (expected = GNU via WSL); suite 75/75 both modes; not deployed |
| 147 | 2026-10-09 | A declaration builtin's name=$v argument is one field (local a=$1 split "x y"; also declare/typeset/export/readonly, name+=, name[i]=) (expected = GNU via WSL); suite 74/74 both modes; not deployed |
| 146 | 2026-10-09 | Literal text is never field-split: a\ b/x* was two words; IFS chars in literal text; a tilde home with a space (expected = GNU via WSL); suite 73/73 both modes; not deployed |
| 145 | 2026-10-08 | RESUME anchor: the global CLAUDE.md df line corrected |
| 144 | 2026-10-08 | RESUME anchor: df deployed (dist = rev 143 tree, rollback .rev136); five running sessions notified |
| 143 | 2026-10-08 | RESUME anchor: escaped-space probe done, a real defect (a word with an escaped space and a glob is split at the space); df not deployed |
| 142 | 2026-10-08 | df: in-process, GNU 8.32's format over Windows volumes (goal-2 item 5); 76/76 identical to GNU via WSL, suite 72/72 both modes (Release and AOT); not deployed |
| 141 | 2026-10-08 | RESUME anchor rewritten: true state, greenlit df, three items awaiting the architect, open defects re-verified on rev 136; resolved items dropped (they live in DECISIONS) |
| 140 | 2026-10-06 | GitHub sync at rev 140: README (article link, 2026-10-06 table from the direct-launch benchmark), .gitignore, article image; anchor |
| 139 | 2026-10-06 | Benchmark launches every shell directly (tools/benchtimer: CreateProcessW, high-resolution clock), not through Git Bash's time; first run logged |
| 138 | 2026-10-06 | compare3.sh: Git Bash is a configured path (F:/Tools/PortableGit/usr/bin/_bash.exe), fatal if missing; no searching |
| 137 | 2026-10-06 | RESUME anchor: rev 136 deployed, benchmark frozen with its first runs logged; two open calls (startup-row launcher, UTF-8 for externals); repair two lines garbled in rev 125 |
| 136 | 2026-10-06 | Freeze the benchmark: compare3.sh encodes its method (best of 9/3, startup as 100-launch totals), --cs for undeployed builds, results log |
| 135 | 2026-10-06 | CORRECTION: the jq build has no startup cost; the +1 ms was a best-of-21 artefact on a 1 ms clock |
| 134 | 2026-10-05 | RESUME anchor: jq verified, encoding fix committed; deploy awaits the +1 ms startup call |
| 133 | 2026-10-05 | jq subset verified byte-identical against jq 1.6 and tuned to it; type filters; jq_messages case |
| 132 | 2026-10-05 | Builtins write UTF-8 with no console (Claude Code got cp1252); ${#s} and substrings count characters |
| 131 | 2026-10-05 | RESUME anchor: jq subset built (rev 130), blocked on the jq 1.6 oracle in WSL |
| 130 | 2026-10-05 | jq: in-process subset (option c); unsupported filters defer to a PATH jq or fail loudly |
| 129 | 2026-10-05 | RESUME anchor: jq subset proposal awaiting the architect |
| 128 | 2026-10-05 | RESUME anchor: rev 127 deployed (three peer-reported defects fixed); jq next |
| 127 | 2026-10-05 | Basic-regex mid $/^ and leading * are literals; grep -P \K; diff - reads stdin |
| 126 | 2026-10-05 | RESUME anchor: README performance numbers re-checked on rev 124 (hold); grep -P \K and sed BRE defects open |
| 125 | 2026-10-05 | RESUME anchor: rev 124 deployed; Q3 done |
| 124 | 2026-10-05 | A detached child gets its own hidden console: a shared one died with the outer shell's job |
| 123 | 2026-10-05 | A nested shell's detached child leaves the outer kill job (BREAKAWAY_OK + CREATE_BREAKAWAY_FROM_JOB) |
| 122 | 2026-10-05 | RESUME anchor: rev 121 deployed; Q3 nested-shell spike results awaiting |
| 121 | 2026-10-05 | A background job runs on its own copy of the shell taken at &; kill %n ends its loop; job numbers restart |
| 120 | 2026-10-04 | RESUME anchor: drop the stale in-flight phase-2 block |
| 119 | 2026-10-04 | RESUME anchor: revs 116-118 committed, deploy awaiting the perf call |
| 118 | 2026-10-04 | Command-word fast path cached per node: rev 116 had made `[` 20x slower (caught before deploy) |
| 117 | 2026-10-04 | sleep and tail -f wait on interrupt events instead of Thread.Sleep polling; tail -f ends quietly |
| 116 | 2026-10-04 | "$@" and an unquoted $cmd in command position are field-split (were one word since rev 80 or earlier) |
| 115 | 2026-10-04 | RESUME anchor: rev 114 deployed and live-verified; open items from phase 2 |
| 114 | 2026-10-04 | External children get the real file handle; redirects apply left to right; one launcher |
| 113 | 2026-10-04 | Background children outlive the shell: nohup, setsid, disown; foreground ones still die with it |
| 112 | 2026-10-04 | RESUME anchor: rev 111 deployed (subshell isolation); redirect rewrite next |
| 111 | 2026-10-04 | Options, shopts, traps and aliases set in a subshell no longer leak; its own EXIT trap fires as it ends |
| 110 | 2026-10-04 | RESUME anchor: rev 109 deployed; subshell leak class (options, traps, aliases) awaiting |
| 109 | 2026-10-04 | Functions defined in ( ), $( ) or <( ) no longer leak into the parent shell |
| 108 | 2026-10-04 | Deployed rev 107 to dist (time keyword, function shadowing); redirect redesign ratified |
| 107 | 2026-10-04 | RESUME anchor: 2026-10-04 state (revs 105-106 undeployed, phase 2 in flight, redirect fork awaiting) |
| 106 | 2026-10-04 | A function named after a builtin utility now shadows it (bash order: function, builtin, PATH) |
| 105 | 2026-10-04 | time [-p] pipeline: a reserved word with TIMEFORMAT, children CPU counted (was "command not found") |
| 104 | 2026-10-04 | DECISIONS/PROJECT_CONTEXT: goal-2 scan (1,271 PowerShell calls) and the proposed backlog |
| 103 | 2026-10-03 | DECISIONS/PROJECT_CONTEXT: rev 102 (cp mtime, escaped globs) deployed and verified live |
| 102 | 2026-10-03 | PROJECT_CONTEXT/IMPLEMENTATION: cp and escape fixes recorded; the per-field glob limitation stated |
| 101 | 2026-10-03 | A backslash-escaped glob/brace/tilde character stays quoted: echo \*, find -name \*.txt (globbed) |
| 100 | 2026-10-03 | cp without -p gives the copy the current mtime (kept the source time; a restored file left MSBuild stale) |
| 99 | 2026-10-03 | DECISIONS/PROJECT_CONTEXT: rev 98 (pattern operators) deployed and verified live |
| 98 | 2026-10-03 | IMPLEMENTATION: ExpandOperand row; the stage row no longer claims a process-wide cwd |
| 97 | 2026-10-03 | Pattern operators expand their operands: ${p#"$s"}, ${p//$o/$n}, ${p%%"lit"} (were used raw, silently returning the input) |
| 96 | 2026-10-03 | DECISIONS/PROJECT_CONTEXT: performance re-tested after both fixes (all within noise); rev 95 deployed and verified live |
| 95 | 2026-10-03 | Each shell has its own working directory: a stage's or script's cd no longer moves anyone else |
| 94 | 2026-10-03 | Array element assignment with an expanded index: map[$k]=v and m+=(["$k"]=v) (ran as a command / set nothing) |
| 93 | 2026-10-03 | Deploy stage isolation (rev 91 code); compare3.sh reaches WSL via wsl.exe; README table re-measured (README itself rides the next push) |
| 92 | 2026-10-03 | DECISIONS: README performance table re-measured rev88 vs rev91 (no row beyond noise); arr[$var]=v defect found |
| 91 | 2026-10-03 | Every pipeline stage runs on its own interpreter, as bash runs each stage in a subshell |
| 90 | 2026-10-03 | DECISIONS: stage-isolation spike measured (clone ~2.3 us/stage, 1.8% of a pipeline); proposal awaiting the architect; snapshot parse defect found |
| 89 | 2026-10-03 | DECISIONS/PROJECT_CONTEXT: rev 88 deployed to dist and verified live |
| 88 | 2026-10-03 | Commands sharing one stdin keep its data; head -c streams exactly N bytes (yes \| head -c 5 hung) |
| 87 | 2026-10-03 | DECISIONS/PROJECT_CONTEXT: rev 86 deployed to dist and verified live |
| 86 | 2026-10-03 | CR is data in every line-reading tool; no BOM sniffing; child streams byte-transparent |
| 85 | 2026-10-03 | DECISIONS/PROJECT_CONTEXT: rev 84 deployed to dist and verified live |
| 84 | 2026-10-03 | ext 2>&1 with an inherited stdout puts the child's stderr on the shell's stdout |
| 83 | 2026-10-03 | DECISIONS/PROJECT_CONTEXT: rev 82 deployed to dist and verified live; stdin finding corrected |
| 82 | 2026-10-03 | Console-less shell: an external with inherited stdio no longer loses its output |
| 81 | 2026-09-14 | No console window flashes for commands run from Claude Code's Bash tool |
| 80 | 2026-09-12 | Ship build is Native AOT; benchmark harness bugs fixed and the table re-measured |
| 79 | 2026-09-12 | DECISIONS: correct the AOT/R2R crossover - ~1.2M shell operations, not 0.4s of work |
| 78 | 2026-09-12 | Native AOT works (startup 0.029s, matches Git Bash) and the Zig apphost measured |
| 77 | 2026-09-12 | PROJECT_CONTEXT: ship-ready state at rev 76; regenerate history artefacts after any later commit |
| 76 | 2026-09-12 | README: three-way performance comparison (C#Bash vs Git Bash vs WSL bash), losses included |
| 75 | 2026-09-12 | docs: ShellEncoding and the path model in ARCHITECTURE/IMPLEMENTATION; README counts; gitignore dist and the bench fixture |
| 74 | 2026-09-12 | bench: find benchmark walks a generated bounded tree, not the repository |
| 73 | 2026-09-12 | Ship build is ReadyToRun; three-way benchmark harness; history and bundle regenerated |
| 72 | 2026-09-12 | One byte-transparent encoding for the whole shell: printf byte escapes, binary through variables and captures |
| 71 | 2026-09-12 | Path form is forward slash in both directions; IO failures loud at every entry point |
| 70 | 2026-09-11 | DECISIONS: correction - the rev-68 run completed (exit 0); and the missing stub was Defender, not MSBuild cleaning |
| 69 | 2026-09-11 | DECISIONS: the "C#Bash crash" was Defender killing the installer app, not the shell |
| 68 | 2026-09-11 | pwd and friends return the forward-slash Windows form (supersedes decision 3); file-open failures no longer kill the shell |
| 67 | 2026-09-11 | Sharpened: /tmp resolves to two different places inside one session (builtin vs native child) |
| 66 | 2026-09-11 | Measured: dotnet publish slowness is the UNC path, not the shell; MSYS arg conversion gap recorded |
| 65 | 2026-09-11 | PROJECT_CONTEXT: resume anchor - dotnet publish slowness under a UNC cwd (in flight, measured local cwd fast) |
| 64 | 2026-09-11 | PROJECT_CONTEXT: rev 63 confirmed by installer-79 on real binaries; its byte suite offered for the surrogateescape work |
| 63 | 2026-09-11 | Binary data through stdin: byte builtins read the raw pipe/file (was UTF-8 decoded and re-encoded) |
| 62 | 2026-09-08 | Lexer: # is a comment only at the start of a word ($((10#0010)) truncated the line) |
| 61 | 2026-09-08 | PROJECT_CONTEXT: probe-authoring note (eval isolates parse-time defects); rev 60 installed on GUY-WINDOWS11 |
| 60 | 2026-09-08 | CSHARPBASH_BUILD / CSHARPBASH_REV variables; a lone $ is literal (=~ ^...$ and a$ operands parsed wrong) |
| 59 | 2026-09-08 | sed: 0,/re/ re-opened its range on every line (first-occurrence patch became a global rewrite) |
| 58 | 2026-09-08 | Globs on UNC paths: //host/share/ is the root (matched nothing before) |
| 57 | 2026-09-05 | head/tail -z: NUL-terminated records (were accepted and ignored) |
| 56 | 2026-09-05 | README: reproduce from a script file, not PowerShell -c (quote stripping); reply addendum |
| 55 | 2026-09-05 | Nested $( ) anywhere inside $(( )); unseparated subshell is a syntax error (web-d6 second report) |
| 54 | 2026-09-05 | launchSettings: debug profile runs --version (architect edit) |
| 53 | 2026-09-05 | --version: line 2 wording (architect edit); re-release |
| 52 | 2026-09-05 | --version: no GNU bash claim, no FSF copyright; honest C#Bash line |
| 51 | 2026-09-05 | Defect report 2026-09-05 (web-d6): nested $( ) inside $(( )) keeps its text; backslash-newline is no word; builds stamped with hg id |
| 50 | 2026-09-04 | Loop cost: P1 regression attributed per node and mostly recovered (489 -> ~430 ns/it, 1092 -> 836 B/it) |
| 49 | 2026-09-04 | Benchmarks: coreutils/pipeline/find scripts + compare.sh; single-file deployable; loop-cost bisect recorded |
| 48 | 2026-09-04 | PROJECT_CONTEXT: 2026-09-04 performance re-measurement vs Git Bash (wall clock, coreutil spawn cost, in-process harness) |
| 47 | 2026-09-04 | README: placing C#Bash as Claude Code's shell; dist/ ignored; anchor readiness note |
| 46 | 2026-09-04 | pkill: second self-protection rule (ppid chains break at MSYS exec); rc=1 when nothing was killed |
| 45 | 2026-09-04 | tests: awk case removes the out.txt a real gawk creates when generating the reference |
| 44 | 2026-09-04 | Ratified: awk print > "/dev/stderr"/"/dev/stdout"; in-process pgrep, pkill, ps |
| 43 | 2026-09-04 | P5 verified: Claude Code driven by C#Bash on a Git-less PATH (docs) |
| 42 | 2026-09-04 | P3+P4: strict coreutil options with fall-through, tool families rewritten, awk subset, <( ), trap ERR |
| 41 | 2026-09-04 | P2: never-hang pipeline model - per-thread console, managed pipes, SIGPIPE emulation, job object |
| 40 | 2026-09-04 | P1 tail: decision-3 spike (native pwd accepted), no-console startup fix |
| 39 | 2026-09-04 | P1: Claude Code invocation contract + language core |
| 38 | 2026-09-04 | tests: P0 - Claude Code compatibility battery (differential vs reference bash, ratcheted baseline) |
| 37 | 2026-09-04 | docs: Claude Code compatibility review - RESUME anchor, decisions 1-7, plan P0-P5 |
| 36 | 2026-06-16 | publish: hoist docs to repo root; brand as C#Bash; add README, MIT LICENSE, .gitignore, .gitattributes |
| 35 | 2026-06-15 | docs: add ARCHITECTURE.md + IMPLEMENTATION.md (with SVG diagrams) |
| 34 | 2026-06-15 | chore: repo cleanup + retire project handoff |
| 33 | 2026-06-14 | coreutils: sed (s///g/i/N, d, p, addresses) + which; grep/sed default to BRE |
| 32 | 2026-06-14 | coreutils: grep/egrep/fgrep (-i/-v/-n/-c/-l/-w/-F/-E/-r/-q/-h/-H/-e) |
| 31 | 2026-06-14 | docs: DECISIONS entry for wave-4 coreutils scope & per-tool simplifications |
| 30 | 2026-06-14 | builtin: hash + PATH-resolution cache for ExecExternal |
| 29 | 2026-06-14 | coreutils: diff (LCS, GNU normal format; -q/-i) — wave-4 complete |
| 28 | 2026-06-14 | coreutils: xargs (-n/-0/-I TOKEN; default echo) |
| 27 | 2026-06-14 | coreutils: ls (scoped: -a/-A/-r/-d, one-per-line) |
| 26 | 2026-06-14 | coreutils: find (-name/-iname/-type/-maxdepth, pre-order walk) |
| 25 | 2026-06-14 | coreutils: split (-l lines / -b bytes[k\|m\|g], prefix+aa/ab/...) |
| 24 | 2026-06-14 | fix: builtin 2>&1 (stderr->stdout merge was a no-op) |
| 23 | 2026-06-14 | fix: external-command redirects (>file no longer crashes; 2>file/2>/dev/null work) |
| 22 | 2026-06-14 | coreutils: sort (-n/-r/-u/-f, -k single-field, -t delim) |
| 21 | 2026-06-14 | coreutils: od (-c/-tx1/-b/-An; default octal words) |
| 20 | 2026-06-14 | coreutils: du (-s/-b/-h) |
| 19 | 2026-06-14 | expr builtin + FIX quoted glob/brace expansion |
| 18 | 2026-06-14 | wave 4b: date, kill + fix /dev/null, fd-aware redirects, error-continue |
| 17 | 2026-06-14 | coreutils wave 4a: uname, hostname, factor, cal |
| 16 | 2026-06-14 | coreutils wave 3: rm, mv, cp (+ fix test ! negation) |
| 15 | 2026-06-14 | coreutils wave 2 finish: touch, rmdir, cmp, tee, comm, paste |
| 14 | 2026-06-14 | fix: builtin-pipeline race (run sequentially) + CRLF output (LF) |
| 13 | 2026-06-14 | coreutils wave 2: tr, cut, uniq, nl, fold |
| 12 | 2026-06-14 | coreutils wave 2: head, tail, wc, rev, tac |
| 11 | 2026-06-14 | coreutils wave 2: byte-faithful stdout accessor + cat |
| 10 | 2026-06-14 | docs: SysV coreutils classification + build-wave plan |
| 9 | 2026-06-14 | coreutils builtins tier 1: basename, dirname, seq, mkdir |
| 8 | 2026-06-14 | perf: single bare $var expansion fast-path |
| 7 | 2026-06-14 | perf+fix: rewrite ArithParser inline; fix && / \|\| crash and 0x hex |
| 6 | 2026-06-14 | Shebang dispatch: run #! scripts as commands (./foo.sh) |
| 5 | 2026-06-14 | docs: record perf round 1 (literal fast-path, arith op-tables) + harness notes |
| 4 | 2026-06-14 | perf: cut per-iteration allocation in the expansion/arith hot path |
| 3 | 2026-06-14 | Add test suite, manual checklist, and perf harness |
| 2 | 2026-06-14 | Phases 6-10: line editor, startup/prompt, traps, job control, positionals |
| 1 | 2026-03-11 | Phase 5 testing |
| 0 | 2026-03-11 | First cut |
