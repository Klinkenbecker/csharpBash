# 2026-10-18 (review R4): icu.dll (~0.7 ms a launch) loads at the first culture-sensitive operation, not on every
# start. A culture-sensitive StartsWith (the parser's `~/`, `--`), current-culture integer parses (positionals, fd
# numbers, `exit N`), ToLowerInvariant, a SID's text and a file's time as local time each loaded it in every call;
# a negative number formatted through the user's culture would too (the shell runs in the invariant culture). The
# timing switch notes at exit whether it is loaded ("icu yes|no"). Positive controls: ${x^^} of a non-ASCII value and
# date (the local time zone) need culture data and load it. Native AOT only: the JIT runtime loads ICU itself at
# start-up. Authored (the switch is C#Bash's).
d=$(mktemp -d) && cd "$d" || exit 1
printf 'one\ntwo\n' > f.txt
icu() { rm -f log; BASH_TIMING_LOG="$d/log" "$BASH" -c "$1" > /dev/null 2>&1 < /dev/null; sed -n 's/^icu //p' log; }
icu 'whoami.exe' > /dev/null   # the command map for this PATH exists before the measured run (a new one is pruned)
for c in 'exit 0' 'true' 'ls' 'cat f.txt | head -1' 'grep -rn two .' 'cd / && pwd' \
         'x=$(echo a); [[ $x == a ]] && echo "$x"' 'set -- p q; echo "$2 $# $UID $PPID $SHLVL"' 'exit 3' \
         'echo x 2> /dev/null' 'f() { local v=1; return 0; }; f' 'whoami.exe' 'echo a | cat' 'cat <(echo a)' \
         'printf "%d %s %5.2f\n" 5 a 1.5' 'for i in 1 2; do (( i > 1 )) && echo $i; done' 'case a in a) echo y;; esac' \
         'read -r v w <<< "x y"; echo "$w"' 'echo $(( 7 * 6 ))' 'echo $((-5)) $((3 - 9))'; do
	printf '%-45s %s\n' "$c" "$(icu "$c")"
done
echo "== positive controls"
for c in 'x=é; echo ${x^^}' 'date'; do
	printf '%-45s %s\n' "$c" "$(icu "$c")"
done
cd / && rm -rf "$d"
