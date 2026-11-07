# 2026-11-07 (guy-e3's review; the architect's "yes"): a Windows program's argument is converted INSIDE it where Git
# Bash converts it: the part after the first `=` (recursively), and after `-` and one letter, each as a whole argument
# would be; an argument holding `;` is left whole (C#Bash converted its first item only: `C:/x;/c/y`); one holding `://`
# or starting with a drive is left. Until now only an argument's start was converted (`--file=/c/x` reached the program
# as typed: the 2026-10-10 rule took Git Bash to do that; it does not). Expected = Git Bash 4.4.23, measured 2026-11-07,
# except the rows marked KEPT (C#Bash's 2026-10-10 rule: a bare /x or / is never converted, a missing drive is left,
# lists get forward slashes). Values computed here, so the case does not depend on this machine's drives.
show() { cmd.exe //c echo "$@" | tr -d '\r'; }
check() { if [ "$2" = "$3" ]; then echo "$1 ok"; else echo "$1 BAD [$2] want [$3]"; fi; }
tmp=$(cd /tmp && pwd)
drives=$(fsutil.exe fsinfo drives 2>/dev/null | tr -d '\r\0')
nd=; for L in q r s t u v w x y z; do U=$(echo "$L" | tr a-z A-Z); case "$drives" in *"$U:"*) ;; *) nd=$L; break ;; esac; done
# after `=`
check eq-opt       "$(show --file=/c/x)"          "--file=C:/x"
check eq-key       "$(show path=/c/x)"            "path=C:/x"
check eq-twice     "$(show a=b=/c/x)"             "a=b=C:/x"
check eq-first     "$(show =/c/x)"                "=C:/x"
check eq-short     "$(show -o=/c/x)"              "-o=C:/x"
check eq-define    "$(show -Dk=/c/x)"             "-Dk=C:/x"
check eq-colon     "$(show x:y=/c/z)"             "x:y=C:/z"
check eq-tmp       "$(show --file=/tmp/x)"        "--file=$tmp/x"
check eq-dbl       "$(show --file=//c)"           "--file=/c"
check eq-list      "$(show --file=/c/a:/c/b)"     "--file=C:/a;C:/b"         # KEPT: Git Bash C:\a;C:\b
check eq-list-rel  "$(show --file=/c/x:rel)"      "--file=/c/x:rel"
check eq-middle    "$(show --file=x/c/y)"         "--file=x/c/y"
check eq-url       "$(show http://h/a=/c/x)"      "http://h/a=/c/x"
check eq-drive     "$(show C:/a=/c/x)"            "C:/a=/c/x"
check eq-start     "$(show /c/x=/c/y)"            "C:/x=/c/y"
check eq-bare      "$(show --file=/c)"            "--file=/c"                # KEPT: Git Bash --file=C:/
check eq-root      "$(show --file=/)"             "--file=/"                 # KEPT: Git Bash its install folder
check eq-no-drive  "$(show --file=/$nd/x)"        "--file=/$nd/x"            # KEPT: no such drive
# after `-` and one letter
check opt          "$(show -I/c/x)"               "-IC:/x"
check opt-tmp      "$(show -I/tmp/x)"             "-I$tmp/x"
check opt-dbl      "$(show -I//c)"                "-I/c"
check opt-list     "$(show -I/c/a:/c/b)"          "-IC:/a;C:/b"              # KEPT: Git Bash -IC:\a;C:\b
check opt-two      "$(show -ab/c/x)"              "-ab/c/x"
check opt-long     "$(show --I/c/x)"              "--I/c/x"
check opt-digit    "$(show -1/c/x)"               "-1/c/x"
check opt-plus     "$(show +I/c/x)"               "+I/c/x"
check opt-dash     "$(show -/c/x)"                "-/c/x"
check opt-bare     "$(show -I/c)"                 "-I/c"                     # KEPT: Git Bash -IC:/
# `;` leaves the argument whole
check semi         "$(show '/c/x;/c/y')"          "/c/x;/c/y"
check semi-end     "$(show '/c/x;')"              "/c/x;"
check semi-rel     "$(show '/c/x;rel')"           "/c/x;rel"
check semi-tmp     "$(show '/tmp/x;/c/y')"        "/tmp/x;/c/y"
check semi-eq      "$(show '--file=/c/x;/c/y')"   "--file=/c/x;/c/y"
check semi-mid     "$(show 'a=/c/x;b')"           "a=/c/x;b"
# this shell run again (a nested bash) reads /c/x itself: its arguments go as typed, as Git Bash's nested bash gets them
check self-shell   "$("$BASH" -c 'echo "$1 $2"' _ /c/x --file=/c/y)"   "/c/x --file=/c/y"
# the off switches
check excl-eq      "$(MSYS2_ARG_CONV_EXCL='--file=' show --file=/c/x)"   "--file=/c/x"
check off-eq       "$(MSYS_NO_PATHCONV=1 cmd.exe /c echo --file=/c/x -I/c/x | tr -d '\r')"   "--file=/c/x -I/c/x"
