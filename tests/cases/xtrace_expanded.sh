# 2026-10-19 (red team F12): set -x shows compound commands as bash 5.1: `(( ))` and arithmetic `for` after their
# expansions (which run once: a `$( )` traces first), an empty for-part as `(( 1 ))`, `for i` as `for i in "$@"`, an
# empty [[ ]] operand as '', and a tilde in a [[ ]] pattern as quoted (`\/\h...`; it also matches literally now).
# Expected: GNU bash 5.1 via WSL, byte-identical.
exec 2>&1
y=5; HOME=/h/x
set -x
(( x = $y + 1 ))
(( x = $(echo 2) * 3 ))
for (( i=$y; i<$y+1; i++ )); do :; done
for (( ;; )); do break; done
f() { for i; do :; done; }; f a
[[ -z "" ]]
[[ -n $y ]]
[[ x == ~ ]]
[[ $y == 5 && "a b" != c ]]
set +x
c=$(mktemp); { set -x; for (( j=$(echo x >> "$c"; echo 0); j<1; j++ )); do :; done; (( $(echo y >> "$c"; echo 1) )); set +x; } 2>/dev/null
echo "under set -x each \$( ) ran $(wc -l < "$c") times in all (once each)"; rm -f "$c"
HOME='/h/[x]'; [[ '/h/[x]' == ~ ]] && echo "a tilde pattern matches literally"
