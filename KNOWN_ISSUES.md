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

## Fixed in this release (build 798), since the release before it (hg-756, build 754)

Each from the build named on.

- **798:** `help fallback` (and `help wsl`) points at the fallback chain's documentation; a `help` that finds no topic
  adds that a command C#Bash does not provide may still run through the chain. KNOWN_LIMITATIONS.md: not listed by
  `compgen`/`type` does not mean not available, with the PDF recipe.
- **789:** `mv` of a folder another process held (its working folder inside) moved every entry, then failed on the
  folder itself: rc 1, "cannot move", the tree emptied into the target. A refused rename now moves nothing, as GNU's
  and Git Bash's mv; copy-then-delete is for a move to another drive only.
- **787:** under `shopt -s nullglob`, `local a=( * )` (also `declare`, `export`) printed EVERY variable, exported
  secrets included, and left the array empty: the array literal was itself taken as a glob that matched no file. It
  is the array's value now, as in bash.
- **785:** `PIPESTATUS` after a plain command kept an earlier pipeline's statuses (`true | false; false` left `0 1`); every
  one-command pipeline sets it now, as bash (a compound command leaves what its last pipeline set).
- **785:** `${p//%X%/R}` and `${p//#/-}`: after `//` a leading `%` or `#` was read as an anchor and nothing was replaced;
  it is the pattern's own character, as in bash (after a single `/` it stays an anchor).
- **785:** `cygpath` says, after "command not found", what to use instead (it was a bare "command not found").
- **785:** an associative array's subscript took `$( )`, backquotes and `$(( ))` literally: `${M[$(cmd)]+x}` read a set
  element as unset, and an assignment went to the wrong key; `unset 'M[$k]'` did not expand its subscript. Both as
  bash now.
- **785:** new: the fallback chain. A command C#Bash does not provide, or a tool option it lacks, is looked for in
  folders of Unix tools, PATH and WSL, in the order `%USERPROFILE%/.bash/etc/csharpBash.config` gives (`unix-tools =
  F:/tools;PATH;WSL`; no file: `PATH;WSL`). Until now a name found nowhere was "command not found" at once; with the
  default chain it is first tried in WSL (about 0.15 s), then "command not found". See USING.md, "The fallback chain".
- **770:** jq's `contains` and `inside` refused two values of different types at any depth (`[1,"a"] |
  contains(["a"])` failed); as in jq 1.6, only the top level is refused, and deeper they simply do not match. New in
  jq: `paths`, `leaf_paths`, `path`, `getpath`, `delpaths`, `tostream`, `walk`, `recurse(f)`, `recurse(f; cond)`,
  `limit`, `indices`, `index`, `rindex`, `splits`, `inside`, `label`/`break`, `input_filename`, and `-S`/`--sort-keys`,
  each as jq 1.6 (string `indices` count bytes there, as jq 1.6's do).
- **768:** `find -anewer` and `-cnewer` went to a PATH `find`; they run in-process now, as GNU find (the access or
  change time later than the reference's modification time).
- **765:** `env -u NAME NAME=value program` started the program without NAME (the removal came after the assignment);
  it removes first and then assigns now, as GNU env.
