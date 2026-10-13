# 2026-10-12: array literals in declaration builtins, as bash 5.1. `local p=($(cmd))` (any `$( )`, `<( )` in a
# declaration's literal) was a syntax error; `export v=(a b)` stored the text; `readonly r=(x y)` set nothing;
# a quoted value that looks like (…) became an array (`local s="(x y)"`), which only -a/-A allow; `${#x[@]}`
# of a set scalar was 0. Not covered: a local array leaking to the global scope (an open defect).
# Expected output: GNU bash via WSL.
f() {
local p=($(echo a b)); echo "1 ${#p[@]} ${p[1]}"
declare -a q=($(echo c d e)); echo "2 ${#q[@]}"
local r=( $(echo "x y") z ); echo "3 ${#r[@]}"
local s=($(echo $(echo n1 n2))); echo "4 ${#s[@]}"
local t=($((1+2)) $(echo w)); echo "5 ${t[*]}"
local u=(<(true)); echo "6 ${#u[@]}"
export v=($(echo e1)); echo "7 [${v[0]}]"
local -a w=( "$(printf 'a b')" $(printf 'c d') ); echo "8 ${#w[@]}"
}
f
declare -f f | head -4
export v2=(a b); echo "9 [${v2[0]}] [${v2[1]}] n=${#v2[@]}"
readonly r2=(x y); echo "10 [${r2[1]}]"
export v3+=(c); echo "11 ${#v3[@]}"
export s1="(not array)"; echo "12 [$s1] ${#s1[@]}"
declare s2="(not array)"; echo "13 [$s2] ${#s2[@]}"
g() { local t9="(q r)"; echo "14 [$t9] ${#t9[@]}"; }; g
x="(p q)"; declare w2=$x; echo "15 [$w2] ${#w2[@]}"
declare -a x2='(a b)'; echo "16 ${#x2[@]} [${x2[0]}]"
declare -A m2='([k]=v)'; echo "17 ${#m2[@]} [${m2[k]}]"
y="(c d)"; declare -a z2=$y; echo "18 ${#z2[@]} [${z2[0]}]"
readonly -a r3='(p q)'; echo "19 ${#r3[@]}"
s20=abc; t20="a b"; unset n20; e20=""; echo "20 ${#s20[@]} ${#t20[@]} ${#n20[@]} ${#e20[@]}"
