# 2026-10-20: moved here from dev_tty_windowless (2026-10-15). git's credential prompt, run in a console with no
# window (Claude Code's), fails at once ("could not read Username") instead of waiting on /dev/tty for typing that
# cannot come. Needs git on PATH; SKIP without it (dev_tty_windowless printed the expected line then, a pass that could
# not fail). The console comes from tests/fixtures/console-kind.ps1. Expected output: authored.
command -v git > /dev/null || { echo "SKIP: needs git on PATH"; exit 0; }
fx="$PWD/fixtures/console-kind.ps1"
d=$(mktemp -d) && cd "$d" || exit 1
cat > probe.sh <<'EOF'
out=$(printf 'protocol=https\nhost=example.invalid\n\n' | PATH="$(dirname "$BASH");$PATH" GIT_ASKPASS= SSH_ASKPASS= git -c credential.helper= -c core.askPass= credential fill 2>&1)
echo "git rc=$? prompt failed: $(printf '%s\n' "$out" | grep -c 'could not read Username')"
EOF
run() { powershell.exe -NoProfile -ExecutionPolicy Bypass -File "$fx" -Kind "$1" -CommandLine "\"$BASH\" $2" -Cwd "$PWD"; }
echo "== a console with no window"; run windowless probe.sh
cd / && rm -rf "$d"
