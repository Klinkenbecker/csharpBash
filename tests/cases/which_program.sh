# 2026-10-19 (red team F8): which reports only what can run: a .js with no #! was reported (and `[ -x ]` said no,
# running it gave 126, `type` did not list it). The files meant as programs are also chmod +x, which GNU needs and
# C#Bash takes as given (a program by extension or #!), so both judge them alike. Expected: GNU which (debianutils)
# via WSL, byte-identical.
d=$(mktemp -d) && cd "$d" || exit 1
mkdir p q
printf 'console.log(1)\n' > p/a.js
printf '#!/bin/sh\necho b\n' > p/b.sh; chmod +x p/b.sh
printf '#!/bin/sh\necho c\n' > p/c; chmod +x p/c
printf 'text\n' > p/d.txt
printf '#!/usr/bin/env node\nconsole.log(1)\n' > p/e.js; chmod +x p/e.js
printf '#!/bin/sh\necho c2\n' > q/c; chmod +x q/c
case "$PATH" in *\;*) sep=';' ;; *) sep=':' ;; esac
W=$(command -v which)   # GNU's /usr/bin/which, found before PATH narrows; C#Bash's builtin
for n in a.js b.sh c d.txt e.js; do out=$(PATH="$d/p$sep$d/q" "$W" "$n"); echo "which $n: rc=$? [${out//$d/D}]"; done
out=$(PATH="$d/p$sep$d/q" "$W" -a c | tr '\n' ' '); echo "which -a c: [${out//$d/D}]"
out=$(PATH="$d/p$sep$d/q" "$W" a.js c); echo "which a.js c: rc=$? [${out//$d/D}]"
cd / && rm -rf "$d"
