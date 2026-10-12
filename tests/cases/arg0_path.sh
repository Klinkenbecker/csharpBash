# 2026-10-11: `$0`, when it is the interpreter's own path as its launcher passed it (argv[0]), is in the
# shell's forward-slash form, as `$BASH` is; it was the launcher's native path, so every `-c` error prefix
# read `F:\...\bash.exe:` under Claude Code. A `-c` name argument stays as typed. A program found through
# PATHEXT gets its on-disk name as argv[0] (C#Bash passed `bash.EXE`; Git Bash passes the on-disk name).
# Authored expectations (Windows-specific); the interpreter under test is launched BY NAME through PATH.
dir=$(dirname "$BASH")
name=$(basename "$BASH"); name=${name%.*}
export PATH="$dir;$PATH"
out=$("$name" -c 'echo "$0"')
echo "1 by name: ${out//\\/<BACKSLASH>}" | sed "s#^1 by name: .*/#1 by name: .../#"
err=$("$name" -c 'x(' 2>&1 | head -1)
case $err in *\\*) echo "2 error prefix: backslash" ;; *) echo "2 error prefix: forward slashes" ;; esac
in=$("$name" -c "\"$name\" -c 'echo \"\$0\"'")
echo "3 a child of it: ${in##*/}"
"$name" -c 'echo "4 name argument: $0"' 'C:\as\typed'
