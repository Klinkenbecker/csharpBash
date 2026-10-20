# 2026-10-19 (found beside red team F11): in sed only a regex has bracket expressions; the replacement and y's parts are
# text. `s/^/[/;s/$/]/` read `[/;s/$/]` as one bracket expression (output `[/;s/$/]abc`), and `-e 's/a/[/' -e
# 's/c/]/'` ran the two scripts together. Expected: GNU sed 4.8 via WSL, byte-identical.
echo abc | sed 's/^/[/;s/$/]/'
echo abc | sed -e 's/a/[/' -e 's/c/]/'
echo abc | sed 's/b/[x/'
echo abc | sed 's/b/]/g'
echo 'a/b' | sed 's/[/]/|/'
echo 'a]b' | sed 's/[]]/X/'
echo abc | sed 'y/abc/[]x/'
echo 'x[1]' | sed 's/\[\([0-9]\)\]/<\1>/'
echo abc | sed '/[b]/s/c/[c]/'
