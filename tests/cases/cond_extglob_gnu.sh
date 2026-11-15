# 2026-11-15 (KNOWN_ISSUES since 2026-11-07; the bugs list): bash's [[ == ]] reads its pattern with extglob on whatever
# the option says: `[[ a == @(a|b) ]]` was a syntax error ("unexpected token `('"), and a group from a variable was
# false. Not the left operand, not `case` (both stay errors in bash). Expected = GNU bash 5.1 via WSL.
t() { if eval "$1"; then echo "y: $1"; else echo "n: $1"; fi; }
t '[[ "a b" == @(a b) ]]'
t '[[ a == @("a"|b) ]]'
t 'x=a; [[ a == @($x|b) ]]'
t '[[ abab == *(ab) ]]'
t '[[ "" == +(a) ]]'
t '[[ foo.txt == !(*.c) ]]'
t '[[ foo.c == !(*.c) ]]'
t '[[ abc == a!(b)c ]]'
t '[[ ac == a!(b)c ]]'
t '[[ abbc == a!(b)c ]]'
t '[[ a == @(a|) ]]'
t '[[ "" == @(a|) ]]'
t '[[ aab == @(a|+(a)b) ]]'
t '[[ "a|b" == @(a\|b) ]]'
t '[[ "a|b" == @(a"|"b) ]]'
t '[[ x != !(x) ]]'
t '[[ "(" == @(\() ]]'
t '[[ a == @([ab]|c) ]]'
t '[[ ab == @(a)@(b) ]]'
t '[[ abc == @(a)* ]]'
t '[[ a.b == *.@(b|c) ]]'
t 'p="!(x)"; [[ y == $p ]]'
shopt -s nocasematch; [[ A == @(a|b) ]] && echo "y: nocase A @(a|b)" || echo "n: nocase A @(a|b)"; shopt -u nocasematch
[[ A == @(a|b) ]] && echo "y: case A @(a|b)" || echo "n: case A @(a|b)"
[[ a == "@(a|b)" ]] && echo "y: quoted" || echo "n: quoted"
q='a(b)'; [[ 'a(b)' == "$q" ]] && echo "y: quoted parens value" || echo "n: quoted parens value"
[[ ab == +(a|b) ]] && echo "y: +ab" || echo "n: +ab"
