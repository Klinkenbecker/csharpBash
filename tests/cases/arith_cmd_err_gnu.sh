# 2026-10-30 (the "match GNU" item, found 2026-10-28): an arithmetic error inside (( )) is the command's own failure,
# as bash's: its redirections are in force (`2>/dev/null` silences it) and its status is 1. It printed past the
# redirection and returned 2. The message's wording is bash's own (error token and all) and not compared here.
# Expected: GNU bash 5.1 via WSL.
y=1
(( y = (y) <<= 1 )) 2>/dev/null; echo "non-variable: rc=$?"
(( 1 + )) 2>/dev/null; echo "operand missing: rc=$?"
(( 1 / 0 )) 2>/dev/null; echo "divide by 0: rc=$?"
(( 2 > 1 )) 2>/dev/null; echo "true: rc=$?"
(( 0 )) 2>/dev/null; echo "false: rc=$?"
f() { (( $1 )) 2>/dev/null; }; f '3 +'; echo "in a function: rc=$?"
if (( 1 + )) 2>/dev/null; then echo yes; else echo "in an if: rc=$?"; fi
(( 1 + )) 2>&1 | wc -l | sed 's/^ *//; s/^/message lines into a pipe: /'
( set -u; (( zz + 1 )) 2>/dev/null; echo "set -u went on" ); echo "set -u: the subshell ended, rc=$?"
echo end
