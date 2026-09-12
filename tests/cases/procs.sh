# P5 follow-up (ratified 2026-09-04): in-process pgrep / pkill / ps.
# A cmd.exe running ping is the guinea pig; the pattern is written so that no ancestor
# shell's command line can match it (the literal text differs from what runs).
n=30
cmd.exe /c "ping -n $n 127.0.0.1 >nul" &
sleep 1

echo "--- pgrep"
pgrep -f "cmd.exe.*ping -n 3[0]" | grep -c .
pgrep -c -f "ping -n 3[0]"
pgrep -x ping | grep -c .
pgrep -l -x ping | awk '{print $2}'
pgrep -x nonexistent-process-xyz; echo "rc=$?"
pgrep 2>/dev/null; echo "rc=$?"
pgrep -x ping -P 1; echo "rc=$?"

echo "--- ps"
ps -p $$ -o pid= | grep -c .
ps -o pid,comm -p $$ | head -1
ps -ef | head -1
[ "$(ps aux | wc -l)" -gt 5 ] && echo many
ps -p 999999 >/dev/null; echo "rc=$?"
out=$(BASH_COREUTILS=builtin ps --forest 2>&1); echo "$out" | head -1

echo "--- pkill"
pkill -f "ping -n 3[0]"; echo "rc=$?"
pkill -TERM -x ping; echo "rc=$?"
sleep 1
pgrep -x ping; echo "rc=$?"
pkill -x nonexistent-process-xyz; echo "rc=$?"
out=$(BASH_COREUTILS=builtin pgrep --uid 5 x 2>&1); echo "$out" | head -1
wait
echo done
