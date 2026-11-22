# C#Bash: known issues

**This copy:** the repository's (the installer replaces this line with the build it installs).

Every C#Bash release installs this file as `%USERPROFILE%/.bash/KNOWN_ISSUES.md`, beside `unserviced.jsonl` (this
machine's log of every request C#Bash, or a program in .bash/bin, refused). An issue is a possible bug, being worked on. Where C#Bash differs from
bash by Windows' limits or by design, each with the way that works, is [KNOWN_LIMITATIONS.md](KNOWN_LIMITATIONS.md),
installed beside it. It describes the build named above; the last line of `bash --version` names the build you are
running. The latest copy: https://github.com/Klinkenbecker/csharpBash/blob/main/KNOWN_ISSUES.md.
Report a new issue with the build: to the C#Bash session, or as a GitHub issue.

## Open: wrong answers, being worked on

- **A refused bare `git` is sometimes not marked** (git-9d, 2026-11-17; confirmed). With Wombat (a git replacement for
  Windows, not yet published) as `git.exe` in `%USERPROFILE%/.bash/bin`, under Claude Code's Bash tool on 885 the same
  plain command it refuses (`git log --decorate=short`) is marked and handed on to the next git in one call, and
  unmarked, so refused loudly (128), in the next: the request log (Wombat's lines) shows both in one session seconds apart
  (`%USERPROFILE%/.bash/unserviced.jsonl`, 2026-11-17T14:37:25Z "handed on, exit 127" and 14:37:37Z "failed, exit
  128", args `log -1 --decorate=short --format=%h`). The form of stdin, `-C` and where stdout goes are ruled out; the
  cause is not found yet.
- **`coproc` is not implemented, but `type` calls it a keyword** (claude-c3 on guy-windows11, 806; relayed by guy-bf,
  the same on 885, 2026-11-17). `type coproc` says "coproc is a shell keyword"; `coproc CP { echo hi; }` is a syntax
  error near `}` (status 2), and `coproc echo hi` is "command not found" (127). GNU bash runs both. Its want, a
  two-way pipe to a child (to drive `claude -p --input-format stream-json --output-format stream-json`), is on the
  features list.
- **jq's `def` and `foreach` say "jq: command not found"** (`def`: guy-0a; `foreach`: claude-c3; both on guy-windows11,
  relayed by guy-bf, 2026-11-17). Neither is in the in-process jq (`[foreach (1,2) as $x (0; . + $x)]`, jq 1.6: `[1,3]`;
  on 885 here WSL's jq answers it). On build 806,
  `jq -n 'def f: 1; f'` printed `jq: command not found`, status 127, though jq is there: the in-process jq has no
  `def`, and the fallback found no full jq. On 885 here WSL's jq answers it (`1`); with no WSL in the chain 885 says
  `jq: def (function definitions): not supported by C#Bash's in-process jq` and why, status 2. Not yet tried: 885 with
  WSL in the chain but no jq in WSL (guy-windows11's case); ASSUMED it still says "command not found", 127, as USING
  says a tool WSL lacks does. `def` itself stays out of the in-process jq (DECISIONS "RULED: jq `def` deferred until a
  second request"; whether this report is that second request is the architect's).
- **`%USERPROFILE%/.bash/help.json` is not refreshed from three earlier tables** (found 2026-11-17). C#Bash replaces the file when it holds an
  earlier built-in table, recognized by hash, but the hashes of the tables at revs 553, 645 and 665 are missing, so a
  file written by one of those stays. No effect today: later tables only added topics, which the built-in table answers.
- **A quoted argument to `cmd //c` reaches cmd as `\"`** (glassmirror-4e on guy-windows11, 681; relayed by guy-bf,
  the same on 885, 2026-11-17). `cmd //c '"C:/Windows/System32/hostname.exe"'` says `'\"C:/Windows/System32/hostname.exe\"'
  is not recognized` (1), where the unquoted form runs; `cmd //c 'type "C:\x\y.json"'` fails the same way. The usual
  argv escaping (`\"`), which cmd does not read. Not yet compared with Git Bash. Until then: put the cmd commands in a
  `.cmd` file and run that.
- **grep -E with a bounded repeat such as `.{0,300}` is about 135 times slower than GNU grep** (facts-8a, 2026-11-17,
  885; not yet reproduced here). On a 3 MB file, `grep -o -E '.{0,300}"unsigned", "invalid".{0,200}'` took 6.2 s
  against GNU grep 3.0's 46 ms (the same match, both exit 0), `-c` 11 s; the same file without the repeats 25 ms; a 36
  MB file ran past 120 s. Until fixed: drop the `.{0,N}` context (`grep -b`, or fixed strings with `-F`).
- **/tmp with TMP and TEMP both in MSYS form** (found fixing 877, 2026-11-16). A shell started with `TMP=/tmp/x
  TEMP=/tmp/y` takes /tmp from the user's temp folder (%LOCALAPPDATA%/Temp), which may not be the /tmp of the shell
  that started it (under Claude Code that one comes from its TEMP).
- **`cp -a` does not keep hard links within what it copies** (found 2026-11-22, 885). Two names of one file (`ln f g`)
  copied with `cp -a` become two separate files; GNU cp (8.32) keeps them one file, as `-a` implies `--preserve=links`.
  `cp -R` keeps none in either. Until fixed: remake the link in the copy (`ln`).

## Fixed in this release (build 885), since the release before it (hg-861, build 859)

- **864, 868:** new: the refusal protocol. A program in `%USERPROFILE%/.bash/bin` (C#Bash's own `bash` and `sh`
  aside) that exits 127 having done nothing (it cannot serve that request) has the command run again by the next
  program of that name in the fallback chain (USING.md, "The fallback chain"; `help fallback`); any other status is
  its answer. Such a call is marked with `BASH_RERUN_ON_127=1` in the program's environment (868), and no other is
  unless the mark is written on the command itself (885: to test a program against the protocol), so a program
  refuses in silence only where it will be run again.
- **870:** a shell started with no console (Claude Code's tool shell) lost its console when its FIRST program was ended
  with its process tree, by a broken pipe (`prog | head -1`), `timeout` or `kill`: that program's console host went
  too, and every later program got a fresh console on code page 437, so a .NET program wrote CP437 (`─` as byte `c4`,
  not `e2 94 80`). A console's host is now never killed; it ends by itself with its last program. Reported by facts-8a.
- **872:** an output redirection onto fd 0 (`0> f`, `0>> f`, `0>/dev/null`) for what C#Bash runs in-process (a
  builtin, `env`, a function, a group) was installed as stdout: `env X=1 git hash-object --stdin 0> f` wrote git's
  output into f, and `echo kept 0>/dev/null` printed nothing. Now it is stdin and stdout is untouched. Reported by
  git-9d.
- **874:** a descriptor closed or open the wrong way is bash's "Bad file descriptor" for what C#Bash runs in-process.
  A stdin closed or open for writing (`0<&-`, `0> f`, `0>&1`) was read as empty, status 0: a read now fails, with
  bash's or GNU's words and status (`read: read error: 0: Bad file descriptor` 1, `sed: read error on stdin: ...` 4,
  `sort: stat failed: -: ...` 2; mapfile ends quietly, as bash's). A stdout closed or open for reading (`>&-`, `1<&-`,
  `1< f`) was written as usual or nowhere: a write now fails, `echo: write error: Bad file descriptor`, status 1. A
  message to such a stderr is lost, as bash loses it. A program C#Bash starts gets NUL there (Windows has no closed
  descriptor).
- **877:** /tmp followed TMP in a shell started by C#Bash: `TMP=/tmp/x bash -c 'cat /tmp/f'` looked in C:/tmp/x (the
  child got TMP as typed and read it as a Windows path), and the child's mktemp and process substitutions failed
  there. /tmp now comes from TMP or TEMP only when it is a full Windows path, else the user's temp folder; the child
  still sees `$TMP` as typed. Reported by facts-8a.
- **882:** an unclosed construct is reported by the innermost one left open, as bash reports it: `"${x:+it's}"` and
  `${x:+it's}` say ``looking for matching `''`` (C#Bash said `"` and `}`), `"$(echo 'hi)"` a `'`, and
  ``"`echo hi"`` a backquote.
