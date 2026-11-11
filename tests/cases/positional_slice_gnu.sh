# 2026-11-09 (git-b3): a quoted slice of the positional parameters, "${@:2}", was ONE word ([b c] where bash gives
# [b][c]); `local x=("${@:2}")` had one element. A quoted @ slice is one field per element, as "$@" and as an array's
# slice; an element holding a space stays whole. Expected: GNU bash via WSL.
f() {
	printf '[%s]' "${@:2}"; echo
	printf '[%s]' "${@:1:2}"; echo
	printf '[%s]' "${@: -1}"; echo
	printf '[%s]' "${*:2}"; echo
	printf '[%s]' ${@:2}; echo
	local x=("${@:2}"); echo "array of ${#x[@]}"
	set -- "${@:2}"; echo "set -- gives $#: [$1]"
	echo "past the end: $(g "${@:9}")"
}
g() { echo $#; }
f a "b c" d
h() { printf '[%s]' "${@:-default}"; echo; }
h
a=("p q" r s)
printf '[%s]' "${a[@]:1}"; echo
printf '[%s]' "${a[@]:0:2}"; echo
printf '[%s]' "${a[@]: -1}"; echo
printf '[%s]' "${a[*]:1}"; echo
