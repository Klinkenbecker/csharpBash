# 2026-10-12: `jobs -x command [args]` runs the command with each `%` word that names a job replaced by the
# job's process group, as bash 5.1 (it was refused). In a script bash gives the shell's own group, `$$` here;
# interactive (job control on) it gives the job's leader, `$!`. A word naming no job passes through; `-x`
# must come before -l/-p/-n. Not covered: `%name` / `%?text` (C#Bash's job lookup has neither, for kill,
# wait and fg too). Expected output: GNU bash via WSL.
f=$(mktemp)
sleep 3 & sleep 3 | cat &
x() { jobs -x "$@" > "$f" 2>&1; local rc=$?; sed "s/^.*line [0-9]*: //; s/$$/SHELL/g" "$f"; echo "rc=$rc"; }
x echo %1 %2
x echo %% %+ %- x
x echo pre%1 "%1"
x echo %9
x
x -l echo %1
x printf '<%s>\n' a %1 -l
x nosuchcmd %1
x false
for o in -xl -xn -lx -nx -px; do jobs $o echo %1 > "$f" 2>&1; echo "$o rc=$? $(sed "s/^.*line [0-9]*: //; s/$$/SHELL/g" "$f")"; done
kill %1 %2; wait
x echo %1
"$BASH" --norc -i -c 'sleep 2 & b=$!; jobs -x echo %1 > "$1"; [ "$(cat "$1")" = "$b" ] && echo "interactive: the job pid"; kill %1' _ "$f" 2>/dev/null < /dev/null
rm -f "$f"
