# 2026-10-12: `kill -l`, `kill -L` and `trap -l` print bash's numbered table (five to a row, tab-joined, 32
# and 33 left out); they printed a short line of nine names. `trap -l` ignores its operands. `kill -l` takes
# options as bash's kill.def (the first `-WORD` is the signal and is not checked when listing: `kill -l -9`
# lists); `kill -l 0` is EXIT, 32 and 33 print nothing, EXIT/DEBUG/ERR/RETURN number 0/65/66/67. `SIG` goes
# only before a real signal's name: `SIGEXIT`, `SIGERR`, `SIG2` are invalid, in trap too. Expected: GNU bash
# via WSL.
e() { sed 's/^.*line [0-9]*: //'; }
kill -l
trap -l INT -p | head -2
kill -L | tail -1 | od -c | head -2
kill -l 2 32 33 0 130 192 TERM sigint SIGRTMIN+2 rtmax-1 EXIT exit DEBUG ERR RETURN; echo "rc=$?"
for a in 128 193 65 -1 x 2x SIGEXIT SIG2 SIGSIGINT SIG; do echo "[$a] $(kill -l "$a" 2>&1 | e) rc=$(kill -l "$a" >/dev/null 2>&1; echo $?)"; done
kill -l -9 | wc -l; kill -l -1 2; kill -l -x | wc -l; kill -l -- 15
kill -ll 9 2>&1 | e; kill -s 2>&1 | e; echo "rc=${PIPESTATUS[0]}"
kill -sINT 2>&1 | e; kill - 2>&1 | e; kill -s EXIT $$; echo "kill -s EXIT rc=$?"
for s in SIGEXIT sigexit SIGDEBUG SIGERR SIGRETURN SIG2 SIGINT sigrtmin+2 USR1 2; do trap : $s 2>&1 | e; echo "trap $s rc=${PIPESTATUS[0]}"; done
trap : SIGINT sigrtmin+2 USR1; trap -p; trap - INT RTMIN+2 USR1
