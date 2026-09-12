#!/usr/bin/env bash
# Wall-clock comparison of every tests/bench/*.sh under a reference bash and C#Bash.
# Best of N (default 3) per script per shell; also checks the two outputs agree.
#
#   tests/bench/compare.sh [path\to\Bash.exe] [reference bash] [rounds]
#
# Defaults: dist/Bash.exe (else Bash/bin/Release/net8.0/Bash.exe); the bash running this
# script; 3 rounds. Run it from Git Bash. Numbers include process startup (~0.08 s for the
# .NET runtime); use tools/bench.csx for per-iteration A/Bs.
here="$(cd "$(dirname "$0")" && pwd)"
root="$(cd "$here/../.." && pwd)"
cs="${1:-}"
if [ -z "$cs" ]; then
	if [ -x "$root/dist/Bash.exe" ]; then cs="$root/dist/Bash.exe"; else cs="$root/Bash/bin/Release/net8.0/Bash.exe"; fi
fi
ref="${2:-$BASH}"
rounds="${3:-3}"
export TIMEFORMAT='%3R'

best() {
	local shell=$1 script=$2 min=999 t r
	for ((r = 0; r < rounds; r++)); do
		t=$( { time "$shell" "$script" >/dev/null 2>&1; } 2>&1 )
		min=$(awk -v a="$t" -v b="$min" 'BEGIN{print (a<b)?a:b}')
	done
	echo "$min"
}

echo "reference : $ref"
echo "candidate : $cs"
printf '%-12s %10s %10s %8s\n' script reference csharpbash ratio
for s in loop arith func loop_big coreutils pipeline find; do
	[ -f "$here/$s.sh" ] || continue
	g=$(best "$ref" "$here/$s.sh")
	c=$(best "$cs" "$here/$s.sh")
	ratio=$(awk -v a="$g" -v b="$c" 'BEGIN{ if (b > 0) printf "%.2fx", a/b; else print "-" }')
	printf '%-12s %10s %10s %8s\n' "$s" "$g" "$c" "$ratio"
done
echo "--- startup: -c 'echo hi', best of 5"
for shell in "$ref" "$cs"; do
	min=999
	for r in 1 2 3 4 5; do
		t=$( { time "$shell" -c 'echo hi' >/dev/null; } 2>&1 )
		min=$(awk -v a="$t" -v b="$min" 'BEGIN{print (a<b)?a:b}')
	done
	echo "$shell: ${min}s"
done
echo "--- outputs agree?"
for s in coreutils pipeline find; do
	[ -f "$here/$s.sh" ] || continue
	if diff <("$ref" "$here/$s.sh" 2>&1) <("$cs" "$here/$s.sh" 2>&1) >/dev/null; then echo "$s: same"; else echo "$s: DIFFER"; fi
done
