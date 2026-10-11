# 2026-10-10: carried bytes that spell a UTF-8 character are that character, as bash in a UTF-8 locale sees
# them: $'\xc3\xa9', $(printf '\xc3\xa9'), printf -v and $'\xc3'$'\xa9' are é (length 1, equal to é, a match
# in case, a file named café.txt, ${x^^} is É). They stayed two carried bytes: ${#x} was 2 and Windows got
# a file name hg saw as caf??.txt. Expected output: GNU bash via WSL.
{
x=$'\xc3\xa9'; echo "1 ${#x} $([[ $x == é ]] && echo eq || echo ne)"
x=$(printf '\xc3\xa9'); echo "2 ${#x} $([[ $x == é ]] && echo eq || echo ne)"
printf -v x '\xc3\xa9'; echo "3 ${#x} $([[ $x == é ]] && echo eq || echo ne)"
x=$'\xc3'$'\xa9'; echo "4 ${#x}"
x=$'\xc3'; y=$'\xa9'; z="$x$y"; echo "5 ${#z} $([[ $z == é ]] && echo eq || echo ne)"
x=$'\xff'; echo "6 ${#x}"; x=$'\xe9'; echo "7 ${#x}"; x="a"$'\xc3'"b"; echo "8 ${#x}"
x=$'\xe2\x9c\x93'; echo "9 ${#x} ${x:0:1}|"
case $'caf\xc3\xa9' in café) echo "10 case match";; *) echo "10 case no";; esac
d=$(mktemp -d); touch "$d/caf$(printf '\xc3\xa9').txt"; touch "$d/"$'m\xc3\xa9'; ls "$d" | od -An -c | tr -s ' '; [ -e "$d/café.txt" ] && echo "11 exists by literal name"; rm -rf "$d"
x=$'\xc3\xa9'; echo "12 $(printf '%s' "${x^^}" | od -An -tx1)"
} 2>&1
