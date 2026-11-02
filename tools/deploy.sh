#!/usr/bin/env bash
# Deploy the committed tree as dist/Bash.exe, the shell every Claude Code session on this machine runs.
#
#   bash tools/deploy.sh       (on the architect's "deploy"; from a Claude Code Bash call, i.e. C#Bash)
#
# Run it in C#Bash: its paths are already Windows form (F:/...), which cmd.exe, PowerShell and .NET take as they
# are, so the paths handed to them are used as written. Until 2026-10-20 they went through `cygpath -w`, a leftover
# from running in Git Bash (whose paths are /f/...); cygpath is Git's and left PATH with it on 2026-10-19, which
# stopped the deploy of rev 367 (DECISIONS 2026-10-20). A shell whose paths are not Windows form is refused below.
# (Until 2026-10-14 it refused C#Bash, a guard copied from compare3.sh with no reason of its own: DECISIONS
# 2026-10-14, the architect: "drop it, go".)
#
# A CONTRACT TOOL (2026-10-12, the architect: "repetitive tools here need to be contracts, period").
# Until then each deploy was done by hand from DECISIONS' notes. It runs one way:
#   1. the tree must be clean: the deployed stamp names a revision;
#   2. tools/publish-aot.cmd publishes it to dist.staging (ignored), NEVER to dist/ (the publish
#      empties its output directory, where the rollback copies live); the stamp must carry the node;
#   3. the suite runs on that exact binary -Detached, as Claude Code starts the shell: any failure stops here. The
#      console run moved to tools/publish.sh (the architect, 2026-11-01: "On deploy, keep 'detatched mode' run, drop
#      'console mode'. On publish, add 'console mode' (detached should have already been run).");
#   4. the battery runs (its Release build is the same tree): a baseline regression stops here;
#   5. the swap, as InstallGen replaces a file in use (2026-11-01): the new build copied IN FULL beside the old
#      (dist/Bash.deploy-new.exe, its stamp checked) and sh.exe's new HARD LINK made to it (a copy only if a link
#      cannot be made; `sh` for scripts and programs that run it, as Git for Windows and MSYS2 ship theirs); then
#      each name takes two renames: the running image aside to dist/<name>.rev<old> (Windows renames a running image
#      and cannot overwrite it; it keeps running), the new one in. A name is missing only between two renames. A
#      refused rename (a process STARTING from the file, measured) puts back what changed and stops: run it again;
#   6. it prints the notice material: the revisions now deployed (hg log since the old one).
# What it does not do, and Claude does next, by the same rule every time: a live check from a Claude Code Bash call
# (`"$BASH" --version`, the deploy's headline fixes), one message to the session whose report a fix answers (no
# broadcast: the architect, 2026-11-01, each message buries that session's work on his screen), then the DECISIONS
# entry and the anchor. Rolling back is the rename in reverse.
set -u
root="$(cd "$(dirname "$0")/.." && pwd)"
cd "$root" || exit 2
stage="$root/dist.staging"; dist="$root/dist"
die() { echo "deploy: $*" >&2; exit 2; }
[[ $root == [A-Za-z]:/* ]] || die "the repository's path is '$root', not a drive path (X:/...): run this in C#Bash, from a drive (cmd.exe cannot cd to a UNC path)"

# 1. a clean tree
command -v hg >/dev/null || die "hg is not on PATH"
dirty=$(hg status -mard) || die "hg status failed"
[ -z "$dirty" ] || die "the working tree has uncommitted changes; commit first:
$dirty"
node=$(hg id -i); rev=$(hg id -n)
old_stamp=$("$dist/Bash.exe" --version 2>/dev/null | tail -1)
old_rev=${old_stamp##* }
[ -n "$old_rev" ] || die "cannot read the deployed build's stamp ($dist/Bash.exe --version)"
[ "$old_rev" != "$rev" ] || die "rev $rev is already deployed ($old_stamp)"
[ ! -e "$dist/Bash.exe.rev$old_rev" ] || die "a rollback copy dist/Bash.exe.rev$old_rev already exists; resolve it by hand"
[ ! -e "$dist/sh.exe.rev$old_rev" ] || die "a rollback copy dist/sh.exe.rev$old_rev already exists; resolve it by hand"
echo "deploy: rev $rev ($node) over the deployed rev $old_rev"

# 2. publish to staging
echo "deploy: publishing with tools/publish-aot.cmd to $stage (log: $root/dist.staging.log)"
MSYS2_ARG_CONV_EXCL='*' cmd.exe /c "$root/tools/publish-aot.cmd" "$stage" > "$root/dist.staging.log" 2>&1 \
  || { tail -5 "$root/dist.staging.log" >&2; die "the publish FAILED"; }
new="$stage/Bash.exe"
new_stamp=$("$new" --version 2>/dev/null | tail -1)
[[ "$new_stamp" == *"hg.$node $rev" ]] || die "the published build's stamp is '$new_stamp', not rev $rev ($node)"

# 3. the suite on that exact binary, detached (the console run is publish's, 2026-11-01)
res=$(powershell.exe -NoProfile -ExecutionPolicy Bypass -File "$root/tests/run-tests.ps1" -Interpreter "$new" -Detached 2>&1 | grep -E 'passed, [0-9]+ failed' | tail -1)
echo "deploy: suite (-Detached): $res"
[[ "$res" == *" 0 failed" ]] || die "the suite failed on $new (-Detached); not deployed"

# 4. the battery, against WSL's GNU bash (its reference since 2026-10-19: Git is reached nowhere but the benchmark
#    and the publish script)
bat=$(powershell.exe -NoProfile -ExecutionPolicy Bypass -File "$root/tests/compat/run-compat.ps1" 2>&1)
bat_rc=$?
echo "deploy: battery: $(grep -E 'pass, [0-9]+ diff' <<< "$bat" | tail -1)"
[ $bat_rc -eq 0 ] || { grep -E 'REGRESS|FAIL' <<< "$bat" >&2; die "the battery regressed; not deployed"; }

# 5. swap, as InstallGen replaces a file in use (installer-a4, 2026-11-01; the architect: "Why aren't you using the swap
#    trick to deploy?"): the new build is copied IN FULL beside the old under a temporary .exe name (and checked), and
#    sh.exe's new hard link made to it, while the old names still work; then each name takes two renames: the running
#    image aside (it keeps running; the aside copy is the rollback), the new one in. A name is missing only between those
#    two renames, no longer for a 7.8 MB copy. A refused rename (a process STARTING from the file, measured 2026-11-01)
#    puts back what changed and stops: run it again. No retry, as the installer.
nb="$dist/Bash.deploy-new.exe"; ns="$dist/sh.deploy-new.exe"
rm -f "$nb" "$ns"
cp "$new" "$nb" || { rm -f "$nb"; die "could not copy the new build in beside the old; nothing changed"; }
got=$("$nb" --version 2>/dev/null | tail -1)
[ "$got" = "$new_stamp" ] || { rm -f "$nb"; die "the copy reads '$got', not '$new_stamp'; nothing changed"; }
# sh.exe is a HARD LINK to the new Bash.exe, made anew every deploy so it never keeps an old build (the architect,
# 2026-11-01: "deploy should make sh.exe a hard link also", as the installer makes it); confirmed, else a copy, said so
if ln "$nb" "$ns" 2>/dev/null && [ "$nb" -ef "$ns" ]; then
  sh_how="a hard link to Bash.exe (confirmed)"
else
  rm -f "$ns"
  cp "$new" "$ns" || { rm -f "$nb" "$ns"; die "could not link or copy sh.exe's new file; nothing changed"; }
  sh_how="a COPY of Bash.exe (the hard link could not be made or confirmed)"
fi
undo_bash() { mv "$dist/Bash.exe" "$nb" && mv "$dist/Bash.exe.rev$old_rev" "$dist/Bash.exe"; }
mv "$dist/Bash.exe" "$dist/Bash.exe.rev$old_rev" ||
  { rm -f "$nb" "$ns"; die "could not set the running dist/Bash.exe aside (a process starting from it?); nothing changed: run again"; }
mv "$nb" "$dist/Bash.exe" ||
  { mv "$dist/Bash.exe.rev$old_rev" "$dist/Bash.exe"; rm -f "$nb" "$ns"; die "could not move the new Bash.exe in; the old one is back: run again"; }
sh_back=
if [ -e "$dist/sh.exe" ]; then
  mv "$dist/sh.exe" "$dist/sh.exe.rev$old_rev" ||
    { undo_bash; rm -f "$nb" "$ns"; die "could not set the running dist/sh.exe aside; Bash.exe put back, nothing changed: run again"; }
  sh_back=" and dist/sh.exe.rev$old_rev"
fi
mv "$ns" "$dist/sh.exe" ||
  { [ -n "$sh_back" ] && mv "$dist/sh.exe.rev$old_rev" "$dist/sh.exe"; undo_bash; rm -f "$nb" "$ns"; die "could not move the new sh.exe in; both put back, nothing changed: run again"; }
[[ $sh_how == *COPY* ]] || [ "$dist/sh.exe" -ef "$dist/Bash.exe" ] || die "dist/sh.exe is not the same file as dist/Bash.exe after the swap"
got_sh=$("$dist/sh.exe" --version 2>/dev/null | tail -1)
[ "$got_sh" = "$new_stamp" ] || die "dist/sh.exe reads '$got_sh', not '$new_stamp'"
echo "deploy: DEPLOYED $got (Bash.exe; sh.exe is $sh_how); rollback: dist/Bash.exe.rev$old_rev$sh_back"

# 6. notice material
echo
echo "revisions now deployed (rev $old_rev -> $rev):"
hg log -r "$((old_rev + 1)):$rev" --template '  {rev}: {desc|firstline}\n'
