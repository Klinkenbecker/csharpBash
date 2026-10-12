# 2026-10-11: DEBUG and RETURN traps run as in bash 5.1 (they were accepted and never ran), `$BASH_COMMAND`
# exists (it was empty, in ERR traps too), -T (functrace) extends them, and a trap's `$LINENO` is the line it
# fires for (an ERR trap said line 1). DEBUG: before each simple command, [[ ]], (( )), each `for` turn, an
# arithmetic for's parts, a case, each pipeline element; not inside functions, ( ), $( ) or stages without
# -T. RETURN: after `source` and after a function that set it, or every function with -T. Each probe is an
# inner script run by relative name from a temp dir. Expected output: GNU bash via WSL.
d=$(mktemp -d) && cd "$d" || exit 1
cat > d1.sh <<'EOS'
trap 'echo "D[$LINENO] $BASH_COMMAND"' DEBUG
x=1
echo "x is $x"
[[ $x == 1 ]] && echo yes
(( x++ ))
for i in a b; do echo "i=$i"; done
case $x in 2) echo two;; esac
f() { echo in-f; }
f
echo a | cat
y=$(echo sub)
( echo paren )
if true; then echo if-body; fi
while false; do :; done
trap - DEBUG
echo untraced
EOS
cat > d2.sh <<'EOS'
trap 'echo "D $BASH_COMMAND"' DEBUG
set -T
f() { echo in-f; g; }
g() { echo in-g; }
f
y=$(echo sub)
( echo paren )
echo a | cat
EOS
cat > d3.sh <<'EOS'
trap 'echo "D $BASH_COMMAND"; false' DEBUG
echo one
echo "two rc=$?"
f() { echo in-f; }
declare -t f
f
trap 'echo trap-cmd' DEBUG
echo three
trap -p DEBUG
EOS
cat > d4.sh <<'EOS'
trap 'echo "D<$BASH_COMMAND>"' DEBUG
for (( i=0; i<2; i++ )); do :; done
echo   spaced    out
echo a >/dev/null
echo b 2>&1 >/dev/null
! true
cat <<EOT
hd
EOT
x=1 y=2
echo "q" 'r' $'s'
time true
f() { :; }
echo $((1+2)) ${x:-d}
EOS
cat > r1.sh <<'EOS'
trap 'echo "RET from ${FUNCNAME[0]:-top} rc=$?"' RETURN
f() { echo in-f; return 3; }
f; echo "after f $?"
printf 'echo in-sourced\n' > src.sh
source ./src.sh; echo "after source"
g() { trap 'echo "RET-g"' RETURN; echo in-g; }
g; echo after-g
h() { echo in-h; }
h
EOS
cat > r2.sh <<'EOS'
set -T
trap 'echo "RET ${FUNCNAME[0]}"' RETURN
f() { echo in-f; }
f
EOS
cat > e1.sh <<'EOS'
trap 'echo "ERR at $LINENO: $BASH_COMMAND"' ERR
echo ok
false
trap 'echo "EXIT at $LINENO"' EXIT
echo last
EOS
cat > rl.sh <<'EOS'
set -T
trap 'echo "RET at $LINENO"' RETURN
f() {
  echo in-f
  :
}
f
printf "echo s\n" > s2.sh
source ./s2.sh
EOS
cat > ft.sh <<'EOS'
trap 'echo "D $BASH_COMMAND"' DEBUG
f() { echo in-f; }
g() { echo in-g; }
declare -ft f; echo "rc=$?"
f
g
declare +ft f
f
declare -f +t f
f
declare -ft nosuch; echo "missing rc=$?"
EOS
# declare -ft NAME: the function is traced as under -T; declare -f +t NAME clears it; in declare +ft the t is a variable's
for s in d1 d2 d3 d4 r1 r2 e1 rl ft; do echo "===== $s"; "$BASH" $s.sh 2>&1; done
cd / && rm -rf "$d"
