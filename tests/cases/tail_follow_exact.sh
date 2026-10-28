# 2026-10-27: tail -f writes what was appended exactly once. The follow loop read to the end as one string but began
# its next round at the length it saw BEFORE the read, so what a busy writer added in between came out twice (30000
# lines appended as fast as a loop could: ~57000-60000 out). Now the appended bytes go through the byte layer and the
# next round starts where the read ended. A writer that starts after tail is up: every line once; a racing start: the
# output is exactly the file's last lines (tail starts at the last 10); appends that end mid-line, raw bytes, no
# final newline. Expected: GNU coreutils 8.32 via WSL.
cd "${TMPDIR:-${TEMP:-/tmp}}" || exit 1
d="tail-follow-$$"; mkdir -p "$d" && cd "$d" || exit 1
: > f; timeout 4 tail -f -s 0.02 f > out &
sleep 1
for ((i = 1; i <= 3000; i++)); do echo "line $i" >> f; done
wait
echo "after start: $(wc -l < f) $(wc -l < out) $(cmp -s f out && echo same || echo differs)"
: > g; timeout 4 tail -f -s 0.02 g > out2 &
for ((i = 1; i <= 3000; i++)); do echo "line $i" >> g; done
wait
n=$(wc -l < out2); echo "racing start: $(sort out2 | uniq -d | wc -l) repeated, $(tail -n "$n" g | cmp -s - out2 && echo "the file's last lines" || echo "not the file's last lines")"
printf 'start\n' > h; timeout 4 tail -f -s 0.02 h > out3 &
sleep 1; printf 'par' >> h; sleep 0.3; printf 'tial\n\200\377 raw\n' >> h; sleep 0.3; printf 'end, no newline' >> h
wait
od -An -c out3 | tr -s ' '
cd .. && rm -rf "$d"
