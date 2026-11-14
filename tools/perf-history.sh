#!/usr/bin/env bash
# The advantage graph at the top of docs/PERFORMANCE.md (the architect, 2026-11-14: "We should generate this graph each
# publish"): C#Bash's advantage over Git Bash and WSL bash, per benchmark row, at every published build.
#
#   bash tools/perf-history.sh --add    the doc review of a publish: append the deployed build's rows, from README's
#                                       benchmark table (already updated from that build's compare3 run), then draw
#   bash tools/perf-history.sh          draw docs/perf-history.svg from docs/perf-history.tsv
#
# A CONTRACT TOOL: tools/publish.sh refuses to publish when the history has no rows for the deployed build.
# docs/perf-history.tsv: build, row, C#Bash s, Git Bash s, WSL bash s (seconds as README published them).
set -u
root="$(cd "$(dirname "$0")/.." && pwd)"
cd "$root" || exit 2
die() { echo "perf-history: $*" >&2; exit 2; }
tsv=docs/perf-history.tsv; svg=docs/perf-history.svg
[ -f "$tsv" ] || die "no $tsv"
if [ "${1-}" = "--add" ]; then
	stamp=$(dist/Bash.exe --version 2>/dev/null | tail -1)
	build=${stamp##* }
	[[ $build =~ ^[0-9]+$ ]] || die "cannot read the deployed build from dist/Bash.exe ('$stamp')"
	cut -f1 "$tsv" | grep -qx "$build" && die "build $build is already in $tsv"
	# README's rows: | `name` — ... | **C#Bash** | Git Bash | WSL | vs Git | vs WSL |; the numbers are the last five cells
	rows=$(grep -E '^\| (`|startup)' README.md | head -8 | tr -d '*×' |
		awk -F'|' -v b="$build" '{n=$2; sub(/^ *`?/,"",n); sub(/[` ].*/,"",n); k=NF-1; printf "%s\t%s\t%s\t%s\t%s\n", b, n, $(k-4)+0, $(k-3)+0, $(k-2)+0}')
	[ "$(printf '%s\n' "$rows" | grep -c .)" -eq 8 ] || die "README.md's benchmark table did not give eight rows"
	printf '%s\n' "$rows" | awk -F'\t' '$3 <= 0 || $4 <= 0 || $5 <= 0 { bad = 1 } END { exit bad }' || die "a row of README's table has no number"
	printf '%s\n' "$rows" >> "$tsv"
	echo "perf-history: build $build added to $tsv"
fi
# drawn by the deployed build's awk, whatever shell runs this script (an older installed shell drew it wrong once:
# its awk passed a function's parameter on empty, fixed in 806)
dist/Bash.exe -c 'awk -f tools/perf-history.awk "$1"' perf-history "$tsv" > "$svg.new" < /dev/null || die "drawing failed"
grep -q '</svg>' "$svg.new" || die "drawing failed"
mv "$svg.new" "$svg"
echo "perf-history: $svg drawn ($(cut -f1 "$tsv" | tail -n +2 | uniq | wc -l) builds)"
