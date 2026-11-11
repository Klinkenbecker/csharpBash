# 2026-11-09 (git-b3's request; the architect: "go"): awk's ENVIRON (POSIX), refused until now ("awk: unsupported:
# ENVIRON"): the environment the awk command receives, the exported variables and its own assignments. Its values are
# the exact text, where -v processes escapes. Expected: gawk 5.1 via WSL.
X=hello awk 'BEGIN { print ENVIRON["X"] }'
export N=41; awk 'BEGIN { print ENVIRON["N"] + 1, ("N" in ENVIRON) }'
U=notexported; awk 'BEGIN { print "[" ENVIRON["U"] "]", ("U" in ENVIRON) }'
T='a\tb\\c' awk 'BEGIN { print "ENVIRON: " ENVIRON["T"] }'
T='a\tb\\c'; awk -v t="$T" 'BEGIN { print "-v: " t }' | od -c | head -1 | tr -s ' '
S='two words' awk 'BEGIN { n = split(ENVIRON["S"], w, " "); print n, w[2] }'
