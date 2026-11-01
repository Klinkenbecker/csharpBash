# 2026-10-31 (found fixing installer-a4's kill report; raised, waited): `wait $!` for a background job that had ended and
# left the job table (dropped by `jobs`, by another `wait`, or by a plain `wait`) said "wait: N: no such job", 127. GNU
# keeps the statuses of the ended children (bgpids): 3 for a job that ended earlier, 5 after `jobs` reported it, 5
# again on a second ask, 7 after a plain `wait`, 9 after `wait -n`. A pid it never started: "pid N is not a child of
# this shell", 127; a %spec it does not have: "%9: no such job".
# Expected: GNU bash 5.1 via WSL (pids shown as PID).
t=$(mktemp)
w() { wait "$@" 2>"$t"; local rc=$?; sed -E 's/^.*: line [0-9]+: //; s/[0-9]{4,}/PID/g' "$t"; return $rc; }
(exit 3) & a=$!
sleep 30 & b=$!
sleep 2
kill $b; w $b; echo "wait for a killed job: $?"
w $a; echo "wait for a job that finished earlier: $?"
(exit 5) & c=$!; sleep 1; jobs > /dev/null
w $c; echo "wait after jobs reported it Done: $?"
w $c; echo "wait for it again: $?"
w 2147483000; echo "wait for a pid never started: $?"
(exit 7) & d=$!; sleep 1
w; echo "wait with no operand after it finished: $?"
w $d; echo "wait \$! after a plain wait: $?"
(exit 9) & e=$!; sleep 1
wait -n; echo "wait -n: $?"
w $e; echo "wait \$! after wait -n: $?"
w %9; echo "wait for a job spec it does not have: $?"
rm -f "$t"
