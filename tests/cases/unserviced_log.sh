# 2026-10-27 (the architect): requests C#Bash did not service itself go to a tracking log, "a list of things to
# consider implementing": %USERPROFILE%/.bash/unserviced.jsonl, one JSON object per line. Each place that logs: a
# builtin's unsupported option (fell through to a PATH program, or failed), a "not currently available" feature, a
# construct refused when parsed, a bash builtin C#Bash lacks, help on a topic it does not implement or that matches
# nothing. BASH_UNSERVICED_LOG names another file; /dev/null turns the log off (the test runners set that). Fields
# shown: tool, feature, outcome, args (the time, build, source and line vary). Expected: authored.
cd "${TMPDIR:-${TEMP:-/tmp}}" || exit 1
d="unserviced-$$"; mkdir -p "$d" && cd "$d" || exit 1
export USERPROFILE="$PWD/profile"
unset BASH_UNSERVICED_LOG
log="$USERPROFILE/.bash/unserviced.jsonl"
show() { jq -r '[.tool, .feature, (.outcome | sub("through to .*/"; "through to ")), ((.args // []) | join(" "))] | join(" | ")' "$1"; }

echo "--- nothing logged for a request that is serviced"
tail -n 1 /dev/null; help cd > /dev/null
[ -e "$log" ] && echo "log exists" || echo "no log"

echo "--- each place"
printf 'a\n' > f
mkdir bin && printf '@echo PATH sort %%*\r\n' > bin/sort.cmd
case "$PATH" in *\;*) sep=';' ;; *) sep=':' ;; esac
(export PATH="$PWD/bin$sep$PATH"; sort --zzz f)   # an unsupported option, handed to the PATH sort
BASH_COREUTILS=builtin sort --zzz f 2> /dev/null   # ... and with nothing to hand it to
awk 'BEGIN { f = "%e\n"; printf f, 1 }' 2> /dev/null   # awk: refused while the program ran
complete -F f x 2> /dev/null                       # not currently available
set -o posix 2> /dev/null; set +o posix            # not available, going on
eval 'cat >(cat)' 2> /dev/null                     # refused when parsed
"$BASH" -c 'cat >(cat)' 2> /dev/null               # ... by a shell of its own
ulimit -n 2> /dev/null                             # a bash builtin C#Bash lacks
help bind > /dev/null                              # help: not implemented
help zzzz 2> /dev/null                             # help: no topic
show "$log"

echo "--- whole lines, valid JSON"
echo "lines: $(wc -l < "$log"), parsed: $(jq -c . "$log" | wc -l), fields: $(jq -r 'keys_unsorted | join(",")' "$log" | head -n 1)"

echo "--- BASH_UNSERVICED_LOG: another file, or /dev/null for none"
(export BASH_UNSERVICED_LOG="$PWD/other.jsonl"; ulimit 2> /dev/null); show other.jsonl
n=$(wc -l < "$log"); (export BASH_UNSERVICED_LOG=/dev/null; ulimit 2> /dev/null); echo "unchanged: $([ "$(wc -l < "$log")" = "$n" ] && echo yes || echo no)"

cd .. && rm -rf "$d"
