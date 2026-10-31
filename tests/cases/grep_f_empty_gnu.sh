# 2026-10-31 (kes-install-43's report): grep -f with an EMPTY pattern file printed the usage text, rc 2 (zero patterns
# were taken for "no pattern given", so the first operand became the pattern). GNU: "The empty file contains zero
# patterns, and therefore matches nothing"; -v then selects every line. GNU 3.7 reads nothing at all without -v or -L
# (no output, -c and -l included, not even a missing file's error), rc 1; -L lists every file. Where it bit: the
# set-difference idiom `sort -u cand | grep -vxFf seen.txt` while seen.txt is still empty.
# Expected: GNU grep 3.7 via WSL.
d=$(mktemp -d) && cd "$d" || exit 1   # every grep with file operands reads /dev/null: an old build took the first operand as the pattern and read stdin
: > empty.txt; printf 'a\nb\n' > in.txt; printf 'c\n' > in2.txt; printf 'a\n' > one.txt
for opts in "" "-v" "-c" "-vc" "-l" "-L" "-q" "-vn" "-vo" "-vx" "-vw" "-vF" "-vE" "-vP" "-o" "-vH" "-vl" "-vL" "-vxF"; do
	printf '%s: ' "[$opts]"; out=$(grep $opts -f empty.txt in.txt in2.txt 2>&1 < /dev/null); rc=$?; printf '%s' "$out" | tr '\n' '|'; echo " rc=$rc"
done
printf 'a\nb\n' | grep -f empty.txt; echo "stdin: rc=$?"
printf 'a\nb\n' | grep -v -f empty.txt; echo "stdin -v: rc=$?"
printf 'b\na\nc\n' | sort -u | grep -vxFf empty.txt; echo "the set-difference idiom: rc=$?"
grep -f empty.txt -f one.txt in.txt < /dev/null; echo "an empty -f and another: rc=$?"
grep -f empty.txt -e b in.txt < /dev/null; echo "an empty -f and -e: rc=$?"
grep -f empty.txt nosuchfile < /dev/null; echo "a missing file, not read: rc=$?"
grep -f empty.txt a in.txt 2>&1 < /dev/null; echo "an operand is a file, not the pattern: rc=$?"
cd / && rm -rf "$d"
