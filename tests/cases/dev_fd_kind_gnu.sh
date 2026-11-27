# 2026-11-27 (bugs list 14, wombat-3a; bugs list 10, guy-94): /dev/stdin, /dev/stdout, /dev/stderr and /dev/fd/N test as
# what the descriptor is: -p a pipe (a C#Bash pipe, a pipe into a child, `$( )`, a here-document or here-string up to
# 65,536 bytes), -f and -s a file (`< f`, `exec 0< f`, `<&3`, /dev/fd/3, a longer here-string), -c /dev/null; they
# were false for all but -c. And `> /dev/stdin` writes to what stdin is: /dev/null, or the file itself (`>` empties it,
# `>>` appends), also from a child and inside one; it was "No such file or directory". A pipe is not written here
# (AWAITING the architect). Expected = GNU bash 5.1 via WSL.
d=$(mktemp -d) && cd "$d" || exit 1
t() { local l=$1; shift; if "$@"; then echo "$l: yes"; else echo "$l: no"; fi; }
r() { local v=no; "$@" && v=yes; echo "$v" > "$d/res"; }   # the test runs before res is opened
printf 'line\n' > f; : > empty
for op in -p -f -s -c; do
	echo x | { t "pipe $op" [ $op /dev/stdin ]; }
	t "file $op" [ $op /dev/stdin ] < f
	t "empty file $op" [ $op /dev/stdin ] < empty
	t "null $op" [ $op /dev/stdin ] < /dev/null
	t "herestring $op" [ $op /dev/stdin ] <<< x
	t "heredoc $op" [ $op /dev/stdin ] <<EOF
x
EOF
	echo x | t "pipe child $op" "$BASH" -c "[ $op /dev/stdin ]"
	t "file child $op" "$BASH" -c "[ $op /dev/stdin ]" < f
done
echo x | t 'pipe [[ -p ]]' eval '[[ -p /dev/stdin ]]'
echo x | t 'pipe -p /dev/fd/0' [ -p /dev/fd/0 ]
exec 3< f; t 'dup <&3 -f' [ -f /dev/stdin ] <&3; t 'fd 3 -f /dev/fd/3' [ -f /dev/fd/3 ]; exec 3<&-
echo x | { exec 0< f; t 'exec 0< f, -f' [ -f /dev/stdin ]; t 'exec 0< f, -p' [ -p /dev/stdin ]; }
{ r [ -p /dev/stdout ]; } | cat; echo "stdout piped -p: $(cat res)"
{ r [ -f /dev/stdout ]; } > out; echo "stdout file -f: $(cat res)"
{ r [ -p /dev/stdout ]; } > out; echo "stdout file -p: $(cat res)"
{ r [ -c /dev/stdout ]; } > /dev/null; echo "stdout null -c: $(cat res)"
x=$(r [ -p /dev/stdout ]); echo "stdout \$( ) -p: $(cat res)"
{ r [ -p /dev/stderr ]; } 2>&1 | cat; echo "stderr piped -p: $(cat res)"
{ r [ -f /dev/stderr ]; } 2> out; echo "stderr file -f: $(cat res)"
for n in 4096 65536 65537; do
	s=$(head -c $((n - 1)) /dev/zero | tr '\0' a)
	if [ -p /dev/stdin ]; then k=pipe; elif [ -f /dev/stdin ]; then k=file; else k=other; fi <<< "$s"
	echo "here-string of $n bytes: $k"
done
echo '-- > /dev/stdin'
{ echo y > /dev/stdin; } < /dev/null; echo "null: rc=$?"
echo one > g; { echo y > /dev/stdin; } < g; echo "file >: rc=$? $(cat g)"
echo one > g; { echo y >> /dev/stdin; } < g; echo "file >>: rc=$? $(cat g | tr '\n' ' ')"
echo one > g; { echo y > /dev/fd/0; } < g; echo "file > /dev/fd/0: rc=$? $(cat g)"
echo one > g; { "$BASH" -c 'echo y' > /dev/stdin; } < g; echo "a child's output: rc=$? $(cat g)"
echo one > g; "$BASH" -c 'echo y > /dev/stdin' < g; echo "inside a child: rc=$? $(cat g)"
echo one > g; { echo y < g > /dev/stdin; echo "a builtin, < g > /dev/stdin: rc=$? $(cat g)"; } < /dev/null
echo one > g; { "$BASH" -c 'echo y' < g > /dev/stdin; echo "a child, < g > /dev/stdin: rc=$? $(cat g)"; } < /dev/null
cd / && rm -rf "$d"
