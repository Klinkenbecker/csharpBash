# 2026-10-19 (red team F13): a path that runs through a file is "Not a directory" (bash's ENOTDIR) for a command, a
# builtin's open and a redirect. Windows drops a trailing `/.`: `hostname.exe/.` ran, `./s.sh/.` read s.sh, and `cat
# file/x` said "No such file or directory". Expected: GNU bash 5.1 via WSL (error prefixes stripped), byte-identical.
d=$(mktemp -d) && cd "$d" || exit 1
printf '#!/bin/sh\necho ran\n' > s.sh; chmod +x s.sh; echo data > file
for p in ./s.sh/. ./s.sh/.. ./s.sh/x; do "$p" 2>&1 | sed 's/^.*line [0-9]*: //'; echo "$p: rc=${PIPESTATUS[0]}"; done
cat file/x 2>&1; echo "cat file/x: rc=$?"
cat file/. 2>&1; echo "cat file/.: rc=$?"
ls file/x 2>&1; echo "ls: rc=$?"
ls file/. 2>&1; echo "ls file/.: rc=$?"
{ echo hi > file/y; } 2>&1 | sed 's/^.*line [0-9]*: //'
{ echo hi >> file/y; } 2>&1 | sed 's/^.*line [0-9]*: //'
{ cat < file/z; } 2>&1 | sed 's/^.*line [0-9]*: //'
cd / && rm -rf "$d"
