# 2026-10-17 (red team): a `~` PATH entry is $HOME's (it was looked up in the profile but launched as <cwd>/~/..., so a
# copy planted in the current directory ran); a variable inherited and then `unset` or `export -n` does not reach a
# child (it did: a child's environment began as C#Bash's own); a missing interpreter is "bad interpreter" (126), and
# through `/usr/bin/env` env's own message (127): both were "command not found". Expected: GNU bash 5.1 (WSL),
# byte-identical (env's quote marks normalised).
exec 2>&1
d=$(mktemp -d) && cd "$d" || exit 1
case "$PATH" in *\;*) sep=';' ;; *) sep=':' ;; esac
m() { sed "s|$d|D|g"; }
mkdir -p home/tbin cwd/~/tbin
printf '#!/bin/bash\necho "zztilde: from HOME"\n' > home/tbin/zztilde
printf '#!/bin/bash\necho "zztilde: PLANTED in the cwd"\n' > cwd/~/tbin/zztilde
chmod +x home/tbin/zztilde cwd/~/tbin/zztilde
echo "== a ~ PATH entry is \$HOME's"
( export HOME="$d/home"; PATH="~/tbin$sep$PATH"; cd cwd; zztilde; command -v zztilde | m )
echo "== an inherited variable unset or unexported does not reach a child"
ZZQ=inherited ZZR=inherited "$BASH" -c 'unset ZZQ; export -n ZZR; "$BASH" -c "echo \"unset: \${ZZQ-gone}, export -n: \${ZZR-gone}\""'
echo "== a missing interpreter"
printf '#!zzmissing\necho x\n' > a.sh; printf '#!/usr/bin/env zzmissing\necho x\n' > b.sh; chmod +x a.sh b.sh
./a.sh 2>&1 | sed 's|^.*\(\./a\.sh: \)|\1|'; echo "rc=${PIPESTATUS[0]}"
./b.sh 2>&1 | sed "s/: .zzmissing.: /: 'zzmissing': /"; echo "rc=${PIPESTATUS[0]}"
cd / && rm -rf "$d"
