# P4: in-process awk subset (DECISIONS 2026-09-04 #7). Supported cases match gawk
# byte-for-byte (generated from Git for Windows' awk and verified); the last block
# shows the loud boundary for constructs outside the contract.
cd "$(dirname "$0")/../fixtures/awk" || exit 1

awk '{print $1}' people.txt
awk '{print $2, $1}' people.txt
awk -F: '{print $2}' kv.txt
awk -F= '{print $1 "=" toupper($2)}' kv.txt
awk -F, '{print NF, $NF}' csv.txt
awk '{print NR": "$0}' people.txt
awk 'NR==2' people.txt
awk 'NR>1 && $2>28 {print $1}' people.txt
awk '$3=="NY"{n++} END{print n+0}' people.txt
awk '{s+=$2} END{print s, s/NR}' people.txt
awk '{a[$3]++} END{for(k in a) print k, a[k]}' people.txt | sort
awk 'BEGIN{OFS="-"} {$1=$1; print}' people.txt
awk '{$2=""; print}' people.txt
awk 'BEGIN{printf "%5.2f|%-5s|%05d|%x|%c|%s%%\n", 3.14159, "ab", 42, 255, 65, "p"}'
awk 'BEGIN{print length("hello"), substr("hello",2,3), index("hello","l"), toupper("x")}'
awk 'BEGIN{s="a-b-c"; n=split(s,p,"-"); print n, p[1], p[3]}'
awk '{gsub(/[aeiou]/,"_"); print}' people.txt
awk '{sub(/^ +/,""); print "["$0"]"}' ws.txt
awk 'BEGIN{x="hello world"; if (match(x,/wor/)) print RSTART, RLENGTH}'
awk '/^b/,/^c/' people.txt
awk '!/NY/' people.txt
awk 'length($0)>11' people.txt
awk '{print length}' people.txt
awk 'BEGIN{i=0; while(i<3){print i; i++}}'
awk 'BEGIN{for(i=1;i<=3;i++) printf "%d ", i; print ""}'
awk 'BEGIN{x=5; x+=2; x*=3; print x, x%4, x^2, -x, !x, 7/2, int(7/2)}'
awk 'BEGIN{print 1==1, "a"<"b", 10<9, "10"<"9", 2 " " 3}'
awk 'BEGIN{print 0.1+0.2, 1e6, 1e16, 100000 * 100000, 1/3, 123456789}'
awk 'BEGIN{print substr("hello",0), substr("hello",-1,3), substr("hello",4,10), substr("hello",2), substr("hello",2.7,2)}'
awk -v n=2 'NR==n' people.txt
awk '{print $1}' OFS=, people.txt
awk 'NR==FNR{a[$1]; next} $1 in a {print "both", $1}' people.txt people.txt | head -2
awk 'BEGIN{a["x"]=1; delete a["x"]; print length(a); a["y"]; print ("y" in a), ("z" in a)}'
awk '{ if ($2 >= 35) print $1, "old"; else print $1, "young" }' people.txt
awk 'END{print NR, $0}' people.txt
awk 'BEGIN { printf("%s-%s\n", "a", "b") }'
awk 'BEGIN{x["a"]=1;x["b"]=2; n=0; for (k in x) n+=x[k]; print n}'
awk '$2 ~ /^3/ {print $1} $2 !~ /^3/ {print "no", $1}' people.txt
awk 'BEGIN { s = "x"; s = s "y" "z"; print s, length(s) }'
awk 'NR==1{next} {print}' people.txt
awk '{exit 3} END{print "end"}' people.txt; echo "rc=$?"
awk 'BEGIN{exit} {print "never"} END{print "end ran"}' people.txt
echo "x y z" | awk '{$5="w"; print; print NF}'
echo "a b c d" | awk '{NF=2; print}'
echo "3abc 0x10 .5 1e2 abc" | awk '{print $1+0, $2+0, $3+0, $4+0, $5+0, ($1==3), ($5=="abc")}'
printf 'b\na\nc\n' | awk '{l[NR]=$0} END{for(i=NR;i>0;i--) print l[i]}'
awk 'BEGIN{print tolower("ABC"), sprintf("%03d", 7), 2^10, 5 % 3, -7 % 3}'
awk 'BEGIN{x = 1 ? "t" : "f"; print x; y = 0 ? "t" : "f"; print y}'
awk 'BEGIN{n=split("a b  c", arr); print n, arr[2]}'
awk 'BEGIN{n=split("a1b22c", arr, /[0-9]+/); print n, arr[3]}'
awk '{print $1, $(NF-1)}' people.txt
awk 'BEGIN{printf "%d %i %5s|%-3d|%+d\n", "12abc", 3.9, "ab", 7, 5}'
awk '{ print NR % 2 ? "odd" : "even" }' people.txt
awk 'BEGIN { if (!("k" in m)) print "absent"; m["k"]; if ("k" in m) print "present" }'
awk 'END { print FILENAME }' people.txt
awk '
/NY/ {
  print "ny:", $1
}
END { print "done" }' people.txt
awk 'BEGIN{a="10"; b=9; print (a<b), (a+0<b)}'
awk 'BEGIN{OFS=":"; $0="a b c"; $1=$1; print; print NF}'
awk 'BEGIN{x="A"; print x ~ "a", x ~ /A/, "abc" ~ "^a.c$"}'
awk 'BEGIN{s="foo.bar"; gsub(".", "-", s); print s}'
awk 'BEGIN{s="aaa"; n=gsub(/a/, "&&", s); print n, s}'
awk 'BEGIN { do { i++ } while (i < 5); print i }'
awk 'BEGIN { for (i=0;i<10;i++) { if (i==2) continue; if (i==5) break; printf "%d", i }; print "" }'
awk '{ print $1 }; END { print "n=" NR }' people.txt
echo "a|b|c" | awk -F'|' '{print NF}'
echo "a.b.c" | awk -F. '{print NF}'
printf 'k\tv\n' | awk -F'\t' '{print $2}'
awk 'BEGIN{print 1e18, 2^53, -1e17}'
awk 'BEGIN{print "to-err" > "/dev/stderr"; print "to-out"}' 2>/dev/null
awk 'BEGIN{print "err-line" > "/dev/stderr"}' 2>&1
awk 'BEGIN{printf "%s|%d\n", "pf", 7 > "/dev/stderr"}' 2>&1 >/dev/null
awk '{print $1 > "/dev/stdout"}' people.txt | head -2

# the loud boundary (BASH_COREUTILS=builtin so no PATH awk is consulted)
export BASH_COREUTILS=builtin
awk 'BEGIN{"date" | getline d; print d}' 2>&1 | head -1
awk 'function f(x){return x*2} BEGIN{print f(2)}' 2>&1 | head -1
awk 'BEGIN{print 1,2 > "out.txt"}' 2>&1 | head -1; rm -f out.txt
awk 'BEGIN{printf "%.3e\n", 12345}' 2>&1 | head -1
out=$(awk 'BEGIN{print ENVIRON["HOME"]}' 2>&1); echo "rc=$?"; echo "$out" | head -1
