# 2026-10-27 (beside koliada-net-28's report): wc and md5sum/sha1sum/sha256sum/sha512sum count and hash as they read,
# a 64 KiB block at a time; the counts must not change where a word, a 2- or 3-byte character or a CRLF straddles a
# block's edge, from a file or a pipe, and the checksums' -c, --tag and stdin forms are as before. Inputs are those on
# which C#Bash's wc already agrees with GNU's (GNU differs on non-printable bytes in words, invalid UTF-8 in -m, the
# display width in -L (a tab, a wide character, CR), pads several counts from stdin to 7 and one count of one file to
# nothing: DECISIONS 2026-10-27), so the CRLF file has no -L. Expected: GNU coreutils 8.32 via WSL (LC_ALL=C.UTF-8).
export LC_ALL=C.UTF-8
cd "${TMPDIR:-${TEMP:-/tmp}}" || exit 1
d="wc-exact-$$"; mkdir -p "$d" && cd "$d" || exit 1
{ head -c 65535 /dev/zero | tr '\0' ' '; printf 'xy rest\n'; } > word
{ head -c 65535 /dev/zero | tr '\0' a; printf '\303\251b\n'; } > two
{ head -c 65534 /dev/zero | tr '\0' a; printf '\342\202\254z\n'; } > three
{ head -c 65535 /dev/zero | tr '\0' a; printf '\r\nb\r\n'; } > crlf
{ head -c 300000 /dev/zero | tr '\0' x; printf '\n'; seq 1 50000; } > long
printf 'caf\303\251 na\303\257ve\nsecond line\n\n  lead  trail  ' > text
: > empty

for f in word two three crlf long text empty; do
	echo "== $f"
	L=-L; [ "$f" = crlf ] && L=
	wc "$f"
	wc -l -w -c -m $L "$f"
	for o in -l -w -c -m $L; do printf '%s %s | ' "$o" "$(cat "$f" | wc $o)"; done; echo
	echo "< $f: $(wc -c < "$f")"
done
echo "== several files"
wc word two text

echo "== checksums"
for t in md5sum sha1sum sha256sum sha512sum; do
	$t text long
	cat text | $t
	$t --tag empty
done
md5sum word two > sums
md5sum -c sums
echo 'c3fcd3d76192e4007dfb496cca67e13b  text' >> sums
md5sum -c sums 2>&1; echo "rc=$?"
md5sum -c --quiet sums 2>&1; echo "rc=$?"

cd .. && rm -rf "$d"
