# 2026-10-08: fold counts columns as GNU fold does (\b back one, \r to 0, tab to the next multiple of 8,
# bytes not characters; -s breaks after the last blank; no newline added) where it cut fixed runs of
# characters; and tee takes /dev/null, /dev/stdout and /dev/stderr (it failed: No such file or
# directory). Expected output: GNU coreutils via WSL.
c() { od -c | sed 's/^[0-7]* *//' | tr -s ' ' | tr '\n' '|'; echo; }
printf 'ab\rcd\n' | fold -w 3 | c
printf 'abcdef\rgh\n' | fold -w 4 | c
printf 'ab\bcd\n' | fold -w 3 | c
printf 'a\tbc\n' | fold -w 4 | c
printf 'a\tbcdefghij\n' | fold -w 10 | c
printf 'ab\rcd\n' | fold -b -w 3 | c
printf 'abcdefgh' | fold -w 3 | c
printf 'the quick brown fox jumps\n' | fold -s -w 10 | c
printf 'aaaaaaaaaaaaaaa bb\n' | fold -s -w 6 | c
printf 'x\ty z\n' | fold -s -w 5 | c
printf 'h\303\251llo\n' | fold -w 3 | c
printf '\n\nab\n' | fold -w 1 | c
printf 'abcdef\n' | fold -3 | c
echo x | tee /dev/null; echo "tee /dev/null rc=$?"
echo y | tee /dev/null /dev/null > /dev/null; echo "tee twice rc=$?"
echo z | tee -a /dev/null; echo "tee -a rc=$?"
echo w | tee /dev/stdout | c
echo v | tee /dev/stderr 2>&1 >/dev/null | c
