# 2026-10-10: a UTF-8 character cut by a read's block boundary (4096 bytes) or split across pipe writes
# is one character, as in bash: `read`, a pipe, `mapfile` and `while read` decoded `é` at byte 4096 as
# two raw bytes (a line one char too long, `${l: -2}` showed a raw byte). Invalid bytes and a sequence
# cut by the end stay raw bytes, byte for byte. Expected output: GNU bash via WSL.
d=$(mktemp -d) && cd "$d" || exit 1
{ printf '%4095s' '' | tr ' ' 'a'; printf '\xc3\xa9b\n'; } > two.txt
{ printf '%4094s' '' | tr ' ' 'a'; printf '\xe2\x82\xacb\n'; } > three.txt
{ printf '%4093s' '' | tr ' ' 'a'; printf '\xf0\x9f\x98\x80b\n'; } > four.txt
{ printf '%4095s' '' | tr ' ' 'a'; printf '\xffb\n'; } > bad.txt
{ printf 'x\n'; printf '%4094s' '' | tr ' ' 'a'; printf '\xc3\xa9y\nlast\n'; } > handoff.txt
printf 'abc\xc3' > cut.txt
for f in two.txt three.txt four.txt bad.txt; do
  read -r line < "$f"; echo "$f redirect: ${#line} $(printf '%s' "${line: -2}" | od -An -tx1 | tr -s ' ')"
  line=$(cat "$f" | { read -r l; echo "$l"; }); echo "$f pipe: ${#line}"
  mapfile -t arr < "$f"; echo "$f mapfile: ${#arr[0]}"
  while IFS= read -r l; do echo "$f while: ${#l}"; done < "$f"
done
read -r line < cut.txt; echo "cut: ${#line} $(printf '%s' "$line" | od -An -tx1 | tr -s ' ')"
x=$( { printf 'p\xc3'; sleep 0.2; printf '\xa9q\n'; } | { read -r v; echo "${#v}"; } ); echo "split writes: $x"
{ read -r a; "$BASH" -c 'read -r b; echo "child: ${#b} ${b: -2}"'; read -r c; echo "parent: $a $c"; } < handoff.txt
cd / && rm -rf "$d"
