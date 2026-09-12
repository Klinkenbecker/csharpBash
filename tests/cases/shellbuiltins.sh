# Shell builtins added for Claude Code compatibility (P1). Runs with cwd = tests/.
shopt -s nullglob; echo "[$(echo nomatch*.zzz)]"; shopt -u nullglob
shopt -q nullglob; echo "nullglob=$?"
shopt -s extglob 2>/dev/null; echo "extglob rc=$?"   # deliberately loud: unsupported (DECISIONS 2026-09-04 #2)
shopt -s expand_aliases; alias hi='echo hello'; hi world; unalias hi; alias hi 2>/dev/null; echo "alias rc=$?"
command -v cd; command -v nosuch_zz; echo "cv rc=$?"; command echo via-command
type -t echo; type -t if; f() { :; }; type -t f; type -t nosuch_zz; echo "type rc=$?"
builtin echo via-builtin
declare -i n=5; n+=2; echo "int=$n"; declare -p n
declare -A m=([k]=v); echo "assoc=${m[k]}"; declare -a arr=(x y z); echo "arr=${arr[1]} ${#arr[@]}"
readonly RO=1; (RO=2) 2>/dev/null; echo "ro=$RO"
g() { local v=inner; echo "$v"; }; v=outer; g; echo "$v"
declare -f g | head -1 | tr -d ' '
declare -F g
let x=3*4; echo "let=$x"; let 0; echo "let0 rc=$?"
pushd cases >/dev/null; basename "$PWD"; popd >/dev/null; basename "$PWD"
set -- a b c; shift; echo "$1 $#"; set -o | grep -c '^pipefail'
set -o pipefail; false | true; echo "pf=$?"; set +o pipefail
read -r a b <<< "1 2 3"; echo "read=$b|$a"
IFS=: read -r p q <<< "x:y"; echo "ifs=$q$p"
mapfile -t lines <<'EOF'
l1
l2
EOF
echo "mapfile=${#lines[@]} ${lines[0]}"
set -- -a -b val; while getopts "ab:" o; do echo "opt=$o:${OPTARG:-}"; done
printf '%-4s|%03d|%5.2f|%x|%q|%b\n' ab 7 3.14159 255 'a b' 'x\ty'
printf '%s-' 1 2 3; echo
printf -v pv 'v%02d' 7; echo "$pv"
echo -n no; echo newline; echo "tab\there"; echo -e 'x\ty'
[ -d cases ] && [ ! -f nosuch ] && [ 3 -gt 2 -a "a" = "a" ] && echo test-ok
[[ -d cases && ! -f nosuch && 3 -gt 2 && a == a ]] && echo dtest-ok
[[ abc == a* && abc != a ]] && echo pattern-ok
[[ "abc123" =~ ([a-z]+)([0-9]+) ]] && echo "re=${BASH_REMATCH[1]}-${BASH_REMATCH[2]}"
exec 3>cases/fd3_tmp.txt; echo to-fd3 >&3; exec 3>&-; cat cases/fd3_tmp.txt; rm cases/fd3_tmp.txt
nosuch_cmd_zz 2>/dev/null; echo "notfound rc=$?"
x=$(nosuch_cmd_zz 2>&1); echo "$x"
