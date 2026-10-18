# 2026-10-17: set -x traces compound commands as bash does: each [[ ]] test with its operands expanded, (( )) with its
# text, every turn of a for head as written, the three parts of an arithmetic for, a case head; none was traced.
# Expected: GNU bash 5.1 (WSL), byte-identical.
exec 2>&1
v='a b'
set -x
[[ $v == a* && -n "$v" ]]
(( 1 + 2 ))
for i in 1 "$v"; do :; done
for (( j=0; j<2; j++ )); do :; done
case $v in a*) : ;; esac
while (( k++ < 1 )); do :; done
[[ $v ]]
[[ $v != "x*" ]]
[[ 2 -lt 10 ]]
[[ $v =~ ^a ]]
[[ ! -z $v ]]
set +x
