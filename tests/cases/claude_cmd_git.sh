# 2026-11-24 (the architect: a separate git for Claude Code itself, "claude-git = [none, path, direct-link]", named
# BASH_CLAUDE_GIT; DECISIONS 2026-11-24): the start file claude.cmd gives Claude Code a PATH of ONE folder, the git it
# runs: `path` (the default) the folder of the first git.exe on the caller's PATH, found once; `none` a folder with no
# programs; a git.exe or a folder: that folder. A value naming nothing says so and runs without git. The real
# installer/claude.cmd (CRLF, as the installer writes it) runs in a home of its own with a copy of where.exe as
# claude.exe: `claude.exe git.exe` searches the PATH Claude Code would get, so it prints the git Claude Code would find.
# (A copy of cmd.exe started by cmd fails "The system cannot find the path specified", measured: not usable here.)
# USERPROFILE in Windows' form (backslashes), as Windows sets it. Windows-only; expected authored from the ruling.
src="$PWD/../installer/claude.cmd"
d=$(mktemp -d) && cd "$d" || exit 1
w=$(pwd -W 2>/dev/null || pwd)
mkdir -p home/.local/bin home/.bash/bin home/.bash/etc gitA/cmd gitB empty
sed 's/$/\r/' "$src" > home/.bash/bin/claude.cmd
cp C:/Windows/System32/where.exe home/.local/bin/claude.exe
: > gitA/cmd/git.exe; : > gitB/git.exe                 # git.exe files to be found (only their names matter)
export USERPROFILE="$(printf '%s' "$w" | tr '/' '\\')\\home"
withgit="$w/empty;$w/gitA/cmd;C:/Windows/System32"; nogit="$w/empty;C:/Windows/System32"
show() { out=$("$w/home/.bash/bin/claude.cmd" git.exe 2>/dev/null | tr -d '\r' | tr '\\' '/' | sed "s|$w|W|g"); echo "${out:-(no git)}"; }
echo -n "default, a git on PATH: "; (unset BASH_CLAUDE_GIT; PATH="$withgit" show)
echo -n "path, a git on PATH: "; (BASH_CLAUDE_GIT=path PATH="$withgit" show)
echo -n "path, no git on PATH: "; (BASH_CLAUDE_GIT=path PATH="$nogit" show)
echo -n "none: "; (BASH_CLAUDE_GIT=none PATH="$withgit" show)
echo -n "a folder: "; (BASH_CLAUDE_GIT="$w/gitB" PATH="$withgit" show)
echo -n "a git.exe: "; (BASH_CLAUDE_GIT="$w/gitA/cmd/git.exe" PATH="$nogit" show)
echo -n "a missing one: "; (BASH_CLAUDE_GIT="$w/missing" PATH="$withgit" show)
cd / && rm -rf "$d"
