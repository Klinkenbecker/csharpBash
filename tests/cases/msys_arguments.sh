# 2026-10-12 (msp430-a2): arguments reach a PATH program intact. An MSYS program (Git's usr/bin tools, which
# C#Bash puts on PATH as Git Bash does) re-parses an unquoted word like a shell: `'{print}'` arrived as `print`,
# `'*.txt'` as the matching files, `"it's"` swallowed the arguments after it, `\` was taken as an escape. C#Bash
# quoted only on white space or `"`. The programs are the PATH ones (`type -P`): Git's MSYS tools under C#Bash,
# the native ones under GNU bash. Expected output: GNU bash via WSL. Off Linux the PATH printf must be an MSYS
# program, else SKIP (2026-10-20: with Git off PATH it found native ports and System32's find.exe, testing nothing).
P=$(type -P printf)
case $OSTYPE in linux*) ;; *) [ -f "$(dirname "$P")/msys-2.0.dll" ] || { echo "SKIP: the PATH printf is not an MSYS program"; exit 0; } ;; esac
d=$(mktemp -d) && cd "$d" || exit 1
touch a.txt b.txt abc
"$P" '[%s]\n' '{print}' '*.txt' "it's" '[ab]c' 'a?c' '{a,b}' 'x{}y' '~' 'plain' 'a\b' 'q"q' 'x\"y' 'end\' '\\srv\share' 'C:\Program Files\x' 'tab	t' '' '$HOME' 'é{x}' 'a\\b'
echo "gawk: $(printf 'l1\n' | "$(type -P gawk)" '{print}')"
echo "sed: $(echo "it's {x}" | "$(type -P sed)" "s/it's {x}/X/")"
echo "find: $("$(type -P find)" . -maxdepth 1 -name '*.txt' | sort | tr '\n' ' ')"
cd / && rm -rf "$d"
