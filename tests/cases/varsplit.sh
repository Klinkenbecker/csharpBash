v="a b c"
for x in $v; do echo "[$x]"; done
e=
for y in $e; do echo NEVER; done
echo "empty-ok"
n=42
echo "plain=$n"
