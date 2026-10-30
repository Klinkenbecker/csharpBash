# 2026-10-30 (raised with item 4): Windows starts no program whose command line is over 32767 characters
# (CreateProcess). xargs's batch for an EXTERNAL program is capped to fit (quoted lengths, room for the program's path);
# GNU's 131072 still holds for everything, so a builtin keeps GNU's batches. Without the cap a big input failed: the
# whole batch could not start. And a command line too long for Windows is "Argument list too long", 126, as bash's
# E2BIG; it said "cannot start a program whose path is over 260 characters". Authored: Windows only.
echo "--- an external program: every item, in several batches"
seq 1 20000 | xargs "$BASH" -c 'echo $#' x > counts; echo "rc=$?"
awk '{ n += $1 } END { print "items", n }' counts; [ "$(wc -l < counts)" -gt 1 ] && echo "batches: more than one"
echo "--- a builtin: GNU's 131072"
seq 1 20000 | xargs echo | wc -l
echo "--- a command line too long"
"$BASH" -c : $(seq 1 20000) 2>&1 | sed 's/^.*: line [0-9]*: //; s/^[^ ]*Bash[^:]*: //'; echo "rc=${PIPESTATUS[0]}"
rm -f counts
