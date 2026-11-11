# 2026-11-09 (installer-a4): a here-document inside a command substitution inside double quotes, "$(cat <<'EOF' ...)",
# failed to parse when its body held an apostrophe ("unexpected EOF while looking for matching `\"'"): the end of the
# "$( )" was looked for by scanning the body as shell text. A body is literal, whatever it holds. Expected: GNU via WSL.
x="$(cat <<'EOF'
it's a b
EOF
)"; echo "[$x]"
v=val
y="$(cat <<EOF
it's $v, a "quote", a ) and a \$( and a \`
EOF
)"; echo "[$y]"
z="$(cat <<-'END'
	tab-stripped: don't
	END
)"; echo "[$z]"
w="$(cat <<A; cat <<'B'
first's
A
second
B
)"; echo "[$w]"
s="$(echo $(( 1 << 2 )))"; echo "shift: [$s]"
msg="$(cat <<'EOF'
The architect's message: "quoted" and (parenthesized).
EOF
)"; echo "$msg"
