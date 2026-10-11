# 2026-10-10: jq -R / --raw-input (each input line a JSON string) and -Rs (the whole input one string), as
# jq 1.6: the inputs are one text (a file with no final newline runs into the next file's first line), a
# last line with no newline still counts, errors are located file:line. msp430-a2 wanted
# `jq -Rs '{text: .}' file` and took a C# path instead; -R was refused before. BASH_COREUTILS=builtin, so
# nothing falls through to a PATH jq. Expected output: jq 1.6 via WSL, the same script.
export BASH_COREUTILS=builtin
d=$(mktemp -d); cd "$d" || exit 1
printf 'a\nb c\n\nd' > f1; printf 'e\nf\n' > f2; printf 'x"y\\z\ttab\r\n' > f3
echo "-- 1"; jq -R . f1
echo "-- 2"; jq -Rs . f1
echo "-- 3"; jq -R . f1 f2
echo "-- 4"; jq -Rs . f1 f2
echo "-- 5"; jq -Rs '{text: .}' f3
echo "-- 6"; jq -R -c 'split(" ")' f1
echo "-- 7"; printf 'p\nq\n' | jq -R 'error("bad")' 2>&1; echo "rc=$?"
echo "-- 8"; printf 'p\nq\n' | jq -Rs 'error("bad")' 2>&1; echo "rc=$?"
echo "-- 9"; printf 'p\nq\n' | jq -nR '.'
echo "-- 10"; printf '' | jq -R . ; echo "rc=$?"
echo "-- 11"; printf '' | jq -Rs . ; echo "rc=$?"
echo "-- 12"; printf '1\n2\n' | jq -R 'tonumber + 1'
echo "-- 13"; printf 'a\nb\n' | jq -Rr '. + "!"'
echo "-- 14"; printf 'a\n' | jq --raw-input -c '[.]'
echo "-- 15"; printf 'k=v\n' | jq -R -c 'split("=") | {(.[0]): .[1]}'
echo "-- 16"; printf 'caf\xc3\xa9\n' | jq -R -c '[., length]'
echo "-- 17"; printf 'a\nb\n' | jq -R -e 'select(. == "z")'; echo "rc=$?"
echo "-- 18"; jq -R 'error("x")' f2 2>&1
echo "-- 19"; jq -R 'error("x")' f1 f2 2>&1
cd /; rm -rf "$d"
