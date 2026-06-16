printf 'apple\nbanana\ncherry\napricot\n' > g.txt
echo "== basic =="
grep 'a' g.txt
echo "== -i =="
grep -i 'APPLE' g.txt
echo "== -v =="
grep -v 'a' g.txt
echo "== -n =="
grep -n 'an' g.txt
echo "== -c =="
grep -c 'a' g.txt
echo "== ^ap regex =="
grep '^ap' g.txt
echo "== -w word =="
printf 'cat\ncategory\nscatter\n' | grep -w cat
echo "== pipe -i =="
printf 'Foo\nbar\n' | grep -i foo
echo "== -F fixed (dot literal) =="
printf 'a.b\naxb\n' | grep -F 'a.b'
echo "== rc no match =="
grep 'zzz' g.txt; echo "rc=$?"
rm -f g.txt
