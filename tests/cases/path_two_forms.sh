# 2026-10-31 (the architect: "Yes, two paths is the right thing; go"): $PATH has two forms. INSIDE the shell it is
# GNU/MSYS form, `/c/x:/f/y`, whatever form it arrives in (the inherited environment, an assignment in Windows, MSYS
# or mixed form), so `IFS=:` gives its entries; empty entries stay as given (RULED 2026-10-17), a one-letter relative
# entry is written `./A` (`A:/c/x` reads as a drive). At every EXIT (a child's environment, `PATH=... cmd` too) it is
# Windows form with FORWARD slashes, `C:/x;F:/y`: never a backslash, never an empty entry. A Windows program's argument
# that is a colon list of /-rooted paths is converted whole; one with any other colon is left as typed (Git Bash's
# rule, measured 2026-10-31; Git Bash writes backslashes). Until then $PATH read `C:\x;F:\y` and only an argument's
# start was converted (`C:/Windows:/c/Users`). Authored (Windows-specific).
d=$(mktemp -d) && cd "$d" || exit 1
child() { cmd /C "echo %PATH%" | tr -d '\r'; }

echo "== a shell started by cmd with a Windows-form PATH (backslashes, an empty entry, a trailing ;)"
cat > probe.sh <<'EOF'
p=${PATH%:*}   # without the shell's own folder, appended last
echo "[$p]"
case $PATH in *\;* | *\\*) echo "Windows text in \$PATH";; *) echo "no ; and no backslash in \$PATH";; esac
type -P whoami
c=$(cmd /C "echo %PATH%" | tr -d '\r'); echo "its child: ${c%;*}"
EOF
printf '@set "PATH=%s"\r\n@"%s" probe.sh\r\n' 'C:\Windows\System32;;C:\Windows;' "$BASH" > launch.cmd
cmd //c '.\launch.cmd' | tr -d '\r'

echo "== an assignment in any form reads in MSYS form; a child gets forward-slash Windows form"
for P in 'C:\Windows\System32;C:\Windows' 'C:/Windows/System32;C:/Windows' '/c/Windows/System32:/c/Windows' 'C:/Windows/System32:/c/Windows/'; do
	( PATH=$P; echo "$PATH"; child )
done

echo "== an empty entry stays in \$PATH and never reaches a child"
( PATH='/c/Windows/System32::/c/Windows:'; echo "[$PATH]"; child )
( PATH='C:/Windows/System32;;C:/Windows'; echo "[$PATH]"; child )

echo "== IFS=: gives the entries; a one-letter relative entry is ./A"
( PATH='C:\Windows\System32;A;C:\Windows'; echo "$PATH"; IFS=: read -r -a e <<< "$PATH"; echo "${#e[@]} entries"; for x in "${e[@]}"; do [ -d "$x" ] && echo "a folder: $x"; done )

echo "== PATH=... cmd: the child gets exactly those entries, forward slashes, in whatever form they were written"
mkdir stub
PATH="$d/stub;C:/Windows/System32" cmd /C "echo %PATH%" | tr -d '\r' | sed "s|$d|D|"
PATH='C:\Windows\System32;C:\Windows' cmd /C "echo %PATH%" | tr -d '\r'
PATH=/c/Windows/System32:/c/Windows cmd /C "echo %PATH%" | tr -d '\r'
echo "== an isolation PATH never falls back to the inherited one (kes-log's): a positive and a negative control"
PATH="$d/stub;C:/Windows/System32" cmd /C whoami > /dev/null 2>&1; echo "System32 on it: rc=$?"
PATH="$d/stub;C:/Windows" cmd /C whoami 2>&1 | tr -d '\r'

echo "== export -p round trip; a nested shell"
( PATH='/c/Windows/System32:/c/Windows'; s=$(export -p | grep ' PATH='); echo "$s"; PATH=/nowhere; eval "$s"; echo "$PATH"
  "$BASH" -c 'IFS=: read -r a b rest <<< "$PATH"; echo "nested: $a $b"' )

echo "== a Windows program's colon-list argument: converted whole, or left as typed"
for a in /c/Windows:/c/Users /c/Windows/System32 /c/Windows:rel /c/Windows: /c/Windows::/c/Users /c/x/a:b http://x/c:/d; do
	printf '%s -> ' "$a"; cmd /C echo "$a" | tr -d '\r'
done
cd / && rm -rf "$d"
