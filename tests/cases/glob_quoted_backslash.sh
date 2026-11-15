# 2026-11-15 (found checking a workaround for facts-8a; bugs list): a QUOTED backslash in a glob's path is a separator,
# as an unquoted one is: `"$TMP"/x*` (TMP holds F:\TEMP) gave a mangled F:/\TEMP/x... Windows-only: expected authored
# from MSYS (Git Bash 2.28: `"$p"/x*` gives F:\TEMP/x-1.txt, the prefix as typed); a pattern all in backslashes expands
# here (MSYS leaves it literal), as an unquoted one already did.
d=$(mktemp -d) && cd "$d" || exit 1
touch x-1.txt x-2.txt
w=$(pwd -W 2>/dev/null || pwd); w=${w//\//\\}   # the folder with backslashes: C:\...\tmp.X
show() { for f in "$@"; do printf '%s ' "${f//"$w"/W}"; done; echo; }
show "$w"/x*
show $w/x*
show "$w\\"x-1*
show "$w"/zz*
cd / && rm -rf "$d"
