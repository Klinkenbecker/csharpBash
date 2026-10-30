# 2026-10-30 (item 4 of the list greenlit 2026-10-30): xargs runs each command as soon as its batch is full, as GNU
# does, instead of reading all its input first. A batch is -n args, -L lines, one -I line, or (default) what fits in
# -s chars (131072): every word, the command's own included, counts its length + 1. Shown without timing: when xargs
# stops early (a command exits 255, or -E's word), a 3-million-line producer is cut off rather than run to its end.
# A command's stdin is /dev/null (GNU's, unless -a), so it cannot eat the items still to come. The reader is GNU's:
# a quote must close on its line ("unmatched ... quote", the items before it still run, status 1), quotes and
# backslashes work under -I too, -0 and -d keep empty items, -E compares after quote removal and is ignored (with a
# warning) under -0 and -d. Errors go to their own file: their order against stdout is not compared.
# Expected: GNU findutils 4.8 via WSL, LC_ALL=C.
export LC_ALL=C
cd "${TMPDIR:-${TEMP:-/tmp}}" || exit 1
d="xargs-stream-$$"; mkdir -p "$d" && cd "$d" || exit 1
p() { printf '%s\n' "--- $1"; }
err() { [ -s err ] && sed "s|$BASH|BASH|; s/^/err: /" err; rm -f err; }
p "a command exiting 255 stops it: the producer is cut off"
{ seq 1 3000000 && : > done; } | xargs -n1 "$BASH" -c 'echo "got $1"; exit 255' x 2>err; echo "rc=$?"; err
[ -e done ] && echo "producer finished" || echo "producer cut off"; rm -f done
p "-E's word stops it: the producer is cut off"
{ echo a STOP b; seq 1 3000000 && : > done; } | xargs -E STOP echo; [ -e done ] && echo "producer finished" || echo "producer cut off"; rm -f done
p "a command's stdin is /dev/null"
printf 'a\nb\n' | xargs -n1 "$BASH" -c 'cat; echo "[$1]"' x
p "default batches: what fits in 131072"
seq 1 100000 | xargs echo | awk '{print NF, length($0)}'
for s in 13 14 8 7; do p "-s $s"; printf 'aa bb cc\n' | xargs -s $s echo 2>err; echo "rc=$?"; err; done
p "-s counts the initial args"; printf 'aa bb\n' | xargs -s 14 echo x
p "-n2 -s 11";  printf 'aa bb cc\n' | xargs -n2 -s 11 echo
p "-L1 -s 11";  printf 'aa bb cc\n' | xargs -L1 -s 11 echo 2>err; echo "rc=$?"; err
p "-L1, a trailing blank continues"; printf 'a \nb\nc\n' | xargs -L1 echo
p "-L2";        printf 'a b\nc\nd e\n' | xargs -L2 echo
p "a quote across a newline"; printf "a 'b\nc' d\n" | xargs echo 2>err; echo "rc=$?"; err
p "the same, -n1";  printf "x y 'b\nc' d\n" | xargs -n1 echo 2>err; echo "rc=$?"; err
p "-n2 then -L1: the later wins"; printf 'a b c\nd\n' | xargs -n2 -L1 echo 2>err; err
p "-L1 then -n2"; printf 'a b c\nd\n' | xargs -L1 -n2 echo 2>err; err
p "an unmatched double quote"; printf 'a "b\n' | xargs echo 2>err; echo "rc=$?"; err
p "backslash-newline joins"; printf 'a\\\nb c\n' | xargs -n1 echo | od -c | sed -n 1p
p "-I: quotes and backslashes work, blanks do not split"; printf "a 'b  c' \\\\x\n" | xargs -I{} echo "[{}]"
p "-I: leading blanks go, a blank line is skipped"; printf '  a\n  \n\nb \n' | xargs -I{} echo "[{}]"
p "-I: a quote across a newline"; printf "a 'b\nc'\n" | xargs -I{} echo "[{}]" 2>err; echo "rc=$?"; err
p "-0 keeps empty items"; printf 'a\0\0b\0' | xargs -0 -n1 echo x
p "-d keeps empty items, not a trailing one"; printf 'a,,b,' | xargs -d, -n1 echo x
p "-E after quote removal"; printf "a 'STOP' b\n" | xargs -E STOP echo
p "-E under -0"; printf 'a\0STOP\0b\0' | xargs -0 -E STOP echo 2>err; err
p "255's message"; printf 'a\nb\n' | xargs -n1 "$BASH" -c 'echo "$1"; exit 255' x 2>err; echo "rc=$?"; err
p "123"; printf 'a\nb\n' | xargs -n1 "$BASH" -c 'exit 1' x; echo "rc=$?"
p "no input runs once; -r does not"; printf '' | xargs echo hi; printf '  \n' | xargs echo hi; printf '' | xargs -r echo hi; echo "rc=$?"
p "a CR is data"; printf 'a\r\nb\n' | xargs -n1 echo | od -c | sed -n 1p
cd .. && rm -rf "$d"
