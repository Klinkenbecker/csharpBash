# 2026-11-16 (git-9d's report): an output redirection onto fd 0 (`0> f`, `0>> f`, `0>/dev/null`) opens f on fd 0 for
# writing, so stdin cannot be read; stdout is untouched. For anything C#Bash runs in-process (a builtin, `env`, a
# function, a group) it was installed as STDOUT: `env X=1 git hash-object --stdin 0> f` wrote git's hash into f, and
# `echo kept 0>/dev/null` printed nothing. A read of that stdin fails "Bad file descriptor" in bash (and in C#Bash since
# 874, case bad_descriptor_gnu), so messages and statuses are dropped here. Expected = GNU
# bash 5.1 via WSL.
d=$(mktemp -d) && cd "$d" || exit 1
printf 'x\n' > in
show() { printf '%s: wo=[%s] out=[%s]\n' "$1" "$(cat wo 2>/dev/null)" "$(cat out 2>/dev/null)"; rm -f wo out; }
{ env X=1 cat 0> wo; } < in > out 2>/dev/null; show "env, a builtin"
{ env X=1 "$BASH" -c cat 0> wo; } < in > out 2>/dev/null; show "env, a program"
{ cat 0> wo; } < in > out 2>/dev/null; show "a builtin"
f() { cat; }; { f 0> wo; } < in > out 2>/dev/null; show "a function"
{ { cat; } 0> wo; } < in > out 2>/dev/null; show "a group"
printf 'old\n' > wo; { env X=1 cat 0>> wo; } < in > out 2>/dev/null; show "appended"
{ echo kept 0>/dev/null; } > out; show "to /dev/null"
true 0> made; [ -f made ] && echo "the file is created"
cd / && rm -rf "$d"
