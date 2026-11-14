# 2026-11-12 (facts-8a's report): every one-command pipeline sets PIPESTATUS, as bash: a simple command, an
# assignment, a function call, (( )), [[ ]], a subshell; a compound command (if, for, while, case, { }) leaves what
# its last pipeline set. Only multi-command and ! pipelines set it, so after `true | false; false` it still said 0 1.
# Expected = GNU bash via WSL.
true | false; echo "A:${PIPESTATUS[*]}"; false; echo "B:${PIPESTATUS[*]}"; true; echo "C:${PIPESTATUS[*]}"
false | true | false; echo "D:${PIPESTATUS[*]}"; true; echo "E:${PIPESTATUS[*]}"
true | false; sh -c "exit 3"; echo "F:${PIPESTATUS[*]}"
true | false; x=5; echo "G:${PIPESTATUS[*]}"
true | false; f() { return 4; }; f; echo "H:${PIPESTATUS[*]}"
true | false; (( 0 )); echo "I:${PIPESTATUS[*]}"
true | false; [[ a == b ]]; echo "J:${PIPESTATUS[*]}"
true | false; { false; true; }; echo "K:${PIPESTATUS[*]}"
true | false; if false; then :; fi; echo "L:${PIPESTATUS[*]}"
true | false; ( exit 6 ); echo "M:${PIPESTATUS[*]}"
true | false; ! false; echo "N:${PIPESTATUS[*]}"
true | false; for i in 1; do false; done; echo "O:${PIPESTATUS[*]}"
true | false; x=$(exit 2); echo "P:${PIPESTATUS[*]}"
true | false; false && true; echo "Q:${PIPESTATUS[*]}"
true | false; echo "R:${PIPESTATUS[*]} ${PIPESTATUS[*]}"
false; echo "S:${PIPESTATUS[0]}"; echo "S2:${PIPESTATUS[0]}"
true | false; a=(1 2); echo "T:${PIPESTATUS[*]}"
true | false; i=0; while (( i++ < 2 )); do true; done; echo "U:${PIPESTATUS[*]}"
true | false; echo "V:$(false; echo ${PIPESTATUS[*]}) ${PIPESTATUS[*]}"
true | false; case x in x) false;; esac; echo "W:${PIPESTATUS[*]}"
