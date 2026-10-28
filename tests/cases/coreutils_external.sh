# 2026-10-11: BASH_COREUTILS=external runs the PATH program for an in-process tool that has one (the README
# documents the mode). It handed the command to RunCommand, which ran the builtin of the same name, which
# handed it back, until the stack overflowed and the shell died, rc 139 (every build to rev 209). A tool
# with no PATH twin, and anything that is not a coreutil, stays in-process. Needs Git for Windows (its
# usr/bin tools). Authored expectations.
command -v git > /dev/null || { echo "SKIP: needs Git for Windows (its usr/bin tools)"; exit 0; }
export BASH_COREUTILS=external
case "$(awk --version | head -1)" in "GNU Awk"*) echo "awk: the PATH program" ;; *) echo "awk: NOT the PATH program" ;; esac
case "$(cat --version | head -1)" in "cat (GNU coreutils)"*) echo "cat: the PATH program" ;; *) echo "cat: NOT the PATH program" ;; esac
echo x | grep -c x
printf 'b\na\n' | sort | tr '\n' ' '; echo
printf '{"a":1}' | jq -c .a
echo "the shell is still here"
BASH_COREUTILS=builtin awk --version | head -1 | cut -c1-29
