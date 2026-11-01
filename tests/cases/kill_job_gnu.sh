# 2026-10-31 (installer-a4's report): `kill -0 $!` on a FINISHED background job said it was running (rc 0), even after
# `jobs` had shown it Done; `kill -0` looked nothing up. GNU: `$!` names a process, so once its job has finished there
# is none: "kill: (PID) - No such process", rc 1, for kill and kill -0 alike, reported by `jobs` or not; a %spec names
# the job, found until `jobs` reports it Done and drops it ("%1: no such job" after). And `kill $!` from a subshell,
# the watchdog idiom `( sleep 1; kill $p ) &`, said "no such job" and the job ran on: C#Bash's `$!` is a synthetic job
# pid (by design), now resolved through the parent's jobs as a real pid is reachable from any subshell.
# Expected: GNU bash 5.1 via WSL (pids shown as PID).
t=$(mktemp)
k() { "$@" 2>"$t"; local rc=$?; sed -E 's/^.*: line [0-9]+: //; s/\(-?[0-9]+\)/(PID)/' "$t"; return $rc; }
sleep 1 & P=$!
sleep 3
k kill -0 $P; echo "kill -0 \$! on a finished job: rc=$?"
k kill $P; echo "kill \$! on a finished job: rc=$?"
k kill -0 %1; echo "kill -0 %1 on a finished job: rc=$?"
k kill %1; echo "kill %1 on a finished job: rc=$?"
jobs > /dev/null
k kill -0 $P; echo "kill -0 \$! after jobs reported it: rc=$?"
k kill -0 %1; echo "kill -0 %1 after jobs reported it: rc=$?"
sleep 30 & Q=$!
k kill -0 $Q; echo "kill -0 \$! on a running job: rc=$?"
k kill $Q; echo "kill \$! on a running job: rc=$?"
wait $Q; echo "wait: rc=$?"
k kill -0 $Q; echo "kill -0 \$! after kill and wait: rc=$?"
echo "== from a subshell"
sleep 30 & p=$!
( kill -0 $p; echo "kill -0 \$! in a subshell: rc=$?" )
kill -0 %% 2>&1 | sed -E 's/^.*: line [0-9]+: //'; echo "kill -0 %% in a pipeline stage (it knows no jobs): rc=${PIPESTATUS[0]}"
( sleep 1; kill $p; echo "the watchdog's kill: rc=$?" ) &
wait $p; echo "the job was killed: wait rc=$?"
wait
rm -f "$t"
