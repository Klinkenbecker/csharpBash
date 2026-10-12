#!/usr/bin/env bash
# Three-way wall-clock comparison: C#Bash vs Git for Windows bash vs WSL bash.
#
#   tests/bench/compare3.sh              the deployed build (dist/Bash.exe)        (run it FROM Git Bash)
#   tests/bench/compare3.sh --build      the committed working tree, published AOT here, against
#                                        the deployed build: THE way to measure an undeployed rev
#   tests/bench/compare3.sh --cs <exe>   some other existing build
#
# On the development machine Git's bash.exe is renamed _bash.exe so Claude Code finds C#Bash, so:
#   CHERE_INVOKING=1 F:/Tools/PortableGit/usr/bin/_bash.exe -l -c 'cd /f/Koliada/Tools/bash && "$BASH" tests/bench/compare3.sh --build'
#
# THIS IS THE BENCHMARK, and it runs one way (frozen 2026-10-06, DECISIONS.md "The benchmark is
# one fixed command"). The method lives here, not in the caller:
#   * every launch is made and timed by tools/benchtimer: CreateProcessW straight to the shell (the
#     way Claude Code launches one) on the high-resolution clock. Never bash's `time` inside Git
#     Bash: that made Git Bash the launcher, which added ~30 ms to every NATIVE launch (C#Bash, WSL)
#     but not to Git Bash launching itself (fixed 2026-10-06);
#   * each row runs the SAME script in each shell: C#Bash best of 9, Git Bash and WSL best of 3;
#   * startup is the MEDIAN of 5 samples, each the total of 100 consecutive `-c 'exit 0'` launches
#     divided by 100, for all three shells (a minimum of single launches on a 1 ms clock invented a
#     "+1 ms" on 2026-10-06; ONE such sample moves by ~1.5 ms between runs and invented a "+1.2 ms
#     regression" on 2026-10-10, so the architect ratified the median of 5 that day);
#   * the outputs are compared, so a fast wrong answer cannot look like a win;
#   * every run prints its build stamp, date and method and appends its table to results.md here.
# The options choose WHICH build, never how it is timed: --build (2026-10-12) publishes the committed
# tree with tools/publish-aot.cmd into tests/bench/aot (a dirty tree is refused; the stamp must name the
# revision) and times it against dist/Bash.exe in alternating rounds, adding "deployed" and "change"
# columns; --cs times a build someone already has. A build made by hand into a scratch directory is
# not the benchmark. Changing the method is an explicit decision recorded in DECISIONS.md, never an
# argument.
#
# Times are wall clock and include process startup, which is the number a user actually pays.
#
# Fairness notes, because the three are not interchangeable:
#  * WSL is reached through `wsl.exe -e bash -c` (System32\bash.exe only as a fallback), so its scripts are sourced
#    into one WSL bash; the other two run the script directly. One shell process either way.
#  * WSL sees this repository through /mnt/<drive>, a 9P filesystem — that is a real cost of
#    using WSL bash on Windows files, so it is reported rather than engineered away, and the
#    filesystem-touching rows are marked.
#  * WSL bash is native Linux bash; Git Bash is bash on the MSYS2 POSIX emulation layer.
#  * MSYS2_ARG_CONV_EXCL is set for every WSL call: Git Bash rewrites a /mnt/... value it passes to
#    a native program, which silently pointed a benchmark at the wrong directory and timed the
#    resulting failure (caught 2026-09-13).
#  * pipeline.sh writes its input to $TMPDIR, which for WSL is ext4 INSIDE the VM and for the other
#    two is the Windows temp directory — so that row gives WSL a filesystem advantage as well as a
#    tool-speed one.
set -u
# Under C#Bash, "Git Bash" below would be C#Bash itself and the harness's own `time` would be ours.
case "${BASH_VERSION:-}" in *koliada*) echo "compare3: run me under Git Bash, not C#Bash (see the header)" >&2; exit 2 ;; esac
here="$(cd "$(dirname "$0")" && pwd)"
root="$(cd "$here/../.." && pwd)"

CS_ROUNDS=9          # C#Bash: best of 9
OTHER_ROUNDS=3       # Git Bash, WSL: best of 3
STARTUP_N=100        # startup: total of 100 launches / 100 is one sample ...
STARTUP_SAMPLES=5    # ... and the row is the median of 5 samples (2026-10-10)

usage() { echo "usage: tests/bench/compare3.sh [--build | --cs <exe>]   (the method is fixed; see the header)" >&2; exit 2; }

cs="$root/dist/Bash.exe"; cs_from="dist (deployed)"
build=0
while [ $# -gt 0 ]; do
  case "$1" in
    --cs)    [ $# -ge 2 ] && [ $build = 0 ] || usage; cs="$2"; cs_from="--cs (NOT the deployed build)"; shift 2 ;;
    --build) [ "$cs_from" = "dist (deployed)" ] || usage; build=1; shift ;;
    *) usage ;;
  esac
done

# ── --build: the committed working tree, published as shipped, timed against the deployed build ──
# (2026-10-12, the architect: "repetitive tools here need to be contracts, period"). The build under
# test was made by hand before, into scratch directories; now it is made here, one way: the tree must
# be clean (the stamp names a revision), tools/publish-aot.cmd publishes it to tests/bench/aot (ignored),
# and its stamp must carry the tree's node. Each C#Bash row then times the build and the deployed
# build in alternating rounds, so drift on the machine falls on both alike (Git Bash moved 1-2% between
# two back-to-back runs on 2026-10-12, as large as the differences being read).
deployed=""
if [ $build = 1 ]; then
  command -v hg >/dev/null || { echo "compare3 --build: hg is not on PATH" >&2; exit 2; }
  dirty=$(cd "$root" && hg status -mard) || { echo "compare3 --build: hg status failed" >&2; exit 2; }
  [ -z "$dirty" ] || { printf 'compare3 --build: the working tree has uncommitted changes; commit first (the stamp must name a revision):\n%s\n' "$dirty" >&2; exit 2; }
  node=$(cd "$root" && hg id -i); rev=$(cd "$root" && hg id -n)
  aot="$here/aot"
  echo "compare3 --build: publishing rev $rev ($node) with tools/publish-aot.cmd to $aot (log: $here/aot.log)" >&2
  MSYS2_ARG_CONV_EXCL='*' cmd.exe /c "$(cygpath -w "$root/tools/publish-aot.cmd")" "$(cygpath -w "$aot")" > "$here/aot.log" 2>&1 \
    || { echo "compare3 --build: the publish FAILED; the end of $here/aot.log:" >&2; tail -5 "$here/aot.log" >&2; exit 2; }
  "$aot/Bash.exe" --version 2>/dev/null | grep -q "hg\.$node $rev\$" \
    || { echo "compare3 --build: $aot/Bash.exe does not carry the stamp of rev $rev ($node)" >&2; exit 2; }
  deployed="$cs"; cs="$aot/Bash.exe"; cs_from="--build: rev $rev, NOT deployed"
  [ -x "$deployed" ] || { echo "compare3 --build: no deployed build at $deployed to compare with" >&2; exit 2; }
fi
[ -x "$cs" ] || { echo "compare3: no C#Bash build at $cs" >&2; exit 2; }

# ── the other two shells ─────────────────────────────────────────────────────
# Git Bash is configured, never searched for. Its bash.exe is renamed _bash.exe on this machine,
# which also breaks the bin/ launcher stub, so this is the real binary under usr/bin.
GITBASH=F:/Tools/PortableGit/usr/bin/_bash.exe
[ -x "$GITBASH" ] || { echo "compare3: FATAL: Git Bash not found at $GITBASH (configured at the top of this script)" >&2; exit 2; }
gitbash="$GITBASH"

# The launcher and clock. Built from tools/benchtimer, never searched for.
TIMER="$root/tools/benchtimer/bin/benchtimer.exe"
[ -x "$TIMER" ] || { echo "compare3: FATAL: the timer is not built: $TIMER (build: dotnet build tools/benchtimer -c Release -o tools/benchtimer/bin)" >&2; exit 2; }

# WSL's bash, as a command prefix: `wsl.exe -e bash` first. System32\bash.exe (the legacy
# launcher) ranks first on a default PATH and pre-empts other shells -- including C#Bash under
# Claude Code -- so it may have been moved away; it is only the fallback (2026-10-03).
wslcmd=()
for c in "${SYSTEMROOT:-/c/WINDOWS}/System32/wsl.exe" /c/WINDOWS/System32/wsl.exe; do
  [ -x "$c" ] && wslcmd=("$c" -e bash) && break
done
if [ ${#wslcmd[@]} -eq 0 ]; then
  for c in "${SYSTEMROOT:-/c/WINDOWS}/System32/bash.exe" /c/WINDOWS/System32/bash.exe; do
    [ -x "$c" ] && wslcmd=("$c") && break
  done
fi
if [ ${#wslcmd[@]} -gt 0 ]; then
  "${wslcmd[@]}" -c 'exit 0' >/dev/null 2>&1 || wslcmd=()     # installed but no distro configured
fi
wsl="${wslcmd[*]}"

# /f/x/y  ->  /mnt/f/x/y
to_wsl() { printf '/mnt%s' "$1"; }

export BENCH_DIR="$here"      # see find.sh: $0 is unusable in a sourced script

# one benchmark in one shell: the best of its rounds, in seconds. The timer prints
# "<min> <total> <n>" and fails (loudly, on stderr) if any launch exits non-zero.
best() {
  local which=$1 script=$2 rounds=$OTHER_ROUNDS out
  [ "$which" = cs ] && rounds=$CS_ROUNDS
  case "$which" in
    cs)   out=$("$TIMER" "$rounds" "$cs" "$script") ;;
    git)  out=$("$TIMER" "$rounds" "$gitbash" "$script") ;;
    # BENCH_DIR is passed explicitly: in a SOURCED script $0 is the shell, not the file, so a
    # benchmark that located itself from $0 built its fixture in /bin and timed the failure.
    wsl)  out=$(MSYS2_ARG_CONV_EXCL='*' "$TIMER" "$rounds" "${wslcmd[@]}" -c "BENCH_DIR=$(to_wsl "$here") . $(to_wsl "$script")") ;;
  esac || { printf 'FAIL'; return; }
  awk -v t="${out%% *}" 'BEGIN{printf "%.3f", t}'
}

ratio() { awk -v a="$1" -v b="$2" 'BEGIN{ if (a ~ /^[0-9.]+$/ && b ~ /^[0-9.]+$/ && b+0 > 0) printf "%.1fx", a/b; else printf "-" }'; }

# --build: the build and the deployed build in ALTERNATING single launches, best of CS_ROUNDS each,
# so both see the same machine. Prints "<build> <deployed>".
best_pair() {
  local script=$1 r out ta="" tb=""
  for ((r = 0; r < CS_ROUNDS; r++)); do
    out=$("$TIMER" 1 "$cs" "$script") || { printf 'FAIL FAIL'; return; }; ta+="${out%% *}"$'\n'
    out=$("$TIMER" 1 "$deployed" "$script") || { printf 'FAIL FAIL'; return; }; tb+="${out%% *}"$'\n'
  done
  printf '%s %s' "$(printf '%s' "$ta" | sort -g | awk 'NR==1{printf "%.3f", $1}')" "$(printf '%s' "$tb" | sort -g | awk 'NR==1{printf "%.3f", $1}')"
}

# --build: startup for both, samples alternating, each row the median of STARTUP_SAMPLES. "<build> <deployed>".
startup_pair() {
  local k out sa="" sb="" exe
  for exe in "$cs" "$deployed"; do "$exe" -c 'exit 7' >/dev/null 2>&1; [ $? -eq 7 ] || { printf 'FAIL FAIL'; return; }; done
  for ((k = 0; k < STARTUP_SAMPLES; k++)); do
    out=$("$TIMER" "$STARTUP_N" "$cs" -c 'exit 0') || { printf 'FAIL FAIL'; return; }
    sa+=$(awk '{printf "%.6f\n", $2/$3}' <<< "$out")$'\n'
    out=$("$TIMER" "$STARTUP_N" "$deployed" -c 'exit 0') || { printf 'FAIL FAIL'; return; }
    sb+=$(awk '{printf "%.6f\n", $2/$3}' <<< "$out")$'\n'
  done
  printf '%s %s' "$(printf '%s' "$sa" | sort -n | awk '{v[NR]=$1} END{printf "%.4f", v[int((NR+1)/2)]}')" \
                 "$(printf '%s' "$sb" | sort -n | awk '{v[NR]=$1} END{printf "%.4f", v[int((NR+1)/2)]}')"
}

# the build's time against the deployed build's, as a signed percentage
change() { awk -v a="$1" -v b="$2" 'BEGIN{ if (a ~ /^[0-9.]+$/ && b ~ /^[0-9.]+$/ && b+0 > 0) printf "%+.1f%%", (a-b)*100/b; else printf "-" }'; }

# per-launch startup of one shell: the median of STARTUP_SAMPLES samples, each the total of STARTUP_N
# launches / STARTUP_N. Gated on a control that the shell really ran a command (exit 7 comes back as
# 7), so a failing launch cannot time fast.
startup() {
  local out k samples=""
  "$@" -c 'exit 7' >/dev/null 2>&1
  [ $? -eq 7 ] || { printf 'FAIL'; return; }
  for ((k = 0; k < STARTUP_SAMPLES; k++)); do
    out=$("$TIMER" "$STARTUP_N" "$@" -c 'exit 0') || { printf 'FAIL'; return; }
    # fields: <n> <total seconds> <launches>; "$@" is the shell, so no `set --` here
    samples+=$(awk '{printf "%.6f\n", $2/$3}' <<< "$out")$'\n'
  done
  printf '%s' "$samples" | sort -n | awk '{v[NR]=$1} END{printf "%.4f", v[int((NR+1)/2)]}'
}

report() {
  echo "date     : $(date '+%Y-%m-%d %H:%M:%S')"
  echo "C#Bash   : $cs   [$cs_from]"
  "$cs" --version 2>/dev/null | tail -1 | sed 's/^/           /'
  if [ -n "$deployed" ]; then
    echo "deployed : $deployed   [timed in alternating rounds with the build]"
    "$deployed" --version 2>/dev/null | tail -1 | sed 's/^/           /'
  fi
  echo "Git Bash : ${gitbash:-<none>}"
  [ -n "$gitbash" ] && "$gitbash" -c 'echo "           bash $BASH_VERSION"' 2>/dev/null
  echo "WSL bash : ${wsl:-<none — skipped>}"
  [ -n "$wsl" ] && "${wslcmd[@]}" -c 'echo "           bash $BASH_VERSION on $(uname -sr)"' 2>/dev/null
  echo "method   : each launch by tools/benchtimer (CreateProcessW, high-resolution clock)"
  echo "           rows = best of $CS_ROUNDS (C#Bash) / best of $OTHER_ROUNDS (Git Bash, WSL); startup = median of $STARTUP_SAMPLES samples, each total of $STARTUP_N launches / $STARTUP_N"
  echo

  if [ -n "$deployed" ]; then
    printf '%-11s %10s %10s %8s %10s %10s   %8s %8s\n' benchmark 'build' 'deployed' 'change' 'Git Bash' 'WSL' 'vs Git' 'vs WSL'
    printf -- '----------------------------------------------------------------------------------------\n'
  else
    printf '%-11s %10s %10s %10s   %8s %8s\n' benchmark 'C#Bash' 'Git Bash' 'WSL' 'vs Git' 'vs WSL'
    printf -- '----------------------------------------------------------------------\n'
  fi

  local b s c d g w mark pair
  for b in loop arith func loop_big coreutils pipeline find; do
    s="$here/$b.sh"
    [ -f "$s" ] || continue
    if [ -n "$deployed" ]; then pair=$(best_pair "$s"); c=${pair% *}; d=${pair#* }; else c=$(best cs "$s"); fi
    g=""; [ -n "$gitbash" ] && g=$(best git "$s")
    w=""; [ -n "$wsl" ] && w=$(best wsl "$s")
    mark=""
    case "$b" in pipeline|find|coreutils) mark=" *" ;; esac
    if [ -n "$deployed" ]; then
      printf '%-11s %10s %10s %8s %10s %10s   %8s %8s%s\n' "$b" "$c" "$d" "$(change "$c" "$d")" "${g:--}" "${w:--}" \
        "$( [ -n "$g" ] && ratio "$g" "$c" || echo - )" \
        "$( [ -n "$w" ] && ratio "$w" "$c" || echo - )" "$mark"
    else
      printf '%-11s %10s %10s %10s   %8s %8s%s\n' "$b" "$c" "${g:--}" "${w:--}" \
        "$( [ -n "$g" ] && ratio "$g" "$c" || echo - )" \
        "$( [ -n "$w" ] && ratio "$w" "$c" || echo - )" "$mark"
    fi
  done

  # ── startup: what a caller pays per invocation (Claude Code pays this per tool call) ──
  if [ -n "$deployed" ]; then pair=$(startup_pair); c=${pair% *}; d=${pair#* }; else c=$(startup "$cs"); fi
  g=""; [ -n "$gitbash" ] && g=$(startup "$gitbash")
  w=""; [ -n "$wsl" ] && w=$(startup "${wslcmd[@]}")
  if [ -n "$deployed" ]; then
    printf '%-11s %10s %10s %8s %10s %10s   %8s %8s\n' startup "$c" "$d" "$(change "$c" "$d")" "${g:--}" "${w:--}" \
      "$( [ -n "$g" ] && ratio "$g" "$c" || echo - )" \
      "$( [ -n "$w" ] && ratio "$w" "$c" || echo - )"
  else
    printf '%-11s %10s %10s %10s   %8s %8s\n' startup "$c" "${g:--}" "${w:--}" \
      "$( [ -n "$g" ] && ratio "$g" "$c" || echo - )" \
      "$( [ -n "$w" ] && ratio "$w" "$c" || echo - )"
  fi
  echo "  * touches the filesystem: WSL reaches these files over /mnt (9P), which is its own cost."
  echo "  startup = bash -c 'exit 0', median of $STARTUP_SAMPLES samples, each the total of $STARTUP_N consecutive launches / $STARTUP_N."

  # ── the outputs must agree, or a time means nothing ──────────────────────────
  echo
  echo "output agreement:"
  local a ok
  for b in loop arith func coreutils pipeline find; do
    s="$here/$b.sh"
    [ -f "$s" ] || continue
    a=$("$cs" "$s" 2>/dev/null)
    ok="C#Bash only"
    if [ -n "$gitbash" ]; then
      if [ "$a" = "$("$gitbash" "$s" 2>/dev/null)" ]; then ok="= Git Bash"; else ok="DIFFERS from Git Bash"; fi
    fi
    if [ -n "$deployed" ]; then
      if [ "$a" = "$("$deployed" "$s" 2>/dev/null)" ]; then ok="$ok, = deployed"; else ok="$ok, DIFFERS from deployed"; fi
    fi
    if [ -n "$wsl" ]; then
      if [ "$a" = "$(MSYS2_ARG_CONV_EXCL='*' "${wslcmd[@]}" -c "BENCH_DIR=$(to_wsl "$here") . $(to_wsl "$s")" 2>/dev/null)" ]; then ok="$ok, = WSL"; else ok="$ok, DIFFERS from WSL"; fi
    fi
    printf '  %-11s %s\n' "$b" "$ok"
  done
}

# Every run is logged, so runs compare over time. The table goes to the terminal as it is produced.
log="$here/results.md"
[ -f "$log" ] || printf '# compare3.sh results\n\nAppended by every run of `tests/bench/compare3.sh`. Newest last. Do not edit by hand.\n' > "$log"
out="$(mktemp)"
report | tee "$out"
{ printf '\n## %s\n\n```\n' "$(date '+%Y-%m-%d %H:%M')"; cat "$out"; printf '```\n'; } >> "$log"
rm -f "$out"
echo
echo "appended to $log"
