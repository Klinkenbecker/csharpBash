# C#Bash: known issues

**This copy:** the repository's (the installer replaces this line with the build it installs).

Every C#Bash release installs this file as `%USERPROFILE%/.bash/KNOWN_ISSUES.md`, beside `unserviced.jsonl` (this
machine's log of every request C#Bash refused). It describes the build named above; the last line of `bash --version`
names the build you are running. The latest copy: https://github.com/Klinkenbecker/csharpBash/blob/main/KNOWN_ISSUES.md.
Report a new issue with the build: to the C#Bash session, or as a GitHub issue.

## Open: wrong answers, not fixed yet

Each was measured against GNU bash 5.1 (and GNU grep) on 2026-11-07, build 681; every build so far has it.

- **POSIX classes are ASCII only.** `[[:alpha:]]` and the other classes do not match a letter such as `é` in
  `[[ =~ ]]`, in `case` and glob patterns, or in `grep`; GNU, in a UTF-8 locale, matches it. Workaround: name the
  characters (`[a-zA-Zé]`).
- **An escaped comma inside braces still splits them.** `echo {x\,y,z}` prints `x y z`; bash prints `x,y z`.
- **An extglob pattern in `[[ ]]` does not match.** `[[ a == @(a|b) ]]` is false; bash takes `@( )` there even with
  `extglob` off. Workaround: `[[ $x == a || $x == b ]]`, or `case`.
- **Arithmetic error messages are worded differently** from bash's (`((: 1 + : Expected number in arithmetic
  expression`, where bash says `syntax error: operand expected (error token is "+ ")`). The status and where the
  message goes match.

## Limits: by design, or Windows'

- A bare name never runs a script from the current folder: write `./script`, as in bash.
- After installing a tool, run `hash -r`: a lookup is remembered across shells, and a remembered program hides one of
  the same name installed later in a folder earlier on PATH.
- `df -i` is not available (Windows keeps no inode counts); it goes to a PATH `df` when there is one.
- There is no terminal: `/dev/tty` reports "No such device or address", so a program that would prompt there fails
  instead of hanging.
- Not available, and each says so: restricted mode, POSIX mode, programmable completion (`complete`), `>( )`, and
  jq's `def` (the in-process jq is a jq 1.6 subset; deferred until a second request, 2026-11-07).
- Windows starts no program whose path is over 260 characters, and a file name cannot end in a dot or a space.

## Fixed in this release (build 681), since the release before it (hg-657, build 655)

Each from the build named on.

- **681:** `[[ … ]] 2>/dev/null` was a syntax error; a test inside `[[ ]]` that cannot be evaluated is false and the
  rest goes on, as in bash. Quoted text inside an `=~` bracket expression is taken as written (`^["[:digit:]"]$`).
  A nested C#Bash gets its arguments as typed (`bash -c 'echo $1' _ /c/x` printed `C:/x`). jq gained `match`,
  `capture`, `scan` and `--rawfile`.
- **677:** `[[ =~ ]]` read POSIX classes wrongly: `[[ 5 =~ [[:digit:]] ]]` failed and `[[ 'a]' =~ ^[[:alpha:]]$ ]]`
  matched. A Windows program now gets `/c/x` converted after `=` and after `-X` (`--file=C:/x`, `-IC:/x`) and in its
  environment, as under Git Bash; a word holding `;` is left whole (its first item was converted alone).
- **671:** awk's `printf` refused `%e`, `%E`, `%g`, `%G`.
- **669:** awk refused `sqrt`, `exp`, `log`, `sin`, `cos`, `atan2`.
- **668:** a Windows program was handed `/dev/null` as written (`curl -o /dev/null` exited 23); it gets `nul`.
- **665:** `iconv` runs in-process (UTF-8/16/32, ISO-8859-1, Windows-1252, ASCII).
- **662:** `ls -l` showed every file's link count as 1.
- **660:** a builtin that ran out of memory took the shell down; it ends `NAME: memory exhausted`, status 2.
