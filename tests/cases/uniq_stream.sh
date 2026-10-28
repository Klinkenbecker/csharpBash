# 2026-10-27: uniq reads a group at a time (it read every line into a list first: failed over 2 GiB). Its switches
# as coreutils': -d and -D drop lone lines, -u drops a repeated group's first line, -D prints its later lines (-d -u
# printed the repeated groups, GNU nothing; -D went to the PATH uniq); -D -c refused; -z NUL records (was ignored);
# -f fields split at space and tab only (a CR was a blank). Groups across the reader's 8192-character block, a line
# longer than it, no final newline, stdin, an output file. Expected: GNU coreutils 8.32 via WSL.
cd "${TMPDIR:-${TEMP:-/tmp}}" || exit 1
d="uniq-stream-$$"; mkdir -p "$d" && cd "$d" || exit 1
printf 'a\na\nb\nc\nc\nc\nd\n' > u
for o in "" -c -d -u -D "-d -u" "-D -u" "-D -d" "-c -d" "-c -u"; do printf '%-6s %s\n' "$o" "$(uniq $o u | tr '\n' ' ')"; done
LC_ALL=C uniq -D -c u 2>&1; echo "rc=$?"
printf 'a\0a\0b\0b' | uniq -z -c | od -An -c | tr -s ' '; printf 'a\0a\0b\0' | uniq -z -D | od -An -c | tr -s ' '
printf 'a\r\na\r\nA\nb\r\n' | uniq -f 1 -c | od -An -c | tr -s ' '
printf 'k 1 x\nk 2 x\nj\t2 x\n' | uniq -f 1 -c
printf 'A\na\nB\n' | uniq -i -D; printf 'x\nx' | uniq | od -An -c | tr -s ' '; printf 'p\nq\nq' | uniq -c
seq 1 30000 | awk '{ print int($1 / 7) }' > dups; uniq -c dups | md5sum | cut -c1-12; uniq -D dups | md5sum | cut -c1-12
uniq -c < dups | tail -n 2; cat dups | uniq -u | wc -l
{ head -c 9000 /dev/zero | tr '\0' a; echo; head -c 9000 /dev/zero | tr '\0' a; echo; echo b; } | uniq -c | cut -c1-10
uniq u out; tr '\n' ' ' < out; echo
uniq missing 2>&1 | sed 's/missing:.*/missing: .../'
cd .. && rm -rf "$d"
