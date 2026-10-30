# 2026-10-30 (the "match GNU" item, found 2026-10-27): split refuses a bad count or size, as GNU does (it made one
# piece and returned 0 for -l x, 0, -1 and '', and -b x fell through to a PATH split). Expected: GNU coreutils 8.32
# via WSL. (-n, chunk mode, is not implemented: any -n falls through to a PATH split, as before; not probed.)
export LC_ALL=C   # plain quotes in the messages, as Claude Code runs its shell (no LANG)
cd "${TMPDIR:-${TEMP:-/tmp}}" || exit 1
d="split-gnu-$$"; mkdir -p "$d" && cd "$d" || exit 1
printf 'a\nb\nc\n' > in
try() { split "$@" in p- 2>&1; echo "rc=$?, pieces: $(ls p-* 2>/dev/null | tr '\n' ' ')"; rm -f p-*; }
for v in x 0 -1 '' 2x 1.5; do echo "--- -l '$v'"; try -l "$v"; done
for v in x 0 -1 '' 2Q 1.5; do echo "--- -b '$v'"; try -b "$v"; done
echo "--- -l 2 (good)"; try -l 2
echo "--- -b 2K (good)"; try -b 2K
cd .. && rm -rf "$d"
