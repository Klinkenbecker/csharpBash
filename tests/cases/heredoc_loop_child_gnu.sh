# 2026-11-27 (bugs list 18, wombat-3a): inside `while read ...; do prog; done << EOF` a native program that does not
# read its stdin leaves the document to the loop, as a bash child shares the document's pipe; C#Bash pumped the whole
# rest into the first one and the loop ran once (Wombat's contract checks skipped 43 values, silently). A program that
# reads everything takes it all; nothing comes back twice. "$BASH" is the native program. Expected = GNU bash 5.1 via WSL.
echo '-- a here-document'
while read -r x; do echo "it $x"; "$BASH" -c ':'; done << 'EOF'
a
b
c
EOF
echo '-- a here-string'
while read -r x; do echo "it $x"; "$BASH" -c ':'; done <<< $'a\nb\nc'
echo '-- the program inside $( )'
while read -r x; do v=$("$BASH" -c 'echo v'); echo "it $x $v"; done << 'EOF'
a
b
c
EOF
echo '-- the program in a function'
f() { "$BASH" -c ':'; }
while read -r x; do echo "it $x"; f; done << 'EOF'
a
b
c
EOF
echo '-- the program as a pipeline stage'
while read -r x; do echo "it $x"; "$BASH" -c ':' | cat; done << 'EOF'
a
b
c
EOF
echo '-- a program that reads everything'
while read -r x; do echo "it $x"; "$BASH" -c 'cat > /dev/null'; done << 'EOF'
a
b
c
EOF
echo '-- read, a program, then cat'
{ read -r x; echo "first $x"; "$BASH" -c ':'; cat; } << 'EOF'
a
b
c
EOF
echo '-- a program, a read, then a program that reads the rest'
{ "$BASH" -c ':'; read -r x; echo "read $x"; "$BASH" -c 'cat'; echo end; } << 'EOF2'
a
b
c
EOF2
