# 2026-11-03 (the architect: "we still need ~/.bash/bin to be first on the path?", "Yes, do it"): the shell puts its own
# folder FIRST on its own PATH, so a nested `bash` is this shell even when another bash.exe comes first on the PATH it
# inherits (WSL's launcher in System32, Git's). It was appended at the end, so the other one ran. A decoy bash.exe (a
# copy of hostname.exe) stands in for it. The folder already on PATH moves to the front once. Authored (Windows).
cd "${TMPDIR:-${TEMP:-/tmp}}" || exit 1
d="own-dir-first-$$"; mkdir -p "$d" || exit 1
cp /c/Windows/System32/hostname.exe "$d/bash.exe"
own=$(dirname "$BASH")
PATH="$PWD/$d:$PATH:$own" "$BASH" -c '
  case "$(bash -c "echo \$BASH_VERSION" 2>&1)" in *koliada*) echo "a nested bash is this shell" ;; *) echo "a nested bash is another program" ;; esac
  [ "$(type -P bash)" = "$(dirname "$BASH")/Bash.exe" ] && echo "type -P bash names this shell"
  echo "its folder on PATH: $(echo "$PATH" | tr ":" "\n" | grep -cxF "$(dirname "$BASH" | sed "s#^\([A-Za-z]\):#/\L\1#")") time(s), first: $([ "${PATH%%:*}" = "$(dirname "$BASH" | sed "s#^\([A-Za-z]\):#/\L\1#")" ] && echo yes)"'
rm -rf "$d"
