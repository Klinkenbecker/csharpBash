# 2026-10-11: three options that were accepted and silently ignored now act as in bash 5.1.
# -t / onecmd: the shell's own input (a script, -c, stdin) runs ONE command, a whole line or compound
# command, then the shell exits with its status; `set -t` is itself that command. -k / keyword: a NAME=value
# word anywhere after the command name is an assignment for the command, not an argument (declare and
# export lose theirs too). -p / privileged: BASH_ENV is not read, `p` is in $-. And `s` now comes last in $-
# (`hBs`; it stood before `u`). Expected output: GNU bash via WSL.
d=$(mktemp -d) && cd "$d" || exit 1
printf 'echo s1\necho s2\n' > two.sh
printf 'echo t1; echo t1b\necho t2\n' > line.sh
printf 'echo before\nset -t\necho after-set\necho never\n' > mid.sh
printf 'if true; then\necho in-if\nfi\necho after-if\n' > comp.sh
printf 'echo SOURCED-BASH_ENV\n' > envfile
echo "== -t"
"$BASH" -t two.sh; echo "rc=$?"
"$BASH" -t line.sh; echo "rc=$?"
"$BASH" mid.sh; echo "rc=$?"
"$BASH" -t -c 'echo c1; echo c2'; echo "rc=$?"
printf 'echo i1\necho i2\n' | "$BASH" -t; echo "rc=$?"
"$BASH" -t -c 'exit 3'; echo "rc=$?"
"$BASH" -t comp.sh; echo "rc=$?"
echo "== -k"
"$BASH" -c 'set -k; printenv KV KV=hello; echo "arg kept: $(echo a KV=x b)"'
"$BASH" -c 'echo a KV=x b'
"$BASH" -c 'set -k; f() { echo "f sees ${KF-unset} args: $*"; }; f one KF=val two'
"$BASH" -c 'set -k; declare D=1 > /dev/null; echo "D=[$D]"; export E=2 Z=3 > /dev/null; echo "E=[$E] Z=[$Z]"'
"$BASH" -c 'set -k; A=pre; printenv A A+=post'
echo "== -p"
BASH_ENV=./envfile "$BASH" -c 'echo body'
BASH_ENV=./envfile "$BASH" -p -c 'echo body-p'
echo "== \$- and set -o"
"$BASH" -kp -c 'echo $-'
echo 'echo $-' | "$BASH" -kps
"$BASH" -c 'set -t; echo $-'
echo 'echo $-' | "$BASH" -s
"$BASH" -c 'set -k -p; set -o | grep -E "^(keyword|onecmd|privileged) "; set +k +p; echo "$-"'
cd / && rm -rf "$d"
