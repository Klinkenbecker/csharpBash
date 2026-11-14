# 2026-11-11 (features list 3, the architect's rulings and go): a command C#Bash does not provide in-process is looked
# for by the steps of `unix-tools` in %USERPROFILE%/.bash/etc/csharpBash.config, in the user's order: folders of Unix
# tools, PATH, WSL; a step left out is not used; no file or no line: PATH;WSL. The file is read once, at the first
# lookup that needs it, never at startup; a bad line warns once and is skipped. Folders are seen by type/command -v as
# PATH is; WSL never is. A changed folder list is a fresh remembered map (nothing stale runs). In WSL a tool runs with
# F:/x arguments as /mnt/f/x; a tool WSL lacks is this shell's "command not found", 127; the folder is not held after.
# Authored (Windows-specific: WSL behind a Windows shell). Each child shell gets USERPROFILE pointing here.
P=$(mktemp -d) || exit 1
mkdir -p "$P/.bash/etc" "$P/tools" "$P/empty"
printf '#!/bin/sh\necho "folder mytool: $*"\n' > "$P/tools/mytool"
printf '#!/bin/sh\necho "folder cmd"\n' > "$P/tools/cmd"
conf() { printf '%s\n' "$@" > "$P/.bash/etc/csharpBash.config"; }
run() { USERPROFILE="$P" "$BASH" -c "$1" < /dev/null 2>&1 | sed "s|$P|P|g; s|^.*: line [0-9]*: |bash: |"; }

echo "== a folder ahead of PATH"
conf "unix-tools = $P/tools;PATH"
run 'mytool a b; type mytool; command -v mytool; cmd //c "echo real cmd"'
echo "== the folder after PATH"
conf "unix-tools = PATH;$P/tools"
run 'cmd //c "echo real cmd"; mytool c'
echo "== PATH only: the folder is gone (nothing remembered runs), and no WSL"
conf "unix-tools = PATH"
run 'mytool d; echo "rc=$?"; nosuchtool_fc; echo "rc=$?"'
echo "== a bad file: nothing read for a builtin; one warning each, once, at the first lookup"
conf "unix-tools = $P/no-such-folder;PATH" "bogus line"
run 'echo builtin only'
run 'cmd //c "echo first"; cmd //c "echo second"'
echo "== WSL"
if [ ! -x C:/Windows/System32/wsl.exe ] || ! WSLENV= C:/Windows/System32/wsl.exe -e true < /dev/null > /dev/null 2>&1; then
	echo "SKIP: no working WSL here"
else
	export WSLENV=
	conf "unix-tools = PATH;WSL"
	printf 'a b\nb c\n' > "$P/pairs.txt"
	run "PATH='$P/empty' tsort '$P/pairs.txt' | tr '\n' ' '; echo \"rc=\${PIPESTATUS[0]}\""
	printf 'some text\0' > "$P/st.bin"   # an option C#Bash's strings lacks (-d) goes the same way
	run "PATH='$P/empty' strings -d '$P/st.bin'; echo \"rc=\$?\""
	run "PATH='$P/empty' nosuchtool_fc_wsl x; echo \"rc=\$?\"; type nosuchtool_fc_wsl tsort 2>&1 | sed 's/^bash: line [0-9]*: //'"
	conf "unix-tools = PATH"
	run "PATH='$P/empty' tsort '$P/pairs.txt'; echo \"rc=\$?\""
fi
cd / && rm -rf "$P" && echo "removed"
