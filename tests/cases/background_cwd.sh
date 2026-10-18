# 2026-10-17: a background job works in its OWN directory, as bash. Once the parent had cd'd, the job's relative
# paths resolved against the parent's (the process cwd): `( …; rm -f victim ) & cd ../r2` deleted r2/victim.
# Also (red team): a relative PATH entry, `cd -` with a relative OLDPWD; and what the fix must not change: `ln -s`
# to a directory, `cp -v`/`ln -v` printing paths as typed. The job waits for the parent's cd through a flag file,
# not a sleep. Expected: GNU bash 5.1 (WSL), byte-identical.
d=$(mktemp -d) && cd "$d" || exit 1
case "$PATH" in *\;*) sep=';' ;; *) sep=':' ;; esac
mkdir -p r1/sub r2/sub r1/bin r2/bin
touch r1/victim r2/victim r1/sub/victim2 r2/sub/victim2 r1/a.tmp r1/b.tmp r2/c.tmp
echo 'echo "the r1 script"' > r1/s.sh; echo 'echo "the r2 script"' > r2/s.sh
printf '#!/bin/bash\necho "the r1 zzex, 0=$0"\n' > r1/bin/zzex; printf '#!/bin/bash\necho "the r2 zzex, 0=$0"\n' > r2/bin/zzex
printf '#!/bin/bash\necho "zzone ran"\n' > r1/bin/zzone; printf '#!/bin/bash\necho "zztwo ran"\n' > r2/bin/zztwo
chmod +x r1/s.sh r2/s.sh r1/bin/zzex r2/bin/zzex r1/bin/zzone r2/bin/zztwo
cp -v r1/a.tmp r1/sub/
cd r1
( while [ ! -e ../go ]; do :; done
  echo w > out.txt; rm -f victim
  echo "glob: $(echo *.tmp)"; echo "pipeline: $(ls *.tmp | wc -l | tr -d ' ')"
  [ -f a.tmp ] && echo "test -f a.tmp: yes" || echo "test -f a.tmp: no"
  ./s.sh
  PATH="bin$sep$PATH" zzex; PATH="bin$sep$PATH" zzone; PATH="bin$sep$PATH" zztwo 2>&1 | sed "s/^.*line [0-9]*: //"
  cp -v b.tmp sub/
  ln -sv a.tmp lc
  mkdir -p sub/d && touch sub/d/inside && ln -s d sub/l && echo "ln -s to a directory: $(ls sub/l/)"
  OLDPWD=sub; cd - > /dev/null; rm -f victim2; cd ..
  cd sub && touch made ) &
cd ../r2
touch ../go
wait
cd ..
for x in r1 r2 r1/sub r2/sub; do echo "$x: $(ls "$x" | tr '\n' ' ')"; done
cd / && rm -rf "$d"
