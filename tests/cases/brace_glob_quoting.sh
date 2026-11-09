# 2026-11-07 (KNOWN_ISSUES.md's open list): brace and pathname expansion take a QUOTED character as plain, and bash
# brace-expands only a word's literal text, not an expansion's result. Both saw the field after quote removal: `{x\,y,z}`
# gave x y z (bash: x,y z), `"{"a,b}` a b, `x='{a,b}'; echo $x` a b, `"a[1]"*` matched a1b. Also an escaped dot: `{1\..3}`
# was a range and `[[ axb =~ a\.b ]]` matched. Expected = GNU bash 5.1 via WSL.
p() { printf '%-26s' "$1"; shift; printf '[%s] ' "$@"; echo; }
p '{x\,y,z}'        {x\,y,z}
p '{x",",y,z}'      {x",",y,z}
p "{x',',y,z}"      {x',',y,z}
p '"{"a,b}'         "{"a,b}
p '\{a,b}'          \{a,b}
p '{a,b"}"'         {a,b"}"
p '{a,b\}'          {a,b\}
p 'a{b\,c}d'        a{b\,c}d
p '{a\{,b}'         {a\{,b}
p '{"a b",c}'       {"a b",c}
p '{a,b}"{c,d}"'    {a,b}"{c,d}"
p '{1"..3"}'        {1"..3"}
p '{1\..3}'         {1\..3}
p 'x{a,"b}"'        x{a,"b}"
d=$(mktemp -d); cd "$d" || exit 1
touch 'a[1]b' a1b ab .hid a.b; mkdir d.x; touch d.x/f1
p '"a[1]"*'         "a[1]"*
p 'a\[1]*'          a\[1]*
p 'a[1]*'           a[1]*
p "'a['1]*"         'a['1]*
p '"a"*'            "a"*
p '".h"*'           ".h"*
p '["a"]1b'          ["a"]1b
p 'a["."]b'          a["."]b
p '"d.x"/f*'         "d.x"/f*
cd / && rm -rf "$d"
x='{a,b}'; p 'x={a,b}; $x' $x
y='a,b';   p 'y=a,b; {$y,c}' {$y,c}
z='a}';    p 'z=a}; {$z,c}' {$z,c}
w='*';     d=$(mktemp -d); cd "$d"; touch f1 f2; p 'w=*; $w (2 files)' $w; p '{$w,x}' {$w,x}; cd / && rm -rf "$d"
[[ axb =~ a\.b ]] && echo "axb =~ a\.b: match" || echo "axb =~ a\.b: no"
[[ a.b =~ ^a\.b$ ]] && echo "a.b =~ ^a\.b$: match" || echo "a.b: no"
echo \.x
