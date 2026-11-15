# 2026-11-14 (the architect: `access-time = default | preserve` in %USERPROFILE%/.bash/etc/csharpBash.config): on NTFS,
# reading a file's details (stat, ls -l: Windows has no execute bit, so C#Bash, like MSYS, reads a file's first bytes for
# `#!`) moved its access time, as under Git Bash. `preserve` makes those reads leave it alone; a real read (cat) still
# moves it; `default` (or no line) is Windows' way. Authored (Windows-specific). The years are printed, never times.
P=$(mktemp -d) || exit 1
mkdir -p "$P/.bash/etc" "$P/w" && cd "$P/w" || exit 1
printf 'x\n' > f; touch -m -d '2024-01-01' f
year() { ls -u --full-time f | awk '{print substr($6,1,4)}'; }
reset() { touch -a -d '2020-01-01' f; }
reset; cat f > /dev/null
if [ "$(year)" = 2020 ]; then echo "SKIP: this volume does not update access times"; cd /; rm -rf "$P"; exit 0; fi
run() { USERPROFILE="$P" "$BASH" -c "$1" < /dev/null; }
for mode in "" "access-time = default" "access-time = preserve"; do
	printf '%s\n' "$mode" > "$P/.bash/etc/csharpBash.config"
	echo "== ${mode:-no line}"
	for op in "stat f" "stat -c %n f" "ls -l f" "cat f"; do reset; run "$op > /dev/null"; echo "$op: $(year)"; done
done
printf 'access-time = sometimes\n' > "$P/.bash/etc/csharpBash.config"
echo "== a bad value: warned, default kept"
reset; run "stat f > /dev/null" 2>&1 | sed "s|$P|P|"; echo "stat f: $(year)"
cd / && rm -rf "$P" && echo removed
