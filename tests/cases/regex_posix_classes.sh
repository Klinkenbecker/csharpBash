# 2026-11-07 (guy-e3's review): `[[ =~ ]]` reads POSIX bracket expressions as glibc's ERE does. The pattern went to .NET
# untranslated, so `[[:digit:]]` was the set `[:digt` followed by `]`: `[[ 5 =~ [[:digit:]] ]]` failed and
# `[[ 'a]' =~ ^[[:alpha:]]$ ]]` matched, silently (a status line matching JSON with `[[:space:]]*` printed its defaults).
# Expected = GNU bash 5.1 via WSL.
r() { if eval "$2"; then echo "match    $1"; else echo "no ($?)   $1"; fi; }
for c in alpha digit alnum upper lower space blank punct xdigit print graph cntrl; do
  case $c in alpha) y=x n=5;; digit) y=0 n=x;; alnum) y=5 n=-;; upper) y=Q n=q;; lower) y=q n=Q;; space) y=' ' n=x;;
             blank) y=' ' n=x;; punct) y='!' n=a;; xdigit) y=f n=g;; print) y='~' n=$'\x01';; graph) y='~' n=' ';;
             cntrl) y=$'\x01' n=a;; esac
  r "[[:$c:]] yes" "[[ \$y =~ ^[[:$c:]]\$ ]]"
  r "[[:$c:]] no" "[[ \$n =~ ^[[:$c:]]\$ ]]"
done
r "'a]' is not ^[[:alpha:]]\$" "[[ 'a]' =~ ^[[:alpha:]]\$ ]]"
r "':]' is not ^[[:alpha:]]\$" "[[ ':]' =~ ^[[:alpha:]]\$ ]]"
re='^[[:digit:]]+$'; r "through \$re" '[[ 123 =~ $re ]]'
r "negated [^[:digit:]]" "[[ x =~ ^[^[:digit:]]\$ ]]"
r "negated, a digit" "[[ 5 =~ ^[^[:digit:]]\$ ]]"
r "class and chars [[:digit:]x]" "[[ x =~ ^[[:digit:]x]+\$ && 5x =~ ^[[:digit:]x]+\$ ]]"
r "two classes [[:upper:][:digit:]]" "[[ Q5 =~ ^[[:upper:][:digit:]]+\$ ]]"
r "leading ] then a class" "[[ ']5]' =~ ^[][:digit:]]+\$ ]]"
r "equivalence [[=a=]]" "[[ a =~ ^[[=a=]]\$ ]]"
r "equivalence, not b" "[[ b =~ ^[[=a=]]\$ ]]"
r "collating [[.a.]]" "[[ a =~ ^[[.a.]]\$ ]]"
r "collating [[.-.]] in a set" "[[ - =~ ^[[.-.]x]\$ ]]"
r "quoted class is literal" "[[ '[[:digit:]]' =~ \"[[:digit:]]\" ]]"
r "quoted class, not a digit" "[[ 5 =~ \"[[:digit:]]\" ]]"
r "unknown class [[:foo:]]" "{ [[ a =~ [[:foo:]] ]]; } 2>/dev/null"
[[ ab12cd =~ ([[:alpha:]]+)([[:digit:]]+) ]] && echo "REMATCH: ${BASH_REMATCH[0]} ${BASH_REMATCH[1]} ${BASH_REMATCH[2]}"
[[ '"a":  17,' =~ \"a\":[[:space:]]*([[:digit:]]+) ]] && echo "status line: ${BASH_REMATCH[1]}"
shopt -s nocasematch; r "nocasematch [[:upper:]] on q" "[[ q =~ ^[[:upper:]]\$ ]]"; shopt -u nocasematch
r "range still works [0-9]" "[[ 5 =~ ^[0-9]\$ ]]"
r "] first [-]a] on ]" "[[ ']' =~ ^[]a]\$ ]]"
# 2026-11-07: quoted text inside a set goes over as written, as bash 5.1 (`["[:digit:]"]` is the class, `["^a"]` a negated set);
# it was escaped into literals. Outside a set it stays literal.
r "5 =~ ^[\"[:digit:]\"]\$"   "[[ 5 =~ ^[\"[:digit:]\"]\$ ]]"
r "b =~ ^[\"a-c\"]\$"         "[[ b =~ ^[\"a-c\"]\$ ]]"
r "- =~ ^[\"a-c\"]\$"         "[[ - =~ ^[\"a-c\"]\$ ]]"
r "x =~ ^[\"x\"]\$"           "[[ x =~ ^[\"x\"]\$ ]]"
r "] =~ ^[x\"]\"]\$"          "[[ ']' =~ ^[x\"]\"]\$ ]]"
r "x] =~ ^[x\"]\"]\$"         "[[ 'x]' =~ ^[x\"]\"]\$ ]]"
r "\\ =~ ^[\"\\\\\"]\$"       "[[ '\\' =~ ^[\"\\\\\"]\$ ]]"
r "a =~ ^[^\"a\"]\$"          "[[ a =~ ^[^\"a\"]\$ ]]"
r "b =~ ^[\"^a\"]\$"          "[[ b =~ ^[\"^a\"]\$ ]]"
r "^ =~ ^[\"^a\"]\$"          "[[ '^' =~ ^[\"^a\"]\$ ]]"
r "5 =~ ^['[:digit:]']\$"     "[[ 5 =~ ^['[:digit:]']\$ ]]"
r ". =~ \".\" (outside)"      "[[ . =~ \".\" ]]"
r "x =~ \".\" (outside)"      "[[ x =~ \".\" ]]"
r "x =~ [\"x\"]+\"*\""        "[[ 'xx*' =~ ^[\"x\"]+\"*\"\$ ]]"
