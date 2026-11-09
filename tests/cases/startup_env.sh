# 2026-11-09 (the architect: "remove the git search entirely"): start-up adds nothing to PATH for a Git found on it.
# Until then a `cmd`, `bin` or `mingw64/bin` entry holding git.exe named a Git root and its usr/bin and mingw64/bin were
# put first (2026-09-04; the probe fixed 2026-10-18), and every start looked for git.exe in each PATH entry (~2 ms with
# 48 entries and no Git). Fake Git trees on PATH: every PATH comes back as given. Red on 704 (it added them).
# PPID: a child shell's $PPID is this shell's $$ (it now comes from the current-process pseudo-handle, not a
# Process object: ~0.45 ms a start). Authored (C#Bash's own start-up).
d=$(mktemp -d) && cd "$d" || exit 1
mkdir -p Git/cmd Git/bin Git/usr/bin Git/mingw64/bin tools
for f in Git/cmd Git/bin Git/mingw64/bin tools; do : > "$f/git.exe"; done
# $PATH's entries are MSYS form, `:`-separated (2026-10-31): D is $d in that form
case $d in ?:*) m="/$(printf %s "${d:0:1}" | tr A-Z a-z)${d:2}";; *) m=$d;; esac
show() { PATH="$1" "$BASH" -c 'IFS=:; for e in $PATH; do printf "%s\n" "$e"; done' | grep -F "$m" | sed "s|$m|D|"; }
for p in "Git/cmd" "Git/bin" "Git/mingw64/bin" "tools;Git/mingw64/bin" "Git/usr/bin;Git/mingw64/bin" "tools"; do
	echo "== PATH=$p"
	show "$(printf '%s' "$p" | sed "s|^|$d/|; s|;|;$d/|g")"
done
echo "== a child's PPID"
[ "$("$BASH" -c 'echo $PPID')" = "$$" ] && echo "PPID = parent's \$\$" || echo "PPID differs: $("$BASH" -c 'echo $PPID') vs $$"
cd / && rm -rf "$d"
