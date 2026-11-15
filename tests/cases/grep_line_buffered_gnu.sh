# 2026-11-14 (git-9d / Wombat, bugs list): `grep --line-buffered` into a pipe held every line until EOF (a
# `tail -f log | grep --line-buffered x` watch delivered nothing). The producer writes its next line only after the
# consumer has seen the last one, or "late" after 10 s: a grep that holds lines gives "late". Expected = GNU via WSL.
d=$(mktemp -d) && cd "$d" || exit 1
wait_for() { local i=0; while [ ! -f "$1" ] && [ $i -lt 100 ]; do sleep 0.1; i=$((i + 1)); done; [ -f "$1" ] && echo "$2" || echo "$2-late"; }
(echo a; wait_for got-a b) | grep --line-buffered . | while read -r l; do echo "match $l"; touch "got-$l"; done
(echo m; echo c; wait_for got-c m2) | grep --line-buffered -A1 m | while read -r l; do echo "context $l"; touch "got-$l"; done
cd / && rm -rf "$d"
