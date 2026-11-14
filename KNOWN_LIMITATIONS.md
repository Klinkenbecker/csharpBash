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
- **`df -i`**: Windows keeps no inode counts. It goes to the fallback chain, as any option C#Bash lacks does; in WSL,
  `df -i /` answers for WSL's own Linux disk, and a drive (`F:/`, there `/mnt/f`) for what WSL reports of it, not NTFS.
- **MSYS2's own programs (runtime 3.1.7) keep the quotes of a quoted word holding a non-ASCII character**: `'é{x}'`
  arrives as `"é{x}"`, from C#Bash or from cmd alike. It is MSYS2's parsing. Git for Windows' MSYS programs, and C#Bash's
  own tools, receive the word correctly.

## By design

- **Not listed does not mean not available.** A command C#Bash does not provide runs through the fallback chain (a
  tools folder, PATH, then WSL; USING.md "The fallback chain", `help fallback`), but `compgen`, `type`, `command -v`
  and `help` see only files: a tool the chain finds in WSL is never listed. Run the command. For example, WSL's
  poppler reads PDFs (even when Claude Code's Read says "pdftoppm is not installed"):
  `pdfinfo f.pdf` (pages, title); `pdftotext -f 20 -l 25 f.pdf -` (those pages' text; `-layout` keeps columns);
  `pdftoppm -png -r 80 -f 20 -l 20 f.pdf "$(mktemp -d)/p"` (page images, which Read opens). Each WSL start costs about
  0.15 s: take a range per call.

- **`$( )` drops a trailing CRLF** as it drops a trailing newline, where bash keeps the CR: `v=$(windows-program)` gets
  a clean value, and text captured from a CRLF file loses its last CR (an inner CRLF stays). Better: write files with
  LF line ends. For the exact bytes of CRLF text, `x=$(cmd; echo .); x=${x%.}` (the same in bash).
- **There is no `cygpath`, and none is needed**: C#Bash's paths are already Windows-rooted with forward slashes (`$PWD`
  is `C:/x`), which every .NET tool reads. Use a path as it is; for an absolute one write `"$PWD/x"`, or `realpath -m x`;
  a Windows program handed `/c/x` gets `C:/x`.
- **`$TEMP` and `$TMP` are as Windows sets them, with backslashes** (`F:\TEMP`); Claude Code exports them so before
  every command. A backslash is eaten at the next hop (JSON, a regex, sed), so for a temporary path use `mktemp`:
  `mktemp -d`, `mktemp -u`, `mktemp --tmpdir x.XXXX` all give `F:/TEMP/…`. For the folder itself, `${TEMP//\\//}`.
- **A bare Windows device name is the device**: `> nul`, `2>nul` and `cat nul` reach NUL, not a file named nul. Write
  `./nul` for that file. (Git Bash makes a file named nul there, one Windows' own tools cannot delete.)
- **There is no terminal**: `/dev/tty` reports "No such device or address", so a program that would prompt there fails
  instead of hanging. Give it its input on stdin, or its non-interactive flags.
- **Lookups are remembered across shells**, so a program installed later in a folder earlier on PATH stays hidden
  behind the remembered one. Run `hash -r` after installing a tool.

## Not available

Each says so when asked for, with a link to ask for it; every request is counted (`unserviced.jsonl`).

- **extglob** (`shopt -s extglob` is refused, so `@( )`, `*( )`, `+( )`, `?( )`, `!( )` patterns are not available):
  use `case` with `|`, or `[[ $x == a || $x == b ]]`.
- **`>( )`**: write to a temporary file, or pipe through `tee`.
- **jq's `def`** (the in-process jq is a jq 1.6 subset): write the helper out where it is used; or run a full jq, which
  the fallback chain reaches in WSL.
- **Restricted mode, POSIX mode, programmable completion (`complete`)**: none has a replacement here; ask if you need
  one.
