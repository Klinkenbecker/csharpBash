# 2026-11-07 (guy-e3's review; the architect's "yes"): a Windows program's ENVIRONMENT is converted as Git Bash converts
# it: each value (exported, or `VAR=x cmd`) as an argument would be. Off with MSYS_NO_PATHCONV; MSYS2_ARG_CONV_EXCL does
# not touch it (Git Bash converts under `*`); MSYS2_ENV_CONV_EXCL is `*` or `;`-separated prefixes of NAME=value,
# case-sensitive. PATH keeps its own rule (the exit form). Every value reached the program as typed: `P=/e/x prog` gave
# a .NET program `/e/x`, which it resolved as C:/e/x. Expected = Git Bash 4.4.23, measured 2026-11-07, except the rows
# marked KEPT (as native_args_within). Values computed here.
check() { if [ "$2" = "$3" ]; then echo "$1 ok"; else echo "$1 BAD [$2] want [$3]"; fi; }
tmp=$(cd /tmp && pwd)
ev()  { cmd.exe //c set CSBT_ 2>&1 | tr -d '\r' | grep '^CSBT_' | tr '\n' ' ' | sed 's/ $//'; }
evc() { cmd.exe /c set CSBT_ 2>&1 | tr -d '\r' | grep '^CSBT_' | tr '\n' ' ' | sed 's/ $//'; }   # when arguments are not converted
check drive        "$(CSBT_A=/c/x ev)"            "CSBT_A=C:/x"
check trailing     "$(CSBT_A=/c/x/ ev)"           "CSBT_A=C:/x/"
check tmp          "$(CSBT_A=/tmp/x ev)"          "CSBT_A=$tmp/x"
check list         "$(CSBT_A=/c/a:/c/b ev)"       "CSBT_A=C:/a;C:/b"         # KEPT: Git Bash C:\a;C:\b
check list-rel     "$(CSBT_A=/c/x:rel ev)"        "CSBT_A=/c/x:rel"
check eq           "$(CSBT_A=--file=/c/x ev)"     "CSBT_A=--file=C:/x"
check opt          "$(CSBT_A=-I/c/x ev)"          "CSBT_A=-IC:/x"
check dbl          "$(CSBT_A=//c ev)"             "CSBT_A=/c"
check semi         "$(CSBT_A='/c/x;/c/y' ev)"     "CSBT_A=/c/x;/c/y"
check middle       "$(CSBT_A=x/c/y ev)"           "CSBT_A=x/c/y"
check spaces       "$(CSBT_A='a b' ev)"           "CSBT_A=a b"
check bare         "$(CSBT_A=/c ev)"              "CSBT_A=/c"                # KEPT: Git Bash C:/
check root         "$(CSBT_A=/ ev)"               "CSBT_A=/"                 # KEPT: Git Bash its install folder
export CSBT_B=/c/x
check exported     "$(ev)"                        "CSBT_B=C:/x"
check shell-keeps  "$CSBT_B"                      "/c/x"
check no-pathconv  "$(MSYS_NO_PATHCONV=1 evc)"    "CSBT_B=/c/x"
check arg-excl-all "$(MSYS2_ARG_CONV_EXCL='*' evc)"   "CSBT_B=C:/x"
check arg-excl-pre "$(MSYS2_ARG_CONV_EXCL=/c ev)"     "CSBT_B=C:/x"
export -n CSBT_B; unset CSBT_B
# this shell run again (a nested bash) reads /c/x itself: it gets the value as typed, as Git Bash's nested bash does
export CSBT_S=/c/x
check self-shell   "$("$BASH" -c 'echo "$CSBT_S"')"   "/c/x"
export -n CSBT_S; unset CSBT_S
check env-excl     "$(MSYS2_ENV_CONV_EXCL=CSBT_A CSBT_A=/c/x ev)"            "CSBT_A=/c/x"
check env-prefix   "$(MSYS2_ENV_CONV_EXCL=CSBT CSBT_A=/c/x ev)"              "CSBT_A=/c/x"
check env-value    "$(MSYS2_ENV_CONV_EXCL='CSBT_A=/c' CSBT_A=/c/x ev)"       "CSBT_A=/c/x"
check env-other    "$(MSYS2_ENV_CONV_EXCL='CSBT_A=/d' CSBT_A=/c/x ev)"       "CSBT_A=C:/x"
check env-case     "$(MSYS2_ENV_CONV_EXCL=csbt_a CSBT_A=/c/x ev)"            "CSBT_A=C:/x"
check env-all      "$(MSYS2_ENV_CONV_EXCL='*' CSBT_A=/c/x ev)"               "CSBT_A=/c/x"
check env-list     "$(MSYS2_ENV_CONV_EXCL='X;CSBT_A' CSBT_A=/c/x ev)"        "CSBT_A=/c/x"
check env-empty    "$(MSYS2_ENV_CONV_EXCL='X;' CSBT_A=/c/x ev)"              "CSBT_A=C:/x"
check env-one      "$(MSYS2_ENV_CONV_EXCL=CSBT_A CSBT_A=/c/x CSBT_C=/c/y ev)" "CSBT_A=/c/x CSBT_C=C:/y"
# PATH: the exit form whatever MSYS2_ENV_CONV_EXCL says (C#Bash's own rule; Git Bash would hand MSYS form under `*`)
p=$(MSYS2_ENV_CONV_EXCL='*' cmd.exe //c echo %PATH% | tr -d '\r')
case "$p" in /[A-Za-z]/*|*';/'[A-Za-z]/*) check path-form "MSYS form: ${p:0:40}" "exit form";; *) check path-form ok ok;; esac
