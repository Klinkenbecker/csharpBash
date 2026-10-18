# 2026-10-17: paths over 260 characters. The shell's own `cd` enters such a directory (C#Bash's manifest now declares
# longPathAware; SetCurrentDirectory refused it: "path is too long") and its file operations work there. Windows starts
# no program from such a directory, nor one whose own path is that long (cmd cannot either): both now say so, 126
# (they were 127, "The directory name is invalid" / "The filename or extension is too long"). Authored (Windows-specific;
# needs LongPathsEnabled=1).
d=$(mktemp -d) && cd "$d" || exit 1
msg() { sed 's|^.*line [0-9]*: ||; s|^.*/w\.exe: |w.exe: |'; }
seg=aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa
deep="$d/$seg/$seg/$seg/$seg/$seg"
mkdir -p "$deep" && cp C:/Windows/System32/whoami.exe "$deep/w.exe" && echo hi > "$deep/f.txt"
[ "${#deep}" -gt 260 ] && echo "a directory over 260 characters: yes"
( cd "$deep" && echo "cd: ok"; cat f.txt; whoami.exe 2>&1 | msg; echo "rc=${PIPESTATUS[0]}" )
"$deep/w.exe" 2>&1 | msg; echo "rc=${PIPESTATUS[0]}"
cd / && rm -rf "$d"
