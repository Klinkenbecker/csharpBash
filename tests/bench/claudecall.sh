#!/usr/bin/env bash
# The cost of a Claude Code Bash call, and where it goes (the performance review, phase R1; ratified 2026-10-16,
# DECISIONS "RATIFIED: the performance review plan").
#
#   bash tests/bench/claudecall.sh              the deployed build (dist/Bash.exe): whole-call times
#   bash tests/bench/claudecall.sh --build      the committed tree, published AOT here, against the deployed build;
#                                               with the anatomy (the timing switch exists from rev 271)
#   bash tests/bench/claudecall.sh --cs <exe>   another build
#
# A CONTRACT TOOL: it runs one way, and changing that is the architect's decision. Any bash may run it.
#   * Each case is a Claude Code Bash call made exactly as Claude Code makes one on Windows (its own command line,
#     read 2026-10-16): <shell> -c "source <snapshot> 2>/dev/null || true && export TEMP='<temp>' TMP='<temp>' &&
#     shopt -u extglob 2>/dev/null || true && { \builtin unalias -- 'unsetenv'; \builtin unset -f -- 'unsetenv'; }
#     >/dev/null 2>&1 || true && eval '<command>' && pwd -P >| <cwd file>", paths in /c/... form, launched by
#     tools/benchtimer (CreateProcessW, the way Claude Code starts a shell) from the repository root. The row
#     "bare" is `-c 'exit 0'`, compare3's startup row, for scale.
#   * The snapshot is tests/bench/claudecall/snapshot.sh, a live one (2026-10-16, 4.9 KB) with the user name
#     scrubbed; its PATH line is filled at run time with this machine's PATH in Windows form, as Claude Code's
#     C#Bash-made snapshots have it.
#   * C#Bash only. Git Bash was a column on the first two runs and hung in them: its `git status` blocks when this
#     C#Bash-made snapshot is sourced with the wrapper's other parts (not with either alone, not without the
#     snapshot; measured 2026-10-16). Git Bash in Claude Code sources a snapshot it made itself, so the column was
#     never like for like; compare3.sh compares the three shells.
#   * Each launch round has a 300 s limit, so a hang fails the run instead of stalling it.
#   * Each row: the median of 5 samples, each the total of 20 consecutive launches / 20 (compare3's startup
#     method), the build and the deployed build in alternating rounds.
#   * The anatomy (C#Bash with the timing switch): 20 more launches with BASH_TIMING_LOG set, each segment's mean
#     over them, from process creation to the exit mark; the rest (the call's mean with the switch on, minus the
#     exit mark) is process exit, and over 10% of the call it reads UNACCOUNTED (the plan's criterion).
#   * The results are printed and appended to tests/bench/claudecall-results.md with the build, date and method.
set -u
here="$(cd "$(dirname "$0")" && pwd)"
root="$(cd "$here/../.." && pwd)"
cd "$root" || exit 2
die() { echo "claudecall: $*" >&2; exit 2; }
usage() { echo "usage: tests/bench/claudecall.sh [--build | --cs <exe>]   (the method is fixed; see the header)" >&2; exit 2; }

cs="$root/dist/Bash.exe"; cs_from="dist (deployed)"; build=0
while [ $# -gt 0 ]; do
  case "$1" in
    --build) build=1; shift ;;
    --cs) [ $# -ge 2 ] || usage; cs="$2"; cs_from="--cs $2"; shift 2 ;;
    *) usage ;;
  esac
done
TIMER="$root/tools/benchtimer/bin/benchtimer.exe"
[ -x "$TIMER" ] || die "the timer is not built: $TIMER (build: dotnet build tools/benchtimer -c Release -o tools/benchtimer/bin)"
command -v cygpath >/dev/null || die "cygpath (Git for Windows' usr/bin) is not on PATH"

deployed=""
if [ $build = 1 ]; then
  # as compare3.sh --build: a clean tree, published as shipped, its stamp checked
  dirty=$(hg status -mard) || die "hg status failed"
  [ -z "$dirty" ] || die "the working tree has uncommitted changes; commit first (the stamp must name a revision):
$dirty"
  node=$(hg id -i); rev=$(hg id -n)
  aot="$here/aot"
  echo "claudecall --build: publishing rev $rev ($node) with tools/publish-aot.cmd to $aot (log: $here/aot.log)" >&2
  MSYS2_ARG_CONV_EXCL='*' cmd.exe /c "$(cygpath -w "$root/tools/publish-aot.cmd")" "$(cygpath -w "$aot")" > "$here/aot.log" 2>&1 \
    || { tail -5 "$here/aot.log" >&2; die "the publish FAILED"; }
  "$aot/Bash.exe" --version 2>/dev/null | grep -q "hg\.$node $rev\$" || die "$aot/Bash.exe does not carry the stamp of rev $rev ($node)"
  deployed="$cs"; cs="$aot/Bash.exe"; cs_from="--build: rev $rev, NOT deployed"
fi
[ -x "$cs" ] || die "no C#Bash build at $cs"

# ── the inputs ─────────────────────────────────────────────────────────────────────
tmp=$(mktemp -d) || die "mktemp failed"
trap 'rm -rf "$tmp"' EXIT
msys() { local w; w=$(cygpath -m "$1") || return 1; printf '/%s%s' "$(printf %s "${w:0:1}" | tr 'A-Z' 'a-z')" "${w:2}"; }
wtemp=$(cygpath -w "$(cd "$tmp/.." && pwd)")
if [[ $PATH == *';'* ]]; then wpath=$PATH; else wpath=$(cygpath -wp "$PATH"); fi
fixture="$here/claudecall/snapshot.sh"
grep -qx 'export PATH=@PATH@' "$fixture" || die "the fixture has no 'export PATH=@PATH@' line: $fixture"
snapshot() {   # $1 the PATH value, $2 the file to write
  { sed '/^export PATH=@PATH@$/,$d' "$fixture"; printf 'export PATH=%q\n' "$1"; sed '1,/^export PATH=@PATH@$/d' "$fixture"; } > "$2"
  }
snapshot "$wpath" "$tmp/snap-cs.sh"
cwdfile=$(msys "$tmp")/claude-cwd
wrap() {   # $1 the snapshot (MSYS form), $2 the command: Claude Code's -c string
  printf "source %s 2>/dev/null || true && export TEMP='%s' TMP='%s' && shopt -u extglob 2>/dev/null || true && { \\\\builtin unalias -- 'unsetenv'; \\\\builtin unset -f -- 'unsetenv'; } >/dev/null 2>&1 || true && eval '%s' && pwd -P >| %s" \
    "$1" "$wtemp" "$wtemp" "$2" "$cwdfile"
  }
snapcs=$(msys "$tmp/snap-cs.sh")

names=(bare true ls cat-head grep-rn git-status cd-pwd)
cmds=("" "true" "ls" "cat README.md | head -5" "grep -rn Timing Bash/Evaluator" "git status --short" "cd Bash && pwd")
SAMPLES=5; N=20

# ── one sample: the mean of N launches, in ms ──────────────────────────────────────
sample() {   # $1 exe, $2 snapshot, $3 command ("" = bare)
  local out
  if [ -z "$3" ]; then out=$(MSYS2_ARG_CONV_EXCL='*' timeout 300 "$TIMER" $N "$1" -c 'exit 0') || return 1
  else out=$(MSYS2_ARG_CONV_EXCL='*' timeout 300 "$TIMER" $N "$1" -c "$(wrap "$2" "$3")") || return 1; fi
  awk '{ printf "%.3f\n", $2 / $3 * 1000 }' <<< "$out"
  }
median() { sort -n | awk '{ a[NR] = $1 } END { print a[int((NR + 1) / 2)] }'; }

stamp=$("$cs" --version 2>/dev/null | tail -1)
hdr=$(printf 'date     : %s\nC#Bash   : %s   [%s]\n           %s\n' "$(date '+%Y-%m-%d %H:%M:%S')" "$cs" "$cs_from" "$stamp"
  [ -n "$deployed" ] && printf 'deployed : %s\n           %s\n' "$deployed" "$("$deployed" --version 2>/dev/null | tail -1)"
  printf 'method   : Claude Code'"'"'s -c wrapper with the snapshot fixture, launched by tools/benchtimer from the repo root;\n'
  printf '           each row the median of %d samples of %d launches (mean per launch), builds in alternating rounds\n' $SAMPLES $N)
echo "$hdr"; echo

# (the loops run in this shell and write to files: a failure inside $( ) or a pipeline would end only a subshell)
{
  if [ -n "$deployed" ]; then printf '%-11s %9s %9s %8s\n' call "C#Bash" deployed change
  else printf '%-11s %9s\n' call "C#Bash"; fi
  echo "------------------------------------------"
  for i in "${!names[@]}"; do
    a=(); b=()
    for s in $(seq $SAMPLES); do
      v=$(sample "$cs" "$snapcs" "${cmds[$i]}") || die "the ${names[$i]} call failed under $cs"; a+=("$v")
      if [ -n "$deployed" ]; then v=$(sample "$deployed" "$snapcs" "${cmds[$i]}") || die "the ${names[$i]} call failed under $deployed"; b+=("$v"); fi
    done
    ma=$(printf '%s\n' "${a[@]}" | median)
    if [ -n "$deployed" ]; then
      mb=$(printf '%s\n' "${b[@]}" | median)
      awk -v n="${names[$i]}" -v a="$ma" -v b="$mb" 'BEGIN { printf "%-11s %8.1fms %8.1fms %+7.1f%%\n", n, a, b, (a - b) / b * 100 }'
    else
      awk -v n="${names[$i]}" -v a="$ma" 'BEGIN { printf "%-11s %8.1fms\n", n, a }'
    fi
  done
} > "$tmp/table.txt"
table=$(cat "$tmp/table.txt")
echo "$table"; echo

# ── the anatomy: where a call's time goes (C#Bash builds with the timing switch) ─────
anat=""
probe="$tmp/probe.log"
BASH_TIMING_LOG=$(cygpath -m "$probe") "$cs" -c 'exit 0' >/dev/null 2>&1
if [ -s "$probe" ]; then
  : > "$tmp/anat.tsv"
    for i in "${!names[@]}"; do
      [ $i -eq 0 ] && continue
      log="$tmp/anat-$i.log"; rm -f "$log"
      out=$(BASH_TIMING_LOG=$(cygpath -m "$log") MSYS2_ARG_CONV_EXCL='*' timeout 300 "$TIMER" $N "$cs" -c "$(wrap "$snapcs" "${cmds[$i]}")") || die "the ${names[$i]} call failed with the timing switch on"
      whole=$(awk '{ printf "%.3f", $2 / $3 * 1000 }' <<< "$out")
      # every block after the first (benchtimer's untimed warm-up launch), averaged
      awk -v whole="$whole" -v col=$i '
        function seg(name, v) { sum[name] += v; if (!(name in seen)) { seen[name] = 1; order[++k] = name } }
        /^pid / { blk++; delete t; ext = 0; next }
        blk > 1 {
          ms = $1; ev = $2
          if (ev == "ext+") xs = ms; else if (ev == "ext-") ext += ms - xs
          else t[ev] = ms
          if (ev == "exit") {
            seg("runtime start (creation to Main)", t["main"])
            seg("console set-up", t["console"] - t["main"])
            seg("evaluator (environment, PATH)", t["evaluator"] - t["console"])
            seg("options, startup files", t["ready"] - t["evaluator"])
            seg("parse the call, reach source", t["source+"] - t["ready"])
            seg("snapshot (source)", t["source-"] - t["source+"])
            seg("wrapper builtins", t["eval+"] - t["source-"])
            seg("the command (eval)", t["eval-"] - t["eval+"])
            seg("  of which external programs", ext)
            seg("cwd write-back (pwd -P >|)", t["done"] - t["eval-"])
            seg("exit handler", t["exit"] - t["done"])
            seg("= the exit mark", t["exit"])
            n++
          }
        }
        END {
          for (j = 1; j <= k; j++) printf "%d\t%s\t%.2f\n", col, order[j], sum[order[j]] / n
          ex = sum["= the exit mark"] / n
          printf "%d\t%s\t%.2f\n", col, "process exit (after the mark)", whole - ex
          printf "%d\t%s\t%.2f\n", col, "whole call (switch on)", whole
          printf "%d\t%s\t%s\n", col, "criterion: exit <= 10%", ((whole - ex) / whole <= 0.10) ? "met" : "UNACCOUNTED"
        }' "$log" >> "$tmp/anat.tsv"
    done
  { printf '%-34s' segment; for i in "${!names[@]}"; do [ $i -gt 0 ] && printf '%11s' "${names[$i]}"; done; echo
    echo "---------------------------------------------------------------------------------------------"
    awk -F'\t' '
      { if (!($2 in seen)) { seen[$2] = 1; order[++k] = $2 } v[$2, $1] = $3; if ($1 > maxc) maxc = $1 }
      END { for (j = 1; j <= k; j++) { printf "%-34s", order[j]; for (c = 1; c <= maxc; c++) printf "%11s", v[order[j], c]; printf "\n" } }' "$tmp/anat.tsv"; } > "$tmp/anat.txt"
  anat=$(cat "$tmp/anat.txt")
  echo "anatomy (ms, mean of $N launches with BASH_TIMING_LOG set, $cs):"
  echo "$anat"
else
  echo "anatomy: not available ($cs has no timing switch; it exists from rev 271)"
fi

{ echo; echo "## $(date '+%Y-%m-%d %H:%M') claudecall.sh"; echo; echo '```'; echo "$hdr"; echo; echo "$table"
  [ -n "$anat" ] && { echo; echo "anatomy (ms, mean of $N launches with BASH_TIMING_LOG set):"; echo "$anat"; }
  echo '```'; } >> "$here/claudecall-results.md"
echo; echo "appended to $here/claudecall-results.md"
