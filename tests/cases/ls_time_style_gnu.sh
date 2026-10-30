# 2026-10-30 (kes-term-a1's report): ls --time-style as GNU's: full-iso, long-iso, iso (recent: MM-DD HH:MM; old: the
# date and a space), locale, +FORMAT (with a newline: old files' format, then recent ones'), and posix- (the default
# under the C locale); anything else "invalid argument ... for 'time style'", 2. Only full-iso was honoured; the rest
# were silently the default. Times and zones differ between the two systems, so the shape is compared: digits as N,
# the zone as ZONE. Expected: GNU coreutils 8.32 via WSL, LC_ALL=C.
export LC_ALL=C
cd "${TMPDIR:-${TEMP:-/tmp}}" || exit 1
d="ls-time-style-$$"; mkdir -p "$d" && cd "$d" || exit 1
printf x > old; touch -d '2020-01-02 03:04:05' old; printf y > new
shape() { sed 's/^[^ ]* *[0-9]* [^ ]* *[^ ]* *//; s/[0-9]/N/g; s/[+-]NNNN/ZONE/'; }
for s in long-iso iso '+%Y/%m/%d' $'+old %Y\nnew %H' locale posix-long-iso full-iso; do
	printf '%s\n' "--- $s"; ls -l --time-style="$s" new old | shape
done
echo "--- --full-time"; ls -l --full-time old | shape
echo "--- a bad style"; ls -l --time-style=bogus old 2>&1; echo "rc=$?"
cd .. && rm -rf "$d"
