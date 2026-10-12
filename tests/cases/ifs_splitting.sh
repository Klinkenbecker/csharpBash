# 2026-10-12: field splitting and `$*` joining follow IFS as bash 5.1 does. C#Bash took IFS='' as unset (so
# `IFS=; for f in $p` split a path at its spaces: daq-d1's glob test failed on it), treated every IFS
# character as white space (IFS=, lost the empty field in `a,,b`), joined unquoted `$@` with spaces whatever
# IFS held, and joined `$*` / `${a[*]}` with a space where bash uses IFS's first character (an assignment,
# `[[ ]]`, a here-string, `case`, `${*:2}`, `${!a[*]}`, `${!p*}`). Expected output: GNU bash via WSL.
p() { printf '[%s]' "$@"; echo " ($#)"; }
x="a b";       IFS='';      echo -n "1 ";  p $x
x="a,,b";      IFS=,;       echo -n "2 ";  p $x
x=",a,";       IFS=,;       echo -n "3 ";  p $x
x=" a , b ,, c "; IFS=' ,'; echo -n "4 ";  p $x
x=" ,a";       IFS=' ,';    echo -n "4b "; p $x
x="a , , b";   IFS=' ,';    echo -n "4c "; p $x
set -- "a b" c
IFS=,;  echo -n "5 ";  p $@
IFS=''; echo -n "6 ";  p $@
IFS=''; echo -n "7 ";  p "$*"
unset IFS; echo -n "8 "; p "$*"
IFS=,;  echo -n "9 ";  p $*
arr=("x y" z); IFS=,; echo -n "10 "; p ${arr[@]}
IFS=: read -r a b <<< "1::2"; echo "11 [$a][$b]"
x="a:b"; IFS=:; set -- $x; echo "12 $#"
IFS=,; x="1,2"; echo -n "13 "; p pre$x"post"
IFS=,; x="";    echo -n "14 "; p $x
IFS=,; x="a,";  echo -n "15 "; p $x
IFS=', '; x="a, b"; echo -n "16 "; p $x
IFS=$'\n'; x=$'a\n\nb'; echo -n "17 "; p $x
IFS=,; arr=("p,q" r); echo -n "18 "; p ${arr[*]}; echo -n "19 "; p "${arr[*]}"
IFS=''; arr=(u v); echo -n "20 "; p "${arr[*]}"; echo -n "21 "; p ${arr[*]}
IFS=,; set -- "a,b" "" c; echo -n "22 "; p $@; echo -n "23 "; p "$@"
unset IFS; x="  lead trail  "; echo -n "24 "; p $x
IFS=' '; x=$'a\tb'; echo -n "25 "; p $x
IFS=,; x=a,b; echo -n "26 "; p $(echo "$x")
IFS=,; for f in $x; do echo -n "27 [$f]"; done; echo
IFS=; y=$(printf 'l1\nl2'); echo -n "28 "; p $y
set -- a "" b
IFS='';  echo -n "29 "; p $@; echo -n "30 "; p $*; echo -n "31 "; p "$*"
IFS=,; set -- a b
x=$*; echo "32 [$x]"; x=$@; echo "33 [$x]"; x="$@"; echo "34 [$x]"; x=${*}; echo "35 [$x]"
[[ $* == a,b ]] && echo "36 [[ \$* ]] joins with ," || echo "36 no"
cat <<< $*; cat <<< "$*"; cat <<< $@
case $* in a,b) echo "37 case joins with ,";; *) echo "37 case: other";; esac
arr=(p q); x=${arr[*]}; echo "38 [$x]"; x=${arr[@]}; echo "39 [$x]"
IFS=''; x=$*; echo "40 [$x]"; x=$@; echo "41 [$x]"; cat <<< $*
unset IFS; x=$*; echo "42 [$x]"
IFS=,; echo "43 ${*}" "${@}"; echo "44 $*"
IFS=,; arr=(a b c); pfx_x=1; pfx_y=2; set -- p q r
echo "45 ${!arr[*]}"; echo "46 ${!arr[@]}"; echo "47 ${!pfx*}"; echo "48 ${!pfx@}"
echo "49 ${arr[*]:1}"; echo "50 ${*:2}"; echo "51 ${arr[*]/a/A}"; echo "52 ${arr[*]^^}"
x="${arr[*]:1}"; echo "53 [$x]"; x=${!arr[*]}; echo "54 [$x]"
IFS=; echo "55 ${arr[*]}" "${!arr[*]}" "$*"
d=$(mktemp -d); mkdir "$d/a b"; touch "$d/a b/x.md"; b="$d/a b"
f1() { local IFS= f; for f in $1; do echo "56 [${f#"$d"/}]"; done; }; f1 "$b/x.md"; f1 "$b/*.md"
rm -rf "$d"
