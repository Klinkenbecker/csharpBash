# 2026-11-11 (the bugs list): ls -u (access time) was accepted and ignored, as -c was until 2026-11-10. -u and -c show
# their time with -l and sort by it with -t; alone they sort by it, newest first. Three files whose access,
# modification and change orders differ; only orders and agreements are printed. Expected: GNU via WSL. The access
# times are set LAST: on NTFS setting a time through a handle counts as an access (Git Bash's touch -m does the same).
d=$(mktemp -d) && cd "$d" || exit 1
for f in a b c; do printf '%s\n' "$f" > "$f"; done
touch -m -d '2021-01-01' a; touch -m -d '2021-01-03' b; touch -m -d '2021-01-02' c
touch -a -d '2020-01-03' a; touch -a -d '2020-01-01' b; touch -a -d '2020-01-02' c
echo "ls -tu: $(ls -tu | tr '\n' ' ')"
echo "ls -u:  $(ls -u | tr '\n' ' ')"
echo "ls -t:  $(ls -t | tr '\n' ' ')"
echo "ls -l shows %Y:  $([ "$(ls -l --time-style=+%s a | tr -s ' ' | cut -d' ' -f6)" = "$(stat -c %Y a)" ] && echo yes || echo no)"
echo "--time=atime is -u: $([ "$(ls -t --time=atime | tr '\n' ' ')" = "$(ls -tu | tr '\n' ' ')" ] && echo yes || echo no)"
cd / && rm -rf "$d"
