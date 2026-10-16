# 2026-10-14 (koliada-net, a second session; scope ratified by the architect): `strings` as GNU strings 2.38
# (binutils). It was not a command at all (Git for Windows ships no binutils). The whole file is scanned, runs of
# at least 4 printable characters (ASCII and tab; -e S adds bytes above 127; -w all white space) one per line;
# -n/--bytes/-N, -t o|d|x and -o offsets, -f file names, -s separator, -e s|S|b|l|B|L (16- and 32-bit text,
# where Windows binaries keep theirs). Synthetic inputs only. Usage errors and the notices are in strings_local.sh
# (their text is C#Bash's). Expected output: GNU strings via WSL.
d=$(mktemp -d) && cd "$d" || exit 1
printf 'ab\0abcd\0abcde\001\177xyzw\tq\200\377high\351bytes\n\nline two  \r\nend' > a.bin
printf 'one\0two2\0three\0' > b.bin
printf 'aa\0bbbbbbbb\0ccccc\0dddddddddddddddd\0' > c.bin
printf 'H\0e\0l\0l\0o\0\0\0x\0W\0i\0d\0e\0 \0s\0t\0r\0' > le.bin
printf '\0H\0e\0l\0l\0o\0\0\0x\0W\0i\0d\0e' > be.bin
printf 'L\0\0\0o\0\0\0n\0\0\0g\0\0\0!\0\0\0' > l32.bin
printf '\0\0\0B\0\0\0i\0\0\0g\0\0\0!' > b32.bin
printf 'zA\0B\0C\0D\0\001\002E\0F\0G\0H\0I' > odd.bin
printf 'abc\0abcd\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0offset-twenty' > th.bin
: > empty.bin
mkdir dir
s() { echo "-- strings $*"; strings "$@" 2>&1; echo "rc=$?"; }
s a.bin
s -n 2 a.bin
s -3 a.bin
s --bytes=6 a.bin
s -n1 b.bin
s -t x th.bin
s -t d th.bin
s -t o th.bin
s -o th.bin
s --radix=x a.bin
s -f a.bin b.bin
s -f -t x b.bin th.bin
echo "-- strings -w a.bin | od -c"; strings -w a.bin | od -c; echo "rc=$?"
s -s '|' b.bin; echo
s --output-separator=:: b.bin; echo
echo "-- strings -e S a.bin | od -c"; strings -e S a.bin | od -c; echo "rc=$?"
s -e l le.bin
s -e b be.bin
s -e L l32.bin
s -e B b32.bin
s --encoding=l odd.bin
s -e l -t x odd.bin
s le.bin
s -a b.bin
s - b.bin
s --all -n 5 b.bin a.bin
s empty.bin
s th.bin
echo "-- stdin"; printf 'from stdin\0x\0four' | strings; echo "rc=$?"
echo "-- stdin -f"; printf 'from stdin' | strings -f; echo "rc=$?"
s nofile.bin
s dir
s a.bin nofile.bin b.bin
s -n 0 a.bin
s -n x a.bin
s -n 010 c.bin
s -n 0x5 c.bin
s -n 5b c.bin
s -12 c.bin
s -o -t x c.bin
s -t x -o c.bin
s -s "" c.bin; echo
cd / && rm -rf "$d"
