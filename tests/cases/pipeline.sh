echo hi | tr a-z A-Z | wc -l
printf 'x\nx\ny\nx\n' | uniq | wc -l
echo 'a b c' | tr ' ' '\n' | tac | tr a-z A-Z
echo hi | wc -c
