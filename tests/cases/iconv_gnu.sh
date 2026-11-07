# 2026-11-05 (installer-a4's need of 2026-11-01: `iconv -f UTF-16 -t UTF-8` of a Windows INF, "command not found"; the
# open list's item 3c): iconv in-process for UTF-8, UTF-16, UTF-32 (LE, BE, and the plain names: a BOM read, and one
# written, little-endian, as glibc), ISO-8859-1, Windows-1252 and ASCII, with glibc's messages and byte positions,
# -c, -o, several files (each its own conversion). Expected: GNU (glibc) iconv via WSL.
w=$(mktemp -d) && cd "$w" || exit 1
r() { echo "--- $*"; "$@" 2>err | od -An -tx1 | head -3; echo "[rc=${PIPESTATUS[0]}]"; sed 's/^/ERR: /' err; }
x() { printf 'a\303\251\342\202\254'; }   # a e-acute euro
x | r iconv -f UTF-8 -t UTF-16
x | r iconv -f UTF-8 -t UTF-16LE
x | r iconv -f UTF-8 -t UTF-16BE
x | r iconv -f UTF-8 -t UTF-32
x | r iconv -f UTF-8 -t UTF-32BE
printf '\377\376a\000\351\000' | r iconv -f UTF-16 -t UTF-8
printf '\376\377\000a\000\351' | r iconv -f UTF-16 -t UTF-8
printf '\000a\000\351' | r iconv -f UTF-16 -t UTF-8
printf 'a\000\351\000' | r iconv -f UTF-16LE -t UTF-8
printf '\377\376a\000' | r iconv -f UTF-16LE -t UTF-8
printf '\377\376\000\000a\000\000\000' | r iconv -f UTF-32 -t UTF-8
x | r iconv -f UTF-8 -t ISO-8859-1
x | r iconv -c -f UTF-8 -t ISO-8859-1
x | r iconv -f UTF-8 -t CP1252
printf 'a\200\201b' | r iconv -f CP1252 -t UTF-8
printf 'a\351' | r iconv -f LATIN1 -t UTF-8
printf 'ab\351cd' | r iconv -f UTF-8 -t UTF-16LE
printf 'ab\351cd' | r iconv -c -f UTF-8 -t UTF-16LE
printf 'ab\303' | r iconv -f UTF-8 -t UTF-16LE
printf 'a\340\200\257b' | r iconv -f UTF-8 -t UTF-16LE        # an overlong form
printf 'a\355\240\200b' | r iconv -f UTF-8 -t UTF-16LE        # a surrogate, encoded
printf 'a\364\220\200\200b' | r iconv -f UTF-8 -t UTF-16LE    # past U+10FFFF
x | r iconv -f UTF-8 -t ASCII
x | r iconv -t UTF-16LE
printf 'a\303\251' | r iconv --from-code=utf8 --to-code=latin1
printf 'a\360\237\230\200' | r iconv -f UTF-8 -t UTF-16LE
printf '\075\330' | r iconv -f UTF-16LE -t UTF-8
printf 'a\0\0\0\0\0\21\0' | r iconv -f UTF-32LE -t UTF-8
printf '\357\273\277a' | r iconv -f UTF-8 -t UTF-16LE
printf '' | r iconv -f UTF-8 -t UTF-16
printf 'xy' > f1; printf 'z\351w' > f2
r iconv -f UTF-8 -t UTF-16LE f1 f2
r iconv -f UTF-8 -t UTF-16 f1 f1
r iconv -f UTF-8 -t UTF-16LE -o out f1; od -An -tx1 out
r iconv -f UTF-8 -t UTF-8 nosuch
echo "a round trip, 100000 characters: $(for i in $(seq 1 20000); do printf 'h\303\251\342\202\254\360\237\230\200\n'; done | iconv -f UTF-8 -t UTF-16 | iconv -f UTF-16 -t UTF-8 | cksum)"
echo "a block edge: $(head -c 65535 /dev/zero | tr '\0' a | { cat; printf '\303\251'; } | iconv -f UTF-8 -t UTF-16LE | tail -c 4 | od -An -tx1)"
{ head -c 70000 /dev/zero | tr '\0' a; printf '\351x'; } | iconv -f UTF-8 -t UTF-16LE > /dev/null 2>err; echo "past a block: rc=$?"; cat err
cd / && rm -rf "$w"
