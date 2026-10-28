# 2026-10-12 (msp430-a2): awk's `getline` and `getline var` read the main input in-process, as gawk (NR, FNR, FILENAME,
# across files, in BEGIN, 0 at the end). They were outside the subset, so any program containing one, even never
# run, went to the PATH awk, Git's gawk, which strips every CR: a CRLF file came back LF. A program that still
# falls through (`cmd | getline`) or BASH_COREUTILS=external now gets -v BINMODE=3 and keeps CR: those two checks need a
# PATH awk and are in awk_fallthrough since 2026-10-20. Expected: GNU awk via WSL.
d=$(mktemp -d) && cd "$d" || exit 1
printf 'a\r\nb\r\nc\r\n' > crlf.txt
printf 'l1 x\nl2 y y\nl3\n' > f1; printf 'm1\nm2\n' > f2
cr() { tr -cd '\r' | wc -c | tr -d ' '; }
echo "1 plain: $(awk '{print}' crlf.txt | cr)"
echo "2 if(0) getline: $(awk '{ if (0) getline; print }' crlf.txt | cr)"
echo "3 /b/{getline}: $(awk '/b/{getline} {print}' crlf.txt | cr)"
echo "4"; awk '/b/{getline} {print}' crlf.txt | od -c | head -2
echo "5"; awk '{ getline; print NR, FNR, NF, $0 }' f1
echo "6"; awk '{ getline v; print NR, $0, "v=" v }' f1
echo "7"; awk 'BEGIN { while ((getline line) > 0) n++; print "BEGIN read", n, NR }' f1
echo "8"; awk 'BEGIN { getline; print "first:", $0, NF, NR }' f1
echo "9"; awk '{ r = getline; print FILENAME, FNR, NR, r, $0 }' f1 f2
echo "10"; awk 'END { r = getline; print "END getline", r, $0 }' f1
echo "11"; awk '{ getline; print v, $0 }' f1 v=7 f2
echo "12"; printf 's1\ns2\ns3\n' | awk 'NR==1 { getline x; print "x=" x } { print NR ": " $0 }'
echo "13"; awk '{ while ((getline n) > 0) c++ } END { print c, NR }' f1 f2
cd / && rm -rf "$d"
