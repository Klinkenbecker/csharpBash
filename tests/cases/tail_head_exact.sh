# 2026-10-27 (beside koliada-net-28's report): tail, and head's from-the-end forms, byte for byte as GNU, from a file
# (read from its end) and from a pipe (a bounded window): a last line without a newline stays without one (tail -n
# added one), -n 0 and -c 0, +N, -z records, CR is data, records across the 64 KiB blocks, a line longer than a block,
# several files, a seekable stdin, and a UTF-8 character cut or kept in a capture. Each line: byte count, then the
# bytes with newline as |, NUL as @, CR as ^. Expected: GNU coreutils 8.32 via WSL.
cd "${TMPDIR:-${TEMP:-/tmp}}" || exit 1
d="tail-exact-$$"; mkdir -p "$d" && cd "$d" || exit 1
show() { eval "$1" > out 2>&1; printf '%-34s %s:%s\n' "$1" "$(wc -c < out)" "$(tr '\n\0\r' '|@^' < out)"; }

printf 'a\nb' > nonl
printf 'a\nb\nc\n' > three
printf 'a\r\nb\r\n' > crlf
: > empty
printf 'a\0b\0c' > zero
printf 'a\0b\0' > zeroend
seq 1 200000 > many
{ head -c 100000 /dev/zero | tr '\0' x; echo; echo short; } > long

for f in nonl three crlf empty; do
	for op in "tail -n 1" "tail -n 2" "tail -n 0" "tail -n +2" "tail -n +0" "tail -c 3" "tail -c 0" "tail -c +2" \
	          "head -n -1" "head -n -2" "head -n -0" "head -c -1" "head -c -100"; do
		show "$op $f"
		show "cat $f | $op"
	done
done

echo "--- -z"
for f in zero zeroend; do
	for op in "tail -z -n 1" "tail -z -n 2" "tail -z -n +2" "head -z -n -1" "head -z -n 1"; do
		show "$op $f"
		show "cat $f | $op"
	done
done

echo "--- across blocks"
for src in "many" "< many" "cat many |"; do
	case $src in "cat many |") pre="cat many | "; arg="" ;; *) pre=""; arg=" $src" ;; esac
	show "${pre}tail -n 3$arg"
	show "${pre}tail -c 10$arg"
	show "${pre}tail -n 70000$arg | head -n 1"
	show "${pre}head -n -199998$arg"
	show "${pre}head -n -70000$arg | tail -n 1"
	show "${pre}head -c -1288890$arg"
done

echo "--- a line longer than a block"
for pre in "" "cat long | "; do
	arg=" long"; [ -n "$pre" ] && arg=""
	echo "$pre tail -n 2: $(eval "${pre}tail -n 2$arg" | wc -c)"
	echo "$pre tail -n 1: $(eval "${pre}tail -n 1$arg" | wc -c)"
	echo "$pre head -n -1: $(eval "${pre}head -n -1$arg" | wc -c)"
done

echo "--- several files, a seekable stdin"
show "tail -n 1 nonl three"
show "tail -c 2 nonl crlf"
show "tail -n 2 < three"
show "head -n -1 < three"

echo "--- UTF-8 in a capture"
v=$(printf 'x\303\251\n' | tail -c 3); echo "kept: ${#v} $v"
v=$(printf 'x\303\251' | tail -c 1 | od -An -tx1); echo "cut: $v"

cd .. && rm -rf "$d"
