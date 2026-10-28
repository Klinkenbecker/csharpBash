# 2026-10-28: output to a redirected file is buffered and written when each builtin finishes, as bash's builtins flush
# theirs (it was a write a line: ~5.8 MB/s). Nothing visible changes: the order holds (builtin / program / builtin,
# printf pieces around a program, a raw-byte tool between, 2>&1, &>, exec > f, a loop, a background job, >> and >|, a
# function), what a builtin wrote is in the file once it finishes (a follower sees "start" while the group sleeps:
# buffering per redirect scope would fail here), `2> f` alone is written at once, large outputs are byte-exact.
# Expected: GNU bash 5.1 via WSL.
cd "${TMPDIR:-${TEMP:-/tmp}}" || exit 1
d="redir-buf-$$"; mkdir -p "$d" && cd "$d" || exit 1
printf 'mid\n' > m
{ echo a; "$BASH" -c 'echo ext'; echo b; } > f1; tr '\n' ' ' < f1; echo
{ printf a; printf b; "$BASH" -c 'printf X'; printf c; } > f2; cat f2; echo
{ echo a; cat m; echo b; } > f3; tr '\n' ' ' < f3; echo
{ echo o1; echo e1 >&2; echo o2; } > f4 2>&1; tr '\n' ' ' < f4; echo
{ echo o1; echo e1 >&2; echo o2; } &> f5; tr '\n' ' ' < f5; echo
( exec > f6; echo x; printf y; "$BASH" -c 'echo z'; echo w ); tr '\n' ' ' < f6; echo
for i in 1 2 3; do echo $i; done > f7; tr '\n' ' ' < f7; echo
{ echo bg1; echo bg2; } > f8 & wait; tr '\n' ' ' < f8; echo
{ echo start; sleep 2; echo end; } > f9 & sleep 1; echo "while it sleeps: $(cat f9)"; wait; tr '\n' ' ' < f9; echo
{ echo err1 >&2; sleep 2; } 2> f10 & sleep 1; echo "stderr at once: $(cat f10)"; wait
seq 1 200000 > f11; md5sum < f11 | cut -c1-12
seq 1 200000 | sed 's/$/!/' > f12; md5sum < f12 | cut -c1-12
seq 1 200000 | awk '{ print $1 * 2 }' > f13; md5sum < f13 | cut -c1-12
echo one > f14; echo two >> f14; cat f14 | tr '\n' ' '; echo three >| f14; cat f14
{ echo a; false; echo b; } > f15; tr '\n' ' ' < f15; echo
f() { echo in-f; return 3; }; f > f16; echo "rc=$? $(cat f16)"
# raw-byte tools with an error between their outputs, stderr into the file: the message in its place (433-442 wrote it
# last: stderr sat in the buffered writer while the bytes went straight to the file; stderr is now never left buffered)
printf 'abcdefgh\n' > s1.bin; strings s1.bin nofile.bin s1.bin > f17 2>&1; tr '\n' '|' < f17; echo
cat m nofile m > f18 2>&1; tr '\n' '|' < f18; echo
cd .. && rm -rf "$d"
