# 2026-10-04: the command word was expanded as ONE string, so "$@" or an unquoted $cmd in command
# position ran "echo a b" as a command name ("command not found"; every build back to rev 80).
# It is expanded like any word, and its first field is the command. Expected output = GNU (WSL).
f() { "$@"; }; f echo a b
g() { "$@" tail; }; g echo x y
c="echo split me"; $c
set -- echo pos args; "$@"
a=(echo from array); "${a[@]}"
run() { echo "+ $*"; "$@"; }; run printf '%s|%s\n' wrapped call
cmd=printf; $cmd '%s\n' "printf via a variable"
q="echo one field"; "$q" 2>/dev/null; echo "a quoted name with spaces is one word: $?"
empty=; $empty echo "an empty name: the next word runs"
set --; "$@"; echo "\"\$@\" with no arguments: $?"
$empty; echo "only an empty expansion: $?"
x=$( $empty ); echo "captured: [$x]"
{echo,brace,expansion}
