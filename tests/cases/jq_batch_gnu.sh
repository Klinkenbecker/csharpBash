# 2026-11-11 (features list 2, the architect: "add them"): the jq batch, each as jq 1.6 answers it: paths, leaf_paths,
# path, getpath, delpaths, tostream, walk, recurse/1 and /2, limit, indices, index, rindex, splits, inside, label and
# break, input_filename, -S/--sort-keys. All were refused ("not supported by C#Bash's in-process jq", exit 2).
# jq 1.6's own edges are kept, measured: string indices count UTF-8 BYTES and do not overlap, limit(0; f) passes one
# output, recurse(f) does not stop at null, an object member whose walk is empty empties the object built so far,
# input_filename on stdin is "<stdin>". Left out: jq 1.6 never returns from indices("") or an unbounded recurse.
# Expected = jq 1.6 via WSL.
d=$(mktemp -d) && cd "$d" || exit 1
j() { printf "%s\n" "$1" | jq -c "$2" 2>&1; echo "  rc $?"; }
D='{"a":1,"b":[2,{"c":3}],"d":{},"e":[]}'
echo "== paths, leaf_paths, path"
j "$D" '[paths]'
j "$D" '[paths(type == "number")]'
j "$D" '[leaf_paths]'
j '1' '[paths]'
j "$D" '[path(..)]'
j "$D" 'path(.a, .b[1].c, .b[]?)'
j "$D" '[path(.b | select(length > 1))]'
j "$D" 'path(1)'
j 'null' 'path(.a[0].b)'
echo "== getpath, delpaths"
j "$D" 'getpath(["b",1,"c"])'
j "$D" 'getpath(["x","y"])'
j "$D" '[getpath(["a"],["b",0])]'
j "$D" 'getpath(["a","b"])'
j "$D" 'delpaths([["a"],["b",0]])'
j "$D" 'delpaths([])'
j "$D" 'delpaths([[]])'
j '[1,2,3,4]' 'delpaths([[0],[2]])'
j "$D" 'delpaths(["a"])'
echo "== tostream"
j "$D" '[tostream]'
j '[1,[2]]' '[tostream]'
j '3' '[tostream]'
j '[]' '[tostream]'
j '{"a":[]}' '[tostream]'
echo "== walk"
j '{"b":[1,{"z":2,"a":3}],"a":"x"}' 'walk(if type == "number" then . + 1 else . end)'
j '[3,[2,1]]' 'walk(if type == "array" then sort else . end)'
j '{"b":1,"a":2}' 'walk(.)'
echo "== recurse/1, recurse/2"
j '{"a":{"a":1}}' '[limit(5; recurse(.a?))]'
j '{"a":{"a":null}}' '[limit(4; recurse(.a))]'
j '2' '[recurse(if . < 20 then . * 2 else empty end)]'
j '2' '[recurse(. * 2; . < 20)]'
j '[[1],[2,[3]]]' '[recurse(.[]?) | numbers]'
echo "== limit"
j 'null' '[limit(3; range(10))]'
j 'null' '[limit(0; 1, 2)]'
j 'null' '[limit(-1; 1, 2)]'
j 'null' '[limit(5; 1, 2)]'
echo "== indices, index, rindex"
j '"a,b, cd, efg"' 'indices(", ")'
j '[0,1,2,1,3,1,2]' 'indices(1)'
j '[0,1,2,1,3,1,2]' 'indices([1,2])'
j '"a,b, cd, efg"' 'index(", "), rindex(", ")'
j '"abc"' 'index("z"), rindex("z")'
j '"aéb,c"' 'indices(",")'
j '"x😀,y"' 'index(","), rindex(",")'
j '[1,2,1]' 'index(1), rindex(1)'
j '"aaa"' 'indices("aa")'
j 'null' 'indices(1)'
j '1' 'indices(1)'
echo "== splits"
j '"ab,cd, ef"' '[splits(", *")]'
j '"aXbxc"' '[splits("x"; "i")]'
j '"abc"' '[splits("")]'
j '1' '[splits("a")]'
echo "== inside"
j '"bar"' 'inside("foobar")'
j '["baz", "bar"]' 'inside(["foobar", "foobaz", "blarp"])'
j '{"foo": 12, "bar": [{"barp": 12}]}' 'inside({"foo": 12, "bar":[1,2,{"barp":12, "blip":13}]})'
j '1' 'inside("a")'
echo "== label, break"
j '[1,2,3,4]' '[label $out | .[] | if . > 2 then ., break $out else . end]'
j 'null' '[label $f | range(10) | ., (select(. == 3) | break $f)]'
j 'null' '[label $a | label $b | 1, break $b, 2], 3'
j 'null' '[(label $a | 1, break $a), 2]'
j 'null' 'break $x'
j 'null' '[label $x | 1] | length'
echo "== input_filename"
printf '1\n' > f1.json; printf '2\n3\n' > f2.json
jq -c '[., input_filename]' f1.json f2.json; echo "  rc $?"
printf '4\n' | jq -c 'input_filename'; echo "  rc $?"
jq -n -c 'input_filename' f1.json; echo "  rc $?"
echo "== -S, --sort-keys"
j '{"b":1,"a":{"d":2,"c":[{"z":1,"y":2}]}}' '.'
printf '%s\n' '{"b":1,"a":{"d":2,"c":[{"z":1,"y":2}]}}' | jq -S -c .; echo "  rc $?"
printf '%s\n' '{"b":1,"a":2}' | jq --sort-keys .; echo "  rc $?"
printf '%s\n' '{"b":1,"a":2}' | jq -S 'keys_unsorted'; echo "  rc $?"
printf '%s\n' '{"b":1,"a":2}' | jq -Sr 'tojson'; echo "  rc $?"
echo "== edges"
j '"aaaa"' 'indices("aa")'
j '[1,1,1,1]' 'indices([1,1])'
j '"a😀a😀"' 'indices("😀")'
j '"abc"' 'index(1)'
j '[1,2]' 'indices("a")'
j '{"a":1}' 'indices("a")'
j '"abc"' '[match("";"g") | .offset]'
j '"aXbXc"' '[splits("x"; null)]'
j '"a1b"' 'splits("[0-9]")'
printf '4\n' | jq -c 'input_filename'; echo "  rc $?"
j '[[1,2],3]' 'getpath(["a"])'
j 'null' 'getpath(["a",1,"b"])'
j '{"a":1}' 'getpath("a")'
j '{"a":1}' 'delpaths("a")'
j '{"a":1}' 'delpaths([["a","b"]])'
j '[1,2]' 'paths(1)'
j '{"a":[{"b":null}]}' '[paths]'
j '{"a":[{"b":null}]}' '[leaf_paths]'
j 'null' '[limit(2; 1, error("x"))]'
j 'null' '[label $out | 1, 2 | ., break $out]'
j 'null' 'label $out | reduce range(10) as $i (0; if $i == 4 then ., break $out else . + $i end)'
j 'null' '[label $a | (label $a | 1, break $a), 2]'
j 'null' '[range(3) | label $x | ., break $x, 9]'
j 'null' 'try (label $x | error("e")) catch .'
j '{"a":1}' 'del(.a.b)'
j '[1]' 'delpaths([["a"]])'
j '{"a":1}' 'delpaths([[0]])'
j '[1,2]' 'indices([])'
j 'null' '[limit(1; 1, error("x"))]'
j '{"a":1}' 'index("a")'
j '[1,2,3]' 'rindex(9)'
j '"x"' 'tostream'
j '{"a":{"b":1}}' 'path(getpath(["a","b"]))'
j '{"a":1}' '[paths(..)]'
jq -c -s '[., input_filename]' f1.json f2.json; echo "  rc $?"
jq -c -R '[., input_filename]' f1.json f2.json; echo "  rc $?"
jq -c -Rs '[., input_filename]' f1.json f2.json; echo "  rc $?"
cat f1.json | jq -c '[., input_filename]' - f2.json; echo "  rc $?"
j 'null' '[1, label $x | 2, 3]'
j 'null' '1 + label $x | 2'
j 'null' '[label $x | 1] + [2]'
j 'null' 'label $x | 1 as $y | $y, break $x, 5'
j 'null' '[.[]?, label $x | 1]'
j 'null' 'label $x'
j 'null' 'label x | 1'
j 'null' '[label $x | break $x]'
j 'null' 'path(label $x | .a, break $x, .b)'
j '{"a":1}' '[paths] | length as $n | label $x | $n'
j '[1,2,3]' 'walk(if . == 2 then empty else . end)'
j '[1,2]' 'walk(if type == "number" then (., 10) else . end)'
j '{"a":1,"b":2,"c":3}' 'walk(if . == 2 then empty else . end)'
j '{"a":1,"b":2}' 'walk(if . == 1 then (5, 6) else . end)'
j '{"a":1,"b":2}' '[walk(if type == "object" then (., 7) else . end)]'
j '1' '[walk(empty)]'
j 'null' '[limit(-1; range(10))]'
j 'null' '[limit(1.5; range(10))]'
j 'null' '[limit(0.5; range(10))]'
j 'null' '[limit(-0.5; range(10))]'
j 'null' '[limit("a"; range(3))]'
j 'null' '[limit(2, 3; range(10))]'
j '{"a":[1,{"b":2}]}' '[recurse(.[]?; type != "number")]'
j '{"a":1}' 'path(getpath(["a"]), getpath(["x","y"]))'
cd / && rm -rf "$d"
