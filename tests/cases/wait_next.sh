# 2026-10-10: `wait -n` waits for the NEXT job to finish and returns its status (127 with none);
# `wait -n ID...` for the next of those; `-p VAR` names the job. -n and -p were accepted and ignored,
# so `wait -n` did not wait at all (a pool of jobs ran unbounded). Gaps of a second keep the order
# fixed whatever a child's start-up costs. Expected output: GNU bash via WSL.
{
"$BASH" -c 'sleep 1.2; exit 3' & a=$!
"$BASH" -c 'sleep 0.2; exit 5' & b=$!
wait -n; echo "first rc=$?"
wait -n; echo "second rc=$?"
wait -n; echo "none rc=$?"
"$BASH" -c 'sleep 0.3; exit 7' & c=$!
wait -n -p who; echo "p rc=$? who=$([ "$who" = "$c" ] && echo c || echo other)"
"$BASH" -c 'exit 2' & d=$!; "$BASH" -c 'sleep 1.0; exit 9' & e=$!
wait -n "$e"; echo "e rc=$?"
wait; echo "all rc=$?"
jobs -l | wc -l | tr -d ' '
sleep 0.1 & wait -n; jobs -l | wc -l | tr -d ' '
} 2>&1
