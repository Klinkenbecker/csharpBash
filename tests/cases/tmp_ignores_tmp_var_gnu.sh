# 2026-11-16 (facts-8a's report): /tmp does not follow TMP. A shell C#Bash starts gets its variables as typed, so
# `TMP=/tmp/x bash -c 'cat /tmp/f'` gave the child TMP=/tmp/x, which Windows' GetTempPath read as a folder on the
# current drive: the child's /tmp was C:/tmp/x, and its mktemp and process substitution files went there too. Now /tmp
# comes from TMP or TEMP only when it is a full Windows path (until 940: since then a C#Bash child inherits its parent's one
# temp folder whatever TMP says, see temp_one_place). Expected = GNU bash 5.1 via WSL.
d=$(mktemp -d /tmp/tmpvar.XXXXXX) && cd "$d" || exit 1
n=${d##*/}
echo hello > f.txt
echo "control: $("$BASH" -c "cat /tmp/$n/f.txt" 2>&1)"
echo "TMP=/tmp/x: $(TMP=/tmp/x "$BASH" -c "cat /tmp/$n/f.txt" 2>&1)"
# the child's $TMP is no longer checked here: GNU's child shows `/tmp/x` as typed; C#Bash's shows the one temp folder
# (RULED 2026-11-24, the architect: one view of TMP and TEMP, "Yes, fix all three"; checked in temp_one_place)
echo "mktemp: $(TMP=/tmp/x "$BASH" -c 'f=$(mktemp) && [ -f "$f" ] && echo made && rm -f "$f"' 2>&1)"
echo "a process substitution: $(TMP=/tmp/x "$BASH" -c 'cat <(echo read)' 2>&1)"
cd / && rm -rf "$d"
