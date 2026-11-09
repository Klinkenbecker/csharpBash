# 2026-11-07 (guy-bf): rm removes a file named with a Windows device name (nul, con, aux.txt, com1) or ending in a dot,
# as MSYS's rm (GNU coreutils) does, through an extended path; a failure on one entry is reported with that entry's path
# and the rest of the tree is still removed, exit 1 at the end, as GNU. rm -rf on a tree holding an MSYS-made dev/nul
# exited 0, said nothing and left almost everything (guy-bf: 8,577 entries of an old PortableGit). Needs MSYS's
# touch.exe (cases/msys_pin.inc) to make the names. Authored (Windows-specific), MSYS rm.exe the reference.
. cases/msys_pin.inc
t=$MSYS_BIN/touch.exe
check() { if [ "$2" = "$3" ]; then echo "$1 ok"; else echo "$1 BAD [$2] want [$3]"; fi; }
cd "$(mktemp -d)" || exit 1
# 1. a tree with reserved names: all of it goes, exit 0
mkdir -p r/a r/b r/c
"$t" r/b/nul r/b/con r/b/aux.txt r/b/com1 'r/b/dot.'
echo x > r/a/f; echo x > r/c/f
check made "$(ls r/b | wc -l)" 5
rm -rf r; rc=$?
check tree-rc "$rc" 0
check tree-gone "$([ -e r ] && echo left || echo gone)" gone
# 2. a reserved name as an operand
mkdir s; "$t" s/nul
rm -f s/nul; rc=$?
check operand-rc "$rc" 0
check operand-gone "$(ls s | wc -l)" 0
rmdir s
# 3. one entry cannot be removed (a running program's image): the rest still goes, that entry is named, exit 1
mkdir -p u/a u/b u/c; echo x > u/a/f; echo x > u/c/f
cp "$(cygpath -u "$SYSTEMROOT" 2>/dev/null || echo "$SYSTEMROOT")/System32/PING.EXE" u/b/p.exe 2>/dev/null || cp C:/Windows/System32/PING.EXE u/b/p.exe
u/b/p.exe -n 4 127.0.0.1 > /dev/null 2>&1 &
pid=$!
sleep 1
msg=$(rm -rf u 2>&1); rc=$?
check held-rc "$rc" 1
check held-names-entry "$(case $msg in *"u/b/p.exe"*) echo yes;; *) echo "no: $msg";; esac)" yes
check held-rest-gone "$([ -e u/a ] || [ -e u/c ] && echo left || echo gone)" gone
check held-kept "$([ -e u/b/p.exe ] && echo kept || echo gone)" kept
wait $pid
rm -rf u
# 4. -f and a name that is not there: silent, exit 0
rm -f does-not-exist; check missing-f "$?" 0
cd / && "$(dirname "$t")/rm.exe" -rf "$OLDPWD"   # MSYS rm: a failing build leaves no litter
