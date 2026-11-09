# 2026-11-07 (git-b3, Wombat's test runner): an array assignment beside other assignments in one statement, as bash.
# `a=() b=()` was "syntax error near unexpected token `b='" and `x= y=1 z=()` assigned none of the three (an array
# assignment ended the statement and dropped the scalar ones before it). Before a command word an array assignment does
# nothing (bash: not even in the function it prefixes); the scalar ones are its prefixes. Expected = GNU bash 5.1 via WSL.
t() { printf '%-40s' "$1"; out=$("$BASH" -c "$1" 2>&1); echo "[$out] rc=$?"; }
t 'a=() b=(); echo ok'
t 'set -u; x= y=1 z=(); echo "[$x][$y][${#z[@]}]"'
t 'a=(1 2) b=3; echo "${a[1]} $b"'
t 'b=3 a=(1 2); echo "$b ${a[1]}"'
t 'a=(1 2) b=(3 4) c=5; echo "${a[1]}${b[0]}$c"'
t 'a+=(x) b=1; echo "${a[0]} $b"'
t 'a=([k]=v) b=2; echo "${a[k]}$b"'
t 'a[1]=x b=2; echo "${a[1]}$b"'
t 'b=2 a[1]=x; echo "$b${a[1]}"'
t 'c=$(echo 1) a=($c $c) d=${a[1]}; echo "$c ${#a[@]} $d"'
t 'a=(1 2) echo hi; echo "after: [${a[1]}]"'
t 'b=3 a=(1 2) echo hi; echo "after: [$b] [${a[1]}]"'
t 'f() { echo "in f: [${a[1]}] [$b]"; }; a=(1 2) b=3 f; echo "after: [${a[1]}] [$b]"'
t 'f() { local a=(1 2) b=3; echo "${a[1]}$b"; }; f'
t 'declare a=(1 2) b=(3); echo "${a[1]}${b[0]}"'
f() { a=() b=(); x=1 y=(2) z=3; }
declare -f f
echo "--- set -x"
"$BASH" -c 'set -x; a=() b=1 c=(2 3)' 2>&1 | sed 's/^+* //'
