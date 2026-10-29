# 2026-10-29 (the installer plan, phase 2): an earlier build's help.json, unedited, is replaced by this build's table the
# next time help reads it; an edited one is never touched; a missing one is written. rev 397's table (cases/
# help_rev397.json = `hg cat -r 397 Bash/help.json`) sent find and xargs to findutils' find_html/ folder, which is not
# online, and every profile written before rev 462 kept it (the file answers for every name it has).
# Expected: authored (C#Bash's own table and behaviour; GNU has no such file).
fx=$PWD/cases/help_rev397.json
cd "${TMPDIR:-${TEMP:-/tmp}}" || exit 1
t="help-refresh-$$"; mkdir -p "$t" && cd "$t" || exit 1
p=${PWD//\\//}
ask() { USERPROFILE=$PWD/$1 HOME=$PWD/$1 "$BASH" -c "help $2" 2>&1 | sed "s#\\\\#/#g; s#$p/##g"; }

echo "--- a profile with no table: written from this build's, and it answers"
mkdir a; ask a xargs
cmp -s a/.bash/help.json "$fx" && echo "a: rev 397's table" || echo "a: not rev 397's table"
echo "--- rev 397's table, unedited: replaced by this build's"
mkdir -p b/.bash; cp "$fx" b/.bash/help.json
ask b xargs
cmp -s b/.bash/help.json a/.bash/help.json && echo "b: now this build's table"
echo "--- asked again: nothing left to replace"
ask b find
echo "--- rev 397's table with one name added by hand: left alone, and it answers"
mkdir -p c/.bash
{ head -1 "$fx"; printf '  "mytool": ["https://example.org/mytool"],\n'; tail -n +2 "$fx"; } > c/.bash/help.json
cp c/.bash/help.json c.copy
ask c xargs
ask c mytool
cmp -s c/.bash/help.json c.copy && echo "c: untouched"
cd .. && rm -rf "$t"
