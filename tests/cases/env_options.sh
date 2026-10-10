# 2026-10-08 (koliada-net-f7's report, 2026-10-10): env's options reach the command, as in GNU env.
# A NAME=VALUE whose name is not a shell identifier (STDOUT.OutputLevel=trace, X-Y=2) was run as the
# command; -u was applied only when env printed; -i did not reach a builtin; -uNAME, --unset=NAME,
# - and -0 were refused or ignored. Never prints a whole environment (counts, or named variables).
# Expected output: GNU env via WSL.
export FOO=1
echo "1  $(env "A.B=1" env | grep '^A\.B=')"
echo "2  $(env "X-Y=2" "$BASH" -c 'printenv X-Y')"
echo "3  $(env "STDOUT.OutputLevel=trace" printenv STDOUT.OutputLevel)"
env -u FOO printenv FOO; echo "4  rc=$?"
echo "5  $(env -u FOO "$BASH" -c 'echo "[${FOO-unset}]"')"
env -uFOO printenv FOO; echo "6  rc=$?"
env --unset=FOO printenv FOO; echo "7  rc=$?"
echo "8  $(env -u FOO env | grep -c '^FOO=')"
echo "9  [$(env -i A=1 printenv | grep -c .)] [$(env -i A=1 printenv A)] [$(env -i A=1 printenv FOO)]"
echo "11 [$(env - A=1 printenv | grep -c .)] [$(env - A=1 printenv A)]"
echo "13 $(env A=1 B.C=2 env | grep -E '^(A|B\.C)=' | sort | tr '\n' ' ')"
echo "14 $(env -- A.B=1 printenv A.B)"
echo "15 $(env -u FOO printenv | grep -c '^FOO=')"
echo "16 $(env "A B=1" printenv "A B")"
echo "17 $(FOO=2 env | grep -c '^FOO=2$')"
echo "18 $(env -i printenv | grep -c .)"
env -u FOO true; echo "19 [$(printenv FOO)] after"
env -i true; printenv FOO > /dev/null && echo "20 FOO still exported after env -i"
echo "21 $(env "A.B=1" "$BASH" -c 'env | grep "^A\.B="')"
echo "22 [$(env -i "$BASH" -c 'printenv FOO')]"
echo "23 $(env -i A=1 B=2 env -0 | tr "\0" "\n" | sort | tr "\n" "|")"
