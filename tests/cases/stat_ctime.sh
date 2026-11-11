# 2026-11-10 (git-b3): the change time was the modification time everywhere: stat %Z/%z, ls -c and find -ctime/-cmin/
# -newerct. It is NTFS's change time now, which `touch -m` to the past moves to now. The reference is MSYS's stat on the
# same file (Linux ctime has no Windows twin), through cases/msys_pin.inc. Authored (Windows-specific).
. cases/msys_pin.inc
d=$(mktemp -d) && cd "$d" || exit 1
printf 'x\n' > f; touch -m -d @1767225600 f
same() { [ "$1" = "$2" ] && echo yes || echo "no [$1] [$2]"; }
echo "stat %Z as MSYS stat: $(same "$(stat -c %Z f)" "$("$MSYS_BIN/stat.exe" -c %Z f)")"
echo "stat %z as MSYS stat: $(same "$(stat -c %z f)" "$("$MSYS_BIN/stat.exe" -c %z f)")"
echo "%Z is not %Y after touch -m: $([ "$(stat -c %Z f)" != "$(stat -c %Y f)" ] && echo yes || echo no)"
echo "ls -lc shows %Z: $(same "$(ls -lc --time-style=+%s f | tr -s ' ' | cut -d' ' -f6)" "$(stat -c %Z f)")"
echo "ls -l shows %Y: $(same "$(ls -l --time-style=+%s f | tr -s ' ' | cut -d' ' -f6)" "$(stat -c %Y f)")"
echo "find -cmin -5: $(find . -maxdepth 1 -name f -cmin -5)  find -mmin -5: $(find . -maxdepth 1 -name f -mmin -5 | wc -l)"
echo "find -newerct yesterday: $(find . -maxdepth 1 -name f -newerct yesterday)"
cd / && rm -rf "$d"
