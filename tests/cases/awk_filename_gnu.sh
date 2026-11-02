# 2026-11-01 (found by Claude; the architect: "ok, go"): awk's FILENAME is "-" while standard input is read (no
# operand, an explicit "-", getline in BEGIN, END after it, even when it was empty), as gawk 5.1; "" only before any
# input; a file's own name otherwise. It was "" for standard input. Expected: gawk 5.1 via WSL.
cd "${TMPDIR:-${TEMP:-/tmp}}" || exit 1
d="awk-filename-$$"; mkdir -p "$d" && cd "$d" || exit 1
printf 'a\n' > f1.txt
echo "no operand [$(echo x | awk '{ print FILENAME }')]"
echo "an explicit - [$(echo x | awk '{ print FILENAME }' -)]"
echo "in BEGIN [$(echo x | awk 'BEGIN { printf "%s", FILENAME }')]"
echo "END after it [$(echo x | awk 'END { print FILENAME }')]"
echo "END, empty input [$(printf '' | awk 'END { print FILENAME }')]"
echo "/dev/stdin [$(echo x | awk '{ print FILENAME }' /dev/stdin)]"
echo "getline in BEGIN [$(echo x | awk 'BEGIN { getline; print FILENAME }')]"
echo "a file, then stdin [$(echo x | awk '{ printf "%s ", FILENAME }' f1.txt -)]"
cd .. && rm -rf "$d"
