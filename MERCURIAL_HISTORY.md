# Development history (Mercurial)

This project was built and version-controlled with **[Mercurial](https://www.mercurial-scm.org/)**, not git — by preference. The git repository you're reading is a published *snapshot*; the genuine, commit-by-commit record lived in hg.

The full history is preserved two ways:

- **Readable** — the table below (`hg log`, newest first).
- **Complete** — `mercurial-history.hg`, an `hg bundle --all` of the entire repository. Recover it with:
  ```sh
  hg init csharpBash && cd csharpBash
  hg unbundle ../mercurial-history.hg
  hg update
  ```

Authored by `Guy <guy@koliada.com>`, co-authored by Claude (Anthropic). 81 changesets.


| rev | date | summary |
|----:|------------|---------|
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
| 29 | 2026-06-14 | coreutils: diff (LCS, GNU normal format; -q/-i) � wave-4 complete |
| 28 | 2026-06-14 | coreutils: xargs (-n/-0/-I TOKEN; default echo) |
| 27 | 2026-06-14 | coreutils: ls (scoped: -a/-A/-r/-d, one-per-line) |
| 26 | 2026-06-14 | coreutils: find (-name/-iname/-type/-maxdepth, pre-order walk) |
| 25 | 2026-06-14 | coreutils: split (-l lines / -b bytes[k/m/g], prefix+aa/ab/...) |
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
| 7 | 2026-06-14 | perf+fix: rewrite ArithParser inline; fix && / // crash and 0x hex |
| 6 | 2026-06-14 | Shebang dispatch: run #! scripts as commands (./foo.sh) |
| 5 | 2026-06-14 | docs: record perf round 1 (literal fast-path, arith op-tables) + harness notes |
| 4 | 2026-06-14 | perf: cut per-iteration allocation in the expansion/arith hot path |
| 3 | 2026-06-14 | Add test suite, manual checklist, and perf harness |
| 2 | 2026-06-14 | Phases 6-10: line editor, startup/prompt, traps, job control, positionals |
| 1 | 2026-03-11 | Phase 5 testing |
| 0 | 2026-03-11 | First cut |
