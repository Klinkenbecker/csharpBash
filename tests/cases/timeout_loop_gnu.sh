# 2026-11-05 (koliada-net-forms-cb): a `timeout` that expired on an in-process command left the shell's interrupt
# raised, so the next loop pass threw "Unhandled Exception: Bash.Evaluator.InterruptException": a .NET stack trace,
# exit 9 (134 here), the rest of the script never ran. Each expiry is 124 and the script goes on, as GNU's.
# Expected: GNU via WSL (from tests/: wsl.exe -e bash cases/timeout_loop_gnu.sh < /dev/null > expected/timeout_loop_gnu.out)
d=/mnt/c/Windows; [ -d "$d" ] || d=C:/Windows
for i in 1 2; do timeout 0.3 find "$d" -name nosuch-zz 2>/dev/null; echo "find $i: rc=$?"; done
for i in 1 2 3; do timeout 0.2 yes > /dev/null; echo "yes $i: rc=$?"; done
n=0; while [ $n -lt 3 ]; do n=$((n+1)); done; echo "a while loop after: $n"
echo "a substitution after: $(for j in a b; do printf %s $j; done)"
echo done
