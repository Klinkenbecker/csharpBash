# 2026-10-19 (red team F2): `read -u N` leaves the descriptor's offset just after what it used, as bash does on a file,
# so the next reader or writer of N starts there. C#Bash kept a reader per descriptor that read ahead: `cat <&8`
# after it printed nothing, `head -n1 <&8` took the rest, `echo X >&8` landed at the end. A pipe's readers share one
# buffer. Expected: GNU bash 5.1 (WSL), byte-identical.
d=$(mktemp -d) && cd "$d" || exit 1
printf 'l1\nl2\nl3\n' > two
exec 8<> two; read -u 8 a; echo "a=$a cat: $(cat <&8 | tr '\n' ' ')"; exec 8<&-
exec 8<> two; read -u 8 a; head -n1 <&8; read -u 8 c; echo "c=$c"; exec 8<&-
exec 8< two; read -u 8 a; echo "rest: $(cat <&8 | tr '\n' ' ')"; exec 8<&-
printf 'abc\ndef\nghi\n' > rw; exec 8<> rw; read -u 8 x; echo X >&8; exec 8>&-; tr '\n' '|' < rw; echo
exec 8< two; read x <&8; read -u 8 y; echo "x=$x y=$y"; exec 8<&-
exec 8< two; read -n 2 -u 8 p; read -u 8 q; read -u 8 r; echo "p=$p q=$q r=$r"; exec 8<&-
exec 8< two; while read -u 8 l; do printf '%s,' "$l"; done; echo; exec 8<&-
exec 8< <(printf 'p1\np2\np3\n'); read -u 8 m; read n <&8; read -u 8 o; echo "m=$m n=$n o=$o"; exec 8<&-
cd / && rm -rf "$d"
