# 2026-10-11: command lookup follows the shell's own PATH, as in bash. It read the process environment and a
# hash table nothing reset: after PATH put B before A the shell still ran A's program; a temporary
# `PATH=… cmd` was not searched for cmd; and `( export PATH=… )` changed the parent's lookups too. Now any
# assignment to PATH resets the hash (bash), a temporary PATH is searched for that command only and not
# hashed, and a subshell's PATH stays in it. PATH here holds Windows entries split by `;` (documented).
# Authored expectations (measured in bash 5.1 with scripts: from-A, from-B, from-A, from-B, not found).
d=$(mktemp -d) && cd "$d" || exit 1
mkdir A B C
printf '@echo from-A\r\n' > A/zzq.cmd; printf '@echo from-B\r\n' > B/zzq.cmd; printf '@echo from-C\r\n' > C/zzc.cmd
P0=$PATH
export PATH="$d/A;$P0"; zzq
export PATH="$d/B;$P0"; zzq
PATH="$d/A;$P0" zzq
zzq
( export PATH="$d/C;$P0"; zzc )
zzc > /dev/null 2>&1; echo "parent after the subshell: rc=$?"
hash -r; zzq > /dev/null; hash | grep -c zzq
PATH=$PATH; hash 2>&1 | head -1
cd / && rm -rf "$d"
