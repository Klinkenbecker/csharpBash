# 2026-10-08: date's %N width is a precision (%3N = milliseconds; it printed all 9 digits), and -d takes
# a fractional @epoch (@1700000000.5; only whole seconds were accepted). Fractions here have at most 7
# digits: .NET time keeps 100 ns. Expected output: GNU date via WSL.
t=@1700000000.1234567
for f in %N %3N %6N %1N %7N %9N %12N %-3N %_3N %03N %-N; do printf '%s=[%s] ' "$f" "$(date -u -d $t +$f)"; done; echo
for e in @1700000000 @1700000000.5 @1700000000.05 @-1.5 @0.25; do printf '%s -> %s\n' "$e" "$(date -u -d $e '+%s %N %Y-%m-%d %H:%M:%S')"; done
echo "now %3N has $(date +%3N | tr -d '\n' | wc -c) digits, %6N $(date +%6N | tr -d '\n' | wc -c), %N $(date +%N | tr -d '\n' | wc -c)"
date -u -d @1700000000.5 '+%s.%3N'
for t in @1.1234567 @1.5 @1; do printf "%s: " $t; for f in %-N %_N %0N %-6N %_6N %-12N %_12N; do printf "%s=[%s] " "$f" "$(date -u -d $t +$f)"; done; echo; done
