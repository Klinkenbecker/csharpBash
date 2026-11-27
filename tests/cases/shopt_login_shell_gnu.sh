# 2026-11-27 (bugs list 12, web-d0, 2026-11-25): login_shell says how the shell was started: `shopt -s` and `shopt -u`
# of it, and `-O login_shell` at startup, are ignored with status 0, as GNU's; C#Bash changed it (Claude Code's snapshot
# runs `shopt -s login_shell`, so every Claude Code call reported a login shell). Expected = GNU bash 5.1 via WSL.
d=$(mktemp -d) && cd "$d" || exit 1
"$BASH" -c 'shopt -s login_shell; echo "set: rc=$?"; shopt login_shell; shopt -q login_shell; echo "q: $?"' 2>&1
HOME=$d "$BASH" -l -c 'shopt login_shell; shopt -u login_shell; echo "unset: rc=$?"; shopt login_shell' 2>&1
"$BASH" -c 'shopt -s login_shell nullglob; echo "two: rc=$?"; shopt login_shell nullglob' 2>&1
"$BASH" -c 'shopt -s -q login_shell; echo "sq: rc=$?"; shopt -p login_shell' 2>&1
"$BASH" -O login_shell -c 'shopt login_shell' 2>&1
cd / && rm -rf "$d"
