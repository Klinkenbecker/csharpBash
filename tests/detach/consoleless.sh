#!/usr/bin/env bash
# Start an outer C#Bash CONSOLE-LESS (as Claude Code starts its tool shell), which runs a nested shell that backgrounds
# the probe and exits. Then read the probe's log: if the probe lost its console when the outer shell's job closed, its
# pings fail ("ping failed" lines, fast ticks). Run by hand:  bash tests/detach/consoleless.sh <Bash.exe> <log name>
# Replaces consoleless.ps1 (2026-11-14, PowerShell removed): the same start (tools/launch --detached, 30 s), the same
# 4 s wait, the same report line.
set -u
exe=$1 log=$2
dir="$(cd "$(dirname "$0")" && pwd)"
launch="$dir/../../tools/launch/bin/launch.exe"
[ -f "$launch" ] || { echo "consoleless: the launcher is not built (dotnet build tools/launch -c Release -o tools/launch/bin)" >&2; exit 2; }
rm -f "$dir/$log"
"$launch" --detached --timeout 30 --cwd "$dir" --out NUL --cmdline "\"$exe\" \"$dir/consoleless-outer.sh\" \"$log\""
sleep 4
printf '%s: %s lines 4 s after the outer shell exited; '"'"'ping failed'"'"': %s\n' "$exe" "$(grep -c . "$dir/$log" 2>/dev/null || echo 0)" "$(grep -c 'ping failed' "$dir/$log" 2>/dev/null || echo 0)"
