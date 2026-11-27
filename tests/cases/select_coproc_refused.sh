# 2026-11-22 (DECISIONS "[CLAUDE] the coproc bug, reframed"; claude-c3 via guy-bf): C#Bash has no `select` and no
# coprocesses. Run, each says so with the link to ask for it (the `complete` refusal: status 2, the script goes on),
# its body never runs, and the request is counted in the request log. They were a syntax error (`coproc CP { …; }`,
# every `select`) or "command not found" (`coproc cat`), the syntax errors never counted.
# Expected output: authored (GNU runs both; there is no GNU oracle for refusing them).
cd "${TMPDIR:-${TEMP:-/tmp}}" || exit 1
d="select-coproc-$$"; mkdir -p "$d" && cd "$d" || exit 1
export BASH_UNSERVICED_LOG="$PWD/log.jsonl"
mask() { sed -e 's/build%20[^ ]*/build%20<stamp>/' -e 's/^.*line [0-9]*: //'; }
{
select x in a b; do echo "SHOULD-NOT-RUN $x"; done; echo "select rc=$?"
coproc CP { echo SHOULD-NOT-RUN; }; echo "coproc NAME { } rc=$?"
coproc echo SHOULD-NOT-RUN; echo "coproc simple rc=$?"
} 2>&1 | mask
echo "--- the request log"
jq -r '[.tool, .feature, .outcome] | join(" | ")' log.jsonl
echo "--- set -e stops at one, as at any failed command"
"$BASH" -ec 'coproc cat; echo SHOULD-NOT-RUN' 2>/dev/null; echo "rc=$?"
cd .. && rm -rf "$d"
