basename /a/b/c.txt
basename /a/b/c.txt .txt
basename /usr/
dirname /a/b/c
dirname file
dirname /a/
seq 3
seq 2 2 8
mkdir -p "$TEMP/bashut/x/y"
[ -d "$TEMP/bashut/x/y" ] && echo "mkdir ok" || echo "mkdir FAIL"
mkdir -p "$TEMP/bashut/x/y"; echo "idem rc=$?"
