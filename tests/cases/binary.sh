# Binary data through stdin (2026-09-11, installer-79): every byte >= 0x80 arriving through a pipe
# or an input redirect used to be decoded as UTF-8 and re-encoded, so 0xFF became EF BF BD.
# Byte tools now read the pipe/file behind stdin directly. Fixtures come from `base64 -d`
# (bytes); `printf '\377'` is NOT used because it emits the UTF-8 encoding of U+00FF (a
# separate, open defect). Expected output generated under Git Bash and identical.
d="${TMPDIR:-${TEMP:-/tmp}}/binary-$$"; mkdir -p "$d"; cd "$d" || exit 1
printf '%s' '//6AAEHBCg==' | base64 -d > b.bin            # ff fe 80 00 41 c1 0a  (7 bytes)
i=0; while [ $i -lt 100 ]; do printf '%s' '/4DB'; i=$((i + 1)); done | base64 -d > big.bin   # ff 80 c1 x100

echo "--- fixture sizes (file arguments)"
wc -c b.bin | cut -d' ' -f1; wc -c big.bin | cut -d' ' -f1

echo "--- sizes: input redirect, pipe, chained pipes"
wc -c < b.bin; cat b.bin | wc -c
wc -c < big.bin; cat big.bin | wc -c; cat < big.bin | wc -c; cat big.bin | cat | cat | wc -c

echo "--- bytes survive: od through the three routes"
od -An -tx1 b.bin | tr -s ' '
od -An -tx1 < b.bin | tr -s ' '
cat b.bin | od -An -tx1 | tr -s ' '

echo "--- head -c / tail -c on stdin"
head -c 3 < b.bin | od -An -tx1 | tr -s ' '
cat b.bin | tail -c 3 | od -An -tx1 | tr -s ' '
cat big.bin | head -c 5 | od -An -tx1 | tr -s ' '

echo "--- cmp / checksums agree between file and stdin"
cmp -s b.bin - < b.bin && echo cmp-redirect-ok
cat b.bin | cmp -s b.bin - && echo cmp-pipe-ok
head -c 300 big.bin | cmp - big.bin && echo head-pipe-identical
[ "$(md5sum < big.bin | cut -d' ' -f1)" = "$(md5sum big.bin | cut -d' ' -f1)" ] && echo md5-agrees
[ "$(cat big.bin | sha256sum | cut -d' ' -f1)" = "$(sha256sum big.bin | cut -d' ' -f1)" ] && echo sha-agrees
base64 < b.bin

echo "--- tee is byte-faithful"
cat big.bin | tee t1.bin > t2.bin; cmp -s t1.bin big.bin && cmp -s t2.bin big.bin && echo tee-ok
tee t3.bin < b.bin | wc -c; cmp -s t3.bin b.bin && echo tee-redirect-ok

echo "--- text sources still work"
cat <<< "here-string"; wc -c <<< "abc"; printf 'x\ny\n' | cat; echo "é" | wc -c
cd /; rm -rf "$d"
