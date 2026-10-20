# 2026-10-19 (red team F11): set -x prints what bash 5.1 prints: control characters in octal ($'a\001b'; also printf
# %q), the assignment `export`/`readonly` make (`declare -x` and `local` none), a subscript as written (`a[$i]=`),
# eval's commands one level deeper (`++`, nested `+++`), and the trace on BASH_XTRACEFD when set. Expected: GNU bash
# 5.1 via WSL, byte-identical.
d=$(mktemp -d) && cd "$d" || exit 1
exec 2>&1
set -x
v=$'a\001b'; w=$'\177'; t=$'x\ty'
export e1="$t"
readonly r1=ro
declare -x d1=dx
local_test() { local l1=lv; }; local_test
i=0; a[$i]=zero; a[i+1]=one; a["$i"]+=more
eval 'echo ev; eval "echo nested"'
f() { echo inf; }; eval f
set +x
printf '%q\n' "$v" "$w"
exec 3> trace
BASH_XTRACEFD=3
set -x
echo one
set +x
exec 3>&-
unset BASH_XTRACEFD
while IFS= read -r l; do echo "trace file: [$l]"; done < trace
cd / && rm -rf "$d"
