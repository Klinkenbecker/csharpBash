# 2026-11-11 (bugs list 2): jq exits 4 on invalid input, as jq 1.6 (it exited 2): truncated, slurped, after a valid
# value, a bad file before a good one. A runtime error stays 5, -e with null 1. Expected: GNU jq 1.6 via WSL.
d=$(mktemp -d) && cd "$d" || exit 1
t() { printf '%-26s' "$1"; out=$(eval "$2" 2>/dev/null); echo "exit $?  [$(printf '%s' "$out" | tr '\n' ' ')]"; }
printf '{x' > bad.json; printf '1' > good.json
t 'truncated'          'printf "[" | jq -c .'
t 'slurp truncated'    'printf "[" | jq -c -s .'
t 'valid then bad'     'printf "1 [" | jq -c .'
t 'bad file'           'jq -c . bad.json'
t 'bad then good file' 'jq -c . bad.json good.json'
t 'runtime error'      'printf "1" | jq -c ".a"'
t '-e with null'       'printf "null" | jq -e .'
cd / && rm -rf "$d"
