# 2026-10-08: set -u (nounset) is enforced. It was accepted and never applied: expanding an unset
# variable printed nothing and the script went on. Each case runs in its own shell; messages are
# normalised (the prefixes differ: bash: line 1: / bash:). Also: the word of ${x:-word} is expanded
# only when selected (${v:-$(cmd)} ran cmd with v set). Expected output: GNU bash via WSL.
d=$(mktemp -d) && cd "$d" || exit 1
t() { printf '%-3s ' "$1"; "$BASH" -c "$2" 2>&1 | sed -E 's/^.*: ([^:]+: (unbound variable|no x))$/E(\1)/' | tr '\n' ' '; echo "rc=${PIPESTATUS[0]}"; }
t 1  'set -u; echo a; echo "[$x]"; echo after'
t 2  'set -u; echo "[${x-d}] [${x:-d}] [${x+a}] [${x:+a}]"'
t 3  'set -u; echo "[${x}]"'
t 4  'set -u; echo "[${#x}]"'
t 5  'set -u; echo "[$1]"'
t 6  'set -u; echo "[$@] [$*] [$#]"'
t 7  'set -u; a=(); echo "[${a[@]}] [${#a[@]}]"'
t 8  'set -u; echo "[${b[@]}]"'
t 9  'set -u; a=(x); echo "[${a[1]}]"'
t 10 'set -u; echo $((n + 1))'
t 11 'set -u; f() { local j; echo "[$j]"; }; f; echo after'
t 12 'set -u; x=1; echo "[$x]"; unset x; echo "[$x]"'
t 13 'set -u; ( echo "[$x]" ); echo "after rc=$?"'
t 14 'set -u; y=$(echo "[$x]"); echo "after rc=$? [$y]"'
t 15 'set -u; echo "${x:?no x}"; echo after'
t 16 'set +u; echo "[$x]"'
t 17 'set -o nounset; echo "[$x]"'
t 18 'set -u; [[ -v x ]] && echo set || echo unset'
t 19 'set -u; echo "[${x#a}]"'
t 20 'set -u; echo "[${x:0:1}]"'
t 21 'set -u; for i in $x; do :; done; echo after'
t 22 'set -u; cat <<EOF
$x
EOF
echo after'
t 23 'set -u; (( z > 0 )); echo "rc=$?"'
t 24 'set -u; let w=w+1; echo "[$w]"'
t 25 'set -u; declare -A m; echo "[${m[k]}]"'
t 26 'set -euo pipefail; echo "[$x]"; echo after'
t 27 'set -u; echo "[$HOME]" > /dev/null; echo ok'
t 28 'set -u; f() { echo "[${1:-none}]"; }; f'
t 29 'set -u; echo "[$x]" || echo "not reached"; echo after'
t 30 'set -u; f() { echo "[$x]"; }; f; echo after'
t 31 'set -u; echo "[${x=dflt}] [$x]"'
t 32 'set -u; [ -n "${x:-}" ] || echo "empty-ok"'
t 33 'set -u; arr=(a b); echo "[${arr[@]}] [${arr[0]}]"'
t 34 'set -u; echo "[$z]" | cat; echo "after rc=$?"'
t 35 'set -u; if [ "$x" = y ]; then :; fi; echo after'
t 36 'set -u; v=1; a=${v:-$undefined}; echo "[$a]"'
t 37 'v=1; x=${v:-$(touch side)}; [ -e side ] && echo ran || echo lazy'
t 38 'v=1; echo "[${v:+$(echo w)}] [${u:+$(touch side2)}]"; [ -e side2 ] && echo ran || echo lazy'
t 39 'set -u; echo "[${10}]"'
t 40 'set -u; a=(x); echo "[${#a[1]}]"'
printf 'set -u\necho a\necho "[$x]"\necho after\n' > nu1.sh; "$BASH" nu1.sh 2>&1 | sed -E 's/^.*: ([^:]+: unbound variable)$/E(\1)/' | tr '\n' ' '; echo "script rc=${PIPESTATUS[0]}"
# the -c exit status: 127, or 1 where set -e is in force (not inside a command followed by && or ||)
r() { "$BASH" -c "$1" 2>/dev/null; echo "$2 rc=$?"; }
r 'set -eu; echo $x' "exit: direct -eu"
r 'eval "set -u; echo \$x"' "exit: eval -u"
r 'eval "set -eu; echo \$x"' "exit: eval -eu"
r 'set -e; eval "set -u; echo \$x"' "exit: -e outside, eval -u"
r 'set -eu; f() { echo $x; }; f' "exit: function -eu"
r 'set -eu; echo ${x:?m}' "exit: -eu :?"
r 'set -eu; if true; then echo $x; fi' "exit: -eu in if"
r 'true && eval '"'"'set -euo pipefail; echo "[$undefined_v]"'"'"' < /dev/null && pwd -P > /dev/null' "exit: Claude Code wrapper shape"
cd / && rm -rf "$d"
