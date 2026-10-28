# 2026-10-19 (red team F3): an MSYS program whose stdout and stderr both append to one file keeps both lines whole.
# MSYS cannot write through an append-only handle, so it got a write handle at the end for each `>>`: two handles at
# the same offset, and the second write overwrote the first ("ls: cannot access ..." lost its first 10 bytes). Now
# one handle per file. Needs MSYS2's ls.exe at F:/Tools/msys64 (an MSYS2 install, not Git). Authored: the lines GNU
# bash keeps under O_APPEND (both whole, in the order ls writes them).
M=F:/Tools/msys64/usr/bin/ls.exe
[ -f "$M" ] || { echo "SKIP: needs MSYS2's ls.exe at $M"; exit 0; }
d=$(mktemp -d) && cd "$d" || exit 1
touch present
"$M" -d present nofile >> o 2>> o; echo "direct: rc=$?"
{ "$M" -d present nofile; } >> p 2>> p
printf '"%s" -d present nofile\n' "$M" > inner.sh
"$BASH" inner.sh >> q 2>> q
for f in o p q; do echo "$f:"; cat "$f"; done
cd / && rm -rf "$d"
