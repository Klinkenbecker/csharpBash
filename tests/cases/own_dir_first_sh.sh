# 2026-11-03: with the shell's own folder first on its PATH, a nested `sh` is its sh.exe even when another sh.exe comes
# first on the PATH it inherits (Git's, UnixUtils'): the installed layout, Bash.exe and sh.exe in one folder. A decoy
# sh.exe (a copy of hostname.exe) stands in for the other. AOT only: the managed build cannot run renamed. Authored.
cd "${TMPDIR:-${TEMP:-/tmp}}" || exit 1
d="own-dir-first-sh-$$"; mkdir -p "$d/decoy" "$d/bin" || exit 1
cp /c/Windows/System32/hostname.exe "$d/decoy/sh.exe"
cp "$BASH" "$d/bin/Bash.exe" && cp "$BASH" "$d/bin/sh.exe"
PATH="$PWD/$d/decoy:$PATH" "$d/bin/Bash.exe" -c '
  case "$(sh -c "echo \$BASH_VERSION" 2>&1)" in *koliada*) echo "a nested sh is this shell'"'"'s sh.exe" ;; *) echo "a nested sh is another program" ;; esac'
rm -rf "$d"
