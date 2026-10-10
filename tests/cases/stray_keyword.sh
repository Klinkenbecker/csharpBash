# 2026-10-08: a closing reserved word with no construct open (fi, done, esac, then, do, else, elif)
# is a syntax error, status 2, and `bash -n` reports it; it ran as a command (127, nothing for -n).
# Quoted, it is an ordinary command name. Messages differ in form between the shells, so each run
# reports E(token) / NF(name) and its status. Expected output: GNU bash via WSL.
d=$(mktemp -d) && cd "$d" || exit 1
t() {
	"$BASH" "$@" > o 2> e; local rc=$?
	local m nf
	m=$(grep -o "unexpected token .[^']*'" e | head -1 | sed -E "s/unexpected token .(.*)'/E(\1)/")
	nf=$(grep -o '[^ :]*: command not found' e | head -1)
	echo "$(tr '\n' ' ' < o)${m:+$m }${nf:+$nf }rc=$rc"
}
for w in fi done esac then do else elif; do t -c "$w"; done
t -c 'echo a; fi'
t -c 'f() { echo f; }; f; done'
printf 'fi\n' > s.sh; t -n s.sh; t s.sh
t -c 'eval fi; echo after'
t -c "'fi'"
t -c 'if true; then echo in; fi; echo ok'
t -c 'for i in 1; do echo $i; done'
t -c 'case x in x) echo x;; esac'
cd / && rm -rf "$d"
