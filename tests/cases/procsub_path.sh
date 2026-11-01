# 2026-10-31 (found answering koliada-net-73's report; raised, waited): `<( )` expanded to `F:\TEMP\bash-procsub-<id>`, a
# backslash path, where every path the shell hands back is forward slash (DECISIONS 2026-09-11; the architect: no
# backslash, ever). Now `F:/TEMP/bash-procsub-<id>`, and the readers still read it: cat, a `< <( )` loop, diff, and a
# separate process given the path. GNU expands to /dev/fd/63; the form is C#Bash's (its temp-file emulation, DECISIONS
# 2026-09-04 #6). Microsoft's own findstr, cmd's type and fc do not open a forward-slash folder path (findstr: "Cannot
# open F:bash-procsub-..."): as with every path the shell hands out ($PWD, mktemp). Authored (Windows-specific).
p=$(echo <(true))
case $p in *\\*) echo "a backslash in the path";; *) echo "no backslash";; esac
case $p in [A-Za-z]:/*/bash-procsub-*) echo "the shell's form";; *) echo "not the shell's form";; esac
cat <(printf 'read by cat\n')
while read -r l; do echo "read: $l"; done < <(printf 'one\ntwo\n')
diff <(printf 'a\nb\n') <(printf 'a\nc\n')
"$BASH" -c 'cat "$1"' child <(printf 'read by a separate process\n')
