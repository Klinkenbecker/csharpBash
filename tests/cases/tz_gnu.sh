# 2026-10-30 (the architect: "fix it"): TZ is read as glibc reads it, by every tool that prints or parses a time. It was
# ignored everywhere: `TZ=UTC date` printed local time. Set but empty is UTC; UTC/GMT names are UTC under that name; a
# POSIX NAME+-h[:mm] is a fixed offset, the sign WEST (UTC+3 is -0300); an IANA name is that zone; an unknown name is
# UTC under its leading letters. %Z is compared only where it comes from TZ itself: .NET has no tzdata abbreviations
# (an IANA zone's %Z is numeric here, `+09`; glibc says JST). A fixed epoch, so the machine's own zone never shows.
# Expected: GNU coreutils 8.32 / findutils 4.8 via WSL, LC_ALL=C.
export LC_ALL=C
cd "${TMPDIR:-${TEMP:-/tmp}}" || exit 1
d="tz-$$"; mkdir -p "$d" && cd "$d" || exit 1
for tz in UTC GMT UTC0 Etc/UTC "" Foo/Bar EST5 UTC+3 '<+0530>-5:30' EST5EDT CET-1CEST; do
	printf '%-16s %s\n' "[$tz]" "$(TZ=$tz date -d @1700000000 '+%F %T %z %Z')"
done
for tz in Asia/Tokyo :Europe/London America/New_York; do
	printf '%-16s %s\n' "[$tz]" "$(TZ=$tz date -d @1700000000 '+%F %T %z')"
done
echo "--- a time read in TZ"
echo "UTC $(TZ=UTC date -d '2020-01-02 03:04:05' +%s)  Tokyo $(TZ=Asia/Tokyo date -d '2020-01-02 03:04:05' +%s)"
echo "--- touch -d in TZ, then every tool"
printf x > f; TZ=UTC touch -d '2020-01-02 03:04:05' f
TZ=UTC ls -l --full-time f | awk '{print $6, $7, $8}'
TZ=UTC stat -c %y f
TZ=UTC find f -printf '%TY-%Tm-%Td %TH:%TM %Tz %TZ\n'
TZ=UTC date -r f '+%F %T %z'
TZ=Asia/Tokyo date -r f '+%F %T %z'
TZ=EST5 ls -l --time-style=long-iso f | awk '{print $6, $7}'
echo "epoch $(date -r f +%s)"
cd .. && rm -rf "$d"
