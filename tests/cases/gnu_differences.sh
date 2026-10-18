# 2026-10-17: smaller differences from GNU bash, fixed together: `source` searches PATH before the current directory;
# `hash`/`type -a` refuse a PATH match that cannot run and `type -a` lists every runnable match; `mktemp x.XXXX` works
# in the current directory and prints the name as built; `readlink -f` prints the shell form; a path through a file
# is "Not a directory" and a directory "Is a directory" (126); `s1.`/`./s1.` find nothing (Win32 drops the dot);
# `find -execdir` runs from the file's directory with ./name; `exec 5<> f` opens fd 5 for `read -u 5`; `hash` of a
# match in a relative PATH entry (it crashed the shell). Expected: GNU bash 5.1 (WSL), byte-identical.
exec 2>&1
d=$(mktemp -d) && cd "$d" || exit 1
case "$PATH" in *\;*) sep=';' ;; *) sep=':' ;; esac
m() { sed "s|$d|D|g; s|^.*line [0-9]*: ||"; }
mkdir srcp p1 sub; echo 'echo FROM-PATH' > srcp/lib1; echo 'echo FROM-CWD' > lib1; echo 'echo ONLY-CWD' > lib2
echo "== 1 source"; ( PATH="$d/srcp$sep$PATH"; . lib1; . lib2; source ./lib1 ) 2>&1 | m
printf 'data\n' > p1/data
echo "== 2 non-runnable only"; ( PATH="$d/p1$sep$PATH"; hash data; echo "hash rc=$?"; type data; echo "type rc=$?"; type -a data; echo "type -a rc=$?"; type -P data; echo "type -P rc=$?" ) 2>&1 | m
echo "== 3 mktemp"; t=$(mktemp zz.XXXX); echo "printed: ${t%.*}.XXXX in-cwd: $([ -f "$d/${t##*/}" ] && echo yes || echo no) relative: $(case $t in /*|?:*) echo no;; *) echo yes;; esac)"
echo "== 4 readlink -f"; touch rf; readlink -f rf | m; readlink -f sub/../rf | m
printf '#!/bin/bash\necho s\n' > sub/s.sh; chmod +x sub/s.sh
echo "== 5 not a dir / is a dir"; sub/s.sh/x 2>&1 | m; echo "rc=${PIPESTATUS[0]}"; ./sub 2>&1 | m; echo "rc=${PIPESTATUS[0]}"
mkdir p2; printf '#!/bin/bash\necho s1-ran\n' > p2/s1; chmod +x p2/s1
echo "== 6 trailing dot"; ( PATH="$d/p2$sep$PATH"; s1. 2>&1 | m; echo "rc=${PIPESTATUS[0]}"; cp p2/s1 .; ./s1. 2>&1 | m; echo "rc=${PIPESTATUS[0]}" )
echo "== 7 find -execdir"; mkdir -p fx/deep; touch fx/deep/f1; find fx -name f1 -execdir echo {} \; ; find fx -name f1 -execdir pwd \; | m
echo "== 8 exec 5<>"; printf 'line1\nline2\n' > rw; exec 5<> rw; read -u 5 l; echo "read: [$l]"; exec 5>&-
mkdir p3 p4; printf '#!/bin/bash\n' > p3/zzm; cp p3/zzm p4/zzm; chmod +x p3/zzm p4/zzm
echo "== 9 type -a, two matches"; ( PATH="$d/p3$sep$d/p4$sep$PATH"; type -a zzm | m )
mkdir rel; printf '#!/bin/bash\necho rel-ran\n' > rel/zzrel; chmod +x rel/zzrel
echo "== 10 hash of a relative PATH entry's match"; ( PATH="rel$sep$PATH"; hash zzrel; echo "hash rc=$?"; zzrel )
touch fx/deep/f2
echo "== 11 find -execdir +"; find fx -name 'f[12]' -execdir echo {} + | tr ' ' '\n' | sort
cd / && rm -rf "$d"
