# 2026-10-08: a Windows program's exit code becomes a shell status of 0-255, as bash on Linux would
# report the same outcome: its low byte (-1 is 255, 256 is 0), and an NTSTATUS crash code is 128 + the
# signal (access violation 139, illegal instruction 132, divide by zero 136, Ctrl+C 130). `$?` was the
# raw code (-1, 256, -1073741819). Authored: Windows exit codes, made with cmd /c exit N.
for c in 0 3 255 256 257 -1 -2 -1073741819 -1073741571 -1073741795 -1073741676 -1073741510 -2147483645 -1073740791 1000; do
	cmd.exe /c "exit $c"; echo "$c -> $?"
done
cmd.exe /c "exit -1" | cat; echo "in a pipeline: ${PIPESTATUS[*]}"
( cmd.exe /c "exit 256" ); echo "in a subshell: $?"
r=$(cmd.exe /c "exit -1"); echo "in \$( ): $?"
cmd.exe /c "exit -1" & wait $!; echo "background, wait: $?"
