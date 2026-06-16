echo "== default echo (all on one line) =="
printf 'a\nb\nc\n' | xargs
echo "== -n1 =="
printf 'a\nb\nc\n' | xargs -n1 echo
echo "== -n2 =="
printf '1\n2\n3\n4\n5\n' | xargs -n2 echo
echo "== -I{} =="
printf 'x\ny\n' | xargs -I {} echo "item=[{}]"
