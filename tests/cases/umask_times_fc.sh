# 2026-10-10: `umask`, `times` and `fc` are builtins. They were "command not found" (127), and `fc` ran
# Windows' fc.exe ("FC: Insufficient number of file specifications"). umask is bash 5.1's umask.def: octal
# and symbolic modes, -p, -S, all-or-none clauses, kept per shell (subshells and pipeline stages keep
# theirs); it has no effect on created files (Windows has no mode bits). times: bash's two lines (digits
# masked: they vary). fc: bash's option check, then "not supported" (bash, in a script, lists an empty
# history; the assertion below accepts both). Inner script by relative name from a temp dir, so both shells
# print `t.sh: line N:`. Expected output: GNU bash via WSL.
d=$(mktemp -d) && cd "$d" || exit 1
cat > t.sh <<'EOF'
{
umask 0022
echo "1 $(umask)"; echo "2 $(umask -S)"; echo "3 $(umask -p)"; echo "4 $(umask -p -S)"
umask 077; echo "5 $(umask) $(umask -S)"
umask u=rwx,g=rx,o=; echo "6 $(umask)"
umask g-x; echo "7 $(umask)"
umask a+r; echo "8 $(umask)"
umask o+w,g=u; echo "9 rc=$? $(umask)"
umask 7; echo "10 $(umask)"
umask 0022
umask 089; echo "11 rc=$?"
umask 12345; echo "12 rc=$? $(umask)"
umask x=r; echo "13 rc=$?"
umask -Z; echo "14 rc=$?"
umask 022 033; echo "15 rc=$? $(umask)"
(umask 077); echo "16 $(umask)"
umask -S 077; echo "17 rc=$? $(umask)"
umask 0022; umask -- 027; echo "18 $(umask)"
umask ug=rw; echo "19 $(umask)"; umask 0022
umask u+; echo "20 rc=$? $(umask)"
umask =r; echo "21 rc=$? $(umask)"; umask 0022
umask +x; echo "22 $(umask)"; umask 0022
umask go-rwx; echo "23 $(umask)"; umask 0022
umask u=rwxs; echo "24 rc=$? $(umask)"
umask 1777; echo "25 $(umask)"; umask 0022
umask 077 | cat; echo "26 $(umask)"
echo "27 $(umask 0; umask) $(umask)"
type umask times fc; type -t umask times fc
times | sed 's/[0-9]/N/g'
times x | sed 's/[0-9]/N/g'; echo "28 rc=$?"
times -Z; echo "29 rc=$?"
fc -Z; echo "30 rc=$?"
out=$(fc -l 2>&1); case $out in "" | *"not supported by this interpreter") echo "31 fc ok" ;; *) echo "31 fc BAD: $out" ;; esac
} 2>&1
EOF
"$BASH" t.sh
cd / && rm -rf "$d"
