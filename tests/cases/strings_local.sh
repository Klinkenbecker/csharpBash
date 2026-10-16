# 2026-10-14: what `strings` says that is C#Bash's own, so authored. --version never claims to be GNU; --help is
# GNU's usage text with a last line of C#Bash's (GNU's names its BFD targets); a bad -t / -e value, or `-` with no
# file, prints that usage on stderr and exits 1, as GNU does; an option GNU does not have follows the in-process
# tools' policy (DECISIONS 2026-09-04 #2); -d, -T, -U / --unicode and @file need object formats or UTF-8 display:
# the not-available notice (a PATH `strings` would take them; Git for Windows ships none). The rest is in
# strings_gnu.sh, against GNU.
d=$(mktemp -d) && cd "$d" || exit 1
printf 'some text\0' > f
v=$(strings --version); case $v in "strings (C#Bash in-process, GNU strings 2.38 compatible) "*) echo "version ok";; *) echo "version BAD [$v]";; esac
v=$(strings -V); case $v in *GNU\ Binutils*) echo "-V BAD";; *) echo "-V ok";; esac
strings --help > h; echo "help rc=$? lines=$(wc -l < h)"; head -1 h; tail -1 h
for a in '-t q' '-e z' '-e ll'; do echo "== strings $a f"; strings $a f 2>&1 >/dev/null | sed -n '1p;$p'; echo "rc=${PIPESTATUS[0]}"; done
echo "== strings -"; printf 'stdin text' | strings - 2>&1 >/dev/null | sed -n 1p; echo "rc=${PIPESTATUS[1]}"
if type -P strings >/dev/null; then
  echo "(a PATH strings exists: the policy and notice lines are not checked here)"
else
  echo "== strings -q f"; strings -q f 2>&1; echo "rc=$?"
  for a in '-d' '-T pe-x86-64' '-U e' '--unicode=hex' '@opts'; do
    echo "== strings $a f"; out=$(strings $a f 2>&1); echo "rc=$?"
    printf '%s\n' "$out" | sed -n 1p
    case $(printf '%s\n' "$out" | sed -n 2p) in "strings: to ask for it, open: https://github.com/klinkenbecker/csharpBash/issues/new?title=Build%20request%3A%20strings"*) echo "link ok";; *) echo "link BAD";; esac
  done
fi
cd / && rm -rf "$d"
