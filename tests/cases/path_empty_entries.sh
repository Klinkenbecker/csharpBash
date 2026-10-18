# 2026-10-17: an EMPTY entry inside PATH (`;;`) never reaches Windows. Windows' launcher searches the process PATH for
# a name C#Bash did not find and takes such an entry as the current directory, NoDefaultCurrentDirectoryInExePath=1
# notwithstanding: a `.exe` planted in the cwd ran (eight live Claude Code sessions inherited a `;;`). A trailing `;`
# alone was harmless (measured), but C#Bash made it a `;;` when it appended its own folder. Now C#Bash's process PATH
# and every child's PATH carry no empty entry; the shell's $PATH keeps what it was given. Authored (Windows-specific).
d=$(mktemp -d) && cd "$d" || exit 1
cp C:/Windows/System32/whoami.exe zzplant.exe
echo "== a planted .exe in the cwd; C#Bash started (by cmd) with an empty PATH entry, or a trailing ';' and no own folder"
for P in 'C:/Windows/System32;;C:/Windows' 'C:/Windows/System32;C:/Windows;'; do
	printf '@set "PATH=%s"\r\n@set NoDefaultCurrentDirectoryInExePath=1\r\n@"%s" -c "zzplant > /dev/null 2>&1; echo rc=$?; case $PATH in *\;\;*) echo \\"its \\$PATH: has an empty entry\\";; *) echo \\"its \\$PATH: none\\";; esac"\r\n' "$P" "$BASH" > launch.cmd
	cmd //c '.\launch.cmd' | tr -d '\r'
done
echo "== a child gets PATH without empty entries; the shell's \$PATH is as written"
( PATH='C:/Windows/System32;;C:/Windows;'; cmd //c set Path | tr -d '\r' | grep -i '^path=' | sed 's/^[Pp][Aa][Tt][Hh]=/Path=/'; echo "[$PATH]" )
env PATH='C:/Windows/System32;;C:/Windows' cmd //c set Path | tr -d '\r' | grep -i '^path=' | sed 's/^[Pp][Aa][Tt][Hh]=/Path=/'
cd / && rm -rf "$d"
