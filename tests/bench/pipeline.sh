# Text-pipeline benchmark: grep | sed | sort | uniq | awk over 50 k lines, 5 times.
# The input is generated once into the temp directory.
f="${TMPDIR:-${TEMP:-/tmp}}/bench-nums.txt"
[ -s "$f" ] || seq 1 50000 > "$f"
for r in 1 2 3 4 5; do
	n=$(grep 7 "$f" | sed 's/7/x/' | sort -r | uniq | awk '{c++} END{print c}')
done
echo "pipeline done: $n"
