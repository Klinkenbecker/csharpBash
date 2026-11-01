@echo off
rem claude.cmd: Claude Code with no tool PATH of its own; C#Bash gets the full PATH through BASH_PATH.
rem Written by the C#Bash installer beside Bash.exe. It finds its own folder, so it works wherever that is.
setlocal
set "BIN=%~dp0"
set "BIN=%BIN:\=/%"
if not exist "%USERPROFILE%/.local/bin/claude.exe" (
  echo claude.cmd: Claude Code is not at %USERPROFILE%/.local/bin/claude.exe - edit the last lines of this file to name it.
  exit /b 1
)
set "BASH_PATH=%PATH%"
set "CLAUDE_CODE_GIT_BASH_PATH=%BIN%Bash.exe"
set "PATH=%BIN:~0,-1%"
"%USERPROFILE%/.local/bin/claude.exe" %*
endlocal
