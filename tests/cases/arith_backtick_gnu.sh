# 2026-10-30 (esdk-a2's report): a backtick substitution inside an arithmetic expression is run and its output
# spliced in before the arithmetic, as `$( )` is and as bash does (both forms). It was "Unexpected '`' in arithmetic
# expression": `dir=${dir:0:`expr index "$dir" '#'`-1}` broke four ESDK SDK helper scripts. Every arithmetic context:
# a substring's offset and length, $(( )), (( )), an array index; bash's backtick escapes (`\$` keeps a `$`).
# Expected: GNU bash 5.1 via WSL.
d='E:/x#y'
echo "[${d:0:`expr index "$d" "#"`-1}]"
echo "[${d:0:$(expr index "$d" "#")-1}]"
echo "[${d:`echo 2`}]"
echo "[${d: -`echo 1`}]"
n=2; echo "[${d:`echo $n`:`echo 3`}]"
echo "$(( `echo 3` + 4 ))"
(( x = `echo 5` * 2 )); echo "x=$x"
a=(p q r s); echo "${a[`echo 2`]}"
n=4; echo "$(( `echo \$n` + 1 ))"
echo "$(( `echo 10` / `echo 2` ))"
