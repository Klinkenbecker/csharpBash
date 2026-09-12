#!/usr/bin/env bash
# Three-way wall-clock comparison: C#Bash vs Git for Windows bash vs WSL bash.
#
#   tests/bench/compare3.sh [rounds]          (run it FROM Git Bash)
#
# Each benchmark runs the SAME script in each shell, best of N (default 3), and the outputs are
# compared so a fast wrong answer cannot look like a win. Times are wall clock and include
# process startup, which is the number a user actually pays.
#
# Fairness notes, because the three are not interchangeable:
#  * WSL's Windows entry point (System32\bash.exe) only takes `-c`, so its scripts are sourced
#    into one WSL bash; the other two run the script directly. One shell process either way.
#  * WSL sees this repository through /mnt/<drive>, a 9P filesystem — that is a real cost of
#    using WSL bash on Windows files, so it is reported rather than engineered away, and the
#    filesystem-touching rows are marked.
#  * WSL bash is native Linux bash; Git Bash is bash on the MSYS2 POSIX emulation layer.
set -u
here="$(cd "$(dirname "$0")" && pwd)"
root="$(cd "$here/../.." && pwd)"
rounds="${1:-3}"

# ── locate the three shells ──────────────────────────────────────────────────
cs=""
for c in "$root/dist/Bash.exe" "$root/Bash/bin/Release/net8.0/Bash.exe"; do
  [ -x "$c" ] && cs="$c" && break
done
[ -n "$cs" ] || { echo "compare3: no C#Bash build found (dist/ or bin/Release)" >&2; exit 2; }

gitbash=""
for c in "$BASH" /usr/bin/bash "$(command -v bash 2>/dev/null)"; do
  case "$c" in *[Ss]ystem32*) continue ;; esac          # never WSL's launcher
  [ -n "$c" ] && [ -x "$c" ] && gitbash="$c" && break
done

wsl=""
for c in "${SYSTEMROOT:-/c/WINDOWS}/System32/bash.exe" /c/WINDOWS/System32/bash.exe; do
  [ -x "$c" ] && wsl="$c" && break
done
if [ -n "$wsl" ]; then
  "$wsl" -c 'exit 0' >/dev/null 2>&1 || wsl=""     # installed but no distro configured
fi

# /f/x/y  ->  /mnt/f/x/y
to_wsl() { printf '/mnt%s' "$1"; }

echo "C#Bash   : $cs"
"$cs" --version 2>/dev/null | tail -1 | sed 's/^/           /'
echo "Git Bash : ${gitbash:-<none>}"
[ -n "$gitbash" ] && "$gitbash" -c 'echo "           bash $BASH_VERSION"' 2>/dev/null
echo "WSL bash : ${wsl:-<none — skipped>}"
[ -n "$wsl" ] && "$wsl" -c 'echo "           bash $BASH_VERSION on $(uname -sr)"' 2>/dev/null
echo "rounds   : $rounds (best of)"
echo

export TIMEFORMAT='%3R'

# run one benchmark in one shell, once; echoes the elapsed seconds
run_once() {
  case "$1" in
    cs)   { time "$cs" "$2" >/dev/null 2>&1; } 2>&1 ;;
    git)  { time "$gitbash" "$2" >/dev/null 2>&1; } 2>&1 ;;
    wsl)  { time "$wsl" -c ". $(to_wsl "$2")" >/dev/null 2>&1; } 2>&1 ;;
  esac
}

best() {
  local which=$1 script=$2 min=999 t r
  for ((r = 0; r < rounds; r++)); do
    t=$(run_once "$which" "$script")
    case "$t" in ''|*[!0-9.]*) t=999 ;; esac
    min=$(awk -v a="$t" -v b="$min" 'BEGIN{print (a<b)?a:b}')
  done
  printf '%s' "$min"
}

ratio() { awk -v a="$1" -v b="$2" 'BEGIN{ if (b+0 > 0 && a+0 < 900) printf "%.1fx", a/b; else printf "-" }'; }

printf '%-11s %10s %10s %10s   %8s %8s\n' benchmark 'C#Bash' 'Git Bash' 'WSL' 'vs Git' 'vs WSL'
printf -- '----------------------------------------------------------------------\n'

for b in loop arith func loop_big coreutils pipeline find; do
  s="$here/$b.sh"
  [ -f "$s" ] || continue
  c=$(best cs "$s")
  g=""; [ -n "$gitbash" ] && g=$(best git "$s")
  w=""; [ -n "$wsl" ] && w=$(best wsl "$s")
  mark=""
  case "$b" in pipeline|find|coreutils) mark=" *" ;; esac
  printf '%-11s %10s %10s %10s   %8s %8s%s\n' "$b" "$c" "${g:--}" "${w:--}" \
    "$( [ -n "$g" ] && ratio "$g" "$c" || echo - )" \
    "$( [ -n "$w" ] && ratio "$w" "$c" || echo - )" "$mark"
done
echo "  * touches the filesystem: WSL reaches these files over /mnt (9P), which is its own cost."

# ── startup: what a caller pays per invocation (Claude Code pays this per tool call) ──
echo
printf '%-11s' "startup"
for k in cs git wsl; do
  case $k in
    cs)  exe="$cs"   ; [ -z "$cs" ] && { printf '%10s' -; continue; } ;;
    git) exe="$gitbash"; [ -z "$gitbash" ] && { printf '%10s' -; continue; } ;;
    wsl) exe="$wsl"  ; [ -z "$wsl" ] && { printf '%10s' -; continue; } ;;
  esac
  min=999
  for r in 1 2 3 4 5 6 7; do
    t=$( { time "$exe" -c 'exit 0' >/dev/null 2>&1; } 2>&1 )
    case "$t" in ''|*[!0-9.]*) t=999 ;; esac
    min=$(awk -v a="$t" -v b="$min" 'BEGIN{print (a<b)?a:b}')
  done
  printf '%10s' "$min"
done
printf '   (bash -c '"'"'exit 0'"'"', best of 7)\n'

# ── the outputs must agree, or a time means nothing ──────────────────────────
echo
echo "output agreement:"
for b in loop arith func coreutils pipeline; do
  s="$here/$b.sh"
  [ -f "$s" ] || continue
  a=$("$cs" "$s" 2>/dev/null)
  ok="C#Bash only"
  if [ -n "$gitbash" ]; then
    if [ "$a" = "$("$gitbash" "$s" 2>/dev/null)" ]; then ok="= Git Bash"; else ok="DIFFERS from Git Bash"; fi
  fi
  if [ -n "$wsl" ]; then
    if [ "$a" = "$("$wsl" -c ". $(to_wsl "$s")" 2>/dev/null)" ]; then ok="$ok, = WSL"; else ok="$ok, DIFFERS from WSL"; fi
  fi
  printf '  %-11s %s\n' "$b" "$ok"
done
