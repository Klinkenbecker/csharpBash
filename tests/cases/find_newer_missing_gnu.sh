# 2026-11-05 (c-parser-ac's report): find -newer with a reference file that does not exist is GNU's error before any
# walk: "find: 'X': No such file or directory", exit 1, nothing listed. It listed every file and exited 0, so a typo
# in the reference turned "nothing changed since REF" into "everything" and passed. -samefile and -newermt already
# refused; an existing reference is the positive control. Expected: GNU findutils via WSL, LC_ALL=C.
export LC_ALL=C
cd "${TMPDIR:-${TEMP:-/tmp}}" || exit 1
d="find-newer-missing-$$"; mkdir -p "$d/t" && cd "$d" || exit 1
touch t/a.txt
echo "== -newer, a missing reference"; find t -newer missing.txt -type f > out 2>&1; rc=$?; sed 's/^[^ ]*find: /find: /' out; echo "exit $rc"
echo "== the same, with -o -print after it"; find t -newer missing.txt -o -print > out 2>&1; rc=$?; sed 's/^[^ ]*find: /find: /' out; echo "exit $rc"
echo "== -samefile, a missing reference"; find t -samefile missing.txt > out 2>&1; rc=$?; sed 's/^[^ ]*find: /find: /' out; echo "exit $rc"
sleep 1; touch ref.txt; sleep 1; touch t/b.txt
echo "== an existing reference"; find t -newer ref.txt -type f; echo "exit $?"
cd .. && rm -rf "$d"
