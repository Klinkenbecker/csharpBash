# 2026-11-24 (RULED, the architect: "c#Bash should have one and only one view of where TEMP and TMP are. Within c#bash
# they should both point to _THE SAME PLACE_"; "Yes, fix all three"): at start TMP, TEMP and TMPDIR all name /tmp's
# folder; a C#Bash this one starts gets that same place whatever was typed for it (bugs list 6: `TMP=/tmp/x TEMP=/tmp/y
# bash` gave the child another /tmp); any other program gets what was typed for it; a shell started by another program
# takes TMP's folder for all three; mktemp reads the shell's TMPDIR (it read the process's, so a TMPDIR set in the shell
# was ignored). 2026-11-25 (his "Yes"): a Windows path assigned to TMP, TEMP or TMPDIR is stored with forward slashes (the
# last section; red on 960). Authored (Windows-specific: the folders are Windows').
d=$(mktemp -d) && cd "$d" || exit 1
place=$(cd /tmp && pwd)
same() { [ "$1" = "$place" ] && echo same || echo "differs: $1"; }
echo "TMP $(same "$TMP"), TEMP $(same "$TEMP"), TMPDIR $(same "$TMPDIR")"
echo marker > "/tmp/temp-one-place-$$.txt"
echo "-- a child given MSYS-form TMP, TEMP and TMPDIR: the parent's place"
TMP=/tmp/x TEMP=/tmp/y TMPDIR=/tmp/z "$BASH" -c '[ "$(cd /tmp && pwd)" = "$1" ] && echo "child /tmp same"
	[ "$TMP|$TEMP|$TMPDIR" = "$1|$1|$1" ] && echo "child TMP TEMP TMPDIR same"; cat "/tmp/temp-one-place-$2.txt"' _ "$place" $$
echo "-- env -i and env -u: the same"
env -i "$BASH" -c '[ "$TMP|$TEMP|$TMPDIR" = "$1|$1|$1" ] && echo "env -i same"' _ "$place"
env -u TMP -u TEMP "$BASH" -c '[ "$TMP|$TEMP" = "$1|$1" ] && echo "env -u same"' _ "$place"
echo "-- any other program: what was typed for it"
TMP=/tmp/x cmd //c 'echo %TMP%' < /dev/null | tr -d '\r' | sed "s|$place|PLACE|"
echo "-- started by cmd with TMP, TEMP and TMPDIR all different: TMP's folder for all three"
mkdir a b c m n
printf '[ "$TMP|$TEMP|$TMPDIR" = "$1|$1|$1" ] && echo "started by cmd: all three ${TMP##*/}"\n' > start.sh
printf '@"%%B%%" "%%S%%" "%%E%%"\r\n' > start.cmd
B=$BASH S=$PWD/start.sh E=$PWD/a TMP=$PWD/a TEMP=$PWD/b TMPDIR=$PWD/c ./start.cmd < /dev/null | tr -d '\r'
echo "-- mktemp: the shell's TMPDIR, per command and exported"
f=$(TMPDIR=$PWD/m mktemp) && [ "${f%/*}" = "$PWD/m" ] && echo "per command: in m"
export TMPDIR=$PWD/n; f=$(mktemp) && [ "${f%/*}" = "$PWD/n" ] && echo "exported: in n"
echo "-- a Windows path assigned to TMP, TEMP or TMPDIR is stored with forward slashes (the architect's \"Yes\", 2026-11-25)"
export TEMP='F:\TEMP' TMP='F:\TEMP'; echo "Claude Code's export: $TMP $TEMP"
TMPDIR='\\host\share\t'; echo "UNC: $TMPDIR"
OTHER='F:\TEMP'; echo "another name, as written: $OTHER"
TMP='/tmp/x\y'; echo "not a Windows path, as written: $TMP"
rm -f "/tmp/temp-one-place-$$.txt"
cd / && rm -rf "$d"
