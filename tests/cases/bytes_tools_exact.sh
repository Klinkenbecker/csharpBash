# 2026-10-27: cmp, od, hexdump and base64 on the shared byte layer, as GNU, where a block's edge (64 KiB) or a skip
# matters. rev 397 got three wrong, all one overflow (an offset plus long.MaxValue): `cmp -i N` without -n compared
# nothing (equal, 0), and so did a skip past both ends; `hexdump -C -s N` without -n printed no line. cmp's byte
# numbers count from after -i, as GNU's. base64 -d writes what decodes before invalid input (rev 397 wrote nothing),
# a last group cut short included, and takes concatenated encodings, as GNU's. Not compared here (C#Bash differs,
# DECISIONS 2026-10-27): cmp's EOF message and -l layout, od's addresses after -j, base64 -w 0's last newline and -d dropping
# spaces and CR (GNU drops only newlines). Expected: GNU via WSL.
cd "${TMPDIR:-${TEMP:-/tmp}}" || exit 1
d="bytes-exact-$$"; mkdir -p "$d" && cd "$d" || exit 1
seq 1 40000 > a                                        # 228894 bytes: several blocks
{ head -c 65535 a; printf 'X'; tail -c +65537 a; } > b  # differs at byte 65536, the last of block 1
{ head -c 65536 a; printf 'X'; tail -c +65538 a; } > c  # ... at 65537, the first of block 2
printf 'line1\nline2\nlineX\n' > t1; printf 'line1\nline2\nlineY\n' > t2; printf 'abc' > t3

echo "== cmp"
for p in "a b" "a c" "-n 65536 a b" "-n 65535 a b" "-i 2 t1 t2" "-i 100 t3 t1" "-i 65000 a c" "-s a a"; do
	printf '%s: ' "$p"; cmp $p 2>&1 | tr '\n' ' '; echo "rc=${PIPESTATUS[0]}"
done
echo "== od"
od -A n -t x1 -j 65530 -N 12 a
od -A n -c -j 131070 -N 6 a
od -A n -t x2 -w4 -N 10 a
echo "== hexdump"
hexdump -C -s 65530 -n 20 a
hexdump -C -s 228880 a
echo "== base64"
base64 a | md5sum; base64 -w 10 a | head -n 3; base64 a | base64 -d | cmp - a && echo "round trip ok"
for in in 'aGVsbG8' 'aGVsbA' 'YQ' 'Y' 'aGVs!G8=' 'YQ==YQ==' 'aGVsbG8==' $'aGVs\nbG8=\n'; do
	printf '[%s]: ' "${in//$'\n'/|}"; printf '%s' "$in" | base64 -d 2>&1 | od -A n -c | tr -s ' ' | tr -d '\n'; echo " rc=${PIPESTATUS[1]}"
done

cd .. && rm -rf "$d"
