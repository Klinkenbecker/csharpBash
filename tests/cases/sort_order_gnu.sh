# 2026-10-30 (the "match GNU" item): sort and comm order lines by their bytes, as GNU's in the C locale (how Claude Code
# runs its shell: no LANG). They compared UTF-16 units: an astral character (a surrogate pair) came before U+E000-FFFF,
# and an undecodable byte (carried as U+DC80-DCFF) between U+00E9 and U+E000 instead of at its byte value. Both or
# neither: comm needs sort's order. Each line ends in an ASCII tag, the only part printed. Expected: GNU coreutils
# 8.32 via WSL, LC_ALL=C.
export LC_ALL=C
cd "${TMPDIR:-${TEMP:-/tmp}}" || exit 1
d="sort-order-$$"; mkdir -p "$d" && cd "$d" || exit 1
tags() { sed 's/.* //' | tr '\n' ' '; echo; }
printf '%s\n' $'\xef\xbc\xa1 FF21' $'\xf0\x9f\x98\x80 1F600' $'\xee\x80\x80 E000' $'\xc3\xa9 E9' 'z 7A' \
	$'\xff xFF' $'\x80 x80' $'\xc3 xC3' $'\xc3\xa9\xf0\x9f\x98\x80 E9+1F600' $'\xc3\xa9\xee\x80\x80 E9+E000' \
	$'\xf0\x9f\x98\x81 1F601' $'\xd7\xbf 5FF' > in
echo "--- sort";          sort in | tags
echo "--- sort -r";       sort -r in | tags
echo "--- sort -k1,1";    sort -k1,1 in | tags
echo "--- sort -u";       sort -u in | tags
sort in > a; printf '%s\n' $'\xee\x80\x80 E000' $'\xf0\x9f\x98\x80 1F600' $'\xff xFF' 'new 0' | sort > b
echo "--- comm -12";      comm -12 a b 2>&1 | tags
echo "--- comm -3";       comm -3 a b 2>&1 | tr -d '\t' | tags
echo "--- comm, status";  comm a b > /dev/null 2>&1; echo "rc=$?"
echo "--- sort -c";       sort -c a 2>&1; echo "rc=$?"
# equal keys: the whole line decides (sort's last resort)
printf '%s\n' $'k \xff xFF' $'k \xf0\x9f\x98\x80 1F600' $'k \xee\x80\x80 E000' 'k z 7A' > tie
echo "--- sort -k1,1, keys tie"; sort -k1,1 tie | tags
cd .. && rm -rf "$d"
