# 2026-10-27: tee copies exactly. A here-document is a text source: tee read it 4096 characters at a time and a
# character past U+FFFF cut by a chunk's end became two U+FFFD in the files, and on stdout (its Flush wrote the held
# half alone); now a high surrogate ending a chunk waits for the next. A pipe goes through a pooled 64 KiB block, its
# text targets (a capture, /dev/stdout, /dev/stderr) through one carrying decoder. One 'a' then 5000 emoji puts every
# pair at an odd offset, so the 4096 edge always cuts one. Expected: GNU coreutils 8.32 via WSL.
cd "${TMPDIR:-${TEMP:-/tmp}}" || exit 1
d="tee-exact-$$"; mkdir -p "$d" && cd "$d" || exit 1
s=$(awk 'BEGIN { s = "a"; for (k = 0; k < 5000; k++) s = s "\360\237\230\200"; print s }')
printf '%s\n' "$s" > ref
check() { if cmp -s ref "$2"; then echo "$1: same"; else echo "$1: differs at $(cmp ref "$2" 2>&1 | sed 's/.*differ: //')"; fi; }
eval "tee f1 > o1 <<EOF
$s
EOF"
check "here-doc to a file" f1; check "here-doc to stdout" o1
eval "tee /dev/stderr > /dev/null 2> o2 <<EOF
$s
EOF"
check "here-doc to /dev/stderr" o2
y=$(eval "tee /dev/null <<EOF
$s
EOF"); [ "$y" = "$s" ] && echo "here-doc captured: same" || echo "here-doc captured: differs"
x=$(cat ref | tee f3); check "pipe to a file" f3; [ "$x" = "$s" ] && echo "pipe captured: same" || echo "pipe captured: differs"
printf 'small\n' | tee /dev/stdout | cat   # one chunk: twice (a longer input interleaves by chunk, and chunk sizes differ)
seq 1 200000 > big; cat big | tee f5 | md5sum | cut -c1-12; md5sum < f5 | cut -c1-12
printf 'x\200\377\0y\n' | tee f6 | od -An -c | tr -s ' '; od -An -c f6 | tr -s ' '
echo one > f7; echo two | tee -a f7 > /dev/null; cat f7
cd .. && rm -rf "$d"
