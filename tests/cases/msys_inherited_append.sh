# 2026-10-17: an MSYS program (Git's usr/bin) inside `Bash.exe s.sh >> out` writes to out. The nested shell inherits
# an append-only handle (C#Bash's `>>` gives one, so concurrent appenders interleave) and passed it on unchanged; MSYS
# takes an append-only handle as unwritable: "write error: Bad file descriptor", the line lost. Now an MSYS child gets
# that handle reopened for writing at the end, once (stdout and stderr share it under 2>&1). Authored (needs Git;
# SKIP without it: until 2026-10-20 it printed the expected lines itself, a pass that could not fail).
mcat=$(type -P cat 2>/dev/null)
case "$mcat" in *usr/bin/cat*) ;; *) echo "SKIP: needs an MSYS cat on PATH (Git's usr/bin)"; exit 0 ;; esac
d=$(mktemp -d) && cd "$d" || exit 1
printf 'echo before\necho msys-line | "%s"\necho after\n' "$mcat" > s.sh
"$BASH" s.sh >> out; echo "out=[$(tr '\n' '|' < out)]"
printf 'echo before\necho msys-out | "%s" - /nonexistent\necho after\n' "$mcat" > t.sh
"$BASH" t.sh >> both 2>&1; echo "both=[$(sed 's|^.*cat\(\.exe\)\{0,1\}: |cat: |' both | tr '\n' '|')]"
cd / && rm -rf "$d"
