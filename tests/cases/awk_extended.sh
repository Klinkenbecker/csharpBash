# 2026-10-10: the in-process awk grows by installer-79's scope requests (2026-09-08) and the getline form a
# session used (nupkg-28): user functions, print/printf > file and >> file, close(), fflush(),
# getline [var] < file, 3-argument match(), awk --version. Until now each fell through to a PATH awk, or
# failed "awk: unsupported" where there is none. Expected output: gawk 5.1 via WSL, the same script.
# BASH_COREUTILS=builtin: nothing falls through, so every line below is the in-process awk's own answer.
export BASH_COREUTILS=builtin
d=$(mktemp -d); cd "$d" || exit 1

echo "-- functions"
awk 'function f(x,  y){y=x*2; return y} function g(a){a[1]="set"} BEGIN{print f(3); g(arr); print arr[1]}'
awk 'function fact(n){return n<=1?1:n*fact(n-1)} BEGIN{print fact(10)}'
awk 'function fib(n){return n<2?n:fib(n-1)+fib(n-2)} BEGIN{print fib(20)}'
awk 'function d(n){return n==0?0:1+d(n-1)} BEGIN{print d(250)}'
awk 'function f(n,  loc){loc["a"]; loc["b"]; return length(loc) + n} BEGIN{print f(1), f(2)}'
awk 'function g(a){a[1]="set"} BEGIN{g(arr); print arr[1], length(arr)}'
awk 'function inner(b){b["k"]="deep"} function outer(a){inner(a)} BEGIN{outer(x); print x["k"], length(x)}'
awk 'function fill(a, n,  i){for(i=1;i<=n;i++) a[i]=i*i} BEGIN{fill(sq,3); print sq[1], sq[2], sq[3], length(sq)}'
awk 'function f(x){x=5} BEGIN{y=1; f(y); print y}'
awk 'function f(){g=7} BEGIN{f(); print g}'
awk 'function f(x){x=9; return x} BEGIN{x=1; print f(2), x}'
awk 'function f(){return} BEGIN{x=f(); print "[" x "]", length(x)}'
awk 'function f(){y=1} BEGIN{print "[" f() "]"}'
awk 'function find(a, x,   k){for (k in a) if (a[k]==x) return k; return "none"} BEGIN{A[1]="p"; A[2]="q"; print find(A,"q"), find(A,"z")}'
awk 'function w(n){while(1){if(n>3) return n; n++}} BEGIN{print w(0)}'
awk 'function v(n,  i){do { if (++i == n) return i * 10 } while (1)} BEGIN{print v(3)}'
printf 'a 1\nb 2\n' | awk 'function dbl(x){return x*2} {print $1, dbl($2)}'
printf '1\n2\n' | awk 'function s(){next} {if ($1==1) s(); print}'
awk 'function bye(){exit 3} BEGIN{bye(); print "no"} END{print "end"}'; echo "rc=$?"
awk 'BEGIN { print twice(4) }
func twice(v,
           extra) { return v * 2 }'
awk 'function f(a){return a} function g(){print "evald"; return 2} BEGIN{print f(1, g())}' 2>&1
awk 'BEGIN{print foo(1)}' 2>&1; echo "rc=$?"

echo "-- output to files"
awk 'BEGIN{print "a" > "f1"; print "b" > "f1"; close("f1"); while ((getline l < "f1") > 0) printf "%s,", l; print ""}'
awk 'BEGIN{print "c" >> "f1"; close("f1")}'; tr '\n' ',' < f1; echo
awk 'BEGIN{printf "%d\n", 42 > "f2"; close("f2")}'; cat f2
awk 'BEGIN{printf("%s-%s\n", "p", "q") > "f3"; close("f3"); getline l < "f3"; print l}'
awk 'BEGIN{n=1; print "x" > "g" n; close("g1"); getline l < "g1"; print "g1:", l}'
printf 'a 1\nb 2\na 3\n' | awk '{print $2 > ($1 ".out")}'; cat a.out b.out
echo "rec" | awk '{print > "r.txt"} END{close("r.txt"); getline l < "r.txt"; print "r:", l}'
awk 'BEGIN{print "dash" > "-"; print "out" > "/dev/stdout"}'
printf 'a\n' | awk '{print > "/dev/null"; print "devnull ok"}'
awk 'BEGIN{print "x" > "nodir/f"; print "after"}' 2>&1; echo "rc=$?"
awk 'BEGIN{print "1" > "t"; print "2" > "t"; close("t"); print "3" >> "t"; close("t"); while((getline l < "t")>0) printf "%s,", l; print ""}'

echo "-- close and fflush"
awk 'BEGIN{print close("nope"); print "a" > "c1"; print close("c1"); getline x < "c1"; print close("c1")}' 2>&1
awk 'BEGIN{print fflush(); print fflush("nope"); print "z" > "ff"; print fflush("ff")}' 2>&1

echo "-- getline < file"
printf 'a b\nc d e\n' > in; printf 'X\n' | awk 'NR==1{getline v < "in"; print v, NR, FNR, $0, NF; getline < "in"; print $0, NF, NR}'
awk 'BEGIN{r = (getline l < "nofile"); print "r=" r}'; echo "rc=$?"
awk 'BEGIN{r = (getline l < "."); print "dir r=" r}'
printf 'S1\n' | awk 'BEGIN{getline l < "-"; print "l=" l}'
printf 'x\n' > g; awk 'BEGIN{a=(getline l < "g"); b=(getline l < "g"); c=close("g"); e=(getline l < "g"); print a, b, c, e, l}'
echo "a b c" | awk '{getline $2 < "g"; print; print NF}'
awk 'BEGIN{getline arr["k"] < "g"; print arr["k"]}'
awk 'function first(f,  l){getline l < f; close(f); return l} BEGIN{print first("in"), first("in")}'
printf '1\n2\n3\n' > nums; awk 'BEGIN{while ((getline n < "nums") > 0) s += n; print s, NR}'

echo "-- match with an array"
awk 'BEGIN{if (match("abc123", /([a-z]+)([0-9]+)/, m)) print m[1], m[2], m[0], RSTART, RLENGTH}'
awk 'BEGIN{n=match("ab", /(a)(x)?(b)/, m); print n; for (k in m) { split(k, p, SUBSEP); print p[1] "|" p[2] "=" m[k] } }' | sort
awk 'BEGIN{match("x10", /([0-9]+)/, m); print (m[1] < 9), (m[0] < 9), m[1,"start"] < 9}'
awk 'BEGIN{m["old"]=1; r=match("abc", /z(y)/, m); print r, length(m), RSTART, RLENGTH}'
awk 'BEGIN{match("k=v", "([a-z])=([a-z])", m); print m[1], m[2], m[0]}'
echo "key: value" | awk 'match($0, /^([^:]+): *(.*)$/, kv) {print kv[2] "<-" kv[1]}'

echo "-- version"
v=$(awk --version | head -1)
case $v in
	"GNU Awk 5.1"*) echo version-ok ;;
	"awk (C#Bash in-process subset, gawk-5.1 compatible) "*) echo version-ok ;;
	*) echo "version BAD: $v" ;;
esac

cd /; rm -rf "$d"
