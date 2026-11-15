# 2026-11-15 (facts-8a, bugs list): a glob rooted at /tmp never expanded (`echo /tmp/sweep*` stayed literal, `ls
# /tmp/x*` printed nothing): the walk began at the drive's root and looked for a folder named tmp, while a plain /tmp
# path is the temporary folder. Files of its own, by a unique name. Expected = GNU bash 5.1 via WSL.
n=globtmp$$x$RANDOM
touch "/tmp/$n-a.txt" "/tmp/$n-b.txt"; mkdir "/tmp/$n-d"; touch "/tmp/$n-d/inner.txt"
show() { for f in "$@"; do printf '%s ' "${f//$n/N}"; done; echo; }
show /tmp/$n*
show /tmp/$n-*.txt
show /tmp/$n*/
show /tmp/$n-d/*.txt
show /tmp/$n-zzz*
for f in /tmp/$n-?.txt; do [ -f "$f" ] && echo "file ${f//$n/N}"; done
ls -d /tmp/$n-* | sed "s/$n/N/"
cd /tmp && show $n-*.txt
rm -r "/tmp/$n-a.txt" "/tmp/$n-b.txt" "/tmp/$n-d"
