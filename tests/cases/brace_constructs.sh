# 2026-10-10: every scanner of ${…}, $( ), $(( )), backquotes and quotes uses one matcher,
# Lexer.ConstructEnd (bash's rule: escapes skip, quotes / backquotes / $( ) / ${ } nest as units, only ${ opens
# a brace level). There were seven copies and ${…} broke in several. Each case runs in its own shell
# so one swallowing bug cannot hide the rest; then `{` / `}` as ordinary words after the command name.
# Expected output: GNU bash via WSL.
P='a=abc b=a c=zz n=1 i=1 k=key HOME=/h; arr=(p q r); declare -A m=([key]=V); unset u v'
r() { printf '%-3s ' "$1"; "$BASH" -c "$P; $2" 2>&1 | sed -E 's/^.*(unbound|bad substitution|unexpected|Unterminated|syntax).*/ERR/' | tr '\n' ' '; echo; }
r2() { r "$@"; }
r 1  'echo ${a#${b}} after'
r 2  'echo "${a#${b}}" after'
r 3  'cat <<EOF
${a#${b}} ${u:-${c}}
EOF
echo after'
r 4  'cat <<< ${a#${b}}; echo after'
r 5  'echo $(( ${#a} + ${n:-${i}} )) after'
r 6  'echo $(( ${u:-${v:-3}} + 1 )) after'
r 7  'echo ${arr[${i}]} ${arr[${#arr[@]}-1]} after'
r 8  'echo ${m[${k}]} after'
r 9  'case $a in ${b}*) echo match;; *) echo no;; esac; echo after'
r 10 '[[ $a == ${b}* ]] && echo match; [[ ${a#${b}} == bc ]] && echo m2; echo after'
r 11 'echo ${u:-${v:-${c:-x}}} "${u:-${v:-${c:-x}}}" after'
r 12 'echo $(echo ${a#${b}}) after'
r 13 'echo `echo ${a#${b}}` after'
r 14 'echo ${u:-"}"} ${u:-'"'"'}'"'"'} ${u:-\}} after'
r 15 'echo ${a//\}/x} ${a/b/\}} after'
r 16 'echo ${a}{1,2} after'
r 17 'x="${a#${b}}"; local_test() { local y=${a#${b}}; echo $x $y; }; local_test; echo after'
r 18 'echo "${c:+${u:-${b}}}" after'
r 19 'echo "${arr[@]:${n}:1}" after'
r 20 'f() { echo ${1:-${HOME}}; }; f; f z; echo after'
r 21 'echo ${a:${#b}:${#c}} after'
r 22 'echo "x${a#${b}}y" x${a#${b}}y after'
r 23 'echo "$(echo "${a#${b}}")" after'
r 24 'echo ${u:-$(echo ${b})} "${u:-$(echo "${b}")}" after'
r 25 'echo ${a:-{x}} ${u:-{x}} after'
r 26 'echo ${u:-a}b}c after'
r 27 'echo "${u:-a}b}c" after'
r 28 'x=${u-"${b}"}; echo "[$x]" after'
r 29 'echo ${#arr[${i}]} after'
r 30 'for x in ${a#${b}} ${c}; do echo -n "$x,"; done; echo after'
r2 31 'echo ${u:-"a\"b"} after'
r2 32 'echo ${u:-$(echo })} after'
r2 33 'echo ${u:-`echo }`} after'
r2 34 'echo "${u:-\}}" after'
r2 35 'echo ${u:-'"'"'a}b'"'"'} after'
r2 36 'echo "${u:-'"'"'a}b'"'"'}" after'
r2 37 'echo "${u:-"a}b"}" after'
r2 38 'echo "${u:-$(echo })}" after'
r2 39 'echo "${a//\}/x}" "${a/b/\}}" after'
r2 40 'echo "${a:-{x}}" "${u:-{x}}" after'
r2 41 'echo ${u:-a\\}b} after'
r2 42 'echo ${u:-$(( 1 + 2 ))} ${u:-${a:+$(echo })}} after'
echo }; echo a } b; echo }}; echo {; echo x{; { echo in; }; f() { echo f; }; f; echo "[$(echo })]"; echo }x {y; echo end
