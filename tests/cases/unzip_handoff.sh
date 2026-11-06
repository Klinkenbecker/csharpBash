# 2026-11-05: what the in-process unzip hands to the PATH unzip (DECISIONS "GO: unzip in-process"), and that it writes
# nothing before it does: an encrypted entry, a compression method .NET cannot read (bzip2), a symbolic-link entry,
# an option it does not read (-v), no archive. BASH_COREUTILS=builtin makes the hand-off a message (no PATH program),
# so the output is the same on every machine. Listing such archives, and piping or testing a link, stay in-process.
# Expected: authored (C#Bash's own messages, checked by hand; GNU has no such hand-off).
w=$(mktemp -d) && cd "$w" || exit 1
base64 -d > enc.zip <<'EOF'
UEsDBBQAAAAAAOtwZVjQ+RU5BgAAAAYAAAAJAAAAcGxhaW4udHh0cGxhaW4KUEsDBBQAAQAAAOtw
ZVjU2tRzFQAAABUAAAAKAAAAc2VjcmV0LnR4dG5vdCByZWFsbHkgZW5jcnlwdGVkClBLAQIUAxQA
AAAAAOtwZVjQ+RU5BgAAAAYAAAAJAAAAAAAAAAAAAACkgQAAAABwbGFpbi50eHRQSwECFAMUAAEA
AADrcGVY1NrUcxUAAAAVAAAACgAAAAAAAAAAAAAApIEtAAAAc2VjcmV0LnR4dFBLBQYAAAAAAgAC
AG8AAABqAAAAAAA=
EOF
base64 -d > bz.zip <<'EOF'
UEsDBC4AAAAMAOtwZVgV4OiHNAAAAHgAAAAGAAAAYnoudHh0QlpoOTFBWSZTWWgfvC4AAB3JgAAQ
EAAQIEAQIAAwwAKVDJwmE6TSaTSfF3JFOFCQaB+8LlBLAQIuAy4AAAAMAOtwZVgV4OiHNAAAAHgA
AAAGAAAAAAAAAAAAAACkgQAAAABiei50eHRQSwUGAAAAAAEAAQA0AAAAWAAAAAAA
EOF
base64 -d > link.zip <<'EOF'
UEsDBBQAAAAAAOtwZViL15qGBwAAAAcAAAAKAAAAdGFyZ2V0LnR4dHRhcmdldApQSwMEFAAAAAAA
63BlWEm6pAAKAAAACgAAAAgAAABsaW5rLnR4dHRhcmdldC50eHRQSwECFAMUAAAAAADrcGVYi9ea
hgcAAAAHAAAACgAAAAAAAAAAAAAApIEAAAAAdGFyZ2V0LnR4dFBLAQIUAxQAAAAAAOtwZVhJuqQA
CgAAAAoAAAAIAAAAAAAAAAAAAAD/oS8AAABsaW5rLnR4dFBLBQYAAAAAAgACAG4AAABfAAAAAAA=
EOF

r() { echo "--- unzip ${*:2}"; BASH_COREUTILS=builtin "$@" 2>err; echo "[rc=$?]"; sed 's/^/ERR: /' err; }
r unzip enc.zip
r unzip -p enc.zip
r unzip bz.zip
r unzip -t bz.zip
r unzip link.zip
r unzip -v enc.zip
r unzip
r unzip -lt enc.zip
echo "written: $(ls | grep -v -e '\.zip$' -e '^err$' | tr '\n' ' ')"
r unzip -l enc.zip
r unzip -l bz.zip
r unzip -p link.zip link.txt; echo
r unzip -t link.zip
r unzip -o enc.zip plain.txt
cat plain.txt
cd / && rm -rf "$w"
