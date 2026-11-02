# 2026-11-01 (found by Claude): a division by zero in awk is gawk's run-time fatal, exit 2: "awk: cmd. line:N:
# (FILENAME=f FNR=n) fatal: division by zero attempted", the place only once input was read, "in `%'" (and `/=', `%=')
# for the others, N the operator's line. A constant `1/0` is gawk's PARSE-time error (it folds constants), exit 1. It was
# "awk: unsupported: syntax: division by zero" and the program went to a PATH awk (awk_divzero_once).
# Expected: gawk 5.1 via WSL.
cd "${TMPDIR:-${TEMP:-/tmp}}" || exit 1
d="awk-divzero-$$"; mkdir -p "$d" && cd "$d" || exit 1
p() { printf '%s\n' "--- $1"; }
p "after input: the place, fatal, 2";       echo x | awk 'END { n=0; print 1/n }' 2>&1; echo "rc=$?"
p "output before it";                       echo x | awk '{ print "before" } END { n=0; print 1/n }' 2>&1; echo "rc=$?"
p "in BEGIN: no place";                     awk 'BEGIN { n=0; print 1/n }' 2>&1; echo "rc=$?"
p "% and the assignments";                  printf 'a\nb\n' | awk 'NR==2 { n=0; x = 5 % n }' 2>&1; echo x | awk '{ n=0; n /= 0 }' 2>&1; echo x | awk '{ n=1; n %= 0 }' 2>&1
p "a file's name";                          printf 'a\n' > dz.txt; awk '{ n=0; print 1/n }' dz.txt 2>&1
p "the operator's line";                    echo x | awk '
{
  n = 0
  print 2 / n
}' 2>&1
p "empty input, END: no place";             printf '' | awk 'END { n=0; print 1/n }' 2>&1; echo "rc=$?"
p "a field over a constant 0: run time";    echo 6 | awk '{ print $1/0 }' 2>&1; echo "rc=$?"
p "a constant 1/0: parse time, 1";          awk 'BEGIN { print 1/0 }' 2>&1; echo "rc=$?"; awk 'BEGIN { print 7 % 0 }' 2>&1; echo "rc=$?"
awk 'BEGIN { x = 1
y = 2 / 0 }' 2>&1; echo "rc=$?"
p "no zero, no error";                      echo x | awk '{ print 7/2, 7%2; n = 9; n /= 3; m = 9; m %= 4; print n, m }'
cd .. && rm -rf "$d"
