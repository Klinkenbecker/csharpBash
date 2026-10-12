#!/usr/bin/env bash
# The `-n` corpus: does C#Bash's syntax check (`bash -n FILE`) give GNU bash's verdict on real scripts?
#
#   tests/corpus/run-n.sh [--no-build] [--cs <exe>] [--update-baseline] [--update-manifest]
#                                                                       (run it FROM Git Bash)
# On the development machine:
#   CHERE_INVOKING=1 F:/Tools/PortableGit/usr/bin/_bash.exe -l -c 'cd /f/Koliada/Tools/bash && "$BASH" tests/corpus/run-n.sh'
#
# THIS IS A CONTRACT TOOL (2026-10-12, the architect: "repetitive tools here need to be contracts,
# period"). It ran from a scratch directory and a command recovered from a transcript until then.
# It runs one way:
#   * the corpus is WSL's /usr/share/bash-completion/completions (Ubuntu's bash-completion package,
#     912 scripts). They are third-party files, so they are NOT in this repository: each run fetches
#     them (symlinks dereferenced) into tests/corpus/files (ignored) and checks every file against
#     manifest.txt (sha256, GNU's verdict, name). A missing, extra or changed file is CORPUS DRIFT and
#     the run stops (exit 2): a different corpus is a different measurement.
#   * GNU bash's verdicts are measured again every run, in WSL, on the same files; one that differs
#     from the manifest is REFERENCE DRIFT (exit 2).
#   * the candidate is the Release build, built first (as tests/run-tests.ps1 does), unless --no-build
#     or --cs <exe>. It is never the harness: the loop runs in Git Bash (a C#Bash defect in reading a
#     file list once hid most of a run).
#   * a file AGREES when both say "fine" (status 0) or both say "error" (non-zero). baseline.txt lists
#     the files that agree; one that stops agreeing fails the run (exit 1); new agreements are
#     reported and folded in with --update-baseline.
#   * --update-manifest re-records the corpus and GNU's verdicts: changing the corpus is a decision
#     recorded in DECISIONS.md, never a routine step.
# Every run writes its per-file verdicts to tests/corpus/last-run.txt (ignored).
set -u
case "${BASH_VERSION:-}" in *koliada*) echo "run-n: run me under Git Bash, not C#Bash (see the header)" >&2; exit 2 ;; esac
here="$(cd "$(dirname "$0")" && pwd)"
root="$(cd "$here/../.." && pwd)"
SRC=/usr/share/bash-completion/completions
files="$here/files"; manifest="$here/manifest.txt"; baseline="$here/baseline.txt"; last="$here/last-run.txt"

usage() { echo "usage: tests/corpus/run-n.sh [--no-build] [--cs <exe>] [--update-baseline] [--update-manifest]" >&2; exit 2; }
build=1; cs="$root/Bash/bin/Release/net8.0/Bash.exe"; upd_base=0; upd_man=0
while [ $# -gt 0 ]; do
  case "$1" in
    --no-build) build=0; shift ;;
    --cs) [ $# -ge 2 ] || usage; cs="$2"; build=0; shift 2 ;;
    --update-baseline) upd_base=1; shift ;;
    --update-manifest) upd_man=1; shift ;;
    *) usage ;;
  esac
done

# Git Bash must not rewrite the Linux paths; wsl.exe's own "Failed to translate" notes about Windows
# PATH entries are dropped (only those lines)
wsl() { MSYS2_ARG_CONV_EXCL='*' wsl.exe -e "$@" 2> >(grep -v '^wsl: Failed to translate' >&2); }
wsl true 2>/dev/null || { echo "run-n: WSL is not available (wsl.exe -e)" >&2; exit 2; }

if [ $build = 1 ]; then
  echo "run-n: building (Release)..." >&2
  dotnet build -c Release "$(cygpath -w "$root/Bash")" -nologo > /dev/null || { echo "run-n: the build failed" >&2; exit 2; }
fi
[ -x "$cs" ] || { echo "run-n: no C#Bash build at $cs" >&2; exit 2; }

# ── fetch the corpus ──────────────────────────────────────────────────────────
rm -rf "$files" && mkdir -p "$files"
wsl tar -C "$SRC" -chf - . 2>/dev/null | tar -C "$files" -xf - || { echo "run-n: fetching $SRC from WSL failed" >&2; exit 2; }

# ── GNU's verdicts, measured now, in one WSL bash ────────────────────────────────
gnu=$(wsl bash -c 'cd "$1" && for f in *; do bash -n "$f" >/dev/null 2>&1; echo "$? $f"; done' _ "$SRC") \
  || { echo "run-n: running GNU bash in WSL failed" >&2; exit 2; }
gnu_ver=$(wsl bash -c 'echo "$BASH_VERSION"')

# ── the corpus as fetched: sha256 + GNU verdict + name, in name order ─────────
declare -A gv
while read -r s n; do gv[$n]=$s; done <<< "$gnu"
now=$(cd "$files" && sha256sum -- * | while read -r h n; do n=${n#\*}; printf '%s %s %s\n' "$h" "${gv[$n]:-?}" "$n"; done | LC_ALL=C sort -k3)

if [ $upd_man = 1 ]; then
  { echo "# tests/corpus/manifest.txt: sha256, GNU bash $gnu_ver's bash -n status, name. Written by --update-manifest."
    echo "# Source: WSL $SRC ($(wsl dpkg-query -W -f '${Package} ${Version}' bash-completion 2>/dev/null))."
    printf '%s\n' "$now"; } > "$manifest"
  echo "run-n: manifest re-recorded ($(printf '%s\n' "$now" | wc -l) files); record why in DECISIONS.md" >&2
fi
[ -f "$manifest" ] || { echo "run-n: no manifest; record one with --update-manifest" >&2; exit 2; }

want=$(grep -v '^#' "$manifest")
if [ "$want" != "$now" ]; then
  echo "run-n: DRIFT: the corpus or GNU's verdicts are not the manifest's (exit 2). Differences (< manifest, > now):" >&2
  diff <(printf '%s\n' "$want") <(printf '%s\n' "$now") | grep '^[<>]' | head -20 >&2
  exit 2
fi

# ── C#Bash's verdicts ─────────────────────────────────────────────────────────
: > "$last"
agree=0; total=0; agreeing=()
while read -r sha g name; do
  "$cs" -n "$files/$name" > /dev/null 2>&1 < /dev/null; c=$?
  total=$((total + 1))
  if [ $(( g == 0 )) = $(( c == 0 )) ]; then agree=$((agree + 1)); agreeing+=("$name"); v=agree; else v=DIFFER; fi
  printf '%s gnu=%s cs=%s %s\n' "$v" "$g" "$c" "$name" >> "$last"
done <<< "$want"

echo "candidate : $cs"
"$cs" --version 2>/dev/null | tail -1 | sed 's/^/            /'
echo "reference : GNU bash $gnu_ver (WSL)"
echo "corpus    : $total files from WSL $SRC, as manifest.txt"
echo "-n corpus : $agree of $total agree with GNU"

# ── the ratchet ───────────────────────────────────────────────────────────────
rc=0
if [ -f "$baseline" ]; then
  lost=$(comm -23 <(grep -v '^#' "$baseline" | sort) <(printf '%s\n' "${agreeing[@]}" | sort))
  gained=$(comm -13 <(grep -v '^#' "$baseline" | sort) <(printf '%s\n' "${agreeing[@]}" | sort))
  base_n=$(grep -vc '^#' "$baseline")
  echo "baseline  : $base_n"
  [ -n "$gained" ] && echo "NEW AGREEMENT (not in baseline): $(printf '%s ' $gained)"
  if [ -n "$lost" ]; then echo "REGRESSION (in baseline, no longer agree): $(printf '%s ' $lost)"; rc=1; fi
elif [ $upd_base = 0 ]; then
  echo "run-n: no baseline yet; record one with --update-baseline" >&2; rc=2
fi
if [ $upd_base = 1 ]; then
  { echo "# tests/corpus/baseline.txt: corpus files whose bash -n verdict agrees with GNU's. Written by --update-baseline."
    printf '%s\n' "${agreeing[@]}" | sort; } > "$baseline"
  echo "baseline rewritten: $agree files"; rc=0
fi
echo "per-file verdicts: $last"
exit $rc
