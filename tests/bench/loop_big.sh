i=0
while [ $i -lt 2000000 ]; do
	i=$((i + 1))
done
echo "loop_big result: $i"
