#!/usr/bin/env bash
# Build the C#Bash installer from the DEPLOYED dist/Bash.exe (docs/installer-plan.md, phases 3 and 4).
#
#   bash tools/installer.sh       (C#Bash; the architect's go, 2026-11-01: "Yes, go, do")
#
# A CONTRACT TOOL. It runs one way:
#   1. the payload, staged in dist.installer/payload (ignored): dist/Bash.exe, installer/claude.cmd with CRLF line
#      ends (the start file, cmd's own format), and KNOWN_ISSUES.md with its "This copy" line naming the build (for
#      %USERPROFILE%/.bash, beside unserviced.jsonl; 2026-11-07); Wombat's deploy/git.exe (the build installer/wombat.build
#      names, refused otherwise), etc/gitconfig and an empty etc/gitattributes, Wombat's three user files for
#      %USERPROFILE%/.bash/wombat (2026-11-27); the manifest installer/csharpBash.json with @VERSION@ written from
#      dist/Bash.exe's stamp ("1.0.0 (hg N)");
#   2. InstallGen (specification 2; its location from INSTALLGEN or tools/installgen.path, never written here) validates and builds with --release,
#      its release gate (every warning refused), to dist.installer/csharpBash-setup.exe and its .sha256, which must
#      check;
#   3. the installer proves itself: a silent install into dist.installer/sandbox through a test-redirection file
#      (home sent there, PATH skipped; nothing outside the sandbox is touched), then: exit 0; Bash.exe reports the
#      deployed stamp; sh.exe is the same file (`-ef`, C#Bash 579+) and the report says "hard link to Bash.exe
#      (confirmed)"; claude.cmd is the start file, byte for byte; KNOWN_ISSUES.md is the staged one, in the home folder;
#      git.exe reports the pinned Wombat build; etc/gitconfig, etc/gitattributes and wombat/ are the staged ones;
#   4. installer/try-install.cmd (CRLF) beside the installer: a REAL install from a Command Prompt, then its checks.
# What it does not do: release it. The architect, 2026-11-01: "We're not shipping until I say so." tools/release.sh
# does not attach it until he does.
set -u
root="$(cd "$(dirname "$0")/.." && pwd)"
cd "$root" || exit 2
die() { echo "installer: $*" >&2; exit 2; }
[[ $root == [A-Za-z]:/* ]] || die "the repository's path is '$root', not a drive path (X:/...): run this in C#Bash"
# InstallGen's location belongs to this machine, so no published file names it: INSTALLGEN in the environment, or the
# one line in tools/installgen.path (ignored by hg and git; the architect, 2026-11-01)
IG=${INSTALLGEN:-}
[ -n "$IG" ] || { [ -f "$root/tools/installgen.path" ] && IG=$(head -1 "$root/tools/installgen.path" | tr -d '\r'); }
[ -n "$IG" ] || die "InstallGen's location is not set: write InstallGen.exe's path into tools/installgen.path, or set INSTALLGEN"
[ -x "$IG" ] || die "InstallGen not found at $IG (from INSTALLGEN or tools/installgen.path)"
# Wombat's git.exe ships too (the architect, 2026-11-27: "IT SHIPS IN THE INSTALLER, IT GOES IN THE USER DOCS"): its
# repository folder belongs to this machine as InstallGen's does (WOMBAT, or the one line in tools/wombat.path, ignored);
# the build shipped is the one installer/wombat.build names, the third line of its `git.exe --version`, as the Wombat
# session names it fit to ship; deploy/git.exe and Wombat's three user files must all be that build
W=${WOMBAT:-}
[ -n "$W" ] || { [ -f "$root/tools/wombat.path" ] && W=$(head -1 "$root/tools/wombat.path" | tr -d '\r'); }
[ -n "$W" ] || die "Wombat's location is not set: write its repository folder into tools/wombat.path, or set WOMBAT"
wgit="$W/deploy/wombat.exe"; [ -x "$wgit" ] || die "Wombat's wombat.exe not found at $wgit"   # git.exe is a hard link to it (his ruling, 2026-11-27)
wbuild=$(head -1 "$root/installer/wombat.build" | tr -d '\r')
[[ $wbuild == "Wombat build "* ]] || die "installer/wombat.build does not name a Wombat build (got '$wbuild')"
wgot=$("$wgit" --version < /dev/null 2>/dev/null | sed -n 3p | tr -d '\r')
[ "$wgot" = "$wbuild" ] || die "$wgit is '$wgot', not the build installer/wombat.build names ('$wbuild')"
wrev=${wbuild##* }
for f in KNOWN_ISSUES.md KNOWN_LIMITATIONS.md docs/USING.md; do
	head -1 "$W/$f" 2>/dev/null | grep -qF "(build $wrev)" || die "Wombat's $f is not build $wrev's (its title: '$(head -1 "$W/$f" 2>/dev/null)')"
done
out="$root/dist.installer"; payload="$out/payload"; setup="$out/csharpBash-setup.exe"; sandbox="$out/sandbox"

# 1. the payload and the manifest
stamp=$("$root/dist/Bash.exe" --version 2>/dev/null | tail -1)
[[ $stamp == "C#Bash build "* ]] || die "cannot read dist/Bash.exe's stamp (got '$stamp')"
rev=${stamp##* }; ver=${stamp#C#Bash build }; ver=${ver%%+*}
rm -rf "$root/dist.installer"
mkdir -p "$payload" || die "cannot make $payload"
cp "$root/dist/Bash.exe" "$payload/Bash.exe" || die "cannot stage Bash.exe"
sed 's/$/\r/' "$root/installer/claude.cmd" > "$payload/claude.cmd" || die "cannot stage claude.cmd"
# KNOWN_ISSUES.md, for %USERPROFILE%/.bash beside unserviced.jsonl, its "This copy" line naming the build it is installed
# with (the architect, 2026-11-07: carried forward, updated and installed with every publish)
sed "s|^\*\*This copy:\*\*.*|**This copy:** installed with $stamp.|" "$root/KNOWN_ISSUES.md" > "$payload/KNOWN_ISSUES.md" ||
	die "cannot stage KNOWN_ISSUES.md"
grep -qxF "**This copy:** installed with $stamp." "$payload/KNOWN_ISSUES.md" || die "KNOWN_ISSUES.md has no '**This copy:**' line to name the build"
# KNOWN_LIMITATIONS.md beside it, stamped the same way (the architect, 2026-11-12: issues, being worked on, and limitations,
# each with its better way, in two files)
sed "s|^\*\*This copy:\*\*.*|**This copy:** installed with $stamp.|" "$root/KNOWN_LIMITATIONS.md" > "$payload/KNOWN_LIMITATIONS.md" ||
	die "cannot stage KNOWN_LIMITATIONS.md"
grep -qxF "**This copy:** installed with $stamp." "$payload/KNOWN_LIMITATIONS.md" || die "KNOWN_LIMITATIONS.md has no '**This copy:**' line to name the build"
# Wombat: git.exe for bin; ~/.bash/etc/gitconfig (core.symlinks = false, Wombat's standing ruling) and an empty
# gitattributes, the bytes as agreed 2026-11-11; its three user files in ~/.bash/wombat, where their names do not
# collide with C#Bash's and their links to each other still work
cp "$wgit" "$payload/wombat.exe" || die "cannot stage wombat.exe"   # the installer makes git.exe a hard link to it
printf '[core]\n\tsymlinks = false\n' > "$payload/gitconfig" || die "cannot stage gitconfig"
: > "$payload/gitattributes" || die "cannot stage gitattributes"
mkdir -p "$payload/wombat/docs" || die "cannot make $payload/wombat/docs"
for f in KNOWN_ISSUES.md KNOWN_LIMITATIONS.md docs/USING.md README.md; do cp "$W/$f" "$payload/wombat/$f" || die "cannot stage Wombat's $f"; done
sed "s/@VERSION@/$ver (hg $rev)/" "$root/installer/csharpBash.json" > "$out/csharpBash.json" || die "cannot write the manifest"
echo "installer: payload from $stamp and $wbuild; product version $ver (hg $rev)"

# 2. validate and build, the release gate
"$IG" validate "$out/csharpBash.json" --payload "$payload" --release || die "InstallGen refused the manifest"
"$IG" --manifest "$out/csharpBash.json" --payload "$payload" --out "$setup" --release --quiet || die "InstallGen failed to build"
( cd "$out" && sha256sum -c --quiet csharpBash-setup.exe.sha256 ) || die "the installer does not match its .sha256"
echo "installer: built $setup ($(stat -c %s "$setup") bytes) and its .sha256 (checked)"

# 3. the installer proves itself, in a sandbox
mkdir -p "$sandbox" || die "cannot make $sandbox"
printf '{ "schema": 2, "places": { "home": "%s/home" }, "skip": [ "path" ] }\n' "$sandbox" > "$out/test.json"
"$setup" --test "$out/test.json" --silent > "$out/sandbox-report.txt" 2>&1; rc=$?
[ $rc -eq 0 ] || { cat "$out/sandbox-report.txt" >&2; die "the sandboxed install exited $rc"; }
bin="$sandbox/home/bin"
got=$("$bin/Bash.exe" --version 2>/dev/null | tail -1)
[ "$got" = "$stamp" ] || die "the installed Bash.exe reports '$got', not '$stamp'"
[ "$bin/sh.exe" -ef "$bin/Bash.exe" ] || die "the installed sh.exe is not the same file as Bash.exe"
grep -q "sh.exe: hard link to Bash.exe (confirmed)" "$out/sandbox-report.txt" || die "the report does not confirm sh.exe's hard link"
cmp -s "$bin/claude.cmd" "$payload/claude.cmd" || die "the installed claude.cmd differs from the payload's"
cmp -s "$sandbox/home/KNOWN_ISSUES.md" "$payload/KNOWN_ISSUES.md" || die "the installed KNOWN_ISSUES.md differs from the payload's"
cmp -s "$sandbox/home/KNOWN_LIMITATIONS.md" "$payload/KNOWN_LIMITATIONS.md" || die "the installed KNOWN_LIMITATIONS.md differs from the payload's"
wgi=$("$bin/git.exe" --version < /dev/null 2>/dev/null | sed -n 3p | tr -d '\r')
[ "$wgi" = "$wbuild" ] || die "the installed git.exe is '$wgi', not '$wbuild'"
[ "$bin/git.exe" -ef "$bin/wombat.exe" ] || die "the installed git.exe is not the same file as wombat.exe"
grep -q "git.exe: hard link to wombat.exe (confirmed)" "$out/sandbox-report.txt" || die "the report does not confirm git.exe's hard link to wombat.exe"
cmp -s "$sandbox/home/etc/gitconfig" "$payload/gitconfig" || die "the installed etc/gitconfig differs from the payload's"
[ -f "$sandbox/home/etc/gitattributes" ] && [ ! -s "$sandbox/home/etc/gitattributes" ] || die "the installed etc/gitattributes is missing or not empty"
for f in KNOWN_ISSUES.md KNOWN_LIMITATIONS.md docs/USING.md README.md; do
	cmp -s "$sandbox/home/wombat/$f" "$payload/wombat/$f" || die "the installed wombat/$f differs from the payload's"
done
echo "installer: sandboxed install checked: Bash.exe $rev, sh.exe a hard link (confirmed), claude.cmd, KNOWN_ISSUES.md, KNOWN_LIMITATIONS.md, wombat.exe ($wbuild) with git.exe a hard link (confirmed), etc/gitconfig, etc/gitattributes, wombat/ (report: $out/sandbox-report.txt)"

# 4. beside it, the script for a REAL install from a Command Prompt (the architect, 2026-11-01: "give me a script I can
#    run from cmd line"): runs the installer, then checks what it installed
sed 's/$/\r/' "$root/installer/try-install.cmd" > "$out/try-install.cmd" || die "cannot write try-install.cmd"
echo "installer: for a real install, from a Command Prompt: $out/try-install.cmd"
