# 2026-10-28: output into a pipeline's pipe is buffered and written when each builtin finishes (and when the stage
# ends), as bash's builtins flush theirs (it was a write a line). Nothing visible changes: the order holds (builtin /
# program / builtin, printf pieces around a program, 2>&1 and |&), a reader downstream gets a line as soon as the builtin
# that wrote it finishes (read -t sees "start" while the producer sleeps: buffering per stage would time it out), a
# closed reader still stops the writer (yes | head, PIPESTATUS 141), large outputs are byte-exact.
# Expected: GNU bash 5.1 via WSL.
cd "${TMPDIR:-${TEMP:-/tmp}}" || exit 1
{ echo a; "$BASH" -c 'echo ext'; echo b; } | tr '\n' ' '; echo
{ printf a; printf b; "$BASH" -c 'printf X'; printf c; } | cat; echo
{ echo o1; echo e1 >&2; echo o2; } 2>&1 | tr '\n' ' '; echo
{ echo o1; echo e1 >&2; echo o2; } |& tr '\n' ' '; echo
{ echo start; sleep 3; echo end; } | { if read -r -t 2 l; then echo "got $l while it sleeps"; else echo "timed out"; fi; cat; }
yes | head -n 3 | tr '\n' ' '; echo
yes | head -n 1 > /dev/null; echo "PIPESTATUS ${PIPESTATUS[*]}"
seq 1 200000 | sed 's/$/!/' | md5sum | cut -c1-12
seq 1 100000 | awk '{ print $1 * 3 }' | tail -n 1
for i in 1 2 3; do echo $i; done | tac | tr '\n' ' '; echo
f() { echo in-f; printf 'no newline'; }; f | od -An -c | tr -s ' '
{ echo first; exit 3; } | cat; echo "rc ${PIPESTATUS[0]}"
