# 2026-10-15 (daq-d1, 2026-10-12: `compgen` was not a builtin; scope greenlit 2026-10-12): compgen as bash 5.1's.
# -W (split at IFS honouring quotes, then expanded: braces, parameters, command substitution, arithmetic, tilde,
# word splitting, never pathname expansion), -X (`&` is the word, a leading ! keeps the matches), -P / -S after the
# filter, -G (the word is ignored), -f / -d (the word completed as a path; dot files included), the name lists
# (-a, function, -v, -e, arrayvar, -k in bash's order, signal, shopt, setopt, -j / running / stopped, the newest
# job first), several actions in bash's order then -G then -W, no duplicate removal, status 1 when nothing
# matches, 2 for a bad option or action. File lists are sorted here: bash prints directory order, which differs
# between file systems. compgen describes THIS shell (the architect, 2026-10-15): -b, the builtins in -c, the
# missing PATH scan (deferred), helptopic and the notices are in compgen_local.sh.
# Expected output: GNU bash via WSL.
d=$(mktemp -d) && cd "$d" || exit 1
mkdir -p sub/deep dir2 p; : > a1.txt; : > a2.txt; : > b.md; : > sub/s.txt; : > .hid
: > p/zznox.txt
r() { echo "== compgen $*"; compgen "$@" 2>&1 | sed 's/^.*line [0-9]*: //'; echo "rc=${PIPESTATUS[0]}"; }
rs() { echo "== compgen $* | sort"; compgen "$@" | sort; echo "rc=${PIPESTATUS[0]}"; }
r -W 'beta alpha gamma alp' al
r -W 'beta alpha gamma alp'
r -W '{x,y}z $NOPE_UNSET "q r" a\ b'
v='m n'; r -W '$v'
r -W '*'
r -W '$(echo c1 c2) $((1+2))'
IFS=, ; r -W 'a,b c'; unset IFS
r -W 'one two three' -X 't*'
r -W 'one two three' -X '!t*'
r -W 'one two three' -P '<' -S '>' t
r -W 'apple apricot banana' -X '&*' ap
r -W 'a&b ab' -X 'a\&*'
r -W 'Aa aa' a
r -W 'x' nomatch
r -W 'x y' -- -x
r -W 'a b' -X 'a' -P p
r -X '*' -W q
r -W ''
r
rs -f
rs -f a
rs -f sub/
r -f sub/deep
r -f zz
rs -d
r -d s
r -d sub/d
rs -S / -d
rs -A directory -P x
rs -G 'a*.txt'
rs -G 'a*.txt' a1
r -G 'zz*'
rs -A file -X '*.txt'
alias zzb='echo b' zza='echo a'
zzf2() { :; }; zzf1() { :; }
zzv2=1; zzv1=2; export zzx=3; declare -a zzarr=(1); declare -A zzas=([k]=v)
r -a zz
r -A alias zzb
r -A function zz
r -v zz
r -e zz
r -A export zzx
r -A arrayvar zz
r -A variable zzv
r -a -A function zz
r -W 'zzf1 zzw' -A function zz
r -k
r -k fi
r -A keyword d
( PATH=$d/p; echo "== compgen -c zz"; compgen -c zz; echo "rc=$?" )
r -A signal SIGU
r -A signal SIGRTMAX-1
echo "signals: $(compgen -A signal | wc -l)"
r -A shopt ext
r -A setopt err
echo "setopt: $(compgen -A setopt | paste -sd' ')"
sleep 30 & { sleep 31; } &
echo "== compgen -j"; compgen -j; echo "rc=$?"
echo "== compgen -A running"; compgen -A running; echo "rc=$?"
echo "== compgen -A stopped"; compgen -A stopped; echo "rc=$?"
kill %1 %2 2>/dev/null; wait 2>/dev/null
r -A bogus
r -Z
cd / && rm -rf "$d"
