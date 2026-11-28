# C#Bash: known issues

**This copy:** the repository's (the installer replaces this line with the build it installs).

Every C#Bash release installs this file as `%USERPROFILE%/.bash/KNOWN_ISSUES.md`, beside `unserviced.jsonl` (this
machine's log of every request C#Bash, or a program in .bash/bin, refused). An issue is a possible bug, being worked on. Where C#Bash differs from
bash by Windows' limits or by design, each with the way that works, is [KNOWN_LIMITATIONS.md](KNOWN_LIMITATIONS.md),
installed beside it. It describes the build named above; the last line of `bash --version` names the build you are
running. The latest copy: https://github.com/Klinkenbecker/csharpBash/blob/main/KNOWN_ISSUES.md.
Report a new issue, with the build, as a GitHub issue.

## Open: wrong answers, being worked on

- **`cal` reads only `cal MONTH YEAR` and ignores everything else** (found 2026-11-24, 926). `cal 2025`, `cal -y 2025`
  and `cal --bogus` all print this month, status 0. util-linux cal prints the whole year for the first two and refuses
  an unknown option (not compared here: this machine has no other cal). Until fixed: `cal MONTH YEAR`.

## Fixed in build 999 (first released as hg-1001), since hg-905 (build 885)

- **999:** `tr` read an option after its first operand (found 2026-11-27): `tr a -d` deleted `a`, where GNU's maps it to
  `-`, and a third operand, or a second with `-d` alone, was taken silently. Every word after the first operand is now
  an operand, with GNU's operand errors in its words (`tr: extra operand 'x'`, the second and third lines). And `tr -s`
  with two sets squeezes a run of any character of the second set after translating, as GNU's: `tr -s a b` of `aab`
  gives `b` (it gave `bb`).
- **997:** `> /dev/stdin` when stdin is a pipe or a here-document said "No such file or directory".
  It is now refused as Windows must: "Permission denied", status 1 (KNOWN_LIMITATIONS); a file and
  /dev/null are written since 982.
- **996:** an option no version of a tool has was handed on to another tool on the chain:
  `grep -m1 -F "- x" f` reads `- x` as options, and an older grep on PATH answered `invalid option -- m`, blaming a
  valid option; others answered in their own words with status 255. A short option that is no letter or digit is now
  refused by C#Bash in GNU's words and status, tool by tool (`grep: invalid option -- ' '`, its usage line and hint,
  2), and never handed on; sed gives its usage line, where GNU prints its whole usage. unzip is not affected (Info-ZIP's
  has `-$`, `-:` and `-^`). Use `-e "- x"` for a pattern that starts with `-`.
- **993:** inside `while read ...; do prog; done << EOF` (or `<<<`), the first native program the loop started took
  the whole rest of the document, though it read none of it, so the loop ran once, with no error.
  A program now leaves what it does not read to the loop, as a
  bash child shares the document's pipe, also inside `$( )`, a function or a pipeline stage; one that reads it all
  still takes it all.
- **992:** a program given a here-document or here-string over 65,536 bytes got it through a pipe (found
  2026-11-27), so `bash -c '[ -f /dev/stdin ]' <<< "$big"` was false. It now gets a temporary file, deleted when the
  program closes it, as GNU bash 5.1 hands it over; a smaller one still goes through a pipe.
- **990:** `cp --preserve=LIST` kept the modification time whatever the list said (found 2026-11-27). The time is
  now kept only for `-p`, `-a`, a bare `--preserve`, or a list naming `timestamps` or `all`, as GNU's;
  `--preserve=links`, `--preserve=mode` and `-d` stamp the copy now, and `--no-preserve` takes it back in the order
  given (`--no-preserve=timestamps -p` keeps it). For files and folders alike.
- **989:** `shopt -s login_shell` turned the option on, and `shopt -u login_shell` off; Claude
  Code's shell snapshot runs `shopt -s login_shell`, so every Claude Code command reported a login shell. The option
  says how the shell was started: setting or unsetting it, or `-O login_shell`, is ignored with status 0, as GNU's.
- **985:** `mv` of a folder to another drive whose source could not all be removed said only `cannot move 'SRC' to
  'DST'`, not that a full copy was left (found 2026-11-24), and a read-only file in it stopped the removal. It now
  removes the source as `rm -rf` does after the copy: a read-only file goes (GNU removes a write-protected one), and
  each entry that cannot go is named, `mv: cannot remove 'SRC/dir/x': Permission denied`, status 1, as GNU's.
- **982:** `/dev/stdin`, `/dev/stdout`, `/dev/stderr` and `/dev/fd/N` now test as what the descriptor is:
  `-p` is true on a pipe (a pipeline, a pipe into `bash -c`, `$( )`, a here-document or here-string up to
  65,536 bytes), `-f` and `-s` on a file (`< f`, `exec 0< f`, `<&3`, `/dev/fd/3`), `-c` on /dev/null. They were false
  for all but `-c`, as in Git® Bash 4.4 (PortableGit's bash); GNU bash 5.1's answers now. And `> /dev/stdin` (or `/dev/fd/0`) writes to what
  stdin is: /dev/null, or the file itself, emptied by `>` and appended to by `>>`, also from a
  program and inside one, and `cmd < f > /dev/stdin` writes `f`; it was "No such file or directory" whatever stdin was.
- **979:** `cp -a` did not keep hard links within what it copied (found 2026-11-22): two names of one file became two
  separate files. Two names of one source file met in one `cp` now become two names of one copy, as GNU's `-a` does
  (it implies `--preserve=links`); `-d`, `--preserve=links` and `--preserve=all` keep them too, and `-a`, `-d`,
  `--preserve` and `--no-preserve` apply in the order given, as GNU's (`--no-preserve=links -a` keeps links). A link to
  a file outside the copy, `-R`, `-p` and two separate `cp` calls still give separate files, as in GNU.
- **954:** a folder named before `PATH` in `unix-tools` was searched ahead of the shell's own folder, so a nested
  `bash` or `sh`, and any program in `%USERPROFILE%/.bash/bin`, could be another folder's (with
  `unix-tools = F:/x;PATH`, `bash -c` ran `F:/x`'s bash). The shell's own folder now comes first, before every step of
  the chain; a name only the folder has still runs from it. **956:** the same held when a script reordered PATH
  (`PATH="/x:$PATH"` with a `bash` in `/x` ran `/x`'s): the shell's own folder now comes first whatever PATH says.
- **963:** under Claude Code `$TMP` and `$TEMP` read `F:\TEMP`, as Claude Code exports them before every command, and
  a backslash path broke `case`, `[[ == ]]`, `${f#$TMP/}`, `sed` and JSON. A Windows path assigned to `TMP`, `TEMP` or
  `TMPDIR` is now stored with forward slashes.
- **940:** one temp folder. `/tmp`, `$TMP`, `$TEMP` and `$TMPDIR` now all name the same folder, with forward slashes
  (under Claude Code from 963):
  they arrived in up to
  three spellings (`F:\TEMP`, `F:/TEMP`, `/f/TEMP`), and a shell started with `TMP=/tmp/x TEMP=/tmp/y`
  took another folder as its /tmp than the shell that started it (found 2026-11-16). A C#Bash started from C#Bash
  keeps its parent's folder, whatever is given for it; any other program gets what is given. `mktemp` ignored a
  `TMPDIR` set in the shell, per command or exported; it now uses it, as GNU's does.
- **937:** `grep` with a bounded repeat around a literal, such as `grep -o -E '.{0,300}"unsigned", "invalid".{0,200}'`,
  was many times slower than GNU grep (6.2 s against 46 ms on a 3 MB file, past 120 s on 36 MB):
  every line was tried from every position. A line that lacks a literal every match must contain is now passed over
  before the pattern is tried, as GNU grep does: on a 3 MB file 0.08 s, where it was 1.5 s (GNU grep 3.1: 0.08 s). A
  line that holds the literal is still matched the slow way, so a pattern whose literal is on most lines gains nothing.
- **934:** `%USERPROFILE%/.bash/help.json` written by the builds of revs 553, 645 or 665 was never replaced by a later
  build's table (found 2026-11-17): C#Bash replaces an earlier built-in table it recognizes, unedited, but those three
  were not among the ones it recognized. They are now, so such a file is replaced the next time `help` reads it (one
  line on stderr says so). An edited file is still never touched.
- **932:** a request handed on to WSL said "command not found", 127, when WSL lacked the tool too, though the tool
  was right here: jq's `def` and `foreach` on another machine said `jq: command not found`,
  and a refusing `.bash/bin` program's request said the same. Now it gets the answer it
  gets with no WSL step: the tool's refusal and why, status 2 (`jq: def (function definitions): not supported by
  C#Bash's in-process jq`), or `<program> does not serve this request, and unix-tools has no other`, 127. Measured
  with a refusing program; a WSL without jq was not tried (WSL here has it). `def` and `foreach` themselves are not in
  the in-process jq (KNOWN_LIMITATIONS "Not available").
- **914:** a refused program in `%USERPROFILE%/.bash/bin` was sometimes neither marked nor run again
  (the same refused `git` handed on in one Claude Code call, refused loudly with 128 in the next). Claude
  Code runs a call whose text holds no `<` as `eval '<command>' < /dev/null`, and that `< /dev/null`, like any on a
  group or a builtin, or a closed stdin, reached a program as a pumped empty stream that C#Bash counted as input it
  could not give again. Such a program now gets NUL, as `/dev/null` reaches every program C#Bash starts, so it is
  marked and a refusal is run again.
- **907:** `select` and `coproc` were a syntax error (`coproc CP { echo hi; }`, every `select`, status 2, which stopped
  a script even where the line never ran) or "command not found" (`coproc cat`, 127), and the syntax errors were never
  counted in the request log. Both now parse as bash parses them (`declare -f` prints them back as bash does), and
  running one says it is not currently available, with the link to ask for it, status 2, counted (KNOWN_LIMITATIONS
  "Not available"). `type` calling them keywords was right: they are bash's.
