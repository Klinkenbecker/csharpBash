# 2026-10-27: a character past U+FFFF (a surrogate pair) written across a writer's chunk boundary stays whole. The
# encoder was stateless: one string over 8192 characters (printf/echo of a capture, to a file or a pipe) and `sed -i`
# turned the emoji at the boundary into two U+FFFD (first bad byte 8534 here). Each line: n 'a's (n = 1..97) and an
# emoji, 5000 lines, so a pair falls on every boundary up to 4096 characters. Expected: GNU via WSL.
cd "${TMPDIR:-${TEMP:-/tmp}}" || exit 1
d="astral-$$"; mkdir -p "$d" && cd "$d" || exit 1
awk 'BEGIN { for (n = 1; n <= 5000; n++) { s = ""; for (k = 0; k < n % 97 + 1; k++) s = s "a"; printf "%s\360\237\222\200\360\237\230\200\n", s } }' > em
check() { if cmp -s em "$2"; then echo "$1: same"; else echo "$1: differs at $(cmp em "$2" 2>&1 | sed 's/.*differ: //')"; fi; }
x=$(cat em)
printf '%s\n' "$x" > o1; check "printf to a file" o1
printf '%s\n' "$x" | cat > o2; check "printf to a pipe" o2
echo "$x" > o3; check "echo to a file" o3
{ printf '%s\n' "$x"; } 2>/dev/null > o4; check "printf in a group" o4
printf '%s\n' "$x" 2> o5 >&2; check "printf to stderr" o5
cp em o6; sed -i 's/^/x/;s/^x//' o6; check "sed -i" o6
sed -n 'w o7' em; check "sed w" o7
y=$(sed '' em); printf '%s\n' "$y" > o8; check "sed capture back out" o8
md5sum < em | cut -c1-12; wc -c < em
cd .. && rm -rf "$d"
