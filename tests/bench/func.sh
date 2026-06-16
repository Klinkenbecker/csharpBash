add() {
	local r=$(( $1 + $2 ))
	: $r
}
i=0
while [ $i -lt 100000 ]; do
	add $i 7
	i=$((i + 1))
done
echo "func done: $i"
