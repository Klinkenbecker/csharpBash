# MANUAL CHECK (not in the suite: the shell under test must exit while its probe runs on).
# Usage: <a C#Bash> tests/detach/survive.sh <C#Bash under test>     ~2 min. DECISIONS 2026-10-04.
# Survival spike 2 (after the redirect rewrite). $1 = the shell under test, started as a CHILD of
# this script's shell. Probe: tick.cmd prints "tick N" once a second, 7 times (~7 s); cmd's echo
# is unbuffered. Each case starts it from the shell under test, which exits at once; from out
# here we check the probe is still running (pgrep ping: its sleeper) and that its log reaches 7.
abs() { echo "$(cd "$(dirname "$1")" && pwd)/$(basename "$1")"; }   # the script cds away
S=$(abs "$1")
D="$(cd "$(dirname "$0")" && pwd)"
W="${TMPDIR:-${TEMP:-/tmp}}/csbash-detach-survive"; rm -rf "$W"; mkdir -p "$W"; cd "$W" || exit 1
cp "$D/tick.cmd" .
lines() { [[ -f $1 ]] && { wc -l < "$1" 2>/dev/null || echo "LOCKED"; } || echo 0; }
alive() { pgrep -c ping; }
finish() {   # finish <label> <log> <t0 s>: alive 2 s after the shell exited, lines once the probe is done
	local a l1
	sleep 2; a=$(alive); l1=$(lines "$2")
	sleep 6.5
	printf '%-36s shell took %4s ms | +2 s: alive %s, %s lines | at end: %s lines\n' "$1" "$3" "$a" "$l1" "$(lines "$2")"
	taskkill /F /IM PING.EXE >/dev/null 2>&1; sleep 0.3
}
run() {   # run <script> [outfile]: time the shell under test into $t (no "$@" as a command: see the open defect)
	local s=$EPOCHREALTIME
	if [[ -n $2 ]]; then "$S" -c "$1" > "$2"; else "$S" -c "$1"; fi
	local e=$EPOCHREALTIME
	t=$(( (${e/./} - ${s/./}) / 1000 ))
	}
taskkill /F /IM PING.EXE >/dev/null 2>&1
run './tick.cmd > a.log 2>&1 &';                 finish 'bg > file 2>&1'                a.log $t
run './tick.cmd >> b.log &';                     finish 'bg >> file'                    b.log $t
run './tick.cmd > /dev/null 2>&1 & echo started > c.log'; finish 'bg > /dev/null 2>&1 (alive?)' c.log $t
run 'nohup ./tick.cmd > d.log 2>&1 &';           finish 'nohup bg > file 2>&1'          d.log $t
run 'setsid -f ./tick.cmd > e.log 2>&1';         finish 'setsid -f > file 2>&1'         e.log $t
run 'f() { ./tick.cmd > g.log; }; f &';          finish 'bg function > file'            g.log $t
run 'cd . && ./tick.cmd > h.log &';              finish 'cd && bg (last line)'          h.log $t
run './tick.cmd &' i.log;                      finish 'bg, OUTER shell owns the file' i.log $t
run '"$BASH" -c "./tick.cmd > j.log 2>&1 &"';    finish 'nested shell, bg (open item)'  j.log $t
# reading the log while the job writes it
"$S" -c './tick.cmd > k.log 2>&1 & sleep 2.5; echo "cat while it writes: $(wc -l < k.log) lines"; wait'
# polarity: a foreground child of a shell killed (the shell alone, not its tree) must die
"$S" -c 'echo $$ > shell.pid; ./tick.cmd > z.log 2>&1' &
sleep 1.5; taskkill /F /PID "$(cat shell.pid)" > /dev/null; finish 'POLARITY fg, shell killed' z.log -
