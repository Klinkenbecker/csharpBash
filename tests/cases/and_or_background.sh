# 2026-10-12: `a && b &` and `a || b &` background the whole and-or list, as bash: one job runs it, and its
# `cd` and assignments stay in that job. C#Bash backgrounded only the last pipeline, so `sleep 1 && echo A &`
# slept in the foreground and `cd / && true &` moved the shell itself. Expected output: GNU bash via WSL.
sleep 1 && echo A & sleep 2 | cat && echo B & jobs; wait
d=$PWD
cd / && true &
wait; [ "$PWD" = "$d" ] && echo "1 cd stayed in the job" || echo "1 cd leaked to the shell"
cd "$d"
v=1 && true & wait; echo "2 [${v-unset}]"
false || w=2 & wait; echo "3 [${w-unset}]"
true && u=3 || true & wait; echo "4 [${u-unset}]"
true && false || echo "5 ran in the job" & wait
x=0; false && x=1 & wait; echo "6 [$x]"
sleep 0 && true & echo "7 status after starting it: $?"
echo first & true && y=8 & wait; echo "8 [${y-unset}]"
z=0; true & z=9 && true; echo "9 [$z] (a foreground chain after a job)"
echo done
