# 2026-11-07 (the architect's ruling on Claude's recommendation): a path whose final name is a Windows device name
# (nul, con, aux.txt, com1) and which has a folder part (d/nul, ./nul) is that FILE, as under Git Bash; a bare name
# (nul, > nul, 2>nul) stays the device, so it never leaves a file Windows' own tools cannot delete. Every such path was
# the device: `cat d/nul` printed nothing, `cp x d/nul` wrote nowhere. Needs MSYS's cp.exe and cat.exe
# (cases/msys_pin.inc) to make and read the files. Authored (Windows-specific).
. cases/msys_pin.inc
mdir=$MSYS_BIN
check() { if [ "$2" = "$3" ]; then echo "$1 ok"; else echo "$1 BAD [$2] want [$3]"; fi; }
cd "$(mktemp -d)" || exit 1
mkdir d; echo hello > d/src; "$mdir/cp.exe" d/src d/nul
check cat-folder   "$(cat d/nul)" hello
check cat-dot      "$(cd d && cat ./nul)" hello
check redirect-in  "$(wc -c < d/nul)" 6
check test-f       "$([ -f d/nul ] && echo yes)" yes
cp d/nul d/copy;   check cp-from "$(cat d/copy)" hello
cp d/src d/aux.txt; check cp-to "$("$mdir/cat.exe" d/aux.txt)" hello
echo x > d/com1;   check redirect-out "$("$mdir/cat.exe" d/com1)" x
echo y >> d/com1;  check redirect-append "$("$mdir/cat.exe" d/com1 | tr '\n' ,)" "x,y,"
check listed       "$(ls d | tr '\n' ' ')" "aux.txt com1 copy nul src "
# a bare name stays the device: nothing is made, the file d/nul is untouched
cd d
echo y > nul; ls no-such-file 2>nul
check bare-write   "$(cat ./nul)" hello
check bare-read    "$(cat nul | wc -c)" 0
check bare-nothing "$(ls | tr '\n' ' ')" "aux.txt com1 copy nul src "
cd ..
rm -rf d; check rm "$([ -e d ] && echo left || echo gone)" gone
cd / && "$mdir/rm.exe" -rf "$OLDPWD"   # MSYS rm: a failing build leaves no litter
