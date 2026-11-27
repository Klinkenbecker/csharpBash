# 2026-11-27 (bugs list 8, found 2026-11-22): cp -a keeps hard links WITHIN what one cp copies, as GNU's -a implies
# --preserve=links: two names of one source file become two names of one copy (each became its own file). A link to a
# file outside the copy, -R, -p, a bare --preserve and two separate cp calls give separate files. -a, -d, --preserve
# and --no-preserve apply in the order given (`--no-preserve=links -a` keeps links). A read-only source links too.
# Expected = GNU cp 8.32 via WSL.
d=$(mktemp -d) && cd "$d" || exit 1
mkdir -p s/sub && echo one > s/f && ln s/f s/g && ln s/f s/sub/h && echo two > s/x && echo out > outside && ln outside s/o
t() { if [ "$1" -ef "$2" ]; then echo "$1 = $2"; else echo "$1 != $2"; fi; }
echo '-- cp -a, a folder'
cp -a s a1; echo "rc=$?"; t a1/f a1/g; t a1/f a1/sub/h; t a1/f a1/x; t a1/o outside; t a1/f s/f
stat -c '%n %h' a1/f a1/g a1/sub/h a1/x a1/o
echo '-- cp -a into an existing folder'
cp -a s a1; echo "rc=$?"; t a1/s/f a1/s/g; t a1/s/f a1/f
echo '-- cp -R'
cp -R s r1; t r1/f r1/g
echo '-- cp -a, two operands in one call'
mkdir m1; cp -a s/f s/g m1/; echo "rc=$?"; t m1/f m1/g
echo '-- cp -a, two calls'
mkdir m2; cp -a s/f m2/; cp -a s/g m2/; t m2/f m2/g
echo '-- cp -a over existing files'
mkdir e1; echo old > e1/f; echo old > e1/g; cp -a s/f s/g e1/; echo "rc=$?"; t e1/f e1/g; cat e1/f e1/g
echo '-- a write through one copied name'
echo changed > a1/f; cat a1/g a1/sub/h s/f
echo '-- the options, in order'
for opts in '-p' '-d' '--preserve' '--preserve=all' '--preserve=links' '--preserve=mode,links' '-a --preserve=timestamps' \
	'-a --no-preserve=links' '-a --no-preserve=all' '-a --no-preserve=mode' '--no-preserve=links -a'; do
	rm -rf o && mkdir o && cp $opts s/f s/g o/; echo "$opts: rc=$? $(t o/f o/g)"
done
echo '-- a read-only source'
chmod a-w s/f; mkdir ro; cp -a s/f s/g ro/; echo "rc=$?"; t ro/f ro/g; [ -w ro/g ] && echo writable || echo read-only
chmod -R u+w "$d"; cd / && rm -rf "$d"
