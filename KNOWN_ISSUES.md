# C#Bash: known issues

**This copy:** the repository's (the installer replaces this line with the build it installs).

Every C#Bash release installs this file as `%USERPROFILE%/.bash/KNOWN_ISSUES.md`, beside `unserviced.jsonl` (this
machine's log of every request C#Bash refused). It describes the build named above; the last line of `bash --version`
names the build you are running. The latest copy: https://github.com/Klinkenbecker/csharpBash/blob/main/KNOWN_ISSUES.md.
Report a new issue with the build: to the C#Bash session, or as a GitHub issue.

## Open: wrong answers, not fixed yet

Each was measured against GNU bash 5.1 (and GNU grep) on 2026-11-07, on the build then current; every build so far has it.

- **An extglob pattern in `[[ ]]` does not match.** `[[ a == @(a|b) ]]` is false; bash takes `@( )` there even with
  `extglob` off. Workaround: `[[ $x == a || $x == b ]]`, or `case`.
- **Arithmetic error messages are worded differently** from bash's (`((: 1 + : Expected number in arithmetic
  expression`, where bash says `syntax error: operand expected (error token is "+ ")`). The status and where the
  message goes match.
- **A word with a non-ASCII character reaches an MSYS2 program with its quotes on.** `printf '[%s]\n' 'é{x}'` run by
  MSYS2's own `printf.exe` prints `["é{x}"]`, where bash prints `[é{x}]` (MSYS2 runtime 3.1.7, measured 2026-11-09).
  Git for Windows' MSYS programs receive it correctly.

## Limits: by design, or Windows'

- A bare name never runs a script from the current folder: write `./script`, as in bash.
- After installing a tool, run `hash -r`: a lookup is remembered across shells, and a remembered program hides one of
  the same name installed later in a folder earlier on PATH.
- `df -i` is not available (Windows keeps no inode counts); it goes to a PATH `df` when there is one.
- There is no `cygpath`, and none is needed: C#Bash's paths are already Windows-rooted with forward slashes (`$PWD` is
  `C:/x`), which every .NET tool reads. For an absolute path write `"$PWD/x"`; a relative one works as it is (a program
  starts in the shell's folder), and a Windows program handed `/c/x` gets `C:/x`. (Git Bash's cygpath is Git's own, in
  its usr/bin, which is on no PATH here.)
- There is no terminal: `/dev/tty` reports "No such device or address", so a program that would prompt there fails
  instead of hanging.
- Not available, and each says so: restricted mode, POSIX mode, programmable completion (`complete`), `>( )`, extglob
  (`shopt -s extglob` is refused, so `@( )`, `*( )`, `+( )`, `?( )`, `!( )` patterns are not available), and jq's
  `def` (the in-process jq is a jq 1.6 subset; deferred until a second request, 2026-11-07).
- Windows starts no program whose path is over 260 characters, and a file name cannot end in a dot or a space.
- A time keeps its fraction of a second to 100 ns (Windows' and .NET's precision): `touch -d '01:02:03.123456789'`
  stores `.123456700`, where GNU on Linux keeps all nine digits.
- A bare Windows device name is the device: `> nul`, `2>nul` and `cat nul` reach NUL, not a file named nul (write
  `./nul` for that file). Git Bash makes a file named nul there, one Windows' own tools cannot delete.
- A regex or a glob sees a character beyond U+FFFF (an emoji) as two: `^.$` and a lone `[[:punct:]]` do not match one,
  `^[[:punct:]]+$` does. The POSIX classes follow .NET's Unicode version where glibc's differs (two characters in the
  BMP, U+0CF3 and U+0ECE, are print here and unassigned in GNU).

## Fixed in this release (build 754), since the release before it (hg-686, build 681)

Each from the build named on.

- **754:** a negative length in a slice of an array or the positional parameters (`${a[@]:0:-1}`) counted back from the
  end; it is bash's error now ("substring expression < 0"), which abandons the command line, status 1. A bad
  substitution (`${x!y}`) abandons it too (the line went on, status 0).
- **753:** `ls -u` (access time) was ignored; it shows and sorts by the access time now. jq exits 4 on invalid input, as
  jq 1.6 (it exited 2).
- **751:** `SystemRoot`, `windir`, `ComSpec`, `ProgramFiles`, `CommonProgramFiles` and `SystemDrive` arrive upper-cased,
  as Git Bash imports them, so `$SYSTEMROOT`, `$WINDIR` and `$COMSPEC` are set.
- **748:** the change time (`stat %Z`/`%z`, `ls -c`, `find -ctime`/`-cmin`/`-newerct`) was the modification time; it is
  NTFS's change time now. `ls -c` was ignored.
- **745:** a quoted subscript in an expansion (`${P["a b"]}`, `${P['c']}`, `${A["1"]}`) read empty or failed; `cat -A`
  and `cat -v` passed bytes 0x80-0xFF raw (M- notation now); `printf %b` left `\NNN` undecoded.
- **743:** awk's `ARGV` and `ARGC` were empty, so `FILENAME==ARGV[1]` never matched.
- **741:** `tr '\001-\037'` (a range with escaped ends) deleted only the ends; jq took a raw control character in a
  string and refused `NaN` and `Infinity`, both unlike jq 1.6.
- **739:** awk's `ENVIRON` was refused.
- **733:** a here-document whose body held an apostrophe, inside `"$( )"`, failed to parse.
- **731:** `"${@:2}"` was one word instead of one per parameter.
- **729:** `touch -d` and `date -d` dropped a clock time's fraction of a second; assigning `RANDOM` did not seed it.

- **725:** awk's `rand()` and `srand()` were refused (handed to a PATH awk, or an error without one). They run
  in-process now, as gawk documents them: the seed is 1 until `srand()`; `srand(x)` seeds with x's integer part and
  returns the previous seed; `srand()` seeds with the time; one seed gives one sequence (its values are not gawk's).
- **716:** every start looked in each PATH folder for `git.exe`, about 2 ms with a long PATH and no Git on it. A start
  touches no PATH folder now.

- **704:** a file named with a Windows device name (`nul`, `con`, `aux.txt`, `com1`, with any extension) was read and
  written as the device: `cat d/nul` printed nothing, `cp x d/nul` wrote nowhere. With a folder part (`d/nul`, `./nul`)
  it is the file now, as under Git Bash; a bare name (`nul`, `> nul`, `2>nul`) stays the device, so it never leaves a
  file named nul that Windows' own tools cannot delete.
- **702:** an array assignment beside other assignments in one statement: `a=() b=()` was a syntax error and
  `x= y=1 z=()` assigned nothing (the variables stayed unset). They are made left to right now, as in bash.
- **696:** an escaped regex character in `[[ =~ ]]` was live: `\+`, `\(`, `\|`, `\^`, `\$` reached the regex without
  their backslash, so `[[ 'a+b' =~ ^a\+b$ ]]` was false and `[[ aab =~ ^a\+b$ ]]` true. Each is literal now, as in bash.
- **694:** `rm` could not remove a file named with a device name (`nul`, `con`, `aux.txt`, `com1`) or ending in a dot,
  and stopped a whole tree at the first failure: `rm -rf` of a tree holding an MSYS dev/nul exited 0, said nothing and
  left almost all of it. Now each failure names its entry, the rest still goes, and the exit is 1.
- **692:** a quoted or escaped character was live in brace and pathname expansion: `{x\,y,z}` gave `x y z` (bash
  `x,y z`), `"{"a,b}` gave `a b`, `"a[1]"*` matched `a1b`. An expansion's result was brace-expanded (`x='{a,b}';
  echo $x` gave `a b`). `{1\..3}` was a range, and `[[ axb =~ a\.b ]]` matched.
- **690:** the POSIX classes were ASCII only: `[[:alpha:]]` did not match `é` in `[[ =~ ]]`, globs, `grep`, `sed` or
  `awk`. They are GNU's in a UTF-8 locale now, checked on every BMP character (`tr` keeps ASCII classes, as GNU's does).
