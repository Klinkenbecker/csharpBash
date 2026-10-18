# 2026-10-18: printf's infinities and NaNs as C writes them: inf, -inf, nan, -nan (INF, NAN for %F %E %G), the sign
# flags kept, the precision ignored, padded with spaces even under 0. The words are read in any case, signed. C#Bash
# printed .NET's "Infinity" and "NaN", refused "inf", and `printf %g` of an infinity threw ("length ('-1') must be a
# non-negative value"). Not here: 1e400, a finite long double to bash and infinite to C#Bash's double.
# Expected: GNU bash 5.1 (WSL).
for a in inf Inf -inf infinity -INFINITY nan -nan NaN 2.5 -0.125; do
	printf '%-10s %f|%5.1e|%G|%+g|%08f|% F|%-6g|%.0e\n' "$a" "$a" "$a" "$a" "$a" "$a" "$a" "$a" "$a"
done
