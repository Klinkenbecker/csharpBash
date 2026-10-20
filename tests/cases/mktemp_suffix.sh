# 2026-10-19 (red team F16): a mktemp template that does not end in X has an implied suffix, everything after its last
# X (GNU): `aXXXXb` was used as the literal name. Fewer than 3 X's is an error; with --suffix the template must end in
# X. Names are random, so each is matched against its pattern. Expected: GNU coreutils 8.32 via WSL, byte-identical.
export LC_ALL=C
d=$(mktemp -d) && cd "$d" || exit 1
# the pattern admits X itself (a random letter), so the name must also differ from the template: the X's were replaced
check() { local out rc; out=$(mktemp -u "$1" 2>&1); rc=$?; [[ $out =~ $2 && $out != "$1" ]] && echo "$1: rc=$rc ok" || echo "$1: rc=$rc [$out]"; }
check aXXXXb '^a[A-Za-z0-9]{4}b$'
check aXXXX.txt '^a[A-Za-z0-9]{4}\.txt$'
check a.XXXXXX '^a\.[A-Za-z0-9]{6}$'
check XXXb '^[A-Za-z0-9]{3}b$'
check aXXXbXXX '^aXXXb[A-Za-z0-9]{3}$'
mktemp -u aXXb 2>&1; echo "aXXb: rc=$?"
mktemp -u aXXbX 2>&1; echo "aXXbX: rc=$?"
out=$(mktemp --suffix=.s aXXXX); [[ $out =~ ^a[A-Za-z0-9]{4}\.s$ ]] && echo "--suffix: ok"
mktemp --suffix=.s aXXXXb 2>&1; echo "--suffix on aXXXXb: rc=$?"
f=$(mktemp aXXXXb) && [ -f "$f" ] && [[ $f =~ ^a[A-Za-z0-9]{4}b$ && $f != aXXXXb ]] && echo "created as the pattern"
cd / && rm -rf "$d"
