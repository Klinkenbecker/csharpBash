# 2026-10-17: PS4 is expanded for each trace line, as bash expands it: parameters, arithmetic and command
# substitution, quotes literal, `$?` and the trace itself untouched by the expansion. It printed raw
# (`+ $LINENO: echo hi`). And $EPOCHREALTIME has microseconds (it moved in millisecond steps).
# Expected: GNU bash 5.1 (WSL), byte-identical. Assignment traces, `++` levels and argument quoting are case
# `xtrace_fidelity` (fixed later the same day).
f() { echo in-f; }
v=val
exec 2>&1
PS4='+ $LINENO: '
set -x
echo hi
f
set +x
PS4='+${FUNCNAME[0]:+${FUNCNAME[0]}(): }'
set -x
f
set +x
PS4='[$((1+2))] '
set -x
echo arith
set +x
PS4='+ "q" $v \$v '
set -x
echo quote
set +x
PS4='+$(echo cs) '
set -x
echo cmdsub
set +x
PS4='+ $? '
set -x
false
echo after-false
set +x
PS4='+$(exit 7) '
set -x
false
echo "status=$?"
set +x
echo "== EPOCHREALTIME: six decimals, not three zeros"
n=0
for i in 1 2 3 4 5 6 7 8; do
	t=$EPOCHREALTIME
	[[ $t =~ ^[0-9]+\.[0-9]{6}$ ]] || echo "bad form: $t"
	[[ ${t: -3} != 000 ]] && n=$((n + 1))
done
[ "$n" -ge 1 ] && echo "microseconds: yes" || echo "microseconds: no"
