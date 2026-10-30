# 2026-10-30 (the architect: "fix the diff -y first"; esdk-a2's need): diff -y as GNU's (diffutils side.c): halves of
# 61 columns at the default width 130, the right one from column 64, the mark at 62 (`|` changed, `<` only left, `>`
# only right, `\`/`/` a changed pair where one line has no newline, `(` a common line under --left-column), padding as
# tabs while a tab stop fits then spaces (spaces only under -t), long lines cut; a change's lines pair first, then its
# remaining insertions, then deletions. It was refused ("not implemented"), so `r=\`diff -y A B | grep ...\`` was always
# empty and a check built on it could not fail. cat -A shows the tabs (^I) and line ends ($).
# Expected: GNU diffutils 3.8 via WSL.
cd "${TMPDIR:-${TEMP:-/tmp}}" || exit 1
d="diff-y-$$"; mkdir -p "$d" && cd "$d" || exit 1
printf 'one\ntwo\nthree\nfour\nfive\n' > a; printf 'one\nTWO\nthree\nfive\nsix\nseven\n' > b
printf 'x\t-Done\ny\n' > l; printf 'x\t-Dtwo\ny\n' > r
echo "--- a b"; diff -y a b | cat -A; echo "rc=${PIPESTATUS[0]}"
echo "--- tabs in the lines"; diff -y l r | cat -A
echo "--- esdk's check"; x=$(diff -y l r | grep "|	-D"); echo "[$x]" | cat -A
echo "--- -W 40"; diff -y -W 40 a b | cat -A
echo "--- --width=50"; diff -y --width=50 a b | cat -A
echo "--- --suppress-common-lines"; diff -y --suppress-common-lines a b | cat -A
echo "--- --left-column"; diff -y --left-column a b | cat -A
echo "--- -t"; diff -y -t l r | cat -A
echo "--- the same"; diff -y a a | cat -A; echo "rc=${PIPESTATUS[0]}"
echo "--- 2 deleted, 1 inserted"; printf 'p\nq\nr\ns\n' > c; printf 'p\nX\ns\n' > e; diff -y c e | cat -A
echo "--- 1 deleted, 2 inserted"; diff -y e c | cat -A
echo "--- a long line"; printf '%s\n' "$(printf 'L%.0s' $(seq 1 80))" > g; printf 'short\n' > h; diff -y g h | cat -A
echo "--- no newline at the end"; printf 'a\nb' > n1; printf 'a\nc\n' > n2; diff -y n1 n2 | cat -A; diff -y n2 n1 | cat -A
echo "--- an empty line"; printf 'a\n\nb\n' > m1; printf 'a\n\nc\n' > m2; diff -y m1 m2 | cat -A
echo "--- -i"; printf 'Hello\n' > i1; printf 'hello\n' > i2; diff -y -i i1 i2 | cat -A; echo "rc=${PIPESTATUS[0]}"
cd .. && rm -rf "$d"
