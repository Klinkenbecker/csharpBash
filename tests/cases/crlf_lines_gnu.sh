# 2026-11-05 (koliada-net-b9; the architect: "windows tools will (generally) always use CRLF and bash tools need to be
# able to cope with it"): the line tools parse a Windows program's CRLF as the line end. `grep 'dll$'` over unzip's or
# nupkg.exe's CRLF output found nothing; read's last field, mapfile -t and $( ) kept the CR.
# The data here ends its lines with CRLF; GNU's answer is for the same data with LF (DECISIONS 2026-11-05 "RULED:
# Windows programs' CRLF lines"). Expected: GNU via WSL, from tests/:
#   CRLF_REFERENCE=lf wsl.exe -e bash cases/crlf_lines_gnu.sh < /dev/null > expected/crlf_lines_gnu.out   (WSLENV=CRLF_REFERENCE)
e=$'\r'; [ "${CRLF_REFERENCE:-}" = lf ] && e=
w=$(mktemp -d) && cd "$w" || exit 1
printf "a.dll$e\nb.txt$e\nc.dll.bak$e\nd DEPLOY_PLAN.md$e\nwhole word$e\n" > f.txt
echo "== grep"
echo "dll\$: $(grep -c 'dll$' f.txt)  ERE: $(grep -cE '(dll|md)$' f.txt)  -x: $(grep -cx 'b.txt' f.txt)  -w: $(grep -cw 'word' f.txt)"
echo "-v: $(grep -cv 'dll$' f.txt)  -F -x: $(grep -cFx 'a.dll' f.txt)  -P: $(grep -cP 'md$' f.txt)  -i: $(grep -ci 'DLL$' f.txt)"
echo "-o: $(grep -o '[a-z]*\.dll$' f.txt | tr '\n' ' ')"
echo "-n: $(grep -n 'txt$' f.txt | tr -d '\r')"
printf "a.dll$e\nb.txt$e\n" > p.txt
echo "-f (a CRLF pattern file): $(grep -c -f p.txt f.txt)"
printf "a.dll\nb.txt\n" > lf.txt; echo "-f over LF data: $(grep -c -f p.txt lf.txt)"
echo "== read, mapfile, \$( )"
while read -r x y; do echo "[$x|$y]"; done < f.txt
while IFS=. read -r a b; do echo "<$b>"; done < f.txt
mapfile -t arr < f.txt; echo "mapfile -t: ${#arr[@]} [${arr[0]}] [${arr[4]}] ${#arr[3]}"
v=$(printf "1.0$e\n$e\n"); echo "\$( ): [$v] ${#v}"
[ "$(printf "yes$e\n")" = yes ] && echo "compare: equal"
n=$(grep -c . f.txt); echo "count: $((n + 1))"
echo "== sed (content: tr -d CR; the bytes are crlf_lines_keep's)"
echo "-n /dll\$/p: $(sed -n '/dll$/p' f.txt | wc -l)  \$=: $(sed -n '$=' f.txt)  /^b.txt\$/=: $(sed -n '/^b.txt$/=' f.txt)"
sed 's/dll$/X/; s/^\(.\) \(.*\)$/\2 \1/' f.txt | tr -d '\r'
sed -n 'N; s/\n/+/; s/t$/T/p' f.txt | tr -d '\r'
echo "== awk"
echo "\$NF ~ /dll\$/: $(awk '$NF ~ /dll$/' f.txt | wc -l)  /md\$/: $(awk '/md$/' f.txt | wc -l)  \$2==\"word\": $(awk '$2 == "word"' f.txt | wc -l)"
awk '{print NF, length($NF), $NF}' f.txt | tr -d '\r'
awk -F. '{print $NF}' f.txt | tr -d '\r'
awk '{n += length($0)} END {print n}' f.txt
cd / && rm -rf "$w"
