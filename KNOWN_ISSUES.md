# C#Bash: known issues

**This copy:** the repository's (the installer replaces this line with the build it installs).

Every C#Bash release installs this file as `%USERPROFILE%/.bash/KNOWN_ISSUES.md`, beside `unserviced.jsonl` (this
machine's log of every request C#Bash refused). An issue is a possible bug, being worked on. Where C#Bash differs from
bash by Windows' limits or by design, each with the way that works, is [KNOWN_LIMITATIONS.md](KNOWN_LIMITATIONS.md),
installed beside it. It describes the build named above; the last line of `bash --version` names the build you are
running. The latest copy: https://github.com/Klinkenbecker/csharpBash/blob/main/KNOWN_ISSUES.md.
Report a new issue with the build: to the C#Bash session, or as a GitHub issue.

## Open: wrong answers, being worked on


None open: report a new one as above.

## Fixed in this release (build 859), since the release before it (hg-811, build 806)

- **859:** built on .NET 10 (since 847; .NET 8 before) with SDK 10.0.404, whose compiler is past CVE-2026-111497 (a
  Brotli decoder overflow in .NET's runtime libraries; C#Bash never decompresses Brotli, so it was not reachable
  through C#Bash, but builds 806-853 carried the affected code).

- **853:** arithmetic error messages are bash's words: `1 + : syntax error: operand expected (error token is "+ ")`,
  `5 / 0 : division by 0 (error token is "0 ")`, `let: …`, `((: …`, `[[: …` (they were C#Bash's own: `Expected number
  in arithmetic expression`). Base 64 takes `@` and `_` as digits (`64#@` was an error) and `0x` alone is 0.

- **851:** a glob whose path holds a quoted backslash (`"$TMP"/x*`, TMP being `F:\TEMP`) came out as a broken
  `F:/\TEMP/x...`; the folders before the pattern are now kept as typed (`F:\TEMP/x-1.txt`, as MSYS).

- **850:** a glob rooted at `/tmp` never expanded (`echo /tmp/x*` stayed literal, `ls /tmp/x*` printed nothing), while a
  plain `/tmp/...` path worked; `/tmp/` in a pattern is now the temporary folder too.

- **849:** an arithmetic error in an expansion or an assignment (`$(( 1/0 ))`, `${s:1+}`, `a[1+]=x`, an integer
  variable's `n=1+`) went on with status 2, or for an integer variable stored 0 silently; as bash, it now abandons the
  rest of the line (a script goes on with the next). `let` and `for (( ))` return 1 (they gave 2). `**` is exact
  (`3**39` was off by 11, through floating point) and a negative exponent is an error (it gave 0).

- **844:** `sed -u` into a pipe held every line until its input ended (`-u` was accepted and ignored); each line now goes
  on as written, as GNU sed.

- **841:** `[[ $x == @(a|b) ]]` was a syntax error (and a group from a variable never matched); bash takes the five
  extended groups (`@( ) *( ) +( ) ?( ) !( )`) in `[[ == ]]` and `!=` with `extglob` off, and so does C#Bash now.
  `shopt -s extglob` is still refused (KNOWN_LIMITATIONS).

- **837:** `grep --line-buffered` into a pipe held every line until its input ended (`tail -f log | grep
  --line-buffered x` delivered nothing); each line now goes on as printed, as GNU grep.

- **835:** `mkdir dir/` (a trailing slash, no `-p`) said "No such file or directory" and made nothing; `mkdir f/`
  over a file now says "File exists". Each as GNU coreutils.

- **833:** awk: `[\]]` and `[\[]` (escaped brackets in a set) matched nothing; `-e`/`--source` went to WSL's gawk (which
  cannot see `F:/` paths); `split(s, a, "")` and `FS=""` gave an empty element at each end. Each as gawk now.

- **816:** new: `access-time = preserve` in `%USERPROFILE%/.bash/etc/csharpBash.config` keeps a file's access time
  when `stat` or `ls -l` only read its details (they moved it, as under Git Bash; `default` keeps that). `benchtimer`
  bounds each launch (300 s): a hung shell is a `TIMEOUT` cell, not a stalled benchmark.

