# 2026-10-19 (red team F4): an argument to a .bat or .cmd reaches it as data. Windows runs a batch file through
# cmd.exe, which parsed the whole command line again: `"&echo INJECTED` ran echo, `%OS%` expanded ("BatBadBut",
# CVE-2024-24576). Each argument is now quoted for cmd as Rust's std quotes it; a line break is refused (126).
# Authored: GNU runs no batch files.
d=$(mktemp -d) && cd "$d" || exit 1
printf '@echo [%%1] [%%2]\r\n' > t.bat
printf '@echo [%%*]\r\n' > all.cmd
./t.bat '"&echo INJECTED' plain
./t.bat '%OS%' '!PATH!'
./t.bat 'a&b|c<d>e^f' 'x y'
./t.bat '' 'tail\'
./t.bat 'C:/x/y.txt' '-o=1,2'
./all.cmd one 'two words' '3"q'
v='"&echo PWNED>pwned.txt&"'; ./t.bat "$v"
[ -e pwned.txt ] && echo "INJECTION RAN" || echo "no file written"
./t.bat "$(printf 'a\nb')" 2>/dev/null; echo "line break: rc=$?"
./t.bat "$(printf 'a\nb')" 2>&1 | grep -o 'line break to a batch file'
case "$PATH" in *\;*) sep=';' ;; *) sep=':' ;; esac
printf '@echo %%~nx0 [%%1]\r\n@exit /b 3\r\n' > zzrc.bat
PATH="$PWD$sep$PATH" zzrc 'a b'; echo "found on PATH: rc=$?"
cd / && rm -rf "$d"
