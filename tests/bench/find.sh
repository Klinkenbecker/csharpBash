# File-system walk benchmark: find over a GENERATED tree of known size, three times.
#
# The tree is built beside this script (on the Windows filesystem) rather than in each shell's
# own temp dir, so all three shells walk the SAME files — for WSL that means reaching them over
# /mnt, which is a real cost of using WSL bash on Windows files and belongs in the number.
# It is generated rather than pointed at the repository so the count is fixed and the walk is
# bounded: pointing it at the repo pulls in .hg and bin/obj and takes minutes over 9P.
# BENCH_DIR, not $0: compare3.sh must SOURCE this into WSL bash (its Windows entry point takes only
# -c), and in a sourced script $0 is the shell, not the file — so `dirname "$0"` became /bin and the
# tree was built, or failed to build, in the wrong place. That silently made the WSL column of this
# row time a failure rather than a walk (caught 2026-09-13). The harness exports BENCH_DIR in each
# shell's own path form; the $0 path is the fallback for running this script directly.
here="${BENCH_DIR:-$(cd "$(dirname "$0")" && pwd)}"
tree="$here/.findtree"
[ -d "$here" ] || { echo "find: bench dir not found: $here" >&2; exit 1; }

if [ ! -d "$tree" ]; then
	mkdir -p "$tree" || exit 1
	d=0
	while [ $d -lt 20 ]; do
		mkdir -p "$tree/d$d/sub"
		f=0
		while [ $f -lt 10 ]; do
			: > "$tree/d$d/f$f.cs"
			: > "$tree/d$d/sub/g$f.md"
			f=$((f + 1))
		done
		d=$((d + 1))
	done
fi

for r in 1 2 3; do
	n=$(find "$tree" -name '*.cs' -o -name '*.md' | wc -l)
done
echo "find done: $n"
