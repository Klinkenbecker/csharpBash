#!/usr/bin/env bash
# The suite: runs every case in tests/cases.tsv and compares its stdout with tests/expected/<name>.out.
#
#   bash tests/run-tests.sh [--no-build] [--detached] [--interpreter <exe>]
#
#   --no-build           use the Release build as it is (otherwise `dotnet build -c Release Bash` first)
#   --interpreter <exe>  test that interpreter instead (implies --no-build); the deploy tests its AOT build so
#   --detached           start every case as Claude Code starts its shell: no console (DETACHED_PROCESS), stdin NUL,
#                        stdout a file, stderr NUL (tools/launch); otherwise each case gets this run's console, an
#                        empty pipe for stdin and a pipe for stdout, stderr dropped
#
# Replaces tests/run-tests.ps1 (the architect, 2026-11-14: PowerShell removed before the next publish), line for line
# in what it prints: PASS / FAIL / SKIP / MISS / BYPASSED / TIMEOUT per case, then "N passed, N failed", "N skipped:
# ...", "!!! N BYPASSED, NOT TESTED (why): names", "Failed: ..." (tools/deploy.sh and tools/publish.sh read those).
# Differences, on purpose: lines are compared EXACTLY (PowerShell's Compare-Object ignored case), and a console-mode
# case is stopped after 120 s as a detached one was (a case reading stdin once hung a run 19 h).
# A case's single output line "SKIP: why" is a skip (never a pass), "BYPASSED: why" a loud bypass (an optional program,
# MSYS, missing: never a failure). Expected files are authored or GNU's, never captured from C#Bash.
set -u
tests="$(cd "$(dirname "$0")" && pwd)"
root="$(cd "$tests/.." && pwd)"
nobuild=0 detached=0 exe=""
while [ $# -gt 0 ]; do
	case $1 in
		--no-build) nobuild=1 ;;
		--detached) detached=1 ;;
		--interpreter) [ $# -ge 2 ] || { echo "run-tests: --interpreter needs a path" >&2; exit 2; }; exe=$2; nobuild=1; shift ;;
		*) echo "run-tests: unknown option '$1' (--no-build, --detached, --interpreter <exe>)" >&2; exit 2 ;;
	esac
	shift
done
# cases ask for unsupported options on purpose: not requests for the unserviced log; a name found nowhere is tried in
# WSL (the fallback chain), and a WSLENV naming an unreachable drive made every WSL start print "Failed to translate"
export BASH_UNSERVICED_LOG=/dev/null WSLENV=
[ -n "$exe" ] || exe="$root/Bash/bin/Release/net10.0/Bash.exe"
if [ $nobuild -eq 0 ]; then
	echo "Building (Release)..."
	dotnet build -c Release "$root/Bash" -nologo < /dev/null > /dev/null || { echo "run-tests: build failed" >&2; exit 2; }
fi
[ -f "$exe" ] || { echo "run-tests: interpreter not found: $exe (run without --no-build)" >&2; exit 2; }
launch="$root/tools/launch/bin/launch.exe"
if [ $detached -eq 1 ] && [ ! -f "$launch" ]; then
	echo "run-tests: the launcher is not built: $launch (build: dotnet build tools/launch -c Release -o tools/launch/bin)" >&2; exit 2
fi
# the Native AOT build (the shipped one) has no Bash.dll beside it; aot-only cases skip otherwise
aot=0; [ -f "$(dirname "$exe")/Bash.dll" ] || aot=1
echo "interpreter: $exe  ($("$exe" --version < /dev/null 2>/dev/null | tail -1))$([ $detached -eq 1 ] && echo '  DETACHED')"

tmp=$(mktemp) && act=$(mktemp) && exp=$(mktemp) || exit 2
norm() { awk '{ sub(/\r$/, ""); print }' "$1"; }   # lines as PowerShell split them: a CR before a newline is the line end
pass=0 fail=0 skip=0 bypass=0; failed=() skipped=() bypassed=(); bypasswhy=""
cd "$tests" || exit 2
while IFS= read -r line; do
	case $line in ''|'#'*) continue ;; esac
	# split by hand: a tab is whitespace to `read`, so an empty column (flags, args) vanished and the next took its place
	name=${line%%$'\t'*}; line=${line#*$'\t'}
	script=${line%%$'\t'*}; line=${line#*$'\t'}
	flags=${line%%$'\t'*}; line=${line#*$'\t'}
	args=${line%%$'\t'*}
	if [ "$flags" = aot-only ] && [ $aot -eq 0 ]; then
		echo "SKIP  $name  (Native AOT only)"; skip=$((skip + 1)); skipped+=("$name (Native AOT only)"); continue
	fi
	expected="$tests/expected/$name.out"
	if [ ! -f "$expected" ]; then echo "MISS  $name  (no expected file)"; fail=$((fail + 1)); failed+=("$name"); continue; fi
	read -r -a argv <<< "$args"
	if [ $detached -eq 1 ]; then
		"$launch" --detached --timeout 120 --cwd "$tests" --out "$tmp" -- "$exe" "$script" ${argv[@]+"${argv[@]}"}
		rc=$?
	else
		true | timeout 120 "$exe" "$script" ${argv[@]+"${argv[@]}"} 2> /dev/null | cat > "$tmp"
		rc=${PIPESTATUS[1]}
	fi
	[ "$rc" -eq 124 ] && echo "TIMEOUT $name"
	norm "$tmp" > "$act"   # compared as files: a $( ) would drop trailing empty lines
	lines=$(wc -l < "$act"); actual=$(head -1 "$act")
	if [ "$lines" -eq 1 ]; then
		case $actual in
			'SKIP: '*) echo "SKIP  $name  (${actual#SKIP: })"; skip=$((skip + 1)); skipped+=("$name (${actual#SKIP: })"); continue ;;
			'BYPASSED: '*) echo "BYPASSED  $name  (${actual#BYPASSED: })"; bypass=$((bypass + 1)); bypassed+=("$name"); bypasswhy=${actual#BYPASSED: }; continue ;;
		esac
	fi
	norm "$expected" > "$exp"
	if cmp -s "$exp" "$act"; then
		echo "PASS  $name"; pass=$((pass + 1))
	else
		echo "FAIL  $name"; fail=$((fail + 1)); failed+=("$name")
		diff "$exp" "$act" | awk '/^</ { print "        expected: " substr($0, 3) } /^>/ { print "        actual  : " substr($0, 3) }' | head -40
	fi
done < "$tests/cases.tsv"
rm -f "$tmp" "$act" "$exp"

echo ""
echo "$pass passed, $fail failed"
join() { local IFS=$1; shift; echo "$*"; }
[ $skip -gt 0 ] && echo "$skip skipped: $(join ';' "${skipped[@]}" | sed 's/;/; /g')"
[ $bypass -gt 0 ] && echo "!!! $bypass BYPASSED, NOT TESTED ($bypasswhy): $(join ',' "${bypassed[@]}" | sed 's/,/, /g')"
if [ $fail -gt 0 ]; then echo "Failed: $(join ',' "${failed[@]}" | sed 's/,/, /g')"; exit 1; fi
exit 0
