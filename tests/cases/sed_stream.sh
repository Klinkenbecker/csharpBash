# 2026-10-27: sed reads lines as its script asks (each file was read whole first: it failed over 2 GiB and never ended
# on endless input, `yes | sed 3q`). On a file stdin `q` leaves the rest to the next command (GNU leaves the offset
# after the line); `$` peeks. The pattern and hold spaces each know whether their line's newline was there (x swaps,
# h/g copy, G/H take the appended one's); `q` writes an owed newline, `Q` does not. `F` names the current file; `q`
# ends the run under -s and -i; `w` files are opened once at the start (truncated even if never written; -s/-i
# reopened them per file); `w /dev/stdout` under -i is stdout; -i writes a temporary file that replaces the original.
# Expected: GNU sed 4.8 via WSL.
cd "${TMPDIR:-${TEMP:-/tmp}}" || exit 1
d="sed-stream-$$"; mkdir -p "$d" && cd "$d" || exit 1
seq 1 10000 > s; printf a > na; printf 'b\n' > nb; : > e; printf 'x\ny' > xy; printf 'a\n' > f1; printf 'b\n' > f2
c() { od -An -c | tr -s ' '; }
echo "== stdin left by q"; { sed 1q; head -n 1; } < s; { sed -n 2p; head -n 1; } < s; { sed 3q > /dev/null; sed -n 1p; } < s
echo "== newline flags"
for s in x 'x;p' G g '$!d;x' '1!G;h;$!d' 'H;$!d;x' 'N;P;D' 'n' 1q '1{p;q}' 1Q '$a\
END'; do
	printf '%-12s %s |%s\n' "$s" "$(printf 'a\nb' | sed "$s" | c)" "$(printf 'a' | sed "$s" | c)"
done
echo "-n 1{p;q} $(sed -n '1{p;q}' na | c) | -n 1{p;Q} $(sed -n '1{p;Q}' na | c)"
echo "na e: $(sed '' na e | c) | na nb p: $(sed p na nb | c) | \$p na nb: $(sed -n '$p' na nb | c)"
echo "\$d nb e: [$(sed '$d' nb e | c)] | -s \$d xy nb: $(sed -s '$d' xy nb | c) | N xy nb: $(sed N xy nb | c)"
printf a | sed -n 'w wo' ; echo "w keeps it missing: $(c < wo)"
echo "== F"; sed -n '1F;$F' xy nb; printf 'q\n' | sed F
echo "== q ends the run"; sed -s 1q xy nb; sed -s 2q s xy | tail -n 1
cp f1 i1; cp f2 i2; sed -i 1q i1 i2; echo "-i 1q: $(cat i1) $(cat i2)"; printf '1\n2\n' > i3; printf '3\n4\n' > i4; sed -i 1q i3 i4; echo "-i 1q: $(cat i3 | tr '\n' ' ')/ $(cat i4 | tr '\n' ' ')"
cp na i5; sed -i 1q i5; echo "-i 1q owed: $(c < i5)"
echo "== w files"; sed -s -n 'w wa' f1 f2; echo "-s: $(tr '\n' ' ' < wa)"; cp f1 g1; cp f2 g2; sed -i -n 'w wb' g1 g2; echo "-i: $(tr '\n' ' ' < wb)[$(cat g1 g2)]"
printf 'stale\n' > wc; sed -n '/nomatch/w wc' f1; echo "never written: [$(cat wc)]"; sed -n '/nomatch/w wd' f1; [ -e wd ] && echo "created"
printf '1\n2\n' > i6; out=$(sed -i 's/1/X/;w /dev/stdout' i6); echo "-i w /dev/stdout: [$(printf '%s' "$out" | tr '\n' ' ')] file: $(tr '\n' ' ' < i6)"
echo "== -i"; printf '3\n4' > i7; sed -i '$s/$/!/' f1 i7; echo "$(cat f1) $(c < i7)"
printf '1\n' > b1; sed -i.bak 's/1/A/' b1; sed -i.bak 's/A/B/' b1; echo "-i.bak twice: $(cat b1) $(cat b1.bak)"
printf '1\n' > b2; sed -i'old_*' 's/1/C/' b2; echo "-i old_*: $(cat b2) $(cat old_b2)"
sed -i 's/x/y/' f1 missing 2> err; echo "rc=$? $(sed 's/missing:.*/missing: .../' err)"
cp s i8; sed -i '$!d' i8; echo "-i \$!d: $(cat i8)"
echo "== \$"; echo "heredoc: $(sed -n '$p' <<'X'
one
two
X
)"; seq 1 3 | sed -n '$='; sed -n '$=' s xy e; sed -s -n '$=' s xy nb
cd .. && rm -rf "$d"
