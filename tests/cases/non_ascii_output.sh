# 2026-10-05: non-ASCII text written by builtins straight to the shell's stdout. With no console
# (Claude Code's Bash tool; this suite's -Detached mode) it went out in the ANSI code page, so
# `echo 'é — ✓'` reached Claude as "� � ?". Pipes and files were already UTF-8. Expected = GNU (WSL).
s='é — ✓ 日本 😀'
echo "echo: $s"
printf 'printf: %s\n' "$s"
printf '%s\n' "$s" | sed 's/^/sed: /'
printf '%s\n' "$s" > f.txt; head -1 f.txt; grep -o '日本' f.txt; awk '{print "awk:", $3}' f.txt
echo "length: ${#s}"   # characters, not UTF-16 units: the emoji is one (it counted 2)
echo "substrings: [${s:9:1}] [${s: -1}] [${s:6:2}] [${s:2:3}] [${s:8}]"
a=(x '😀y'); echo "element length: ${#a[1]}"
echo "error to stdout: $(echo 'é' >&2 2>&1)" 2>&1
rm -f f.txt
