# 2026-11-11 (the architect's yes, 2026-11-09): six Windows environment names arrive upper-cased, as Git Bash imports
# them (measured: SystemRoot, windir, ComSpec, ProgramFiles, CommonProgramFiles, SystemDrive; Path was already PATH),
# so a script written for Git Bash finds $SYSTEMROOT, $WINDIR, $COMSPEC. Every other name keeps its spelling. A Windows
# program still finds them (Windows names are case-insensitive). Authored (Windows-specific).
for n in SYSTEMROOT WINDIR COMSPEC PROGRAMFILES COMMONPROGRAMFILES SYSTEMDRIVE; do
	[ -n "${!n}" ] && echo "$n: set" || echo "$n: missing"
done
echo "SystemRoot: ${SystemRoot-unset}  windir: ${windir-unset}"
echo "ProgramFiles(x86) keeps its spelling: $(env | grep -c '^ProgramFiles(x86)=')"
echo "ProgramData keeps its spelling: $(env | grep -c '^ProgramData=')"
r=$(cmd.exe //d //c 'echo %SystemRoot%' | tr -d '\r'); [ -n "$r" ] && [ "$r" != '%SystemRoot%' ] && echo "a Windows program sees SystemRoot: yes"
