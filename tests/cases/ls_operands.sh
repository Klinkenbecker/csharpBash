# 2026-10-10: ls gives each directory a heading whenever there is more than one operand, a failing one
# included (`ls dir missing` printed no `dir:`), or under -R; directory operands come in the listing's
# sort order (`ls plain 'a b'` lists `a b:` first). Expected output: GNU ls via WSL.
d=$(mktemp -d) && cd "$d" || exit 1
mkdir 'a b' plain zdir; touch 'a b/f1' plain/f2 zdir/f3 file
for args in "a\ b/ x" "plain x" "x plain" "plain" "a\ b/" "plain file" "file x" "plain a\ b" "x y" \
            "zdir plain a\ b" "-r zdir plain a\ b" "-d zdir plain" "-R plain" "zdir file plain x"; do
  echo "== ls $args"; eval "ls $args" 2>&1; echo "rc=$?"
done
cd / && rm -rf "$d"
