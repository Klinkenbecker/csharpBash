# 2026-10-30 (kes-term-a1's report): find -printf takes flags, a width and a precision before any directive, as GNU's:
# the text cut to the precision, then padded with spaces (`%05s` too: GNU pads a %s with spaces), on the right under
# `-`. They printed literally (`[%10s]`). Expected: GNU findutils 4.8 via WSL, LC_ALL=C.
export LC_ALL=C
cd "${TMPDIR:-${TEMP:-/tmp}}" || exit 1
d="find-printf-$$"; mkdir -p "$d/sub" && cd "$d" || exit 1
printf x > f; printf abcdef > sub/longername
find . -maxdepth 1 -name f -printf '[%s][%f]\n'
find . -maxdepth 1 -name f -printf '[%10s][%-6f][%05s][%.1f][%-4s|]\n'
find . -name longername -printf '[%-15f][%15p][%.3f][%3d][%-3d]\n'
find . -name longername -printf '[%8s][%-8y][%.0f]\n'
cd .. && rm -rf "$d"
