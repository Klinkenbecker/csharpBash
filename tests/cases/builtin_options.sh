# 2026-10-10: builtins check their options as bash does: an invalid one is `name: -Z: invalid option`
# then bash's usage line (no script prefix), status 2; options end at the first operand or `--`. Until
# then most printed no usage line, and printf, cd, pwd, export, readonly, unset, alias, unalias, source,
# pushd, popd, dirs, trap, history, jobs and wait took `-Z` as an operand or ignored it; `kill -Z 1`
# went on to signal; `trap -- 'cmd' SIG` (what `trap -p` prints) failed; readarray / typeset / local
# were named mapfile / declare. The script runs by relative name from a temp dir, streams merged
# inside, so every shell names it the same and keeps the order. Expected output: GNU bash via WSL.
d=$(mktemp -d) && cd "$d" || exit 1
mkdir a b sub
echo 'echo "sourced: ${BASH_SOURCE[0]} $#"' > lib.sh
cat > t.sh <<'EOF'
{
for b in read mapfile readarray declare typeset local export readonly unset type command hash set shopt disown printf cd pwd kill wait jobs trap alias unalias pushd popd dirs history getopts let shift source . exit return; do
  case $b in
    local)  ( f() { local -Z; }; f; echo "$b rc=$?" ) ;;
    return) ( f() { return -Z; }; f; echo "$b rc=$?" ) ;;
    *)      ( $b -Z; echo "$b rc=$?" ) ;;
  esac
done
echo "== edges"
( cd a b; echo "cd a b rc=$?" )
( cd -P sub && echo "cd -P ok"; cd -L -- . && echo "cd -L -- ok"; cd - >/dev/null && echo "cd - ok" )
( pwd foo >/dev/null; echo "pwd foo rc=$?" ); ( pwd -LZ; echo "pwd -LZ rc=$?" ); pwd -P >/dev/null && echo "pwd -P ok"
( export X_ZZ=1 -n; echo "export X -n rc=$?" ); ( export -n X_ZZ; echo "export -n rc=$?" )
( x=1; unset -v x; echo "unset -v ${x-gone}"; unset X -v; echo "unset X -v rc=$?" )
( printf -v; echo "printf -v rc=$?" ); ( printf '-%s\n' x; echo "printf -%s rc=$?" ); printf -- '-%s\n' x; printf -v v '%s' y; echo "printf -v $v"
( source; echo "source rc=$?" ); ( source --; echo "source -- rc=$?" ); source -- ./lib.sh 1 2; . ./lib.sh
( alias -p; echo "alias -p rc=$?" ); ( unalias; echo "unalias rc=$?" ); ( alias q=echo; unalias -a; alias; echo "unalias -a rc=$?" )
( pushd +Z; echo "pushd +Z rc=$?" ); ( pushd +3; echo "pushd +3 rc=$?" ); ( pushd a b; echo "pushd a b rc=$?" ); ( popd +Z; echo "popd +Z rc=$?" ); ( popd foo; echo "popd foo rc=$?" )
( dirs +Z; echo "dirs +Z rc=$?" ); ( dirs foo; echo "dirs foo rc=$?" ); ( dirs -cv; echo "dirs -cv rc=$?" ); ( dirs -c; echo "dirs -c rc=$?" )
( kill -Z 1; echo "kill -Z rc=$?" ); ( kill -s Z 1; echo "kill -s Z rc=$?" ); ( kill -n 99 1; echo "kill -n 99 rc=$?" ); ( kill -s; echo "kill -s rc=$?" )
kill -l 9; kill -l INT; kill -l 137; ( kill -l 99; echo "kill -l 99 rc=$?" ); kill -0 $$ && echo "kill -0 ok"
( trap -- 'echo hi' EXIT ); ( trap -lZ; echo "trap -lZ rc=$?" ); ( trap 'echo x' INT; trap INT; trap -p INT; echo "trap INT rc=$?" )
( trap 'echo x' INT TERM; trap 2 15; trap; echo "trap 2 15 rc=$?" ); ( trap 'echo hi'; echo "trap one rc=$?" ); ( trap -; echo "trap - rc=$?" )
( wait -p; echo "wait -p rc=$?" ); ( jobs -l; echo "jobs -l rc=$?" ); ( history -Z; echo "history rc=$?" )
} 2>&1
EOF
"$BASH" t.sh
cd / && rm -rf "$d"
