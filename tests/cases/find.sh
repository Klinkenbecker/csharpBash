rm -rf ftest 2>/dev/null
mkdir ftest
mkdir ftest/sub
printf 'a\n' > ftest/a.txt
printf 'b\n' > ftest/b.log
printf 'c\n' > ftest/sub/c.txt
echo "== all =="
find ftest
echo "== name *.txt =="
find ftest -name '*.txt'
echo "== type d =="
find ftest -type d
echo "== maxdepth 1 =="
find ftest -maxdepth 1
rm -rf ftest
