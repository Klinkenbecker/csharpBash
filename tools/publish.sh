#!/usr/bin/env bash
# Publish: bring GitHub's main up to the committed hg tree (hg is the development VCS; git only mirrors it).
#
#   bash tools/publish.sh <message-file> <token-file>      (on the architect's "push"; C#Bash or Git Bash)
#
# Any bash runs it. Until 2026-10-14 it refused C#Bash, a guard copied from compare3.sh with no reason of its
# own; every command form here was then run under Git Bash, the deployed C#Bash and the next one with identical
# results: the history table byte for byte, the staging list, base64 and the token mask (DECISIONS 2026-10-14).
#
# A CONTRACT TOOL (2026-10-12, the architect: "repetitive tools here need to be contracts, period").
# Until then each push was done by hand. Before it runs, by the push rule of 2026-10-10 ("another
# benchmark and a doc review first"): `tests/bench/compare3.sh` on the DEPLOYED build (checked here: the
# newest such table in results.md must carry dist/Bash.exe's stamp), and the doc review committed in hg
# (README's performance table from that run and its case count, ARCHITECTURE, IMPLEMENTATION). The
# message file is the git commit message: its first line must be "C#Bash at hg rev <tip>: <headline>".
# It runs one way:
#   1. the hg tree must be clean; tip = rev N;
#   2. the benchmark check above;
#   3. MERCURIAL_HISTORY.md: the table regenerated from `hg log N:0` (`|` in a summary escaped as `\|`)
#      and the changeset count; mercurial-history.hg: `hg bundle --all`, verified by unbundling it into
#      a fresh repository and comparing the tip. Both are ignored by hg and exist only in git;
#   4. git stages EXACTLY hg's tracked files (those .gitignore does not exclude), the two history files
#      and deletions; any other untracked file is listed and never added;
#   5. commit with the message file; push with the token as an http.extraHeader given on the command
#      line only (git 2.28 here: its credential prompt needs /dev/tty and it predates GIT_CONFIG_COUNT),
#      output masked; then the header must be absent from git config and the remote main must be HEAD.
# The token is briefly visible on git's command line to processes of the same user.
set -u
root="$(cd "$(dirname "$0")/.." && pwd)"
cd "$root" || exit 2
die() { echo "publish: $*" >&2; exit 2; }
[ $# -eq 2 ] || die "usage: tools/publish.sh <message-file> <token-file>"
msg=$1; tokfile=$2
[ -s "$msg" ] || die "no message file: $msg"
[ -s "$tokfile" ] || die "no token file: $tokfile"

# 1. clean hg tree
dirty=$(hg status -mard) || die "hg status failed"
[ -z "$dirty" ] || die "the hg tree has uncommitted changes; commit first:
$dirty"
tip=$(hg id -n); node=$(hg id -i)
first=$(head -1 "$msg")
[[ "$first" == "C#Bash at hg rev $tip: "* ]] || die "the message's first line must start 'C#Bash at hg rev $tip: ' (it is: $first)"

# 2. the benchmark of the deployed build
stamp=$("$root/dist/Bash.exe" --version 2>/dev/null | tail -1 | sed 's/^ *//')
bench=$(awk '/^C#Bash +: .*\[dist \(deployed\)\]/{want=1} want && /C#Bash build/{print; want=0}' tests/bench/results.md | tail -1 | sed 's/^ *//')
[ "$bench" = "$stamp" ] || die "the newest compare3 run on the deployed build is for '$bench', but dist/Bash.exe is '$stamp': run tests/bench/compare3.sh first"
echo "publish: hg rev $tip ($node); benchmark of the deployed build ($stamp) found"

# 3. the history files
count=$((tip + 1))
{ sed -n '1,/^|----:|/p' MERCURIAL_HISTORY.md | sed "s/[0-9]\{1,\} changesets\./$count changesets./"
  hg log -r "$tip:0" --template '{rev}\t{date|shortdate}\t{desc|firstline}\n' |
  while IFS=$'\t' read -r r d s; do s=${s//|/\\|}; printf '| %s | %s | %s |\n' "$r" "$d" "$s"; done
} > MERCURIAL_HISTORY.md.new || die "could not write the history table"
mv MERCURIAL_HISTORY.md.new MERCURIAL_HISTORY.md
rm -f mercurial-history.hg
hg bundle --all mercurial-history.hg -q || die "hg bundle failed"
chk=$(mktemp -d)
( cd "$chk" && hg init -q v && cd v && hg unbundle -q "$root/mercurial-history.hg" && hg log -r tip --template '{node|short}' ) > "$chk/tip" 2>/dev/null
[ "$(cat "$chk/tip")" = "$node" ] || { rm -rf "$chk"; die "the bundle does not unbundle to tip $node"; }
rm -rf "$chk"
echo "publish: MERCURIAL_HISTORY.md ($count changesets) and mercurial-history.hg (verified) written"

# 4. stage exactly what hg tracks
mapfile -t tracked < <(hg files | tr '\\' '/')
mapfile -t ignored < <(printf '%s\n' "${tracked[@]}" | git check-ignore --stdin 2>/dev/null)
git add -u || die "git add -u failed"
git add -- MERCURIAL_HISTORY.md mercurial-history.hg || die "git add of the history files failed"
printf '%s\n' "${tracked[@]}" | grep -vxF -f <(printf '%s\n' "${ignored[@]}" "") | tr '\n' '\0' | xargs -0 git add -- || die "git add failed"
stray=$(git ls-files --others --exclude-standard)
[ -n "$stray" ] && printf 'publish: NOT published (untracked in hg):\n%s\n' "$stray" | sed 's/^/  /'

# 5. commit and push
git commit -q -F "$msg" || die "git commit failed"
head=$(git rev-parse HEAD)
token=$(tr -d '\r\n' < "$tokfile")
auth=$(printf 'Klinkenbecker:%s' "$token" | base64 | tr -d '\n')
git -c credential.helper= -c "http.https://github.com/.extraHeader=Authorization: Basic $auth" push origin main 2>&1 | sed "s#$token#***#g; s#$auth#***#g"
unset token auth
[ -z "$(git config --get-all http.https://github.com/.extraHeader)" ] || die "an extraHeader is in git config: remove it NOW"
remote=$(git ls-remote origin refs/heads/main | cut -f1)
[ "$remote" = "$head" ] || die "the push did not land: remote main is $remote, HEAD is $head"
echo "publish: GitHub main = git $(git rev-parse --short HEAD) = hg rev $tip"
