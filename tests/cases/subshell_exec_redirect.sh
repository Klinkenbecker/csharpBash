# 2026-10-17: an `exec` redirect made inside a subshell, `$( )` or a pipeline stage ends with it, as bash: `( exec > f;
# echo in ); echo after` sent "after" and everything later into f. Its EXIT trap still writes inside it; a `{ }`
# group's `exec` stays (the group is the shell itself). Expected: GNU bash 5.1 (WSL), byte-identical.
exec 2>&1
d=$(mktemp -d) && cd "$d" || exit 1
( exec > f; echo in ); echo "after subshell"
( exec 2> e; echo err-in >&2 ); echo "after stderr subshell" >&2
x=$(exec > h; echo x-in); echo "after cmdsub: x=[$x]"
( exec > p; echo p-in ) | cat; echo "after pipeline stage"
( exec 3> t; echo t-in >&3 ); { echo y >&3; } 2>/dev/null && echo "fd 3 after: open" || echo "fd 3 after: closed"
( trap 'echo trap-out' EXIT; exec > q; echo q-in ); echo "after trap subshell"
( exec > r; exit 3 ); echo "after exit: rc=$?"
echo "f=[$(cat f)] e=[$(cat e)] h=[$(cat h)] p=[$(cat p)] t=[$(cat t)] q=[$(tr '\n' ' ' < q)] r=[$(cat r)]"
{ exec 4> g; }; echo g-in >&4; exec 4>&-; echo "a group's exec stays: g=[$(cat g)]"
cd / && rm -rf "$d"
