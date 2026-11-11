# 2026-11-09 (git-b3): an assignment to RANDOM seeds it (bash's manual); it did nothing. One seed gives one sequence,
# in one shell and across runs; the value is arithmetic, taken modulo 2^32 (abc is 0, 4294967298 is 2); SRANDOM is not
# seeded by it. The values are C#Bash's own, not bash's, so only properties are printed. Expected: GNU bash via WSL.
RANDOM=42; a="$RANDOM $RANDOM $RANDOM"; RANDOM=42; b="$RANDOM $RANDOM $RANDOM"
echo "reseeded, same sequence: $([ "$a" = "$b" ] && echo yes || echo no)"
c=$("$BASH" -c 'RANDOM=42; echo $RANDOM $RANDOM $RANDOM')
echo "another shell, same sequence: $([ "$a" = "$c" ] && echo yes || echo no)"
RANDOM=43; e="$RANDOM $RANDOM $RANDOM"; echo "another seed, another sequence: $([ "$a" != "$e" ] && echo yes || echo no)"
RANDOM=abc; x=$RANDOM; RANDOM=0; y=$RANDOM; echo "abc seeds as 0: $([ "$x" = "$y" ] && echo yes || echo no)"
RANDOM=4294967298; x=$RANDOM; RANDOM=2; y=$RANDOM; echo "modulo 2^32: $([ "$x" = "$y" ] && echo yes || echo no)"
ok=yes; RANDOM=7; for i in $(seq 1 2000); do r=$RANDOM; [ "$r" -ge 0 ] && [ "$r" -le 32767 ] || ok=no; done; echo "2000 values in 0..32767: $ok"
RANDOM=9; s1=$SRANDOM; RANDOM=9; s2=$SRANDOM; echo "SRANDOM not seeded by RANDOM: $([ "$s1" != "$s2" ] && echo yes || echo no)"
