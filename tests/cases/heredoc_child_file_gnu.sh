# 2026-11-27 (bugs list 16, found fixing bugs list 14): a program given a here-document or here-string gets it through
# a pipe up to 65,536 bytes and as a temporary file beyond, as bash 5.1 hands them over (C#Bash: always a pipe, so
# `[ -f /dev/stdin ]` in the child was false); its size and contents whole either way; the file is gone afterwards
# (counted by this shell's own pid, never the shared TEMP's other files). Expected = GNU bash 5.1 via WSL.
for n in 4096 65536 65537 200000; do
	s=$(head -c $((n - 1)) /dev/zero | tr '\0' a)
	"$BASH" -c 'if [ -f /dev/stdin ]; then k=file; elif [ -p /dev/stdin ]; then k=pipe; else k=other; fi; c=$(wc -c); echo "$k $c"' <<< "$s" | sed "s/^/here-string of $n bytes, child: /"
done
s=$(head -c 99999 /dev/zero | tr '\0' b)
"$BASH" -c 'head -c 3; echo; [ -s /dev/stdin ] && echo nonempty' <<EOF
$s
EOF
echo "temporary files left: $(ls "${TMPDIR:-/tmp}" | grep -c "^bash-heredoc-$$-")"
