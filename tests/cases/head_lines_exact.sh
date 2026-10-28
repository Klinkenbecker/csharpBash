# 2026-10-27: head -n N (forwards) keeps a last line's missing newline missing, as GNU (it added one: `printf a | head
# -n 1` was a\n), still through the shared line reader, so a following command reads on from the next line (a file
# stdin as GNU; a pipe exactly too, where GNU's head takes the pipe's whole block: not tested here). CR, raw bytes,
# several files with headers, -n +N, 0, -N, a line longer than the reader's block. Expected: GNU coreutils 8.32 via WSL.
cd "${TMPDIR:-${TEMP:-/tmp}}" || exit 1
d="head-lines-$$"; mkdir -p "$d" && cd "$d" || exit 1
seq 1 5 > f; printf 'a\nb' > nb
c() { od -An -c | tr -s ' '; }
head -n 1 nb | c; head -n 2 nb | c; head -n 5 nb | c; printf a | head -n 1 | c; printf '' | head -n 1 | c
{ head -n 1; cat; } < f | tr '\n' ' '; echo; { head -n 2; head -n 1; } < f | tr '\n' ' '; echo
{ head -n 1; cat; } < nb | c
printf 'x\r\ny\n' | head -n 1 | c; printf '\200\377\nz' | head -n 2 | c
head -n 2 f nb | c; head -n 2 nb f | c; head -n +2 f | c; head -n 0 nb | c; head -2 nb | c
{ head -c 9000 /dev/zero | tr '\0' a; printf '\nlast'; } > long; head -n 2 long | wc -c; head -n 5 long | tail -c 5 | c
cd .. && rm -rf "$d"
