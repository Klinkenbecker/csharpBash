# 2026-10-10: trap takes bash's signal specs on Linux (names in any case with or without SIG,
# RTMIN+n / RTMAX-n, numbers 0-64, EXIT DEBUG ERR RETURN) and reports anything else as an invalid
# signal specification, still setting the valid ones (`trap x BADSIG`, `trap x 65` were accepted);
# `trap -p` lists in bash's order and spelling (SIGINT, SIGRTMIN+3, 32) and only the named traps when
# given operands. The streams are merged inside, so their order is exact under every relay; the
# script runs by relative name from a temp dir, so the error prefix names it the same in every shell.
# Expected output: GNU bash via WSL.
d=$(mktemp -d) && cd "$d" || exit 1
cat > t.sh <<'EOF'
{
for s in USR1 10 PIPE WINCH CHLD RTMIN RTMIN+3 RTMAX-2 RTMAX RTMIN+30 RTMAX-30 RTMIN+31 RTMAX-31 RTMIN-1 31 32 33 34 64 65 sigusr1 SigPipe IOT CLD POLL BADSIG 0 exit EXIT ERR RETURN -1 ''; do
  trap ':' "$s" 2>/dev/null; r1=$?; trap - "$s" 2>/dev/null; r2=$?; printf '%s:%s%s ' "$s" "$r1" "$r2"
done; echo
trap ':' BADSIG; echo "rc=$?"; trap - BADSIG; echo "rc=$?"; trap ':' INT BADSIG TERM; echo "rc=$?"
trap ': x' 32 33 34 35 49 50 63 64 10 rtmin+3 sigpipe 1 EXIT 0 ERR RETURN; trap "echo 'it''s'" USR2
trap -p
echo "-- some"; trap -p INT SIGTERM 2 BADSIG USR1; echo "rc=$?"
trap - EXIT ERR
} 2>&1
EOF
"$BASH" t.sh
cd / && rm -rf "$d"
