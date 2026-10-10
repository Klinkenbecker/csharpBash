# 2026-10-08: literal text is never field-split; only the results of expansions are. A word with
# a backslash-escaped space and a glob character came back as two words (`a\ b/x*` gave `a` and
# `b/x*`), an IFS character in literal text split the word, and so did a tilde whose home contains
# a space (POSIX: a tilde result is not split). Expected output: GNU bash via WSL.
d=$(mktemp -d) && cd "$d" || exit 1
mkdir "a b" && touch "a b/x1" "a b/x2"
p() { printf '[%s]' "$@"; echo; }
echo "escaped space + glob:"
p a\ b/*zz*
p a\ b/x*
p a\ b/?zz
p a\ b/[q]zz
p a\ b/[x]1
p no\ pe*
p a\ b/ a\ b/*zz*
echo "escaped space, no glob, and quoted forms (were right):"
p a\ b/x1
p "a b"/*zz*
p 'a b'/x*
echo "escaped space + brace, + a variable, in a for loop:"
p a\ {b,c}
v=x; p a\ b/$v*
for f in a\ b/x*; do p "$f"; done
echo "an IFS character in literal text:"
( IFS=:; v=1; p ab:c$v; p ab:c* )
echo "a tilde whose home has a space:"
mkdir "h o" && touch "h o/f1"
( HOME='h o'; p ~; p ~/f*; p ~/f1 )
echo "expansion results still split:"
w='p q'; p $w
w='a b'; p $w/x*
cd / && rm -rf "$d"
