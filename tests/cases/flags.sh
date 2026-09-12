# Invocation contract: option order (-c -l), combined flags, --version, -o, positionals,
# a piped stdin script, $-, exit codes. --noprofile keeps the machine's profile out.
"$BASH" -c -l --noprofile 'echo "c-l ok $#"' </dev/null 2>/dev/null
"$BASH" -lc --noprofile 'echo "lc ok"' 2>/dev/null
"$BASH" -c 'echo "$0 $1 $#"' name a b
v=$("$BASH" --version); echo "${v%%,*}"
"$BASH" -o pipefail -c 'false | true; echo "pipefail=$?"'
"$BASH" -ec 'false; echo not-reached'; echo "errexit rc=$?"
echo 'echo "from stdin $((2+3))"' | "$BASH"
f=$("$BASH" -c 'echo "flags=$-"'); case "$f" in *c*) echo "dash-c flag";; esac
"$BASH" -c 'exit 7'; echo "exit=$?"
"$BASH" -c 'if' 2>/dev/null; echo "syntax rc=$?"
"$BASH" -c 'set -euo pipefail; false | true; echo not-reached'; echo "euo rc=$?"
# build identity as VALUES (2026-09-08): CSHARPBASH_BUILD is the stamp string, CSHARPBASH_REV an integer
[[ $CSHARPBASH_BUILD == 1.0.0+hg.* ]] && echo "build-var-ok"
[[ $CSHARPBASH_REV =~ ^[0-9]+$ ]] && (( CSHARPBASH_REV >= 59 )) && echo "rev-var-ok"
"$BASH" --version | grep -c 'C#Bash build 1.0.0+hg\.'
[ "$("$BASH" --version | grep -o 'hg\.[0-9a-f]*+* [0-9]*+*')" = "${CSHARPBASH_BUILD#1.0.0+}" ] && echo "stamp-agrees"
