# 2026-10-31 (installer-a4's unmet need; the architect: "yes, go"): `cksum` in-process, as GNU coreutils 8.32: the POSIX
# CRC and the byte count, a line ending in LF (PATH found only a 1997 port here, which wrote CRLF: a captured
# fingerprint kept a trailing CR). Standard input, `-`, files, a 70000-byte file (more than one read), a missing file and
# a directory (exit 1).
# Expected: GNU coreutils 8.32 via WSL, LC_ALL=C.
export LC_ALL=C
d=$(mktemp -d) && cd "$d" || exit 1
printf 'abc' | cksum; printf '' | cksum
printf 'abc' | cksum | od -c | head -2
printf 'abc' > c1; head -c 70000 /dev/zero > c2; seq 1 20000 > c3
cksum c1 c2 c3; echo "rc=$?"
cksum - < c1
cksum missing c1 2>&1; echo "rc=$?"
mkdir dir; cksum dir 2>&1; echo "rc=$?"
f=$(printf 'abc' | cksum | awk '{print $1"-"$2}'); echo "[$f]"
cd / && rm -rf "$d"
