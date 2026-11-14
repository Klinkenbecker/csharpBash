# 2026-11-12 (daq-a1's report): an associative array's subscript took `cmd`, $( ) and $(( )) literally, so
# ${M[$(lower "$k")]+x} read a set element as unset (and a read, ${#…}, an assignment went to the wrong key);
# `unset 'M[$k]'` did not expand its subscript either. Indexed arrays were right. Expected = GNU bash via WSL.
lower() { printf '%s' "$1" | tr '[:upper:]' '[:lower:]'; }
declare -A M=(); M[abc/1.0-dev]=x
key='ABC/1.0-DEV'
echo "1: [${M[$(lower "$key")]+x}]"
lk="$(lower "$key")"; echo "2: [${M[$lk]+x}]"
echo "3: [${M[$(lower "$key")]}]"
echo "4: [${M[$(echo abc/1.0-dev)]+x}]"
echo "5: [${M[$(echo abc)]-unset}]"
declare -a I=(a b c); echo "6: [${I[$(echo 1)]+x}] [${I[$(echo 1)]}]"
echo "7: [${#M[$(echo abc/1.0-dev)]}]"
echo "8: [${M[`echo abc/1.0-dev`]+x}]"
echo "9: [${M["$(echo abc/1.0-dev)"]+x}]"
M[$(echo new)]=y; echo "10: [${M[new]-unset}]"
M[$((1+1))]=two; echo "11: [${M[2]-unset}] [${M[$((3-1))]}]"
set -- p q; M[$#]=hash; echo "12: [${M[2]}]"
M[k$(echo 1)]=v; echo "13: $(printf "%s\n" "${!M[@]}" | sort | tr "\n" " ")"
unset 'M[$(echo new)]' 2>/dev/null; echo "14: [${M[new]-unset}]"
