printf 'a\nb\nc\nd\n' > d1.txt
printf 'a\nc\nd\ne\n' > d2.txt
echo "== delete+append =="
diff d1.txt d2.txt; echo "rc=$?"
printf 'hello\nworld\n' > c1.txt
printf 'hello\nthere\n' > c2.txt
echo "== change =="
diff c1.txt c2.txt; echo "rc=$?"
echo "== identical =="
diff d1.txt d1.txt; echo "rc=$?"
echo "== brief =="
diff -q c1.txt c2.txt; echo "rc=$?"
rm -f d1.txt d2.txt c1.txt c2.txt
