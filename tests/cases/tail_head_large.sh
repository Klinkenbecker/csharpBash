# 2026-10-27 (koliada-net-28): tail, and head's from-the-end forms, on a file over 2 GiB. They read the whole file
# into one .NET array or string and failed ("Stream was too long", "The length cannot be greater than the capacity");
# a file is now read from its end (a pipe keeps a bounded window). The file is SPARSE, so no disk is used:
# "first\nsecond\n", zeros up to 2 GiB, then "\nlast1\nlast2\nEND\n" (2147483665 bytes). Made with fsutil on Windows,
# truncate elsewhere; SKIP with neither. Expected: GNU coreutils 8.32 via WSL.
cd "${TMPDIR:-${TEMP:-/tmp}}" || exit 1
d="tail-large-$$"; mkdir -p "$d" && cd "$d" || exit 1
printf 'first\nsecond\n' > big
if type -P fsutil > /dev/null; then fsutil sparse setflag big > /dev/null && fsutil file seteof big 2147483648 > /dev/null
elif type -P truncate > /dev/null; then truncate -s 2147483648 big
else cd .. && rm -rf "$d"; echo "SKIP: needs fsutil or truncate"; exit 0; fi
printf '\nlast1\nlast2\nEND\n' >> big
hex() { od -An -tx1 | tr -s ' ' | tr '\n' ' '; echo; }

echo "--- tail -c 4";               tail -c 4 big | hex
echo "--- tail -n 3";               tail -n 3 big
echo "--- tail -2";                 tail -2 big
echo "--- tail -c +2147483649";     tail -c +2147483649 big | hex
echo "--- tail -z -n 1";            tail -z -n 1 big | hex
echo "--- tail -n 2 < big";         tail -n 2 < big
echo "--- head -c -2147483652";     head -c -2147483652 big
echo "--- head -n -4 (back over the zeros)"; head -n -4 big
echo "--- tail -n +2 | head -n 1";  tail -n +2 big | head -n 1
echo "--- rc"; tail -c 1 big > /dev/null; echo $?

cd .. && rm -rf "$d"
