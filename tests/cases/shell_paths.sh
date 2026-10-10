# 2026-10-08: the paths the shell itself produces are in its forward-slash form (DECISIONS
# 2026-09-11/12): $HOME (inherited as C:\Users\x), ~ and ~/x, x=~/y, $BASH, and what type / command
# -v / command -V / which / hash print for a program found on PATH. Windows-specific values, so the
# expectations are authored: no backslash, and the path names a real file or directory.
bs() { case $1 in *"\\"*) echo "$2: backslash [$1]" ;; *) echo "$2: forward" ;; esac; }
bs "$HOME" 'HOME'
bs ~ 'tilde'
bs ~/x 'tilde/x'
x=~/y; bs "$x" 'x=~/y'
bs "$BASH" 'BASH'
"$BASH" -c 'echo "nested shell runs"'
p=$(command -v cmd); bs "$p" 'command -v'; [ -f "$p" ] && echo "command -v: a file"
bs "$(command -V cmd)" 'command -V'
bs "$(type -p cmd)" 'type -p'
bs "$(type cmd)" 'type'
bs "$(type -P cmd 2>/dev/null || type -p cmd)" 'type -P'
bs "$(which cmd)" 'which'
hash cmd; bs "$(hash)" 'hash'
cd && [ "$(pwd)" = "$HOME" ] && echo "cd goes HOME"
HOME='C:\Users\Someone Else'; bs ~ 'tilde, HOME set with backslashes'; echo "[$HOME] kept as set"
