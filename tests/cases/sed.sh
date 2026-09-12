echo "== which whoami (nonempty) =="
which whoami >/dev/null && echo "which-ok"
which nosuchcmd_xyz123; echo "which-missing rc=$?"
echo "== s/// first =="
printf 'foo foo foo\n' | sed 's/foo/bar/'
echo "== s///g =="
printf 'foo foo foo\n' | sed 's/foo/bar/g'
echo "== s with & =="
printf 'abc\n' | sed 's/b/[&]/'
echo "== s with group =="
printf 'John Smith\n' | sed 's/\(.*\) \(.*\)/\2 \1/'
echo "== alt delim =="
printf '/usr/bin\n' | sed 's|/usr|/opt|'
echo "== -n p =="
printf 'a\nb\nc\n' | sed -n '2p'
echo "== d delete line2 =="
printf 'a\nb\nc\n' | sed '2d'
echo "== /re/d =="
printf 'keep\ndrop me\nkeep2\n' | sed '/drop/d'
echo "== s///i =="
printf 'HELLO\n' | sed 's/hello/hi/i'
echo "== \$ last line =="
printf 'a\nb\nc\n' | sed '$d'

# 0,/re/ closed the range and then re-opened it on every following line (2026-09-08, web-d6):
# "patch the first occurrence" silently rewrote every occurrence. Compare with 1,/re/ and /a/,/b/.
sf="${TMPDIR:-${TEMP:-/tmp}}/sed-zero-$$.txt"
printf 'k1 x\nk2 x\nother\nk3 x\n' > "$sf"
sed '0,/^k/s/x/FIRST/' "$sf"
sed '0,/^k/{s/x/FIRST/}' "$sf"
sed -n '0,/other/p' "$sf"
sed '1,/^k/{s/x/T/}' "$sf"
sed '/k2/,/k3/{s/x/T/}' "$sf"
sed '0,/^k2/{s/^\(k[0-9]\) /\1 TAMPERED /}' "$sf"
cp "$sf" "$sf.2"; sed -i '0,/^k/{s/x/I/}' "$sf.2"; cat "$sf.2"
rm -f "$sf" "$sf.2"
