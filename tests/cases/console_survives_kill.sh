# 2026-11-16 (facts-8a's report): a shell started with no console (as Claude Code starts its tool shell) adopts the
# hidden console of its first child (HiddenConsole, DECISIONS 2026-09-14), and that console's host, conhost.exe, is
# the first child's own child (measured: its parent id is the child's). Ending that child with its whole tree (a
# broken pipe, timeout, kill) killed the host too: the shell lost its console, and every later program got a fresh
# one on the OEM code page (437), so a .NET program wrote CP437 into its pipe. A console host ends by itself with its
# last client, so a tree kill now spares conhost.exe. Expected output: authored (Windows-specific; GNU has no
# console); 65001 is the code page C#Bash gives a console.
fx="$PWD/../tools/launch/bin/launch.exe"   # tools/launch: started as Claude Code starts its shell (no console)
d=$(mktemp -d) && cd "$d" || exit 1
cat > pipe.sh <<'EOF'
ping -n 6 127.0.0.1 | awk 'NR==1{exit}'; echo "broken pipe rc=${PIPESTATUS[0]} code page $(chcp.com | tr -dc 0-9)"
EOF
cat > timeout.sh <<'EOF'
timeout 1 ping -n 6 127.0.0.1 > /dev/null; echo "timeout rc=$? code page $(chcp.com | tr -dc 0-9)"
EOF
run() { "$fx" --detached --timeout 30 --print --cwd "$PWD" --cmdline "\"$BASH\" $1"; }
run pipe.sh; run timeout.sh
cd / && rm -rf "$d"
