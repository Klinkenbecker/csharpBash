# 2026-10-19 (red team F6): realpath and readlink -f/-e/-m check components as GNU does (all but the last must exist
# and be directories for -f and realpath's default; all for -e; none for -m; a trailing slash needs a directory and
# is dropped). They checked nothing: `readlink -f nodir/x` printed a path, rc 0, and kept trailing slashes.
# Expected: GNU coreutils 8.32 via WSL, byte-identical.
d=$(mktemp -d) && cd "$d" || exit 1
mkdir -p a/b; touch file a/b/f
for p in nodir/x file/ file/x nodir/../file file/.. file/. a/b/ a//b/ missing missing/ a/missing/ a/b/f/ . a/./b ./a/../a/b; do
  for m in -f -e -m; do out=$(readlink $m "$p" 2>&1); rc=$?; printf 'readlink %s %-14s -> [%s] %s\n' "$m" "$p" "${out//$d/B}" "$rc"; done
  for m in "" -e -m; do out=$(realpath $m "$p" 2>&1); rc=$?; printf 'realpath %-2s %-14s -> [%s] %s\n' "$m" "$p" "${out//$d/B}" "$rc"; done
done
cd / && rm -rf "$d"
