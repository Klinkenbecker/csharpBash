# 2026-11-15 (KNOWN_ISSUES "arithmetic error messages", the bugs list): what an arithmetic error does, as bash 5.1. In an
# expansion or an assignment ($(( )), a substring offset, a subscript, an integer variable) it abandons the line, the
# rest of it unrun (C#Bash went on, status 2; an integer variable took 0 silently); let, (( )), for (( )) and [[ -eq ]]
# report it and return 1 (let and for (( )) gave 2). `**` is integer (3**39 was off through a double) and a negative
# exponent is an error even in a branch not taken (it gave 0). Messages are stderr, dropped here (their wording: its own
# case). Expected = GNU bash 5.1 via WSL.
exec 2>/dev/null
let "1 +"; echo "A let rc=$?"
(( 1 + )); echo "B (( )) rc=$?"
a[1+]=x; echo "C subscript-assign rc=$?"
s=hello; echo "${s:1+}"; echo "D substring rc=$?"
for ((i=0; i<1+; i++)); do :; done; echo "E for-arith rc=$?"
[[ 1 -eq 1+ ]]; echo "F [[ -eq ]] rc=$?"
declare -i n=1+; echo "G declare -i rc=$?"
declare -i m; m=1+; echo "H int-assign rc=$?"
m+=1+; echo "I int-append rc=$?"
echo $(( 2 ** -1 )); echo "J neg-exp rc=$?"
x=$(( 1/0 )) y=2; echo "K assign-only rc=$? y=$y"
echo "Z end"
echo $((3**39)) $((2**63)) $((2**64)) $((-3**3)) $((0**0)) $((7**1))
f() { echo $(( 1 / 0 )); echo "in f"; }; f; echo "after f $?"
echo $(( 0 && 1/0 )) $(( 1 || 1%0 ))
echo $(( 0 && 2**-1 )); echo "not here"
echo "the end"
