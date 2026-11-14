# 2026-11-12 (facts-8a's report): a folder rename on one volume is all or nothing, as GNU's and Git Bash's mv. While
# another process had its cwd inside the folder, mv fell back to copy-then-delete: every entry moved, then the held
# folder itself failed, so it said "cannot move", rc 1, with the tree emptied into the target. Now the rename is
# refused, rc 1, and nothing moves; once released it succeeds. Authored (Windows: cmd holds the folder as its cwd).
d=$(mktemp -d) && cd "$d" || exit 1
mkdir -p src/sub ctl; echo a > src/a.txt; echo b > src/sub/b.txt
mv ctl ctl2; echo "control rc=$? $(ls | tr '\n' ' ')"
cmd.exe //c "cd /d $(printf '%s' "$d/src" | tr / '\\') && ping -n 4 127.0.0.1 >nul" &
sleep 1
mv src dst 2> err; echo "held rc=$?"
echo "message: $(grep -c "^mv: cannot move 'src' to 'dst': " err)"
echo "src: $(ls -A src | tr '\n' ' ')"; echo "dst exists: $([ -e dst ] && echo yes || echo no)"
wait
mv src dst; echo "released rc=$? dst: $(ls -A dst | tr '\n' ' ')$(cat dst/sub/b.txt)"
cd / && rm -rf "$d"
