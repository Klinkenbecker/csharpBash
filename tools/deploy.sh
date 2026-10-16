#!/usr/bin/env bash
# Deploy the committed tree as dist/Bash.exe, the shell every Claude Code session on this machine runs.
#
#   bash tools/deploy.sh       (on the architect's "deploy"; from a Claude Code Bash call, i.e. C#Bash, or Git Bash)
#
# Any bash runs it. Until 2026-10-14 it refused C#Bash, a guard copied from compare3.sh with no reason of its
# own; every command form here was then run under Git Bash, the deployed C#Bash and the next one with identical
# results, renaming a running image included (DECISIONS 2026-10-14, the architect: "drop it, go").
#
# A CONTRACT TOOL (2026-10-12, the architect: "repetitive tools here need to be contracts, period").
# Until then each deploy was done by hand from DECISIONS' notes. It runs one way:
#   1. the tree must be clean: the deployed stamp names a revision;
#   2. tools/publish-aot.cmd publishes it to dist.staging (ignored), NEVER to dist/ (the publish
#      empties its output directory, where the rollback copies live); the stamp must carry the node;
#   3. the suite runs on that exact binary, with a console and -Detached: any failure stops here;
#   4. the battery runs (its Release build is the same tree): a baseline regression stops here;
#   5. the running dist/Bash.exe is RENAMED to dist/Bash.exe.rev<old> (Windows renames a running image
#      and cannot overwrite it) and the new one copied in; its stamp is checked again;
#   6. it prints the notice material: the revisions now deployed (hg log since the old one).
# What it does not do, and Claude does next, by the same rule every time: a live check from a Claude
# Code Bash call (`echo $BASH_VERSION; "$BASH" --version`, plus the deploy's headline fixes), then the
# notice to every live local session (ListAgents) leading with the behaviour changes, then the
# DECISIONS entry and the anchor. Rolling back is the rename in reverse.
set -u
root="$(cd "$(dirname "$0")/.." && pwd)"
cd "$root" || exit 2
stage="$root/dist.staging"; dist="$root/dist"
die() { echo "deploy: $*" >&2; exit 2; }

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
echo "deploy: rev $rev ($node) over the deployed rev $old_rev"

# 2. publish to staging
echo "deploy: publishing with tools/publish-aot.cmd to $stage (log: $root/dist.staging.log)"
MSYS2_ARG_CONV_EXCL='*' cmd.exe /c "$(cygpath -w "$root/tools/publish-aot.cmd")" "$(cygpath -w "$stage")" > "$root/dist.staging.log" 2>&1 \
  || { tail -5 "$root/dist.staging.log" >&2; die "the publish FAILED"; }
new="$stage/Bash.exe"
new_stamp=$("$new" --version 2>/dev/null | tail -1)
[[ "$new_stamp" == *"hg.$node $rev" ]] || die "the published build's stamp is '$new_stamp', not rev $rev ($node)"

# 3. the suite on that exact binary, both modes
for mode in console -Detached; do
  args=(-Interpreter "$(cygpath -w "$new")"); [ "$mode" = -Detached ] && args+=(-Detached)
  res=$(powershell.exe -NoProfile -ExecutionPolicy Bypass -File "$(cygpath -w "$root/tests/run-tests.ps1")" "${args[@]}" 2>&1 | grep -E 'passed, [0-9]+ failed' | tail -1)
  echo "deploy: suite ($mode): $res"
  [[ "$res" == *" 0 failed" ]] || die "the suite failed on $new ($mode); not deployed"
done

# 4. the battery
bat=$(powershell.exe -NoProfile -ExecutionPolicy Bypass -File "$(cygpath -w "$root/tests/compat/run-compat.ps1")" 2>&1)
bat_rc=$?
echo "deploy: battery: $(grep -E 'pass, [0-9]+ diff' <<< "$bat" | tail -1)"
[ $bat_rc -eq 0 ] || { grep -E 'REGRESS|FAIL' <<< "$bat" >&2; die "the battery regressed; not deployed"; }

# 5. swap: rename the running image, copy the new one in
mv "$dist/Bash.exe" "$dist/Bash.exe.rev$old_rev" || die "could not rename the running dist/Bash.exe"
cp "$new" "$dist/Bash.exe" || { mv "$dist/Bash.exe.rev$old_rev" "$dist/Bash.exe"; die "copy failed; the old build is back"; }
got=$("$dist/Bash.exe" --version 2>/dev/null | tail -1)
[ "$got" = "$new_stamp" ] || die "dist/Bash.exe reads '$got' after the copy, not '$new_stamp'"
echo "deploy: DEPLOYED $got; rollback: dist/Bash.exe.rev$old_rev"

# 6. notice material
echo
echo "revisions now deployed (rev $old_rev -> $rev):"
hg log -r "$((old_rev + 1)):$rev" --template '  {rev}: {desc|firstline}\n'
