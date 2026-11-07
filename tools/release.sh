#!/usr/bin/env bash
# Release: attach the installer, built from the deployed Bash.exe, and its SHA-256 to a GitHub Release for the published
# hg rev, so nobody has to build C#Bash to use it (docs/installer-plan.md, phase 1; the architect, 2026-10-29: "fix mv
# first and then P1 & 2"). Bash.exe itself is no longer an asset (the architect, 2026-11-07: "drop bash.exe from the
# github release"): it ships inside the installer, with sh.exe, claude.cmd and that build's KNOWN_ISSUES.md.
#
#   bash tools/release.sh <token-file> [--dry-run]     (tools/publish.sh calls it once its push has landed; run it
#                                                        alone to retry a release that failed)
#
# A CONTRACT TOOL. It runs one way:
#   1. the release is for the commit GitHub's main has now (git HEAD after publish), tagged hg-<rev>: the hg rev that
#      commit published, read from its message's first line ("C#Bash at hg rev <rev>: ...");
#   2. the build is dist/Bash.exe, the DEPLOYED build (the one publish's benchmark is of); its stamp names its own rev,
#      which can be older than the published one (records-only revs follow a deploy), and the release text says both,
#      with that Bash.exe's SHA-256;
#   3. a release that already exists for the tag is reused (a retry), and an asset already on it is left; with none,
#      and the newest release's text already naming this Bash.exe's SHA-256, NO release is made: the Releases page
#      lists builds, not publishes (the architect, 2026-10-29: "Yes, I agree; go"). Until hg-657 that SHA-256 was also
#      a Bash.exe.sha256 asset; its text named it too, so the same check reads both;
#   2b. the INSTALLER, shipped from 2026-11-01 (the architect: "Yes, go"): tools/installer.sh builds
#      dist.installer/csharpBash-setup.exe and its .sha256 from this same dist/Bash.exe and checks it by a sandboxed
#      install; they are the release's two assets (until hg-657 Bash.exe and Bash.exe.sha256 were attached beside
#      them). The installer's content follows Bash.exe: a publish whose Bash.exe the newest release already carries
#      makes no release, so a change to the manifest, the start file or KNOWN_ISSUES.md alone ships with the next build;
#   4. after the uploads, the installer is downloaded back from its public URL and must match its SHA-256 (one left on
#      the release by an earlier run: the release's own .sha256), or exit 1;
#   5. this machine's own install (~/.bash/bin) is upgraded by running the downloaded installer --silent (PATH: its
#      default, not added), then checked: the released build (its stamp and SHA-256), sh.exe the same file
#      (2026-11-02), the start file and KNOWN_ISSUES.md (2026-11-07).
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
# 2b. the installer, from this same Bash.exe
setup=$root/dist.installer/csharpBash-setup.exe
bash "$root/tools/installer.sh" > "$work/installer.log" 2>&1 || { cat "$work/installer.log" >&2; die "the installer did not build or failed its sandboxed check"; }
setup_sum=$(sha256sum < "$setup" | cut -c1-64)
[ "$(cut -c1-64 "$setup.sha256")" = "$setup_sum" ] || die "$setup does not match its .sha256"
grep -q "installer: payload from $stamp;" "$work/installer.log" || die "the installer was not built from $stamp"
body=$(printf '%s\n\ncsharpBash-setup.exe installs %s (Native AOT, win-x64: one file, no .NET runtime needed; that Bash.exe'"'"'s SHA-256: %s): Bash.exe, sh.exe (a hard link to it) and the start file claude.cmd into %%USERPROFILE%%/.bash/bin, and this build'"'"'s KNOWN_ISSUES.md into %%USERPROFILE%%/.bash; it asks before adding that folder to PATH (default no).\nInstaller SHA-256: %s\n\nHow to use it with Claude Code: README.md.' \
	"$("$GIT" log -1 --format=%b HEAD)" "$stamp" "$sum" "$setup_sum")
create=$(jq -n --arg t "$tag" --arg c "$head" --arg n "C#Bash hg rev $rev" --arg b "$body" \
	'{tag_name: $t, target_commitish: $c, name: $n, body: $b, draft: false, prerelease: false}') || die "jq failed"
if [ -n "$dry" ]; then
	echo "release: DRY RUN: $tag at git ${head:0:7}; csharpBash-setup.exe SHA-256 $setup_sum, installing $stamp (Bash.exe SHA-256 $sum); it would create (if absent):"
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

# 3. the release: the tag's own (a retry), none (the newest already carries this build), or a new one
split "$(api GET "https://api.github.com/repos/$REPO/releases/tags/$tag")"
if [ "$code" = 404 ]; then
	# through the API, never the releases/latest/download/ link: asking that link before the release exists left
	# GitHub's edge redirecting it to the PREVIOUS release's Bash.exe.sha256 for a while after hg-600 (2026-11-01).
	# The newest release's TEXT names its Bash.exe's SHA-256 (every release's has, hg-657's and before as "SHA-256: <sum>")
	newest=$(curl -s "https://api.github.com/repos/$REPO/releases/latest" | jq -r '.body // empty')
	if [ -n "$newest" ] && grep -qF "$sum" <<< "$newest"; then
		echo "release: the newest release already carries this Bash.exe ($stamp, SHA-256 $sum): no release for $tag"
		exit 0
	fi
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
upload "$setup" csharpBash-setup.exe application/octet-stream
upload "$setup.sha256" csharpBash-setup.exe.sha256 text/plain

# 4. downloaded back, as a user would
surl="https://github.com/$REPO/releases/download/$tag/csharpBash-setup.exe"
# an installer already on the release (a retry) is checked against the release's own .sha256: a rebuild here is not
# byte-identical (InstallGen's output differs from build to build), so this run's setup_sum names a different file
exp=$setup_sum
grep -qxF csharpBash-setup.exe <<< "$have" && exp=$(curl -s -L "$surl.sha256" | cut -c1-64)
inst="$work/csharpBash-setup.exe"
curl -s -L -o "$inst" "$surl" || { echo "release: could not download $surl" >&2; exit 1; }
got=$(sha256sum < "$inst" | cut -c1-64)
[ "$got" = "$exp" ] || { echo "release: $surl gives SHA-256 $got, not $exp" >&2; exit 1; }
echo "release: $tag = git ${head:0:7}: csharpBash-setup.exe ($stamp inside) downloaded back, SHA-256 $exp matches: $surl"

# 5. this machine's own install, upgraded "for free" by the installer just released, as a user gets it (the architect,
#    2026-11-02: "publish should prolly upgrade it 'for free'?"): --silent, so PATH takes its default (not added);
#    then the installed Bash.exe must report the released build and sh.exe be the same file. Every publish is thereby
#    a real install of the shipped installer. A failure here comes after the release landed: it says so and exits 1.
bin="$HOME/.bash/bin"
"$inst" --silent > "$work/install.log" 2>&1; irc=$?
[ $irc -eq 0 ] || { cat "$work/install.log" >&2; echo "release: the released installer exited $irc on this machine (the release itself landed)" >&2; exit 1; }
istamp=$("$bin/Bash.exe" --version 2>/dev/null | tail -1 | sed 's/^ *//')
[ "$istamp" = "$stamp" ] || { echo "release: $bin/Bash.exe reports '$istamp', not '$stamp' after the install" >&2; exit 1; }
isum=$(sha256sum < "$bin/Bash.exe" | cut -c1-64)
[ "$isum" = "$sum" ] || { echo "release: $bin/Bash.exe has SHA-256 $isum, not the released build's $sum" >&2; exit 1; }
[ "$bin/sh.exe" -ef "$bin/Bash.exe" ] || grep -q "sh.exe: copy of Bash.exe" "$work/install.log" ||
	{ echo "release: $bin/sh.exe is not Bash.exe after the install" >&2; exit 1; }
# the start file this machine starts Claude Code with (C:/Users/guy/bin/claude.bat hands off to it since 2026-11-07)
cmp -s "$bin/claude.cmd" "$root/dist.installer/payload/claude.cmd" ||
	{ echo "release: $bin/claude.cmd is not the released start file after the install" >&2; exit 1; }
# and the known issues of that build, beside unserviced.jsonl (2026-11-07)
grep -qxF "**This copy:** installed with $stamp." "$HOME/.bash/KNOWN_ISSUES.md" 2>/dev/null ||
	{ echo "release: $HOME/.bash/KNOWN_ISSUES.md does not name $stamp after the install" >&2; exit 1; }
echo "release: this machine's install upgraded by it: $bin/Bash.exe = $stamp; $(grep -o 'sh\.exe: \(hard link\|copy of\)[^\r]*' "$work/install.log" | head -1)"
