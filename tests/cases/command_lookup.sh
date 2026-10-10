# 2026-10-08: a program found on PATH prints with its on-disk name (cmd.exe, not PATHEXT's cmd.EXE),
# and `hash` speaks bash: the `hits command` table with real counts, -t, -l, -d, -p. It printed bare
# paths and ignored -t / -l / -d. Windows paths, so authored; directories are stripped (b).
b() { sed 's|C:/[^ ]*/||'; }
echo "1 [$(command -v cmd | b)]"
echo "2 [$(type -p cmd | b)]"
echo "3 [$(which cmd | b)]"
echo "4 [$(type cmd | b)]"
hash -r; hash
hash cmd; hash | b
cmd /c rem; cmd /c rem; hash | b
hash -t cmd | b
hash -t nosuch 2>/dev/null; echo "-t not hashed: rc=$?"
hash -t cmd cmd | b
hash -l | b
hash -d cmd; hash
hash -d cmd 2>/dev/null; echo "-d not hashed: rc=$?"
hash -p /x/y.exe myprog; hash -t myprog
hash echo; echo "builtin: rc=$?"; hash -t echo 2>/dev/null; echo "builtin not hashed: rc=$?"
