echo "ostype=$OSTYPE machtype=$MACHTYPE"
[ "$PPID" -gt 0 ] && echo ppid-ok
[ -n "$RANDOM" ] && [ "$RANDOM" -ge 0 ] && echo random-ok
[ "$UID" -ge 0 ] && [ "$EUID" = "$UID" ] && echo uid-ok
echo "versinfo=${BASH_VERSINFO[0]}.${BASH_VERSINFO[1]}"
echo "lineno=$LINENO"
false | true; echo "pipestatus=${PIPESTATUS[0]} ${PIPESTATUS[1]}"
sleep 0.1 & j=$!; [ "$j" -gt 0 ] && echo bang-ok; wait $j; echo "wait rc=$?"
echo "$-" | grep -q h && echo flags-ok
f() { echo "fn=${FUNCNAME[0]}"; }; f
echo "src=$(basename "${BASH_SOURCE[0]}")"
[ "$SHLVL" -ge 1 ] && echo shlvl-ok
[ "$SECONDS" -ge 0 ] && echo seconds-ok
echo "home-set=$([ -n "$HOME" ] && echo y)"
x=$(exit 3); echo "subst rc=$?"
x=$(false) || echo "assign-fail-branch"
