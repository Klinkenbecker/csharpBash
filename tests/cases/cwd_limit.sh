# 2026-10-19 (red team F10): starting a program from a directory at Windows' limit. A directory of 258 characters
# works; from 259 on Windows refuses (the directory, its trailing \ and the NUL in 260), and C#Bash says so, 126. At
# exactly 259 Windows' own "The directory name is invalid" came out with 127 ("command not found" to a script).
# Authored: GNU has no such limit.
base=$(mktemp -d) || exit 1
for n in 258 259 260; do
  pad=$((n - ${#base} - 1))
  dir="$base/$(printf '%*s' "$pad" '' | tr ' ' a)"
  mkdir -p "$dir" && cd "$dir" || exit 1
  C:/Windows/System32/hostname.exe > /dev/null 2> err; rc=$?
  echo "cwd ${#PWD}: rc=$rc $(sed 's/^.*hostname.exe: //' err)"
  cd "$base"
done
cd / && rm -rf "$base"
