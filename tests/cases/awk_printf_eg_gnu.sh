# 2026-11-05 (found beside the awk math work): awk's printf %e, %E, %g, %G were refused ("awk: unsupported: printf %g");
# now C's, as gawk's: precision, width, flags (- + space 0 #), exponents of two digits or more, %g's choice of form
# and trimmed zeros, inf and nan. Expected: gawk 5.1 via WSL.
awk 'BEGIN { printf "[%e] [%E] [%g] [%G]\n", 65.5, 65.5, 65.5, 65.5 }'
awk 'BEGIN { printf "[%.2e] [%10.3e] [%-12.1e] [%+e] [% e] [%012.3e]\n", 1234.5678, 1234.5678, -0.000123, 42, 42, 3.14159 }'
awk 'BEGIN { printf "[%g] [%g] [%g] [%g] [%g] [%g] [%g]\n", 0.0001, 0.00001, 123456, 1234567, 100000, 1e100, -2.5e-7 }'
awk 'BEGIN { printf "[%.3g] [%.10g] [%#g] [%#.3g] [%8.2g] [%-8g] [%08.3g]\n", 3.14159, 1/3, 2, 2, 1234, 1.5, 2.71828 }'
awk 'BEGIN { printf "[%e] [%g] [%G]\n", 0, 0, 1e-300 * 1e-300 }'
awk 'BEGIN { x = 2 ^ 1024; printf "[%e] [%g] [%G] [%e]\n", x, -x, x, log(-1) }' 2>/dev/null
printf '1.5\n2.25e3\n-0.000078\n' | awk '{ printf "%s -> %.3e %g\n", $1, $1, $1 }'
