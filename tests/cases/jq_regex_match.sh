# 2026-11-07 (guy-96's need; the architect's go): jq's match, capture and scan, and --rawfile. They were refused
# ("capture/1 (match/1): not supported by C#Bash's in-process jq", exit 2); guy-96 split `grep -n` output of a transcript
# into the line number and the JSON. Offsets and lengths count code points, as jq does. Expected = jq 1.6 via WSL.
cd "$(mktemp -d)" || exit 1
j() { printf '%s\n' "$1" | jq -c "$2"; echo "  rc $?"; }
j '"ab12cd345"' 'match("[0-9]+")'
j '"ab12cd345"' '[match("[0-9]+"; "g")]'
j '"ab12cd345"' '[match("(?<d>[0-9])(?<e>[a-z])?"; "g")]'
j '"ab12"' 'match("([a-z]+)([0-9]+)") | .captures'
j '"foo bar foo"' '[match("foo"; "g") | .offset]'
j '"FOO"' 'match("foo"; "i") | .string'
j '"aéb12"' 'match("[0-9]+") | [.offset, .length]'
j '"x😀12"' 'match("[0-9]+") | [.offset, .length]'
j '"😀x😀"' '[match("x|😀"; "g") | [.offset, .length, .string]]'
j '"abc"' '[match("x")]'
j '"abc"' '[match("x"; "g")]'
j '"ab12"' 'capture("(?<n>[0-9]+)")'
j '"ab12"' 'capture("(?<l>[a-z]+)(?<n>[0-9]+)") | .l, .n'
j '"ab"' 'capture("(?<l>[a-z]+)(?<n>[0-9]+)?")'
j '"12:{\"a\":1}"' 'capture("^(?<n>[0-9]+):(?<j>.*)$") | .n, (.j | fromjson)'
j '"a1b22"' '[capture("(?<c>[a-z])(?<d>[0-9]+)"; "g")]'
j '"abc"' '[capture("(?<x>z)")]'
j '"a1b22c333"' '[scan("[0-9]+")]'
j '"a1b22c333"' '[scan("([a-z])([0-9]+)")]'
j '"xyz"' '[scan("q")]'
j '1' 'match("1")'
j '"a"' 'match(1)'
j '"a"' 'match("("; "g")'
j '"a"' 'match("a"; "q")'
# jq 1.6's edges, measured: array forms, scan/2 not defined, zero-width matches (no captures, the start moves on by one), "n"
j '"aAbB"' '[scan("a"; "i")]' 2>/dev/null
j '"aAbB"' '[match(["a","gi"]) | .string]'
j '"aAbB"' '[match(["a"]) | .string]'
j '"aAbB"' 'capture(["(?<x>a)","i"])'
j '"aAbB"' 'test(["A","i"])'
j '"aAbB"' '[match([])]' 2>/dev/null
j '"test"' '[match("";"g")] | length'
j '"a.b"' '[match("\\."; "g") | .offset]'
j '"ab"' 'match("(?<x>a)(b)") | .captures | map(.name)'
j '"ab"' '[match("(?=b)"; "g") | .offset]'
j '"ab"' '[match("(x)?"; "g")]'
j '"ab"' 'match("(x)?")'
j '"a1"' '[match("[0-9]*"; "g") | [.offset, .length]]'
j '"aXbX"' '[match("X"; "gn") | .offset]'
j '"ab"' '[match("x*"; "gn")]'
j '""' '[match(""; "g")] | length'
j '"ab"' '[scan("(x)?")]'
j '"ab"' '[capture("(?<v>x)?"; "g")]'
printf 'line1\nline2\n' > f.txt
jq -n --rawfile t f.txt '$t'; echo "  rc $?"
jq -n -r --rawfile t f.txt '$t | length'; echo "  rc $?"
jq -n --rawfile t f.txt --arg u x '[$t, $u]' -c; echo "  rc $?"
jq -n --rawfile t no-such-file '$t' 2>/dev/null; echo "  rc $?"
jq -n --rawfile t 2>/dev/null; echo "  rc $?"
cd / && rm -rf "$OLDPWD"
