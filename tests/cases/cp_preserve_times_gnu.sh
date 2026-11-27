# 2026-11-27 (bugs list 15, found fixing bugs list 8): cp keeps a copy's modification time only for -p, -a, a bare
# --preserve, or a --preserve list naming timestamps or all, as GNU's; --preserve=links, =mode and -d stamp the copy
# now (any --preserve kept the time); --no-preserve takes it back, in the order given. A copied folder's own time too.
# Expected = GNU cp 8.32 via WSL.
d=$(mktemp -d) && cd "$d" || exit 1
echo x > f && touch -d '2020-01-01 00:00' f
for opts in '--preserve' '-p' '-a' '--preserve=timestamps' '--preserve=all' '--preserve=links' '--preserve=mode' '-d' \
	'-a --no-preserve=timestamps' '-p --no-preserve=all' '--no-preserve=timestamps -p' '-a --no-preserve=mode' \
	'--preserve=mode,timestamps'; do
	rm -f g; cp $opts f g; echo "$opts: $(stat -c %y g | cut -c1-4)"
done
mkdir s && echo y > s/h && touch -d '2020-01-01 00:00' s/h s
for opts in '-a' '-r --preserve=links' '-r --preserve=timestamps' '-a --no-preserve=timestamps'; do
	rm -rf t; cp $opts s t; echo "folder $opts: $(stat -c %y t | cut -c1-4) $(stat -c %y t/h | cut -c1-4)"
done
cd / && rm -rf "$d"
