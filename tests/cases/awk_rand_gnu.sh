# 2026-11-09 (git-b3's request; the architect: "nothing in gawk docs say srand() has to be implementation predictable,
# only that the sequence is the same"): awk's rand() and srand() by their documented behavior, not gawk's values. The
# seed is 1 until srand() is called; srand(x) seeds with x's integer part (as a number: "abc" is 0, "12xy" 12) and
# returns the previous seed; srand() seeds with the time in seconds; rand() is in [0, 1); one seed, one sequence, in
# one run and across runs. Only properties are printed, so gawk prints the same. Expected: gawk 5.1 via WSL.
awk 'BEGIN { print "first srand() returns", srand(5); print "then", srand(7); srand(2.7); print "after srand(2.7):", srand() }'
awk 'BEGIN { srand("abc"); a = srand(); srand("12xy"); b = srand(); print "srand(\"abc\") seed", a, "srand(\"12xy\") seed", b }'
awk 'BEGIN { srand(-4); print "negative seed kept:", srand(); srand(10000000000); print "large seed kept:", srand() }'
awk 'BEGIN { srand(3); x1 = rand(); x2 = rand(); x3 = rand(); srand(3); y1 = rand(); y2 = rand(); y3 = rand()
	print "same seed, same sequence:", (x1 == y1 && x2 == y2 && x3 == y3)
	srand(4); z = rand(); print "another seed, another sequence:", (z != x1)
	srand(2); p = rand(); srand(2.9); q = rand(); print "srand(2.9) is srand(2):", (p == q) }'
a=$(awk 'BEGIN { print rand() }'); b=$(awk 'BEGIN { srand(1); print rand() }'); c=$(awk 'BEGIN { print rand() }')
echo "no srand() is srand(1): $([ "$a" = "$b" ] && echo 1 || echo 0); two runs agree: $([ "$a" = "$c" ] && echo 1 || echo 0)"
awk 'BEGIN { srand(9); n = 0; lo = 1; hi = 0; sum = 0
	for (i = 0; i < 100000; i++) { r = rand(); if (r < 0 || r >= 1) n++; if (r < lo) lo = r; if (r > hi) hi = r; sum += r }
	m = sum / 100000
	print "100000 draws outside [0,1):", n, " min below 0.001:", (lo < 0.001), " max above 0.999:", (hi > 0.999), " mean within 0.49-0.51:", (m > 0.49 && m < 0.51) }'
awk 'BEGIN { srand(); t = srand(); print "srand() seeds with the time:", (t > 1700000000 && t == int(t)) }'
