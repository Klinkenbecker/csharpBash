#!/usr/bin/env bash
# The compatibility battery: C#Bash against a reference bash (WSL's GNU bash), probe by probe, with a ratcheted
# baseline.
#
#   bash tests/compat/run-compat.sh [--no-build] [--reference <bash.exe>] [--update-baseline] [--only 12,34] [--show-all]
#
# probes.txt holds probe scripts separated by lines holding only "----" (append new ones at the end: numbers are
# positional). Each probe runs as a script file in both shells, each in a fresh fixture folder (a.txt, b.txt, c.txt,
# d2/sub/deep.txt, empty.txt, n.md), stdin an empty pipe, a 20 s limit. A probe PASSES when stdout plus "exit=<code>"
# are byte-identical. baseline.txt lists the probes expected to pass: one failing is a REGRESSION and the run exits 1;
# newly passing ones are listed, and --update-baseline writes them in. The reference is WSL's bash (wsl.exe -e bash)
# unless --reference names a bash.exe; it is run first to identify it, and C#Bash (its BASH_VERSION says koliada) is
# refused, since a battery against itself cannot fail. The candidate is the Release build of the tree.
#
# Replaces run-compat.ps1 (the architect, 2026-11-14: PowerShell removed before the next publish), printing the same
# lines: REGRESS/DIFF per probe, then "<n> pass, <n> diff (<n> hung) of <n> probes; baseline <n>", "NEW PASS (not in
# baseline): ...", "REGRESSIONS: ..." (tools/deploy.sh reads those).
set -u
here="$(cd "$(dirname "$0")" && pwd)"
root="$(cd "$here/../.." && pwd)"
nobuild=0 update=0 showall=0 reference="" only=","
while [ $# -gt 0 ]; do
	case $1 in
		--no-build) nobuild=1 ;;
		--update-baseline) update=1 ;;
		--show-all) showall=1 ;;
		--reference) reference=$2; shift ;;
		--only) only=",$2,"; shift ;;
		*) echo "run-compat: unknown option '$1'" >&2; exit 2 ;;
	esac
	shift
done
export BASH_UNSERVICED_LOG=/dev/null
exe="$root/Bash/bin/Release/net10.0/Bash.exe"
if [ $nobuild -eq 0 ]; then
	echo "Building (Release)..."
	dotnet build -c Release "$root/Bash" -nologo < /dev/null > /dev/null || { echo "run-compat: build failed" >&2; exit 2; }
fi
[ -f "$exe" ] || { echo "run-compat: interpreter not found (run without --no-build)" >&2; exit 2; }
if [ -n "$reference" ]; then
	[ -f "$reference" ] || { echo "run-compat: --reference $reference does not exist" >&2; exit 2; }
	ref=("$reference")
else
	wsl="${SYSTEMROOT:-C:/Windows}/System32/wsl.exe"
	[ -f "$wsl" ] || { echo "run-compat: no reference bash: WSL is not installed; pass --reference <path to bash.exe>" >&2; exit 2; }
	ref=("$wsl" -e bash)
fi
refver=$("${ref[@]}" -c 'echo "$BASH_VERSION"' < /dev/null 2> /dev/null | tr -d '\r')
[ -n "$refver" ] || { echo "run-compat: the reference bash does not run: ${ref[*]}" >&2; exit 2; }
case $refver in *koliada*) echo "run-compat: the reference is C#Bash ($refver), not a reference bash" >&2; exit 2 ;; esac
echo "reference : ${ref[*]}  (bash $refver)"
echo "candidate : $exe"

work="$here/work"
rm -rf "$work"; mkdir -p "$work/probes" || exit 2
# the probes: split at lines that are only "----" (CRLF read as LF), each ending in a newline
awk -v dir="$work/probes" 'BEGIN { n = 1; f = sprintf("%s/%03d.sh", dir, n) } { sub(/\r$/, "") } /^----$/ { close(f); n++; f = sprintf("%s/%03d.sh", dir, n); printf "" > f; next } { print > f } END { close(f); print n > (dir "/count") }' "$here/probes.txt"
total=$(cat "$work/probes/count")
fixture() {
	rm -rf "$1"; mkdir -p "$1/d2/sub"
	printf 'a\nb\n' > "$1/a.txt"; printf 'zz\n' > "$1/b.txt"; printf 'aXb\n' > "$1/c.txt"
	printf 'deep\n' > "$1/d2/sub/deep.txt"; : > "$1/empty.txt"; printf '# md\n' > "$1/n.md"
}
# one shell on one probe, from its fixture: stdout + "exit=<code>" into $3, stderr into $4; 124 when it ran past 20 s
runone() {
	local fx=$1; shift; local out=$1 err=$2; shift 2
	( cd "$fx" && true | timeout 20 "$@" 2> "$err" | cat > "$out"; echo "exit=${PIPESTATUS[1]}" >> "$out" )
}
clip() { local s; s=$(tr '\n' '|' < "$1"); [ $showall -eq 1 ] || [ ${#s} -le "$2" ] && printf '%s' "$s" || printf '%s...' "${s:0:$2}"; }
baseline=","; [ -f "$here/baseline.txt" ] && baseline=",$(grep -E '^[0-9]+$' "$here/baseline.txt" | tr -d '\r' | paste -sd, -),"
passed=() failed=() hung=() newpass=() regressed=()
for ((n = 1; n <= total; n++)); do
	[ "$only" = "," ] || [[ $only == *",$n,"* ]] || continue
	script=$(printf '../probes/%03d.sh' "$n")
	fixture "$work/fx_real"; fixture "$work/fx_cs"
	runone "$work/fx_real" "$work/r.out" "$work/r.err" "${ref[@]}" "$script"
	runone "$work/fx_cs" "$work/c.out" "$work/c.err" "$exe" "$script"
	if cmp -s "$work/r.out" "$work/c.out"; then
		passed+=("$n"); [[ $baseline == *",$n,"* ]] || newpass+=("$n")
	else
		failed+=("$n")
		[ "$(tail -1 "$work/c.out")" = "exit=124" ] && hung+=("$n")
		tag=DIFF; [[ $baseline == *",$n,"* ]] && { tag=REGRESS; regressed+=("$n"); }
		printf '%-7s %03d: %s\n' "$tag" "$n" "$(head -1 "$work/probes/$(printf '%03d' "$n").sh")"
		echo "        real: $(clip "$work/r.out" 240)"
		echo "        cs  : $(clip "$work/c.out" 240)"
		[ -s "$work/c.err" ] && echo "        cserr: $(clip "$work/c.err" 200)"
	fi
done
nb=$(printf '%s' "$baseline" | tr ',' '\n' | grep -c .)
echo ""
echo "${#passed[@]} pass, ${#failed[@]} diff (${#hung[@]} hung) of $(( ${#passed[@]} + ${#failed[@]} )) probes; baseline $nb"
[ ${#newpass[@]} -gt 0 ] && echo "NEW PASS (not in baseline): $(IFS=,; echo "${newpass[*]}")"
[ ${#regressed[@]} -gt 0 ] && echo "REGRESSIONS: $(IFS=,; echo "${regressed[*]}")"
if [ $update -eq 1 ]; then
	if [ "$only" = "," ]; then printf '%s\n' "${passed[@]}" | sort -n -u > "$here/baseline.txt"
	else { printf '%s' "$baseline" | tr ',' '\n' | grep . | while read -r b; do [[ $only == *",$b,"* ]] || echo "$b"; done; printf '%s\n' "${passed[@]}"; } | sort -n -u > "$here/baseline.txt"; fi
	echo "baseline written: $(grep -c . "$here/baseline.txt") probes"
fi
[ ${#regressed[@]} -gt 0 ] && exit 1
exit 0
