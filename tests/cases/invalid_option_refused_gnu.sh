# 2026-11-27 (bugs list 13, web-fe, 2026-11-25; the architect's go on Claude's position): a short option that is no
# letter or digit is in no version of the tool, so C#Bash refuses it in GNU's words and status instead of handing it on
# (an older tool on the chain blamed another option, `invalid option -- m`, or exited 255). `grep -m1 -F "- x"` reads
# `- x` as options, as GNU's does. Every in-process tool measured. Expected = GNU coreutils 8.32, grep, diffutils via WSL.
d=$(mktemp -d) && cd "$d" || exit 1
printf 'x\n- x\n' > f
echo '== grep -m1 -F "- x" f'; grep -m1 -F "- x" f 2>&1; echo "rc=$?"
for t in grep cut sort ls diff cmp env timeout head wc uniq cat tail od date tee nl fold paste comm seq stat touch \
	mkdir rm cp mv ln du df chmod realpath basename dirname readlink mktemp tac rev split md5sum sha256sum base64 cksum \
	dd iconv sed xargs; do
	out=$("$t" -% 2>&1 < /dev/null); rc=$?; n=3; [ "$t" = sed ] && n=2   # sed: GNU prints its whole usage, C#Bash its usage line
	printf '%s rc=%s: %s\n' "$t" "$rc" "$(printf '%s\n' "$out" | head -$n | tr '\n' '|')"
done
cd / && rm -rf "$d"
