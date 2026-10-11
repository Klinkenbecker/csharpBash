# 2026-10-11: a script, `-c`, `source` and `eval` run one top-level command at a time, as bash reads them
# (DECISIONS 2026-10-11, the architect: C#Bash must match Claude's expectations of bash): the commands
# before a syntax error RUN, a lexing error (an unterminated quote) waits until it is reached, and nothing
# after an `exit` is parsed (an installer's payload). C#Bash parsed the whole text first and ran nothing.
# One line is one unit (`echo same; if then` runs nothing, in both). `bash -n` still checks the whole file
# and runs nothing. Inner scripts by relative name from a temp dir, so both shells print `x.sh: line N:`.
# eval's status and line are phase 4's (only its output is checked here). Expected output: GNU bash via WSL.
d=$(mktemp -d) && cd "$d" || exit 1
printf 'echo before\nif then\necho after\n' > a.sh
printf 'echo start\nrm -f marker; touch marker\nfi\n' > b.sh
printf 'echo s1\nif then\n' > bad.sh
printf 'echo installing\nexit 0\n__PAYLOAD__\n)(*& not ( shell\n"\n' > c.sh
printf 'echo q1\necho "unterminated\necho q2\n' > e.sh
printf 'echo h1\ncat <<EOF\nbody line\n' > h.sh
printf 'f() { echo fn; }\nf\nif then\n' > f.sh
printf 'echo same; if then\n' > g.sh
printf 'echo x\nexit 3\nfi\n' > x.sh
printf '(echo p) fi\n' > p.sh
printf 'echo q; )\n' > q.sh
{ printf 'echo installing\nexit 0\n'; head -c 300000 /dev/urandom; } > big.sh
printf 'echo c1 &&\necho c2\necho s;\nif true; then echo in; fi\nif then\n' > m.sh
{
for s in a b c e h f g x p q m big; do echo "== $s.sh"; "$BASH" $s.sh; echo "rc=$?"; done
echo "== b.sh made marker: $([ -e marker ] && echo yes || echo no)"
echo "== source"; source ./bad.sh; echo "after source rc=$?"
echo "== eval"; eval $'echo e1\nif then' 2>/dev/null; echo "eval ran its first line"
echo "== -c multi-line"; "$BASH" -c $'echo l1\nif then' cname; echo "rc=$?"
echo "== -n checks the whole file and runs nothing"; "$BASH" -n a.sh; echo "rc=$?"; "$BASH" -n c.sh; echo "rc=$?"
echo "== eval syntax-error statuses (2; an unclosed array literal 1)"
t() { eval "$1" 2>/dev/null; echo "$2 rc=$?"; }
t "if then" token; t fi stray-fi; t "x=(" eof-array; t "if true" eof-if; t "echo \"abc" eof-quote; t "a &&" eof-and; t "[[ -f ]]" cond
} 2>&1
cd / && rm -rf "$d"
