head -n2 cases/lines.txt
echo ---
tail -n2 cases/lines.txt
wc cases/lines.txt
wc -l cases/lines.txt
echo abc | rev
tac cases/lines.txt
cat cases/lines.txt | head -n3

# -z (NUL-terminated records) for head and tail: accepted-but-ignored until 2026-09-05
zf="${TMPDIR:-${TEMP:-/tmp}}/textutils-z-$$.bin"
printf 'a\0b\0c\0' > "$zf"
head -z -n 1 "$zf" | tr '\0' '\n'
head -z -n -1 "$zf" | tr '\0' ','; echo
tail -z -n 1 "$zf" | tr '\0' '\n'
tail -z -n +2 "$zf" | tr '\0' ','; echo
printf 'p\0q' > "$zf"
tail -z -n 1 "$zf" | od -c | head -1
rm -f "$zf"
