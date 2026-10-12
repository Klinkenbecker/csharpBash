# 2026-10-12: pushd / popd / dirs as bash 5.1's pushd.def. `+N` / `-N` rotate (pushd), remove (popd) and
# show (dirs) entry N from the left / the right of `dirs`; they were refused. `-n` means "no cd" (pushd -n
# DIR saves DIR as entry 1 as written; popd -n removes entry 1; both still print the stack), not "print
# nothing". A `-n` rotation drops the entry it would have changed to, as bash does. dirs -p / -l work;
# `dirs -lv` is an invalid number; `~` only at a `/` boundary of a HOME longer than `/`; cd's errors under
# pushd say `pushd:`. Not covered: DIRSTACK (C#Bash has none). Expected output: GNU bash via WSL.
d=$(mktemp -d) && cd "$d" && mkdir a b c x && HOME=$d || exit 1
w() { echo "  pwd=${PWD/#"$d"/\~} stack: $(dirs -v | tr -s ' ' | tr '\n' '|')"; }
r() { echo "== ${*//"$d"/D}"; "$@" 2>"$d/.err"; local rc=$?; sed 's/^.*line [0-9]*: //' "$d/.err"; echo "  rc=$rc"; w; }
r pushd a; r pushd ../b; r pushd ../c
r dirs -p; r dirs -lv; r dirs -pv; r dirs -x; r dirs foo
dirs -l | sed "s#$d#D#g"; dirs -v -l | sed "s#$d#D#g"; dirs -l +3 | sed "s#$d#D#g"
r dirs +0; r dirs +2; r dirs -0; r dirs -1; r dirs -v +1; r dirs -v -1; r dirs -v -0; r dirs +9; r dirs -9; r dirs +1 +2
r pushd +1; r pushd -0; r pushd -n ~/x; r pushd -n +2; r pushd -n -1; r pushd +9; r pushd +x; r pushd -x; r pushd -n; r pushd
r pushd +0; r pushd -n +0; r pushd nonexist; r pushd a b
r popd +1; r popd -0; r popd -n; r popd -n +1; r popd +0; r popd +9; r popd x; r popd -x; r popd
r pushd -n; r pushd; r pushd +0; r pushd +1
r pushd -n b; r dirs; r popd +1
dirs | od -c | sed -n 1p; dirs -v | od -c | sed -n 1p
HOME=$d/; dirs | sed "s#$d#D#g"; HOME=${d%/*}; dirs | sed "s#${d##*/}#T#g"; HOME=/; dirs | sed "s#$d#D#g"
cd / && rm -rf "$d"
