# 2026-10-18: at start-up, a Git found on PATH adds its usr/bin and mingw64/bin (its MSYS tools: ssh, perl, ...) when
# they are missing. Git's root is the parent of a `cmd` or `bin` entry holding git.exe, or the grandparent of a
# `mingw64/bin` one. The mingw64 case was never reached (`bin` matched first), so a PATH with only `.../mingw64/bin`
# got no usr/bin, and the probe went on through every later entry (0.66 ms a start here). And "already on PATH"
# compared backslash paths with entries as written, so a forward-slash entry was added again.
# PPID: a child shell's $PPID is this shell's $$ (it now comes from the current-process pseudo-handle, not a
# Process object: ~0.45 ms a start). Authored (C#Bash's own start-up).
d=$(mktemp -d) && cd "$d" || exit 1
mkdir -p Git/cmd Git/bin Git/usr/bin Git/mingw64/bin tools
for f in Git/cmd Git/bin Git/mingw64/bin tools; do : > "$f/git.exe"; done
show() { PATH="$1" "$BASH" -c 'printf "%s\n" "$PATH"' | tr ';\\' '\n/' | grep -F "$d" | sed "s|$d|D|"; }
for p in "Git/cmd" "Git/bin" "Git/mingw64/bin" "tools;Git/mingw64/bin" "Git/usr/bin;Git/mingw64/bin" "tools"; do
	echo "== PATH=$p"
	show "$(printf '%s' "$p" | sed "s|^|$d/|; s|;|;$d/|g")"
done
echo "== a child's PPID"
[ "$("$BASH" -c 'echo $PPID')" = "$$" ] && echo "PPID = parent's \$\$" || echo "PPID differs: $("$BASH" -c 'echo $PPID') vs $$"
cd / && rm -rf "$d"
