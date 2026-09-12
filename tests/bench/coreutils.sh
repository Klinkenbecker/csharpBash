# Spawn-cost benchmark: 300 iterations of the coreutil calls scripts make in loops
# (900 process launches under a real bash; zero here). Compare with tests/bench/compare.sh.
i=0
while [ $i -lt 300 ]; do
	b=$(basename /some/path/file.txt)
	d=$(dirname /some/path/file.txt)
	n=$(echo "$b" | wc -c)
	i=$((i + 1))
done
echo "coreutils done: $b $d $n"
