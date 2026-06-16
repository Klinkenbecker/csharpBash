arr=(one two three)
echo "T1 single index: ${arr[0]} / ${arr[2]}"

arr[3]=four
echo "T2 append index: ${arr[3]}"

echo "T3 for over @:"
for f in "${arr[@]}"; do echo "  - $f"; done

echo "T4 keys: ${!arr[@]}"

unset arr[1]
echo "T5 after unset arr[1]: values=${arr[@]} len=${#arr[@]} keys=${!arr[@]}"

declare -A map
map[name]=Alice
echo "T6 assoc single: ${map[name]}"

declare -A colors
colors[red]=ff0000
colors[green]=00ff00
echo "T7 assoc iterate:"
for k in "${!colors[@]}"; do echo "  $k=${colors[$k]}"; done

i=2
echo "T8 arith index: ${arr[$i]} / ${arr[$((i+1))]}"
