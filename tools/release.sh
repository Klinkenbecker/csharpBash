#!/usr/bin/env bash
# Release: attach the deployed Bash.exe and its SHA-256 to a GitHub Release for the published hg rev, so nobody has to
# build C#Bash to use it (docs/installer-plan.md, phase 1; the architect, 2026-10-29: "fix mv first and then P1 & 2").
#
#   bash tools/release.sh <token-file> [--dry-run]     (tools/publish.sh calls it once its push has landed; run it
#                                                        alone to retry a release that failed)
#
# A CONTRACT TOOL. It runs one way:
#   1. the release is for the commit GitHub's main has now (git HEAD after publish), tagged hg-<rev>: the hg rev that
#      commit published, read from its message's first line ("C#Bash at hg rev <rev>: ...");
#   2. the asset is dist/Bash.exe, the DEPLOYED build (the one publish's benchmark is of); its stamp names its own rev,
#      which can be older than the published one (records-only revs follow a deploy), and the release text says both;
#   3. a release that already exists for the tag is reused (a retry), and an asset already on it is left;
#   4. after the uploads, Bash.exe is downloaded back from its public URL and must match its SHA-256, or exit 1.
# The token reaches curl on its standard input (`--config -`), never on a command line, and nothing prints it.
# --dry-run: everything up to the first request, then what would be sent; needs no token and no network.
set -u
root="$(cd "$(dirname "$0")/.." && pwd)"
cd "$root" || exit 2
die() { echo "release: $*" >&2; exit 2; }
GIT=F:/Tools/PortableGit/bin/git.exe   # as publish.sh: configured, never searched for
REPO=Klinkenbecker/csharpBash
[ $# -ge 1 ] || die "usage: tools/release.sh <token-file> [--dry-run]"
tokfile=$1; dry=""; [ "${2:-}" = --dry-run ] && dry=1
[ -n "$dry" ] || [ -s "$tokfile" ] || die "no token file: $tokfile"
for t in curl jq sha256sum; do command -v $t > /dev/null || die "needs $t"; done
[ -x "$GIT" ] || die "Git not found at $GIT"

# 1. what is released
head=$("$GIT" rev-parse HEAD) || die "git rev-parse failed"
first=$("$GIT" log -1 --format=%s HEAD)
[[ $first =~ ^C#Bash\ at\ hg\ rev\ ([0-9]+):\  ]] || die "HEAD's message does not start 'C#Bash at hg rev N: ' (it is: $first)"
rev=${BASH_REMATCH[1]}; tag=hg-$rev
# 2. the asset
exe=$root/dist/Bash.exe
[ -f "$exe" ] || die "no $exe"
stamp=$("$exe" --version 2>/dev/null | tail -1 | sed 's/^ *//')
[ -n "$stamp" ] || die "$exe gives no version stamp"
binrev=${stamp##* }
[[ $binrev =~ ^[0-9]+$ ]] && [ "$binrev" -le "$rev" ] ||
	die "dist/Bash.exe is rev $binrev, newer than the published rev $rev: a release carries a build of what it publishes (publish first)"
sum=$(sha256sum < "$exe" | cut -c1-64)
work=$(mktemp -d) || die "mktemp failed"
trap 'rm -rf "$work"' EXIT
printf '%s  Bash.exe\n' "$sum" > "$work/Bash.exe.sha256"
body=$(printf '%s\n\nBash.exe: %s (Native AOT, win-x64: one file, no .NET runtime needed)\nSHA-256: %s\n\nHow to use it with Claude Code: README.md.' \
	"$("$GIT" log -1 --format=%b HEAD)" "$stamp" "$sum")
create=$(jq -n --arg t "$tag" --arg c "$head" --arg n "C#Bash hg rev $rev" --arg b "$body" \
	'{tag_name: $t, target_commitish: $c, name: $n, body: $b, draft: false, prerelease: false}') || die "jq failed"
if [ -n "$dry" ]; then
	echo "release: DRY RUN: $tag at git ${head:0:7}; Bash.exe = $stamp, SHA-256 $sum; it would create (if absent):"
	printf '%s\n' "$create"
	exit 0
fi

# api METHOD URL [curl options]: prints the response body, then its HTTP code on a line of its own
api() {
	local m=$1 u=$2; shift 2
	{ printf 'header = "Authorization: Bearer %s"\n' "$(tr -d '\r\n' < "$tokfile")"
	  printf 'header = "Accept: application/vnd.github+json"\n'
	  printf 'header = "X-GitHub-Api-Version: 2022-11-28"\n'; } |
		curl -s -S -X "$m" --config - -w '\n%{http_code}' "$@" "$u"
}
split() { code=${1##*$'\n'}; json=${1%$'\n'*}; }
why() { jq -r '.message // empty' <<< "$json" 2> /dev/null; }

# 3. the release: the tag's own (a retry) or a new one
split "$(api GET "https://api.github.com/repos/$REPO/releases/tags/$tag")"
if [ "$code" = 404 ]; then
	split "$(api POST "https://api.github.com/repos/$REPO/releases" -H 'Content-Type: application/json' --data-binary "$create")"
	[ "$code" = 201 ] || die "creating release $tag failed: HTTP $code $(why)"
	echo "release: $tag created"
elif [ "$code" = 200 ]; then echo "release: $tag exists; reused"
else die "looking up release $tag failed: HTTP $code $(why)"; fi
up=$(jq -r '.upload_url | sub("\\{.*$"; "")' <<< "$json")
have=$(jq -r '.assets[].name' <<< "$json")
upload() {
	local file=$1 name=$2 type=$3
	if grep -qxF "$name" <<< "$have"; then echo "release: $name already on $tag; left"; return 0; fi
	split "$(api POST "$up?name=$name" -H "Content-Type: $type" --data-binary "@$file")"
	[ "$code" = 201 ] || die "uploading $name failed: HTTP $code $(why)"
	echo "release: $name uploaded"
}
upload "$exe" Bash.exe application/octet-stream
upload "$work/Bash.exe.sha256" Bash.exe.sha256 text/plain

# 4. downloaded back, as a user would
url="https://github.com/$REPO/releases/download/$tag/Bash.exe"
got=$(curl -s -L "$url" | sha256sum | cut -c1-64)
[ "$got" = "$sum" ] || { echo "release: $url gives SHA-256 $got, not $sum" >&2; exit 1; }
echo "release: $tag = git ${head:0:7}: Bash.exe ($stamp) downloaded back, SHA-256 $sum matches: $url"
