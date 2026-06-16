echo a >/dev/null; echo "null-ok"
kill -0 999999 2>/dev/null; echo "missing rc=$?"
echo x > /nonexist_zzz/f.txt; echo "continued"
echo $((1/0)); echo "div-continued"
printf 'one\ntwo\n' | head -n1
out=$(cat nosuchfile_xyz 2>&1); echo "merged=[$out]"
cat nosuchfile_xyz >/dev/null 2>&1; echo "both rc=$?"
