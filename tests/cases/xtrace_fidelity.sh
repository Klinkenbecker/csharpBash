# 2026-10-17: set -x as bash prints it: every assignment traced (bare, prefix before its command, compound as written);
# PS4's first character once more per level of command substitution (`++`, `+++`); words quoted by bash's rule
# ('a b', '', 'c'\''d', '$x', '*.nomatch'); PS4's prompt escapes decoded (\s, \v, \\). None of it was. Expected: GNU
# bash 5.1 (WSL), byte-identical.
exec 2>&1
f() { echo in-f; }
set -x
x=1
y=$(echo sub)
echo 'a b' "" c\'d '$x' $'tab\tx' plain *.nomatch
f
FOO=bar true
a=(1 "2 3")
(echo sub-shell)
: $(echo nested $(echo deeper))
x=$((1+2)) z=two
set +x
PS4='\s-\v> '; set -x; true; set +x
PS4='[\\$] '; set -x; true; set +x
