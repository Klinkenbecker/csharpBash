# Byte transparency (ratified 2026-09-12): a shell string is a byte sequence, so bytes that are
# not valid UTF-8 must survive escapes, variables, command substitution and every write.
# ShellEncoding carries an undecodable byte as U+DC80+byte and maps it back on the way out.
# Fixtures are built with printf byte escapes here ON PURPOSE — that is half of what is under test.
cd "${TMPDIR:-${TEMP:-/tmp}}" || exit 1
d="bytes-$$"; mkdir -p "$d" && cd "$d" || exit 1

echo "--- printf and echo -e emit BYTES, not code points"
printf '\377'     | od -An -tx1 | tr -s ' '
printf '\xff'     | od -An -tx1 | tr -s ' '
printf '\xe9'     | od -An -tx1 | tr -s ' '
printf '\101\102' ; echo
echo -e '\xff'    | od -An -tx1 | tr -s ' '
printf '%s' $'\xff' | od -An -tx1 | tr -s ' '
echo $'\101'

echo "--- \\u is still a CODE POINT escape (two bytes for U+00E9)"
printf 'é' | od -An -tx1 | tr -s ' '

echo "--- ordinary text is ordinary UTF-8"
printf 'h\xc3\xa9llo' | od -An -tx1 | tr -s ' '
printf 'h\xc3\xa9llo' | wc -c

echo "--- bytes survive a variable and a command substitution"
printf '\xff\xfe\x80A\xc1' > b.bin
x=$(cat b.bin)
printf '%s' "$x" | cmp -s - b.bin && echo capture-round-trip-ok
y="$x"; printf '%s' "$y" | cmp -s - b.bin && echo variable-round-trip-ok
printf '%s' "$x" | wc -c

echo "--- and through a pipe, a file and a here-string"
printf '%s' "$x" > out.bin; cmp -s out.bin b.bin && echo file-write-ok
printf '%s' "$x" | cat | cmp -s - b.bin && echo pipe-ok
cat <<< "$x" | head -c 5 | cmp -s - b.bin && echo here-string-ok

echo "--- escapes compose with text"
printf 'a\xffb' | od -An -tx1 | tr -s ' '
printf 'a\xffb' | wc -c
v=$(printf 'a\xffb'); echo "len=${#v}"

cd ..; rm -rf "$d"
