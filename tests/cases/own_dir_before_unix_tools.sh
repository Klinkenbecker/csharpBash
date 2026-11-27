# 2026-11-24 (bugs list 7; DECISIONS 2026-11-17 "RECORD (batten down, fourth audit)" item 4): USING's startup step 3 says a nested `bash` and `sh` are this
# shell even when another comes first; the 2026-11-15 refusal-protocol ruling puts this shell's own folder ahead of the
# unix-tools chain, which is fallback only (the architect, 2026-11-24). A folder named BEFORE PATH in unix-tools was
# searched ahead of this shell's own folder (Fallbacks.SearchPath: it came first in the one search list), so `bash -c`
# ran the folder's bash. Now this shell's own folder comes first; the configured steps follow, in their order.
# The installed layout is built here (a copy of the shell as Bash.exe and sh.exe in a folder of its own), as
# own_dir_first_sh does: the deploy's staging folder held no sh.exe until hg 960. A test home of its own (USERPROFILE). AOT only: a
# lone copy of the managed build cannot run. Authored (Windows-specific: the search list is C#Bash's).
P=$(mktemp -d) || exit 1
mkdir -p "$P/.bash/etc" "$P/tools" "$P/own"
cp "$BASH" "$P/own/Bash.exe" && cp "$BASH" "$P/own/sh.exe" || exit 1
printf '@echo FOLDER-%%~n0\r\n' > "$P/tools/bash.cmd"; cp "$P/tools/bash.cmd" "$P/tools/sh.cmd"
printf '@echo FOLDER-only\r\n' > "$P/tools/onlyinfolder_o7.cmd"
printf 'unix-tools = %s;PATH\n' "$P/tools" > "$P/.bash/etc/csharpBash.config"
USERPROFILE="$P" "$P/own/Bash.exe" -c 'bash -c "echo this shell: bash"; sh -c "echo this shell: sh"; onlyinfolder_o7' < /dev/null 2>&1 | tr -d '\r'
cd / && rm -rf "$P"
