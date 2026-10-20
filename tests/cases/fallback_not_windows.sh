# 2026-10-19 (red team): an option a builtin lacks hands over to the PATH program of that name, never to Windows' own:
# System32 is ahead of the tools on PATH, and its find.exe (another program) ran: "FIND: Parameter format not
# correct", 2. Windows' sort.exe, timeout.exe, expand.exe are the same case. Authored.
d=$(mktemp -d) && cd "$d" || exit 1
mkdir bin ff
printf '@echo the PATH find ran: %%*\r\n' > bin/find.cmd
case "$PATH" in *\;*) sep=';' ;; *) sep=':' ;; esac
S="${SYSTEMROOT:-C:/Windows}/System32"
PATH="$S$sep$PWD/bin" find ff -okdir echo {} \; ; echo "rc=$?"
PATH="$S" find ff -okdir echo {} \; 2>&1 | grep -o 'C#Bash: .*'; echo "rc=${PIPESTATUS[0]}"
cd / && rm -rf "$d"
