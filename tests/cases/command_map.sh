# 2026-10-16: the persistent command map (DECISIONS "RATIFIED: the tool-lookup plan", "only `hash` records and
# obeys misses"). Lookups are kept across processes per PATH value. Hits: every lookup. Misses: `hash name` alone
# records one and obeys one an EARLIER process recorded, saying so; the process that recorded it searches again;
# running a program, `command -v` and `type` search past it, and a found name replaces it. `hash -r` / `hash -d`
# forget; a PATH with a relative entry is not kept; shells writing at once leave a readable file. And BASH_PATH: a
# launcher's PATH for the shell, taken once and removed, so a nested shell keeps its parent's PATH.
# Authored expectations (C#Bash-specific: bash has neither).
d=$(mktemp -d) && cd "$d" || exit 1
export HOME="$d/home" d
mkdir A B
export PATH="$d/A;$PATH"
run() { "$BASH" -c "$1" 2>&1 | sed 's/^.*: line [0-9]*: //'; }
echo "== hash records a miss"; run 'hash zzm; echo rc=$?'
printf '@echo zzm-ran\r\n' > A/zzm.cmd
echo "== a later hash obeys it, and says so"; run 'hash zzm; echo rc=$?'
echo "== running it searches past the miss; the hit replaces it"; run 'zzm; hash zzm; echo rc=$?'
echo "== a remembered hit"; run 'hash zzm; echo rc=$?; type zzm | sed "s|$d|D|"'
echo "== the process that recorded a miss searches again"
run 'hash zzs; printf "@echo zzs-ran\r\n" > A/zzs.cmd; hash zzs; echo rc=$?; zzs'
echo "== command -v and type record no miss"; run 'command -v zzv || echo "command -v: none"; type zzv'
printf '@echo zzv-ran\r\n' > A/zzv.cmd; run 'hash zzv; echo rc=$?'
echo "== running a missing command records no miss"; run 'zzx; echo rc=$?'
printf '@echo zzx-ran\r\n' > A/zzx.cmd; run 'hash zzx; echo rc=$?'
echo "== a remembered program that has gone is searched for"; rm A/zzm.cmd; run 'hash zzm; echo rc=$?'
printf '@echo zzm-ran\r\n' > A/zzm.cmd
echo "== hash -d forgets one name"; run 'hash -d zzm; echo rc=$?; hash zzm; echo rc=$?'
echo "== a long-lived shell's hash -d forgets a name another shell wrote since it read the map"   # 2026-10-17
printf '@echo zzl-ran\r\n' > A/zzl.cmd
run 'hash zzm; "$BASH" -c "hash zzl"; hash -d zzl; echo rc=$?'
echo "remembered after hash -d: $(cat "$HOME"/.bash/commands/*.map | grep -c "^zzl"$'\t')"
echo "== a drive-relative name (F:x) is a path: it runs, and the map keeps nothing for it"   # 2026-10-17
printf '#!/bin/bash\necho "drive-relative ran"\n' > zzdr; dv=${d%%:*}
run "$dv:zzdr; echo rc=\$?"
echo "kept: $(cat "$HOME"/.bash/commands/*.map | grep -c "^$dv:zzdr")"
echo "== hash -r forgets everything"; rm A/zzm.cmd; run 'hash zzm'; printf '@echo zzm-ran\r\n' > A/zzm.cmd
run 'hash zzm; echo rc=$?'; run 'hash -r; hash zzm; echo rc=$?'
echo "== a PATH with a relative entry is not kept"; run 'PATH="A;$PATH"; hash zzr'; run 'PATH="A;$PATH"; hash zzr'
echo "== shells writing at once leave a readable map"
for i in 1 2 3 4 5 6 7 8; do "$BASH" -c "hash zzc$i" 2>/dev/null & done; wait
n=$(cat "$HOME"/.bash/commands/*.map | grep -c '^zzc[1-8]	$')
bad=$(for f in "$HOME"/.bash/commands/*.map; do tail -n +4 "$f" | grep -vc '	'; done | awk '{ s += $1 } END { print s + 0 }')
[ "$n" -ge 1 ] && [ "$n" -le 8 ] && echo "remembered: between 1 and 8" || echo "remembered: $n"
echo "malformed lines: $bad"
echo "== a map in use is touched when read; one unused for 30 days is pruned"
f=$(grep -l '^zzm	' "$HOME"/.bash/commands/*.map)
touch -d '2 hours ago' "$f"; run 'hash zzm'; [ -n "$(find "$f" -mmin -1)" ] && echo "in use: touched" || echo "in use: NOT touched"
touch -d '40 days ago' "$HOME/.bash/commands/0000000000000000.map"
mkdir C; run "PATH=\"$d/C;\$PATH\"; hash zzp"
[ -e "$HOME/.bash/commands/0000000000000000.map" ] && echo "unused: kept" || echo "unused: pruned"
printf '@echo zzb-ran\r\n' > B/zzb.cmd
echo "== BASH_PATH becomes the PATH, once"
( export BASH_PATH="$d/B;${SystemRoot:-${SYSTEMROOT:-C:/Windows}}/System32"; run 'zzb; [ "${PATH%%;*}" = "$d/B" ] && echo "PATH starts with it"; echo "BASH_PATH=${BASH_PATH-unset}"; printenv BASH_PATH || echo "not in the environment"' )
echo "== a nested shell keeps its parent's PATH"
printf '@echo zzn-ran\r\n' > A/zzn.cmd
printf '#!/bin/sh\nzzn\n' > s.sh
( export BASH_PATH="$d/B;${SystemRoot:-${SYSTEMROOT:-C:/Windows}}/System32"; run 'export PATH="$d/A;$PATH"; "$BASH" -c zzn; ./s.sh' )
cd / && rm -rf "$d"
