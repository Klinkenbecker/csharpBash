# 2026-10-08: the word of ${x:-word} / ${x:+word} is expanded in the ${ }'s own context. Inside
# "..." it is double-quoted text ("${v:+$v }" kept its trailing space only now); unquoted, its blanks
# separate fields and its quoted parts stay quoted (${v:+-o "$w"} is two fields). Before, the word
# was expanded alone and re-joined with single spaces. Expected output: GNU bash via WSL.
HOME=/home/u; v=x; w='p  q'; unset u
d=$(mktemp -d) && cd "$d" || exit 1
touch f1 f2
p() { printf '[%s]' "$@"; echo; }
echo "inside double quotes:"
echo "1  [${v:+$v }]"
echo "2  [${u:-a  b}]"
echo "3  [${u:-  lead}]"
echo "4  [${u:-"a  b"}]"
echo "5  [${u:-'a  b'}]"
echo "6  [${u:-*}]"
echo "7  [${u:-$w}]"
echo "8  [${u:-$(echo a  b)}]"
echo "9  [${u:-\"q\"}]"
echo "10 [${u:-a\ b}]"
echo "11 [${u:-~}]"
echo "12 [${v:+ --flag $v}]"
echo "13 [${v:+"$w"}]"
echo "14 [${u:-${v:+<$v >}}]"
echo "15 [${u:-$(echo "a  b")}]"
echo "16 [${u:-`echo hi`}]"
echo "17 [${v:-dflt}] [${v:+alt}] [${u+set}] [${u-unset}]"
echo "unquoted:"
set -- ${u:-a "b c" d}; echo "18 $# [$2]"
p ${u:-f*}
p ${u:-"f*"}
set -- ${u:-$w}; echo "21 $#"
p ${v:+-o "$w"}
p ${v:+"--name=$w"}
p ${u:-~/x}
p ${u:-a|b}
set -- "a b" c; p ${1+"$@"}
set -- ${u:-}; echo "27 $#"
set -- "${u:-}"; echo "28 $#"
e=""; p "[${e-dflt}]" "[${e:-dflt}]"
( IFS=:; p ${u:-a b} )
p pre${u:- a b }post
echo "string contexts:"
x=${u:-$w}; echo "32 [$x]"
x=${u:-'a  b'}; echo "33 [$x]"
x="${u:-'a'}"; echo "34 [$x]"
: ${u:=$w}; echo "35 [$u]"; unset u
x=${v:+"$w" z}; echo "36 [$x]"
cd / && rm -rf "$d"
