# 2026-10-20: what reaches the PATH awk, moved here from awk_unsupported_once (2026-10-18) and awk_getline (2026-10-12)
# so that a missing PATH awk skips only these lines. A construct outside the in-process subset that is visible in the
# program text (a literal printf/sprintf format with %e %g, a literal RS, system()) is refused before anything runs,
# so the PATH awk runs the program once; a program that falls through (`cmd | getline`) or BASH_COREUTILS=external
# gets -v BINMODE=3 and keeps CR. SKIP without a PATH awk. Expected: the "first" counts authored, the CR counts GNU
# awk's (via WSL).
type -P awk > /dev/null || { echo "SKIP: needs a PATH awk"; exit 0; }
once() { local out; out=$(eval "$1" 2>&1); printf '%-24s first x%s\n' "$2" "$(grep -c '^first$' <<< "$out")"; }
once 'awk "BEGIN { print \"first\"; printf \"%e\n\", 1 }"' 'literal %e'
once 'awk "BEGIN { print \"first\"; x = sprintf(\"%G\", 2.5); print x }"' 'literal sprintf %G'
once 'printf "a,b\n" | awk "BEGIN { print \"first\"; RS = \",\" } { print }"' 'literal RS'
once 'awk "BEGIN { print \"first\"; system(\"\") }"' 'system()'
d=$(mktemp -d) && cd "$d" || exit 1
printf 'a\r\nb\r\nc\r\n' > crlf.txt
cr() { tr -cd '\r' | wc -c | tr -d ' '; }
echo "fall-through keeps CR: $(awk '{ "echo x" | getline y; print }' crlf.txt | cr)"
echo "BASH_COREUTILS=external keeps CR: $(BASH_COREUTILS=external awk '{ print }' crlf.txt | cr)"
cd / && rm -rf "$d"
