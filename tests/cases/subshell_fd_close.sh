# 2026-10-19 (red team F1): a descriptor CLOSED in a subshell is closed there only; the parent's stays open. C#Bash
# disposed the parent's stream: its later `echo >&3` said "Bad file descriptor" and the line was lost, and
# `read -u` failed with a .NET message. Expected: GNU bash 5.1 (WSL), byte-identical.
d=$(mktemp -d) && cd "$d" || exit 1
exec 3> fa
( exec 3>&- ); echo A >&3
x=$(exec 3>&-); echo B >&3
( exec 3>&- ) | cat; echo C >&3
( exec 3>&- ) & wait; echo D >&3
{ exec 3>&-; } | cat; echo E >&3
f() ( exec 3>&- ); f; echo F >&3
exec 3>&-
echo "fa: $(tr '\n' ' ' < fa)"
printf 'one\ntwo\n' > fe
exec 4< fe
( exec 4<&- ); read -u 4 v; echo "v=$v"
( exec 4<&- ) | cat; read -u 4 w; echo "w=$w"
exec 4<&-
# the shell that opened a descriptor still closes it
exec 5> fb; echo x >&5; exec 5>&-; echo y 2>/dev/null >&5 || echo "fd 5 closed: rc=$?"
echo "fb: $(cat fb)"
# a subshell closes what it opened itself
( exec 6> fc; echo in >&6; exec 6>&-; echo out 2>/dev/null >&6 || echo "fd 6 closed in the subshell" ); cat fc
cd / && rm -rf "$d"
