# 2026-11-05 (the 2026-11-01 spike, built: koliada-net-73's `grep -f <(hg)` and the 2026-10-10 loop limitation): in a
# pipeline stage, a native program's stdin is fed from the stage's input, and what it did not read is GIVEN BACK to
# the stage for the next command, as a shared OS pipe leaves it in bash and Git Bash. It was copied to the end and
# lost: `printf zz | grep -v -f <(cmd ...)` printed nothing, a loop calling cmd ran once. Expected: Git Bash.
echo "1 a native program in <( ) does not eat the stage's input: $(printf 'zz\n' | grep -v -F -f <(cmd //c echo nomatch))"
echo "2 cmd reads nothing, then cat: [$(printf 'zz\n' | { cmd //c echo x > /dev/null; cat; })]"
echo "3 100000 lines past cmd: $(seq 1 100000 | { cmd //c echo x > /dev/null; wc -l; })"
echo "4 sort.exe reads all, then cat: [$(printf 'a\nb\n' | { C:/Windows/System32/sort.exe > /dev/null; cat; })]"
echo "5 set /p reads part, then cat: [$(printf 'a\nb\nc\n' | { cmd //c "set /p x=& echo got %x%" | tr -d '\r'; cat; } | tr '\n' ' ')]"
echo "6 a slow producer: [$({ printf 'a\n'; sleep 1; printf 'b\n'; } | { cmd //c echo x > /dev/null; cat; } | tr '\n' ' ')]"
echo "7 a loop calling cmd: [$(printf '1\n2\n3\n' | while read -r l; do cmd //c echo "l=$l" | tr -d '\r'; done | tr '\n' ' ')]"
