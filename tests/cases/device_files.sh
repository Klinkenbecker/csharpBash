# 2026-10-10: Linux's device files work: /dev/null, /dev/zero, /dev/random and /dev/urandom, /dev/stdin,
# /dev/stdout, /dev/stderr and /dev/fd/N as utility operands, redirect targets (in-process and for a
# program) and under `test`. Only the redirect code knew /dev/null, /dev/stdout and /dev/stderr: `cat
# /dev/null`, `head -c 5 /dev/zero`, `diff -u /dev/null new`, `cp /dev/null f`, `[ -e /dev/null ]` all
# failed. Also: tr streams (`tr '\0' z < /dev/zero | head -c 3` never ended); od prints its offset for an
# empty input. /dev/tty depends on a console, so it is not here. Expected output: GNU bash via WSL.
d=$(mktemp -d) && cd "$d" || exit 1
{
echo "1 $(head -c 5 /dev/zero | od -An -tx1)"
echo "2 $(head -c 5 < /dev/zero | od -An -tx1)"
echo "3 $(head -c 16 /dev/urandom | wc -c)"
echo "4 $(head -c 16 /dev/random | wc -c)"
echo "5 $(echo x | cat /dev/stdin)"
echo "6 $(echo x | cat /dev/fd/0)"
echo "7 $(echo x | cat - /dev/null)"
echo "8 $(cat /dev/null | wc -c)"
echo data > f; exec 3< f; echo "9 $(cat /dev/fd/3)"; exec 3<&-
echo "10 $( (echo to-fd2 > /dev/fd/2) 2>&1 )"
echo "11 $( (echo to-stderr > /dev/stderr) 2>&1 )"
for p in /dev/null /dev/zero /dev/urandom /dev/random /dev/stdin /dev/stdout /dev/fd/0; do
  printf '%s e=%s c=%s r=%s w=%s f=%s\n' "12 $p" "$([ -e $p ] && echo y || echo n)" "$([ -c $p ] && echo y || echo n)" "$([ -r $p ] && echo y || echo n)" "$([ -w $p ] && echo y || echo n)" "$([ -f $p ] && echo y || echo n)"
done < /dev/null
echo "13 $(tr '\0' 'z' < /dev/zero | head -c 3)"
echo "14 $(wc -c < /dev/null)"
echo "15 $(head -c 4 /dev/zero > /dev/null; echo ok)"
for c in "wc -c /dev/null" "awk 'END{print NR}' /dev/null" "sed -n p /dev/null" "sort /dev/null" "md5sum /dev/null" "cut -c1 /dev/null" "uniq /dev/null" "tac /dev/null" "nl /dev/null" "od -c /dev/null" "base64 /dev/null" "cmp /dev/null /dev/null" "diff /dev/null /dev/null" "tail -n1 /dev/null" "head -n1 /dev/null" "paste /dev/null" "grep -c x /dev/null" "cp /dev/null e1; wc -c < e1" "cat /dev/null > e2; wc -c < e2" "ls /dev/null" "stat -c '%F %s %a %f' /dev/null" "jq . /dev/null" "sha256sum /dev/null" "printf '' | od -c"; do
  printf '%s => ' "$c"; eval "$c" 2>&1 | tr '\n' ' '; echo "rc=${PIPESTATUS[0]}"
done
printf 'a\nb\n' > new.txt; echo "16 $(diff -u /dev/null new.txt | grep -c '^+')"
echo "x1 $("$BASH" -c 'head -c 3 | od -An -tx1' < /dev/zero)"
echo "x2 $("$BASH" -c 'echo to-err >&2' 2> /dev/fd/1)"
echo "x3 $( ("$BASH" -c 'echo to-out' > /dev/fd/2) 2>&1 )"
echo "x5 $(echo piped | "$BASH" -c 'cat /dev/stdin')"
echo "x6 $(echo in | cat < /dev/stdin)"
echo "x7 $(echo tr-stream | tr a-z A-Z)"
} 2>&1
cd / && rm -rf "$d"
