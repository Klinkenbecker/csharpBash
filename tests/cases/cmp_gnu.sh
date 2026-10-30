# 2026-10-30 (the "match GNU" item, found 2026-10-27): cmp's messages and layouts as GNU's: the EOF message (which
# file, after which byte, in which line), -l's columns (the offset, then the two bytes in octal, no leading zero),
# -b's bytes, an empty file, -s and the exit codes. Expected: GNU diffutils via WSL.
cd "${TMPDIR:-${TEMP:-/tmp}}" || exit 1
d="cmp-gnu-$$"; mkdir -p "$d" && cd "$d" || exit 1
printf 'abcdef\nline2\n' > a; printf 'abXdef\nline2\n' > b; printf 'abc' > short; printf 'abcdef\nli' > part; : > empty
printf 'abcdef\nline2\nmore\n' > longer
r() { "$@" 2>&1; echo "rc=$?"; }

echo "--- differ";            r cmp a b
echo "--- same";              r cmp a a
echo "--- EOF, mid-line";     r cmp a short
echo "--- EOF, other side";   r cmp short a
echo "--- EOF, after a line"; r cmp a part
echo "--- EOF, whole lines";  r cmp a longer
echo "--- empty";             r cmp empty a
echo "--- empty, other side"; r cmp a empty
echo "--- -s differ";         r cmp -s a b
echo "--- -s EOF";            r cmp -s a short
echo "--- -b";                r cmp -b a b
echo "--- -l";                r cmp -l a b
echo "--- -l EOF";            r cmp -l a short
echo "--- -lb";               r cmp -lb a b
head -c 1500 /dev/zero | tr '\0' 'x' > big1; cp big1 big2
printf 'Y' | dd of=big2 bs=1 seek=4 conv=notrunc 2> /dev/null
printf 'Z' | dd of=big2 bs=1 seek=999 conv=notrunc 2> /dev/null
printf '\200' | dd of=big2 bs=1 seek=1499 conv=notrunc 2> /dev/null
echo "--- -l, larger offsets"; r cmp -l big1 big2
echo "--- -lb, larger offsets"; r cmp -lb big1 big2
echo "--- -i 2";              r cmp -i 2 a b
head -c 3000 big1 > huge; printf 'abXdef\nline2\n' >> huge
echo "--- -l, sizes 13 and 3013 (the list, then the message)"; cmp -l a huge 2> /dev/null | sed -n '1,2p;$p'; cmp -l a huge 2>&1 > list.out
echo "--- -l, from a pipe";   printf 'abXdef\nline2\n' | { cmp -l a - 2>&1; echo "rc=$?"; }
cd .. && rm -rf "$d"
