# 2026-10-19 (red team F5): a quoted or data `~` is a name, as in bash. It was the Windows profile whatever $HOME said:
# `mkdir "$d"` (d='~') said "File exists", a write under "$d/" landed in the profile, and `rm -r "$d"` would have aimed
# at it. An unquoted ~ is still $HOME. Expected: GNU bash 5.1 (WSL), byte-identical.
d=$(mktemp -d) && cd "$d" || exit 1
export HOME="$d/home"; mkdir home
t='~'
# the guard: on a build where a quoted ~ is the profile, stop before anything below can write or remove there
if [ -e "$t" ]; then echo "a quoted ~ names something before mkdir: stopped"; cd / && rm -rf "$d"; exit 1; fi
echo "no ~ yet"
cd "$t" 2>/dev/null && echo "cd into ~ worked" || echo "cd ~ (quoted): no such directory"
mkdir "$t" && echo x > "$t/f" && cat "./~/f"
ls "$t"
cd "$t" && pwd | sed "s#^$d#D#" && cd ..
echo y > '~/g'; ls '~' | tr '\n' ' '; echo
cd ~ && pwd | sed "s#^$d#D#" && cd "$d"
echo ~ ~/x | sed "s#$d#D#g"
x=~/y; echo "$x" | sed "s#$d#D#"
rm -r "$t"; [ -e "$t" ] && echo "still there" || echo "removed ./~"
ls -d home
cd / && rm -rf "$d"
