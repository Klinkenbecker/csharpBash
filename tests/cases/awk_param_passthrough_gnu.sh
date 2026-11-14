# 2026-11-14 (found drawing a chart; every build since awk's user functions, 2026-10-10): a function that passed its
# own parameter or local on to another function passed it EMPTY (`function f(x) { return g(x) }` gave g ""): a value
# bound to a parameter was not marked scalar, so the next call took it for an untyped variable that might become an
# array and handed over no value. Arrays and untyped variables still pass by reference through any number of calls.
# Expected = gawk via WSL.
awk '
function inner(which) { return "[" which "]" }
function inner2(other) { return "[" other "]" }
function outer(which) { return inner(which) inner2(which) }
function outerB(x,   loc) { loc = x "!"; return inner(x) inner(loc) }
function swap(a, b) { return a "-" b }
function callswap(a, b) { return swap(b, a) }
function ratio(which, v) { return which == "git" ? v * 2 : v }
function graph(which,   i, s) { for (i = 1; i <= 3; i++) s = s " " ratio(which, i); return s }
function fill(arr, n) { arr[n] = n * 10 }
function fill2(arr, n) { fill(arr, n); fill(arr, n + 1) }
function depth(n, acc) { return n == 0 ? acc : depth(n - 1, acc n) }
BEGIN {
	print outer("git"), outerB("git"), callswap("1", "2")
	print "git:" graph("git"), "wsl:" graph("wsl")
	fill2(made, 3); print "made:", made[3], made[4], length(made)
	print depth(3, "")
}'
