# 2026-10-11: restricted mode is not implemented, so asking for it is refused instead of silently running
# unrestricted (DECISIONS 2026-10-11): `-r` (alone or in a cluster), `--restricted` refuse to start, status 2;
# `set -r` ends the shell (a subshell: only that one), status 2. Each prints that it is not available and a
# link to a prefilled GitHub issue, carrying the feature and the build, never the command. `+r` is harmless.
# Started as `rbash` is refused the same way (checked by hand: the case cannot rename the interpreter).
# Expected output: authored (bash has restricted mode; there is no GNU oracle for refusing it).
mask() { sed -e 's/build%20[^ ]*/build%20<stamp>/' ; }
for how in -r --restricted; do "$BASH" $how -c 'echo SHOULD-NOT-RUN SECRET-TOKEN' 2>&1 | mask; echo "rc=${PIPESTATUS[0]}"; done
"$BASH" -cr 'echo SHOULD-NOT-RUN' 2>&1 | mask; echo "rc=${PIPESTATUS[0]}"
"$BASH" -c 'echo before; set -r; echo SHOULD-NOT-RUN' t1 2>&1 | mask; echo "rc=${PIPESTATUS[0]}"
"$BASH" -c '( set -r; echo SHOULD-NOT-RUN ); echo "parent goes on rc=$?"' t2 2>&1 | mask
"$BASH" +r -c 'echo plus-r is harmless'
"$BASH" -c 'shopt restricted_shell'
echo "the command never reaches the link: $("$BASH" -r -c 'echo SECRET-TOKEN' 2>&1 | grep -c SECRET-TOKEN)"
# 2026-10-11: POSIX mode (--posix, -o posix, set -o posix, shopt -so posix) was accepted and silently ignored;
# now each says it is not available and goes on in bash's default mode, status 0 (`set +o posix` says nothing)
"$BASH" --posix -c 'echo posix-ran rc=$?' 2>&1 | mask
"$BASH" -o posix -c 'echo o-posix-ran' 2>&1 | mask
"$BASH" -c 'set -o posix; echo "set rc=$?"; set +o posix; echo "unset rc=$?"; shopt -so posix; echo "shopt rc=$?"' t3 2>&1 | mask
