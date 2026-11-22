# 2026-11-17 (DECISIONS "BUG FIXED ... an unclosed construct is named by the innermost one; build 882"): an unclosed
# construct is reported by the innermost one left open, as bash's parse_matched_pair does: `"${x:+it's}"` and
# `${x:+it's}` lack a `'`, `"$(echo 'hi)"` a `'`, "`echo hi" inside double quotes a backquote. C#Bash named the outer
# `"` or `}`. The rest are guards that were right. The first line of each message. Expected = GNU bash 5.1 via WSL.
for src in "x=1; echo \"\${x:+it's}\"" "x=1; echo \${x:+it's}" 'echo "$(echo hi"' "echo \"\$(echo 'hi)\"" \
           'echo "${x"' 'echo "a' 'echo "`echo hi"' 'echo ${x' 'echo $(echo hi'; do
	msg=$("$BASH" -c "$src" 2>&1 < /dev/null | head -1 | sed 's/^.*line 1: //')
	printf '%s => %s\n' "$src" "$msg"
done
