# Regressions from the 2026-09-05 defect report (session web-d6): nested $( ) inside $(( ))
# lost its spaces, and a backslash-newline that was a whole "word" became an empty argument.
# Items 2 and 3 of that report (quoted $(cmd arg), echo -e/printf) are guarded here as well.
# Expected output generated under Git Bash and verified identical.
f="${TMPDIR:-${TEMP:-/tmp}}/parser2-$$.txt"
printf 'l1\nl2\nl3\n' > "$f"

echo "--- nested substitution inside arithmetic"
n=$(( $(wc -l < "$f") + 1 )); echo "n=$n"
m=$(( $(printf '%s\n' 10 20 | tail -1) * 2 )); echo "m=$m"
echo "$(( $(echo 3) + $(echo 4) ))"
k=$(( ${#f} > 0 ? 1 : 0 )); echo "k=$k"
echo "$((1+2)) $(( 2 * (3 + 4) )) $(( 0x10 + 010 ))"

echo "--- backslash-newline continuation"
cat "$f" \
  | sed 's/l/L/' \
  | sort -r
grep -c L <<< "$(cat "$f" \
  | sed 's/l/L/')"
echo one \
  two \
     three
x=a\
b; echo "$x"
echo "quoted \
continuation"

echo "--- \$(cmd arg) inside double quotes"
echo "[$(echo hi)] [$(echo a b | wc -w)] [$(printf '%s-%s' x y)]"
y="$(echo hi there)"; echo "$y"

echo "--- echo -e and printf escapes"
echo -e 'a\nb'
printf 'a\nb\n'
printf '%s\n' 'x\ny'
echo 'lit\nlit'

rm -f "$f"

echo "--- 2026-09-05 second report: nested \$( ) anywhere inside \$(( )); unseparated subshell is a syntax error"
t=0; t=$(( t + $(echo 3) )); echo "t=$t"
x=$(( 1 + $(echo 3) )); echo "x=$x"
y=$(( $(echo 2) * $(echo 5) + $(( 1 + 1 )) )); echo "y=$y"
for i in 1 2; do t=$(( t + $(echo 10) )); done; echo "t=$t"
z=$(( (1 + 2) * $(echo 4) )); echo "z=$z"
"$BASH" -c 'echo x (y) z' 2>&1 | grep -c 'syntax error'
"$BASH" -c 'echo x (y) z' >/dev/null 2>&1; echo "rc=$?"
printf "%s\n" "double-quoted format"
printf "%s|%s\n" "a" "b"

echo "--- lone \$ is literal (2026-09-08): =~ anchored at \$, a\$ operands, \$\"locale\""
x=12; [[ $x =~ ^[0-9]+$ ]] && echo num
[[ a$ == a$ ]] && echo lit
echo "a$" $ 'b$' c$
[[ ab =~ (a)(b)$ ]] && echo "${BASH_REMATCH[2]}"
echo $"locale-quoted"

echo "--- # inside a word is not a comment (2026-09-08, installer-79): base#digits, a#b, \$x#y"
echo $((10#0011)) $((16#ff)) $((2#101)) $((8#17)) $((36#z))
( echo $((10#0010)) ) 2>&1 | head -1
x=$((10#0010)); echo "x=$x"
echo a#b c#d; echo x #comment
v=val; echo "$v#y" $v#y '#lit' "#q"
echo -n "  \$((10#0010)) -> "; ( echo $((10#0010)) ) 2>&1 | head -1
