# 2026-11-05 (the architect: a line passed through keeps its CRLF: "keep it"): the bytes beside crlf_lines_gnu. A line
# tool parses a Windows program's CRLF as the line end, and prints the line as it came, CRLF and all (sed -i on a CRLF
# file must not turn it LF: msp430-a2's 2026-10-03 damage); a CR inside a line is data; a pattern that names the CR
# finds it (msp430-a2's `grep -c $'\r'` check for CRLF files); byte offsets count the real bytes; a CR at the very end
# of the input, no LF after it, stays data; $( ) keeps a CRLF inside its value.
# Expected: authored (checked by hand, byte for byte: GNU on Linux never meets a Windows program's CRLF).
w=$(mktemp -d) && cd "$w" || exit 1
printf 'a.dll\r\nb.txt\r\nc\rd\r\ne.dll\n' > f.txt
echo "grep 'dll\$' (passed through):"; grep 'dll$' f.txt | od -An -c
echo "grep -v 'dll\$':"; grep -v 'dll$' f.txt | od -An -c
echo "grep -c \$'\\r': $(grep -c $'\r' f.txt)  grep -cP '\\r\$': $(grep -cP '\r$' f.txt)"
echo "a CR inside a line is data: $(grep -c 'c.d$' f.txt) $(grep -c '^c$' f.txt)"
echo "-b -o (the real bytes): $(grep -bo 'txt$' f.txt)"
mapfile arr < f.txt; echo "mapfile, no -t (passed through):"; printf '%s' "${arr[0]}" | od -An -c
v=$(printf 'x\r\ny\r\n\r\n'); echo "\$( ) keeps an inner CRLF:"; printf '%s' "$v" | od -An -c
printf 'z\r' | { read -r q; echo "read, a CR with no LF after it:"; printf '%s' "$q" | od -An -c; }
printf 'a.dll\r\nb\r\n' > s.txt
echo "sed s/dll\$/X/:"; sed 's/dll$/X/' s.txt | od -An -c
echo "sed N, G:"; sed N s.txt | od -An -c; sed G s.txt | od -An -c
echo "sed 's/\\r\$//' (names the CR, dos2unix):"; sed 's/\r$//' s.txt | od -An -c
cp s.txt i.txt; sed -i 's/^b$/B/' i.txt; echo "sed -i keeps CRLF:"; od -An -c i.txt
echo "sed a (text sed writes: LF):"; sed '1a new' s.txt | od -An -c
printf 'a b.dll\r\nc d\r\n' > w.txt
echo "awk /dll\$/ (passed through):"; awk '/dll$/' w.txt | od -An -c
echo "awk print \$NF (what awk makes ends with LF):"; awk '{print $NF}' w.txt | od -An -c
echo "awk \$2 = \"X\"; print (the record, edited, keeps its end):"; awk '{$2 = "X"; print}' w.txt | od -An -c
echo "awk printf, END (what awk writes: LF):"; awk '{printf "%s|", $2} END {print NR}' w.txt | od -An -c
echo "awk sub(/\\r\$/, \"\") (names the CR):"; awk '{sub(/\r$/, "")} 1' w.txt | od -An -c
echo "awk ORS set:"; awk -v ORS=';' '{print $1}' w.txt | od -An -c
printf 'a b c\r\nd e\n' > t.txt
echo "cut -d' ' -f1 (what cut selects ends with LF):"; cut -d' ' -f1 t.txt | od -An -c
echo "cut -c2-3:"; cut -c2-3 t.txt | od -An -c
echo "cut -f1 (no delimiter: the line whole):"; cut -f1 t.txt | od -An -c
echo "rev (what rev writes ends with LF):"; rev t.txt | od -An -c
printf 'x\r\nx\r\ny\n' > u.txt
echo "uniq (the first line as it came):"; uniq u.txt | od -An -c
echo "sort (each line as it came):"; printf 'b\r\na\n' | sort | od -An -c
echo "nl (a CRLF blank line is not numbered):"; printf 'x\r\n\r\n' | nl | od -An -c
cd / && rm -rf "$w"
