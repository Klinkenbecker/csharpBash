# 2026-10-30 (esdk-a2's report): a name Windows cannot have (`*`, `?`, ...: a quoted one, or a pattern that matched
# nothing and so is the word itself) gets bash's words, not .NET's "The filename, directory name, or volume label syntax
# is incorrect. : 'F:\...'". A read: "No such file or directory" (no such file can exist, as GNU says of a missing one);
# a write: "Invalid argument" (GNU would create the file; Windows cannot). Every path: a builtin's redirection, append,
# an external program's both ways, `$(< )`, and a tool's own open (cat). Authored: Windows only (Linux allows the names).
cd "${TMPDIR:-${TEMP:-/tmp}}" || exit 1
d="redirect-badname-$$"; mkdir -p "$d" && cd "$d" || exit 1
p() { printf '%s\n' "--- $1"; }
msg() { [ -s err ] && sed 's/^.*: line [0-9]*: //; s/^/err: /' err; rm -f err; }
p "read, quoted";        cat 2>err < "*.none"; echo "rc=$?"; msg
p "write, quoted";       echo x 2>err > "*.none"; echo "rc=$?"; msg
p "write, no match";     echo x 2>err > *.none; echo "rc=$?"; msg
p "append";              echo x 2>err >> "a?b"; echo "rc=$?"; msg
p "external, write";     "$BASH" -c : 2>err > "?.none"; echo "rc=$?"; msg
p "external, read";      "$BASH" -c cat 2>err < "?.none"; echo "rc=$?"; msg
p "\$(< )";              { x=$(< "*.none"); } 2>err; echo "rc=$?"; msg
p "cat's own open";      cat "*.none" 2>err; echo "rc=$?"; msg
p "nothing created";     ls | wc -l
cd .. && rm -rf "$d"
