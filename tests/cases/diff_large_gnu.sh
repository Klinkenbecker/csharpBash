# 2026-11-03 (c-parser-ac's report): diff on large files, as GNU: 50,000 lines differing in one line, and in a few
# scattered ones, in the normal and the unified format, and in a pipe. It allocated an n x m table (10 GB at 50,000
# lines: OutOfMemoryException, exit 9; silent in a pipe) and took quadratic time. Expected: GNU diffutils via WSL.
export LC_ALL=C
cd "${TMPDIR:-${TEMP:-/tmp}}" || exit 1
d="diff-large-$$"; mkdir -p "$d" && cd "$d" || exit 1
seq 1 50000 > a.txt
sed 's/^25000$/x/' a.txt > b.txt
echo "--- one line"; diff a.txt b.txt; echo "rc=$?"
echo "--- unified"; diff -u --label a --label b a.txt b.txt; echo "rc=$?"
echo "--- in a pipe"; diff a.txt b.txt | head -3
sed -e 's/^7$/seven/' -e '/^12000$/d' -e 's/^30000$/&\nnew/' -e 's/^49999$/y/' a.txt > c.txt
echo "--- scattered"; diff a.txt c.txt; echo "rc=$?"
echo "--- scattered, unified"; diff -u --label a --label c a.txt c.txt; echo "rc=$?"
echo "--- identical"; diff a.txt a.txt; echo "rc=$?"
seq 1 3000 | awk '{ print "w" (($1 * 7919) % 23) }' > r1
awk 'NR % 13 == 0 { next } NR % 11 == 0 { print "x" NR % 7 } { print } NR % 17 == 0 { print "n" }' r1 > r2
echo "--- repetitive lines (many equally short edits: the count, as GNU's, which is minimal here)"; echo "$(diff r1 r2 | grep -c '^[<>]') changed lines"
cd .. && rm -rf "$d"
