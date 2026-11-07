# 2026-11-07 (found writing guy-e3's =~ case): `[[ ]]` takes redirections, as any compound command: `[[ … ]] 2>/dev/null`
# was "syntax error near unexpected token `2'". And a test inside it that cannot be evaluated is false and the rest goes
# on, as bash: an invalid regex silently (status 2 when `[[ ]]` ends on it), a bad number with its message printed
# INSIDE the redirections; the whole `[[ ]]` stopped with status 2 and the message escaped them.
# Expected = GNU bash 5.1 via WSL.
cd "$(mktemp -d)" || exit 1
[[ a == a ]] 2>/dev/null && echo "2>/dev/null: runs"
[[ a == b ]] > /dev/null; echo "false with > /dev/null: $?"
[[ -n x ]] > out.txt; echo "> file: $? size $(wc -c < out.txt)"
[[ -n x ]] 2>&1 | cat; echo "in a pipeline: ${PIPESTATUS[0]}"
[[ -z x ]] 2>/dev/null || echo "|| after the redirect"
f() { [[ -n $1 ]] 2>/dev/null && echo "f: $1"; }
f yes
declare -f f
echo "--- errors inside"
[[ a =~ [[:foo:]] ]]; echo "invalid regex: $?"
[[ a =~ [[:foo:]] || -n x ]]; echo "invalid regex || true: $?"
[[ -n x && a =~ [[:foo:]] ]]; echo "true && invalid regex: $?"
[[ a =~ [[:foo:]] || -z x ]]; echo "invalid regex || false: $?"
! [[ a =~ [[:foo:]] ]]; echo "! invalid regex: $?"
echo "invalid regex prints: $({ [[ a =~ [[:foo:]] ]]; } 2>&1 | wc -c)"
[[ 1 -eq 1+ ]] 2>/dev/null; echo "bad number: $?"
[[ 1 -eq 1+ || -n x ]] 2>/dev/null; echo "bad number || true: $?"
echo "bad number prints: $({ [[ 1 -eq 1+ ]]; } 2>&1 | grep -c .) line(s)"
echo "silenced by its own 2>/dev/null: $({ [[ 1 -eq 1+ ]] 2>/dev/null; } 2>&1 | wc -c)"
cd / && rm -rf "$OLDPWD"
