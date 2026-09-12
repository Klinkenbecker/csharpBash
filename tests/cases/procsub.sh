# P4: process substitution <( ) by temp-file emulation (DECISIONS 2026-09-04 #6),
# |& stderr pipes, trap ERR (with and without errtrace), timeout.
cd "$(dirname "$0")/../fixtures/awk" || exit 1

echo "--- <( )"
diff <(echo a) <(echo b); echo "rc=$?"
cat <(echo ps)
while read -r l; do echo "got $l"; done < <(printf '1\n2\n')
comm -12 <(printf 'a\nb\n') <(printf 'b\nc\n')
paste <(echo x) <(echo y)
echo "$(cat <(echo inner))"
n=$(ls "${TEMP:-/tmp}" 2>/dev/null | grep -c bash-procsub); echo "leftover=$n"
echo 'echo hi > >(cat)' > "${TEMP:-/tmp}/procsub-out.sh"
"$BASH" "${TEMP:-/tmp}/procsub-out.sh" 2>&1 | grep -c 'output process substitution >( ) is not supported'; echo "rc=${PIPESTATUS[0]}"
rm -f "${TEMP:-/tmp}/procsub-out.sh"

echo "--- |&"
cat nonexist |& head -c 4; echo
cat nonexist |& wc -l

echo "--- trap ERR"
trap 'echo "err:$?"' ERR
echo 1; false
echo 2; false || true
echo 3; if false; then :; fi
echo 4; ! true
echo 5; f(){ false; echo in; }; f
echo 6; g(){ false; }; g
echo 7; true | false
echo 8; [[ 1 == 2 ]]
echo 9; false && true
echo 10; x=$(false)
echo 11; (false)
echo 12; { false; }
echo 13; [ 1 = 2 ]
echo 14; (( 0 ))
echo 15; set -o errtrace; h(){ false; echo inh; }; h
echo 16; set +o errtrace; h
trap - ERR
false; echo "no trap"

echo "--- timeout"
timeout 1 sleep 3; echo "rc=$?"
timeout 5 echo fast; echo "rc=$?"
timeout 0.5s true; echo "rc=$?"
timeout xx true 2>/dev/null; echo "rc=$?"
timeout --preserve-status 1 sleep 3; echo "rc=$?"
