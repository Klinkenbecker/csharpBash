# 2026-10-20: moved here from dev_tty_windowless (2026-10-15). git's credential prompt, run in a console with no
# window (Claude Code's), fails at once ("could not read Username") instead of waiting on /dev/tty for typing that
# cannot come. Needs Git for Windows; SKIP without it (dev_tty_windowless printed the expected line then, a pass that could
# not fail). The console comes from tests/fixtures/console-kind.ps1. Expected output: authored.
# Git for Windows' prompt is the subject: since 2026-11-15 a bare `git` may be Wombat (~/.bash/bin), which refuses
# `credential fill`; then the pinned PortableGit (the MSYS cases' copy) is used
GFW=$(command -v git)
if [ -z "$GFW" ] || "$GFW" --version 2>/dev/null | grep -qi wombat; then GFW=F:/Tools/PortableGit-2.28.0/cmd/git.exe; fi
[ -x "$GFW" ] || { echo "SKIP: needs Git for Windows (git on PATH, or the pinned PortableGit 2.28.0)"; exit 0; }
export GFW
fx="$PWD/../tools/launch/bin/launch.exe"   # tools/launch: a console of a chosen kind (console-kind.ps1 until 2026-11-14)
d=$(mktemp -d) && cd "$d" || exit 1
cat > probe.sh <<'EOF'
out=$(printf 'protocol=https\nhost=example.invalid\n\n' | PATH="$(dirname "$BASH");$PATH" GIT_ASKPASS= SSH_ASKPASS= "$GFW" -c credential.helper= -c core.askPass= credential fill 2>&1)
echo "git rc=$? prompt failed: $(printf '%s\n' "$out" | grep -c 'could not read Username')"
EOF
run() { "$fx" --"$1" --timeout 15 --stderr out --print --cwd "$PWD" --cmdline "\"$BASH\" $2"; }
echo "== a console with no window"; run windowless probe.sh
cd / && rm -rf "$d"
