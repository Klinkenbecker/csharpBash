# 2026-10-12: `jobs` shows a background job's command as bash 5.1 prints it: brace groups and subshells on one
# line, for/if/case on several, assignments and redirections kept (1>&2), a pipeline's ! not shown. C#Bash
# showed node types (BraceGroup, ForCommand) and dropped assignments and redirections. Expected: GNU via WSL.
{ sleep 5; echo hi; } > /dev/null &
( sleep 5; true ) &
for i in 1; do sleep 5; done &
while false; do :; done; sleep 5 &
if true; then sleep 5; fi &
case a in a) sleep 5;; esac &
{ if true; then sleep 5; fi; } &
sleep 5 | cat && true &
f() { sleep 5; }; f &
x=1 sleep 5 &
sleep 5 >&2 2>/dev/null &
[[ -n x ]] && sleep 5 &
time sleep 5 &
! sleep 5 &
jobs
kill %1 %2 %3 %4 %5 %6 %7 %8 %9 %10 %11 %12 %13 2>/dev/null; wait 2>/dev/null
