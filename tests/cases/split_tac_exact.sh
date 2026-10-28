# 2026-10-27: split and tac on the shared byte layer, as GNU. split writes each piece as it reads (it read the whole
# input first, which failed over 2 GiB; checked by hand on a 2 GiB file: 1 GiB, 1 GiB, 16 bytes, joined = the file):
# pieces by lines and bytes around a 64 KiB block's edge, suffixes, stdin, no final newline, a line longer than a
# block. tac reads a file from its end and copies a pipe to a temporary file first; a last line without a newline
# stays without one (`a\nb`: ba\n; it was b\na\n). Each split run: its pieces' names, sizes and sums. Expected: GNU
# coreutils 8.32 via WSL. Past its suffix space (2026-10-27; it wrapped to xaa and overwrote the first pieces): without
# -a the suffix widens (xyz, xzaaa; -d: x89, x9000, x9899, x990000), with -a (or --suffix-length) split stops,
# "output file suffixes exhausted", rc 1, the pieces before it whole; -a 0 is the default; -a x / -1 / '' / 3x refused.
# Each such run: rc, the count, four names from the given position, the last, and the sum of all pieces joined.
cd "${TMPDIR:-${TEMP:-/tmp}}" || exit 1
d="split-tac-$$"; mkdir -p "$d" && cd "$d" || exit 1
seq 1 40000 > s; printf 'a\nb\nc' > nonl; : > empty; { head -c 300000 /dev/zero | tr '\0' y; echo; seq 1 5; } > longl
pieces() { for f in out/*; do [ -f "$f" ] && echo "${f#out/} $(wc -c < "$f") $(md5sum < "$f" | cut -c1-12)"; done; }
run() { rm -rf out; mkdir out; ( cd out && split "$@" 2>&1 ); echo "rc=$? pieces=$(ls out | wc -l)"; pieces | head -n 4; pieces | tail -n 1; }

for a in "-l 300 ../s" "../s" "-b 1000 ../s" "-b 65536 ../s" "-b 65537 ../s" "-b 1 ../nonl" "-d -a 3 -l 5000 ../s" \
         "--additional-suffix=.txt -l 20000 ../s" "-l 1 ../nonl" "../empty" "-l 2 ../longl" "-b 70000 ../longl" "-l 7000 ../s pre"; do
	echo "== split $a"; run $a
done
echo "== split stdin"; rm -rf out; mkdir out; ( cd out && seq 1 25 | split -l 10 ); pieces

seq 1 700 > k; seq 1 1000 > t; seq 1 100 > h; seq 1 101 > h2; head -c 100 /dev/zero > z
many() { at=$1; shift; rm -rf out; mkdir out; ( cd out && split "$@" 2>&1 )
	echo "rc=$? pieces=$(ls out | wc -l) $(ls out | sed -n "$at,$((at + 3))p" | tr '\n' ' ')last=$(ls out | tail -n 1) all=$(cat out/* | md5sum | cut -c1-12)"; }
for a in "649 -l 1 ../k" "675 -l 1 -a 2 ../k" "25 --suffix-length=1 -l 1 ../k" "89 -d -l 1 ../t" "989 -d -l 1 ../t" \
         "99 -d -a 2 -l 1 ../h2" "89 -a 0 -d -l 1 ../h" "89 --additional-suffix=.t -d -l 1 ../h" "89 -d -b 1 ../z" "1 -a 3 -l 1 ../k"; do
	echo "== split $a"; many $a
done
echo "== split widen stdin"; rm -rf out; mkdir out; ( cd out && seq 1 100 | split -d -l 1 ); echo "$(ls out | wc -l) $(ls out | tail -n 1)"
for a in x -1 '' 3x ' 3'; do
	rm -rf out; mkdir out; ( cd out && LC_ALL=C split -a "$a" -l 300 ../s 2>&1 ); echo "-a '$a' rc=$? $(ls out | head -n 1) $(ls out | wc -l)"
done

for f in s nonl empty longl; do
	echo "== tac $f: $(tac $f | md5sum | cut -c1-16) $(cat $f | tac | md5sum | cut -c1-16) $(tac $f | head -c 12 | od -An -c | tr -s ' ')"
done
echo "== tac several"; tac nonl s | head -n 3

cd .. && rm -rf "$d"
