# 2026-11-03 (koliada-net-7d's report): whoami given an operand refuses it as GNU coreutils does ("extra operand",
# exit 1). It was ignored: `whoami /groups` (Windows' whoami.exe usage) printed the user name and 0, so a check for an
# integrity label found nothing and looked like an answer. Windows' own same-name programs are never run (FEATURES).
# Expected: GNU coreutils 8.32 via WSL, LC_ALL=C.
export LC_ALL=C
whoami /groups 2>&1; echo "rc=$?"
whoami a b 2>&1; echo "rc=$?"
whoami -- x 2>&1; echo "rc=$?"
whoami > /dev/null; echo "no operand: rc=$?"
[ "$(whoami)" = "$(id -un)" ] && echo "whoami = id -un"
