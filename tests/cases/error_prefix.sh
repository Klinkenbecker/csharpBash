# 2026-10-10: error messages carry bash's prefix: `name: line N: ` (name = BASH_SOURCE[0], else $0;
# so a function's errors name the file it was defined in), and a syntax error's input name when it
# differs (`script: eval: line N: `, `name: -c: line N: `). Every message used to say `bash: `.
# The scripts run by relative name from a temp dir, so the names are the same in every shell.
# Syntax errors: only the prefix is compared (their message text is a separate item), one line each.
# Expected output: GNU bash via WSL.
d=$(mktemp -d) && cd "$d" || exit 1
cat > lib.sh <<'EOF'
libf() {
  nosuch_lib
}
nosuch_src
EOF
cat > main.sh <<'EOF'
echo start
nosuch_top
. ./lib.sh
libf
f() { nosuch_in_f; }
f
eval 'nosuch_eval'
eval $'true\nnosuch_eval2'
x=$(nosuch_cs)
echo a | nosuch_pipe
( nosuch_sub )
for i in 1 2; do
  nosuch_loop
done
if nosuch_cond; then :; fi
echo a \
  b; nosuch_cont
cd /nonexistent_zz
printf '%d\n' zz
read -z 2>&1 | head -1
cat < /nonexistent_zz
echo hi > /nonexistent_zz/x
( echo hidden > /nonexistent_zz/x ) 2>/dev/null
. ./nofile_zz
g() { echo ${u:?custom message}; }
( g )
echo "[$(nosuch_q)]"
cat <<EOT
$(nosuch_hd)
EOT
echo ${u:-$(nosuch_op)} "${u:-$(nosuch_opq)}" $(( $(nosuch_ar) + 1 ))
fq() { echo "$(nosuch_fq)"; }
fq
echo "L $(echo $LINENO) $(( LINENO ))"; eval $'echo L $LINENO\necho L $LINENO'
echo ${u:?last}
echo never
EOF
"$BASH" main.sh 2>&1
"$BASH" -c 'true
nosuch_c' myname 2>&1
"$BASH" -c 'f() { nosuch_cf; }; f' myname 2>&1
printf 'f() { eval "if then"; }\nf\n' > evsyn.sh
"$BASH" evsyn.sh 2>&1 | sed -E 's/(line [0-9]+: ).*/\1(syntax error)/' | uniq
"$BASH" -c 'if then' myname 2>&1 | sed -E 's/(line [0-9]+: ).*/\1(syntax error)/' | uniq
printf 'true\nfi\n' > stray.sh
"$BASH" stray.sh 2>&1 | sed -E 's/(line [0-9]+: ).*/\1(syntax error)/' | uniq
cd / && rm -rf "$d"
