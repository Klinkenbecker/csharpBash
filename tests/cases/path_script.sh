# 2026-10-17: a script found on PATH runs by its #! line, as in bash; C#Bash said "Permission denied" (CreateProcess
# cannot run it). The search takes the first EXECUTABLE match (C#Bash's -x: a PATHEXT extension, .sh, or #!) and
# falls back to the first match it cannot run, reported by its path; a bare name never runs a script from the
# current directory (a planted #! `git` there ran instead of git.exe). Expected: GNU bash 5.1 (WSL), byte-identical.
d=$(mktemp -d) && cd "$d" || exit 1
case "$PATH" in *\;*) sep=';' ;; *) sep=':' ;; esac
export D="$d"
mkdir bin p1 p2 cwd
printf '#!/bin/bash\necho "hello: $# args, first [$1], 0=[${0#"$D"}]"\nexit 3\n' > bin/hello
printf 'echo "plain.sh ran"\n' > bin/plain.sh
printf '#!/bin/bash\necho "zzt from PATH"\n' > bin/zzt
printf 'just data\n' > p1/tool
printf '#!/bin/bash\necho "p2 tool ran"\n' > p2/tool
printf 'just data\n' > p1/data
printf '#!/bin/bash\necho "cwd script ran"\n' > cwd/zzt
cp cwd/zzt cwd/onlyhere
chmod +x bin/hello bin/plain.sh bin/zzt p2/tool cwd/zzt cwd/onlyhere
P0=$PATH
msg() { sed "s|$d|D|g; s|^.*line [0-9]*: ||"; }

export PATH="$d/bin$sep$P0"
echo "== a #! script found on PATH"
hello "a b" c; echo "rc=$?"
command hello z; echo "rc=$?"
type -t hello; command -v hello | msg; hash -t hello | msg
echo "== a .sh file with no #! found on PATH"
plain.sh; echo "rc=$?"

echo "== a match that cannot run is passed over for a later one"
export PATH="$d/p1$sep$d/p2$sep$P0"
tool; echo "rc=$?"
echo "== the only match cannot run"
export PATH="$d/p1$sep$P0"
data 2>&1 | msg; echo "rc=${PIPESTATUS[0]}"

echo "== a bare name never runs a script from the current directory"
export PATH="$d/bin$sep$P0"
cd cwd
zzt; echo "rc=$?"
onlyhere 2>&1 | msg; echo "rc=${PIPESTATUS[0]}"
./onlyhere; echo "rc=$?"
cd / && rm -rf "$d"
