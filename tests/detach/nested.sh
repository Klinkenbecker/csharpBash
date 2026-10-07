# MANUAL CHECK (not in the suite). Usage: <a C#Bash> tests/detach/nested.sh <new build> <old build>
# <old build> must predate BREAKAWAY_OK (e.g. dist/Bash.exe.rev121), or the control and the
# fallback cases D/E test nothing. The console-less variant: tests/detach/consoleless.ps1.
# ~1 min. DECISIONS 2026-10-05 (nested shells: CREATE_BREAKAWAY_FROM_JOB).
# Q3 spike: can a nested shell's detached child leave the OUTER shell's kill-on-close job?
# $1 = spike build (BREAKAWAY_OK on its job; detached children start with CREATE_BREAKAWAY_FROM_JOB)
# $2 = deployed build (no BREAKAWAY_OK), for the fallback case. Probe: tick.cmd, 7 ticks ~7 s.
abs() { echo "$(cd "$(dirname "$1")" && pwd)/$(basename "$1")"; }   # the script cds away
S=$(abs "$1") OLD=$(abs "$2")
D="$(cd "$(dirname "$0")" && pwd)"
W="${TMPDIR:-${TEMP:-/tmp}}/csbash-detach-nested"; rm -rf "$W"; mkdir -p "$W"; cd "$W" || exit 1
cp "$D/tick.cmd" .
lines() { [[ -f $1 ]] && wc -l < "$1" || echo 0; }
report() {   # report <label> <log>: lines 2.5 s after the outer shell is gone, and at the end
	local a; sleep 2.5; a=$(lines "$2"); sleep 6
	printf '%-58s +2.5 s: %s lines | end: %s lines\n' "$1" "$a" "$(lines "$2")"
	taskkill /F /IM PING.EXE >/dev/null 2>&1; sleep 0.3
}
taskkill /F /IM PING.EXE >/dev/null 2>&1
# A: outer -> inner (foreground) -> tick & ; the outer exits right after the inner
"$S" -c '"$BASH" -c "./tick.cmd > a.log 2>&1 &"';                       report 'A nested, bg (new)' a.log
"$OLD" -c '"$BASH" -c "./tick.cmd > a0.log 2>&1 &"';                    report 'A control: nested, bg (old)' a0.log
# B: two levels of nesting
"$S" -c '"$BASH" -c "\"\$BASH\" -c \"./tick.cmd > b.log 2>&1 &\""';    report 'B doubly nested, bg (new)' b.log
# C: polarity -- a FOREGROUND tick under a nested shell dies when the outer shell is killed (alone)
"$S" -c 'echo $$ > outer.pid; "$BASH" -c "./tick.cmd > c.log 2>&1"' &
sleep 1.5; taskkill /F /PID "$(cat outer.pid)" >/dev/null;              report 'C POLARITY nested, fg, outer killed (new)' c.log
# D: fallback -- the spike shell runs inside the DEPLOYED shell's job, which forbids breakaway
"$OLD" -c '"'"$S"'" -c "./tick.cmd > d.log 2>&1 &"; sleep 1.2; echo "D fallback started it: $(wc -l < d.log) line(s) after 1.2 s"'
taskkill /F /IM PING.EXE >/dev/null 2>&1; sleep 0.3
# E: ...and that child stays in the deployed outer job, so it dies with it (the old behaviour)
"$OLD" -c '"'"$S"'" -c "./tick.cmd > e.log 2>&1 &"';                   report 'E fallback: dies with a non-breakaway outer job' e.log
