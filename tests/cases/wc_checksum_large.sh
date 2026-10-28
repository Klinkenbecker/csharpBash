# 2026-10-27 (koliada-net-28): wc and the checksums on over 2 GiB of input, file or pipe. They read the whole input
# into one array (and wc into one string) and failed ("Stream was too long"); they now count and hash as they read.
# The file is SPARSE, so no disk is used: "first\nsecond\n", zeros up to 2 GiB, then "\nlast1\nlast2\nEND\n"
# (2147483665 bytes). Made with fsutil on Windows, truncate elsewhere; SKIP with neither. Expected: GNU coreutils 8.32
# via WSL (LC_ALL=C.UTF-8: -m counts characters; a NUL is one).
export LC_ALL=C.UTF-8
cd "${TMPDIR:-${TEMP:-/tmp}}" || exit 1
d="wc-large-$$"; mkdir -p "$d" && cd "$d" || exit 1
printf 'first\nsecond\n' > big
if type -P fsutil > /dev/null; then fsutil sparse setflag big > /dev/null && fsutil file seteof big 2147483648 > /dev/null
elif type -P truncate > /dev/null; then truncate -s 2147483648 big
else cd .. && rm -rf "$d"; echo "SKIP: needs fsutil or truncate"; exit 0; fi
printf '\nlast1\nlast2\nEND\n' >> big

echo "--- wc -c (the length)";       wc -c big
echo "--- wc -l < big";             wc -l < big
echo "--- wc -lc";                  wc -lc big
echo "--- wc -m (every character)"; wc -m big
echo "--- cat | wc -c (a pipe)";    cat big | wc -c
echo "--- wc -c < big";             wc -c < big
echo "--- md5sum";                  md5sum big
echo "--- rc"; wc -l big > /dev/null; echo $?

cd .. && rm -rf "$d"
