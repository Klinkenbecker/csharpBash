# 2026-11-11 (git-b3's report): env -u NAME NAME=value applies the removal first, then the assignment, so NAME
# arrives set (GNU). A program C#Bash starts got the removal last: NAME arrived unset. The builtins were right.
# Expected output: GNU env via WSL.
export X=old
echo "1 [$(env -u X X=set "$BASH" -c 'echo "${X-unset}"')]"
echo "2 [$(env -u X X=set printenv X)]"
echo "3 [$(env -u X "$BASH" -c 'echo "${X-unset}"')]"
echo "4 [$(env -i X=set "$BASH" -c 'echo "${X-unset}"')]"
echo "5 [$(env -u X -u Y Y=y X=set "$BASH" -c 'echo "${X-unset} ${Y-unset}"')]"
echo "6 [$(X=pre env -u X X=set "$BASH" -c 'echo "${X-unset}"')]"
echo "7 [$(X=pre env -u X "$BASH" -c 'echo "${X-unset}"')]"
