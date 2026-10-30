# 2026-10-30 (the "match GNU" item, found 2026-10-27): od's addresses after -j count from the start of the input, not
# from after the skip; -j past the end, across files and from a pipe. Expected: GNU coreutils 8.32 via WSL.
cd "${TMPDIR:-${TEMP:-/tmp}}" || exit 1
d="od-gnu-$$"; mkdir -p "$d" && cd "$d" || exit 1
printf 'abcdefghijklmnopqrstuvwxyz' > f; printf '0123456789' > g
r() { "$@" 2>&1; echo "rc=$?"; }

echo "--- -j 5";            r od -j 5 f
echo "--- -j 5 -N 4 -c";    r od -j 5 -N 4 -c f
echo "--- -A x -j 16";      r od -A x -j 16 f
echo "--- -A n -j 2 -c";    r od -A n -j 2 -c f
echo "--- -j 0x10 -c";      r od -j 0x10 -c f
echo "--- -t x1 -j 20";     r od -t x1 -j 20 f
echo "--- -j across files"; r od -j 24 -c f g
echo "--- -j past the end"; r od -j 40 f
echo "--- from a pipe";     printf 'abcdefghij' | { od -j 3 -c 2>&1; echo "rc=$?"; }
echo "--- -j 8 -w4 -c";     r od -j 8 -w4 -c f
cd .. && rm -rf "$d"
