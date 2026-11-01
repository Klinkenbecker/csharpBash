# 2026-11-01 (installer-a4's report, to confirm an installer-made hard link): a file's link count and index (its inode)
# were 1 and 0 for every file, so `[ a -ef b ]` was false for two names of one file, stat %h/%i, ls -i, find -links
# found nothing and find -samefile/-inum were refused (handed to a PATH find). Now from GetFileInformationByHandle:
# the link count, the file index on its volume, the volume as the device. Numbers differ between systems, so the
# relations are compared. Not here, on purpose: ls -l's link column (still 1: reading it costs an open per entry,
# the architect's call) and a directory's count (Windows keeps 1, Unix 2 + subdirectories).
# Expected: GNU bash 5.1 + coreutils 8.32 via WSL.
cd "$(mktemp -d)" || exit 1
printf x > a; ln a b; printf y > c
for f in a b c; do printf '%s: %s links\n' $f "$(stat -c %h $f)"; done
[ "$(stat -c %i a)" = "$(stat -c %i b)" ] && echo "a b: one index" || echo "a b: two indexes"
[ "$(stat -c %i a)" = "$(stat -c %i c)" ] && echo "a c: one index" || echo "a c: two indexes"
[ "$(stat -c %i a)" != 0 ] && echo "a's index is not 0" || echo "a's index is 0"
[ "$(stat -c %d a)" = "$(stat -c %d c)" ] && echo "a c: one device" || echo "a c: two devices"
[ "$(stat -c '%D' a)" = "$(printf '%x' "$(stat -c %d a)")" ] && echo "%D is %d in hex" || echo "%D is not %d in hex"
[ a -ef b ] && echo "[ a -ef b ]: true" || echo "[ a -ef b ]: false"
[ a -ef c ] && echo "[ a -ef c ]: true" || echo "[ a -ef c ]: false"
[ a -ef ./a ] && echo "[ a -ef ./a ]: true" || echo "[ a -ef ./a ]: false"
[ a -ef nope ] && echo "[ a -ef nope ]: true" || echo "[ a -ef nope ]: false"
ia=$(ls -i a | awk '{print $1}'); ib=$(ls -i b | awk '{print $1}'); ic=$(ls -i c | awk '{print $1}')
[ "$ia" = "$ib" ] && [ "$ia" != "$ic" ] && [ "$ia" = "$(stat -c %i a)" ] && echo "ls -i: as stat, a = b, c differs" || echo "ls -i: a=$ia b=$ib c=$ic"
echo "find -samefile a: $(find . -samefile a | sort | tr '\n' ' ')"
echo "find -links 2: $(find . -type f -links 2 | sort | tr '\n' ' ')"
echo "find -links -2: $(find . -type f -links -2 | sort | tr '\n' ' ')"
echo "find -inum (a's): $(find . -inum "$(stat -c %i a)" | sort | tr '\n' ' ')"
find . -type f -printf '%p %n\n' | sort
[ "$(find . -name a -printf %i)" = "$(stat -c %i a)" ] && echo "find -printf %i: as stat" || echo "find -printf %i: differs"
rm b; echo "after rm b: a has $(stat -c %h a) link"
