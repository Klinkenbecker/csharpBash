# 2026-11-22 (DECISIONS "[CLAUDE] the coproc bug, reframed"; claude-c3 via guy-bf): `select` and `coproc` parse as
# bash parses them. `coproc CP { echo hi; }` and every `select` were syntax errors (status 2), which stopped a script
# even where the line never ran. A word after `coproc` is the NAME only when a compound command follows it. What GNU
# and C#Bash share is checked here: parsing, a branch that never runs, `declare -f` printing them back, `type`.
# Running one is refused (case select_coproc_refused). Expected = GNU bash 5.1 via WSL.
f() { select x in a b; do echo "$x"; done; coproc CP { echo hi; }; coproc cat; coproc ( echo sub ); coproc CP2 while false; do :; done; select y; do break; done; }
declare -f f
if false; then select z in q; do :; done; coproc N { :; }; fi; echo "untaken branch, then rc=$?"
g() { coproc X echo hi; }; declare -f g
type select coproc
