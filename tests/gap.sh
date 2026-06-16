declare -a empty
echo "G1 empty array for-loop (expect zero iterations):"
for x in "${empty[@]}"; do echo "  iter [$x]"; done
echo "G1 done"

echo "G2 empty array as args (expect 'a|b'):"
echo "a" "${empty[@]}" "b" | tr ' ' '|'

echo "G3 quoted empty string (expect one iteration []):"
for x in ""; do echo "  iter [$x]"; done

echo "G4 unset unquoted (expect zero iterations):"
for x in $nodef; do echo "  iter [$x]"; done

echo "G5 single-quoted with spaces (expect one iter [p q]):"
for x in 'p q'; do echo "  iter [$x]"; done

echo "G6 undeclared array (expect zero iterations):"
for x in "${nodef2[@]}"; do echo "  iter [$x]"; done

arr=(one two three)
echo "G7 mid-word join (expect pre-one / two / three-post):"
for x in "pre-${arr[@]}-post"; do echo "  iter [$x]"; done
