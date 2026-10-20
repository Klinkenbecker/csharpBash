# 2026-10-19 (red team, older): `export -f` reaches a child shell, as bash passes it (BASH_FUNC_name%%=() { ... }); it
# was accepted and did nothing ("command not found" in the child). A value that is not exactly one function
# definition is never run (Shellshock). Expected: GNU bash 5.1 via WSL, byte-identical.
d=$(mktemp -d) && cd "$d" || exit 1
f() { echo "f ran with $1"; }
g() { echo "g ran"; }
export -f f
"$BASH" -c 'f child'
"$BASH" -c 'g 2>/dev/null || echo "g is not exported"'
export -fn f
"$BASH" -c 'f again 2>/dev/null || echo "f is no longer exported"'
declare -fx g; "$BASH" -c 'g'
export -f nosuch 2>/dev/null; echo "export -f nosuch: rc=$?"
h() { local x=1; echo "h: $x $(echo sub) $#"; }; export -f h
printf 'h one two\n' > s.sh; "$BASH" s.sh
printf 'h three\n' | "$BASH"
env | grep -c '^BASH_FUNC_h%%=() {'
unset -f h; env | grep -c '^BASH_FUNC_h%%' ; "$BASH" -c 'h 2>/dev/null || echo "h is gone"'
env 'BASH_FUNC_x%%=() { echo x; }; echo INJECTED' "$BASH" -c 'type -t x || echo "x is not defined"' 2>/dev/null
env 'BASH_FUNC_y%%=() { echo "y ran"; }' "$BASH" -c 'y; declare -F | grep -c "declare -fx y"'
k() { :; }; export -f k; declare -f k; declare -F k; declare -F | grep ' k$'; export -fn k; declare -F | grep ' k$'
( export -f k ); declare -F | grep ' k$'; export -f k; ( export -fn k ); declare -F | grep ' k$'
cd / && rm -rf "$d"
