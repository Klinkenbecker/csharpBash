# 2026-11-01 (koliada-net-73's report): after `word \` and a newline, a continued line that starts with a quote, $'..',
# $VAR, or another continuation is a NEW word, as in GNU bash; it was glued to the word before (`echo x \⏎'y'` printed
# "xy"; `printf '%s\n' \⏎'x' \⏎'%Pz'` glued the format to its arguments: "`P': invalid format character"). The lexer's
# step at `\` made no token (the continuation alone) and forgot the space before it. Since rev 80 at least.
# Expected: GNU bash 5.1 via WSL. The script is written by printf so its bytes are exact.
cd "${TMPDIR:-${TEMP:-/tmp}}" || exit 1
d="continuation-quote-$$"; mkdir -p "$d" && cd "$d" || exit 1
printf '%s\n' "echo a \\" "'b' \\" "c" > t1.sh
printf '%s\n' "printf '%s\\n' \\" "'x' \\" "'%Pz'" > t2.sh
printf '%s\n' "echo \\" "\"q\" \\" "done" > t3.sh
printf '%s\n' "echo one \\" "\"two\"" "echo p \\" "\$'q'" "v=V; echo r \\" "\$v" "echo b \\" "\\" "\"q\"" > t4.sh
printf '%s\n' "echo a\\" "\"b\"" "echo one \\" "  \"two\"" "echo x \\" "y" "echo c \\" "{x,y}" > t5.sh
printf '%s\n' "echo x \\" "'y'z" "echo m \\" "\"n\"'o'" "echo a # c" "echo b\"c\"d" > t6.sh
for f in t1 t2 t3 t4 t5 t6; do echo "--- $f"; "$BASH" ./$f.sh 2>&1; done
echo "--- -c"; "$BASH" -c "$(cat t1.sh)"
cd .. && rm -rf "$d"
