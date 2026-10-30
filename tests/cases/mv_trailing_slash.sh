# 2026-10-30: a destination written with a trailing slash names a directory. `mv file nodir/` (nodir missing) renamed
# the file to nodir and returned 0; GNU refuses ("Not a directory", rc 1) and leaves the file. A DIRECTORY moved to a
# missing name with a slash is a rename, in GNU too. Expected: GNU coreutils 8.32 via WSL.
cd "${TMPDIR:-${TEMP:-/tmp}}" || exit 1
d="mv-slash-$$"; mkdir -p "$d" && cd "$d" || exit 1
show() { ls -1p | tr '\n' ' '; echo; }
echo f > f; echo g > g; mkdir dir src

echo "--- a file to a missing name/"
mv f nodir/ 2>&1; echo "rc=$?"; show
echo "--- a file to an existing file/"
mv f g/ 2>&1; echo "rc=$?"; show
echo "--- a file into an existing dir/"
mv f dir/ 2>&1; echo "rc=$?"; show; ls dir
echo "--- a directory to a missing name/: a rename"
mv src newdir/ 2>&1; echo "rc=$?"; show
echo "--- two files to a missing name/"
echo h > h; mv g h nodir/ 2>&1; echo "rc=$?"; show
echo "--- cp: a file to a missing name/"
cp g nodir/ 2>&1; echo "rc=$?"; show
echo "--- cp: a file to an existing file/"
cp g h/ 2>&1; echo "rc=$?"; show
cd .. && rm -rf "$d"
