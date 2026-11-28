# C#Bash: known limitations

**This copy:** the repository's (the installer replaces this line with the build it installs).

Every C#Bash release installs this file as `%USERPROFILE%/.bash/KNOWN_LIMITATIONS.md`, beside `KNOWN_ISSUES.md`. A
limitation is not a bug: it is where C#Bash differs from bash on Linux, and each comes with the way that works. Bugs,
the ones being worked on, are in [KNOWN_ISSUES.md](KNOWN_ISSUES.md). It describes the build named above; the last line
of `bash --version` names the build you are running. The latest copy:
https://github.com/Klinkenbecker/csharpBash/blob/main/KNOWN_LIMITATIONS.md.

## Windows', .NET's and MSYS2's

- **A path over 260 characters, or a name ending in a dot or a space.** Windows starts no program whose path is over
  260 characters, and its file API drops a trailing dot or space. Use a shorter path; name files without them.
- **A time keeps its fraction of a second to 100 ns** (Windows' and .NET's precision): `touch -d '01:02:03.123456789'`
  stores `.123456700`, where GNU on Linux keeps all nine digits. Compare times to 100 ns or coarser.
- **A regex or a glob sees a character beyond U+FFFF (an emoji) as two**: `^.$` and a lone `[[:punct:]]` do not match
  one; `^[[:punct:]]+$` does. The POSIX classes follow .NET's Unicode version where glibc's differs (U+0CF3 and U+0ECE
  are print here, unassigned in GNU). Match such a character with a `+` class, or by its text.
- **Reading a file's details moves its access time** on NTFS: `stat` and `ls -l` read a file's first bytes for `#!`
  (Windows has no execute bit), as those of Git® Bash (PortableGit's bash) do, so `ls -u` and `find -atime` see the look as a use. Put
  `access-time = preserve` in `%USERPROFILE%/.bash/etc/csharpBash.config` to keep it; `cat` still counts as a read.
- **`df -i`**: Windows keeps no inode counts. It goes to the fallback chain, as any option C#Bash lacks does; in WSL,
  `df -i /` answers for WSL's own Linux disk, and a drive (`F:/`, there `/mnt/f`) for what WSL reports of it, not NTFS.
- **MSYS2's own programs (runtime 3.1.7) keep the quotes of a quoted word holding a non-ASCII character**: `'é{x}'`
  arrives as `"é{x}"`, from C#Bash or from cmd alike. It is MSYS2's parsing. Git® for Windows' MSYS programs, and C#Bash's
  own tools, receive the word correctly.
- **A quoted argument to `cmd //c` reaches cmd as `\"`**: `cmd //c '"C:/x/y.exe"'` says it is not recognized, and
  `cmd //c 'type "C:\x\y.json"'` that the syntax is incorrect. C#Bash passes arguments with Windows' usual escaping,
  which a program reading its own arguments decodes, as PortableGit's bash does (the same output, 4.4 and 5.3); cmd reads its
  command line as written. Put the cmd commands in a `.cmd` file and run that, or leave the quotes out where the path
  has no space (`cmd //c 'type C:\x\y.json'`).

## By design

- **One temp folder: `$TMP`, `$TEMP` and `$TMPDIR` in a C#Bash started from C#Bash are its parent's folder, not what
  was given for it.** `TMP=/tmp/x bash -c 'echo "$TMP"'` prints the folder (`F:/TEMP`), where GNU bash prints
  `/tmp/x`, so every shell of a family shares one /tmp. To hand a temp folder to a program, give it to that program
  (`TMP=/tmp/x prog` reaches it), or to mktemp with `-p`.
- **`> /dev/stdin` when stdin is a pipe or a here-document is "Permission denied"**, status 1. GNU bash writes into
  the pipe, reopening it through /proc; Windows cannot open a pipe's writing end from its reading end. A Claude Code
  command whose text holds a `<` has such a stdin. Write where the data is going by its own name. A file or /dev/null
  as stdin is written as in GNU.
- **There is no `/proc`.** `/proc/self/fd/0` and the rest of `/proc` are Linux's; `/dev/stdin`, `/dev/stdout`,
  `/dev/stderr` and `/dev/fd/N` are here, and test as what the descriptor is. Use those.
- **`<( )` is a file, not a pipe.** Its command runs to its end, its output goes to a temp file, and the word becomes
  that file's path (removed when the outer command ends). So `[ -p <(cmd) ]` is false, where GNU says true, and a
  command that never ends (`<(tail -f log)`) never hands over its path. Read such a command through a pipe
  (`tail -f log | ...`).
- **Not listed does not mean not available.** A command C#Bash does not provide runs through the fallback chain (by
  default PATH then WSL, with any folders unix-tools names in its order; USING.md "The fallback chain", `help fallback`), but `compgen`, `type`, `command -v`
  and `help` see only files: a tool the chain finds in WSL is never listed. Run the command. For example, WSL's
  poppler reads PDFs (even when Claude Code's Read says "pdftoppm is not installed"):
  `pdfinfo f.pdf` (pages, title); `pdftotext -f 20 -l 25 f.pdf -` (those pages' text; `-layout` keeps columns);
  `pdftoppm -png -r 80 -f 20 -l 20 f.pdf "$(mktemp -d)/p"` (page images, which Read opens). Each WSL start costs about
  0.15 s: take a range per call.
- **A tool the chain runs in WSL is the Linux one.** A `git` request the installed `git.exe` (Wombat) does not serve yet goes on along the chain; with no Windows `git` there, it reaches WSL's Linux git, which
  reads an `E:/repo.git` remote as an ssh host, and treats line ends and file modes as Linux does. Put the folder
  of the Windows tool in the chain before WSL: for example `unix-tools = C:/Tools/PortableGit/cmd;PATH;WSL` in
  `%USERPROFILE%/.bash/etc/csharpBash.config` (Windows' PATH stays as it is). A program of that name in
  `%USERPROFILE%/.bash/bin` still comes first, wherever the folder is named.

- **`$( )` drops a trailing CRLF** as it drops a trailing newline, where bash keeps the CR: `v=$(windows-program)` gets
  a clean value, and text captured from a CRLF file loses its last CR (an inner CRLF stays). Better: write files with
  LF line ends. For the exact bytes of CRLF text, `x=$(cmd; echo .); x=${x%.}` (the same in bash).
- **There is no `cygpath`, and none is needed**: C#Bash's paths are already Windows-rooted with forward slashes (`$PWD`
  is `C:/x`), which every .NET tool reads. Use a path as it is; for an absolute one write `"$PWD/x"`, or `realpath -m x`;
  a Windows program handed `/c/x` gets `C:/x`.
- **A Windows program gets MSYS-form paths converted, as under PortableGit's bash.** An argument `/c/x` reaches it as `C:/x`; a
  colon list `/c/x:/f/y` as `C:/x;F:/y` (any other argument with a colon as typed); the same after the first `=` and
  after `-` and one letter (`--file=/c/x` → `--file=C:/x`, `-I/c/x` → `-IC:/x`); and in its environment (`X=/c/x prog`,
  or an exported `X`). A word holding `;` is passed whole, a bare `/c` or `/` as typed. The trap: a switch shaped like a
  drive path is converted too (`xcopy a b /e/i/h` gets `E:/i/h`). Turn it off with `MSYS_NO_PATHCONV=1` (all of it),
  `MSYS2_ARG_CONV_EXCL` (`*`, or `;`-separated prefixes: the arguments) or `MSYS2_ENV_CONV_EXCL` (prefixes of
  `NAME=value`: the environment); pass a switch to `cmd` as `cmd //c …`, or `cmd /c` when conversion is off. A nested
  `bash` (this shell) and an MSYS program get the environment as typed. (USING.md, "Write paths with forward slashes".)
- **A bare Windows device name is the device**: `> nul`, `2>nul` and `cat nul` reach NUL, not a file named nul. Write
  `./nul` for that file. (PortableGit's bash makes a file named nul there, one Windows' own tools cannot delete.)
- **There is no terminal**: `/dev/tty` reports "No such device or address", so a program that would prompt there fails
  instead of hanging. Give it its input on stdin, or its non-interactive flags.
- **Lookups are remembered across shells**, so a program installed later in a folder earlier on PATH stays hidden
  behind the remembered one. Run `hash -r` after installing a tool.

## Error wording

Where C#Bash's error text differs from bash's, the status does not, nor whether the line goes on; a few of these also
differ in what is printed or run besides the message (said in each). Test the status, not the message.

- **An unclosed `${` inside an expansion's word.** In `"[${x:+a'${y'}]"` the apostrophes hide the inner `}`, so the
  word holds a `${` that never closes. Both shells abandon the line; bash says ``bad substitution: no closing `}' in
  a'${y'``, C#Bash `${y'}: bad substitution`, and when the hiding quote closes on a later line C#Bash prints the text
  between the quotes instead. Close every quote you open inside `${…}`.
- **A tool's words for a closed or wrong-way descriptor** (`0<&-`, `0> f`, `>&-`, `1< f`). Reads and writes fail with
  GNU's status, but head, tail, wc, uniq and base64 word it their own way (`head: cannot open '-' for reading: Bad file
  descriptor`, where GNU's says `head: error reading 'standard input': Bad file descriptor`); the second line some GNU
  tools add (`cat: closing standard input: ...`) and what od and wc print first (`0000000`, `0`) are not printed; GNU's
  xargs runs its command once first; and cat with a closed stdout says `write error` where GNU's says `standard output`.
- **`[ -t /dev/stdin ]`** (`-t` takes a descriptor number): both return 1; bash says nothing, C#Bash prints `test: -t:
  unary operator expected` (`[ -t x ]` is silent in both). Write `[ -t 0 ]`.

## Not available

Each says so when asked for, with a link to ask for it; every request is counted (`unserviced.jsonl`).

- **extglob** (`shopt -s extglob` is refused, so `@( )`, `*( )`, `+( )`, `?( )`, `!( )` patterns are not available in
  `case`, `${x#…}` or file names): `[[ $x == @(a|b) ]]` takes them, as bash does with the option off; or use `case`
  with `|`.
- **`>( )`**: write to a temporary file, or pipe through `tee`.
- **jq's `def` and `foreach`** (the in-process jq is a jq 1.6 subset): for `def`, write the helper out where it is used;
  for either, run a full jq, which the fallback chain reaches in WSL.
- **Restricted mode, POSIX mode, programmable completion (`complete`)**: none has a replacement here; ask if you need
  one.
- **`select` and `coproc`**: they parse (a script holding one runs up to it, and a branch that never reaches it costs
  nothing), and running one is refused, status 2. For a menu, print the choices and `read` the answer in a loop;
  `coproc` (a two-way pipe to a child) has no replacement here.
