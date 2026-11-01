@echo off
rem try-install.cmd: install C#Bash for real from the csharpBash-setup.exe beside this file, then check the result.
rem Run it from a Command Prompt. It installs into %USERPROFILE%/.bash/bin and asks one question, PATH (default no).
rem Any arguments go to the installer (for a sandboxed run: --test <file> --silent, with CSBASH_BIN naming its bin).
setlocal
set "HERE=%~dp0"
set "HERE=%HERE:\=/%"
if defined CSBASH_BIN (set "BIN=%CSBASH_BIN%") else (set "BIN=%USERPROFILE%/.bash/bin")
set "BIN=%BIN:\=/%"
if not exist "%HERE%csharpBash-setup.exe" (
  echo try-install: csharpBash-setup.exe is not beside this script.
  exit /b 1
)

echo === 1. the installer
"%HERE%csharpBash-setup.exe" %*
set "RC=%ERRORLEVEL%"
echo installer exit code: %RC%
if not "%RC%"=="0" exit /b %RC%

echo.
echo === 2. what it installed, in %BIN%
echo --- Bash.exe:
"%BIN%/Bash.exe" --version
echo --- sh.exe:
"%BIN%/sh.exe" --version
echo --- sh.exe and Bash.exe, one file?
"%BIN%/Bash.exe" -c "[ '%BIN%/sh.exe' -ef '%BIN%/Bash.exe' ] && echo yes, one file with $(stat -c %%h '%BIN%/Bash.exe') names || echo NO, two separate files"
echo --- the start file:
if exist "%BIN%/claude.cmd" (echo claude.cmd is there) else (echo claude.cmd is MISSING)

echo.
echo Start Claude Code on C#Bash with: "%BIN%/claude.cmd"
echo In that session, `echo $BASH_VERSION` should say 5.1.0-koliada.
echo The installer's own log: %HERE%csharpBash-setup.exe.log
endlocal
