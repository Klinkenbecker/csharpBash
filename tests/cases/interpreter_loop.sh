# 2026-10-17: a chain of #! interpreters is capped. `#!/usr/bin/env self` re-entered the launcher until the stack
# overflowed and the shell died (a script found on PATH and `./self` alike); GNU execs forever. Now the chain stops
# with "bad interpreter", 126, its stderr redirect honoured, and the shell goes on.
# Authored (C#Bash-specific: GNU has no limit).
d=$(mktemp -d) && cd "$d" || exit 1
mkdir bin
printf '#!/usr/bin/env selfy\n' > bin/selfy
printf '#!/usr/bin/env mb\n' > bin/ma
printf '#!/usr/bin/env ma\n' > bin/mb
export PATH="$d/bin;$PATH"
msg() { sed 's/^.*line [0-9]*: //'; }
selfy 2>&1 | msg; echo "rc=${PIPESTATUS[0]}"
./bin/selfy 2>&1 | msg; echo "rc=${PIPESTATUS[0]}"
ma 2>&1 | msg; echo "rc=${PIPESTATUS[0]}"
selfy 2>/dev/null; echo "quiet: rc=$?"
echo "the shell goes on"
cd / && rm -rf "$d"
