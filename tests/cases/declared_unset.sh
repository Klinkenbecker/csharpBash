# 2026-10-08: a declaration with no value leaves the variable UNSET, as in bash. `local j` set j to
# "" (so ${j-dflt}, [[ -v j ]] and declare -p saw it as set), `export m` on an unset m put m="" in a
# child's environment, and an empty array read as set. Expected output: GNU bash via WSL.
f1() { local j; echo "1  [${j-unset}] [${#j}]"; [[ -v j ]] && echo "1v set" || echo "1v unset"; }; f1
j=global; f2() { local j; echo "2  [${j-unset}]"; }; f2; echo "2b [$j]"; unset j
f3() { local j; j+=x; echo "3  [$j]"; }; f3
f4() { local -i n; n=5+1; echo "4  [$n]"; }; f4
f5() { local j; declare -p j; }; f5
f6() { local j=; echo "6  [${j-unset}]"; }; f6
f7() { typeset j; echo "7  [${j-unset}]"; }; f7
k=kept; declare k; echo "8  [$k]"
unset m; export m; echo "9  [${m-unset}]"; "$BASH" -c 'echo "9c [${m-unset}]"'; export -p | grep -c '^declare -x m$'
f10() { local -a arr; echo "10 [${arr-unset}] [${#arr[@]}]"; }; f10
unset r; readonly r; echo "11 [${r-unset}]"
f12() { local x y=2 z; echo "12 [${x-u}] [${y-u}] [${z-u}]"; }; f12
v=outer; f13() { local v; v=inner; }; f13; echo "13 [$v]"
f14() { local j=1; local j; echo "14 [$j]"; }; f14
g=global; f15() { local g; g=tmp printenv g; echo "15 [${g-unset}]"; }; f15; echo "15b [$g]"
f16() { local -x lx; "$BASH" -c 'echo "16 [${lx-unset}]"'; }; f16
declare -a a1; [[ -v a1 ]] && echo "17 set" || echo "17 unset"
a2=(x); [[ -v a2 ]] && echo "18 set" || echo "18 unset"
a3=([1]=x); [[ -v a3 ]] && echo "19 set" || echo "19 unset"; echo "19b [${a3-unset}]"
declare -A m1; m1[k]=v; [[ -v m1 ]] && echo "20 set" || echo "20 unset"
fn() { :; }; declare -a fn; unset fn; type -t fn
