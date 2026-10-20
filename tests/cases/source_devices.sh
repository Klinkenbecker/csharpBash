# 2026-10-19 (red team, older): `source` reads a device as bash reads a file: /dev/null is empty, /dev/stdin is the
# command's stdin (a here-string, a pipe), /dev/fd/N a descriptor. They were "No such file or directory", 1.
# Expected: GNU bash 5.1 via WSL, byte-identical.
source /dev/null; echo "null rc=$?"
. /dev/null; echo "dot rc=$?"
source /dev/stdin <<< 'x=1; echo "from stdin x=$x"'; echo "stdin rc=$? x=$x"
echo 'y=2' | { source /dev/stdin; echo "piped y=$y"; }
source /dev/stdin a b <<< 'echo "args: $1 $2"'
exec 3< <(printf 'z=3\n'); source /dev/fd/3; echo "fd3 z=$z"; exec 3<&-
printf 'w=4\nreturn 5\nw=6\n' > /tmp/src_dev_$$ 2>/dev/null || printf 'w=4\nreturn 5\nw=6\n' > src_dev_$$
exec 4< "$( [ -e /tmp/src_dev_$$ ] && echo /tmp/src_dev_$$ || echo src_dev_$$ )"; source /dev/fd/4; echo "fd4 rc=$? w=$w"; exec 4<&-
rm -f /tmp/src_dev_$$ src_dev_$$
