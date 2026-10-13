# 2026-10-12 (adc-framework-d8): rm as GNU rm. A write-protected file (`chmod a-w`; Windows' ReadOnly) is removed
# when standard input is not a terminal (C#Bash refused it without -f). -i asks before each removal and -I once
# (more than three operands, or -r), the answer a line of standard input, end of input meaning no (C#Bash
# removed without asking); the last of -f / -i / -I wins. A directory link is removed, never descended into
# (`rm -r` on a junction or directory symlink emptied its target). Expected output: GNU rm via WSL.
d=$(mktemp -d) && cd "$d" || exit 1
ro() { : > "$1"; chmod a-w "$1"; }
e() { "$@" 2>&1 | sed 's/^.*line [0-9]*: //'; echo " rc=${PIPESTATUS[0]}"; }
k() { [ -e "$1" ] && echo "  $1 kept" || echo "  $1 removed"; }
ro a; echo "1 rm write-protected, stdin not a terminal:"; e rm a </dev/null; k a
: > b; echo "2 rm -i, end of input:"; e rm -i b </dev/null; k b
ro c; echo "3 rm -i write-protected:"; e rm -i c </dev/null; k c
: > d1; : > d2; : > d3; : > d4; echo "4 rm -I four files, end of input:"; e rm -I d1 d2 d3 d4 </dev/null; ls d? | wc -l
: > m1; : > m2; echo "5 rm -I two files (no question):"; e rm -I m1 m2 </dev/null; ls m? 2>/dev/null | wc -l
mkdir t; ro t/x; echo "6 rm -r with a write-protected file inside:"; e rm -r t </dev/null; k t
ro f; echo "7 rm -f write-protected:"; e rm -f f; k f
echo x > g; echo "8 echo y | rm -i:"; echo y | rm -i g 2>&1; echo; k g
echo x > w; echo "9 echo yes | rm -i:"; echo yes | rm -i w 2>&1; echo; k w
echo x > n; echo "10 echo no | rm -i:"; echo no | rm -i n 2>&1; echo; k n
ro h; echo "11 rm -i -f (the last wins):"; e rm -i -f h </dev/null; k h
echo x > j; echo "12 rm -f -i (the last wins):"; e rm -f -i j </dev/null; k j
mkdir u; echo x > u/f; echo "13 rm -ri, answer n:"; printf 'n\n' | rm -ri u 2>&1; echo; k u
mkdir v; echo x > v/f; echo "14 rm -ri, answers y y y:"; printf 'y\ny\ny\n' | rm -ri v 2>&1; echo; k v
mkdir r; echo "15 rm -rI, end of input:"; e rm -rI r </dev/null; k r
mkdir ed; echo "16 rm -di, answer y:"; echo y | rm -di ed 2>&1; echo; k ed
mkdir tg; echo keep > tg/f; ln -s tg l1; echo "17 rm -r a directory link (C#Bash emptied the target):"; e rm -r l1; k l1; k tg/f
ln -s tg l2; echo "18 rm a directory link:"; e rm l2; k l2; k tg/f
cd / && rm -rf "$d"
