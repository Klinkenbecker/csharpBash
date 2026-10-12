# 2026-10-12: jobspecs as bash 5.1's get_job_spec: `%name` (a command starting with name, case kept),
# `%?text` (a command containing text, case ignored), a bare `%` (the current job); two matches are
# "ambiguous job spec". A match on a pipeline's later command is always ambiguous (bash meets it twice).
# C#Bash found none of the three ("no such job"), in jobs, kill, wait, disown and jobs -x alike.
# Expected output: GNU bash via WSL.
f=$(mktemp)
x() { "$@" > "$f" 2>&1; local rc=$?; sed "s/^.*line [0-9]*: //; s/$$/SHELL/g" "$f"; echo "rc=$rc"; }
sleep 30 & sleep 31 | cat & sleep 32 &
x jobs %sleep
x jobs '%sleep 31'
x jobs %?32
x jobs %?31
x jobs %?CAT
x jobs %SLEEP
x jobs %
x jobs %% %+ %-
x jobs %nosuch
x jobs -x echo '%sleep 32' %?32 %?cat
x disown '%sleep 30'
x jobs
x kill '%?32'
kill %2 2>/dev/null; wait 2>/dev/null
rm -f "$f"
