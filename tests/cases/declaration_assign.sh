# 2026-10-08: an assignment-shaped argument of a declaration builtin (local declare typeset export
# readonly) expands like an assignment: one field, no splitting, no globbing. `local a=$1` with
# "x y" set a=x and declared a second variable y. Expected output: GNU bash via WSL.
f() {
	local a=$1 b=$2; echo "local:    [$a] [$b]"
	declare c=$1; echo "declare:  [$c]"
	typeset d=$1; echo "typeset:  [$d]"
	export e=$1; echo "export:   [$e]"
	"$BASH" -c 'echo "child:    [$e]"'
	local h=$3; echo "glob val: [$h]"
	local i=$1 j; echo "mixed:    [$i]"
	local k=pre; local k+=$1; echo "append:   [$k]"
	local "m=$1"; echo "quoted:   [$m]"
	local -a n[1]=$1; echo "indexed:  [${n[1]}]"
	local -a arr=($1); echo "array:    [${#arr[@]}] [${arr[0]}]"
	local $4; echo "name var: [${p-unset}] [${q-unset}]"
	local r=$5; echo "empty:    [$r]"
	local -x s=$1; echo "flag:     [$s]"
}
f "x y" "-P @D" "*" "p=1 q=2" ""
readonly g=$(echo "u v"); echo "readonly: [$g]"
cmd=local; g2() { $cmd z=$1; echo "via var:  [$z]"; }; g2 "x y"
declare -f f | grep -c 'local a=\$1 b=\$2'
