# 2026-11-10 (git-b3): printf's %b decoded \0NNN but not \NNN (1-3 octal digits): '\101' stayed literal (GNU: A), so a
# generator using printf -v NAME '%b' '\100N' produced the literal text. A value past 255 keeps its low byte (\400 is
# NUL). echo -e takes only \0NNN, in both. Expected: GNU bash via WSL.
printf '%b|' '\101' '\0101' '\101x\101' '\1' '\12' '\777' '\08' | od -An -c
printf -v v '%b' '\101x\101'; echo "v=$v"
echo -e 'e:\101|\0101'
printf '\101\400|' | od -An -c
