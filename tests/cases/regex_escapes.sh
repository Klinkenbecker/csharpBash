# 2026-11-07 (KNOWN_ISSUES.md's open list): in [[ =~ ]] a backslash-escaped character is literal, as in bash. The lexer
# kept the escape only for `* ? [ { } , ~ \ .`, so `^a\+b$` reached the regex as `^a+b$` (one or more a's):
# `[[ 'a+b' =~ ^a\+b$ ]]` was false and `[[ aab =~ ^a\+b$ ]]` true. Outside =~ nothing changes. Expected = GNU bash 5.1.
r() { if eval "$2"; then echo "match    $1"; else echo "no ($?)   $1"; fi; }
r "'a+b' =~ ^a\+b\$"     "[[ 'a+b' =~ ^a\\+b\$ ]]"
r "aab =~ ^a\+b\$"       "[[ aab =~ ^a\\+b\$ ]]"
r "'a(b' =~ a\(b"        "[[ 'a(b' =~ a\\(b ]]"
r "'a)b' =~ a\)b"        "[[ 'a)b' =~ a\\)b ]]"
r "'a|b' =~ ^a\|b\$"     "[[ 'a|b' =~ ^a\\|b\$ ]]"
r "b =~ ^a\|b\$"         "[[ b =~ ^a\\|b\$ ]]"
r "'a^b' =~ a\^b"        "[[ 'a^b' =~ a\\^b ]]"
r "'\$' =~ ^\\\$\$"      "[[ '\$' =~ ^\\\$\$ ]]"
r "'a d' =~ ^a\ d\$"     "[[ 'a d' =~ ^a\\ d\$ ]]"
r "ab =~ ^a\b\$"         "[[ ab =~ ^a\\b\$ ]]"
r "'a.b' =~ ^a\.b\$"     "[[ 'a.b' =~ ^a\\.b\$ ]]"
r "axb =~ ^a\.b\$"       "[[ axb =~ ^a\\.b\$ ]]"
r "'x{2}' =~ ^x\{2\}\$"  "[[ 'x{2}' =~ ^x\\{2\\}\$ ]]"
[[ 'a(b)c' =~ a\((.)\)c ]] && echo "REMATCH: ${BASH_REMATCH[0]} ${BASH_REMATCH[1]}"
re='^a\+b$'; r "through \$re (the backslash is in the value)" '[[ "a+b" =~ $re ]]'
# outside =~ an escape means what it did
echo \+ \( \) \| \^ \$x
case '+' in \+) echo "case \+ matches +";; esac
x=a+b; echo "${x/\+/-}"
