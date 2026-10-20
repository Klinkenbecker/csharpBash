# 2026-10-19 (red team, older): two shells appending to one file keep every line, as O_APPEND does. C#Bash's own `>>`
# was an ordinary write stream at the end, so each wrote over the other: 3000 of 6000 lines were left. Expected: GNU
# bash 5.1 (WSL, on its own file system), byte-identical.
d=$(mktemp -d) && cd "$d" || exit 1
( for i in $(seq 3000); do echo A; done >> f ) &
( for i in $(seq 3000); do echo B; done >> f ) &
wait
echo "loop redirect: $(wc -l < f) lines, A $(grep -c A f), B $(grep -c B f)"
( for i in $(seq 1500); do echo C >> g; done ) &
( for i in $(seq 1500); do echo D >> g; done ) &
wait
echo "per command: $(wc -l < g) lines, C $(grep -c C g), D $(grep -c D g)"
exec 3>> h
( for i in $(seq 2000); do echo E >&3; done ) &
( for i in $(seq 2000); do echo F >> h; done ) &
wait
exec 3>&-
echo "fd 3 and >>: $(wc -l < h) lines, E $(grep -c E h), F $(grep -c F h)"
cd / && rm -rf "$d"
