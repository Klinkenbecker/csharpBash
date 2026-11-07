# 2026-11-05 (Claude's find, raised with a position): a Windows program given /dev/null got a path that does not exist:
# `curl -o /dev/null` exited 23 (a write error). Git Bash hands it `nul`: the whole argument, after `=`, after a
# one-letter option; `x/dev/null` and `/dev/null/x` stay; an MSYS2_ARG_CONV_EXCL prefix leaves it alone.
# Expected: Git Bash 4.4.23 (F:/Tools/PortableGit/bin/bash.exe cases/native_devnull.sh), the reference for what a
# Windows program receives.
cmd //c echo /dev/null --out=/dev/null -o/dev/null x/dev/null /dev/null/x
MSYS2_ARG_CONV_EXCL='-o' cmd //c echo -o/dev/null /dev/null
curl -s -o /dev/null file:///C:/Windows/win.ini; echo "curl -o /dev/null: rc=$?"
curl -s --output /dev/null -w '%{http_code}|' file:///C:/Windows/win.ini; echo " rc=$?"
