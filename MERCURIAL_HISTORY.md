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

Authored by `Guy <guy@koliada.com>`, co-authored by Claude (Anthropic). 183 changesets.


| rev | date | summary |
|----:|------------|---------|
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
