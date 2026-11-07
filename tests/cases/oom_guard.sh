# 2026-11-05 (the open list's OOM guard; raised 2026-11-03 with diff's n x m table): a builtin that runs out of memory
# took the whole shell down: an unhandled OutOfMemoryException, a .NET stack trace, exit 134, every later command and
# pipeline stage lost. Now it ends as a GNU tool does, "NAME: memory exhausted", status 2, and the script goes on.
# The OOM is a request past .NET's largest string (printf's width 2,000,000,000), refused before anything is allocated.
# Expected: authored (GNU's printf would write 2 GB here; the point is the shell's survival and GNU's words).
exec 2>&1   # the runner compares stdout only: the messages are the point
echo before
printf '%2000000000s' x > /dev/null; echo "printf: rc=$?"
x=$(printf '%2000000000s' x); echo "in \$( ): rc=$? len=${#x}"
printf '%2000000000s' x | cat > /dev/null; echo "a pipeline stage: ${PIPESTATUS[0]} ${PIPESTATUS[1]}"
awk 'BEGIN { printf "%2000000000s", "x" }' > /dev/null; echo "awk: rc=$?"
f() { printf '%2000000000s' x; echo "the function goes on: rc=$?" >&2; }; f > /dev/null; echo "after f: rc=$?"
echo after
