# P2: the threaded pipeline model — every stage on its own thread over managed pipes,
# per-thread console streams, SIGPIPE emulation (a consumer that exits early stops the
# producer), externals killed when their reader goes away, no Console races. Uses the
# interpreter itself ($BASH) as the "external" so the case is machine-independent.
"$BASH" -c 'echo ext-out' | cat
"$BASH" -c 'seq 1 200000' | head -1
yes | head -3
yes | head -1 | cat
"$BASH" -c 'yes' | head -2
"$BASH" --version | head -1 | cut -d, -f1
"$BASH" -c 'echo out; echo err >&2' 2>&1 | sort
echo x | "$BASH" -c 'cat' | cat
printf 'a\nb\n' | "$BASH" -c 'cat' | while read -r l; do echo "[$l]"; done
{ echo g1; "$BASH" -c 'echo g2'; } | cat
x=$("$BASH" -c 'seq 1 3' | tail -1); echo "x=$x"
n=$(seq 1 100 | "$BASH" -c 'cat' | wc -l); echo "n=$n"
set -o pipefail; "$BASH" -c 'exit 3' | cat; echo "pf=$?"; set +o pipefail
"$BASH" -c 'exit 3' | cat; echo "nopf=$? ps=${PIPESTATUS[0]} ${PIPESTATUS[1]}"
set -o | grep -c 'on$' | "$BASH" -c 'cat' | head -n 1000 > cases/pipe_tmp.txt; cat cases/pipe_tmp.txt; rm cases/pipe_tmp.txt
{ printf 'l1\nl2\nl3\n' | "$BASH" -c 'cat'; } | while read -r l; do echo "<$l>"; done
echo a | tr a b | tr b c | tr c d
