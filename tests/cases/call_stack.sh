# 2026-10-10: FUNCNAME, BASH_SOURCE and BASH_LINENO are one frame stack, as in bash: a function call
# pushes (its name, the file it was defined in, the call line), `source` pushes ("source", the file,
# the line), a script file has a bottom frame ("main", the script, 0); -c and stdin have none, and a
# function defined there is from "environment" / "main". FUNCNAME exists only while a function runs.
# The scripts run by relative name from a temp dir, so the names are the same in every shell.
# Expected output: GNU bash via WSL.
d=$(mktemp -d) && cd "$d" || exit 1
cat > lib.sh <<'EOF'
lf() { echo "lf: S=[${BASH_SOURCE[*]}] F=[${FUNCNAME[*]}] L=[${BASH_LINENO[*]}]"; }
echo "src: S=[${BASH_SOURCE[*]}] F=[${FUNCNAME[*]}] L=[${BASH_LINENO[*]}] set=${FUNCNAME+y}"
EOF
cat > main.sh <<'EOF'
echo "top: S=[${BASH_SOURCE[*]}] F=[${FUNCNAME[*]}] L=[${BASH_LINENO[*]}] set=${FUNCNAME+y}"
. ./lib.sh
echo "after: S=[${BASH_SOURCE[*]}] L=[${BASH_LINENO[*]}]"
lf
g() {
  lf
}
g
h() { . ./lib.sh; }
h
k() { echo "k: S=[${BASH_SOURCE[*]}] F=[${FUNCNAME[*]}] L=[${BASH_LINENO[*]}]"; }
( k )
x=$(k); echo "$x"
echo "end: S=[${BASH_SOURCE[*]}] set=${FUNCNAME+y}"
EOF
printf '#!/bin/bash\necho "child: S=[${BASH_SOURCE[*]}] L=[${BASH_LINENO[*]}] set=${FUNCNAME+y}"\n' > child.sh
chmod +x child.sh
"$BASH" main.sh
./child.sh
"$BASH" -c 'f() { echo "c: S=[${BASH_SOURCE[*]}] F=[${FUNCNAME[*]}] L=[${BASH_LINENO[*]}]"; }; echo "c top: #S=${#BASH_SOURCE[@]} set=${FUNCNAME+y}"; f'
echo 'f() { echo "stdin: S=[${BASH_SOURCE[*]}] F=[${FUNCNAME[*]}]"; }; f' | "$BASH"
cd / && rm -rf "$d"
