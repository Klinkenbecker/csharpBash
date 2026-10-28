# 2026-10-18: the in-process awk never runs a program twice. A construct outside its subset that is visible in the
# program text (a literal printf/sprintf format with %e %g, a literal RS, system()) is refused before anything runs,
# so the PATH awk runs it once (DECISIONS 2026-09-04: "parse-time ... loud"); one met only while the program runs (a
# format held in a variable) fails loud, 2, instead of the PATH awk running it all again: `print "first"` came out
# twice. And out-of-range numbers print as gawk 5.1's +inf, -inf, -nan, +nan (they were inf, nan, 0 for %d, Infinity
# for %f). Authored; the non-finite lines are gawk 5.1's. The fall-through lines need a PATH awk and are in
# awk_fallthrough since 2026-10-20 (until then a missing PATH awk ended this case before the lines below ran).
echo "== a format known only while running"
awk 'BEGIN { print "first"; f = "%e\n"; printf f, 1 }' 2>&1 | sed 's/^awk: (C#Bash: .*/awk: (C#Bash: ...)/'; echo "status ${PIPESTATUS[0]}"
echo "== out-of-range numbers"
awk 'BEGIN { x = 2^2000; n = x - x; printf "[%d][%5d][%-6d][%05d][%5.2f][%f][%i][%+d]\n", x, x, x, x, x, -x, n, n
             printf "[%x][%o][%u][%F][%5s][%.3d][%X]\n", x, -x, n, x, x, x, -n
             print x, -x, n, -n; OFMT = "%.2f"; print x, n; CONVFMT = "%.3f"; y = x ""; print y, length(n) }'
