# 2026-10-30 (the "match GNU" item, found 2026-10-27): base64 -w 0 writes no newline; -d skips newlines only (a space,
# a tab or a CR is invalid input) unless -i (--ignore-garbage) skips everything outside the alphabet. Expected: GNU
# coreutils 8.32 via WSL.
r() { "$@" 2>&1; echo "rc=$?"; }
hex() { od -An -tx1 | tr -s ' ' | tr '\n' ' '; echo; }

echo "--- -w 0";                    printf 'hello world' | base64 -w 0 | hex
echo "--- -w 0, empty";             printf '' | base64 -w 0 | hex
echo "--- -w 8";                    printf 'hello world' | base64 -w 8 | hex
echo "--- default";                 printf 'hello world' | base64 | hex
echo "--- -d, wrapped lines";       printf 'aGVs\nbG8g\nd29y\nbGQ=\n' | { base64 -d 2>&1; echo " rc=$?"; }
echo "--- -d, CRLF lines";          printf 'aGVs\r\nbG8=\r\n' | { base64 -d 2>&1; echo " rc=$?"; }
echo "--- -d, a space";             printf 'aGVs bG8=\n' | { base64 -d 2>&1; echo " rc=$?"; }
echo "--- -d, a tab";               printf 'aGVs\tbG8=\n' | { base64 -d 2>&1; echo " rc=$?"; }
echo "--- -d -i, a space";          printf 'aGVs bG8=\n' | { base64 -d -i 2>&1; echo " rc=$?"; }
echo "--- -d -i, CR and garbage";   printf 'aG!Vs\r\nbG*8=\r\n' | { base64 -d -i 2>&1; echo " rc=$?"; }
echo "--- -d, garbage";             printf 'aG!Vs\n' | { base64 -d 2>&1; echo " rc=$?"; }
echo "--- -i without -d";           printf 'hi' | { base64 -i 2>&1; echo "rc=$?"; }
