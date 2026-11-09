# 2026-11-05 (the open list's small gaps): awk's math built-ins sqrt, exp, log, sin, cos, atan2 (they were refused:
# "awk: unsupported: sqrt()", handed to a PATH awk, none here). rand() and srand(): awk_rand_gnu. Expected: gawk 5.1
# via WSL.
awk 'BEGIN { print sqrt(2), sqrt(16), exp(1), exp(0), log(10), log(1), sin(0), cos(0), atan2(0, -1), atan2(1, 1) * 4 }'
awk 'BEGIN { print int(sqrt(17)), exp(log(5)), sin(atan2(1, 1)) ^ 2, cos(3.14159265358979) }'
awk 'BEGIN { OFMT = "%.12g"; x = sqrt(2); print x, x * x; printf "%.4f %.10f\n", exp(2), atan2(1, 0) }'
printf '2\n4\n4\n4\n5\n5\n7\n9\n' | awk '{ s += $1; ss += $1 * $1; n++ } END { m = s / n; printf "mean %.3f sd %.3f\n", m, sqrt(ss / n - m * m) }'
printf '1 1\n3 4\n5 12\n' | awk '{ printf "%d %d -> %.6f\n", $1, $2, sqrt($1 ^ 2 + $2 ^ 2) }'
awk 'BEGIN { for (d = 0; d <= 180; d += 45) printf "%d %.6f %.6f\n", d, sin(d * atan2(0, -1) / 180), cos(d * atan2(0, -1) / 180) }'
