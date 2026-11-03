# 2026-11-03 (koliada-net-73's need; the architect: "accept and go"): `pwd -W`, Git Bash's "print the Win32 value of
# the physical directory", is accepted alone or combined and prints what pwd prints here (the drive form, F:/...); it
# was "pwd: -W: invalid option", 2. Operands are ignored, as in bash. The usage line is Git Bash's [-LPW].
# Authored: GNU bash has no -W; Git Bash's `pwd -W` prints this same form.
cd "${TMPDIR:-${TEMP:-/tmp}}" || exit 1
[ "$(pwd -W)" = "$(pwd)" ] && echo "pwd -W = pwd"
[ "$(pwd -LW)" = "$(pwd)" ] && echo "pwd -LW = pwd"
[ "$(pwd -WP)" = "$(pwd)" ] && echo "pwd -WP = pwd"
pwd -W extra > /dev/null; echo "an operand is ignored: rc=$?"
case "$(pwd -W)" in [A-Za-z]:/*) echo "the drive form" ;; *) echo "not the drive form: $(pwd -W)" ;; esac
pwd -Z 2>&1 | sed 's/^.*pwd: -Z/pwd: -Z/'
