# 2026-10-17: a command name with a slash is a path from the shell's own directory, never searched for, as in bash.
# Windows' launcher searched for it: under NoDefaultCurrentDirectoryInExePath=1 (Claude Code sets it) `sub/w.exe`
# was "command not found" whatever the PATH, and a `sub/v.exe` that existed only under a PATH directory ran from
# there. A missing path is "No such file or directory", as bash. Authored (it needs a Windows program).
d=$(mktemp -d) && cd "$d" || exit 1
mkdir -p sub pdir/sub
cp "${SYSTEMROOT:-C:/Windows}/System32/whoami.exe" sub/w.exe
cp sub/w.exe pdir/sub/v.exe
msg() { sed "s|$d|D|g; s|^.*line [0-9]*: ||"; }
echo "== sub/w.exe with NoDefaultCurrentDirectoryInExePath=1"
NoDefaultCurrentDirectoryInExePath=1 "$BASH" -c 'sub/w.exe > /dev/null; echo rc=$?' 2>&1 | msg
echo "== and without it"
env -u NoDefaultCurrentDirectoryInExePath "$BASH" -c 'sub/w.exe > /dev/null; echo rc=$?' 2>&1 | msg
echo "== sub/v.exe exists only under a PATH directory"
NoDefaultCurrentDirectoryInExePath=1 "$BASH" -c "PATH=\"$d/pdir;\$PATH\"; sub/v.exe > /dev/null; echo rc=\$?" 2>&1 | msg
echo "== a missing path"
./nope 2>&1 | msg; echo "rc=${PIPESTATUS[0]}"
sub/nope 2>&1 | msg; echo "rc=${PIPESTATUS[0]}"
echo "== a name with a colon but no drive is an NTFS stream, never a program (it ran from the cwd)"
cat sub/w.exe > 'zads:bar.exe'
zads:bar.exe 2>&1 | msg; echo "rc=${PIPESTATUS[0]}"
env -u NoDefaultCurrentDirectoryInExePath "$BASH" -c 'zads:bar > /dev/null 2>&1; echo rc=$?'
type zads:bar.exe 2>&1 | msg
echo "== ../sub/w.exe from sub, and in a pipeline stage that changed directory"
( cd sub && ../sub/w.exe > /dev/null; echo "rc=$?" )
true | ( cd sub && ../sub/w.exe > /dev/null; echo "stage rc=$?" )
cd / && rm -rf "$d"
