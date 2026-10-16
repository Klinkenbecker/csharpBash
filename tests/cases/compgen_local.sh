# 2026-10-15: what compgen says about THIS shell, so authored (the architect: "C#Bash is really _only_ about its
# builtins"). -b / -A builtin / enabled list every C#Bash builtin, the in-process tools included, sorted, once;
# none of bash's builtins C#Bash lacks. -c lists aliases, keywords, functions, then builtins, and scans no PATH
# (deferred). -o, -F, -C, -u, -g, -s and the actions binding, group, helptopic (no `help` here), hostname,
# service, user, and the builtins complete and compopt, answer with the not-available notice and status 2.
# The rest is in compgen.sh, against GNU bash.
d=$(mktemp -d) && cd "$d" || exit 1
check() { if [ "$2" = "$3" ]; then echo "$1 ok"; else echo "$1 BAD [$2] want [$3]"; fi; }
b=$(compgen -b)
for n in . : '[' echo cd compgen complete compopt cat grep sed awk jq strings find; do
  printf '%s\n' "$b" | grep -qxF -- "$n" && echo "-b has $n" || echo "-b LACKS $n"
done
for n in bind enable help ulimit caller logout suspend; do
  printf '%s\n' "$b" | grep -qxF -- "$n" && echo "-b HAS $n" || echo "-b has no $n"
done
check sorted-once "$(printf '%s\n' "$b" | LC_ALL=C sort -u)" "$b"
check enabled "$(compgen -A enabled)" "$b"
check builtin-ec "$(compgen -A builtin ec)" "echo"
compgen -A disabled; echo "disabled rc=$?"
compgen -A stopped; echo "stopped rc=$?"
alias ezal=echo; ezfn() { :; }
c=$(compgen -c e)
check c-head "$(printf '%s\n' "$c" | head -5 | paste -sd' ')" "ezal else elif esac ezfn"
check c-rest "$(printf '%s\n' "$c" | tail -n +6)" "$(compgen -b e)"
check c-fi "$(compgen -c fi | paste -sd' ')" "fi find"
type -P notepad >/dev/null && { compgen -c notepad; echo "notepad (on PATH) rc=$?"; } || echo "notepad (on PATH) rc=1"
for a in '-o filenames -f x' '-F f' '-C c' '-u' '-g' '-s' '-A binding' '-A group' '-A helptopic' '-A hostname' '-A service' '-A user'; do
  out=$(compgen $a 2>&1); rc=$?
  echo "== compgen $a: rc=$rc"
  printf '%s\n' "$out" | sed -n 1p | sed 's/^.*line [0-9]*: //'
  case $(printf '%s\n' "$out" | sed -n 2p) in *"compgen: to ask for it, open: https://github.com/klinkenbecker/csharpBash/issues/new?title=Build%20request%3A%20compgen"*) echo "link ok";; *) echo "link BAD";; esac
done
for c in 'complete -F f cmd' 'compopt -o nospace'; do
  out=$($c 2>&1); rc=$?
  echo "== $c: rc=$rc"; printf '%s\n' "$out" | sed -n 1p | sed 's/^.*line [0-9]*: //'
done
cd / && rm -rf "$d"
