# 2026-11-11 (found building the jq batch): contains and inside refused two values of different types at every level;
# jq 1.6 refuses them only at the top, and inside an array or object they simply do not match. Expected = jq 1.6 via WSL.
j() { printf "%s\n" "$1" | jq -c "$2" 2>&1; echo "  rc $?"; }
j '[1,"a"]' 'contains(["a"])'
j '[1,2]' 'contains(["a"])'
j '[[1],{"a":1}]' 'contains([{"a":1}])'
j '{"a":1}' 'contains({"a":"x"})'
j '{"a":[1]}' 'contains({"a":1})'
j '{"a":{"b":[1,"x"]}}' 'contains({"a":{"b":["x"]}})'
j '[1]' 'contains(1)'
j '"a"' 'contains(["a"])'
j '{"a":1}' 'contains("a")'
j '[1,2,{"barp":12,"blip":13}]' 'contains([{"barp":12}])'
j '"bar"' 'inside(["bar"])'
