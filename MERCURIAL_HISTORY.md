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

Authored by `Guy <guy@koliada.com>`, co-authored by Claude (Anthropic). 1002 changesets.


| rev | date | summary |
|----:|------------|---------|
| 1001 | 2026-11-27 | doc review for the publish of build 999: README table and startup line, PERFORMANCE (measured-on, wins; the noisy-start baseline and the rows against 885 stated), perf history (999 added, graph redrawn), results.md (the 999 run), KNOWN_ISSUES' release section (999 since hg-905; the 885 section dropped) |
| 1000 | 2026-11-27 | records: DEPLOYED 999 (bugs list 19, 20; suite 338/0; checked live); only (9) left; publish waits on wombat's quiet |
| 999 | 2026-11-27 | FIXED (bugs list 19, 20): tr takes every word after its first operand as an operand, GNU's operand rules and words; -s with two sets squeezes the last set's characters after translating; case tr_operands_gnu (red on 997, mutations fail it); KNOWN_ISSUES; counts 338; DECISIONS; anchor |
| 998 | 2026-11-27 | records: DEPLOYED 997 (bugs list 13, 10's pipe case; suite 337/0; checked live); anchor |
| 997 | 2026-11-27 | FIXED (bugs list 10's pipe case, his go on Claude's position): > /dev/stdin on a pipe or a here-document refused, Permission denied, 1; KNOWN_LIMITATIONS; case dev_stdin_pipe_refused (authored; red on 993, a mutation fails it); KNOWN_ISSUES; counts 337; DECISIONS; anchor (AWAITING items done removed) |
| 996 | 2026-11-27 | FIXED (bugs list 13, web-fe; his go on Claude's position): a short option that is no letter or digit is refused in GNU's words and status by tool, never handed on (unzip exempt); env's hint line, dd's invalid option; case invalid_option_refused_gnu (red on 993, a mutation fails it); (19) tr found; KNOWN_ISSUES; counts 336; DECISIONS; anchor |
| 995 | 2026-11-27 | anchor: GREENLIT: bugs list on Claude's positions (10 pipe refused, 13 non-alphanumeric not handed on); publish when wombat and web say quiet (asked); replaces the symbolic-ref stop |
| 994 | 2026-11-27 | records: DEPLOYED 993 (bugs list 18; suite 335/0; checked live; wombat-3a told); every open bugs-list item now waits on his word |
| 993 | 2026-11-27 | FIXED (bugs list 18, wombat-3a): a native program in a here-document loop leaves the rest to the loop (HereDocReader becomes a C#Bash pipe at the first child, fed with the give-back); case heredoc_loop_child_gnu (red on 992, mutations fail it); KNOWN_ISSUES; counts 335; DECISIONS; anchor (deploy 992 recorded) |
| 992 | 2026-11-27 | FIXED (bugs list 16): a program gets a here-document over 65,536 bytes as a delete-on-close temp file, a smaller one a pipe, as bash 5.1; case heredoc_child_file_gnu (red on 990, mutations fail it); KNOWN_ISSUES; counts 334; DECISIONS; anchor |
| 991 | 2026-11-27 | records: DEPLOYED 990 (bugs list 12, 15; suite 333/0; checked live); anchor |
| 990 | 2026-11-27 | FIXED (bugs list 15): cp keeps times only for -p, -a, bare --preserve, or a list naming timestamps/all, --no-preserve in order, as GNU; case cp_preserve_times_gnu (red on 986, a mutation fails it); KNOWN_ISSUES; counts 333; DECISIONS; anchor |
| 989 | 2026-11-27 | FIXED (bugs list 12, web-d0): shopt -s/-u login_shell and -O login_shell ignored, status 0, as GNU; startup marks -l via MarkLoginShell; case shopt_login_shell_gnu (red on 986, a mutation fails it); KNOWN_ISSUES; counts 332; DECISIONS; anchor |
| 988 | 2026-11-27 | USING: the first lookup is the shell's own folder, the chain's PATH is the system PATH, what a PATH step costs (measured), WSL last wherever written; KNOWN_LIMITATIONS example in his order; DECISIONS; anchor (his request, relayed by guy-8b) |
| 987 | 2026-11-27 | records: DEPLOYED 986 (bugs list 11; 985 stopped at its gate, expected file); (17) WITHDRAWN (his correction: first find is .bash/bin, the chain's PATH is the system PATH; code right since 954), a USING change instead; KNOWN_ISSUES; anchor |
| 986 | 2026-11-27 | mv_cross_volume: expected output for the folder form (GNU's, generated at 985 but not copied in; the deploy's AOT suite caught it) |
| 985 | 2026-11-27 | FIXED (bugs list 11): mv of a folder to another drive removes the source as rm -rf (each entry kept named, rc 1; a read-only file goes), as GNU; mv_cross_volume folder form (red on 982); KNOWN_ISSUES; DECISIONS; anchor |
| 984 | 2026-11-27 | anchor: (10)'s pipe case: guy-94's data point (a Claude call whose text holds < gets Claude Code's pipe as stdin) |
| 983 | 2026-11-27 | records: DEPLOYED 982 (dev/fd kinds; suite 331/0; checked live); bugs list (17): unix-tools folders before PATH win the first lookup (his relayed ruling: fallback only), details AWAITING; KNOWN_ISSUES; session name bash-ec |
| 982 | 2026-11-27 | FIXED (bugs list 14 + 10 but its pipe case): /dev/stdin, /dev/stdout, /dev/stderr, /dev/fd/N test as what the descriptor is (-p pipe, -f/-s file, -c null; here-docs a pipe up to 65,536 bytes) and > /dev/stdin writes to /dev/null or the file itself; case dev_fd_kind_gnu (red on 979, mutations fail it); the pipe case AWAITING; (16) found; no /proc limitation; counts 331; DECISIONS; anchor |
| 981 | 2026-11-27 | anchor: (9) cal blocked on a reference (WSL has no cal; ncal needs his sudo; util-linux cal not packaged for 22.04); list goes on with (10)+(14) |
| 980 | 2026-11-27 | records: DEPLOYED 979 (cp -a hard links; suite 330/0, battery 221/237; checked live); anchor |
| 979 | 2026-11-27 | FIXED (bugs list 8): cp -a keeps hard links within one cp, as GNU (-a, -d, --preserve, --no-preserve in the order given); case cp_hardlinks_gnu (red on 963, mutations fail it); KNOWN_ISSUES (8) fixed, (15) cp --preserve=LIST keeps mtime (found); counts 330; DECISIONS; anchor |
| 978 | 2026-11-27 | anchor: hold lifted with a stop (his: re-start bug fixing, stop when Wombat reports symbolic-ref completed) |
| 977 | 2026-11-27 | (14) is new, not a re-report: -p has been false for every path since rev 39; [ -p <(cmd) ] false is by design (2026-09-04 decision 6): KNOWN_LIMITATIONS gains '<( ) is a file, not a pipe' (never listed); KNOWN_ISSUES (14) corrected; anchor |
| 976 | 2026-11-27 | anchor: publish's git measured (PortableGit 2.56.0.windows.4 by path); CLAUDE.md now says bare git = Wombat (his "make the switch now"); Claude's position on the scripts |
| 975 | 2026-11-27 | anchor: HOLD, his "do _not_ start a fix" (no bug fix until lifted); AWAITING: publish now from 963, Wombat as publish's git (Claude's positions) |
| 974 | 2026-11-27 | KNOWN_ISSUES + bugs list (14): [ -p /dev/stdin ] false on a pipe (wombat-3a, 963; GNU 5.1 true, Git Bash 4.4 false; also -f /dev/stdin on a file); with (10): both need fd 0's kind |
| 973 | 2026-11-26 | anchor refreshed last (batten down): battened down through hg 972; 963 current, nothing to deploy; next (8) |
| 972 | 2026-11-26 | batten-down pass 7: Fallbacks comment (prepended since 956; FallbackProgram only pre-956); PROJECT_CONTEXT Test Assets (run-compat.sh, its options; the old runner's stale-kill step gone); DECISIONS |
| 971 | 2026-11-26 | batten-down pass 6: CORRECTION (the off-PATH case is FallbackProgram's own-folder look, not Windows' launcher); ARCHITECTURE/IMPLEMENTATION search order; claude.cmd comment; PROJECT_CONTEXT test section (run-tests.sh, 329); cases.tsv rows; bare reference; USING inference marked; DECISIONS |
| 970 | 2026-11-26 | batten-down pass 5 fixes: 'cannot run renamed' -> 'a lone copy ... cannot run' (3 cases, cases.tsv); tmp_ignores_tmp_var_gnu header (until 940); DEVELOPING 'percent'; DECISIONS |
| 969 | 2026-11-26 | batten-down pass 4 fixes: refusal_protocol comment (956); anchor (957's live check placed, (13)'s remedy under AWAITING); KNOWN_ISSUES 932 wording; DECISIONS |
| 968 | 2026-11-26 | batten-down pass 3 fixes: test comments (off-PATH, staging sh.exe since 960); anchor (deploy chain, NEXT PUBLISH guy-windows11 check, A4 out of AWAITING, (13)'s remedy and claude.bat TMP under his word); cases.tsv/temp_one_place 963 section; KNOWN_ISSUES spellings; DECISIONS |
| 967 | 2026-11-26 | batten-down pass 2 fixes: 877 entry superseded note; procsub/memory dates (966); pitfalls dates; anchor (955 in the deploy chain, (A2) expanded, Ctrl-G closure); DECISIONS (963's unrecorded changes, the procsub gap) |
| 966 | 2026-11-26 | batten-down pass 1 fixes: procsub's child through cmd (the 940 rule had un-isolated its leftover count); deploy.sh step list; KNOWN_ISSUES 940 note; DEVELOPING AOT reason; USING link; anchor (chain line, bugs 11-13, labels, todate feature); DECISIONS (955/957 deploys recorded) |
| 965 | 2026-11-25 | KNOWN_ISSUES + bugs list (13): an option no version of the tool has (grep -F "- x") is handed on to the chain instead of refused (web-fe, 963) |
| 964 | 2026-11-25 | records: DEPLOYED 963 (seen live: a Claude Code call reads TMP=TEMP=TMPDIR=F:/TEMP); anchor |
| 963 | 2026-11-25 | RULED (his Yes) + BUILT: a Windows path assigned to TMP, TEMP or TMPDIR is stored with forward slashes (Claude Code's per-command export now reads F:/TEMP; a backslash $TMP broke case, [[ ]], ${f#}, sed, JSON: measured); temp_one_place section (red on 960); KNOWN_LIMITATIONS' backslash entry removed; USING; KNOWN_ISSUES (963); DECISIONS; anchor |
| 962 | 2026-11-25 | KNOWN_ISSUES + bugs list (12): shopt -s login_shell changes the option (web-d0, 960; GNU ignores it); BASH_EXECUTION_STRING's second request (web-d0) |
| 961 | 2026-11-24 | records: DEPLOYED 960 (deploy.sh's staging sh.exe and help-table check proven; suite 329/0, battery 221); anchor refreshed |
| 960 | 2026-11-24 | RULED (his 1/ 2/ 3/ yes): this machine's unix-tools = his line (switched, checked); deploy.sh makes staging's sh.exe before its suite and checks every earlier help table's hash (both polarities shown); claude.bat is the guy session's; DECISIONS, anchor |
| 959 | 2026-11-24 | BUILT: the JIT build makes sh.exe beside Bash.exe (it runs there, measured; the JIT suite's sh was the installed build); deploy.sh's staging gap proposed (his); Ctrl-G closed (his, via guy-94); DEVELOPING; DECISIONS; anchor |
| 958 | 2026-11-24 | anchor: DEPLOYED 957; bug 7 note (954+956+957); his chain line awaiting (test-home check first); the JIT sh.exe question owed; Ctrl-G open items with guy-94 |
| 957 | 2026-11-24 | FIXED (957): a refusal is run again in the installed layout (NextProgram's list was recomposed with the own folder first: 956's deploy stopped on unix_tools_order); CORRECTION (guy-94): Ctrl-G's 'session log' may not be his prompt (externalEditorContext defaults off); DECISIONS |
| 956 | 2026-11-24 | TESTED + FIXED (his "every variation"; bugs list 7): the own folder first ALWAYS, whatever PATH a script sets |
| 955 | 2026-11-24 | own_dir_before_unix_tools builds the installed layout itself (the staging folder has no sh.exe; the 954 deploy stopped on it); DECISIONS |
| 954 | 2026-11-24 | FIXED (bugs list 7, from the record): this shell's own folder is searched before every unix-tools step |
| 953 | 2026-11-24 | MEASURED (bugs list 7): a unix-tools folder named before PATH outranks this shell's own bash/sh (documented rule; exposure: Git's usr/bin first); remedy awaiting him |
| 952 | 2026-11-24 | records: Ctrl-G works (his word) with EDITOR = installed Notepad++ after the stale CS-Script plugin was moved out; anchor |
| 951 | 2026-11-24 | anchor: bug (11)'s first GNU run was invalid (its /tmp argument converted for wsl.exe); rerun owed |
| 950 | 2026-11-24 | bugs list (11) UNVERIFIED: cross-volume mv with an unremovable source says only 'cannot move', not that a copy was left (found when a mv left a 50 MB copy in the repo, removed); GNU comparison owed |
| 949 | 2026-11-24 | KNOWN_ISSUES + bugs list (10): > /dev/stdin 'No such file or directory' (guy-94, 941), measured against Git Bash and GNU |
| 948 | 2026-11-24 | DOCS: USING's Wombat refusal figure 'about 3 ms' (build 127 medians 2.7-3.5 ms), aligned with Wombat's docs (wombat-46) |
| 947 | 2026-11-24 | CORRECTION: claude.bat ownership is his proposal (question form), not a ruling; awaiting his word (guy-94 caught it); anchor and DECISIONS |
| 946 | 2026-11-24 | anchor: his claude.bat is the guy session's (rule) |
| 945 | 2026-11-24 | DOCS + RULED (his): USING's claude.bat example shows TMP/TEMP for a Claude-only temp folder (both lines; why claude.bat, not a C#Bash-only setting); his ~/bin/claude.bat is the guy session's (handed to guy-94 with its C#Bash-made lines); DECISIONS |
| 944 | 2026-11-24 | anchor: one backslash in the quoted Claude Code export (sed doubled it) |
| 943 | 2026-11-24 | anchor: DEPLOYED 941; the TMP/TEMP assignment-spelling question under AWAITING THE ARCHITECT |
| 942 | 2026-11-24 | CORRECTION: Claude Code's wrapper re-exports TMP/TEMP (backslashes) in every call: KNOWN_LIMITATIONS entry restored, reworded; USING and KNOWN_ISSUES say so; DEPLOYED 941 recorded; the assignment-spelling question open for him |
| 941 | 2026-11-24 | README: case count 327 (the 940 commit's line-number edit missed: the line had moved) |
| 940 | 2026-11-24 | RULED + BUILT (his "Yes, fix all three"; bugs list 6): ONE temp folder; TMP, TEMP and TMPDIR all name it |
| 939 | 2026-11-24 | DOCS (his request, via wombat-46): USING 'Which to choose' for BASH_CLAUDE_GIT (a full git ~25 ms with the view, none nothing, Wombat's refusal ~4 ms; measured figures); README step 3 the same in one sentence; 'about five per Bash call' corrected (background polling); DECISIONS |
| 938 | 2026-11-24 | records: DEPLOYED 937 (suite 326/0, battery 220); AOT-vs-AOT cost measured (none where the prefilter cannot help; 1.50 s -> 0.015 s); owed note delivered to guy-94 (guy-bf ended); a too-early 'deployed' recorded |
| 937 | 2026-11-24 | FIXED (bugs list 5, facts-8a): grep skips a line without a literal every match must hold (GNU's must strings) |
| 936 | 2026-11-24 | MOVED (bugs list 4) to KNOWN_LIMITATIONS: cmd //c and a quoted argument match Git Bash 4.4 and 5.3 byte for byte (cmd reads its line raw); workarounds measured; not special-cased (a feature, his); anchor, DECISIONS |
| 935 | 2026-11-24 | records: DEPLOYED 934 (suite 325/0, battery 220, baseline 216; this machine's rev-553 help.json replaced, seen); anchor |
| 934 | 2026-11-24 | FIXED (bugs list 3): help.json tables of revs 553, 645, 665 recognized and replaced |
| 933 | 2026-11-24 | records: DEPLOYED 932 (suite 325/0 detached, battery 220, baseline 216); anchor (deployed state, the OWED note to guy-bf gains 932) |
| 932 | 2026-11-24 | FIXED (bugs list 2b): a request handed on to WSL that WSL lacks too gets the no-WSL answer, not "command not found" |
| 931 | 2026-11-24 | records: Ctrl-G (no Notepad here; EDITOR in his claude.bat, his yes, pending his check); the jq hand-over bug's cause found, the fix designed, its proof awaiting him; cal ignores options, KNOWN_ISSUES + bugs list (9) |
| 930 | 2026-11-24 | records: the new claude.cmd checked (his 'setup still works correctly', via guy-94; CLAUDE.md wording landed as ~/.claude 7ee1720); the 926 deploy recorded (the anchor said 914); anchor |
| 929 | 2026-11-24 | RULED (widened): no DECISIONS references in ARCHITECTURE (41), IMPLEMENTATION (9), DEVELOPING (1); review fixes (an exact empty-expansion fact, deferred-decision wording, ruling words, the term P1 surface); DECISIONS |
| 928 | 2026-11-24 | records: claude.cmd installed here (old as .prev), BASH_CLAUDE_GIT=none in his claude.bat; the replaced-while-running hazard measured and fixed; exit code not passed (pre-existing); the MCP plan noted; anchor |
| 927 | 2026-11-24 | claude.cmd: launch and exit on one line (cmd rereads a .cmd by name: a file replaced mid-session was read on from its old offset, measured: a fragment ran); README drops PROJECT_CONTEXT from its design-docs list (a process doc, his) |
| 926 | 2026-11-24 | RULED (his): user docs carry no DECISIONS references - 5 removed (README, USING x2, KNOWN_ISSUES x2) + the >( ) refusal message; BASH_CLAUDE_GIT in README step 3 and without-start-file; USING status-line sentence corrected (it runs through the shell); DECISIONS |
| 925 | 2026-11-24 | claude.cmd: the architect shortened the comment beside BASH_CLAUDE_GIT to one line (his edit, committed by bash-56; claude_cmd_git still green) |
| 924 | 2026-11-24 | claude.cmd comments (his): set BASH_CLAUDE_GIT in your own claude.bat, which no install touches; this file is rewritten at every install or upgrade (said at the top and beside the option) |
| 923 | 2026-11-24 | BUILT (his yes; his name BASH_CLAUDE_GIT): installer/claude.cmd gives Claude Code one folder on its PATH, its git (path default, found once / none / a git); case claude_cmd_git (red on the old claude.cmd); USING; counts 325; not yet in ~/.bash/bin: the status-line risk is his |
| 922 | 2026-11-24 | RULED (his): no setting, claude.cmd stops narrowing Claude Code's PATH; SPIKE 2 (no git: quiet, no Wombat calls; the Bash tool keeps Wombat first); the exact diff proposed; anchor |
| 921 | 2026-11-24 | SPIKE (his yes): Claude Code 2.1.331 finds git by PATH, GIT_BASH_PATH beats a PATH bash.exe (#94077 fixed since 2.1.316, now in DECISIONS), a refusing git tolerated, includeGitInstructions does not stop the calls; anchor |
| 920 | 2026-11-24 | records: separate-git feature - his point (dual hg+git projects are git repositories); measured 21 false modifications vs the mirror; recommendation for him: none |
| 919 | 2026-11-24 | FEATURE REQUESTED (his, via wombat-46): a separate git for Claude Code itself; found: published installs give Claude Code no git today; positions + spike first; features list |
| 918 | 2026-11-22 | records: the GIT_CONFIG_COUNT log line explained (Claude Code's own remote calls; wombat-46) |
| 917 | 2026-11-22 | records: Wombat 127 (-c allowlist) into ~/.bash/bin by the standing ruling; checked live (served, refused and handed on, Claude Code's -c form); anchor |
| 916 | 2026-11-22 | records: 914 independently confirmed by wombat-46 (3/3 handed on vs 907 loud 10/12) |
| 915 | 2026-11-22 | records: DEPLOYED 914 (suite 324/0, battery 220; live check under Claude Code's < /dev/null wrap: handed on, rc 0); wombat-46 told |
| 914 | 2026-11-22 | BUG FIXED (his "The fix; go"): an in-process < /dev/null (group, builtin, eval: Claude Code's wrapper) or a closed stdin reaches a program as NUL, so a refused program in ~/.bash/bin is marked and rerun; the 910 local diagnostic removed; refusal_protocol +4 probes (red on 910); USING, KNOWN_ISSUES, DECISIONS (+ ruling: no CLAUDE_CODE_DISABLE_GIT_INSTRUCTIONS), anchor |
| 913 | 2026-11-22 | records: CORRECTION (grounding) - bug (1)'s trigger was on record since 2026-10-03 (cited; the fix reframed as the 10-04 ratified NUL rule's missed case); A4 closed by his ruling via Wombat (keep logging; Wombat builds -c); anchor |
| 912 | 2026-11-22 | CORRECTION (wombat-46): not every < stops Claude Code's < /dev/null wrap (: '<' alone does not); KNOWN_ISSUES interim advice is only the program's own < /dev/null; DECISIONS, anchor |
| 911 | 2026-11-22 | CAUSE FOUND, bug (1): Claude Code wraps a command as eval ... < /dev/null unless its text holds a <; that in-process /dev/null stdin is counted unreplayable, so the mark is withheld (measured with the 910 diagnostic, both sides of the rule); the fix awaits his word; KNOWN_ISSUES (cause, verified workaround), DECISIONS, anchor |
| 910 | 2026-11-22 | LOCAL DIAGNOSTIC (bug (1), not for publish): BASH_RERUN_DIAG=<file> logs, per launch of a bare-name program in ~/.bash/bin, the stdin kind chosen and the mark decision (Evaluator.RerunDiag, ChildLauncher.FileTypeOf); his "local only, not published"; DECISIONS, anchor (removal first on NEXT PUBLISH) |
| 909 | 2026-11-22 | FINDING (partial), bug (1): C#Bash withholds the mark when it judges the child's stdin unreplayable, per process in stretches; not Wombat, not the form or wrapper; why not found; next step (instrument BASH_TIMING_LOG) awaits his word. KNOWN_ISSUES, DECISIONS, anchor |
| 908 | 2026-11-22 | records: DEPLOYED 907 (suite 324/0 detached, battery 220); anchor: GREENLIT (a) done, (b) next, the reporter owed |
| 907 | 2026-11-22 | BUG FIXED: select and coproc parse as bash's (declare -f prints them back) and are refused when run (the Not available refusal, status 2, counted); claude-c3 via guy-bf; cases select_coproc_parse_gnu + select_coproc_refused (red on 885); KNOWN_LIMITATIONS, KNOWN_ISSUES, DECISIONS; counts 324 |
| 906 | 2026-11-22 | records: PUBLISHED hg-905 = build 885 (the installer over a deployed ~/.bash checked first; cp -a hard-link bug found; the coproc reframe; the go's scope); resume anchor 2026-11-22 |
| 905 | 2026-11-22 | publish doc review (builds 864-885): README table + startup line and PERFORMANCE from the 885 compare3 run (2026-11-22, quiet, C#Bash host); graph adds 885; case count 322 (README was 308, missed at hg-861); KNOWN_ISSUES release section for 885, cp -a hard-link bug entered (open) |
| 904 | 2026-11-17 | KNOWN_ISSUES: coproc not implemented but type calls it a keyword; jq foreach joins def (claude-c3 via guy-bf); features list: coproc, jq foreach/def; anchor |
| 903 | 2026-11-17 | KNOWN_ISSUES: grep -E with a bounded repeat ~135x slower than GNU (facts-8a); anchor |
| 902 | 2026-11-17 | KNOWN_ISSUES: a quoted argument to cmd //c reaches cmd as backslash-quote (glassmirror-4e via guy-bf); anchor |
| 901 | 2026-11-17 | batten down, fourth audit fixed: resume block (bug (1) waits on A1, the list's order, A4, the unverified bash/sh order), KNOWN_ISSUES (log wording, seconds, help.json), KNOWN_LIMITATIONS (MSYS path conversion for every machine, guy-bf's need), USING (the not-run-again condition), case headers and rows cite DECISIONS; DECISIONS record |
| 900 | 2026-11-17 | anchor: pending message for Wombat's session (git-9d not running) |
| 899 | 2026-11-17 | batten down, third audit fixed: resume block (A1-A3 to the architect, once fixed, the switch, the publish path), KNOWN_ISSUES (bug (1) evidence, jq deferral, help.json), KNOWN_LIMITATIONS (chain order, printed or run), USING (step 3, example, the mark), RerunMark comment; DECISIONS record |
| 898 | 2026-11-17 | batten down, second audit fixed: resume block (bug (1) vs the ruling, the git line, Wombat KNOWN_ISSUES name open, guy-windows11, publish checklist), KNOWN_ISSUES (/tmp gap, jq wording, the mark's builds, the link), KNOWN_LIMITATIONS ([ -t /dev/stdin ] moved, preamble), USING (the mark), deploy.sh header, case comments; DECISIONS record |
| 897 | 2026-11-17 | batten down: the resume block rewritten (first audit's anchor findings: stale state, leftovers, refs) |
| 896 | 2026-11-17 | batten down, first audit fixed: catch-up DEPLOYED record 868-885 + the withdrawn stdin cause (DECISIONS); jq def report (KNOWN_ISSUES); chain advice PATH first (KNOWN_LIMITATIONS, USING); deploy.sh: header, message, KNOWN_ copies name the build; refusal_protocol row text |
| 895 | 2026-11-17 | batten down: KNOWN_ISSUES [ -t /dev/stdin ] (open); DECISIONS record of the session observations |
| 894 | 2026-11-17 | anchor: bugs list holds the intermittent mark; the KNOWN_ files rule |
| 893 | 2026-11-17 | RULED: KNOWN_LIMITATIONS is C#Bash's alone, every report enters KNOWN_ISSUES first; git-9d's intermittent-mark report entered (open); DECISIONS |
| 892 | 2026-11-17 | RULED: the last two bugs are limitations (KNOWN_LIMITATIONS Error wording); bugs list empty; anchor tidied (stale bugs lines, superseded (w)) |
| 891 | 2026-11-17 | RULED: a Claude session's loud Wombat refusal is fine (his words); DECISIONS, anchor |
| 890 | 2026-11-17 | his goal done: unix-tools PATH;PortableGit/cmd;WSL and Wombat 102 in ~/.bash/bin; found: Claude Code's stdin withholds the mark (open, his); DECISIONS, anchor |
| 889 | 2026-11-17 | RULED: the rerun chain stays PATH;WSL and ~/.bash/bin keeps Wombat 92 (his: No leave as is); DECISIONS, anchor |
| 888 | 2026-11-17 | anchor: the Wombat line no longer says his go is needed for the copy |
| 887 | 2026-11-17 | DECISIONS + anchor: each Wombat deploy goes into ~/.bash/bin by C#Bash (his ruling via Wombat); 102 waits on the rerun chain (PATH;WSL would send refusals to WSL git) |
| 886 | 2026-11-17 | anchor: 885 deployed (a written mark goes through); the features item done |
| 885 | 2026-11-17 | a mark written on the command itself (BASH_RERUN_ON_127=1 prog, env ...) goes through as written, by path too (RULED: the architect, Your position, go; git-9d); case refusal_protocol; USING, KNOWN_ISSUES, DECISIONS |
| 884 | 2026-11-17 | anchor: features list: a supported way to run a program as a marked call (git-9d); awaiting his ruling |
| 883 | 2026-11-17 | anchor: 882 deployed to dist and ~/.bash/bin |
| 882 | 2026-11-17 | an unclosed construct is named by the innermost one left open, as bash (Lexer.ConstructEnd records it); case unclosed_innermost_gnu; KNOWN_ISSUES (fixed; an unclosed ${ inside a word left open), DECISIONS |
| 881 | 2026-11-17 | anchor: 880 deployed to dist and ~/.bash/bin; every local session runs it |
| 880 | 2026-11-17 | deploy goes into ~/.bash/bin too, where every local session starts (the architect: deploy is supposed to deploy to ~/.bash/), and dist; KNOWN_ files copied; one rollback kept there; KNOWN_ISSUES: the apostrophe in a double-quoted ${x:+...} (open); DECISIONS, anchor |
| 879 | 2026-11-17 | anchor: deploy has not reached ~/.bash since 2026-11-07 (the architect's question, his words); proposal awaiting his go |
| 878 | 2026-11-16 | anchor: 877 deployed (/tmp ignores an MSYS-form TMP) |
| 877 | 2026-11-16 | /tmp no longer follows an MSYS-form TMP in a child shell: TempFolder (TMP/TEMP only when a full Windows path) for /tmp, mktemp, process substitution, diff (facts-8a); case tmp_ignores_tmp_var_gnu; KNOWN_ISSUES, DECISIONS |
| 876 | 2026-11-16 | anchor: Wombat 102 deployed, joint try with 874 passed; awaiting his go on ~/.bash/bin/git.exe 102 and the rerun chain |
| 875 | 2026-11-16 | anchor: 874 deployed (bad descriptor); the tools' wording left open |
| 874 | 2026-11-16 | a closed or wrong-way descriptor is bash's Bad file descriptor for in-process commands: reads fail with GNU's words and status, writes fail write error 1, stderr's messages lost; case bad_descriptor_gnu; KNOWN_ISSUES (fixed; tools' wording left open), DECISIONS |
| 873 | 2026-11-16 | anchor: 872 deployed (fd 0 output redirect); the wider class open on the bugs list |
| 872 | 2026-11-16 | an output redirect onto fd 0 (0> f, 0>> f, 0>/dev/null) for env, a builtin, a function or a group was installed as stdout; now stdin, read as empty (git-9d); case redirect_fd0_output_gnu; KNOWN_ISSUES (fixed + the wider class open), DECISIONS |
| 871 | 2026-11-16 | anchor + DECISIONS: 870 deployed (the console-host fix); the JIT first-run exit checked on AOT |
| 870 | 2026-11-16 | a tree kill (broken pipe, timeout, kill, job kill) never kills a console host: a console-less shell's first child's conhost is the shell's adopted console, and killing it left code page 437 (facts-8a); case console_survives_kill; KNOWN_ISSUES, DECISIONS |
| 869 | 2026-11-16 | anchor: 868 deployed (the refusal protocol mark) |
| 868 | 2026-11-16 | the refusal protocol: a program decides before it reads its input or writes anything, reading files to decide is fine (RULED); USING, DECISIONS, anchor |
| 867 | 2026-11-16 | DECISIONS: correction, Wombat starts no program, so it strips no mark |
| 866 | 2026-11-16 | the refusal protocol's mark: BASH_RERUN_ON_127=1 only where C#Bash reruns a 127 (Wombat's ruling, "silent only for C#Bash's marked calls"); bash and sh in ~/.bash/bin never rerun (864 ran `bash -c '...; exit 127'` twice); case refusal_protocol; USING, KNOWN_ISSUES, DECISIONS, anchor |
| 865 | 2026-11-15 | anchor: 864 deployed (the refusal protocol) |
| 864 | 2026-11-15 | the refusal protocol: exit 127 from a ~/.bash/bin program reruns the command with the next in unix-tools; case refusal_protocol; USING, KNOWN_ISSUES |
| 863 | 2026-11-15 | anchor: drop the orphaned unpublished-builds line |
| 862 | 2026-11-15 | record: published hg-861 = build 859 (first on .NET 10) |
| 861 | 2026-11-15 | publish prep: README table and PERFORMANCE from build 859 (first on .NET 10), perf-history 859, KNOWN_ISSUES release section 859, DEVELOPING 316 cases, anchor |
| 860 | 2026-11-15 | DECISIONS: the refusal protocol (exit 127, nothing written) and C#Bash's rerun with the unix-tools chain, ratified |
| 859 | 2026-11-15 | KNOWN_ISSUES: 859 is built with SDK 10.0.404 (the patched compiler); dev_tty_git_prompt uses Git for Windows when git on PATH is Wombat |
| 858 | 2026-11-15 | global.json asks for SDK 10.0.404 (the Brotli CVE's patched compiler); anchor: Wombat and SDK installed, publish go |
| 857 | 2026-11-15 | docs: README and PERFORMANCE say C#Bash runs on .NET 10 since build 847 |
| 856 | 2026-11-15 | docs: build docs name the supported toolchain (.NET 10 SDK, VS C++ build tools); README/USING drop the PowerShell setx line (the architect's go) |
| 855 | 2026-11-15 | anchor: Wombat's folder is F:/Koliada/Tools/wombat (renamed 2026-11-15) |
| 854 | 2026-11-15 | anchor: 853 deployed (suite 316/0, battery 220); bugs list empty |
| 853 | 2026-11-15 | arithmetic error messages in bash's words (EXPR: MESSAGE (error token is ...)), per context; base 64 @ and _; 0x is 0; case arith_error_messages_gnu |
| 852 | 2026-11-15 | record: 851 deployed; Wombat ready for adoption (awaiting the architect) |
| 851 | 2026-11-15 | glob: a quoted backslash in a path is a separator; literal folders kept as typed; case glob_quoted_backslash |
| 850 | 2026-11-15 | a glob rooted at /tmp expands (facts-8a); case glob_tmp_root_gnu; records for 849 |
| 849 | 2026-11-15 | arithmetic errors as bash: an expansion/assignment error abandons the line; let and for (( )) return 1; integer ** and negative exponent error; case arith_errors_flow_gnu; redirect's expected = GNU |
| 848 | 2026-11-15 | record: 847 deployed on .NET 10; NU1903 (ILCompiler 10.0.5, Brotli CVE) awaiting a patched SDK |
| 847 | 2026-11-15 | the supported toolchain (ruled): .NET 10 (net10.0, global.json), Build Tools 2022; publish-aot links with the released install; base64 left-over bits; posix_classes_unicode excludes Unicode 15.1/16 code points |
| 846 | 2026-11-15 | publish-aot.cmd finds the C++ tools through vswhere (any edition, Build Tools included) instead of VS 2022 Professional's hard-coded path; DEVELOPING prerequisites |
| 845 | 2026-11-15 | record: sed -u fixed, 844 deployed; AOT toolchain research (awaiting the architect); publish-aot.cmd on the bugs list |
| 844 | 2026-11-15 | sed -u flushes each line it writes, as GNU; case sed_unbuffered_gnu |
| 843 | 2026-11-15 | anchor: bugs list after 841; sed -u working |
| 842 | 2026-11-15 | record: [[ == ]] extended groups fixed, 841 deployed |
| 841 | 2026-11-15 | KNOWN_ISSUES: the extglob fix deploys as build 841 |
| 840 | 2026-11-15 | [[ == ]] reads extended groups @( ) *( ) +( ) ?( ) !( ) as bash does with extglob off (KNOWN_ISSUES); case cond_extglob_gnu |
| 839 | 2026-11-15 | anchor: the open KNOWN_ISSUES are the bugs list (the architect); sed -u on it |
| 838 | 2026-11-14 | record: grep --line-buffered fixed, 837 deployed, git-9d told; sed -u found (awaiting) |
| 837 | 2026-11-14 | grep --line-buffered flushes each printed line (git-9d); case grep_line_buffered_gnu |
| 836 | 2026-11-14 | record: mkdir trailing slash fixed, 835 deployed, git-9d told |
| 835 | 2026-11-14 | mkdir: a trailing slash names the folder itself (git-9d); case mkdir_trailing_slash_gnu |
| 834 | 2026-11-14 | Deployed 833 (facts-8a's awk fixes); facts-8a told; anchor |
| 833 | 2026-11-14 | RESUME anchor: 833 deploying (facts-8a's awk fixes) |
| 832 | 2026-11-14 | Bugs list (facts-8a): awk escaped brackets in a set, -e/--source in-process, an empty separator per character; case awk_brackets_e_split_gnu, red on 827 |
| 831 | 2026-11-14 | Bugs list: three awk bugs from facts-8a, verified against GNU (\] in brackets, -e to WSL, empty separator) |
| 830 | 2026-11-14 | RESUME anchor: the nightly-publish idea recorded as his to ponder; nothing scheduled |
| 829 | 2026-11-14 | RESUME anchor rewritten fresh at 90% context: true state, awaiting items, done today, rules, next publish |
| 828 | 2026-11-14 | Deployed 827: the first deploy through the bash runners (suite 307/0, battery 220) |
| 827 | 2026-11-14 | Recorded: PowerShell removed (proof, the slip undone); RESUME anchor |
| 826 | 2026-11-14 | PowerShell removed (the architect: before the next publish): deploy.sh and publish.sh run tests/run-tests.sh and tests/compat/run-compat.sh; the console-kind cases use tools/launch --print; consoleless.sh; the four .ps1 files gone; docs. Proven first: old and new runners agree case for case on 816 in both modes, a planted failure fails in both, the batteries report the same probes |
| 825 | 2026-11-14 | Undo: tests/expected/args.out carried the planted-failure line (taken into hg 824 by a commit with no file list while the equivalence test ran); restored to rev 822's |
| 824 | 2026-11-14 | RESUME anchor: run-tests equivalent; planted failure and battery comparison running |
| 823 | 2026-11-14 | PowerShell removal, step 2 (not switched over): run-tests.sh splits cases.tsv by hand (an empty column vanished under read's tab handling: args.sh got no arguments, aot-only cases their comment); tests/compat/run-compat.sh (the battery, same lines) |
| 822 | 2026-11-14 | RESUME anchor: PowerShell removal step 1 committed, equivalence runs going |
| 821 | 2026-11-14 | PowerShell removal, step 1 (not switched over): tools/launch (detached / windowless / hidden-window starts, timeout kills the tree), tests/cases.tsv (the 307 cases with their comments, from run-tests.ps1), tests/run-tests.sh (same options and lines; exact comparison; console cases get pipes as before and a 120 s bound) |
| 820 | 2026-11-14 | RESUME anchor and DECISIONS: the PowerShell runners' measured behavior and the removal plan; zig spike done |
| 819 | 2026-11-14 | Spike recorded: zig links .NET AOT output and makes the Windows import libraries, but not MSVC's C/C++ runtime (LIBCMT, OLDNAMES, libcpmt): VS's build tools stay needed |
| 818 | 2026-11-14 | RESUME anchor: greenlit PowerShell removal and the zig AOT spike |
| 817 | 2026-11-14 | Deployed 816 (access-time, benchtimer limit); PowerShell removal and the zig AOT spike put to the architect |
| 816 | 2026-11-14 | RESUME anchor: 816 deploying (access-time, benchtimer limit) |
| 815 | 2026-11-14 | access-time = default \| preserve (the architect): preserve keeps a file's access time when stat / ls -l read its details (the #! read and the detail handles hold it via SetFileTime); case access_time_preserve, red on 806. Ruled: WSL a permitted dependency, off the TBD list |
| 814 | 2026-11-14 | benchtimer: a per-launch bound (300 s, --limit) in the same wait, nothing added to the timed path; a hang is killed with its children and compare3's cell reads TIMEOUT (the architect's go) |
| 813 | 2026-11-14 | RESUME anchor: published hg-811 (build 806) |
| 812 | 2026-11-14 | Published hg 811 (release hg-811, build 806); recorded |
| 811 | 2026-11-14 | The 806 publish's doc review: README table and startup line from compare3 on 806 (C#Bash host, quiet), 306 cases; PERFORMANCE; KNOWN_ISSUES for the 806 release; the advantage graph with build 806 |
| 810 | 2026-11-14 | PERFORMANCE.md: the advantage section's paragraphs reflowed for reading (the architect's edit, Typora) |
| 809 | 2026-11-14 | Advantage graph: each point's hover names its hg revision; PERFORMANCE.md links the graph on its own (raw, interactive) in place of an artifact |
| 808 | 2026-11-14 | The advantage graph (the architect): docs/perf-history.tsv + tools/perf-history.sh draw docs/perf-history.svg, atop PERFORMANCE.md with a brief note; publish.sh refuses without the deployed build in it |
| 807 | 2026-11-14 | Deployed 806 (PIPESTATUS on read, awk pass-through fix) |
| 806 | 2026-11-14 | RESUME anchor: 806 deploying (PIPESTATUS on read, awk pass-through fix) |
| 805 | 2026-11-14 | Speed: PIPESTATUS written only when read (func/loop/arith 6-8 % faster than 798); bugs list: awk passed a function's own parameter or local empty to another function (since 2026-10-10); case awk_param_passthrough_gnu, red on 798 |
| 804 | 2026-11-14 | Recorded: the unasked config change, its revert, and the architect's choice (A): this machine's chain puts PortableGit's cmd first |
| 803 | 2026-11-14 | Fixed (the architect): a bare git ran WSL's Linux git through the chain; this machine's csharpBash.config puts PortableGit's cmd first; KNOWN_LIMITATIONS: a WSL-run tool is the Linux one |
| 802 | 2026-11-14 | Published hg 801 (release hg-801, build 798); recorded |
| 801 | 2026-11-14 | Doc review for the 798 publish: ARCHITECTURE's lookup paragraph and IMPLEMENTATION's file table carry the fallback chain (Fallbacks.cs) |
| 800 | 2026-11-14 | The 798 publish's doc review: README table and startup line from compare3 on 798 (hosted by C#Bash, quiet), 305 cases; PERFORMANCE; DEVELOPING: C#Bash hosts the benchmark (the architect); KNOWN_ISSUES for the 798 release (765-798) |
| 799 | 2026-11-13 | Deployed 798 (help fallback, the no-topic hint); c-parser-92 told |
| 798 | 2026-11-13 | The fallback chain discoverable (the architect: 1 & 2; go): help fallback/wsl topics, the no-topic hint, KNOWN_LIMITATIONS' not-listed-is-not-absent entry with the PDF recipe |
| 797 | 2026-11-13 | Answered c-parser-92: PDF text and page images through the fallback chain (WSL's poppler), checked on C99.pdf |
| 796 | 2026-11-12 | RESUME anchor: bugs line and deployed-beyond-release refreshed; the two KNOWN_ISSUES bugs put to the architect |
| 795 | 2026-11-12 | RESUME anchor refreshed: deployed 789, the two KNOWN_ISSUES bugs put to the architect |
| 794 | 2026-11-12 | Ruled (the architect): $TEMP/$TMP left as Windows and Claude Code set them; KNOWN_LIMITATIONS: use mktemp for a temporary path (glassmirror-4e's need) |
| 793 | 2026-11-12 | Ruled (the architect): KNOWN_ISSUES.md for bugs being worked on, KNOWN_LIMITATIONS.md (new) for limitations, by-design differences and features not available, each with the way that works; the installer installs both |
| 792 | 2026-11-12 | Ruled (the architect): keep the $( ) CRLF trim, a limitation easily overcome; KNOWN_ISSUES in real terms; USING.md: write files with LF |
| 791 | 2026-11-12 | KNOWN_ISSUES Limits: $( ) drops trailing CRLFs (the 2026-11-05 ruling), with a byte-exact idiom; glassmirror-4e's report recorded |
| 790 | 2026-11-12 | Deployed 789 (facts-8a's mv fix); facts-8a told |
| 789 | 2026-11-12 | RESUME anchor: 787 deployed, 789 (the mv fix) deploying |
| 788 | 2026-11-12 | Bugs list (facts-8a): a refused same-volume folder rename moves nothing (it copied every entry, then failed); copy-then-delete only across drives; case mv_held_dir, red on 787. Deployed 787 recorded |
| 787 | 2026-11-12 | Bugs list (git-9d, a leak): under nullglob a declaration builtin's array literal with a glob listed every variable; the folded literal is quoted; case nullglob_decl_array_gnu (counts, never prints, the dump), red on 785 |
| 786 | 2026-11-12 | Deployed 785 (the fallback chain, three bug fixes, the cygpath hint); facts-8a and daq-a4 told |
| 785 | 2026-11-12 | Ruled (the architect): the fallback's wsl.exe starts with WSLENV cleared (no translate noise before a fallback tool or a miss); USING.md; build 785 carries the fallback chain, three bug fixes and the cygpath hint |
| 784 | 2026-11-12 | Need counted (facts-8a): PDF text with a page range; WSL's pdftotext reaches it through the fallback chain once deployed |
| 783 | 2026-11-12 | Bugs list (facts-8a): every one-command pipeline sets PIPESTATUS, as bash; compound commands leave their last pipeline's; case pipestatus_simple_gnu, red on 770 |
| 782 | 2026-11-12 | Bugs list (daq-a1): after // a leading # or % is the pattern's own character, not an anchor, as GNU; case subst_global_literal_anchor_gnu, red on 770 |
| 781 | 2026-11-12 | KNOWN_ISSUES: the undeployed fixes listed under the next build; RESUME anchor: deploy waits on the WSLENV answer |
| 780 | 2026-11-12 | The cygpath hint (RULED 2026-11-07, never built; found on daq-a1's need): not found, 127, then what to use; never to WSL; case cygpath_hint, red on 770 |
| 779 | 2026-11-12 | Bugs list (daq-a1): an associative subscript expands `cmd`, $( ), $(( )) and special parameters; unset 'M[$k]' expands it; case assoc_subscript_subst_gnu, red on 770. Raised: WSLENV and the fallback's WSL start (the 778 deploy stopped on the battery) |
| 778 | 2026-11-11 | Fallback chain: strings' not-available options reach WSL; strings_local and fallback_not_windows test the dead end under a chain without WSL; fallback_chain covers an option handed to WSL (the 777 deploy stopped on them) |
| 777 | 2026-11-11 | Features list 3: the fallback chain (folders of Unix tools, PATH, WSL, in the order of %USERPROFILE%/.bash/etc/csharpBash.config; read once at the first lookup, never at startup); WSL runs from System32 so the shell's folder is never held; case fallback_chain, red on 770; runner clears WSLENV |
| 776 | 2026-11-11 | Ruled (the architect): fallbacks configured in ~/.bash/etc/, read once at the first fallback, the order on one line |
| 775 | 2026-11-11 | Ruled (the architect): the first fallback's setting is not an environment variable |
| 774 | 2026-11-11 | Ruled (the architect): first fallback = a folder of Unix tools (unset: PATH), then WSL; none is a state, not a switch |
| 773 | 2026-11-11 | Ruled (the architect): fallback order in-process, a configured first fallback, PATH, WSL; supersedes Claude's proposal |
| 772 | 2026-11-11 | Proposed: the WSL fallback chain and BASH_FALLBACK (features list 3), measured; awaiting the architect |
| 771 | 2026-11-11 | Deployed 770 (the jq batch, jq contains fix); lantern-7f told |
| 770 | 2026-11-11 | Features list 2: the jq batch (paths, leaf_paths, path, getpath, delpaths, tostream, walk, recurse/1-2, limit, indices, index, rindex, splits, inside, label/break, input_filename, -S), as jq 1.6; bugs list: contains/inside refuse mismatched types only at the top (jq 1.6); cases jq_batch_gnu and jq_contains_nested_gnu, red on 768 |
| 769 | 2026-11-11 | Deployed 768 (find -anewer/-cnewer); restated: the benchmark is publish's, never deploy's (the architect) |
| 768 | 2026-11-11 | KNOWN_ISSUES: find -anewer/-cnewer in-process (deployed as 768) |
| 767 | 2026-11-11 | Corrected: no benchmark run per feature (Claude's widening); a feature that would slow the benchmark is a consult, one that cannot deploys normally (the architect: why do I want a window for 766?) |
| 766 | 2026-11-11 | Features list 1: find -anewer/-cnewer (access/change time against the reference's modification time, as GNU find); case find_anewer_cnewer_gnu, red on 765; not deployed (benchmark first). jq --rawfile was already in (2026-11-07); FEATURES.md's stale awk rand/srand line corrected |
| 765 | 2026-11-11 | Bugs list 1 (git-b3): env -u NAME NAME=value removes first, then assigns, so a started program gets NAME, as GNU env (the removal came last); case env_unset_assign_gnu, red on 754 |
| 764 | 2026-11-11 | Recorded: Wombat's system files (gitconfig symlinks=false, empty gitattributes) go with its git.exe (relayed by git-b3) |
| 763 | 2026-11-11 | Features list: jq index/rindex join the jq batch (lantern-54's need) |
| 762 | 2026-11-11 | Bugs list: env -u NAME NAME=value leaves NAME unset in a Windows program (git-b3; verified); for the next session |
| 761 | 2026-11-11 | RESUME anchor saved for a fresh session (the architect: save everything, context full): state 754 published as hg-756, the two lists, next: features (1) jq --rawfile, find -anewer/-cnewer |
| 760 | 2026-11-11 | Ruled: features ordered by least hit on performance; the list reordered |
| 759 | 2026-11-11 | KNOWN_ISSUES: one list, no stray blank line |
| 758 | 2026-11-11 | KNOWN_ISSUES: the MSYS2 quoting failure documented as MSYS2's (Limits) and dropped from the open bugs (the architect) |
| 757 | 2026-11-11 | Published hg 756 (GitHub main 273943b, release hg-756 with build 754); recorded; RESUME anchor |
| 756 | 2026-11-11 | The 754 publish's doc review: README table and startup line from compare3 on 754 (window held), 294 cases; PERFORMANCE; KNOWN_ISSUES for the 754 release (729-754) |
| 755 | 2026-11-11 | Deployed 754; the MSYS2 quoting failure is MSYS2's own (cmd delivers the quotes too); bugs list empty; RESUME anchor |
| 754 | 2026-11-11 | Bugs list 3: a negative length in a list slice (${a[@]:0:-1}, ${@:1:-1}) and a bad substitution abandon the top-level line, status 1, as GNU (they counted back, gave nothing, or went on); case abandon_line_gnu, red on 753; suite 288/0 console and detached |
| 753 | 2026-11-11 | Bugs list 1 and 2: ls -u / --time=atime show and sort by the access time (it was ignored); jq exits 4 on invalid input, as jq 1.6 (it was 2); cases ls_time_kinds_gnu, jq_exit_gnu, red on 751; suite 287/0 console and detached |
| 752 | 2026-11-11 | Deployed 751; ruled: two lists (bugs, features), one process, bugs first, features by speed, a benchmark slowdown is a consult; the lists in the RESUME anchor |
| 751 | 2026-11-11 | Upper-cased env names matched ASCII-only (ToUpperInvariant loaded ICU at every start: icu_lazy failed the deploy's AOT suite) |
| 750 | 2026-11-11 | SystemRoot, windir, ComSpec, ProgramFiles, CommonProgramFiles, SystemDrive arrive upper-cased, as Git Bash imports them (measured; the architect's yes of 2026-11-09); case env_names_upper, red on 748; suite 285/0 console and detached |
| 749 | 2026-11-11 | Deployed 748; recorded |
| 748 | 2026-11-11 | The change time is NTFS's in stat %Z/%z, ls -c and find -ctime/-cmin/-newerct (git-b3: it was the modification time); find -newerat reads the access time; case stat_ctime (reference MSYS stat), red on 745; suite 284/0 console and detached |
| 747 | 2026-11-10 | RESUME anchor: deployed list 729-745; lantern-92's bugs done; open: the jq batch, column |
| 746 | 2026-11-10 | Deployed 745; recorded |
| 745 | 2026-11-10 | Quoted subscripts in expansions read as words; cat -A/-v M- notation for high bytes; printf %b decodes \NNN (echo -e keeps \0NNN) (git-b3's three); cases red on 743; suite 283/0 console and detached |
| 744 | 2026-11-10 | Deployed 743 (awk ARGV); raised git-b3's three bugs (quoted subscripts, cat -A high bytes, printf %b \nnn) with positions; column counted |
| 743 | 2026-11-10 | awk's ARGV and ARGC (c-parser-92: both were empty, so FILENAME==ARGV[1] never matched); files read from ARGV as it stands; case awk_argv_gnu (expected = gawk 5.1), red on 741; suite 280/0 console and detached |
| 742 | 2026-11-10 | Deployed 741; recorded |
| 741 | 2026-11-10 | tr: a range's endpoints may be escapes ('\001-\037'); jq input as jq 1.6: a raw control character U+0001-U+001E in a finished string refused, NaN/Infinity read (lantern-92's three; the architect: bugs; go); case jq_tr_edges_gnu, red on 739; suite 279/0 console and detached; Wombat's git.exe ruling in its two steps |
| 740 | 2026-11-10 | Deployed 739 (awk ENVIRON); recorded: the 19-hour hung rerun (a direct case run without </dev/null), the architect's ruling relayed by git-b3 (Wombat's git.exe into ~/.bash/bin after its Plan 4); RESUME anchor |
| 739 | 2026-11-10 | awk's ENVIRON (git-b3's request; the architect: go): the exported variables and the command's assignments, exact text; case awk_environ_gnu (expected = gawk 5.1), red on 733; the awk case's refused-ENVIRON line now checks the value; suite 278/0 console and detached |
| 738 | 2026-11-10 | Raised (lantern-92): jq control characters and NaN/Infinity against jq 1.6, tr octal ranges (bugs, awaiting go); 14 jq builtins and jq -S counted as one need |
| 737 | 2026-11-09 | Counted (git-b3): awk's ENVIRON, refused; a feature, awaiting the architect |
| 736 | 2026-11-09 | Raised (the architect): WSL as the default fallback for external Linux tools, overridable or none; open, after the variable-name spelling item |
| 735 | 2026-11-09 | Session close: RESUME anchor (deployed 733, published hg-727 with 726, 729-733 awaiting a publish; open items); DEVELOPING's MSYS rows as the pin works (bypass, not skip) |
| 734 | 2026-11-09 | Deployed 733; recorded |
| 733 | 2026-11-09 | A here-document body inside "$( )" is literal (installer-a4: an apostrophe in it broke the parse); Lexer.ConstructEnd passes over each body; case heredoc_in_quoted_comsub_gnu, red on 731; suite 277/0 console and detached |
| 732 | 2026-11-09 | Deployed 731; recorded |
| 731 | 2026-11-09 | "${@:N[:M]}" is one field per parameter (git-b3: it was one word), as "$@" and array slices; one Slice helper for both paths; case positional_slice_gnu, red on 729; suite 276/0 console and detached |
| 730 | 2026-11-09 | Deployed 729 (git-b3's two fixes); recorded |
| 729 | 2026-11-09 | touch -d / date -d keep a clock time's fraction of a second (to 100 ns); an assignment to RANDOM seeds it (value arithmetic, modulo 2^32; SRANDOM apart) (git-b3's two reports); cases date_fraction_gnu and random_seed_gnu, red on 726; suite 275/0 console and detached |
| 728 | 2026-11-09 | Published hg 727 (GitHub main eb16a78, release hg-727 with build 726); recorded, with lantern-92's note on the window (the table stands: baselines within 2.6%) |
| 727 | 2026-11-09 | The 726 publish's doc review: README table and startup line from compare3 on 726 (window held, baselines within 1.3%), 279 cases; PERFORMANCE measured-on and figures; KNOWN_ISSUES for the 726 release (716 no Git search, 725 awk rand/srand); RESUME anchor |
| 726 | 2026-11-09 | Tests' MSYS pinned to PortableGit 2.28.0's usr/bin (the architect: pin against PortableGit); all nine MSYS cases pass; the MSYS2 quoting failure flagged as an open bug (KNOWN_ISSUES, DECISIONS) |
| 725 | 2026-11-09 | awk rand() and srand() (git-b3's request; the architect's go): gawk's documented behavior, measured; .NET's System.Random as the generator (only reproducibility matters, not the values); case awk_rand_gnu (properties, expected = gawk 5.1 via WSL), red on 716, mutation red |
| 724 | 2026-11-09 | compare3 runs any Git Bash, its version under the column title (the architect's ruling, superseding the missing-if-wrong default); PERFORMANCE: every benchmark before 2026-11-09 ran with Git Bash 4.4.23(1)-release |
| 723 | 2026-11-09 | compare3 compares against whatever is installed (the architect's ruling): a missing or wrong-version Git Bash, or a missing WSL, leaves its column out with a note; with neither it is bypassed (loud line, exit 0, nothing timed or appended); docs/DEVELOPING.md: Running the benchmark (what it uses, the pinned PortableGit's SHA-256, how to run it) |
| 722 | 2026-11-09 | Tests' MSYS pinned (F:/Tools/msys64/usr/bin), else found on PATH by msys-2.0.dll, else the case prints BYPASSED: a loud complaint in the runner and deploy.sh, never a failure (the architect's ruling); nine cases use tests/cases/msys_pin.inc; found: msys_arguments fails on the pinned MSYS2 (a quoted non-ASCII word keeps its quotes), open |
| 721 | 2026-11-09 | Found: the MSYS test cases run only in a hand-made third run against an unpinned MSYS (msys_redirects silently skipping since PortableGit 5.3.15, 2026-11-07); open for the architect with Claude's position (pin MSYS2, run in every suite run, fail when missing) |
| 720 | 2026-11-09 | No Git and no history in current-state docs (the architect: a contract does not care what went before, and it is in the file history): module-contract section 5 and header, ARCHITECTURE's PATH bullet, IMPLEMENTATION's run-time paragraph, PROJECT_CONTEXT's PATH line |
| 719 | 2026-11-09 | American spelling in docs/module-contract.md section 5 (global rule): recognized |
| 718 | 2026-11-09 | docs/module-contract.md unfrozen (the architect: "unfreeze it and change it in both places"): the MSYS bridge as it works without the Git search, in this copy and PortableBash's (its hg 58); IMPLEMENTATION's run-time paragraph corrected |
| 717 | 2026-11-09 | Deployed 716 (no Git search; start-up 10.7 ms against 704's 12.5 from a C#Bash host); the architect's rule, every project: no tool's source, ever ("we don't translate any tools source"): the rand()/srand() plan and Builtins.Awk.cs's comment corrected, the question how to build them put to him; RESUME anchor |
| 716 | 2026-11-09 | No Git search at start-up (the architect: "remove the git search entirely"): AugmentPathWithGitTools removed; a start touches no PATH folder (~2 ms saved, 48 entries, Git off PATH); startup_env checks every PATH comes back as given (red on 704); suite 264/0 console and detached, 273/0 with Git's usr/bin, mingw64/bin and cmd on PATH; docs (USING startup steps renumbered, DEVELOPING, ARCHITECTURE, PROJECT_CONTEXT); no KNOWN_ISSUES line (ruled: not a known issue) |
| 715 | 2026-11-09 | RESUME anchor: compare3 WSL fix (713) and the probe finding (714); awaiting the architect: the publish, env name case, the probe's fix |
| 714 | 2026-11-09 | Found: the C#Bash-hosted startup difference is the Git-tools probe (File.Exists git.exe on all 48 PATH entries when Git is off PATH, ~2 ms every start; a Git Bash host puts Git first so the probe stops at entry 1); measured, interleaved, with controls; fix awaits the architect |
| 713 | 2026-11-09 | compare3 finds WSL under either host: reads SYSTEMROOT or SystemRoot, Windows-rooted fallback C:/WINDOWS (the C#Bash-hosted calibration run's WSL rows failed on /c/WINDOWS); Git Bash host's command unchanged |
| 712 | 2026-11-08 | Calibration done (window 07:24-07:50, 12 sessions held): no host effect beyond noise on the script rows; C#Bash startup ~1.5 ms slower under a C#Bash host; B's WSL rows failed (env name case: SystemRoot vs Git Bash's SYSTEMROOT, and compare3's /c/WINDOWS fallback); three tables in results.md; the publish from A2 awaits the architect |
| 711 | 2026-11-08 | Benchmark baselines: quiet references set (sieve 0.0310 s, launch 0.0076 s) from the window of 2026-11-08, all 12 sessions holding; both baselines shown to fail under load (20 sieves: 13-16x and 25x; 4 sieves: +60-80% and 2.4-2.6x); git-b3's awk rand need counted |
| 710 | 2026-11-08 | Benchmark baselines (the architect's go): benchtimer --sieve (primes below 10,000,000, checked) and a fixed HOSTNAME.EXE launch, timed by compare3 before and after the rows; quiet reference, fails-under-load check and the calibration await the benchmark window |
| 709 | 2026-11-08 | RESUME rewritten for compaction: waiting for the architect's go on the benchmark calibration (A B A, exact commands), then the 704 publish, then the queue (koliada-net-b9's arithmetic bug; the cygpath hint, realpath, lantern-5f) |
| 708 | 2026-11-08 | Benchmark pinned to PortableGit 2.28.0 (bash 4.4.23(1), verified download) and runnable from either host, named in its tables; calibration (A B A) awaits the architect's go |
| 707 | 2026-11-08 | Request-log spec: redact a config write's extraheader value too (git-b3; the architect: "close it") |
| 706 | 2026-11-08 | Publish paused: the benchmark's Git Bash is now 5.3.15 (WSL column broken, ratios shift), run stopped, fork to the architect; extglob need (git-b3) recorded, KNOWN_ISSUES lists extglob |
| 705 | 2026-11-07 | Before the 704 publish: KNOWN_ISSUES for the 704 release (no cygpath line), deploy 704 recorded, the cygpath ruling, koliada-net-b9's arithmetic report raised; RESUME rewritten (the publish in flight, the queue after it) |
| 704 | 2026-11-07 | A Windows device name with a folder part (d/nul, ./aux.txt) is the file, a bare one (nul, > nul, 2>nul) the device (the architect's ruling); case device_names; docs/unserviced-log.md, the request log's spec agreed with Wombat (git-b3), secrets redacted; suite 264/0 both modes, 273/0 with Git on PATH |
| 703 | 2026-11-07 | Deployed 702 (array assignments beside others); git-b3 told; anchor |
| 702 | 2026-11-07 | An array assignment beside other assignments in one statement, as bash (git-b3: a=() b=() was a syntax error, x= y=1 z=() assigned nothing): kept in order, made left to right; before a command word an array assignment does nothing (bash); declare -f prints it as written; case assign_array_list; suite 264/0 both modes, 272/0 with Git on PATH |
| 701 | 2026-11-07 | Raised: git-b3's array assignment beside other assignments |
| 700 | 2026-11-07 | Anchor: git-b3 wants the unserviced.jsonl spec path once ratified |
| 699 | 2026-11-07 | Raised: unserviced.jsonl as a shared format (Wombat), a spec for the architect; git-b3 answered |
| 698 | 2026-11-07 | Fork measured: no bash-level >nul in 65 transcripts; Claude now recommends Git Bash's rule (the file everywhere); awaiting the architect |
| 697 | 2026-11-07 | Deployed 696 (=~ escapes); fork raised: device names outside rm (file or device), awaiting the architect; anchor |
| 696 | 2026-11-07 | In [[ =~ ]] every backslash-escaped character is literal, as bash (^a\+b$ matched aab, not a+b): Token.EscapedAt, read only for =~'s operand; case regex_escapes; suite 263/0 both modes, 271/0 with Git on PATH |
| 695 | 2026-11-07 | Deployed 694 (rm and device-named files); guy-bf told; anchor |
| 694 | 2026-11-07 | rm removes files named with a device name (nul, con, aux.txt, com1) or ending in a dot through an extended path, and goes on past a failure naming the entry, exit 1, as MSYS rm (guy-bf: rm -rf of an old PortableGit exited 0 and left 8,577 entries); case rm_reserved_names; found: other commands read a device-named file as the device; suite 262/0 both modes, 270/0 with Git on PATH |
| 693 | 2026-11-07 | Deployed 692; raised: guy-bf's rm -rf on reserved names; anchor |
| 692 | 2026-11-07 | A quoted character is plain in brace and pathname expansion ({x\,y,z}, "{"a,b}, "a[1]"*) and an expansion's result is no brace expansion (x='{a,b}'; echo $x), as bash; an escaped dot is quoted ({1\..3}, =~ a\.b); case brace_glob_quoting; raised: escaped regex characters in =~; suite 262/0 both modes, 270/0 with Git on PATH (a one-off MSYS echo miss before) |
| 691 | 2026-11-07 | Deployed 690 (the POSIX classes); anchor |
| 690 | 2026-11-07 | The POSIX classes as GNU in a UTF-8 locale ([[:alpha:]] matches Ã© in =~, globs, grep, sed, awk; tr stays ASCII as GNU's), checked on every BMP character; case posix_classes_unicode; KNOWN_ISSUES moves it to fixed; suite 261/0 both modes, 269/0 with Git on PATH |
| 689 | 2026-11-07 | E:/BashRequest.md and its reply removed (the architect); the RESUME no longer points at them |
| 688 | 2026-11-07 | guy-windows11 on 681 (guy-e3 via guy-bf) recorded; USING: cmd /c under MSYS_NO_PATHCONV or MSYS2_ARG_CONV_EXCL=* (guy-e3's note) |
| 687 | 2026-11-07 | Published hg 686 (release hg-686: the installer only, build 681); anchor: nothing deployed is unpublished |
| 686 | 2026-11-07 | Doc review for the publish of 681: README table and startup line from compare3 on deployed 681 (a busy machine, said in PERFORMANCE); KNOWN_ISSUES.md names this release's fixes; a release carries only the installer (the architect: "drop bash.exe from the github release"): release.sh, publish.sh, README, USING, IMPLEMENTATION |
| 685 | 2026-11-07 | The start procedure recorded (the home-folder session's b60267f and 285c529; claude.bat hands off to the installed claude.cmd); release.sh checks the installed claude.cmd; USING shows the hand-off; RESUME corrected |
| 684 | 2026-11-07 | KNOWN_ISSUES.md (DRAFT for the architect): the known issues of a build, installed beside ~/.bash/unserviced.jsonl with every publish (the architect's ruling); the installer stages it with the build stamped and checks the sandboxed install; release.sh checks this machine's copy; README, USING, IMPLEMENTATION, PROJECT_CONTEXT |
| 683 | 2026-11-07 | Raised: one source for known issues (guy-bf, for the architect); the proposal recorded, awaiting him |
| 682 | 2026-11-07 | Deployed 681 (f1-f3, the jq pass); guy-e3's reply rewritten for 681; anchor: 660-681 unpublished, def deferred, f4 open |
| 681 | 2026-11-07 | jq match, capture, scan and --rawfile as jq 1.6 (offsets in code points, captures in pattern order, zero-width matches as jq), the array forms of test/match/capture; case jq_regex_match; new sessions on guy-windows8 start on the published build (guy-bc, the architect's decision): recorded; suite 260/0 both modes, 268/0 with Git on PATH |
| 680 | 2026-11-07 | [[ ]] takes redirections ([[ â€¦ ]] 2>/dev/null was a syntax error) and a test that cannot be evaluated is false as bash (invalid regex silent, status 2; a bad number reported inside the redirections); quoted text inside an =~ set goes over as written, as bash; a nested C#Bash gets its arguments as typed, as Git Bash; jq def deferred (the architect); suite 259/0 both modes, 267/0 with Git on PATH |
| 679 | 2026-11-07 | GO recorded: the jq pass (capture/match/scan, --rawfile); jq def put to the architect as a fork with pros and cons (Claude: defer); anchor |
| 678 | 2026-11-07 | Deployed 677 (=~ brackets; native arguments and environment as Git Bash); reply to guy-e3 written; anchor: 660-677 unpublished, jq def need, found f1-f4 |
| 677 | 2026-11-07 | [[ =~ ]] reads POSIX bracket expressions as glibc ([[:digit:]] never matched; 'a]' matched ^[[:alpha:]]$); a Windows program's arguments converted after = and -X, a word holding ; left whole (its first item was converted), and its environment converted, all as Git Bash (guy-e3's review; the architect's "yes"); cases regex_posix_classes, native_args_within, native_env; suite 258/0 both modes, 266/0 with Git on PATH |
| 676 | 2026-11-07 | Doc sweep before a new session: the RESUME block rewritten (deployed 671, 660-671 unpublished, needs and open questions, live sessions), PROJECT_CONTEXT's body, counts 267, stale lines in README, DEVELOPING, USING, PERFORMANCE, FEATURES, ARCHITECTURE, IMPLEMENTATION; the delegated runner plan |
| 675 | 2026-11-07 | Anchor: the two needs awaiting his word (file, jq capture/match), the open questions, 660-671 unpublished |
| 674 | 2026-11-07 | Need (guy-96): jq capture() and match(); Claude's position recorded, awaiting the architect |
| 673 | 2026-11-06 | Need (daq-a1): file, for encoding and line endings before an edit; Claude's position recorded, awaiting the architect |
| 672 | 2026-11-05 | Deployed 671 (awk printf %e/%g); anchor |
| 671 | 2026-11-05 | awk's printf %e %E %g %G, C's, as gawk's (they were refused); the cases that used %e as an unsupported example use %a; case awk_printf_eg_gnu |
| 670 | 2026-11-05 | Deployed 669 (/dev/null, awk math); anchor |
| 669 | 2026-11-05 | awk's sqrt, exp, log, sin, cos, atan2 (they were refused); rand/srand stay refused until gawk's generator is matched (OPEN); found: awk printf %e/%g refused; case awk_math_gnu |
| 668 | 2026-11-05 | A Windows program is given nul for /dev/null (whole, after =, after a one-letter option), as Git Bash: curl -o /dev/null exited 23; case native_devnull |
| 667 | 2026-11-05 | Raised: /dev/null reaches a native program verbatim (curl -o /dev/null exits 23); OPEN: Git Bash converts --file=/c/x |
| 666 | 2026-11-05 | Deployed 665 (iconv); anchor; found: curl -o /dev/null exits 23 under C#Bash (to check) |
| 665 | 2026-11-05 | iconv in-process, as glibc's for UTF-8/16/32, ISO-8859-1, Windows-1252, ASCII: its BOM handling, messages and byte positions, -c, -o, -l (installer-a4's need; the open list's 3c); cases iconv_gnu, iconv_local |
| 664 | 2026-11-05 | Record correction: a deploy's rollback pair is still hard-linked (2 links, rightly); anchor's NEXT line |
| 663 | 2026-11-05 | Deployed 662 (ls -l's link column); anchor |
| 662 | 2026-11-05 | ls -l's link column shows the real hard-link count, read by name with no open (NtQueryInformationByName; else the open): +1-4% on 2000 files (diagnostic), where the open cost ~8%; it was 1 for every file |
| 661 | 2026-11-05 | Deployed 660 (the OOM guard); anchor |
| 660 | 2026-11-05 | OOM guard: a builtin out of memory ends "NAME: memory exhausted", status 2, and the shell goes on (it was an unhandled exception, a stack trace, exit 134); case oom_guard |
| 659 | 2026-11-05 | Anchor: true state is 655, deployed and published |
| 658 | 2026-11-05 | Published hg 657 (release hg-657, Bash.exe 655): record; anchor |
| 657 | 2026-11-05 | Publish doc review: README table and figures from compare3 on 655 (2026-11-05, a quiet machine), PERFORMANCE; results.md |
| 656 | 2026-11-05 | Deployed 655 (the CRLF rule complete, corrected); README/FEATURES list every tool it covers; anchor |
| 655 | 2026-11-05 | CRLF rule corrected: what a tool makes from a CRLF line ends with LF (cut's selections, rev, awk's print of fields); only a line passed through or edited in place keeps its CRLF (the battery caught `curl --version \| cut -d' ' -f1`) |
| 654 | 2026-11-05 | The line tools parse a Windows program's CRLF as the line end, third batch: xargs (items), cut, sort, uniq, comm (compared and split without the CR, lines written as they came), rev, nl; crlf and xargs_stream_gnu: their CR probes are now the ruling's |
| 653 | 2026-11-05 | Published hg 652 (release hg-652, Bash.exe 651): record; anchor; OPEN: diff and the CRLF rule |
| 652 | 2026-11-05 | Publish doc review: README table and figures from compare3 on 651 (2026-11-05, a quiet machine), PERFORMANCE; 261 cases; README/FEATURES/IMPLEMENTATION describe the CRLF rule; results.md |
| 651 | 2026-11-05 | The line tools parse a Windows program's CRLF as the line end, second batch: sed (the line end travels with the line; N/G/H keep the CR inside; -i keeps a CRLF file CRLF) and awk (RT "\r\n"; print's default ORS follows the record); a script naming the CR sees it as data (dos2unix's idioms); crlf: one more expected line is the ruling's |
| 650 | 2026-11-05 | Anchor: 648 deployed; the CRLF rule's next batch is sed and awk |
| 649 | 2026-11-05 | Deployed 648 (the CRLF rule, first batch) |
| 648 | 2026-11-05 | The line tools parse a Windows program's CRLF as the line end, first batch: grep (a CR-naming pattern still sees the raw line; lines print as they came), read, mapfile -t, $( )'s trailing CRLFs (koliada-net-b9; the architect's ruling); cases crlf_lines_gnu, crlf_lines_keep; crlf and stdin_shared: six expected lines are now the ruling's |
| 647 | 2026-11-05 | Deployed 646 (unzip, the timeout interrupt, no debug symbols); RULED: the line tools parse CRLF as a line end, a passed-through line keeps it, pipes stay bytes (the architect: "keep it"); anchor |
| 646 | 2026-11-05 | timeout clears the interrupt it raised to stop an in-process command (the next loop pass threw: a .NET stack trace, exit 9); a stray interrupt ends a non-interactive shell with 130, never a trace; case timeout_loop_gnu (koliada-net-forms-cb) |
| 645 | 2026-11-05 | unzip in-process: UnZip 6.00's -l -t -p, extraction (-o/-n/the replace prompt), -q/-qq, -j, -d, members and -x, byte for byte; LF line ends (koliada-net-b9: UnxUtils' unzip wrote CRLF; the architect: "priority addition"); cases unzip_gnu, unzip_handoff |
| 644 | 2026-11-05 | publish-aot.cmd clears the AOT bin folder and fails on any .pdb in its output: a stale 2026-10-18 Bash.pdb was copied into every publish since (never shipped; dist/ and every release checked). Records the unzip GO. |
| 643 | 2026-11-05 | Release builds set DebugType none in the project (the global standing order, 2026-11-05: no debug symbols in any release build); publish-aot.cmd already passed it. Checked: no .pdb with it, Bash.pdb without (control) |
| 642 | 2026-11-05 | A native child's unread stdin is given back to the pipeline stage (the 2026-11-01 spike, built); case stdin_giveback (koliada-net-73, nupkg-33) |
| 641 | 2026-11-05 | find -newer fix confirmed by c-parser-ac |
| 640 | 2026-11-05 | Deployed rev 639 (find -newer); anchor |
| 639 | 2026-11-05 | find -newer with a missing reference is GNU's error, exit 1 (it listed every file, exit 0); case find_newer_missing_gnu (c-parser-ac) |
| 638 | 2026-11-03 | GitHub issues #1 and #2 closed with comments (fixed in hg-636) |
| 637 | 2026-11-03 | Deployed 635 and published: GitHub main = git 93b0a6e = hg 636, release hg-636 (Bash.exe 635 + installer); local install upgraded; anchor |
| 636 | 2026-11-03 | doc review for the publish: README's table and PERFORMANCE.md from the 2026-11-03 compare3 run of deployed 635 (outputs identical in all three shells); case count 254; results.md |
| 635 | 2026-11-03 | the shell's own folder FIRST on its own PATH: a nested bash and sh are this shell, ahead of WSL's launcher or Git (the architect); cases own_dir_first, own_dir_first_sh; two cases adjusted to the new order |
| 634 | 2026-11-03 | docs: Claude Code issue #94077 appears fixed in 2.1.316 (measured): USING's setx step, PERFORMANCE's note |
| 633 | 2026-11-03 | measured: Claude Code 2.1.316 honors CLAUDE_CODE_GIT_BASH_PATH over PATH (first-on-PATH wins only without it); README corrected: no PATH reordering, make the variable reach Claude Code |
| 632 | 2026-11-03 | GitHub #1: the start file sets CLAUDE_CODE_USE_POWERSHELL_TOOL=0 (installer and USING); #2: README checks with ! bash --version and explains PATH discovery (#94077) and the start file as the remedy; WSL case not yet tested |
| 631 | 2026-11-03 | diff fix confirmed by c-parser-ac on its 424,439-line files (631 ms) |
| 630 | 2026-11-03 | Deployed rev 629 (diff on large files); anchor |
| 629 | 2026-11-03 | diff: Myers' O(ND) in linear space for large files, the table kept for small ones (it allocated n x m: OutOfMemoryException at 50,000 lines); case diff_large_gnu (c-parser-ac); the OOM guard raised |
| 628 | 2026-11-03 | anchor: wording; koliada-net-73 ended |
| 627 | 2026-11-03 | Deployed rev 626 (pwd -W, whoami refuses an operand): the first real two-rename swap; unblocked by freeing commit (Brave quit) |
| 626 | 2026-11-03 | verified: the page file is fixed at 8 GB (not dynamic); recommendation recorded |
| 625 | 2026-11-03 | FOUND: the AOT crash is the commit limit nearly exhausted (72.5 of 73.5 GB; ilc: insufficient memory), not Defender or the code; remedies for the architect |
| 624 | 2026-11-03 | whoami refuses an operand as GNU coreutils (extra operand, exit 1); it was ignored (whoami /groups printed the user); case whoami_operand_gnu (koliada-net-7d); not deployed: the AOT build is blocked |
| 623 | 2026-11-03 | AOT crash findings (2): only ilc started by dotnet.exe crashes (0xC0000005 / 0xC0000409); same command and env from a console succeed; no mitigation settings or events; options for the architect |
| 622 | 2026-11-03 | AOT crash findings: Defender intelligence and two new feature controls arrived after the last good build (correlation); reboot recommended |
| 621 | 2026-11-03 | BLOCKED: the AOT publish crashes under dotnet publish (ilc access violation), rev 612's source too: environmental; 620 not deployed; dist intact |
| 620 | 2026-11-03 | pwd -W accepted as Git Bash (usage [-LPW]); case pwd_w (koliada-net-73's need); an external_redirects flicker recorded to watch |
| 619 | 2026-11-02 | NEED recorded (koliada-net-73): pwd -W as Git Bash; awaiting the architect |
| 618 | 2026-11-02 | release.sh: each publish upgrades this machine's install with the installer it released (--silent, then checked); a retry checks a left installer against the release's own .sha256; ~/.bash/bin upgraded 583 -> 612 |
| 617 | 2026-11-02 | Published: GitHub main = git f7e9816 = hg 616, release hg-616 = Bash.exe (612) + installer; anchor rewritten clean |
| 616 | 2026-11-02 | doc review for the publish: README's table and PERFORMANCE.md from the 2026-11-02 compare3 run of deployed 612 (outputs identical in all three shells); case count 249; results.md |
| 615 | 2026-11-01 | deploy swaps as InstallGen does (the new build in full beside the old, then two renames per name; each refused rename puts back); no window or go-round for a deploy, only for the benchmark (the architect) |
| 614 | 2026-11-01 | anchor: wording |
| 613 | 2026-11-01 | Deployed rev 612 (the continuation fix): suite 241/0 detached, battery 217; sh.exe re-linked; anchor |
| 612 | 2026-11-01 | lexer: after word \ and a newline, a continued line starting with a quote or $ is a new word, as GNU (it was glued: echo a \'b' gave ab; printf's format glued to its args); case continuation_quote_gnu (koliada-net-73) |
| 611 | 2026-11-01 | NEED recorded (installer-a4): iconv for a UTF-16LE INF; lossless workaround sent; awaiting the architect |
| 610 | 2026-11-01 | anchor: esdk-a2's make-recipe probe on 608 clean through the hard-linked sh.exe |
| 609 | 2026-11-01 | Deployed rev 608 (awk division by zero, FILENAME); dist/sh.exe a hard link for the first time; anchor |
| 608 | 2026-11-01 | awk: FILENAME is "-" for standard input, as gawk 5.1 (it was ""); case awk_filename_gnu |
| 607 | 2026-11-01 | awk: a division by zero is gawk's run-time fatal (the input's place, the operator's line, exit 2) or, for a constant, its parse-time error (exit 1); never handed to a PATH awk (it ran the program again after the output); cases awk_divzero_gnu, awk_divzero_once |
| 606 | 2026-11-01 | noted: spec 2 freeze stays 17:39:50Z; INST-Q-055 (underscore keys ship inside an installer) awaits the architect |
| 605 | 2026-11-01 | hg-600's installer replaced by the clean rebuild; republished: GitHub main = git d0f32ce = hg 604 |
| 604 | 2026-11-01 | InstallGen specification 2 is the whole reference: the installer doc rewritten as what ships; installer.sh reads InstallGen's location from INSTALLGEN or an ignored local file; no machine path or contract citation in current files |
| 603 | 2026-11-01 | the installer manifest carries no comment (it named E:/Claude/Installer/CONTRACT.md, embedded verbatim in the shipped installer); rebuilt installer clean |
| 602 | 2026-11-01 | release.sh: the newest-release check reads the API, not releases/latest/download/ (which then stayed cached at the previous release's checksum) |
| 601 | 2026-11-01 | Published + shipped: GitHub main = git c626ba3 = hg 600, release hg-600 with Bash.exe (583) and the installer (InstallGen 0.0.8; spec 2 froze 17:39:50Z); .gitignore ignores dist.installer/; anchor |
| 600 | 2026-11-01 | doc review for the publish: README's table and PERFORMANCE.md from the 2026-11-01 compare3 run of deployed 583 (outputs identical in all three shells); results.md |
| 599 | 2026-11-01 | RAISED: awk division by zero filed as unsupported, falls through to a PATH awk after output (duplicate output); fix after the publish |
| 598 | 2026-11-01 | ship the installer: release.sh builds it from the deployed Bash.exe and attaches it with its .sha256 (downloaded back and compared); README leads with it (plan phase 5); case count 245 |
| 597 | 2026-11-01 | installer: his second real install with the 0.0.8 PATH question, accepted ("perfect!"); not shipped |
| 596 | 2026-11-01 | the PATH question as he spec'd it: InstallGen 0.0.8 (INST-045), our manifest drops prompt; rebuilt, the line identical character for character |
| 595 | 2026-11-01 | INST-Q-052: his spec settles it, the PATH question shows the folder with backslashes as he wrote it (relayed) |
| 594 | 2026-11-01 | correction: under InstallGen 0.0.7 our planned PATH form is refused, not silently added (measured); the trap was overstated; installer-a4's 0.0.8 plan recorded |
| 593 | 2026-11-01 | INST-Q-053 ruled: A (fixed PATH wording, prompt removed) and no freeze by his install; relayed; our prompt stays until InstallGen ships A |
| 592 | 2026-11-01 | INST-Q-053 (installer-a4): the PATH question's form (A fixed wording, B placeholder) and the freeze reading await the architect; ship nothing built to spec 2 meanwhile |
| 591 | 2026-11-01 | the PATH question: his form "Add [<folder>] to your PATH? [y/N]: " sent to installer-a4 (spec 2 cannot inline the folder); interim prompt shortened; his first real install recorded |
| 590 | 2026-11-01 | installer/try-install.cmd: a real install from a Command Prompt, then its checks (the architect asked); placed beside the installer by tools/installer.sh; run sandboxed only, polarity shown |
| 589 | 2026-11-01 | installer: the cheap remaining cases checked (native stub, no Claude Code, a path with a space); what is not verified recorded |
| 588 | 2026-11-01 | installer-a4: skip-path observation withdrawn (measured safe); backslash display is INST-Q-052; our installer not shipped (spec 2 not frozen) |
| 587 | 2026-11-01 | the C#Bash installer, BUILT not released (the architect: go): installer/csharpBash.json (InstallGen spec 2, sh.exe as a hard link), installer/claude.cmd (the start file, finds its own folder), tools/installer.sh (build from the deployed Bash.exe with --release, then a sandboxed install checked); upgrade, re-run and a Claude Code session through the start file observed |
| 586 | 2026-11-01 | deploy runs the suite detached only, publish runs it with a console on dist/Bash.exe first; deploy makes dist/sh.exe a hard link to dist/Bash.exe (confirmed with -ef, else a copy) (the architect, 2026-11-01); spike and polarity recorded |
| 585 | 2026-11-01 | anchor: installer-a4 confirmed 579's hard-link identity on the live 583 (fsutil probe), closed |
| 584 | 2026-11-01 | Deployed rev 583 (579 hard-link identity, 583 sed escapes): suite 237/0 both modes, battery 217; deploy time measured (5.5 min) for the architect's question; anchor |
| 583 | 2026-11-01 | sed converts GNU's escapes \a \f \v \cX \dNNN \oNNN \xHH in the regex (raw), the replacement (a produced & or \ literal), y and every line of a/i/c text (c-parser-ac: s/-/\x27/ wrote x27); an escaped final backslash ends a/i/c text; case sed_escapes_gnu |
| 582 | 2026-11-01 | RAISED: sed converts none of GNU's \cX \dNNN \oNNN \xHH (c-parser-ac); installer-a4's deploy go withdrawn (anchor) |
| 581 | 2026-11-01 | DECISIONS: the architect's design, sh.exe as an installer-managed hard link (INST-Q-051, built in InstallGen 0.0.6), recorded |
| 580 | 2026-11-01 | RESUME anchor 2026-11-01 (context low); the stdin give-back spike patch kept in docs/spikes; installer-a4 built INST-Q-051 (INST-043, InstallGen 0.0.6): sh.exe as an installer-managed hard link |
| 579 | 2026-11-01 | A file link count, index and device from GetFileInformationByHandle: stat %h %i %d %D, test -ef, ls -i, find -links/-inum/-samefile/-printf (installer-a4); ls -l link column held; case hardlink_identity_gnu |
| 578 | 2026-11-01 | installer: sh.exe as an installer-managed hard link (the architect design), asked of installer-a4 for spec 2; nothing ships before his word |
| 577 | 2026-11-01 | installer plan: sh.exe is a second file on disk, not a link (hard link keeps the old build after an upgrade; symlink needs Developer Mode or elevation; measured) |
| 576 | 2026-11-01 | installer: sh.exe needs no second payload copy (a to naming a path renames; +44 bytes, measured); correction of the same day; plan on schema 2 |
| 575 | 2026-11-01 | anchor: installer: spec 2 first client (relayed), freeze rule, sh.exe second copy measured (+95%), rename ask filed with installer-a4 |
| 574 | 2026-11-01 | American spelling in code comments and current docs (global rule, 2026-11-01); installer back with the architect (InstallGen 0.0.5 landed) |
| 573 | 2026-11-01 | SPIKE recorded: give a native child unread stdin back to the stage (equals Git Bash on 8 probes, throughput within noise); reverted, patch in scratchpad; awaiting the build ruling |
| 572 | 2026-11-01 | ls_time_style_gnu: mask a recent file month and day too (the case failed on 2026-11-01: Nov  1 against Oct NN); expected regenerated from GNU |
| 571 | 2026-11-01 | anchor: the installer PARKED pending InstallGen updates (the architect, 2026-11-01) |
| 570 | 2026-11-01 | installer plan: INST-Q-047 built (Installer rev 34), nothing blocks it; schema 2 spelling change coming; awaiting the architect |
| 569 | 2026-10-31 | Deployed rev 568 (<( ) forward-slash path; procsub case in its own TEMP; suite 235/0 both modes, battery 217); anchor |
| 568 | 2026-10-31 | anchor: <( ) path fixed (567), deploy pending; never sweep the shared TEMP |
| 567 | 2026-10-31 | <( ) expands to the forward-slash form F:/TEMP/bash-procsub-<id>; the procsub case counts leftovers in a TEMP of its own (shared TEMP made it fail); case procsub_path |
| 566 | 2026-10-31 | RAISED: native program drains in-process stdin it never reads (koliada-net-73, known 2026-10-10 limitation; give-back spike proposed); <( ) path backslashes raised |
| 565 | 2026-10-31 | Deployed rev 564 (awk RS other than newline, RT; suite 234/0 both modes, battery 217); pnputil CRLF note for kes-install-43; anchor |
| 564 | 2026-10-31 | anchor: awk RS built (563), deploy next; the go read as awk RS only (installer still awaiting) |
| 563 | 2026-10-31 | awk RS other than newline as gawk 5.1: paragraph mode, one character, a regex; RT; newline as a field separator in paragraph mode (kes-install-43; the architect: go); case awk_rs_gnu |
| 562 | 2026-10-31 | GO recorded: awk RS other than newline as gawk 5.1 (kes-install-43); plan in DECISIONS; case awk_rs_gnu with gawk expected (not registered yet) |
| 561 | 2026-10-31 | anchor: awk RS other than newline (kes-install-43 unmet need) recorded, awaiting the architect |
| 560 | 2026-10-31 | Deployed rev 559 (kill/kill -0 on a finished job, kill from a subshell, wait for an ended job; suite 233/0 both modes, battery 217); anchor: installer awaiting the architect |
| 559 | 2026-10-31 | anchor: kill/wait fixes built (557, 558), deploy pending; installer phases 3-5 unblocked by Installer INST-Q-046 (rev 33), awaiting the architect; installer-plan status updated |
| 558 | 2026-10-31 | wait $! for an ended job that left the table gives its status (bash bgpids); an unknown pid "is not a child of this shell"; case wait_ended_gnu |
| 557 | 2026-10-31 | kill/kill -0 on the $! of a finished job: "No such process", as GNU; kill $! from a subshell (the watchdog idiom) works (installer-a4); case kill_job_gnu |
| 556 | 2026-10-31 | PUBLISHED: GitHub main = git 5abfc5f = hg rev 555, release hg-555 (Bash.exe build 553); anchor: kill -0 fix next |
| 555 | 2026-10-31 | Publish preparation: benchmark on deployed 553 (all rows within noise of 536), README table/startup, case count 239, PERFORMANCE figures |
| 554 | 2026-10-31 | Deployed rev 553 (dd, cksum, help path; suite 231/0 both modes, battery 217); quiet window for the publish benchmark: all eight; kill -0 bug raised (fix after the publish) |
| 553 | 2026-10-31 | dd and cksum in-process as GNU coreutils 8.32 (installer-a4; the architect: go); help names its table with forward slashes; cases dd_gnu, cksum_gnu, help_refresh + rev 462 table |
| 552 | 2026-10-31 | GO recorded: dd and cksum in-process (GNU 8.32), then deploy and publish; plan, scope and acceptance in DECISIONS |
| 551 | 2026-10-31 | Record: kes-term-a1's Release build passes on 549 (MSBuild Exec finds hg on the forward-slash PATH); the 549 checks are closed |
| 550 | 2026-10-31 | Deployed rev 549 ($PATH two forms + grep -f empty; suite 229/0 both modes, battery 217); peers' checks recorded; anchor rewritten: dd/cksum awaiting, publish pending |
| 549 | 2026-10-31 | grep -f with an empty pattern file: zero patterns, as GNU 3.7 (kes-install-43); case grep_f_empty_gnu |
| 548 | 2026-10-31 | DECISIONS: BUILT entry for $PATH two forms (rev 547; the edit adding it had failed before that commit) |
| 547 | 2026-10-31 | $PATH has two forms: GNU/MSYS inside (/c/x:/f/y), forward-slash Windows at every exit (C:/x;F:/y); colon-list arguments as Git Bash; case path_two_forms; docs |
| 546 | 2026-10-31 | GO recorded: $PATH two forms (GNU/MSYS inside, forward-slash Windows at the exits); plan and acceptance criteria in DECISIONS; anchor: building |
| 545 | 2026-10-31 | Deployed rev 544 (grep -P \Q...\E; suite 227/0 both modes, battery 217); $PATH discussion recorded: exit slashes forward, separator awaiting the architect; anchor updated |
| 544 | 2026-10-31 | grep -P \Q...\E literal quoting (c-parser-ac): resolved before .NET sees it, as PCRE/GNU; case grep_pcre_quote_gnu |
| 543 | 2026-10-31 | PUBLISHED: GitHub main = git fe8bb29 = hg rev 542, release hg-542 (Bash.exe build 536); resume block updated |
| 542 | 2026-10-31 | Publish preparation: benchmark on deployed 536, README table/startup, PERFORMANCE figures |
| 541 | 2026-10-31 | Resume anchor, second pass: tip wording, the bug-fix rule supersedes the old deploy-only-on-his-word line, the open items named plainly, USING in the PATH build list |
| 540 | 2026-10-31 | Resume anchor rewritten for 2026-10-31 (supersedes the stacked blocks, now history); docs: case count 234, USING states $PATH's form and the accepted assignment forms |
| 539 | 2026-10-31 | SPIKE recorded: $PATH in GNU/MSYS form inside, Windows form at the exits: works as designed; 10 suite failures explained; caucus: no session reads $PATH as Windows text; reverted, awaiting the ruling |
| 538 | 2026-10-31 | anchor: PATH-in-GNU-form spike in progress; caucus answers so far (6 of 7: none read $PATH as Windows text; esdk-a2 assigns a Windows-form PATH for children) |
| 537 | 2026-10-31 | Deployed rev 536 (PATH append idiom; suite 226/0 both modes, battery 217); seven sessions now; not published |
| 536 | 2026-10-31 | PATH="$PATH:DIR" and PATH="DIR:$PATH" add DIR: a native ;-list's entries are split again at non-drive colons (qa-de); $PATH's visible form unchanged |
| 535 | 2026-10-31 | anchor: RAISED PATH="$PATH:/dir" not added (qa-de); fix after ~10 min under the bug-fix rule; $PATH's visible form stays (design) |
| 534 | 2026-10-30 | PUBLISHED: GitHub main = git 64476f5 = hg rev 533, release hg-533 (Bash.exe build 532) |
| 533 | 2026-10-30 | Deployed rev 532 (diff -y; suite 225/0 both modes, battery 217); publish preparation: benchmark on 532, README table/startup/case count, PERFORMANCE, DEVELOPING |
| 532 | 2026-10-30 | diff -y (side-by-side) as GNU's layout: -W/--width, --suppress-common-lines, --left-column, -t; it was refused (esdk-a2; the architect: fix it, then publish) |
| 531 | 2026-10-30 | anchor: GREENLIT diff -y, then publish (the architect) |
| 530 | 2026-10-30 | Deployed rev 529 (hash -p obeyed; suite 224/0 both modes, battery 217); not published |
| 529 | 2026-10-30 | An explicit hash -p PATH NAME runs PATH even where C#Bash has NAME in-process (bash builtins excepted, functions win), in pipeline stages too; a subshell keeps the hash table (esdk-a2; a known LOW defect) |
| 528 | 2026-10-30 | anchor: RAISED hash -p not obeyed for embedded tools (esdk-a2; a known LOW defect); fix after ~10 min under the bug-fix rule |
| 527 | 2026-10-30 | anchor: esdk-a2 confirmed rev 525; AWAITING the architect: diff -y (side-by-side), a feature request |
| 526 | 2026-10-30 | Deployed rev 525 (backticks in arithmetic; suite 223/0 both modes, battery 217); not published |
| 525 | 2026-10-30 | A backtick substitution inside arithmetic (substring offset/length, $(( )), (( )), array index) runs before the arithmetic, as $( ) and bash (esdk-a2: four ESDK SDK helper scripts) |
| 524 | 2026-10-30 | anchor: RAISED backticks in arithmetic (esdk-a2, verified against GNU); fix after ~10 min under the bug-fix rule |
| 523 | 2026-10-30 | PUBLISHED: GitHub main = git 2559b1c = hg rev 522, release hg-522 (Bash.exe build 520) |
| 522 | 2026-10-30 | Publish preparation: benchmark on deployed 520 (results.md), README table/startup/case count, PERFORMANCE figures, DEVELOPING count, FEATURES (TZ), IMPLEMENTATION (ShellClock) |
| 521 | 2026-10-30 | Deployed rev 520 (TZ; suite 222/0 both modes on the AOT build, IANA names resolve; battery 217); not published |
| 520 | 2026-10-30 | TZ is read as glibc reads it (UTC/GMT names, empty = UTC, POSIX offsets, IANA zones, unknown = UTC) by date, date -d/-r, touch, ls, stat, find -printf (+%Tz/%TZ) and diff: one resolver, ShellClock |
| 519 | 2026-10-30 | anchor: GREENLIT TZ (one resolver, glibc rules, every time-printing tool) |
| 518 | 2026-10-30 | RULED DEFERRED: MSYS paths inside --opt=/x, -I/x, x=/x arguments, until a real script needs them (the architect) |
| 517 | 2026-10-30 | RULED: a no-#! file found by a bare name on PATH is not run (the architect: fix the files); comments updated, no behaviour change |
| 516 | 2026-10-30 | Deployed rev 515 (find -printf widths, ls --time-style; suite 221/0 both modes, battery 217); not published |
| 515 | 2026-10-30 | find -printf takes flags, width and precision for every directive; ls --time-style honours long-iso, iso, +FORMAT, posix- and locale, and refuses a bad style (kes-term-a1) |
| 514 | 2026-10-30 | PUBLISHED: GitHub main = git 87a79e3 = hg rev 513, release hg-513 (Bash.exe build 511); next: find -printf widths, ls --time-style |
| 513 | 2026-10-30 | Publish preparation: benchmark on deployed 511 (results.md), README table/startup/case count, PERFORMANCE figures, DEVELOPING count, ARCHITECTURE (xargs streams) |
| 512 | 2026-10-30 | Deployed rev 511 ((a)-(g); suite 219/0 both modes, battery 217); raised: find -printf widths, ls --time-style (kes-term-a1) |
| 511 | 2026-10-30 | (c) a no-#! text file run by path runs as a script (binary: Exec format error); (d) /bin/NAME and /usr/bin/NAME run NAME when the drive has no such folder; (e) xargs fits an external batch to Windows' 32767 command line, and a too-long line is Argument list too long; (f) the ESDK build timing in PERFORMANCE.md; (g) $( ) NULs closed as settled |
| 510 | 2026-10-30 | (b) a redirection's word is expanded as bash's (braces, globs, splitting; not one word is ambiguous redirect); a name Windows cannot have: No such file or directory / Invalid argument, not .NET's text |
| 509 | 2026-10-30 | (a) sed reads an s command's flags as GNU does (blanks skipped; an unknown one refused at its char, before a valid e goes elsewhere); compile errors carry their char (always 0) |
| 508 | 2026-10-30 | anchor: GREENLIT (a)-(g): sed flags, redirection expansion, m1 by path, m2 /bin names, xargs external cap, ESDK timing doc; $( ) NULs closed as settled; one deploy, then publish |
| 507 | 2026-10-30 | anchor: AWAITING the architect: redirection words get no pathname or brace expansion, and an impossible Windows name gets the raw .NET message (esdk-a2, verified against GNU) |
| 506 | 2026-10-30 | RECEIVED (esdk-a2): a real make build, msys 1.0 75-76 s against C#Bash 46-48 s, same output; placement in the docs awaits the architect |
| 505 | 2026-10-30 | anchor: AWAITING the architect: sed hands an s command with an e flag to PATH's old sed before checking the rest (koliada-net-forms-cb, verified); ESDK build passes on 503 |
| 504 | 2026-10-30 | Deployed rev 503 (xargs streaming; suite 213/0 both modes, battery 217); the greenlit list is done; the ESDK build now runs on dist/sh.exe and dist/Bash.exe |
| 503 | 2026-10-30 | Item 4: xargs runs each batch as soon as it is full (-n, -L, -I, -s 131072), reading as it goes, commands on /dev/null; GNU's reader (quotes close on their line, -I quotes, -0/-d empty items, -E after quotes); -n/-L later wins; 255 message |
| 502 | 2026-10-30 | RATIFIED: the installer asks whether ~/.bash/bin goes on PATH, default no; filed InstallGen ask INST-Q-047 |
| 501 | 2026-10-30 | installer plan: PATH is the user's choice, asked by the installer (the architect); needs an InstallGen ask; the default awaits him |
| 500 | 2026-10-30 | Deployed rev 499 (match GNU batch + dist/sh.exe; suite 212/0 both modes, battery 216); RULED: sh.exe ships despite make's recipe shell switch; the installer carries sh.exe and is released with each publish |
| 499 | 2026-10-30 | anchor: deploy held, AWAITING the architect: an sh.exe on PATH makes GNU make run every recipe through it (esdk-a2, confirmed) |
| 498 | 2026-10-30 | sh: deploy.sh ships dist/sh.exe, a copy of the build, as Git for Windows and MSYS2 ship theirs (bash default mode, no interpreter code); case sh_name; docs |
| 497 | 2026-10-30 | match GNU 10/11: sort and comm order lines by their bytes, as GNU in the C locale (they compared UTF-16 units: astral characters before U+E000, an undecodable byte out of place) |
| 496 | 2026-10-30 | match GNU 9/11: an arithmetic error in (( )) is reported inside its redirections with status 1, as bash's (it escaped them, status 2) |
| 495 | 2026-10-30 | match GNU 8/11: a numeric option refuses a bad value in GNU's words and exit code (uniq nl fold od base64 grep xargs du); GetInt removed |
| 494 | 2026-10-30 | match GNU 7/11: split refuses a bad -l count or -b size as GNU does (they made one piece, rc 0) |
| 493 | 2026-10-30 | match GNU 6/11: sed's l wraps at 70 by GNU's rule, l N and -l N set the width, bytes as \ooo |
| 492 | 2026-10-30 | match GNU 5/11: printf %q quotes as bash's sh_backslash_quote (= and % never escaped; ~ and # only first) |
| 491 | 2026-10-30 | match GNU 4/11: base64 -w 0 writes no newline; -d skips newlines only (space, tab, CR invalid); -i ignores garbage |
| 490 | 2026-10-30 | match GNU 3/11: od's combined input: addresses from its start, -j across files, a skip past its end an error; -j/-N take 0x and 0 forms |
| 489 | 2026-10-30 | match GNU 2/11: cmp's EOF message (after byte N, line L / in line L; which is empty), -b, and -l's columns are GNU's |
| 488 | 2026-10-30 | match GNU 1/11: wc's words (printable runs), characters (no invalid bytes), display widths (-L) and padding are GNU's; textutils' expected file had the old padding |
| 487 | 2026-10-30 | Item 2: <( ) not streaming is the ratified temp-file design (2026-09-04), not a bug; ARCHITECTURE's non-goals line corrected |
| 486 | 2026-10-30 | Deployed rev 485 (trailing-slash fix; suite 201/0 both modes, battery 216); notified |
| 485 | 2026-10-30 | A destination with a trailing slash names a directory: mv file nodir/ refused as GNU (it renamed to nodir, rc 0); cp gives GNU's words (was Windows' error) |
| 484 | 2026-10-30 | Anchor: the installer plan's phases 1-2 question is answered (both built and released) |
| 483 | 2026-10-30 | Published: GitHub main = git 017986f = hg rev 482 (README thanks, ARCHITECTURE §15); no release |
| 482 | 2026-10-30 | ARCHITECTURE §15: help and the record of what C#Bash cannot do (the map is now §16); README thanks the GNU Project, the FSF and the other manual sites |
| 481 | 2026-10-30 | Published: GitHub main = git eb6ed20 = hg rev 480; release step skipped (Bash.exe unchanged) |
| 480 | 2026-10-30 | README step 2: setx for Command Prompt (quoted) and PowerShell (the architect's edit) |
| 479 | 2026-10-29 | check-help-urls keeps the promises made to gnu.org (which lifted the ban): an honest User-Agent, robots.txt followed, checked only when the table changes |
| 478 | 2026-10-29 | release.sh makes no release when the newest one already carries the same Bash.exe (the Releases page lists builds, not publishes) |
| 477 | 2026-10-29 | Published: GitHub main = git 3312e22 = hg rev 476 (README restructure), release hg-476 (Bash.exe build 472, unchanged) |
| 476 | 2026-10-29 | README is the highlights (the architect's edit; Architecture kept); the detail moved to docs/USING, FEATURES, PERFORMANCE, DEVELOPING |
| 475 | 2026-10-29 | Anchor: the README restructure is drafted, uncommitted, and the architect is editing it (do not commit or overwrite) |
| 474 | 2026-10-29 | Published: GitHub main = git ec718e1 = hg rev 473, with the first release hg-473 (Bash.exe build 472 + SHA-256, downloaded back and checked); deployed 472 recorded |
| 473 | 2026-10-29 | Publish doc review: README table and prose from compare3 on deployed 472 (all outputs agree), 208 cases; IMPLEMENTATION: tools/release.sh |
| 472 | 2026-10-29 | Installer plan, phase 5: the architect's leaning (not a ruling): the README links both the installer and the bare Bash.exe |
| 471 | 2026-10-29 | README links the releases: the latest Bash.exe (releases/latest/download) and the Releases page; skipping the build in the Claude Code setup |
| 470 | 2026-10-29 | Ruled: releases go on the same GitHub repository as the source (its Releases page); the plan says so |
| 469 | 2026-10-29 | Installer plan phase 2: an unedited earlier built-in help.json is rewritten with this build's when help reads it (an edited one never touched); rev 397's find/xargs reach old profiles |
| 468 | 2026-10-29 | Installer plan phase 1: tools/release.sh attaches the deployed Bash.exe and its SHA-256 to GitHub release hg-<rev>, run by publish.sh after its push (first real run: the next publish) |
| 467 | 2026-10-29 | mv to another drive whose source cannot be removed (a running program) says 'cannot remove' and fails, as GNU (was silent success, both files left) |
| 466 | 2026-10-29 | Logged: mv to another drive reports success when the source could not be removed (Builtins.Files.cs:336); INST-Q-046 refined with the architect's .deleted folder and sweeper |
| 465 | 2026-10-29 | Ruled: InstallGen learns to replace a file in use (flagged as Installer INST-Q-046, measured); C#Bash's installer plan written, blocked on it |
| 464 | 2026-10-29 | Ruled: C#Bash owns its installer, PortableBash its own; InstallGen at E:/Claude/Installer/release; help.json is not installer payload |
| 463 | 2026-10-29 | Deployed rev 462 (help's find/xargs; suite 198/0 both modes, battery 216); notified; an installer raised, awaiting the architect |
| 462 | 2026-10-29 | help: find and xargs point at findutils' html_node/ pages (find_html/ is not online; found by check-help-urls); this machine's ~/.bash/help.json too |
| 461 | 2026-10-29 | check-help-urls reads each archive's name from the manual's index (a name rule passed find/xargs, whose folder is not online); 138/140 pass, find + xargs fail |
| 460 | 2026-10-29 | check-help-urls: one download per GNU manual (its html_node archive), not a request per page: 79 gnu.org requests -> 8 |
| 459 | 2026-10-29 | check-help-urls retries a no-answer once after 30-90 s (the architect: 'just to retry'); a stop names curl's exit code |
| 458 | 2026-10-29 | Anchor: help check batch 4 retry (15 pages, 0 FAIL; the 02:03 000 did not repeat); 59 of 87 cached; batch 5 scheduled 06:47 |
| 457 | 2026-10-29 | Anchor: help check batch 4 stopped at its first page (000 at 02:03; VPN or a block, not concluded); nothing scheduled, awaiting the architect |
| 456 | 2026-10-28 | Anchor: help check batch 3 (15 pages, no refusal, 0 FAIL; 44 of 87 cached; this morning's refused page answered); batch 4 scheduled 02:03 |
| 455 | 2026-10-28 | Anchor: batch 2 ended 19:58 |
| 454 | 2026-10-28 | Anchor: help check batch 2 (15 pages, no refusal, 0 FAIL; 29 of 87 cached); batch 3 scheduled 22:53 |
| 453 | 2026-10-28 | Anchor: help check batch 1 (15 pages, no refusal; FAIL 404 xargs's findutils URL); batch 2 scheduled 19:47 |
| 452 | 2026-10-28 | check-help-urls in batches: HELP_CHECK_PAGES=N pages a run, exit 4 when pages are left (the architect: 'get the next N pages, and so on') |
| 451 | 2026-10-28 | Correction: the help check's first run (rev 440) kept no cache; a rerun fetches all 87 pages, not 47 |
| 450 | 2026-10-28 | Published: GitHub main = git 2876ef9 = hg rev 449 by tools/publish.sh (compare3 on deployed 448: pipeline 0.080 s, 1.3x WSL); deployed 448 recorded; nothing greenlit left |
| 449 | 2026-10-28 | README: table and prose from compare3 on deployed 448 (pipeline 0.080 s, 1.3x WSL: no row loses now); benchmark log appended |
| 448 | 2026-10-28 | Benchmark log: compare3 on deployed 445 (pipeline 0.080 s, 1.4x WSL; func/loop_big 6-8% slower: fixed in 447, re-run to follow) |
| 447 | 2026-10-28 | No empty flush after every builtin (step (c) made func +6%, loop_big +8%); the publish doc review |
| 446 | 2026-10-28 | Deployed rev 445 (own stdout buffered, stderr never buffered; AOT suite 198/0 both modes, battery 216); notified; next: publish |
| 445 | 2026-10-28 | C#Bash's own redirected stdout is buffered (step (c)); stderr is never left buffered (fixes a 433-442 reorder) |
| 444 | 2026-10-28 | check-help-urls keeps the pages it got (a cache) and checks their anchors even when a run stops |
| 443 | 2026-10-28 | Deployed rev 442 (buffered pipes; AOT suite 197/0 both modes, battery 216); notified; next: step (c) |
| 442 | 2026-10-28 | Output into a pipeline's pipe is buffered, written when each builtin finishes (step (b)) |
| 441 | 2026-10-28 | Test runner: each console-mode case gets an empty, closed stdin ($null \|), as -Detached gives NUL |
| 440 | 2026-10-28 | check-help-urls paced like a person: shuffled pages, a random 20-70 s pause before each, a browser User-Agent, stop at the first refusal (exit 3) |
| 439 | 2026-10-28 | Anchor: greenlit (1) runner stdin NUL, (2) buffering step (b) pipes, (3) step (c) own stdout, then publish |
| 438 | 2026-10-28 | Deployed rev 437 (sed empty regex; AOT suite 196/0 both modes, battery 216); notified; "2 & 3 and then 6" done |
| 437 | 2026-10-28 | sed's empty regex `//` is the last regex executed (GNU); runtime error when none has run |
| 436 | 2026-10-28 | Deployed rev 435 (arith << fix; AOT suite 195/0 both modes, battery 216); notified koliada-net-28, kes-term-a1; next: sed's empty regex |
| 435 | 2026-10-28 | `<<` inside an unquoted $(( )) is a shift: the lexer queued a here-document and the next line failed |
| 434 | 2026-10-28 | Deployed rev 433 (buffered file output; AOT suite 194/0 both modes, battery 216); notified koliada-net-28, kes-term-a1; next: item 6 |
| 433 | 2026-10-28 | Output to a redirected file is buffered, written when each builtin finishes (step (a)) |
| 432 | 2026-10-28 | Agreed + go: buffered output to redirected files, flushed when each builtin finishes (step (a) first); item 6 paused, its findings recorded |
| 431 | 2026-10-28 | Measured, no change: cat, wc and the checksums keep 64 KiB reads (8 KiB slower for them); anchor: item 5 is the architect's (gnu.org blocks), the order 2, 3, then 6 |
| 430 | 2026-10-28 | README: "Money talks, bullshit walks:" (the saying's order; for the next publish) |
| 429 | 2026-10-28 | Expected files fieldutils, path_lookup, unserviced_log converted from CRLF to LF (CRs only; suite 192/0 both modes) |
| 428 | 2026-10-28 | Correction: the three CR expected files are CRLF line endings, harmless (the runner ignores line endings; verified); no second push needed |
| 427 | 2026-10-28 | Published: GitHub main = git 9a5a95c = hg rev 426 by tools/publish.sh (compare3 on deployed 424); found: three expected files lose a needed CR in git |
| 426 | 2026-10-28 | Benchmark of the deployed build (rev 424) and the doc review, for publishing |
| 425 | 2026-10-27 | Deployed rev 424 (head; AOT suite 193/0 both modes, battery 216); notified; the greenlit list is done; anchor rewritten (next: publish, awaiting the architect) |
| 424 | 2026-10-27 | head -n N keeps a last line's missing newline; still the shared line reader (plan revised, measured) |
| 423 | 2026-10-27 | Deployed rev 422 (fold+strings pooled; AOT suite 192/0 both modes, battery 216); notified koliada-net-28, kes-term-a1; next: head -n N |
| 422 | 2026-10-27 | fold and strings use the pooled block; tr keeps its own buffer (measured) |
| 421 | 2026-10-27 | Deployed rev 420 (tee; AOT suite 192/0 both modes, battery 216); notified koliada-net-28, kes-term-a1; next: fold, tr, strings (measure first) |
| 420 | 2026-10-27 | tee copies a here-document's surrogate pairs whole; a pooled block read 8 KiB at a time (measured) |
| 419 | 2026-10-27 | Deployed rev 418 (tail -f; AOT suite 191/0 both modes, battery 216); notified koliada-net-28, kes-term-a1; next: tee |
| 418 | 2026-10-27 | tail -f writes each appended byte once (a busy writer's lines came out twice) |
| 417 | 2026-10-27 | Deployed rev 416 (paste; AOT suite 190/0 both modes, battery 216); notified koliada-net-28, kes-term-a1; next: tail -f |
| 416 | 2026-10-27 | paste a row at a time; `paste - -` reads stdin in turn; GNU's empty delimiters; -z |
| 415 | 2026-10-27 | Deployed rev 414 (comm; AOT suite 189/0 both modes, battery 216); notified koliada-net-28, kes-term-a1; next: paste |
| 414 | 2026-10-27 | comm merges a line at a time; --total, -z, GNU's order check, empty delimiter = NUL |
| 413 | 2026-10-27 | Deployed rev 412 (uniq; AOT suite 188/0 both modes, battery 216); notified koliada-net-28, kes-term-a1; next: comm |
| 412 | 2026-10-27 | uniq a group at a time; coreutils' -d/-u/-D switches; -D -c refused; -z; -f blanks are space/tab |
| 411 | 2026-10-27 | Deployed rev 410 (sed streams; AOT suite 187/0 both modes, battery 216); notified koliada-net-28, kes-term-a1; next: uniq |
| 410 | 2026-10-27 | sed reads as it goes; GNU's per-line newline flags; q ends the run; w files once; -i via a temporary file |
| 409 | 2026-10-27 | Deployed rev 408 (the output encoder; AOT suite 186/0 both modes, battery 216); notified koliada-net-28, kes-term-a1; next: sed |
| 408 | 2026-10-27 | The output encoder carries a surrogate pair across chunks (emoji broke past 8192 characters) |
| 407 | 2026-10-27 | Deployed rev 406 (split's suffix; AOT suite 185/0 both modes, battery 216); notified koliada-net-28, kes-term-a1; next: sed |
| 406 | 2026-10-27 | split's suffix widens or stops as GNU's (it wrapped and overwrote the first pieces); -a checked |
| 405 | 2026-10-27 | Deployed rev 404 (AOT suite 185/0 both modes, battery 216); notified koliada-net-28, kes-term-a1; anchor rewritten as a resume point (next: split's suffix, GNU's rule recorded) |
| 404 | 2026-10-27 | Anchor: the match-GNU item ruled; the tool-by-tool sequence greenlit (deploy, then split, sed, uniq, comm, paste, tail -f, tee, fold/tr/strings, head -n; then publish) |
| 403 | 2026-10-27 | Built (phase B2): split and tac on the shared byte layer; tac keeps a last line without a newline (GNU); ReadBytes removed |
| 402 | 2026-10-27 | Built (phase B1): cmp, od, hexdump, base64 on the shared byte layer; rev 397's overflow bugs fixed; base64 -d as GNU |
| 401 | 2026-10-27 | Built: one shared byte layer (Builtins.Bytes.cs) with pooled blocks; wc's ASCII path; faster than rev 397 at every size measured |
| 400 | 2026-10-27 | Fixed: wc and md5sum/sha1sum/sha256sum/sha512sum over 2 GiB: counted and hashed as they read |
| 399 | 2026-10-27 | Anchor: wc over 2 GiB reported by koliada-net-28 (reproduced on rev 397), queued behind the architect's go |
| 398 | 2026-10-27 | Deployed rev 397 (AOT suite 180/0 both modes, battery 216); notified koliada-net-28; anchor: deployed state |
| 397 | 2026-10-27 | Built: help and --help point at the manual pages (help.json); enable; a log of requests C#Bash does not service |
| 396 | 2026-10-27 | Fixed: tail, and head's -c -N / -n -N / -z, on a file over 2 GiB: bytes as they are, a file read from its end |
| 395 | 2026-10-20 | Anchor (d): the module work in PortableBash now awaits the ratification of its design record, spec and architecture together; the runtime ruling noted |
| 394 | 2026-10-20 | Handoff: the anchor up to date (tip, deployed and notified 373, unpublished 367-394, module work in PortableBash, item (d) un-garbled) |
| 393 | 2026-10-20 | Anchor: the PortableBash shell is named xpBash (was zigBash) |
| 392 | 2026-10-20 | PortableBash created (F:/Koliada/Tools/PortableBash): the module documents moved there and frozen here at rev 391; anchor points at PortableBash |
| 391 | 2026-10-20 | Ruled: PortableBash is the contract, Zig modules and zigBash (deferred, designed for now); C#Bash a host, legacy once zigBash is built; portability high but negotiable; Linux, macOS, WASM/bare-metal in that order |
| 390 | 2026-10-20 | Proposed by the architect: v2 as a GitHub project PortableBash (any platform), C#Bash continuing, a zigBash; the 2.0 plan and spec on hold pending discussion |
| 389 | 2026-10-20 | The 2.0 plan (docs/v2-plan.md, proposed) and phase 1: the module specification v1 in draft (docs/module-spec.md); its Zig declarations compile, with a failing control; anchor (d) rewritten |
| 388 | 2026-10-20 | Ruled: the override is a file (~/.bash/providers) plus a variable; signing by one signed file (Authenticode on public traction); the installer built with InstallGen; everything under ~/.bash (layout proposed: bin/Bash.exe alone, modules/, providers, commands/) |
| 387 | 2026-10-20 | Ratified: contract draft 3 as the basis of v1 (Zig, one-file form), the cache design; replaceable built-ins decided (modules after built-ins; override for coreutils except kill; shell builtins never); signing and the override's form put to the architect; ~/.bash installed location and an installer ruled |
| 386 | 2026-10-20 | Measured on Windows 11 (26300): the Zig one-file module passes there too. Contract DRAFT 3: modules in Zig, one plain EXE loaded directly with the shell binding its imports; no packaging, no module cache |
| 385 | 2026-10-20 | Measured: a Zig module is one plain EXE loaded directly once the shell binds its imports (Windows binds none for an EXE loaded as a library): 0.22 ms first use in-process, 2.3 ms as a program, 50 KB |
| 384 | 2026-10-20 | Ruled: the internal/external/MSYS split stands, a C# module's first call (~0.6 ms) accepted, runtime sharing parked for the architect's alternate-runtime project. Contract DRAFT 2: hot-path criteria, no repeated work, the ratified streams/files/2.0, costs per provider |
| 383 | 2026-10-20 | Found: a module DLL cannot use Bash.exe's runtime (nothing exported; own runtime and core types per image); ILC's multi-module mode shares at build time only; IlcDehydrate=false saves ~0.06 ms of the ~0.5 ms start |
| 382 | 2026-10-20 | Measured: an AOT module DLL starts its own runtime at its first call (~0.6 ms, isolated with a no-op export; not the GC thread); a C module (zig cc) loads in ~0.12 ms and needs no start |
| 381 | 2026-10-20 | Ratified: streams as callbacks (handle on request), files option c (resolve + open through the shell), C#Bash 2.0.0 on a named branch; the exec and file hot paths are the gold-standard criteria |
| 380 | 2026-10-20 | Ruled: providers per utility with overridable default order; MSYS as a bridge module; path-invoked MSYS programs to the bridge. Contract DRAFT 1 (providers, csb_provides, handle on request, declined = next provider, resolutions in the command map: Claude's position to confirm) |
| 379 | 2026-10-20 | Draft 0 of the utility-module contract (docs/module-contract.md): for discussion, nothing ratified; anchor points at it |
| 378 | 2026-10-20 | Measured: the packaged form with a C launcher (zig cc, 58,880 bytes): packaged tool 1.18 MB (runtime once), 7.3-7.6 ms as a program, ~1.1 ms in-process warm; Installer's Defender lesson applied |
| 377 | 2026-10-20 | Measured: the packaged form works: one EXE carrying its module DLL as a Win32 resource; warm in-process ~1.1 ms vs ~7-8 ms as a program; cold once per version 23-32 ms; ~1.1-1.2 MB of runtime per module and per launcher |
| 376 | 2026-10-20 | Measured: Native AOT utility modules work (load 0.2 ms, first call ~0.7 ms, 0.3 us after, two runtimes in one process) against 7 ms for a process start; one file the direct way (an AOT EXE's exports, loaded) crashes |
| 375 | 2026-10-20 | Docs: how the build and tests use WSL, Git for Windows and MSYS2 (README 'What the build and the tests use', IMPLEMENTATION §11 Tooling, the old §11 now §12); 184 cases; claudecall.sh's cygpath recorded as open |
| 374 | 2026-10-20 | Deployed rev 373 (suite 176/0 on AOT both modes, battery 216, baseline 216); anchor: state, (c) done, the module proposal, sh, the battery's place in deploy |
| 373 | 2026-10-20 | Tests no longer need Git: a case whose program is missing prints SKIP: <reason> and the runner names it; detach_builtins on "$BASH" -c; the three self-passing checks and the PATH-awk lines moved to their own cases (native_args_msys, native_args_git, dev_tty_git_prompt, awk_fallthrough); battery baseline 216 without the machine-tool probes |
| 372 | 2026-10-20 | Proposed by the architect: utilities as loadable modules (a DLL in an EXE: loaded by C#Bash, or run standalone); no MSYS in C#Bash; the MSYS stand-in plan withdrawn; open questions recorded |
| 371 | 2026-10-20 | Found: how the tests use Git for Windows: the six failures, three checks that pass without testing, the battery's six; a bare sh finds nothing (raised) |
| 370 | 2026-10-20 | Deploy of rev 369 refused at the suite: the cygpath fix works end to end; on the AOT build exactly the Git-dependent six fail (icu_lazy passes); the battery's place in deploy raised; anchor (c) |
| 369 | 2026-10-20 | deploy.sh no longer needs cygpath (Git's): paths handed to cmd.exe and PowerShell as C#Bash gives them; a root that is not a drive path refused; MSYS2_ARG_CONV_EXCL kept (C#Bash honours it) |
| 368 | 2026-10-20 | Found: tools/deploy.sh needs cygpath (Git's, off PATH since 2026-10-19); the deploy of rev 367 stopped at step 2, nothing changed in dist; recorded as open with Claude's position; anchor (c) |
| 367 | 2026-10-19 | Published: GitHub main = git 0a8eef7 = hg rev 366 by tools/publish.sh (compare3 re-run on deployed 324; not deployed); anchor updated |
| 366 | 2026-10-19 | Doc review for the push: README's table from compare3 on deployed rev 324 (2026-10-19 18:45; every row within a few percent of the 04:15 run, Git Bash and WSL slower too, so the machine), its prose figures, 180 cases; results.md |
| 365 | 2026-10-19 | Anchor: tip 365; the global CLAUDE.md committed in its git repository (65ecb3b) |
| 364 | 2026-10-19 | run-tests.ps1 ASCII-only again (a .ps1 without a BOM is read as ANSI): 7 typographic characters in comments, from revs 3, 6, 25, 39, 111, 188, made ASCII; suite 173 passed + the 6 Git-dependent failures, both modes |
| 363 | 2026-10-19 | Anchor: tip 363; the global CLAUDE.md edit left uncommitted for the architect (a git repository) |
| 362 | 2026-10-19 | Docs for today's fixes: ARCHITECTURE (a ~ in a path is a name), IMPLEMENTATION's file map (batch arguments, ShellToken, exported functions, append-only >>, Not a directory, read -u's reader), README (a builtin's fallback never uses Windows' own program) |
| 361 | 2026-10-19 | Anchor for the restart: tip 360, not deployed (324) or published (333); the red team's fixes by revision; what remains (F14, F15, older LOWs: paused for the architect's steer); awaiting: the awk hang, arithmetic error parity, Git-dependent tests (deploy would refuse); how each fix was verified |
| 360 | 2026-10-19 | Red team F12: set -x shows (( )) and arithmetic for expanded (evaluated once), an empty for-part as (( 1 )), for i as for i in "$@", '' for an empty [[ ]] operand, a tilde pattern quoted (and matched literally); case xtrace_expanded; a suite hang on awk recorded (open) |
| 359 | 2026-10-19 | Found beside red team F11: sed reads a [ in a replacement or y as a character (it swallowed the delimiter; two -e scripts ran together); case sed_brackets_text |
| 358 | 2026-10-19 | Red team F11: set -x as bash 5.1: octal control characters (printf %q too), export/readonly assignment lines, subscripts as written, eval one level deeper, BASH_XTRACEFD; case xtrace_details |
| 357 | 2026-10-19 | Test: traps_debug_return no longer prints CPU times (a 15.6 ms tick made it fail 2 runs in 30); expectation re-taken from GNU |
| 356 | 2026-10-19 | Red team F16: mktemp's implied suffix for a template not ending in X (aXXXXb was the literal name); too few X's and --suffix as GNU; case mktemp_suffix |
| 355 | 2026-10-19 | Red team F13: a path through a file is Not a directory for a command (hostname.exe/. ran), cat, ls, du, chmod and redirects; case path_through_file |
| 354 | 2026-10-19 | Anchor: the red team's MEDs done (revs 346-353); the LOWs next; arithmetic error parity found, open |
| 353 | 2026-10-19 | Red team (older MED): an arithmetic variable whose value is no valid expression is an error, not 0; self-reference is the recursion error (cap 64); found: arithmetic error parity (open); case arith_bad_value |
| 352 | 2026-10-19 | Red team (older MED): export -f / declare -fx reach a child as BASH_FUNC_name%% and are imported at startup (one definition only, Shellshock rule); per-subshell; declare -F shows -fx; case export_function |
| 351 | 2026-10-19 | Red team (older MED): source reads /dev/null, /dev/stdin and /dev/fd/N as bash does (they were No such file or directory); case source_devices |
| 350 | 2026-10-19 | Red team F10: a program started from a directory of 259 characters says the Windows limit (over 258), 126, not Windows' own text with 127; case cwd_limit |
| 349 | 2026-10-19 | Red team F9: a script run as a command takes env -S and its #! options (#!/bin/bash -e went on past a failure, #!/bin/sh -x traced nothing); case shebang_options |
| 348 | 2026-10-19 | Red team F8: which reports only what can run (a .js with no #! was reported); case which_program |
| 347 | 2026-10-19 | Red team F7: find -execdir takes a starting point as written (. ran in the parent) and refuses a PATH holding the current directory or a relative entry, as GNU; case find_execdir |
| 346 | 2026-10-19 | Red team F6: realpath and readlink -f/-e/-m check each component as GNU does (all but the last must exist for -f) and drop a trailing slash; .. stays lexical; case readlink_canon |
| 345 | 2026-10-19 | Anchor: the red team's HIGHs done (revs 337-344); the MEDs next |
| 344 | 2026-10-19 | Red team (older HIGH): a builtin's unsupported-option fallback and BASH_COREUTILS=external never run Windows' own program of that name (System32's find.exe ran for find -okdir); case fallback_not_windows |
| 343 | 2026-10-19 | Red team F3: an MSYS program's >> f 2>> f keeps both lines whole: one write handle per file (by file id) for its redirects, handed-off streams and reopened inherited handles; case msys_append_shared |
| 342 | 2026-10-19 | Red team (older HIGH): two shells appending to one file keep every line: the shell's own >> is an append-only handle (and a child's >> handle is reopened by the real path, not .NET's [Unknown]); case append_concurrent |
| 341 | 2026-10-19 | The case files revs 337-340 registered but did not add (hg add was missed): bat_args, tilde_literal, subshell_fd_close, read_fd_offset |
| 340 | 2026-10-19 | Red team F2: after read -u N, the descriptor's next reader or writer starts just after the line (read-ahead given back to a file before any other use; a pipe's readers share one buffer); case read_fd_offset |
| 339 | 2026-10-19 | Red team F1: a descriptor closed in a subshell, pipeline stage, job or $( ) stays open in the parent (its stream was disposed); case subshell_fd_close |
| 338 | 2026-10-19 | Red team F5: a quoted or data ~ is a name, as in bash (it was the Windows profile whatever HOME said); startup files and ~ PATH entries from HOME; case tilde_literal |
| 337 | 2026-10-19 | Red team F4 (security): an argument to a .bat or .cmd is data: cmd.exe started explicitly, each argument quoted as Rust's std does since CVE-2024-24576, a line break refused; case bat_args |
| 336 | 2026-10-19 | Records: the no-windows hook's false positives fixed (its hg rev 4) and the user Path's trailing ';' removed; the red team on revs 307-316: 16 findings, five HIGH, awaiting the architect; a session started before Git left PATH runs Git Bash as bare bash |
| 335 | 2026-10-19 | Git reached only by the benchmark and publish.sh: the battery against WSL's GNU bash (baseline 222), publish.sh's configured git.exe, the -n corpus and the df check run in WSL's bash; README: run hash -r after installing any tool (a remembered hit hides a program added later earlier on PATH); GREENLIT list |
| 334 | 2026-10-19 | Published: GitHub main = git 0b5ece5 = hg rev 333 by tools/publish.sh (the force-push undone); resume note updated |
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
