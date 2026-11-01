#!/usr/bin/env bash
# Build the C#Bash installer from the DEPLOYED dist/Bash.exe (docs/installer-plan.md, phases 3 and 4).
#
#   bash tools/installer.sh       (C#Bash; the architect's go, 2026-11-01: "Yes, go, do")
#
# A CONTRACT TOOL. It runs one way:
#   1. the payload, staged in dist.installer/payload (ignored): dist/Bash.exe, and installer/claude.cmd with CRLF line
#      ends (the start file, cmd's own format); the manifest installer/csharpBash.json with @VERSION@ written from
#      dist/Bash.exe's stamp ("1.0.0 (hg N)");
#   2. InstallGen (specification 2, configured below, by path: it is on no PATH) validates and builds with --release,
#      its release gate (every warning refused), to dist.installer/csharpBash-setup.exe and its .sha256, which must
#      check;
#   3. the installer proves itself: a silent install into dist.installer/sandbox through a test-redirection file
#      (home sent there, PATH skipped; nothing outside the sandbox is touched), then: exit 0; Bash.exe reports the
#      deployed stamp; sh.exe is the same file (`-ef`, C#Bash 579+) and the report says "hard link to Bash.exe
#      (confirmed)"; claude.cmd is the start file, byte for byte;
#   4. installer/try-install.cmd (CRLF) beside the installer: a REAL install from a Command Prompt, then its checks.
# What it does not do: release it. The architect, 2026-11-01: "We're not shipping until I say so." tools/release.sh
# does not attach it until he does.
set -u
root="$(cd "$(dirname "$0")/.." && pwd)"
cd "$root" || exit 2
die() { echo "installer: $*" >&2; exit 2; }
[[ $root == [A-Za-z]:/* ]] || die "the repository's path is '$root', not a drive path (X:/...): run this in C#Bash"
IG=E:/Claude/Installer/release/InstallGen.exe
[ -x "$IG" ] || die "InstallGen not found at $IG (configured at the top of this script)"
out="$root/dist.installer"; payload="$out/payload"; setup="$out/csharpBash-setup.exe"; sandbox="$out/sandbox"

# 1. the payload and the manifest
stamp=$("$root/dist/Bash.exe" --version 2>/dev/null | tail -1)
[[ $stamp == "C#Bash build "* ]] || die "cannot read dist/Bash.exe's stamp (got '$stamp')"
rev=${stamp##* }; ver=${stamp#C#Bash build }; ver=${ver%%+*}
rm -rf "$root/dist.installer"
mkdir -p "$payload" || die "cannot make $payload"
cp "$root/dist/Bash.exe" "$payload/Bash.exe" || die "cannot stage Bash.exe"
sed 's/$/\r/' "$root/installer/claude.cmd" > "$payload/claude.cmd" || die "cannot stage claude.cmd"
sed "s/@VERSION@/$ver (hg $rev)/" "$root/installer/csharpBash.json" > "$out/csharpBash.json" || die "cannot write the manifest"
echo "installer: payload from $stamp; product version $ver (hg $rev)"

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
echo "installer: sandboxed install checked: Bash.exe $rev, sh.exe a hard link (confirmed), claude.cmd (report: $out/sandbox-report.txt)"

# 4. beside it, the script for a REAL install from a Command Prompt (the architect, 2026-11-01: "give me a script I can
#    run from cmd line"): runs the installer, then checks what it installed
sed 's/$/\r/' "$root/installer/try-install.cmd" > "$out/try-install.cmd" || die "cannot write try-install.cmd"
echo "installer: for a real install, from a Command Prompt: $out/try-install.cmd"
