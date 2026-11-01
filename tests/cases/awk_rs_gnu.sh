# 2026-10-31 (kes-install-43's unmet need; the architect: "go"): awk's RS other than newline, as gawk. It was refused,
# "awk: unsupported: RS other than newline", with no PATH awk to hand over to (splitting `pnputil /enum-drivers` into
# one record per driver package). RS="" is paragraph mode: records end at one or more blank lines, leading blank
# lines are skipped, and newline also separates fields when FS is one character (gawk, measured: not for a regex FS,
# and not in split()).
# A one-character RS is that character, literally; a longer RS is a regular expression (gawk). RT is the text that
# ended the record. RS may be set by -v, as an operand between files, or between records, and getline follows it.
# Expected: gawk 5.1 via WSL.
d=$(mktemp -d) && cd "$d" || exit 1
echo "== the report: paragraph mode"
printf 'a\nb\n\nc\nd\n' | awk 'BEGIN{RS="";FS="\n"} {print NR": "$1"|"$2}'
echo "== paragraph: leading blank lines skipped, blank lines in a row are one separator, a line of spaces is not blank, RT"
printf '\n\n\np1 l1\np1 l2\n\n\n\np2 l1\n  \np2 l3\n\n' | awk 'BEGIN{RS=""} {x=$0; gsub(/\n/,"\\n",x); r=RT; gsub(/\n/,"\\n",r); print NR" NF="NF" ["x"] RT=["r"]"}'
echo "== paragraph: input without a final newline; with one"
printf 'x\ny\n\nz' | awk 'BEGIN{RS=""} {x=$0; gsub(/\n/,"\\n",x); r=RT; gsub(/\n/,"\\n",r); print NR": ["x"] RT=["r"]"}'
printf 'c\nd\n' | awk 'BEGIN{RS=""} {x=$0; gsub(/\n/,"\\n",x); r=RT; gsub(/\n/,"\\n",r); print NR": ["x"] RT=["r"]"}'
echo "== paragraph: newline separates fields whatever FS is"
printf 'a:b\nc:d\n\ne:f\n' | awk 'BEGIN{RS="";FS=":"} {print NR": NF="NF" "$1","$2","$3","$4}'
printf 'a:b;c\nd\n' | awk 'BEGIN{RS="";FS="[:;]"} {print "regex FS: NF="NF; for(i=1;i<=NF;i++) print i"=["$i"]"}'
printf 'a b\nc\n' | awk 'BEGIN{RS=""} {print "default FS: NF="NF}'
echo "== split() in paragraph mode"
printf 'a:b\nc\n' | awk 'BEGIN{RS=""} {n=split($0, p, ":"); print "split: "n; for(i=1;i<=n;i++){x=p[i]; gsub(/\n/,"\\n",x); print i"=["x"]"}}'
echo "== one character"
printf 'a;b;;c\n' | awk 'BEGIN{RS=";"} {x=$0; gsub(/\n/,"\\n",x); print NR": ["x"] RT=["RT"]"}'
printf 'a.b.c' | awk 'BEGIN{RS="."} {print NR": "$0}'
printf 'a b  c' | awk 'BEGIN{RS=" "} {print NR": ["$0"]"}'
echo "== a regular expression"
printf 'a12b345c\n' | awk 'BEGIN{RS="[0-9]+"} {x=$0; gsub(/\n/,"\\n",x); print NR": ["x"] RT=["RT"]"}'
printf 'one\r\ntwo\r\nthree\r\n' | awk 'BEGIN{RS="\r\n"} {print NR": ["$0"] length="length($0)}'
printf 'aXYbXYc' | awk 'BEGIN{RS="XY"} END{print NR, $0}'
echo "== RS by -v, and as an operand between files"
printf 'p\nq\n\nr\n' > f1; printf 'u;v;w' > f2
awk -v RS= '{print FILENAME": "NR": "$1}' f1
awk '{print FILENAME": "FNR": "$0}' RS= f1 RS=';' f2
echo "== RS changed between records"
printf 'a\nb;c;d\ne\n' | awk 'NR==1{RS=";"} {x=$0; gsub(/\n/,"\\n",x); print NR": ["x"]"}'
printf 'p1\np2\n\nq1\nq2\n' | awk 'BEGIN{RS=""} NR==1{RS="\n"} {x=$0; gsub(/\n/,"\\n",x); print NR": ["x"]"}'
printf 'r1;r2\nr3\n' | awk 'BEGIN{RS="[;]+"} NR==1{RS="\n"} {print NR": ["$0"]"}'
echo "== getline and getline < file follow RS"
printf 'm1;m2;m3' | awk 'BEGIN{RS=";"} NR==1{getline; print "after getline: "$0; while ((getline line < "f2") > 0) print "f2: ["line"]"}'
echo "== a separator split between two reads (a pipe that hands over the rest a second later) is one separator"
{ printf 'a\n\n'; sleep 1; printf '\n\nb\n'; } | awk 'BEGIN{RS=""} {r=RT; gsub(/\n/,"\\n",r); print NR": ["$0"] RT=["r"]"}'
{ printf 'a12'; sleep 1; printf '34b\n'; } | awk 'BEGIN{RS="[0-9]+"} {x=$0; gsub(/\n/,"\\n",x); print NR": ["x"] RT=["RT"]"}'
echo "== a long input: records across reads"
awk 'BEGIN{for(i=1;i<=30000;i++){print "line " i; if(i%7==0) print ""}}' > big
awk 'BEGIN{RS=""} END{print NR, $1, $NF}' big
awk 'BEGIN{RS="\n\n"} END{print NR, $2}' big
awk 'BEGIN{RS="e"} END{print NR}' big
cd / && rm -rf "$d"
