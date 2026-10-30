# 2026-10-30 (esdk-a2's m1): a text file with no #! line, run BY ITS PATH, runs as a shell script, as bash does with an
# executable one (its ENOEXEC fallback) and as msys does: $0 is the path, its variables stay its own, its status is
# the command's. Binary content is refused: "cannot execute binary file: Exec format error", 126. It was "Permission
# denied", 126 (no #!, no .sh: not executable to C#Bash). A bare name on PATH is NOT run this way (Claude's position,
# AWAITING the architect; msys does). chmod +x for GNU; C#Bash has no execute bit. Expected: GNU bash 5.1 via WSL.
cd "${TMPDIR:-${TEMP:-/tmp}}" || exit 1
d="noshebang-$$"; mkdir -p "$d" && cd "$d" || exit 1
msg() { sed 's/^.*: line [0-9]*: //'; }
printf 'echo "ran $0 $1"\nx=inside\nexit 3\n' > noshe; chmod +x noshe
printf 'echo\0bin\n' > binfile; chmod +x binfile
echo "--- a text file by path"; ./noshe a; echo "rc=$? x=${x-unset}"
echo "--- by a longer path";    "$PWD/noshe" b > out; echo "rc=$?"; sed "s|$PWD|PWD|" out
echo "--- binary content";      ./binfile 2>&1 | msg; echo "rc=${PIPESTATUS[0]}"
cd .. && rm -rf "$d"
