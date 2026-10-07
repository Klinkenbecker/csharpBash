# 2026-10-05: the in-process jq subset (DECISIONS 2026-10-05, option (c)). Runs unchanged under
# GNU bash with real jq 1.6 (Ubuntu 22.04, the oracle), which generated the expected output.
cd "$(mktemp -d)" || exit 1
t() { printf '%s: ' "$1"; shift; "$@" 2>&1 | tr '\n' ' '; echo; }

# the evidence cases (E:/Claude/csharpbash-findings-2026-10-05-jq)
cat > journal.jsonl <<'EOF'
{"agent":"a1","result":{"summary":"two gaps","verdict":"partial","gaps":[{"severity":"high","missing":"tests","consequence":"regressions"},{"severity":"low","missing":"docs","consequence":"confusion"}]}}
{"agent":"a2","result":{"summary":"clean","verdict":"pass","gaps":[]}}
EOF
jq -r '.result | "######## verdict: \(.verdict)", .summary, (.gaps[] | "[\(.severity)] \(.missing)\n    -> \(.consequence)")' journal.jsonl
jq -s -c 'map({agent, n: (.result.gaps | length)}), (max_by(.result.summary | length) | .agent)' journal.jsonl
cat > comments.json <<'EOF'
[{"user":{"login":"alice"},"created_at":"2026-01-02T03:04:05Z","body":"First\nline two"},{"user":{"login":"bob"},"created_at":"2026-01-03T00:00:00Z","body":"Second"}]
EOF
jq -r 'length, (.[] | "=== \(.user.login) \(.created_at)\n\(.body)")' comments.json
echo '{"hooks":{"PreToolUse":[{"matcher":"Bash","hooks":[{"command":"x.sh"}]}]}}' | jq -r '.hooks.PreToolUse[0] | "matcher=\(.matcher) command=\(.hooks[0].command)"'
echo '{"a":1}' | jq empty && echo valid

# printing
jq . <<< '{"a":[1,{"b":null}],"c":{},"d":[],"e":"x"}'
t 'numbers' jq -c '.' <<< '[1.0, 3.14, -0, 100000, 1e17, 1e300, 0.0001, 0.00001, 1.5e-7, 123456789012, 0.1]'
t 'escapes' jq -c '.' <<< '["tab\tnl\ncr\rq\"bs\\", "\u0001\u001f\u007f", "é✓", "/"]'

d='{"a":{"b":[1,2,3]},"name":"x","tags":["p","q"],"n":null}'
t 'paths'      jq -c '.a.b[1], .a.b[-1], .a.b[1:], .a.b[:1], ("hello"[1:3]), (.a | .[])' <<< "$d"
t 'optional'   jq -c '.name.x?, (5 | .[]?), "ok"' <<< "$d"
t 'arith'      jq -c '1 + 2 * 3, 10 / 4, 7 % 3, -5 % 3, "a" + "b", [1] + [2], {a:1} + {b:2}, null + 1, [1,2,3] - [2], ("ab" * 3), ({a:{b:1}} * {a:{c:2}})' <<< null
t 'compare'    jq -c '1 < 2, "a" < "b", [] < {}, (1 == 1.0), (true and false), (null or 1), (1 | not)' <<< null
t 'sort mixed' jq -c 'sort' <<< '[{"a":1},[2],"s",3,true,false,null,{"a":0},[1]]'
t 'alt'        jq -c '.n // "dflt", .name // "dflt", (error("x") // 5), (empty // 7)' <<< "$d"
t 'if'         jq -c '.[] | if . < 2 then "lo" elif . < 3 then "mid" else "hi" end' <<< '[1,2,3]'
t 'as'         jq -c '. as [$a, $b] | {a: $a, b: $b}, (.[] as $x | $x * 10)' <<< '[1,2]'
t 'as object'  jq -c '. as {a: $x, $name} | [$x, $name]' <<< '{"a":1,"name":"n"}'
t 'reduce'     jq -c 'reduce .[] as $x (0; . + $x)' <<< '[1,2,3,4]'
t 'reduce pat' jq -c 'reduce .[] as [$k, $v] ({}; .[$k] = $v)' <<< '[["a",1],["b",2]]'
t 'construct'  jq -c '{name, first: .tags[0], (.name): 1, "k v": 2, $x}' --arg x X <<< "$d"
t 'interp'     jq -r '"\(.name)-\(.tags | length)-\(.a)-\(.n)"' <<< "$d"
t 'cartesian'  jq -c '{a: (1,2), b: (3,4)}, "\(1,2)-\(3,4)", ((1,2) + (10,20))' <<< null
t 'assign'     jq -c '.a.b[0] = 9 | .new = "v" | .tags[5] = "z"' <<< "$d"
t 'update'     jq -c '.a.b |= map(. * 2) | .count += 1 | .n //= "was null" | .tags[] |= ascii_upcase' <<< "$d"
t 'del'        jq -c 'del(.a, .tags[0]), del(.[] | select(. == null))' <<< "$d"
t 'map etc'    jq -c 'map(select(. > 1)), map_values(. + 1), (.[] |= . * 10)' <<< '[1,2,3]'
t 'keys'       jq -c 'keys, keys_unsorted, has("a"), length, (.name | length), ([1,2] | has(1), has(2))' <<< '{"b":1,"a":"xyz","name":"héllo"}'
t 'entries'    jq -c 'to_entries, with_entries(.value += 1), ([{"name":"k","value":1},{"k":"j","v":2}] | from_entries)' <<< '{"a":1,"b":2}'
t 'add/any'    jq -c 'add, any, all, (map(. > 1) | any), any(.[]; . > 2), all(. > 0), ([] | add)' <<< '[1,2,3]'
t 'sort/group' jq -c 'sort_by(.k), group_by(.k), unique_by(.k), (map(.k) | unique), sort_by(.k, .j)' <<< '[{"k":2,"j":1},{"k":1,"j":0},{"k":2,"j":0}]'
t 'min/max'    jq -c 'min, max, ([{"a":3},{"a":1}] | min_by(.a), max_by(.a)), ([] | max)' <<< '[3,1,2]'
t 'first etc'  jq -c 'first, last, reverse, ([1,[2,[3]]] | flatten, flatten(1)), first(.[] | select(. > 1)), last(.[])' <<< '[1,2,3]'
t 'range'      jq -c '[range(3)], [range(1;7;2)], [range(5;0;-2)], [range(0,1;3,4)]' <<< null
t 'convert'    jq -c '(1|tostring), ("12"|tonumber), ([1]|tostring), map(type), (1.5|tostring)' <<< '[1,"a",null,true,[],{}]'
t 'json'       jq -c 'tojson, (tojson | fromjson)' <<< '{"a":[1,"x"]}'
t 'split/join' jq -c 'split(","), (split(",") | join("-")), ([1,null,"a",true] | join(","))' <<< '"a,b,c"'
t 'regex'      jq -c 'test("b+"), test("B"; "i"), sub("b"; "X"), gsub("[ac]"; "_"), sub("(?<l>[a-z])"; "<\(.l)>"), gsub("(?<d>c)"; "[\(.d)]")' <<< '"abcabc"'
t 'strings'    jq -c 'startswith("ab"), endswith("bc"), ltrimstr("ab"), rtrimstr("bc"), ascii_upcase, ("ABC" | ascii_downcase), ltrimstr("zz")' <<< '"abc"'
t 'contains'   jq -c 'contains({a:[1]}), contains({b:"x"}), ("foobar" | contains("oba")), contains({a:[9]})' <<< '{"a":[1,2],"b":"xyz"}'
t 'formats'    jq -r '@csv, @tsv, @json, (.[1] | @base64), @text' <<< '[1,"a\"b","c\td",null,true,1.5]'
t 'env'        env FOO=bar jq -r 'env.FOO, $ENV.FOO' <<< null
t 'recurse'    jq -c '[.. | select(type == "number")], [recurse | select(type == "array") | length]' <<< '[1,[2,{"a":3}]]'
t 'try'        jq -c 'try error("boom") catch ., (.[] | try (if . == 2 then error("x") else . end) catch "caught"), (try error({"code":1}) catch .code)' <<< '[1,2,3]'
t 'options'    jq -n -c '1+1, [1,2]'
printf '1 2 3' | jq -s -c .
echo null | jq -e . ; echo "e=$?"
echo '[]' | jq -e '.[]' ; echo "e=$?"
jq -j '.[]' <<< '["a","b",1]'; echo
jq -n -c --argjson v '{"k":[1]}' --arg s str '$v.k, $s'

# errors (stdout and stderr together)
t 'index num'  jq '.a.b' <<< '{"a":5}'
t 'iterate'    jq '.[]' <<< '5'
t 'add types'  jq '1 + "a"' <<< null
t 'div zero'   jq '1 / 0' <<< null
t 'not str'    jq 'error({"a":1})' <<< null
t 'bad json'   jq '.' <<< '{"a":'
t 'syntax'     jq '.a |' <<< null
echo '{"a":5}' | jq '.a.b' >/dev/null 2>&1; echo "status rc=$?"

# type filters (added at the architect's "2/ yes"), also as paths
t 'types'      jq -c '[.[] | numbers], [.[] | strings], [.[] | booleans], [.[] | nulls], [.[] | arrays], [.[] | objects], [.[] | iterables], [.[] | scalars], [.[] | values]' <<< '[1,"a",true,null,[2],{"b":3}]'
t 'types path' jq -c 'del(.. | nulls), [.. | numbers], (.. |= (strings |= ascii_upcase))' <<< '{"a":null,"b":[1,null,"x"],"c":{"d":2}}'
