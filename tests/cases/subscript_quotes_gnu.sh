# 2026-11-10 (git-b3): a quoted subscript in an expansion was looked up WITH its quotes: ${P["a b"]}, ${P["$k"]},
# ${P['c']} and x=${P["c"]} read empty (GNU: the value), and an indexed ${A["1"]} was an arithmetic error. A subscript is
# read as a word: '...' literal, "..." expanded inside, \ takes the next character. Expected: GNU bash via WSL.
declare -A P; k="a b"; P[$k]=one; P[c]=two; P['x"y']=three; P['$k']=four
echo "[${P["a b"]}] [${P["$k"]}] [${P[$k]}] [${P["c"]}] [${P[c]}] [${P['c']}]"
x=${P["c"]}; echo "assigned: [$x]"
echo "quote inside: [${P['x"y']}] [${P["x\"y"]}] dollar literal: [${P['$k']}]"
[[ -v P["c"] ]] && echo "-v quoted: yes"; [[ -v P["nope"] ]] || echo "-v missing: no"
echo "default: [${P["missing"]:-dflt}] length: ${#P["a b"]}"
declare -a A=(z y w); i=2
echo "indexed: [${A["1"]}] [${A["$i"]}] [${A[1]}]"
