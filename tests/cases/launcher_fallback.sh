# 2026-10-17: a bare name C#Bash does not find on PATH is looked for only in C#Bash's own folder, System32 and the
# Windows folder (so `cmd` still runs on an empty PATH), else "command not found" (127). Windows' launcher searched for
# it instead: with NoDefaultCurrentDirectoryInExePath unset (an ordinary terminal) a `.exe` planted in the current
# directory RAN, and a cwd script not on PATH was "Permission denied" (126). Authored (Windows-specific).
d=$(mktemp -d) && cd "$d" || exit 1
cp C:/Windows/System32/whoami.exe zzplant.exe
printf '#!/bin/bash\necho ran\n' > zzcwd.sh
msg() { sed 's/^.*line [0-9]*: //'; }
echo "== an ordinary terminal (NoDefaultCurrentDirectoryInExePath unset): nothing runs from the current directory"
env -u NoDefaultCurrentDirectoryInExePath "$BASH" -c 'zzplant > /dev/null; echo "planted .exe: rc=$?"; zzcwd.sh; echo "cwd script: rc=$?"' 2>&1 | msg
echo "== an empty PATH still runs Windows' own programs; command -v still reports nothing"
env -u NoDefaultCurrentDirectoryInExePath "$BASH" -c 'PATH=; cmd //d //c echo cmd-ran | tr -d "\r"; command -v cmd || echo "command -v cmd: none"' 2>&1 | msg
echo "== a redirect on a command not found is still made, as bash"
zznothere > out.txt 2>/dev/null; echo "rc=$? out.txt: $([ -e out.txt ] && echo made || echo missing)"
cd / && rm -rf "$d"
