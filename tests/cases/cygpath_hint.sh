# 2026-11-12 (daq-a1's need; the hint RULED 2026-11-07, "get over it, you don't need cygpath", never built until now):
# C#Bash has no cygpath and needs none. `cygpath` is "command not found", 127, and the next line says what to use
# instead; it is never handed to WSL (which has none). The same two lines reach a redirect. Authored.
d=$(mktemp -d) && cd "$d" || exit 1
cygpath -m "$d" 2>&1 | sed 's/^.*: line [0-9]*: /bash: /'; echo "rc=${PIPESTATUS[0]}"
cygpath -w x 2> err; echo "rc=$? lines=$(wc -l < err)"
echo "a Windows-rooted path already: $(case $d in [A-Za-z]:/*) echo yes;; *) echo no;; esac)"
cd / && rm -rf "$d"
