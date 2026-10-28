# 2026-10-27: the tools moved onto the shared byte layer, on over 2 GiB of input: cmp, od, hexdump, base64, tac read the
# whole input into one array and failed ("Stream was too long"); they now read a block at a time. Two SPARSE files
# (no disk used): "first\nsecond\n", zeros up to 2 GiB, then "\nlast1\nlast2\nEND\n" (2147483665 bytes); the second
# ends "ENX". Made with fsutil on Windows, truncate elsewhere; SKIP with neither. od's addresses are off (-A n): C#Bash
# counts them from after -j, GNU from the file's start (DECISIONS 2026-10-27). Expected: GNU coreutils 8.32 and
# diffutils via WSL.
cd "${TMPDIR:-${TEMP:-/tmp}}" || exit 1
d="bytes-large-$$"; mkdir -p "$d" && cd "$d" || exit 1
sparse() {
	printf 'first\nsecond\n' > "$1"
	if type -P fsutil > /dev/null; then fsutil sparse setflag "$1" > /dev/null && fsutil file seteof "$1" 2147483648 > /dev/null
	elif type -P truncate > /dev/null; then truncate -s 2147483648 "$1"
	else return 1; fi
	printf '\nlast1\nlast2\n%s\n' "$2" >> "$1"
	}
sparse big END && sparse big2 ENX || { cd .. && rm -rf "$d"; echo "SKIP: needs fsutil or truncate"; exit 0; }

echo "--- cmp (the difference past 2 GiB)"; cmp big big2; echo "rc=$?"
echo "--- cmp -s, the same file";           cmp -s big big; echo "rc=$?"
echo "--- od -j past 2 GiB";                od -A n -t x1 -j 2147483648 big
echo "--- hexdump -C -s past 2 GiB";        hexdump -C -s 2147483648 big
echo "--- tac (the last lines first; it held every line in a list)"; tac big | head -n 3
echo "--- base64 -w 0 (its last 24; GNU ends with no newline, C#Bash with one: not this case's subject)"; base64 -w 0 big | tr -d '\n' | tail -c 24; echo

cd .. && rm -rf "$d"
