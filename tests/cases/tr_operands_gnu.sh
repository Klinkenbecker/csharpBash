# 2026-11-27 (bugs list 19, found fixing 13; and 20, found fixing 19): tr takes every word after its first operand as an
# operand (`tr a -d` maps a to -), as GNU's; GNU's operand rules and words (a third operand, a second with -d alone,
# the missing-operand lines); and -s with two sets squeezes a run of any character of the last set after translating
# (`tr -s a b` of "aab" gives "b"; C#Bash gave "bb"). Expected = GNU coreutils 8.32 via WSL in the C locale, whose
# quotes are C#Bash's (a UTF-8 locale makes GNU quote with ‘ ’).
t() { printf '%s: ' "$*"; printf 'a-b c\n' | tr "$@" 2>&1 | head -2 | tr '\n' '|'; echo " rc=${PIPESTATUS[1]}"; }
t a -d
t a "- x"
t -d a
t -d a -s
t -s a -d
t a b -c
t -- -d x
t a-b x
t -d -
t '\-' x
printf 'aab\n' | tr -s a b; echo "rc=$?"
printf 'a-b c\n' | tr -d a -s 2>&1; echo "rc=$?"
printf 'a-b c\n' | tr a b -c 2>&1; echo "rc=$?"
printf 'a-b c\n' | tr -d a b 2>&1; echo "rc=$?"
printf 'a\n' | tr 2>&1; echo "rc=$?"
printf 'a\n' | tr a 2>&1; echo "rc=$?"
for a in "-d -s a" "-d -s a b c" "-s a b c" "-d a b c" "-c a" "-t a" "-s" "-d"; do printf '%s\n' "== tr $a"; printf 'aab\n' | tr $a 2>&1; echo "rc=$?"; done
printf 'aab cc\n' | tr -cs 'a' 'X'; printf 'aab cc\n' | tr -s 'a-z' 'A-Z'
