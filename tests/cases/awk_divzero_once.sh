# 2026-11-01 (found by Claude): a division by zero never hands the program to a PATH awk. It was filed as an
# unsupported construct, so with an awk on PATH the whole program ran again there after this run's output ("before"
# twice) and the exit status was the other awk's. A stand-in awk on PATH must not run. Authored (C#Bash only: under
# GNU the stand-in would be the awk itself); the message is gawk's (awk_divzero_gnu).
cd "${TMPDIR:-${TEMP:-/tmp}}" || exit 1
d="awk-divzero-once-$$"; mkdir -p "$d" || exit 1
printf '#!/bin/sh\necho "a PATH awk ran the program again"\n' > "$d/awk"; chmod +x "$d/awk"
PATH="$PWD/$d:$PATH"; hash -r
echo x | awk '{ print "before" } END { n=0; print 1/n }' 2>&1; echo "status $?"
rm -rf "$d"
