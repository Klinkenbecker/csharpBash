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
