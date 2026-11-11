# 2026-11-10 (c-parser-92): awk's ARGC and ARGV were empty, so `FILENAME==ARGV[1]` never matched (a two-file compare
# read file 1 as empty). ARGV[0] is "awk", then each operand in order, assignments included; ARGC counts them. The
# files are read from ARGV as it stands, so a BEGIN that empties, deletes or adds an element changes what is read
# (POSIX). Expected: gawk 5.1 via WSL.
d=$(mktemp -d) && cd "$d" || exit 1
printf 'x\n' > fa.txt; printf 'y\n' > fb.txt
awk '{print FILENAME, "|", ARGV[1], "|", (FILENAME==ARGV[1])}' fa.txt fb.txt
awk 'BEGIN{print ARGC; for(i=0;i<ARGC;i++) print i, ARGV[i]}' fa.txt fb.txt
awk -v x=1 'BEGIN{print ARGC, ARGV[1], ARGV[2]} {print v, $0}' v=set fa.txt
awk 'BEGIN{ARGV[1]=""} {print "empty skipped:", FILENAME}' fa.txt fb.txt
awk 'BEGIN{delete ARGV[1]} {print "deleted skipped:", FILENAME}' fa.txt fb.txt
awk 'BEGIN{ARGV[ARGC++]="fb.txt"} {print "added:", FILENAME, $0}' fa.txt
awk 'FILENAME==ARGV[1]{a[$0]; next} !($0 in a){print "only in 2:", $0}' fa.txt fb.txt
cd / && rm -rf "$d"
