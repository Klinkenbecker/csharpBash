# 2026-10-18: only what Windows' launcher starts is a program: .com .exe .bat .cmd (and .sh, #!). Windows' PATHEXT also
# lists .js .vbs .wsf .msc …, which run only through a file association: a `zz.js` on PATH stopped the search for `zz`
# ("Permission denied") before a later `zz.exe`, and `[ -x zz.js ]` was true. Now, as Git Bash (and bash, which passes
# over what it cannot execute), the later program runs, `-x` is false, and a `.js` with a `#!` line is a script.
# Authored (Windows-specific). The architect: "Yes make the change".
d=$(mktemp -d) && cd "$d" || exit 1
msg() { sed "s|$d|D|g; s|^.*line [0-9]*: ||"; }
mkdir p7 p8
printf 'WScript.Echo("js ran");\n' > p7/zz.js; cp C:/Windows/System32/whoami.exe p8/zz.exe
printf 'WScript.Echo("js ran");\n' > p7/zzonly.js
printf '#!/bin/bash\necho "a .js with a #! line ran"\n' > p7/zzsh.js
PATH="$d/p7;$d/p8;$PATH"
echo "== zz.js before zz.exe: the program runs"
zz > /dev/null; echo "rc=$?"; type -a zz | msg
echo "== [ -x ] of a .js"
[ -x p7/zz.js ] && echo "-x zz.js: true" || echo "-x zz.js: false"
echo "== a name only a .js answers"
zzonly 2>&1 | msg; echo "rc=${PIPESTATUS[0]}"
zzonly.js 2>&1 | msg; echo "rc=${PIPESTATUS[0]}"
echo "== a .js with a #! line, named in full"
zzsh.js
cd / && rm -rf "$d"
