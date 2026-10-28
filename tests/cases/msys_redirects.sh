# 2026-10-08: an MSYS program (Git's usr/bin tools) under `>>` or under the shell's own redirect
# (`{ ...; } > f`, a coreutil falling back to the PATH program) wrote nothing and failed with "Bad
# file descriptor": it was handed an append-only handle (FILE_APPEND_DATA, kept for concurrent
# appenders), which MSYS takes as read-only. It now gets a write handle at the end. Needs Git for
# Windows (its usr/bin beside mingw64/bin). Authored expectations.
g=$(command -v git) || { echo "SKIP: needs Git for Windows (its usr/bin tools)"; exit 0; }
E=$(dirname "$g")/../../usr/bin/echo.exe
[ -f "$E" ] || { echo "SKIP: needs Git for Windows' usr/bin/echo.exe"; exit 0; }
d=$(mktemp -d) && cd "$d" || exit 1
{ "$E" msys; } > o1; echo "1 rc=$? [$(cat o1)]"
echo first > o2; "$E" appended >> o2; echo "2 rc=$? [$(tr '\n' '|' < o2)]"
echo a > o3; { "$E" b; } >> o3; echo c >> o3; echo "3 [$(tr '\n' '|' < o3)]"
{ echo 1; "$E" 2; echo 3; } > o4; echo "4 [$(tr '\n' '|' < o4)]"
{ "$E" to-stderr >&2; } 2>> o5; echo "5 rc=$? [$(cat o5)]"
echo first > o6; cmd.exe /c "echo native" >> o6; echo "6 [$(tr -d '\r' < o6 | tr '\n' '|')]"
printf 'a\n' > in.txt; awk '{ getline y < "in.txt"; print "got " y }' in.txt > o7; echo "7 rc=$? [$(cat o7)]"
cd / && rm -rf "$d"
