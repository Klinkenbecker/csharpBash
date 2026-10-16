# 2026-10-15: /dev/tty in a console with no window. Claude Code gives its shell such a console (CREATE_NO_WINDOW),
# and everything the shell starts inherits it. C#Bash opened it as /dev/tty, and a read waited for typing that
# cannot come: git's credential prompt (`bash -c 'cat >/dev/tty && read -r line </dev/tty ...'`, which finds C#Bash
# as `bash`) hung the call to its timeout. Now, without a window, /dev/tty is "No such device or address" at once,
# as GNU bash gives it with no controlling terminal (setsid) and Git Bash in the same place; git then fails at
# once ("could not read Username"). A console with a window, even hidden (a classic console; Windows Terminal's
# ConPTY has one too, measured), keeps /dev/tty. The consoles come from tests/fixtures/console-kind.ps1, the same
# in both suite modes. Expected output: authored (Windows-specific); the messages are GNU bash's.
fx="$PWD/fixtures/console-kind.ps1"
d=$(mktemp -d) && cd "$d" || exit 1
cat > probe.sh <<'EOF'
{ echo to-tty > /dev/tty; } 2>&1; echo "write rc=$?"
read -r line </dev/tty 2>&1; echo "read rc=$?"
cat /dev/tty 2>&1; echo "cat rc=$?"
if command -v git >/dev/null; then
  out=$(printf 'protocol=https\nhost=example.invalid\n\n' | PATH="$(dirname "$BASH");$PATH" GIT_ASKPASS= SSH_ASKPASS= git -c credential.helper= -c core.askPass= credential fill 2>&1)
  echo "git rc=$? prompt failed: $(printf '%s\n' "$out" | grep -c 'could not read Username')"
else echo "git rc=128 prompt failed: 1"; fi   # no git here: nothing to check
EOF
echo '{ echo to-tty > /dev/tty; } 2>&1; echo "write rc=$?"' > writer.sh
run() { powershell.exe -NoProfile -ExecutionPolicy Bypass -File "$fx" -Kind "$1" -CommandLine "\"$BASH\" $2" -Cwd "$PWD"; }
echo "== a console with no window"; run windowless probe.sh
echo "== a console with a (hidden) window"; run window writer.sh
cd / && rm -rf "$d"
