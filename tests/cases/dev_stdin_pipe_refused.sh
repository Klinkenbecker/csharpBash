# 2026-11-27 (bugs list 10's pipe case; the architect's go on Claude's position): `> /dev/stdin` when stdin is a pipe
# or a here-document is refused, "Permission denied", status 1 (it was "No such file or directory"). GNU writes into
# the pipe through /proc; Windows cannot open a pipe's writing end from its reading end, so one answer for every pipe:
# a C#Bash pipe, a here-document, a here-string, a child whose stdin is a pipe, a child's own here-string. A file and
# /dev/null are written as GNU's (case dev_fd_kind_gnu). Authored: GNU's answer is the one ruled out.
echo x | { echo y > /dev/stdin; echo "pipe: rc=$?"; } 2>&1
{ { echo y > /dev/stdin; echo "herestring: rc=$?"; } <<< x; } 2>&1
{ { echo y > /dev/stdin; echo "heredoc: rc=$?"; } << 'EOF2'
x
EOF2
} 2>&1
echo x | "$BASH" -c 'echo y > /dev/stdin; echo "child, its stdin a pipe: rc=$?"' 2>&1 | sed 's/^[^ ]*: line 1: /line 1: /'
{ "$BASH" -c 'echo y' <<< x > /dev/stdin; echo "child, its own here-string first: rc=$?"; } 2>&1
{ "$BASH" -c 'echo y' > /dev/stdin <<< x; echo "child, /dev/stdin before its here-string: rc=$?"; } < /dev/null 2>&1
