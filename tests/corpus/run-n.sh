#!/usr/bin/env bash
# The `-n` corpus: does C#Bash's syntax check (`bash -n FILE`) give GNU bash's verdict on real scripts?
#
#   tests/corpus/run-n.sh [--no-build] [--cs <exe>] [--update-baseline] [--update-manifest]
#                                                                       (run it IN WSL's bash)
# From a Claude Code Bash call, in the repository:
#   wsl.exe -e bash tests/corpus/run-n.sh
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
#     or --cs <exe> (F:/... or /mnt/f/...). It is never the harness: the loop runs in WSL's bash (a
#     C#Bash defect in reading a file list once hid most of a run), and starts C#Bash through WSL's
#     interop with each file's path in Windows form. Until 2026-10-19 the loop ran in Git Bash (Git
#     is now reached nowhere but the benchmark and the publish script).
#   * a file AGREES when both say "fine" (status 0) or both say "error" (non-zero). baseline.txt lists
#     the files that agree; one that stops agreeing fails the run (exit 1); new agreements are
#     reported and folded in with --update-baseline.
#   * --update-manifest re-records the corpus and GNU's verdicts: changing the corpus is a decision
#     recorded in DECISIONS.md, never a routine step.
#   * --printer (2026-10-12, the printer plan's P4) compares, instead of `bash -n`, what `declare -f` prints
#     after each file is sourced (`bash --norc -c 'source FILE; declare -f'`), GNU's measured live in WSL;
#     a file AGREES when the two outputs are byte-identical; printer-baseline.txt is its ratchet; the two
#     outputs of every file are kept in tests/corpus/printer/{gnu,cs} (ignored) for the differences.
# Every run writes its per-file verdicts to tests/corpus/last-run.txt (ignored).
set -u
command -v wslpath > /dev/null || { echo "run-n: run me in WSL's bash, not C#Bash or Git Bash (see the header)" >&2; exit 2; }
here="$(cd "$(dirname "$0")" && pwd)"
root="$(cd "$here/../.." && pwd)"
SRC=/usr/share/bash-completion/completions
files="$here/files"; manifest="$here/manifest.txt"; baseline="$here/baseline.txt"; last="$here/last-run.txt"

usage() { echo "usage: tests/corpus/run-n.sh [--printer] [--no-build] [--cs <exe>] [--update-baseline] [--update-manifest]" >&2; exit 2; }
build=1; cs="$root/Bash/bin/Release/net8.0/Bash.exe"; upd_base=0; upd_man=0; printer=0
while [ $# -gt 0 ]; do
  case "$1" in
    --no-build) build=0; shift ;;
    --cs) [ $# -ge 2 ] || usage; cs="$2"; build=0; shift 2
          case "$cs" in [A-Za-z]:*) cs=$(wslpath -u "$cs") ;; esac ;;
    --update-baseline) upd_base=1; shift ;;
    --update-manifest) upd_man=1; shift ;;
    --printer) printer=1; baseline="$here/printer-baseline.txt"; shift ;;
    *) usage ;;
  esac
done

if [ $build = 1 ]; then
  echo "run-n: building (Release)..." >&2
  dotnet.exe build -c Release "$(wslpath -m "$root/Bash")" -nologo > /dev/null || { echo "run-n: the build failed" >&2; exit 2; }
fi
[ -x "$cs" ] || { echo "run-n: no C#Bash build at $cs" >&2; exit 2; }

# ── fetch the corpus ──────────────────────────────────────────────────────────
rm -rf "$files" && mkdir -p "$files"
tar -C "$SRC" -chf - . 2>/dev/null | tar -C "$files" -xf - || { echo "run-n: fetching $SRC failed" >&2; exit 2; }
wfiles=$(wslpath -m "$files")   # the same folder as C#Bash names it

# ── GNU's verdicts, measured now, in one bash ─────────────────────────────────
gnu=$(bash -c 'cd "$1" && for f in *; do bash -n "$f" >/dev/null 2>&1; echo "$? $f"; done' _ "$SRC") \
  || { echo "run-n: running GNU bash failed" >&2; exit 2; }
gnu_ver=$(bash -c 'echo "$BASH_VERSION"')

# ── the corpus as fetched: sha256 + GNU verdict + name, in name order ─────────
declare -A gv
while read -r s n; do gv[$n]=$s; done <<< "$gnu"
now=$(cd "$files" && sha256sum -- * | while read -r h n; do n=${n#\*}; printf '%s %s %s\n' "$h" "${gv[$n]:-?}" "$n"; done | LC_ALL=C sort -k3)

if [ $upd_man = 1 ]; then
  { echo "# tests/corpus/manifest.txt: sha256, GNU bash $gnu_ver's bash -n status, name. Written by --update-manifest."
    echo "# Source: WSL $SRC ($(dpkg-query -W -f '${Package} ${Version}' bash-completion 2>/dev/null))."
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

: > "$last"
agree=0; total=0; agreeing=()
if [ $printer = 0 ]; then
  # ── C#Bash's `bash -n` verdicts ─────────────────────────────────────────────
  while read -r sha g name; do
    "$cs" -n "$wfiles/$name" > /dev/null 2>&1 < /dev/null; c=$?
    total=$((total + 1))
    if [ $(( g == 0 )) = $(( c == 0 )) ]; then agree=$((agree + 1)); agreeing+=("$name"); v=agree; else v=DIFFER; fi
    printf '%s gnu=%s cs=%s %s\n' "$v" "$g" "$c" "$name" >> "$last"
  done <<< "$want"
  what="-n corpus"
else
  # ── `declare -f` after sourcing each file: GNU in one bash, then C#Bash ────────
  pdir="$here/printer"; rm -rf "$pdir" && mkdir -p "$pdir/gnu" "$pdir/cs"
  bash -c 'cd "$1" && for f in *; do timeout 20 bash --norc -c "source \"./\$1\" >/dev/null 2>&1; declare -f" _ "$f" > "$2/$f" 2>/dev/null < /dev/null; done' \
    _ "$SRC" "$pdir/gnu" || { echo "run-n: GNU's declare -f run failed" >&2; exit 2; }
  while read -r sha g name; do
    timeout 20 "$cs" --norc -c 'source "$1" >/dev/null 2>&1; declare -f' _ "$wfiles/$name" > "$pdir/cs/$name" 2>/dev/null < /dev/null
    total=$((total + 1))
    if cmp -s "$pdir/gnu/$name" "$pdir/cs/$name"; then agree=$((agree + 1)); agreeing+=("$name"); v=agree; else v=DIFFER; fi
    printf '%s %s\n' "$v" "$name" >> "$last"
  done <<< "$want"
  what="printer corpus"
fi

echo "candidate : $cs"
"$cs" --version 2>/dev/null | tail -1 | sed 's/^/            /'
echo "reference : GNU bash $gnu_ver (WSL)"
echo "corpus    : $total files from WSL $SRC, as manifest.txt"
echo "$what : $agree of $total agree with GNU"

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
  { echo "# tests/corpus/$(basename "$baseline"): corpus files that agree with GNU ($what). Written by --update-baseline."
    printf '%s\n' "${agreeing[@]}" | sort; } > "$baseline"
  echo "baseline rewritten: $agree files"; rc=0
fi
echo "per-file verdicts: $last"
exit $rc
