# 2026-10-28: C#Bash's own stdout, when redirected (a pipe or a file: how Claude Code and this runner read it), is
# buffered and written when each builtin finishes, before any program starts, and at exit (it was a write a line); a
# console stays unbuffered. Nothing visible changes: the order of builtin and program output at top level, in a
# function, a subshell, a group with 2>&1, a background job, a capture, a here-document; the last words of a script
# that ends without a newline. Expected: GNU bash 5.1 via WSL.
echo a; "$BASH" -c 'echo ext'; echo b
printf a; printf b; "$BASH" -c 'printf X'; printf 'c\n'
echo o1; echo e1 >&2; echo o2
{ echo g1; "$BASH" -c 'echo g2'; echo g3; } 2>&1
f() { printf 'in f'; "$BASH" -c 'echo ", then a program"'; }; f
( echo sub1; "$BASH" -c 'echo sub2'; echo sub3 )
{ echo bg; } & wait
echo "$( echo captured; "$BASH" -c 'echo program in the capture' )"
cat <<HERE
here-document line
HERE
seq 1 50000 | md5sum | cut -c1-12
yes | head -n 2
for i in 1 2 3; do printf '%s ' $i; "$BASH" -c 'printf "p "'; done; echo
printf 'last words, no newline'
