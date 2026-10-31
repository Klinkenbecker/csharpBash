# 2026-10-31 (qa-de's report): the POSIX idioms PATH="$PATH:DIR" and PATH="DIR:$PATH" add DIR to the search, as bash's.
# C#Bash keeps PATH in native `;` form (by design) and converted an all-colon, /-rooted PATH on assignment, but a mixed
# value (the native list plus `:DIR`) was left as it was, so DIR was glued onto the last entry and never searched:
# "qaprobe: command not found". DIR as $PWD gives (F:/... on Windows), in MSYS form (/f/...), and in a fresh shell.
# Expected: GNU bash 5.1 via WSL (there $PWD is /tmp/..., which is its own MSYS form).
cd "${TMPDIR:-${TEMP:-/tmp}}" || exit 1
d="path-append-$$"; mkdir -p "$d/pbin" && cd "$d" || exit 1
p() { printf '%s\n' "--- $1"; }
printf '#!/bin/sh\necho "probe ran: $1"\n' > pbin/qaprobe; chmod +x pbin/qaprobe
p "not on PATH"; ( qaprobe a ) 2>&1 | sed 's/^.*: line [0-9]*: //'
p "appended"; ( PATH="$PATH:$PWD/pbin"; qaprobe b )
p "prepended"; ( PATH="$PWD/pbin:$PATH"; qaprobe c )
case $PWD in ?:*) m="/$(printf %s "${PWD:0:1}" | tr A-Z a-z)${PWD:2}";; *) m=$PWD;; esac
p "appended, MSYS form"; ( PATH="$PATH:$m/pbin"; qaprobe d )
p "appended: the last entry is DIR"; ( PATH="$PATH:$PWD/pbin"; case $PATH in *[\;:]"$PWD/pbin") echo yes;; *) echo no;; esac )
p "appended in MSYS form: the last entry is DIR, converted"; ( PATH="$PATH:$m/pbin"; case $PATH in *[\;:]"$PWD/pbin") echo yes;; *) echo no;; esac )
p "a fresh shell"; "$BASH" -c 'PATH="$PATH:'"$PWD"'/pbin"; qaprobe e'
p "exported to a child"; ( export PATH="$PATH:$PWD/pbin"; "$BASH" -c 'qaprobe f' )
cd .. && rm -rf "$d"
