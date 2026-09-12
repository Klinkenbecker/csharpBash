# P4: date -d/-u/-I/-R, uname family, id/whoami/nproc/printenv/tty/arch, strict options.
date -u -d "2024-03-05 14:07:09" "+%F %T %s"
date -u -d "2024-03-05 +3 days" +%F
date -u -d "2024-03-05 01:02:03 +0100" +%T
date -u -d "Mar 5 2024" +%F
date -u -d "5 Mar 2024 10:00" "+%F %T"
date -u -d "2024-03-05T14:07:09Z" +%T
date -u -d "2024-03-05 -1 month" +%F
date -u -d "20240305" +%F
date -u -d "2024-03-05 next day" +%F
date -u -d "@1709676429" -Iseconds
date -u -d "2024-03-05" -Ihours
date -u -Idate -d "2024-03-05 01:02:03"
date -u -d "Tue, 05 Mar 2024 09:02:03 +0000" +%s
date -u -d "2024-03-05 01:02:03" -R
date -u --iso-8601=seconds -d "2024-03-05 01:02:03"
date -u --rfc-3339=seconds -d "2024-03-05 01:02:03"
date -u -d "2024-03-05 9:30 pm" +%T
date -u -d "12/25/2024" +%F
date -u -d "2024-03-05 12:00" "+%-d/%-m %_H %^a %5Y %j %u %w %e|%k|%l %p %P %C %y %D %R"
date -u -d "2024-12-30" "+%V %G %U %W"
date -d bogus 2>&1; echo "rc=$?"
date +%Y | grep -cE '^[0-9]{4}$'
date -u | grep -c ' UTC '
out=$(BASH_COREUTILS=builtin date -s "2020-01-01" 2>&1); echo "rc=$?"; echo "$out" | head -1
out=$(BASH_COREUTILS=builtin date --bogus 2>&1); echo "$out" | head -1

uname -s | cut -d- -f1
uname -o
uname -m
uname -a | awk '{print $1, $(NF-1), $NF}' | cut -d- -f1,3-
arch
nproc | grep -cE '^[1-9][0-9]*$'
id -u | grep -cE '^[0-9]+$'
[ "$(id -un)" = "$(whoami)" ] && echo same-user
id | cut -c1-4
hostname | grep -c .
tty; echo "rc=$?"
tty -s; echo "rc=$?"
export ZZ_PROBE=probe
printenv ZZ_PROBE
printenv NOPE_NOT_SET; echo "rc=$?"
printenv | grep -c '^ZZ_PROBE=probe$'
