# 2026-10-31 (installer-a4's unmet need; the architect: "yes, go"): `dd` in-process, as GNU coreutils 8.32. PATH found
# only a 1997 port here (UnixUtils' "GNU fileutils 3.16"), which refused `status=none`. Records as GNU counts them
# (bs= writes each block as read; ibs/obs reblock), the report on stderr in GNU's words and sizes (its time and rate
# are not compared), count/skip/seek on files and pipes, truncation, conversions, flags, suffixes, devices, and every
# error GNU gives, exit 1. Record counts are taken on files: a pipe may hand over less than a block on either side.
# Expected: GNU coreutils 8.32 via WSL, LC_ALL=C.
export LC_ALL=C
d=$(mktemp -d) && cd "$d" || exit 1
st() { sed -E 's/, [0-9.e+-]+ s, [0-9.,]+ [kMGT]?B\/s$/, T s, R/'; }
echo "== the report: a byte into a file, notrunc, status=none"
printf 'abcdefgh' > f; printf 'X' | dd of=f bs=1 seek=3 conv=notrunc status=none; echo "rc=$?"; cat f; echo
echo "== the report's sizes"
for n in 0 1 2 999 1000 1023 1024 9999 15000 99999 999999 1048576 10485760; do head -c $n /dev/zero > z; dd if=z bs=1M 2>&1 >/dev/null | st; done
printf 'hello' | dd 2>&1 >/dev/null | st
printf 'hello' | dd status=noxfer 2>&1 >/dev/null
printf 'hello' | dd status=progress 2>&1 >/dev/null | st
echo "== records: ibs/obs reblock, bs= block for block"
printf 'abcdefg' > g; dd if=g ibs=3 obs=2 2>&1 >/dev/null | st; dd if=g bs=3 2>&1 >/dev/null | st; dd if=g ibs=2 2>&1 >/dev/null | st
echo "== count, skip (a file, a pipe)"
printf '0123456789' > n; dd if=n bs=2 skip=1 count=2 status=none; echo; printf '0123456789' | dd bs=2 skip=1 count=2 status=none; echo
dd if=n bs=3 count=0 2>&1 >/dev/null | st
echo "== seek: truncates without notrunc; past the end fills zeros"
printf 'abcdefgh' > t; printf 'XY' | dd of=t bs=1 seek=2 status=none; od -c t | head -2
printf 'ab' > s; printf 'Z' | dd of=s bs=1 seek=4 status=none; od -c s | head -2
echo "== of=: a new file; an existing one truncated"
printf 'hello' | dd of=new status=none; cat new; echo; printf 'hi' | dd of=new status=none; cat new; echo
echo "== conv"
printf 'Hello\n' | dd conv=ucase status=none; printf 'Hello\n' | dd conv=lcase status=none
printf 'abc' | dd bs=5 conv=sync status=none | od -c | head -2
printf 'abcde' | dd conv=swab status=none; echo
printf 'abcde' > sw; dd if=sw conv=swab bs=3 status=none; echo   # an odd block's last byte carries into the next
dd if=sw conv=swab bs=3 2>&1 >/dev/null | st   # and swab reblocks, bs= or not
echo "== bs= writes each read as a record; ibs/obs gathers (a pipe that hands over 2 bytes, then 2 more a second later)"
{ printf ab; sleep 1; printf cd; } | dd bs=4 2>&1 >/dev/null | st
{ printf ab; sleep 1; printf cd; } | dd ibs=4 obs=4 2>&1 >/dev/null | st
printf 'abc' | dd conv=noerror,fsync status=none; echo
echo "== excl, nocreat"
dd if=/dev/null of=f conv=excl status=none 2>&1; echo "rc=$?"
dd if=/dev/null of=nope conv=nocreat status=none 2>&1; echo "rc=$?"; [ -e nope ] && echo "nope was made"
echo "== flags"
dd if=n iflag=skip_bytes,count_bytes skip=3 count=4 bs=2 status=none; echo
printf 'abcdefgh' > o; printf 'XY' | dd of=o oflag=seek_bytes seek=5 conv=notrunc bs=4 status=none; cat o; echo
printf 'ab' > a; printf 'cd' | dd of=a oflag=append conv=notrunc status=none; cat a; echo
head -c 3000 /dev/zero > big; dd if=big bs=1K count=2 iflag=fullblock 2>&1 >/dev/null | st
echo "== number suffixes"
head -c 5000 /dev/zero > fivek; for b in 1c 1w 1b 1K 1k 1kB 2x3 1KiB 2b 1x1x7; do printf '%s: ' $b; dd if=fivek bs=$b count=1 2>/dev/null | wc -c; done
echo "== devices"
dd if=/dev/zero bs=4 count=2 status=none | od -c | head -2
printf 'x' | dd of=/dev/null 2>&1 | st
echo "== errors, each exit 1"
for e in bs=none bogus=1 noequals if=missing conv=frob status=bogus iflag=frob oflag=frob bs=0 count=-1 of=dir/x conv=ucase,lcase conv=excl,nocreat; do
	dd $e < /dev/null 2>&1 | st; echo "rc=${PIPESTATUS[0]}"
done
mkdir dd1; dd if=dd1 2>&1 | st; echo "rc=${PIPESTATUS[0]}"
printf 'Z' | dd bs=1 seek=2 status=none 2>err | wc -c; cat err
cd / && rm -rf "$d"
