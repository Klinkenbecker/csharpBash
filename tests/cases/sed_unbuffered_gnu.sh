# 2026-11-15 (found by Claude beside git-9d's grep --line-buffered; bugs list): `sed -u` into a pipe held every line
# until its input ended (-u was parsed and ignored). The producer writes its next line only after the consumer has seen
# the last one, or "late" after 10 s: a sed that holds lines gives "late". Expected = GNU sed via WSL.
d=$(mktemp -d) && cd "$d" || exit 1
wait_for() { local i=0; while [ ! -f "$1" ] && [ $i -lt 100 ]; do sleep 0.1; i=$((i + 1)); done; [ -f "$1" ] && echo "$2" || echo "$2-late"; }
(echo a; wait_for got-a b) | sed -u 's/^/s:/' | while read -r l; do echo "$l"; touch "got-${l#s:}"; done
(echo c; wait_for got-c d) | sed -nu 'p' | while read -r l; do echo "p:$l"; touch "got-$l"; done
(echo e; wait_for got-x f) | sed -u 'a\
x' | while read -r l; do echo "a:$l"; touch "got-$l"; done
cd / && rm -rf "$d"
