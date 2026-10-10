# 2026-10-10: `exec 3>&1`, `4>&2`, `3<&0` copy a standard descriptor to a numbered one, as in bash: fd 3
# goes where fd 1 went at that moment, so `>&3` reaches it after stdout moves. It was never made:
# `echo x >&3` said "3: Bad file descriptor"; and closing a copy (`exec 1>&3 3>&-`) broke the other.
# The usual idioms, each with a fixed output order, run by relative name from a temp dir.
# Expected output: GNU bash via WSL.
d=$(mktemp -d) && cd "$d" || exit 1
cat > t.sh <<'EOF'
{
exec 3>&1
out=$( { echo to-stdout; echo to-stderr >&2; } 2>&1 >&3 )
echo "captured=[$out]"
exec 3>&-
( { echo out; echo err >&2; } 3>&1 1>&2 2>&3 ) 2>/dev/null | sed 's/^/piped: /'
exec 3>&1; exec >/dev/null; echo hidden; echo shown >&3; exec 1>&3 3>&-; echo back
f() { echo "to fd 4" >&4; }; f 4>&1
exec 3>&1; out=$("$BASH" -c 'echo ext-out; echo ext-err >&2' 2>&1 >&3); echo "ext captured=[$out]"; exec 3>&-
echo line | { exec 3<&0; read -u 3 v; echo "read3=$v"; }
exec 4>&2; echo "via 4" >&4 2>/dev/null; exec 4>&-
echo "closed" >&3; echo "rc=$?"
x=$(exec 3>&1; echo "inner via 3" >&3); echo "x=[$x]"
} 2>&1
EOF
"$BASH" t.sh
cd / && rm -rf "$d"
