# 2026-10-10: syntax errors are reported in bash's words and at bash's lines (parse.y report_syntax_error,
# parser_error, cond_error): "syntax error near unexpected token `fi'" plus the source line, "syntax
# error: unexpected end of file", "unexpected EOF while looking for matching `)'", the [[ ]] forms; the
# grammar bash rejects is rejected (`then fi`, `{ }`, `a &&` at the end, `f() echo x`, an unclosed
# backquote); an unterminated heredoc warns. C#Bash printed its own text (`Expected 'fi' but got '' at
# 5:1`); a last line with no newline counts as a line (the end of file is the line after it). Commands
# before the error are `true`: bash runs them, C#Bash parses the whole file first (an
# open question, not tested here; nor is a syntax error inside $( ), which bash 5.1 reports at run time
# as `name: command substitution: line N:` and survives). Each script runs by relative name from a temp dir.
# Expected output: GNU bash via WSL.
d=$(mktemp -d) && cd "$d" || exit 1
i=0
w() { i=$((i+1)); printf '%s' "$1" > "s$i.sh"; }
w $'true\nif true; then\nfi\ntrue\n'
w $'true\nfi\n'
w $'if true; then echo x\n'
w $'while true; do echo x\n'
w $'echo "unterminated\n'
w $'echo \'unterminated\n'
w $'echo $(echo x\n'
w $'case x in\n'
w $'for in a; do :; done\n'
w $'echo a |\n'
w $'echo a && \n'
w $'( echo a\n'
w $'{ echo a\n'
w $'echo )\n'
w $'f() echo x\n'
w $'echo a; ; echo b\n'
w $'if then fi\n'
w $'echo ${\n'
w $'x=(a b\n'
w $'[[ a == \n'
w $'cat <<EOF\nno end\n'
w $'true\n  done\ntrue\n'
w $'for x in a b\necho $x\ndone\n'
w $'[[ a == ]]\n'
w $'[[ -f ]]\n'
w $'[[ a b ]]\n'
w $'[[ ]]\n'
w $'[[ a == b\n'
w $'[[ ( a == b ]]\n'
w $'[[ a == b ) ]]\n'
w $'[[ ! ]]\n'
w $'[[ a && ]]\n'
w $'true\n[[ x =~ ]]\n'
w $'x=(a b\necho c\n'
w $'f() { x=(a; }\n'
w $'echo `echo x\n'
w $'{ }\n'
w $'( )\n'
w $'while :; do done\n'
w $'for x in a; do done\n'
w $'true ||\n'
w $'true && fi\n'
w $'true\neval \'if then\'\necho after\n'
w $'echo a &&'
w $'true\necho a &&'
w $'echo "x'
w $'if true; then'
for n in $(seq 1 $i); do echo "== s$n"; "$BASH" "s$n.sh" 2>&1; echo "rc=$?"; done
echo "== -c"; "$BASH" -c 'echo a &&' 2>&1 | sed 's/^.*: -c: line/BASH: -c: line/'
cd / && rm -rf "$d"
