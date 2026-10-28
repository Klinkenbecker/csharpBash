# 2026-10-27: paste writes a row at a time, a line from each input (each was read into a list first: failed over
# 2 GiB). As GNU: each `-` reads the next line of the one stdin (`seq 4 | paste - -` is 1\t2, 3\t4; the first `-` took
# all of stdin); `-d '\0'` and `-d ''` are an empty delimiter (were a NUL and a tab); -z NUL records (was ignored);
# delimiters cycle per row (per file with -s). Expected: GNU coreutils 8.32 via WSL.
cd "${TMPDIR:-${TEMP:-/tmp}}" || exit 1
d="paste-stream-$$"; mkdir -p "$d" && cd "$d" || exit 1
printf '1\n2\n3\n' > a; printf 'x\ny\n' > b; printf 'p' > c; : > e
c() { od -An -c | tr -s ' '; }
paste a b c; paste -d ',;' a b c; paste -d 'ab' a b c a; paste -s a b; paste -s -d ',;' a; paste -s e | c; paste e | c; paste e a
paste -d '\0' a b | c; paste -d '' a b | c; paste -s -d '' a; paste -d '\n' a b | c; paste -d '\\' a b
seq 4 | paste - -; seq 5 | paste - - - | c; seq 3 | paste a -; seq 3 | paste -s - a; seq 3 | paste -s - -
printf 'a\0b\0' | paste -z - - | c; printf 'a\0b' | paste -s -z - | c; printf 'a\r\nb\n' | paste - - | c
seq 1 20000 > s; paste s s | md5sum | cut -c1-12; paste -s s | md5sum | cut -c1-12; paste -s -d '\n' s | md5sum | cut -c1-12
{ head -c 9000 /dev/zero | tr '\0' a; echo; echo b; } > l; paste l a | cut -c1-4,9000-9010 | c
paste a nope 2>&1; echo "rc=$?"
cd .. && rm -rf "$d"
