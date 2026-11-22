#!/usr/bin/env bash
# Deploy the committed tree as ~/.bash/bin/Bash.exe, the shell every Claude Code session on this machine starts on, and
# as dist/Bash.exe, the build publish and release take. The architect, 2026-11-17: "All local claude sessions use
# ~/.bash/ for find bash. deploy is supposed to deploy to ~/.bash/ so that all local session pick up the latest". Since
# 2026-11-07 the sessions started on ~/.bash/bin (DECISIONS "RECORDED: the Claude Code start procedure") and this
# script still swapped dist alone, so no deploy reached a session until a publish (DECISIONS 2026-11-17 "RULED + BUILT:
# deploy goes into ~/.bash/bin").
#
#   bash tools/deploy.sh       (by the bug and feature process, once the suite passes; from a Claude Code Bash call)
#
# Run it in C#Bash: its paths are already Windows form (F:/...), which cmd.exe and .NET take as they
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
#   3. the suite runs on that exact binary --detached, as Claude Code starts the shell: any failure stops here. The
#      console run moved to tools/publish.sh (the architect, 2026-11-01: "On deploy, keep 'detatched mode' run, drop
#      'console mode'. On publish, add 'console mode' (detached should have already been run).");
#   4. the battery runs (its Release build is the same tree): a baseline regression stops here;
#   5. the swap, into dist/ and then ~/.bash/bin (each skipped when it already holds this rev, so a deploy stopped
#      between them is run again), with KNOWN_ISSUES.md and KNOWN_LIMITATIONS.md copied to ~/.bash as the installer
#      puts them; ~/.bash/bin keeps only the newest rollback copy (an older one in use stays until a later deploy).
#      Each swap is the one InstallGen makes for a file in use (2026-11-01): the new build copied IN FULL beside the old
#      (<folder>/Bash.deploy-new.exe, its stamp checked) and sh.exe's new HARD LINK made to it (a copy only if a link
#      cannot be made; `sh` for scripts and programs that run it, as Git for Windows and MSYS2 ship theirs); then
#      each name takes two renames: the running image aside to <folder>/<name>.rev<old> (Windows renames a running image
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
# ~/.bash/bin: where the sessions start (the installer's folder); on a machine without it, dist alone, said so
home_bin="${USERPROFILE//\\//}/.bash/bin"; home_old=
if [ -f "$home_bin/Bash.exe" ]; then
  home_stamp=$("$home_bin/Bash.exe" --version 2>/dev/null | tail -1); home_old=${home_stamp##* }
  [ -n "$home_old" ] || die "cannot read the installed build's stamp ($home_bin/Bash.exe --version)"
else
  echo "deploy: no $home_bin/Bash.exe (C#Bash is not installed here): dist alone" >&2
fi
to_dist=; [ "$old_rev" != "$rev" ] && to_dist=1
to_home=; [ -n "$home_old" ] && [ "$home_old" != "$rev" ] && to_home=1
[ -n "$to_dist$to_home" ] || die "rev $rev is already deployed ($old_stamp)${home_old:+, and in $home_bin}"
for d in ${to_dist:+"$dist:$old_rev"} ${to_home:+"$home_bin:$home_old"}; do
  [ ! -e "${d%:*}/Bash.exe.rev${d##*:}" ] || die "a rollback copy ${d%:*}/Bash.exe.rev${d##*:} already exists; resolve it by hand"
  [ ! -e "${d%:*}/sh.exe.rev${d##*:}" ] || die "a rollback copy ${d%:*}/sh.exe.rev${d##*:} already exists; resolve it by hand"
done
echo "deploy: rev $rev ($node) over ${to_dist:+rev $old_rev in dist}${to_dist:+${to_home:+ and }}${to_home:+rev $home_old in ~/.bash/bin}"

# 2. publish to staging
echo "deploy: publishing with tools/publish-aot.cmd to $stage (log: $root/dist.staging.log)"
MSYS2_ARG_CONV_EXCL='*' cmd.exe /c "$root/tools/publish-aot.cmd" "$stage" > "$root/dist.staging.log" 2>&1 \
  || { tail -5 "$root/dist.staging.log" >&2; die "the publish FAILED"; }
new="$stage/Bash.exe"
new_stamp=$("$new" --version 2>/dev/null | tail -1)
[[ "$new_stamp" == *"hg.$node $rev" ]] || die "the published build's stamp is '$new_stamp', not rev $rev ($node)"

# 3. the suite on that exact binary, detached (the console run is publish's, 2026-11-01)
# the launcher the detached suite starts each case with (tools/launch; built when missing, as benchtimer is)
[ -f "$root/tools/launch/bin/launch.exe" ] || dotnet build "$root/tools/launch" -c Release -o "$root/tools/launch/bin" -nologo < /dev/null > /dev/null || die "cannot build tools/launch"
suite=$(bash "$root/tests/run-tests.sh" --interpreter "$new" --detached < /dev/null 2>&1)   # run-tests.ps1 until 2026-11-14 (PowerShell removed)
res=$(grep -E 'passed, [0-9]+ failed' <<< "$suite" | tail -1)
echo "deploy: suite (--detached): $res"
# an optional program missing (MSYS: tests/cases/msys_pin.inc) is a loud complaint, never a stop (2026-11-09)
grep -E '^!!! [0-9]+ BYPASSED' <<< "$suite" | sed 's/^/deploy: /' >&2
[[ "$res" == *" 0 failed" ]] || die "the suite failed on $new (--detached); not deployed"

# 4. the battery, against WSL's GNU bash (its reference since 2026-10-19; it runs no Git Bash)
bat=$(bash "$root/tests/compat/run-compat.sh" < /dev/null 2>&1)   # run-compat.ps1 until 2026-11-14
bat_rc=$?
echo "deploy: battery: $(grep -E 'pass, [0-9]+ diff' <<< "$bat" | tail -1)"
[ $bat_rc -eq 0 ] || { grep -E 'REGRESS|FAIL' <<< "$bat" >&2; die "the battery regressed; not deployed"; }

# 5. swap, as InstallGen replaces a file in use (installer-a4, 2026-11-01; the architect: "Why aren't you using the swap
#    trick to deploy?"): the new build is copied IN FULL beside the old under a temporary .exe name (and checked), and
#    sh.exe's new hard link made to it, while the old names still work; then each name takes two renames: the running
#    image aside (it keeps running; the aside copy is the rollback), the new one in. A name is missing only between those
#    two renames, no longer for a 7.8 MB copy. A refused rename (a process STARTING from the file, measured 2026-11-01)
#    puts back what changed and stops: run it again. No retry, as the installer.
# swap <folder> <its rev>: the new build in as <folder>/Bash.exe and sh.exe, the running ones aside as <name>.rev<rev>
swap() {
  local d=$1 o=$2 nb="$1/Bash.deploy-new.exe" ns="$1/sh.deploy-new.exe" got got_sh sh_how sh_back=
  rm -f "$nb" "$ns"
  cp "$new" "$nb" || { rm -f "$nb"; die "could not copy the new build in beside $d/Bash.exe; nothing changed there"; }
  got=$("$nb" --version 2>/dev/null | tail -1)
  [ "$got" = "$new_stamp" ] || { rm -f "$nb"; die "the copy in $d reads '$got', not '$new_stamp'; nothing changed there"; }
  # sh.exe is a HARD LINK to the new Bash.exe, made anew every deploy so it never keeps an old build (the architect,
  # 2026-11-01: "deploy should make sh.exe a hard link also", as the installer makes it); confirmed, else a copy, said so
  if ln "$nb" "$ns" 2>/dev/null && [ "$nb" -ef "$ns" ]; then
    sh_how="a hard link to Bash.exe (confirmed)"
  else
    rm -f "$ns"
    cp "$new" "$ns" || { rm -f "$nb" "$ns"; die "could not link or copy sh.exe's new file in $d; nothing changed there"; }
    sh_how="a COPY of Bash.exe (the hard link could not be made or confirmed)"
  fi
  undo_bash() { mv "$d/Bash.exe" "$nb" && mv "$d/Bash.exe.rev$o" "$d/Bash.exe"; }
  mv "$d/Bash.exe" "$d/Bash.exe.rev$o" ||
    { rm -f "$nb" "$ns"; die "could not set the running $d/Bash.exe aside (a process starting from it?); nothing changed there: run again"; }
  mv "$nb" "$d/Bash.exe" ||
    { mv "$d/Bash.exe.rev$o" "$d/Bash.exe"; rm -f "$nb" "$ns"; die "could not move the new Bash.exe into $d; the old one is back: run again"; }
  if [ -e "$d/sh.exe" ]; then
    mv "$d/sh.exe" "$d/sh.exe.rev$o" ||
      { undo_bash; rm -f "$nb" "$ns"; die "could not set the running $d/sh.exe aside; Bash.exe put back, nothing changed there: run again"; }
    sh_back=" and sh.exe.rev$o"
  fi
  mv "$ns" "$d/sh.exe" ||
    { [ -n "$sh_back" ] && mv "$d/sh.exe.rev$o" "$d/sh.exe"; undo_bash; rm -f "$nb" "$ns"; die "could not move the new sh.exe into $d; both put back, nothing changed there: run again"; }
  [[ $sh_how == *COPY* ]] || [ "$d/sh.exe" -ef "$d/Bash.exe" ] || die "$d/sh.exe is not the same file as $d/Bash.exe after the swap"
  got_sh=$("$d/sh.exe" --version 2>/dev/null | tail -1)
  [ "$got_sh" = "$new_stamp" ] || die "$d/sh.exe reads '$got_sh', not '$new_stamp'"
  echo "deploy: DEPLOYED $got to $d (Bash.exe; sh.exe is $sh_how); rollback: Bash.exe.rev$o$sh_back"
}
[ -z "$to_dist" ] || swap "$dist" "$old_rev"
if [ -n "$to_home" ]; then
  swap "$home_bin" "$home_old"
  # the two files the installer puts beside bin/, from the same tree (their "Fixed after this release" is this build's)
  # their "This copy:" line names the build, as the installer writes it (a session checks it against bash --version)
  for f in KNOWN_ISSUES.md KNOWN_LIMITATIONS.md; do
    sed "s|^\\*\\*This copy:\\*\\* .*|**This copy:** $new_stamp, deployed.|" "$root/$f" > "$home_bin/../$f" || die "could not copy $f to ~/.bash"
  done
  # only the newest rollback copy stays in the folder every session starts from; one in use stays until a later deploy
  for f in "$home_bin"/Bash.exe.rev* "$home_bin"/sh.exe.rev*; do
    [ -e "$f" ] || continue
    case $f in *".rev$home_old") continue ;; esac
    rm -f "$f" 2>/dev/null || true
  done
fi

# 6. notice material
echo
base=$old_rev; [ -n "$to_dist" ] || base=$home_old   # dist already held it: since what the sessions ran
echo "revisions now deployed (rev $base -> $rev):"
hg log -r "$((base + 1)):$rev" --template '  {rev}: {desc|firstline}\n'
