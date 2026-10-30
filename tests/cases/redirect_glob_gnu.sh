# 2026-10-30 (esdk-a2's report): a redirection's word is expanded as bash expands it: braces, pathname expansion and
# splitting too, and anything but exactly one word is "WORD: ambiguous redirect" (the word as written), status 1. It
# was expanded to one string, so `read l < *.sdk` opened a file named "*.sdk". Both paths: a builtin's redirections and
# an external program's. A pattern that matches nothing is the word itself (`[z].txt`: a name Windows allows). The
# message's prefix (script and line) is not compared. Expected: GNU bash 5.1 via WSL, LC_ALL=C.
export LC_ALL=C
cd "${TMPDIR:-${TEMP:-/tmp}}" || exit 1
d="redirect-glob-$$"; mkdir -p "$d" && cd "$d" || exit 1
p() { printf '%s\n' "--- $1"; }
msg() { [ -s err ] && sed 's/^.*: line [0-9]*: //; s/^/err: /' err; rm -f err; }
echo one > only.sdk
p "one match: read";       read l < *.sdk; echo "got=$l"
p "one match: \$(< )";     echo "sub=$(< *.sdk)"
p "one match: write";      echo two > *.sdk; cat only.sdk
p "one match: append";     echo three >> *.sdk; cat only.sdk
p "an external program";   "$BASH" -c 'read l; echo "ext=$l"' < *.sdk
echo four > b.sdk
p "two matches";           read l 2>err < *.sdk; echo "rc=$?"; msg
p "two, external";         "$BASH" -c 'cat' 2>err < *.sdk; echo "rc=$?"; msg
p "braces";                echo y 2>err > {c,d}.txt; echo "rc=$?"; msg
p "no match: the word";    echo z > [z].txt; cat '[z].txt'
p "unquoted, empty";       x=; echo a 2>err > $x; echo "rc=$?"; msg
p "unquoted, two words";   f="a b"; echo a 2>err > $f; echo "rc=$?"; msg
p "quoted: one word";      echo a > "$f"; cat "a b"
p "nullglob, no match";    shopt -s nullglob; echo n 2>err > *.zzz; echo "rc=$?"; msg; shopt -u nullglob
p "\$(< missing)";          { x=$(< missing.txt); } 2>err; echo "rc=$?"; msg
p "the files";             ls
cd .. && rm -rf "$d"
