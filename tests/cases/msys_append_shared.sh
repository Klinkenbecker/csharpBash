# 2026-10-19 (red team F3): an MSYS program whose stdout and stderr both append to one file keeps both lines whole.
# MSYS cannot write through an append-only handle, so it got a write handle at the end for each `>>`: two handles at
# the same offset, and the second write overwrote the first ("ls: cannot access ..." lost its first 10 bytes). Now
# one handle per file. Needs MSYS's ls.exe (cases/msys_pin.inc). Authored: the lines GNU bash keeps under O_APPEND
# (both whole, in the order ls writes them).
. cases/msys_pin.inc
M=$MSYS_BIN/ls.exe
d=$(mktemp -d) && cd "$d" || exit 1
touch present
"$M" -d present nofile >> o 2>> o; echo "direct: rc=$?"
{ "$M" -d present nofile; } >> p 2>> p
printf '"%s" -d present nofile\n' "$M" > inner.sh
"$BASH" inner.sh >> q 2>> q
for f in o p q; do echo "$f:"; cat "$f"; done
cd / && rm -rf "$d"
