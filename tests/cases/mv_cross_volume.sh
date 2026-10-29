# 2026-10-29: mv to another filesystem whose source cannot be removed. C#Bash said nothing and returned 0, leaving the
# source AND a copy (Windows' move copies across volumes and "succeeds" when it cannot delete the source); GNU says
# "cannot remove" and returns 1, both files left. A plain file across filesystems must still simply move.
# The unremovable source: on Linux a file in a read-only directory; on Windows a RUNNING program (a copy of this shell):
# it can be opened for deletion, so the move copies it, but not deleted. (A deny-delete ACL is refused up front, and
# a read-only attribute or a file this shell holds open is deleted: neither shows the bug.) The program is known to be
# running when it has printed its pid into the pipe, so no pause is needed; one started and moved at once raced a
# brief hold on the fresh copy. Filesystems: Linux /tmp against the starting directory (WSL: /mnt/f); Windows HOME's
# drive against TEMP's. SKIP when they are one. Expected: GNU coreutils 8.32 via WSL.
same() { echo "SKIP: needs two filesystems ($1)"; exit 0; }
show() { local line; while IFS= read -r line; do printf '%s\n' "${line//"$2"/$1}"; done; }
held_move() {
	echo "--- a source that cannot be removed, to another filesystem"
	mv "$src" "$b/held.exe" 2>&1 | show SRC "$src"; echo "rc=${PIPESTATUS[0]}"
	[ -e "$src" ] && echo "source kept: yes" || echo "source kept: no"
	[ -e "$b/held.exe" ] && echo "copy made: yes" || echo "copy made: no"
}
if [ -r /proc/version ]; then
	a=/tmp/csb-mv-$$; b=$PWD/csb-mv-$$
	mkdir -p "$a/ro" "$b" || exit 1
	[ "$(stat -c %d "$a")" != "$(stat -c %d "$b")" ] || { rm -rf "$a" "$b"; same "$a $b"; }
	src=$a/ro/held.exe; echo data > "$src"; chmod a-w "$a/ro"
	held_move
	chmod u+w "$a/ro"
else
	drive() { local p=${1#/}; printf '%s' "${p:0:1}" | tr a-z A-Z; }
	a=$HOME/.csb-mv-$$; b=${TEMP:-$HOME}/csb-mv-$$
	[ "$(drive "$a")" != "$(drive "$b")" ] || same "$a $b"
	mkdir -p "$a/ro" "$b" || exit 1
	src=$a/ro/held.exe; cp "$BASH" "$src"
	"$src" -c 'echo $$; sleep 30' < /dev/null | { read -r pid; held_move; kill "$pid"; }
fi

echo "--- a plain file to another filesystem"
echo plain > "$a/plain"
mv "$a/plain" "$b/plain"; echo "rc=$?"
[ -e "$a/plain" ] && echo "source kept: yes" || echo "source kept: no"
cat "$b/plain"
rm -rf "$a" "$b"
