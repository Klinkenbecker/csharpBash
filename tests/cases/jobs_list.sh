# 2026-10-10: `jobs` in bash's format (`[1]-  Running` then the state 24 wide, the command, ` &`), with
# -l (pid), -p (pids alone: `kill $(jobs -p)`), -r, -s and job specs; a pipeline stage lists the parent's
# jobs (`jobs | sed` listed nothing). Every option was ignored. Pids differ between shells, so they are
# replaced (and -l's padding collapsed). Expected output: GNU bash via WSL.
{
"$BASH" -c 'sleep 2' & p1=$!
"$BASH" -c 'sleep 2' & p2=$!
echo "== jobs"; jobs | sed "s/$p1/P1/; s/$p2/P2/"
echo "== -l"; jobs -l | sed -E "s/ +$p1 / P1 /; s/ +$p2 / P2 /"
echo "== -p"; jobs -p | sed "s/^$p1\$/P1/; s/^$p2\$/P2/"
echo "== -r"; jobs -r | sed "s/$p1/P1/; s/$p2/P2/"
echo "== -s"; jobs -s
echo "== %1"; jobs %1 | sed "s/$p1/P1/"
echo "== -p %2"; jobs -p %2 | sed "s/^$p2\$/P2/"
kill $(jobs -p) 2>/dev/null; wait 2>/dev/null; echo "== after kill: $(jobs -p | wc -l | tr -d ' ')"
} 2>&1
