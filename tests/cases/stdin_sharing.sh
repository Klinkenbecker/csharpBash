# 2026-10-10: a program run while the shell reads a FILE shares the file offset, as on Unix: it reads what
# it reads and the shell goes on from there. `while read -r f; do prog; done < list` ended after ONE line
# (the rest of the file was pumped into prog, or lost to the child's own `< /dev/null`); a C#Bash child's
# read-ahead is given back at its exit. Children are "$BASH" so every shell runs a real program.
# Expected output: GNU bash via WSL.
d=$(mktemp -d) && cd "$d" || exit 1
seq 1 5 > five.txt; seq 1 6 > six.txt
printf 'while read -r x; do echo "in:$x"; "$BASH" -c '"'"'read -r y; echo "kid:$y"'"'"'; done\n' > inner.sh
echo "A: $(while read -r f; do echo "$f"; done < five.txt | tr '\n' ' ')"
echo "B: $(while read -r f; do "$BASH" -c 'exit 0' < /dev/null; echo "$f"; done < five.txt | tr '\n' ' ')"
echo "C: $(while read -r f; do "$BASH" -c 'exit 0'; echo "$f"; done < five.txt | tr '\n' ' ')"
echo "D: $(while read -r x; do echo "loop:$x"; "$BASH" -c 'read -r y; echo "child:$y"'; done < six.txt | tr '\n' ' ')"
echo "E: $(while read -r x; do echo "loop:$x"; head -n 1; done < six.txt | tr '\n' ' ')"
echo "F: $( { read -r a; "$BASH" -c 'read -r b; echo "c:$b"'; read -r c; echo "p:$a,$c"; } < six.txt | tr '\n' ' ')"
echo "G: $("$BASH" inner.sh < six.txt | tr '\n' ' ')"
echo "H: $(printf '1\n2\n3\n' | while read -r x; do "$BASH" -c 'exit 0' < /dev/null; echo "$x"; done | tr '\n' ' ')"
echo "I: $(while read -r x; do "$BASH" -c 'cat > /dev/null' < /dev/null; echo "$x"; done < five.txt | tr '\n' ' ')"
echo "J: $(while read -r x; do "$BASH" -c 'cat'; done < five.txt | tr '\n' ' ')"
cd / && rm -rf "$d"
