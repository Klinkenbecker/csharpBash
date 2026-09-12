# Heredocs, here-strings, &>, redirect ordering, MSYS/tmp paths, nested substitution,
# double-quote escapes, parameter-expansion operators, arithmetic commands.
cat <<EOF
expanded $((1+1)) $HOME_TEST_UNSET-end
EOF
v=val; cat <<'EOF'
literal $v
EOF
cat <<-EOF
	stripped
EOF
cat > cases/hd_tmp.txt <<EOF
to-file
EOF
cat cases/hd_tmp.txt; rm cases/hd_tmp.txt
cat <<EOF | tr a-z A-Z
piped
EOF
tr a-z A-Z <<< herestring
echo out 2>&1 >/dev/null | cat; echo "swap-done"
echo both &> cases/both_tmp.txt; cat cases/both_tmp.txt; rm cases/both_tmp.txt
cd /tmp && echo "tmp-ok"; cd - >/dev/null
[ -d /c/Windows ] && echo "msys-path-ok"
echo "$(echo outer $(echo inner))"
echo "C:\Users\x" 'C:\Users\y'
echo "quote: \"q\" \$notvar"
x=hello; echo "${x:1:3} ${x^^} ${x^} ${x: -2} ${x//l/L} ${#x}"
arr=(a b c); arr+=(d); echo "${arr[@]:1:2} ${#arr[@]} ${arr[-1]}"
s+=x; s+=y; echo "$s"
n=1; ((n++)); ((n+=10)); echo "$n"; (( n > 5 )) && echo "arith-true"
for ((i=0; i<3; i++)); do printf '%d' "$i"; done; echo
y=x; x=indirect; echo "${!y}"
echo "${undef:-default}|${undef+set}|"
p=/a/b/c.txt; echo "${p##*/} ${p%.*} ${p#/a/}"
