@echo off
rem claude.cmd: Claude Code with C#Bash as its shell. C#Bash gets the full PATH through BASH_PATH; Claude Code itself
rem gets one folder on its PATH, the git it runs, chosen by BASH_CLAUDE_GIT (below).
rem Written by the C#Bash installer beside Bash.exe. It finds its own folder, so it works wherever that is.
rem Every install or upgrade REWRITES this file, so do not edit it: set BASH_CLAUDE_GIT in your own claude.bat, the
rem one that calls this file (no install touches it), or with setx.
setlocal
set "BIN=%~dp0"
set "BIN=%BIN:\=/%"
if not exist "%USERPROFILE%/.local/bin/claude.exe" (
  echo claude.cmd: Claude Code is not at %USERPROFILE%/.local/bin/claude.exe - edit the last lines of this file to name it.
  exit /b 1
)
set "BASH_PATH=%PATH%"
rem without this Claude Code tries PowerShell before bash (GitHub issue #1)
set "CLAUDE_CODE_USE_POWERSHELL_TOOL=0"
set "CLAUDE_CODE_GIT_BASH_PATH=%BIN%Bash.exe"
rem The git Claude Code itself runs (its branch, status and worktree checks, in the background); Claude's own commands
rem get C#Bash's git, unaffected. Claude Code finds git on its PATH, and a search of a long PATH is slow, so its PATH
rem is one folder:
rem   BASH_CLAUDE_GIT=path   (the default) the folder of the first git.exe on your PATH, found once, here; none if none
rem   BASH_CLAUDE_GIT=none   no git for Claude Code
rem   BASH_CLAUDE_GIT=<git>  that git.exe, or the folder holding it
rem Set BASH_CLAUDE_GIT in your own claude.bat before it calls this file; a set here is lost at next install/upgrade.
if not defined BASH_CLAUDE_GIT set "BASH_CLAUDE_GIT=path"
set "GITDIR="
if /i "%BASH_CLAUDE_GIT%"=="path" for %%G in (git.exe) do set "GITDIR=%%~dp$PATH:G"
if /i "%BASH_CLAUDE_GIT%"=="path" goto :run
if /i "%BASH_CLAUDE_GIT%"=="none" goto :run
for %%G in ("%BASH_CLAUDE_GIT%") do if exist "%%~fG\" (set "GITDIR=%%~fG") else if exist "%%~fG" set "GITDIR=%%~dpG"
if not defined GITDIR echo claude.cmd: BASH_CLAUDE_GIT=%BASH_CLAUDE_GIT% is no git.exe or folder; Claude Code runs without git.
:run
rem none, or no git found: a folder with no programs in it, so the search ends at once
if not defined GITDIR set "GITDIR=%BIN%../etc"
set "PATH=%GITDIR%"
set "GITDIR="
rem Launch and exit on one line: cmd rereads this file by name, so an upgrade mid-session must not be read on from.
"%USERPROFILE%/.local/bin/claude.exe" %* & exit /b
