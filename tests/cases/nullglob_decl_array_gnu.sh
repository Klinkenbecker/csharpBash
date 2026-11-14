# 2026-11-12 (git-9d's report): under nullglob, a declaration builtin's array literal with a glob (local a=( * ))
# was itself taken as a pattern (`a=(*)`), matched no file and was removed, so `local` ran with no argument and
# LISTED EVERY VARIABLE (secrets included), and the array stayed empty. The parser now hands the folded literal over
# quoted. A regression must not print the environment into a log either: the body runs in a child and only its
# non-declare lines are shown, with a count of declare lines (0). Expected = GNU bash via WSL.
if [ -z "${NG_CHILD-}" ]; then out=$(NG_CHILD=1 "${BASH:-bash}" "$0" 2>&1); echo "declare lines: $(printf "%s\n" "$out" | grep -c "^declare")"; printf "%s\n" "$out" | grep -v "^declare" | grep -v "^\"$"; exit 0; fi
d=$(mktemp -d) && cd "$d" && touch a b c || exit 1
f1() { shopt -s nullglob; local a=( * ); echo "1 n=${#a[@]}"; }; f1
f2() { shopt -s nullglob; local a=( z* ); echo "2 n=${#a[@]}"; }; f2
f3() { shopt -s nullglob; declare a=( * ); echo "3 n=${#a[@]}"; }; f3
f4() { shopt -s nullglob; local -a a=( * b ); echo "4 n=${#a[@]}"; }; f4
shopt -s nullglob; declare g=( * ); echo "5 n=${#g[@]}"; export e=( * ) 2>/dev/null; echo "6 n=${#e[@]}"
f7() { local x=*; echo "7 x=$x"; }; f7
shopt -u nullglob; f8() { local a=( * ); echo "8 n=${#a[@]}"; }; f8
cd / && rm -rf "$d"
d=$(mktemp -d) && cd "$d" && touch a b c || exit 1
f9() { local a=( {1..3} ); echo "9 n=${#a[@]} ${a[2]}"; }; f9
f10() { local a=( "x y" $(echo p q) 'r s' ); echo "10 n=${#a[@]} [${a[0]}] [${a[3]}]"; }; f10
f11() { local -A m=( [k]=v [j]=w ); echo "11 ${m[k]}${m[j]}"; }; f11
f12() { local a=( 1 ); local a+=( 2 * ); echo "12 n=${#a[@]}"; }; f12
shopt -s nullglob; f13() { local a=( x* ) b=( * ); echo "13 n=${#a[@]} ${#b[@]}"; }; f13
cd / && rm -rf "$d"
