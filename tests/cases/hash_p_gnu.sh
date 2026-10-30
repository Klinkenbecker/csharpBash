# 2026-10-30 (esdk-a2's report; a known LOW defect, "hash -p /abs name not obeyed"): an explicit `hash -p PATH NAME`
# runs PATH for NAME, as bash runs a hashed program, also when C#Bash has NAME in-process (diff, sort: programs in GNU).
# It was recorded (`hash -t` showed it) but the in-process tool ran. bash's own builtins (echo, printf) ignore it, as in
# bash; a function still wins; `hash -d` and `hash -r` give the name back. The stand-in is a #! script that names
# itself. Expected: GNU bash 5.1 via WSL.
cd "${TMPDIR:-${TEMP:-/tmp}}" || exit 1
d="hash-p-$$"; mkdir -p "$d" && cd "$d" || exit 1
p() { printf '%s\n' "--- $1"; }
printf '#!/bin/bash\necho "stand-in ${0##*/}: $*"\n' > standin; chmod +x standin
printf 'a\n' > l; printf 'b\n' > r
p "hash -p a program for diff"; hash -p "$PWD/standin" diff; diff l r; echo "rc=$?"
p "hash -t"; hash -t diff | sed "s|$PWD|PWD|"
p "a function still wins"; diff() { echo "function diff"; }; diff l r; unset -f diff
p "the hash again"; diff l r
p "hash -d: the tool again"; hash -d diff; diff l r; echo "rc=$?"
p "bash's own builtins ignore it"; hash -p "$PWD/standin" echo; echo "builtin echo"; hash -p "$PWD/standin" printf; printf '%s\n' "builtin printf"
p "hash -r: the tool again"; hash -p "$PWD/standin" sort; printf 'b\na\n' | sort; hash -r; printf 'b\na\n' | sort
cd .. && rm -rf "$d"
