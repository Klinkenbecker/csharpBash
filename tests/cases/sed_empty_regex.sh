# 2026-10-28: sed's empty regex `//` is the last regex EXECUTED (an address or an `s`; across files too), as GNU. It
# was refused always in `s//x/` and, as an address, matched every line (Regex("")). With no regex before it in the
# script: "no previous regular expression" (compile time); with one in the script that has not run yet: the same, at run
# time, rc 1; a modifier on an empty regex is refused. Error lines have GNU's character position normalised (C#Bash
# prints char 0 for every script error: a separate difference). Expected: GNU sed 4.8 via WSL.
cd "${TMPDIR:-${TEMP:-/tmp}}" || exit 1
d="sed-empty-$$"; mkdir -p "$d" && cd "$d" || exit 1
n() { sed 's/char [0-9]*:/char N:/'; }
printf 'foo\nbar\n' | sed -n '/foo/{//p}'
printf 'foo\nbar foo\n' | sed -n '/foo/!d;s//X/p'
printf 'abc\n' | sed -E 's/(b)/[\1]/;s//<\1>/'
printf 'xx\n' | sed 's/x/y/;s//z/g'
printf 'one\n' | sed '/o/s//0/;s//O/'
printf 'aaa\n' | sed 's/a/b/;s//c/'
printf 'a1\n' > f1; printf 'a2\n' > f2; sed -s '1{/a/!d};s//X/' f1 f2
printf 'k1\nk2\n' | sed -n '/k/{s//K/;p}'
printf 'q\n' | sed 's//x/' 2>&1 | n; echo "rc=${PIPESTATUS[1]}"
printf 'q\n' | sed -n '//p' 2>&1 | n; echo "rc=${PIPESTATUS[1]}"
printf 'a\nb\n' | sed '2s/a/b/;s//c/' 2>&1 | n; echo "rc=${PIPESTATUS[1]}"
printf 'aA\n' | sed 's/a/x/;s//y/I' 2>&1 | n; echo "rc=${PIPESTATUS[1]}"
printf 'aA\n' | sed -n '/a/p;//Ip' 2>&1 | n; echo "rc=${PIPESTATUS[1]}"
printf 'z\n' > g1; sed -i '2s/z/y/;s//x/' g1 2>&1 | n; echo "-i untouched: $(cat g1)"
cd .. && rm -rf "$d"
