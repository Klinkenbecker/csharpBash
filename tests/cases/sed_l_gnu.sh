# 2026-10-30 (the "match GNU" item, found 2026-10-28): sed's `l` wraps its output at 70 characters by default (a line
# ends in `\` at 69), `l N` and `-l N` set the width, `l 0` and `-l 0` never wrap, and an escape is never split.
# Expected: GNU sed via WSL, LC_ALL=C.UTF-8.
export LC_ALL=C.UTF-8
long=$(printf 'abcdefghij%.0s' 1 2 3 4 5 6 7 8 9 10)
echo "--- l, 100 characters";          printf '%s\n' "$long" | sed -n l
echo "--- l 20";                        printf '%s\n' "$long" | sed -n 'l 20'
echo "--- l 0";                         printf '%s\n' "$long" | sed -n 'l 0'
echo "--- -l 30";                       printf '%s\n' "$long" | sed -n -l 30 l
echo "--- -l 0";                        printf '%s\n' "$long" | sed -n -l 0 l
echo "--- l 1";                         printf 'abc\n' | sed -n 'l 1'
echo "--- l 2";                         printf 'abc\n' | sed -n 'l 2'
echo "--- escapes near the edge";       printf 'abcdefghijklmn\topqrstu\001vwxyz\\yz\n' | sed -n 'l 10'
echo "--- non-ASCII";                   printf 'caf\303\251 na\303\257ve\n' | sed -n l
echo "--- non-ASCII wrapped";           printf 'aaaaaaaa\303\251bbbbbbb\n' | sed -n 'l 10'
echo "--- exactly 69";                  printf '%s\n' "${long:0:69}" | sed -n l
echo "--- exactly 70";                  printf '%s\n' "${long:0:70}" | sed -n l
