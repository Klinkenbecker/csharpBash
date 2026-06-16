paste cases/p1.txt cases/p2.txt
echo --
paste -d, cases/p1.txt cases/p2.txt
echo ==
comm cases/c1.txt cases/c2.txt
echo ==
cmp cases/p1.txt cases/p1.txt; echo "same rc=$?"
cmp cases/p1.txt cases/p2.txt; echo "diff rc=$?"
echo ==
echo hello | tee "$TEMP/bash_teed.txt"; cat "$TEMP/bash_teed.txt"
touch "$TEMP/bash_tch.txt"; [ -f "$TEMP/bash_tch.txt" ] && echo touched
mkdir -p "$TEMP/bash_rmd"; rmdir "$TEMP/bash_rmd"; [ -d "$TEMP/bash_rmd" ] && echo STILL || echo removed
