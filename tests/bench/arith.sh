sum=0
i=0
while [ $i -lt 200000 ]; do
	sum=$((sum + i * 2 - 1))
	i=$((i + 1))
done
echo "arith result: $sum"
