# 2026-10-19 (red team F7): find -execdir takes a starting point as written (`.` runs in `.` as `./.`, `proj/` in `.`
# as `./proj/`; `.` ran in the parent, `proj/` inside proj), and refuses, as GNU, when PATH holds the current
# directory or a relative entry (a program planted beside a file ran). Expected: GNU findutils 4.8 via WSL; the
# refusals compared on their first 90 characters (the rest names this PATH's separator).
export LC_ALL=C
case "$PATH" in *\;*) sep=';' ;; *) sep=':' ;; esac
clean=""; IFS=$sep read -r -a ents <<< "$PATH"
for e in "${ents[@]}"; do case "$e" in /*|[A-Za-z]:[/\\]*) clean="${clean:+$clean$sep}$e" ;; esac; done
PATH=$clean
d=$(mktemp -d) && cd "$d" || exit 1
mkdir -p proj/sub; touch proj/f proj/sub/g
cd proj
echo "1: $(find . -maxdepth 0 -execdir pwd \; | sed "s#$d#D#")"
echo "2: $(find . -maxdepth 0 -execdir echo {} \;)"
echo "3: $(find . -name g -execdir echo {} \;) $(find . -name g -execdir pwd \; | sed "s#$d#D#")"
cd ..
echo "4: $(find proj/ -maxdepth 0 -execdir echo {} \;) $(find proj/ -maxdepth 0 -execdir pwd \; | sed "s#$d#D#")"
echo "5: $(find proj -maxdepth 0 -execdir echo {} \;) $(find proj -maxdepth 0 -execdir pwd \; | sed "s#$d#D#")"
echo "6: $(find proj -name g -execdir echo {} \;) $(find proj -name g -execdir pwd \; | sed "s#$d#D#")"
echo "7: $(find proj/sub/g -execdir echo {} \;) $(find proj/sub/g -execdir pwd \; | sed "s#$d#D#")"
echo "8: $(find ./proj/sub -maxdepth 0 -execdir echo {} \;)"
echo "9: $(find "$d/proj" -maxdepth 0 -execdir echo {} \;) $(find "$d/proj" -maxdepth 0 -execdir pwd \; | sed "s#$d#D#")"
echo "10: $(find proj -mindepth 1 -execdir echo {} + | sort | tr '\n' ' ')"
for P in ".$sep$PATH" "rel$sep$PATH" "$sep$PATH" "$PATH$sep" "$PATH$sep./x"; do
  out=$(PATH="$P" find proj -name f -execdir echo {} \; 2>&1); rc=$?
  echo "rc=$rc ${out:0:90}"
done
cd / && rm -rf "$d"
