# 2026-10-30 (the "match GNU" item, found 2026-10-27): wc's words are runs of PRINTABLE characters (NUL, control and
# invalid bytes are neither words nor separators... as GNU decides below); -m does not count an invalid UTF-8 byte;
# -L is the display width (a tab to the next multiple of 8, a wide character 2, NUL and CR 0); one count of one
# file is printed unpadded, several counts from a pipe in width 7. Expected: GNU coreutils 8.32 via WSL, LC_ALL=C.UTF-8.
export LC_ALL=C.UTF-8
cd "${TMPDIR:-${TEMP:-/tmp}}" || exit 1
d="wc-gnu-$$"; mkdir -p "$d" && cd "$d" || exit 1
w() { printf '%-28s' "$1"; shift; printf "$1" | wc "${@:2}"; }

echo "--- -w"
w 'NULs' '\0\0\0\n' -w
w 'a NUL b' 'a\0b\n' -w
w 'a NUL space b' 'a\0 b\n' -w
w 'control chars' '\001\002 x\n' -w
w 'invalid bytes' '\377\376 ab\n' -w
w 'invalid between letters' 'a\377b c\n' -w
w 'tab and CR' 'a\tb\rc\n' -w
w 'form feed, vtab' 'a\fb\vc\n' -w
w 'no-break space U+00A0' 'a\302\240b\n' -w
w 'ideographic space U+3000' 'a\343\200\200b\n' -w
w 'CJK' '\344\270\255\346\226\207 x\n' -w

echo "--- -m"
w 'invalid bytes' '\377\376 ab\n' -m
w 'a 2-byte char' 'caf\303\251\n' -m
w 'a cut 3-byte char' 'a\344\270\n' -m
w 'NUL' 'a\0b\n' -m

echo "--- -L"
w 'tab' 'a\tb\n' -L
w 'tab at 8' '12345678\tx\n' -L
w 'CJK wide' '\344\270\255\346\226\207\n' -L
w 'NUL and CR' 'ab\0\rcd\n' -L
w 'combining accent' 'e\314\201\n' -L
w 'longest of lines' 'abc\nabcdef\nab\n' -L
w 'invalid byte' 'a\377b\n' -L

echo "--- padding"
printf 'one line of text\n' > f1; printf 'a\nb\nc\n' > f2
wc -l f1
wc -c f1
wc f1
wc -l f1 f2
wc f1 f2
printf 'x\ny\n' | wc -l
printf 'x\ny\n' | wc
printf 'x\ny\n' | wc -lw
wc -l < f1
wc < f1
wc <<< 'x y z'
wc /dev/null
wc missing f1 2>&1

echo "--- edges"
w 'emoji -m' '\360\237\230\200\n' -m
w 'emoji -L' '\360\237\230\200\n' -L
w 'emoji -w' 'a\360\237\230\200b\n' -w
w 'ZWSP -w' 'a\342\200\213b\n' -w
w 'ZWSP -L' 'a\342\200\213b\n' -L
w 'word joiner -w' 'a\342\201\240b\n' -w
w 'figure space -w' 'a\342\200\207b\n' -w
w 'figure space -L' 'a\342\200\207b\n' -L
w 'soft hyphen -L' 'a\302\255b\n' -L
w 'NEL U+0085 -w' 'a\302\205b\n' -w
w 'DEL -w' 'a\177b c\n' -w
w 'DEL -L' 'a\177b\n' -L
w 'fullwidth A -L' '\357\274\241\n' -L
w 'hangul -L' '\352\260\200\n' -L
w 'ideographic sp -L' '\343\200\200\n' -L
w 'vtab -L' 'ab\vcd\n' -L
w 'FF -L' 'abc\fd\n' -L
w 'no newline -L' 'abcde' -L
w 'all, mixed' 'a\tb\302\240c\n\344\270\255 \377d\n' -lwmcL
cd .. && rm -rf "$d"
