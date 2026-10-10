# 2026-10-08: an unquoted ${...} holding another ${...} (${a#${b}}, ${1:-${HOME}}) counted the inner
# `${` twice, so its depth never closed and the token swallowed the REST OF THE FILE without an
# error: nothing after it ran. Git's git-prompt.sh (__git_ps1_show_upstream) failed to parse that
# way. Expected output: GNU bash via WSL.
a=abc b=a c=zz HOME=/h
x=${a#${b}}; echo "1 [$x]"
echo "2 [${a%${b}c}] [${a##${b}}] [${a/${b}/X}] [${a//${b}/}]"
echo "3 [${u:-${b}}] [${u:-${u2:-${c}}}] [${a:+${b}${c}}]"
y=${1:-${HOME}}; echo "4 [$y]"
for ((n=1; n <= 2; n++)); do
	z=${a#${b}}
done
echo "5 [$z] after the loop"
arr=(p q r); echo "6 [${arr[${#arr[@]} - 1]}] [${a#${arr[0]}}]"
echo "7 [${a:${#b}}] [${a:0:${#c}}]"
echo "8 still running"
printf '%s\n' 'echo ${a' > unclosed.sh; "$BASH" unclosed.sh > /dev/null 2>&1; echo "9 unclosed \${: rc=$?"; rm -f unclosed.sh
