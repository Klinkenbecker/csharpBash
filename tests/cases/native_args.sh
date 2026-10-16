# 2026-10-10: a Windows program gets an MSYS-form path at the START of an argument in Windows form, as under
# Git Bash: /c/x -> C:/x (a drive that exists), /tmp/x -> the temp directory. Never a bare /c (cmd /c), never
# inside an argument, never for an MSYS program (Git's usr/bin, which reads /c/x itself); off with
# MSYS_NO_PATHCONV or MSYS2_ARG_CONV_EXCL. Every argument used to reach the program verbatim (`git -C /c/repo`
# failed). Expected output: authored (Windows-specific; GNU bash has no such conversion); values are computed
# here, so the case does not depend on this machine's drives.
show() { cmd.exe /c echo "$@" | tr -d '\r'; }
check() { if [ "$2" = "$3" ]; then echo "$1 ok"; else echo "$1 BAD [$2] want [$3]"; fi; }
tmp=$(cd /tmp && pwd)
nd=; for L in q r s t u v w x y z; do [ -d "$L:/" ] || { nd=$L; break; }; done
check drive      "$(show /c/Windows)"            "C:/Windows"
check root       "$(show /c/)"                   "C:/"
check tmp        "$(show /tmp/x)"                "$tmp/x"
check tmp-bare   "$(show /tmp)"                  "$tmp"
check bare-c     "$(show /c)"                    "/c"
check inside     "$(show --file=/c/x)"           "--file=/c/x"
check middle     "$(show x/c/y)"                 "x/c/y"
check unc        "$(show //host/share/x)"        "//host/share/x"
check no-drive   "$(show /$nd/x)"                "/$nd/x"
check backslash  "$(show '/c/a\b')"              'C:/a\b'
check off        "$(MSYS_NO_PATHCONV=1 show /c/Windows)"              "/c/Windows"
check excl-all   "$(MSYS2_ARG_CONV_EXCL='*' show /c/Windows)"         "/c/Windows"
check excl-pref  "$(MSYS2_ARG_CONV_EXCL='/c/W' show /c/Windows /c/Users)" "/c/Windows C:/Users"
# 2026-10-13: `//x` is Git Bash's escape for a Windows switch: one leading / goes, \ becomes /; two slashes with a
# later / is UNC and kept; a glob or quote character leaves it alone (measured against Git Bash). `cmd //c` started
# an interactive cmd, which waits on standard input (here NUL: the banner instead of `hi`).
check cmd-dbl-c  "$(cmd //c echo hi </dev/null | tr -d '\r')"         "hi"
check dbl        "$(show //c //nologo // ///x)"                       "/c /nologo / //x"
check dbl-bs     "$(show '//a\b\c' '//c\')"                           "/a/b/c /c/"
check dbl-unc    "$(show //c/x //host/share '//a\b/c' ///x/y)"        "//c/x //host/share //a/b/c //x/y"
check dbl-alone  "$(show '//c*' '//c]x' '//c`x')"                    '//c* //c]x //c`x'
check dbl-off    "$(MSYS_NO_PATHCONV=1 show //c)"                     "//c"
check dbl-excl   "$(MSYS2_ARG_CONV_EXCL='//c' show //c //k)"          "//c /k"
if m=$(type -P echo.exe) && [ -f "$(dirname "$m")/msys-2.0.dll" ]; then
  check msys     "$("$m" /c/Windows)"            "/c/Windows"
else echo "msys ok"; fi   # no Git usr/bin here: nothing to check
r=$(git -C "/c/Windows" rev-parse --is-inside-work-tree 2>&1); case $r in *"cannot change to"*) echo "git BAD [$r]";; *) echo "git ok";; esac
