# 2026-11-16 (DECISIONS "BUG FIXED ... a closed or wrong-way descriptor is EBADF; build 874", found fixing git-9d's report): a
# stdin closed or open for writing (`0<&-`, `0> f`, `0>&1`) fails a read with "Bad file descriptor", and a stdout closed
# or open for reading (`>&-`, `1<&-`, `1< f`) fails a write. C#Bash read such a stdin as empty and wrote such a stdout
# as usual (or nowhere), status 0. For what it runs in-process: the first line of each message and the status; head,
# tail, wc, uniq and base64 by status only (their own wording differs: KNOWN_LIMITATIONS "Error wording"). Expected = GNU bash 5.1 via WSL.
d=$(mktemp -d) && cd "$d" || exit 1
printf 'x\n' > in
first() { printf '%s\n' "${1%%$'\n'*}" | sed 's/^[^ ]*: line [0-9]*: //'; }
echo "--- reads, 0<&-"
for c in 'read v' 'sort' 'grep x' 'sed p' 'awk 1' 'tr a b' 'od -c' 'tee' 'cat' 'cut -c1' 'md5sum' 'xargs echo'; do
	err=$(eval "$c 0<&-" 2>&1 >/dev/null); rc=$?; echo "[$c] rc=$rc $(first "$err")"
done
for c in 'head -1' 'tail -1' 'wc -l' 'uniq' 'base64'; do eval "$c 0<&-" >/dev/null 2>&1; echo "[$c] rc=$?"; done
v=old; read v 0<&- 2>/dev/null; echo "read: rc=$? v=[$v]"
a=(old); mapfile a 0<&- 2>/dev/null; echo "mapfile: rc=$? count=${#a[@]}"
echo "--- the other forms of an unreadable stdin"
err=$( { cat 0> wo; } 2>&1 < in ); echo "0> wo: rc=$? $(first "$err") wo=[$(cat wo)]"
err=$( { cat 0>&1; } 2>&1 < in ); echo "0>&1: rc=$? $(first "$err")"
err=$( { cat 0>&-; } 2>&1 < in ); echo "0>&-: rc=$? $(first "$err")"
echo "--- writes"
for c in 'echo hi' 'printf hi' 'pwd'; do
	err=$(eval "$c >&-" 2>&1); rc=$?; echo "[$c >&-] rc=$rc $(first "$err")"
done
err=$(cat in 1< in 2>&1); echo "cat in 1< in: rc=$? $(first "$err")"
err=$(echo hi 1<&- 2>&1); echo "1<&-: rc=$? $(first "$err")"
err=$(echo hi 1< in 2>&1); echo "1< in: rc=$? $(first "$err")"
err=$( { echo a; echo b; } >&- 2>&1 ); echo "a group: rc=$? lines=$(printf '%s\n' "$err" | wc -l)"
out=$(echo hi 2< in >&2); echo "2< in >&2: rc=$? out=[$out]"
printf '' >&-; echo "nothing written: rc=$?"
cd / && rm -rf "$d"
