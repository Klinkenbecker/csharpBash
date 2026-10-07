# 2026-10-03: a shell with NO console (Claude Code's Bash tool) started an external whose stdio
# was all inherited on a hidden console of its own, so the output went there: lost, status 0.
# Under Claude Code that was the first external of every command containing `<`. Meaningful only
# under `run-tests.ps1 -Detached`; from a console it always passed.
"$BASH" -c 'echo first external, stdout inherited'
"$BASH" -c 'echo second external'
"$BASH" -c 'exit 7'; echo "status $?"
# 2>&1 on an external whose stdout is inherited: its stderr must land on the shell's stdout. It
# stayed on the shell's stderr whenever that stdout was a pipe or a file (with a console too).
"$BASH" -c 'echo external stderr, merged by 2-to-1 1>&2' 2>&1
echo done
