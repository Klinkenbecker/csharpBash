# 2026-10-12 (msp430-a2): arguments reach a PATH program intact. An MSYS program re-parses an unquoted word like a
# shell: `'{print}'` arrived as `print`, `'*.txt'` as the matching files, `"it's"` swallowed the arguments after it,
# `\` was taken as an escape. C#Bash quoted only on white space or `"`. The programs are the PATH ones (`type -P`):
# MSYS's under C#Bash (cases/msys_pin.inc puts its folder first), the native ones under GNU bash. Expected output:
# GNU bash via WSL.
. cases/msys_pin.inc
P=$(type -P printf)
d=$(mktemp -d) && cd "$d" || exit 1
touch a.txt b.txt abc
"$P" '[%s]\n' '{print}' '*.txt' "it's" '[ab]c' 'a?c' '{a,b}' 'x{}y' '~' 'plain' 'a\b' 'q"q' 'x\"y' 'end\' '\\srv\share' 'C:\Program Files\x' 'tab	t' '' '$HOME' 'é{x}' 'a\\b'
echo "gawk: $(printf 'l1\n' | "$(type -P gawk)" '{print}')"
echo "sed: $(echo "it's {x}" | "$(type -P sed)" "s/it's {x}/X/")"
echo "find: $("$(type -P find)" . -maxdepth 1 -name '*.txt' | sort | tr '\n' ' ')"
cd / && rm -rf "$d"
