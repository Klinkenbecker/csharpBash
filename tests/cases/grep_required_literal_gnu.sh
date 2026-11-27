# 2026-11-24 (bugs list 5, facts-8a): grep -E with a bounded repeat (`.{0,300}"unsigned"...`) was ~135x slower than GNU:
# .NET's backtracking tried every start of every line. A line without a literal every match must contain is no
# longer given to the regex, as GNU skips by its "must" strings. These shapes would show a wrong literal: an optional
# or repeated char, alternation, groups, brackets holding ( or |, escapes, BRE intervals, -w/-x/-v/-c/-o/-F, several
# patterns, -i, a backreference. Expected = GNU via WSL.
d=$(mktemp -d) && cd "$d" || exit 1
printf '%s\n' ac abc abbc xyz yz x 'a.b' axb aab b cd abcd ababcd foo foobar 'a foo b' ABC abcabc '(x' 'y' \
	'k "q" z' 'k  "q"' 'abab' 'needle here' 'my needle' zz azz > in.txt
t() { printf '%s:' "$*"; grep "$@" in.txt | tr '\n' ' '; echo " rc=${PIPESTATUS[0]}"; }
t -E 'ab?c'
t -E 'x|yz'
t -E '(ab)*cd'
t 'a\{0,1\}bc'
t -E 'k.{0,3}"q"'
t -E '[(]x|y'
t -E '[|]'
t -E 'a\.b'
t -w foo
t -x foo
t -v -E '.{0,5}zz'
t -c -E 'ab+c'
t -o -E '.{0,4}needle.{0,4}'
t -F 'a.b'
t -e abc -e xyz
t -i 'ABC'
t '\(ab\)\1'
t -E 'a{2}b'
t -E 'ab+c'
t -E '^a.c$'
t -E 'b$'
t 'a*b'
cd / && rm -rf "$d"
