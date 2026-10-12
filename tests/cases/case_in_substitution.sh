# 2026-10-11: a `case` inside `$( )` (or `"$( )"`, or backquotes): its patterns' `)` do not close the
# substitution, as bash 5.1 reads it (parse_comsub). C#Bash ended the substitution at the first pattern's `)`,
# so `x=$(case $1 in a) …;; esac)` was a syntax error that lost the whole line. `case` is a keyword only where
# a command starts (`$(echo case in; …)` is just words). Expected output: GNU bash via WSL.
x=$(case a in a) echo yes;; *) echo no;; esac); echo "1 [$x]"
P=abc
echo "2 $(case "$P" in *"b"*) echo has-b;; *) echo no-b;; esac)"
y=$(case $P in (abc) echo paren-form;; esac); echo "3 [$y]"
z=`case a in a) echo bq;; esac`; echo "4 [$z]"
echo "5 $(case x in
  x) echo multi;;
esac)"
w=$(case b in a) echo A;; b) (echo nested-subshell);; esac); echo "6 [$w]"
v=$(case q in q) echo "$(case r in r) echo inner;; esac)";; esac); echo "7 [$v]"
u=$(echo case in; echo x); echo "8 [$u]"
t=$(for c in a; do case $c in a) echo in-loop;; esac; done); echo "9 [$t]"
s=$(if true; then case z in z) echo after-then;; esac; fi); echo "10 [$s]"
r="$(echo case in x; case k in k) echo dq;; esac)"; echo "11 [$r]"
q=$(case "m" in "m") echo quoted-pat;; esac); echo "12 [$q]"
# a bare `$name` in `"$( )"` hung the text scan of the first version of this fix (never shipped)
p="$(echo $P a$P $1 $$x)"; echo "13 [${p%% *}]"
echo "14 $(case $P in abc) echo $P-dq;; esac)"
