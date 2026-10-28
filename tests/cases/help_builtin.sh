# 2026-10-27 (koliada-net-28; the architect): `help` and a builtin's `--help` answer with the manual page(s) of what was
# asked, from %USERPROFILE%/.bash/help.json (written from the built-in table when missing; the file's entries win
# name by name, the built-in table answers for the rest); a topic C#Bash does not implement says so beside its page.
# `help` ran Windows' help.exe (no error, Windows' answer) and `tail --help` the PATH tail's help; `enable` did not
# exist. bash's matching: an exact name or glob first, else every name it begins; -d -m -s accepted. `--help` is an
# argument to : [ echo false test true (as bash), and to an option that takes a value (grep -e --help) or a command
# after its first operand (timeout 5 echo --help). Expected: authored (the pages are C#Bash's table).
cd "${TMPDIR:-${TEMP:-/tmp}}" || exit 1
d="help-$$"; mkdir -p "$d" && cd "$d" || exit 1
export USERPROFILE="$PWD/profile"
table="$USERPROFILE/.bash/help.json"

echo "--- builtins now"
type -t help enable

echo "--- help: exact, prefix, glob, keyword, several"
help cd
help -s read
help re | cut -d: -f1 | tr '\n' ' '; echo
help 'comp*' | cut -d: -f1 | tr '\n' ' '; echo
help '[[' 'for' | cut -d: -f1 | tr '\n' ' '; echo
help -d -m tail
echo "--- the file was written: $([ -f "$table" ] && jq -r 'length > 140' "$table")"

echo "--- not implemented, beside its page"
help ulimit bind coproc select

echo "--- no topic, bad option, no pattern"
noprefix() { sed 's/^.*: line [0-9]*: //'; }
{ help zzzz; echo "rc=$?"; } 2>&1 | noprefix
{ help -x; echo "rc=$?"; } 2>&1 | noprefix
help | head -n 1

echo "--- --help"
tail --help; echo "rc=$?"
tail -n 2 --help
cd --help
jq --help
printf 'a\n--help\n' | grep -e --help
echo --help
true --help; echo "rc=$?"
timeout 5 echo --help
BASH_COREUTILS=builtin sort --help

echo "--- the file wins name by name; its own names are topics"
jq '.cd = ["https://example.org/my-cd"] | .mytool = ["https://example.org/mytool"]' "$table" > t && mv t "$table"
help cd mytool tail

echo "--- a file that does not parse: said, and the built-in table answers"
echo '{ broken' > "$table"
help cd 2>&1 | sed 's/^help: .*help.json: .*the built-in table answers$/help: (help.json does not parse); the built-in table answers/'

echo "--- enable"
enable | head -n 4
enable -a | grep -c '^enable '
enable -s | tr '\n' ' '; echo
enable -n; echo "rc=$?"
enable cd tail; echo "rc=$?"
{ enable zzz; echo "rc=$?"; } 2>&1 | noprefix
enable -n cd 2>&1 | head -n 1 | noprefix
{ enable -q; echo "rc=$?"; } 2>&1 | noprefix

cd .. && rm -rf "$d"
