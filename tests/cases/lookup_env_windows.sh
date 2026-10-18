# 2026-10-17 (red team), the Windows-specific halves: a PATH entry in MSYS form (`/f/x`) is that drive's (it was looked
# up as F:\f\x); in a pipeline stage that changed directory, `\x` is on the stage's drive and `C:x` in the stage's
# directory when it is on C: (both took the process cwd's); `unset PATH` gives a child no PATH (it got C#Bash's process
# PATH) and an exported `path` does not replace PATH; cmd.exe by a full `/` path reads its command line (it took its
# own path's `/`s as switches). Authored (Windows-specific).
d=$(mktemp -d) && cd "$d" || exit 1
drv=${d%%:*}; low=$(echo "$drv" | tr A-Z a-z); rest=${d#*:}
mkdir bin q; printf '@echo zzmsys-ran\r\n' > bin/zzmsys.cmd; cp C:/Windows/System32/whoami.exe q/w.exe
echo "== an MSYS-form PATH entry"
( PATH="/$low$rest/bin;$PATH"; zzmsys | tr -d '\r' )
echo "== \\x and C:x in a pipeline stage that changed directory"
rootrel=$(printf '%s' "$rest/q/w.exe" | tr '/' '\\')
if [ "$drv" != C ]; then { cd C:/Windows && "$rootrel" > /dev/null 2>&1; echo "\\x on the stage's drive (C:): rc=$?"; } | cat
else echo "\\x on the stage's drive (C:): rc=127"; fi
{ cd C:/Windows && C:System32/whoami.exe > /dev/null 2>&1; echo "C:x in the stage's directory: rc=$?"; } | cat
echo "== a child's PATH: unset, and an exported lowercase path"
( unset PATH; C:/Windows/System32/cmd.exe //d //c "if defined PATH (echo the child has a PATH) else (echo the child has no PATH)" | tr -d "\r" )
( PATH='C:/Windows/System32;C:/Windows'; export path='C:/nowhere'; cmd //d //c "echo %PATH%" | tr -d '\r' )
echo "== cmd.exe by a full forward-slash path"
C:/Windows/System32/cmd.exe //d //c echo cmd-by-full-path | tr -d '\r'
cd / && rm -rf "$d"
