# The shell's path form is FORWARD SLASH in BOTH directions (ratified 2026-09-11, made symmetric
# 2026-09-12, superseding decision 3 of 2026-09-04). Outbound: pwd/$PWD/mktemp/realpath return
# `E:/x`, never `E:\x` — backslash is the shell's escape character, so a backslash path is a value
# the shell's own operations misread. Inbound: TranslatePath maps MSYS `/c/x` to `C:/x` and
# normalises any backslashes, so a path that goes out and comes back in is the same string.
# The conversion is a PATH boundary, not a text rewrite: backslash keeps its escape meaning in
# every argument (installer-79's caution, made permanent below).
cd "$(dirname "$0")/.." || exit 1

echo "--- outbound paths carry no backslashes"
case "$(pwd)" in *\\*) echo "pwd BACKSLASH-LEAK";; *) echo "pwd clean";; esac
case "$PWD"    in *\\*) echo "PWD BACKSLASH-LEAK";; *) echo "PWD clean";; esac
[ "$(pwd)" = "$PWD" ] && echo "pwd == PWD"
d=$(mktemp -d); case "$d" in *\\*) echo "mktemp BACKSLASH-LEAK";; *) echo "mktemp clean";; esac; rmdir "$d"
r=$(realpath .); case "$r" in *\\*) echo "realpath BACKSLASH-LEAK";; *) echo "realpath clean";; esac

echo "--- the value is usable by the shell's own string operations"
R=$(pwd)
case "$R" in */tests) echo "case-suffix-ok";; *) echo "case-suffix-BAD";; esac
[[ "$R" == */tests ]] && echo "glob-match-ok"
b=${R##*/}; echo "basename-strip=$b"
p=${R%/*}; case "$p" in */tests) echo "dirname-strip-BAD";; *) echo "dirname-strip-ok";; esac
cd "$R" && echo "round-trip-cd-ok"

echo "--- SYMMETRY: out then back in is the same string"
t="${TMPDIR:-${TEMP:-/tmp}}/paths-sym-$$"; mkdir -p "$t" && cd "$t" || exit 1
here=$(pwd)
cd "$here" && [ "$(pwd)" = "$here" ] && echo "pwd->cd->pwd stable"
echo one > a.txt
[ "$(realpath a.txt)" = "$here/a.txt" ] && echo "realpath agrees with pwd"
cat "$here/a.txt" >/dev/null && echo "absolute read ok"
cd /
case "$(realpath "$here/a.txt")" in *\\*) echo "abs BACKSLASH-LEAK";; *) echo "abs clean";; esac
cat "$here/a.txt" >/dev/null && echo "read from elsewhere ok"

echo "--- INBOUND: the MSYS /<d>/... form resolves to the same file"
cd "$t" || exit 1
# Build the MSYS form only from a drive-letter pwd; under a shell that already reports MSYS
# paths (Git Bash) $here IS that form, so the case below just re-uses it.
case "$here" in
  [A-Za-z]:/*) msys="/$(printf '%s' "${here%%:*}" | tr 'A-Z' 'a-z')${here#*:}" ;;
  *)           msys="$here" ;;
esac
cat "$msys/a.txt" && echo "msys-form read ok"
case "$(realpath "$msys/a.txt")" in *\\*) echo "msys BACKSLASH-LEAK";; *) echo "msys clean";; esac

echo "--- IO failures are LOUD, never fatal"
split -l 1 a.txt nodir/sub/pre 2>/dev/null; echo "split rc=$? (shell alive)"
cat nodir/sub/missing.txt 2>/dev/null; echo "cat rc=$?"
# Run in a CHILD shell so the message is comparable: the shell emits a failed redirect's error
# itself, and two known fidelity gaps make it un-suppressable in place here — bash prefixes it
# "<script>: line N:" where C#Bash says "bash:", and C#Bash prints it after the enclosing
# redirect scope is released, so `( … ) 2>/dev/null` does not catch it. Both recorded 2026-09-12.
"${BASH:-bash}" -c 'echo x > nodir/sub/out.txt' 2>/dev/null; echo "redirect rc=$?"
md5sum nodir/f 2>/dev/null; echo "md5sum rc=$?"
echo "still running after four failures"

echo "--- OUTBOUND ONLY: backslash keeps its escape meaning in arguments"
printf 'a\tb' | od -An -c | tr -s ' '
echo Xtxt | grep -c '\.txt'
echo .txt | grep -c '\.txt'
echo "C:\temp"
x='a\b'; echo "$x"
find . -maxdepth 1 -name '\*' | wc -l
echo 'lit\nlit'
cd /; rm -rf "$t"
