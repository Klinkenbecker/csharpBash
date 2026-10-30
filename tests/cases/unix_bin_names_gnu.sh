# 2026-10-30 (esdk-a2's m2): a command named /bin/NAME or /usr/bin/NAME that does not exist on the drive runs NAME (a
# builtin first, then PATH), as a #! line already did; msys build scripts call `/bin/sed`. It was "No such file or
# directory", 127. A path elsewhere is still that path. Expected: GNU bash 5.1 via WSL (where the files exist).
echo E:x | /bin/sed 's/E:/e/'
/usr/bin/env printf '%s\n' "env ok"
/bin/echo "echo ok"
/usr/bin/printf '%s\n' "printf ok"
/nonexistent/sed 2>&1 | sed 's/^.*: line [0-9]*: //'; echo "rc=${PIPESTATUS[0]}"
