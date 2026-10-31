# 2026-10-31 (c-parser-ac's report): grep -P `\Q...\E`, PCRE's literal quoting, was refused: "Invalid pattern ...
# Unrecognized escape sequence \Q", exit 2 (.NET's regex has no \Q). Everything between \Q and \E is literal; an
# unterminated \Q quotes to the end of the pattern; a lone \E is ignored; `\\Q` is an escaped backslash, then Q; it
# works inside a class, with -i, -o, -c, \K and (?x) (a quoted space still counts). The report's own use: looking up
# an arbitrary key in a TSV index, `grep -P "^\Q$key\E\t"`.
# Not here, on purpose: -x / -w with an unterminated \Q (GNU grep 3.7 wraps the pattern as text, so the \Q swallows
# its `)`: "missing )"), and -P with several -e (GNU 3.7 refuses; C#Bash joins them).
# Expected: GNU grep 3.7 -P via WSL.
cd "$(mktemp -d)" || exit 1
m() { local s=$1; shift; printf '%s\n' "$s" | grep "$@" 2>&1; echo "rc=$?"; }
m 'a.b'     -P '^\Qa.b\E$'
m 'axb'     -P '^\Qa.b\E$'
m 'a.b'     -P '^a\.b$'
m 'a.b'     -P '^\Qa.b$'
m 'a.b$'    -P '^\Qa.b$'
m 'x]'      -P '[\Q]\E]'
m 'xa-y'    -oP '[\Qa-z\E]+'
m 'ab'      -P 'a\Eb'
m 'a\Qb'    -P 'a\\Qb'
m 'x\Q-y'   -P 'x\\Q.y'
m 'a b'     -P '(?x) a \Q \E b'
m 'ab'      -P '(?x) a \Q \E b'
m 'a\b'     -P '^\Qa\b\E$'
m 'foo.bar' -oP 'foo\K\Q.bar\E'
m 'A.B'     -iP '\Qa.b\E'
m 'a.b'     -cP '\Q\E^a'
m 'a.b'     -P '\Q\E'
key='k[1](x)*+?{2}|$^.#\d'
printf '%s\tv1\nk1\tv2\nk[1](x)\tv3\n' "$key" > idx.tsv
grep -P "^\Q$key\E\t" idx.tsv; echo "rc=$?"
grep -cP "^\Qk1\E\t" idx.tsv
rm -f idx.tsv
