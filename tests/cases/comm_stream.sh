# 2026-10-27: comm merges its two inputs a line at a time (each was read into a list first: failed over 2 GiB). As
# GNU, and each accepted and ignored before: --total (the counts of all three columns), -z NUL records, the order check
# (--check-order stops at the first line out of order, status 1; by default a file is checked once an unpairable line
# has been seen, warned once, status 1 at the end; --nocheck-order), an empty --output-delimiter is one NUL; the file
# name in an open error; GNU's operand messages. Expected: GNU coreutils 8.32 via WSL (LC_ALL=C).
cd "${TMPDIR:-${TEMP:-/tmp}}" || exit 1
d="comm-stream-$$"; mkdir -p "$d" && cd "$d" || exit 1
export LC_ALL=C
printf 'a\nb\nd\ne\n' > x; printf 'b\nc\nd\nf\n' > y
for o in "" -12 -3 "--output-delimiter=|" --total "--total -123"; do echo "[$o]"; comm $o x y; done
comm --output-delimiter= x y | od -An -c | tr -s ' '
printf 'a\0b\0' > z1; printf 'b\0c' > z2; comm -z --total z1 z2 | od -An -c | tr -s ' '
printf 'a\nc\nb\n' > d1; printf 'b\nc\nd\n' > d2; comm d1 d2 2>&1; echo "rc=$?"
printf 'a\nz\nb\n' > e1; printf 'c\ny\nd\n' > e2; comm e1 e2 2>&1; echo "rc=$?"
comm --nocheck-order d1 d2 2>&1; echo "rc=$?"
printf 'b\na\n' > p1; cp p1 p2; comm p1 p2 2>&1; echo "rc=$?"; comm --check-order p1 p2 2>&1; echo "rc=$?"
cat x | comm - y | tr '\n' '~'; echo; printf 'a\nb' > n1; printf 'b' > n2; comm n1 n2 | od -An -c | tr -s ' '
seq 1 3 30000 | sort > s3; seq 1 2 30000 | sort > s2; comm --total s3 s2 | tail -n 1; comm -12 s3 s2 | md5sum | cut -c1-12
{ head -c 9000 /dev/zero | tr '\0' a; echo; echo b; } > l1; { head -c 9000 /dev/zero | tr '\0' a; echo; echo c; } > l2; comm l1 l2 | cut -c1-6
comm x nope 2>&1; echo "rc=$?"; comm x 2>&1; echo "rc=$?"; comm 2>&1; echo "rc=$?"; comm x y z 2>&1; echo "rc=$?"
cd .. && rm -rf "$d"
