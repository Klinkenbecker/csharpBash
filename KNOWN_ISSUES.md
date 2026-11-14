# C#Bash: known issues

**This copy:** the repository's (the installer replaces this line with the build it installs).

Every C#Bash release installs this file as `%USERPROFILE%/.bash/KNOWN_ISSUES.md`, beside `unserviced.jsonl` (this
machine's log of every request C#Bash refused). An issue is a possible bug, being worked on. Where C#Bash differs from
bash by Windows' limits or by design, each with the way that works, is [KNOWN_LIMITATIONS.md](KNOWN_LIMITATIONS.md),
installed beside it. It describes the build named above; the last line of `bash --version` names the build you are
running. The latest copy: https://github.com/Klinkenbecker/csharpBash/blob/main/KNOWN_ISSUES.md.
Report a new issue with the build: to the C#Bash session, or as a GitHub issue.

## Open: wrong answers, being worked on

Each was measured against GNU bash 5.1 (and GNU grep) on 2026-11-07, on the build then current; every build so far has it.

- **An extglob pattern in `[[ ]]` does not match.** `[[ a == @(a|b) ]]` is false; bash takes `@( )` there even with
  `extglob` off. Workaround: `[[ $x == a || $x == b ]]`, or `case`.
- **Arithmetic error messages are worded differently** from bash's (`((: 1 + : Expected number in arithmetic
  expression`, where bash says `syntax error: operand expected (error token is "+ ")`). The status and where the
  message goes match.

## Fixed in this release (build 806), since the release before it (hg-801, build 798)

- **806:** awk: a function that passed its own parameter or local on to another function passed it empty
  (`function f(x) { return g(x) }` gave `g` an empty `x`); every build since awk's user functions (2026-10-10). Arrays
  and untyped variables still pass by reference, as gawk.
- **806:** speed: `PIPESTATUS` (set after every command since 785) is written only when read; the func, loop and arith
  benchmark rows are 6-8 % faster than on 798.
