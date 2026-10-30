# 2026-10-30 (the architect: "Yes", on Claude's position): `sh` is a copy of the shell named sh.exe beside Bash.exe, as
# Git for Windows and MSYS2 ship theirs (each one's sh.exe is byte-identical to its bash.exe); tools/deploy.sh puts it
# in dist/. No code knows the name: this proves a lone copy found on PATH serves what scripts and programs ask of `sh`
# (-c, a script, stdin, xargs and find -exec, which run through the shell's own lookup, an exit status), and that its
# messages name it by its path, as $0 is argv[0] (DECISIONS 2026-10-11). It runs bash's default mode, as a #!/bin/sh
# script does (POSIX mode is not available). Authored. AOT only: the managed build's apphost cannot run renamed.
d=${TEMP:-/tmp}/csb-sh-$$
mkdir -p "$d/bin" "$d/w" || exit 1
cp "$BASH" "$d/bin/sh.exe" || exit 1
PATH="$d/bin;$PATH"
cd "$d/w" || exit 1
echo "--- found";      t=$(type -P sh); [ "${t##*/}" = sh.exe ] && echo "sh.exe, first on PATH" || echo "found: $t"
echo "--- -c";         sh -c 'echo "$0 ran with $# args: $*"' name a b
echo "--- script";     printf 'echo "script $1"\n' > s; sh s one
echo "--- stdin";      echo 'echo "from stdin"' | sh
echo "--- xargs";      printf '%s\n' x y | xargs sh -c 'echo "xargs: $*"' sh
echo "--- find -exec"; : > f; find . -name f -exec sh -c 'echo "find: $1"' sh {} \;
echo "--- status";     sh -c 'exit 3'; echo "rc=$?"
echo "--- message";    sh -c nosuch 2>&1 | sed 's#.*/##'
cd / && rm -rf "$d"
