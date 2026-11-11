# 2026-11-10 (lantern-92; the architect: "bugs; go"): (1) tr reads a range whose endpoints are escapes: '\001-\037'
# deleted only the two endpoints, '\000-\037' only the NUL. (2) jq's input as jq 1.6 reads it: a raw control character
# U+0001-U+001E in a string is refused (U+0000 and U+001F are taken), and NaN / Infinity in any case with a sign are
# numbers (null and +-1.7976931348623157e+308 on output), where `nan` alone is an invalid literal. Expected: GNU via WSL.
printf 'a\001b\037c\nd' | tr -d '\001-\037' | od -c | head -1
printf 'a\000b\001c\nd' | tr -d '\000-\037' | od -c | head -1
printf 'abc\n' | tr '\141-\143' 'x-z'
echo hello | tr 'a-z' 'A-Z'
echo 'a-b' | tr -d 'a-'
refused=""; for b in $(seq 0 31); do o=$(printf "%03o" $b); printf "[\"a\\$o\"]" | jq -c . >/dev/null 2>&1 || refused="$refused $b"; done
echo "control bytes refused:$refused"
for w in NaN -NaN +NaN NAN Nan -nan Infinity -Infinity INFINITY infinity Inf inf nan nAn; do
	printf '%s ' "$w:"; printf '[%s]' "$w" | jq -c . 2>/dev/null || printf 'refused'; echo
done
printf '{"v":NaN,"w":-Infinity}' | jq -c '[.v, .w, (.w < 0)]'
