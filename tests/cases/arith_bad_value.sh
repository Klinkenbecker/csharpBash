# 2026-10-19 (red team, older): a variable whose value is not a valid arithmetic expression is an error, not 0
# (`x='1+'; $((x))` was 0); a value naming itself is "expression recursion level exceeded" (it was cut off as 0 at
# depth 8). Valid indirect values evaluate, a chain of 60 too. Each probe runs in a child shell with its stderr
# redirected: the error's wording, where it is printed and what bash abandons after it are an open difference.
# Expected: GNU bash 5.1 via WSL, byte-identical.
export x='1+' s=s
"$BASH" -c 'echo "a: $((x))"' 2>/dev/null; [ $? -ne 0 ] && echo "x='1+' in \$(( )): an error" || echo "x='1+': no error"
"$BASH" -c '(( x ))' 2>/dev/null; [ $? -ne 0 ] && echo "x='1+' in (( )): fails"
"$BASH" -c 'let "w = x"' 2>/dev/null; [ $? -ne 0 ] && echo "x='1+' in let: fails"
"$BASH" -c 'echo "$((s))"' 2>&1 | grep -o 'expression recursion level exceeded'
y='2*3'; echo "y+1: $((y + 1))"
z=zz; zz=4; echo "z: $((z))"
a1=a2; for i in $(seq 2 60); do eval "a$i=a$((i+1))"; done; a61=5; echo "chain of 60: $((a1))"
e=''; echo "empty: $((e + 1))"
