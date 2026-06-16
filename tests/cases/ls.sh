rm -rf lstest 2>/dev/null
mkdir lstest
printf '' > lstest/b.txt
printf '' > lstest/a.txt
printf '' > lstest/.hidden
echo "== plain =="
ls lstest
echo "== -a =="
ls -a lstest
echo "== -A =="
ls -A lstest
echo "== -r =="
ls -r lstest
echo "== file arg =="
ls lstest/a.txt
rm -rf lstest
