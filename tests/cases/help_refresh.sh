# 2026-10-29 (the installer plan, phase 2): an earlier build's help.json, unedited, is replaced by this build's table the
# next time help reads it; an edited one is never touched; a missing one is written. rev 397's table (cases/
# help_rev397.json = `hg cat -r 397 Bash/help.json`) sent find and xargs to findutils' find_html/ folder, which is not
# online, and every profile written before rev 462 kept it (the file answers for every name it has).
# Expected: authored (C#Bash's own table and behaviour; GNU has no such file).
fx=$PWD/cases/help_rev397.json
cd "${TMPDIR:-${TEMP:-/tmp}}" || exit 1
t="help-refresh-$$"; mkdir -p "$t" && cd "$t" || exit 1
p=${PWD//\\//}
# 2026-10-31: no backslash is rewritten here any more: help's own messages named the file `C:/x\.bash\help.json` (fixed)
ask() { USERPROFILE=$PWD/$1 HOME=$PWD/$1 "$BASH" -c "help $2" 2>&1 | sed "s#$p/##g"; }

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
# 2026-10-31: rev 462's table (cases/help_rev462.json = `hg cat -r 462 Bash/help.json`), the one before dd and cksum were
# added, is an earlier build's too (EarlierHelpTables), so a profile written from it gets the new names
echo "--- rev 462's table, unedited: replaced by this build's, which has dd"
mkdir -p d/.bash; cp "${fx%397.json}462.json" d/.bash/help.json
ask d dd
cmp -s d/.bash/help.json a/.bash/help.json && echo "d: now this build's table"
# 2026-11-24 (KNOWN_ISSUES, found 2026-11-17): the tables of revs 553, 645 and 665 (cases/help_revN.json = `hg cat -r N
# Bash/help.json`) were never added to EarlierHelpTables, so a profile written from one kept it; each is replaced now
for r in 553 645 665; do
	echo "--- rev $r's table, unedited: replaced by this build's"
	mkdir -p "e$r/.bash"; cp "${fx%397.json}$r.json" "e$r/.bash/help.json"
	ask "e$r" dd
	cmp -s "e$r/.bash/help.json" a/.bash/help.json && echo "e$r: now this build's table"
done
cd .. && rm -rf "$t"
