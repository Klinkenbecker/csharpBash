# 2026-10-20: moved here from native_args (2026-10-10), where `git -C /c/repo` was the report: a Windows program gets
# /c/x at the start of an argument as C:/x. Needs git on PATH; SKIP without it (native_args printed "git ok" then, a
# pass that could not fail; it still checks the conversion with cmd.exe). Authored.
command -v git > /dev/null || { echo "SKIP: needs git on PATH"; exit 0; }
r=$(git -C "/c/Windows" rev-parse --is-inside-work-tree 2>&1); case $r in *"cannot change to"*) echo "git BAD [$r]";; *) echo "git ok";; esac
