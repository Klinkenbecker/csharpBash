# 2026-11-11 (bugs list 3, and one found beside it): a negative length in a slice of a LIST (an array, the positional
# parameters) is an error, "-1: substring expression < 0", and a bad substitution (${x!y}) is one too: each abandons the
# top-level command line, status 1, and the script goes on with the next line. C#Bash counted back from the end for an
# array, gave nothing for the positionals, and went on with the line after a bad substitution, status 0. A negative
# length in a STRING slice counts back from the end, in both. Expected: GNU bash via WSL (stdout only).
a=(p q r s); s=abcdef
echo "${a[@]:0:-1}"; echo "array: same line ran"
echo "after array: status $?"
echo "${a[*]:1:-2}"; echo "array star: same line ran"
echo "after array star: status $?"
set -- x y z; echo "${@:1:-1}"; echo "positional: same line ran"
echo "after positional: status $?"
f() { echo "${a[@]:1:-1}"; echo "in function after"; }; f; echo "after the call, same line"
echo "after function line: status $?"
echo "${x!y}"; echo "bad substitution: same line ran"
echo "after bad substitution: status $?"
echo "string: [${s:0:-1}] [${s:1:-2}] [${s: -3:-1}]"
echo "array, no length: [${a[@]:2}] [${a[@]: -2}] length 0: [${a[@]:1:0}]"
