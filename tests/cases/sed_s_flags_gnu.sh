# 2026-10-30 (koliada-net-forms-cb's report): an `s` command's flags are read as GNU reads them: blanks among them are
# skipped (`s/a/b/ p` is p), and an unknown one is "unknown option to `s'" at its position, status 1. The flag loop
# stopped at a blank, so `s#..#..#31 exclusion -->...` ended at "31" and took "exclusion" as the next command, `e`,
# which is not available: the whole command went to PATH's old sed.exe ("invalid option -- i", exit 4, the file
# unchanged but the error not sed's). Errors found while parsing carry their position (it was always char 0); those
# found after it (unmatched `{`) are char 0 in GNU too. Expected: GNU sed 4.8 via WSL, LC_ALL=C.
export LC_ALL=C
cd "${TMPDIR:-${TEMP:-/tmp}}" || exit 1
d="sed-s-flags-$$"; mkdir -p "$d" && cd "$d" || exit 1
p() { printf '%s\n' "--- $1"; }
err() { [ -s err ] && sed 's/^/err: /' err; rm -f err; }
printf 'x\n</Project>\n' > r5.txt
p "an unknown flag after e: sed's own error, the file unchanged"
sed -i 's#^</Project>#  <X/><!-- #31 exclusion -->\n</Project>#' r5.txt 2>err; echo "rc=$?"; cat r5.txt; err
p "an unknown flag";              echo a | sed 's/a/b/q' 2>err; echo "rc=$?"; err
p "blanks among the flags";       echo a | sed -n 's/a/b/ p'; echo a | sed 's/a/b/ g ; s/b/c/'
p "a number and g";               echo aaa | sed 's/a/b/2g'
p "an unknown command";           echo a | sed 'p;k' 2>err; echo "rc=$?"; err
p "y's lengths";                  echo a | sed 'y/ab/c/' 2>err; echo "rc=$?"; err
p "an empty regex with I";        echo a | sed 's//x/I' 2>err; echo "rc=$?"; err
p "unmatched {";                  echo a | sed '{p' 2>err; echo "rc=$?"; err
cd .. && rm -rf "$d"
