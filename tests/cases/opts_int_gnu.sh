# 2026-10-30 (the "match GNU" item): an option that takes a number refuses one that is not, as GNU does, each tool in
# its own words and exit code. Opts.GetInt took a bad value as the default (split -l x made one piece, rc 0), and so did
# every tool below. Expected: GNU coreutils 8.32, grep, findutils (xargs) via WSL, LC_ALL=C (plain quotes).
export LC_ALL=C
cd "${TMPDIR:-${TEMP:-/tmp}}" || exit 1
d="opts-int-$$"; mkdir -p "$d" && cd "$d" || exit 1
printf 'a b\nc d\n' > in
r() { "$@" < in 2>&1 | head -3; echo "rc=${PIPESTATUS[0]}"; }
for v in x -1 0; do
	echo "=== value '$v'"
	echo "--- uniq -f";   r uniq -f "$v"
	echo "--- uniq -s";   r uniq -s "$v"
	echo "--- uniq -w";   r uniq -w "$v"
	echo "--- nl -w";     r nl -w "$v"
	echo "--- nl -v";     r nl -v "$v"
	echo "--- nl -i";     r nl -i "$v"
	echo "--- fold -w";   r fold -w "$v"
	echo "--- od -w";     r od -w"$v"
	echo "--- base64 -w"; r base64 -w "$v"
	echo "--- grep -A";   r grep -A "$v" a
	echo "--- grep -B";   r grep -B "$v" a
	echo "--- grep -m";   r grep -m "$v" a
	echo "--- xargs -n";  r xargs -n "$v" echo
	echo "--- xargs -L";  r xargs -L "$v" echo
	echo "--- du -d";     du -d "$v" in 2>&1 > /dev/null | head -3; echo "rc=${PIPESTATUS[0]}"   # errors only: its sizes are the file system's
done
echo "--- grep -c -m 0"; r grep -c -m 0 a
cd .. && rm -rf "$d"
