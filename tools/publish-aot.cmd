@echo off
REM ============================================================================
REM  The ship build: Native AOT. One static 5.7 MB executable, no .NET runtime
REM  on the target, ~29 ms startup (the framework-dependent and self-contained
REM  managed builds are 68-74 ms; see README "Performance").
REM
REM  Usage:  tools\publish-aot.cmd [output-dir]        default: dist
REM
REM  TWO PREREQUISITES, both environmental and both on the BUILD machine only:
REM    1. the MSVC C++ toolchain and Windows SDK - the ILCompiler shells out to
REM       link.exe, which needs the LIB/INCLUDE/PATH that vcvars64.bat sets.
REM    2. vswhere.exe ON PATH. It lives in the VS Installer directory, which
REM       vcvars64.bat does NOT add. The ILCompiler runs vswhere to locate the
REM       linker and, when it is missing, splices its own error text into the
REM       command line it then tries to run - producing
REM         error MSB3073: The command ""'vswhere.exe' is not recognized ...
REM         ;...\link.exe" @"...link.rsp"" exited with code 123
REM       which reads as a missing linker while naming the real linker. That
REM       message cost a day's misdiagnosis on 2026-09-12. If AOT fails, check
REM       vswhere BEFORE concluding the toolchain is absent.
REM ============================================================================
setlocal
set "OUT=%~1"
if "%OUT%"=="" set "OUT=%~dp0..\dist"

set "VS=C:\Program Files\Microsoft Visual Studio\2022\Professional"
if not exist "%VS%\VC\Auxiliary\Build\vcvars64.bat" (
  echo publish-aot: vcvars64.bat not found under "%VS%".
  echo             Edit VS= in this script, or install the VS C++ build tools.
  exit /b 2
)
call "%VS%\VC\Auxiliary\Build\vcvars64.bat" >nul 2>&1
if errorlevel 1 (echo publish-aot: vcvars64 failed & exit /b 2)

set "PATH=C:\Program Files (x86)\Microsoft Visual Studio\Installer;%PATH%"
where vswhere.exe >nul 2>&1 || (echo publish-aot: vswhere.exe is not on PATH - see the note above & exit /b 2)
where link.exe   >nul 2>&1 || (echo publish-aot: link.exe is not on PATH after vcvars & exit /b 2)

cd /d "%~dp0.."
REM obj MUST go: a stale intermediate is silently reused and the new settings are ignored.
rmdir /s /q Bash\obj 2>nul
rmdir /s /q "%OUT%" 2>nul
dotnet publish Bash\Bash.csproj -c Release -r win-x64 -p:PublishAot=true -p:DebugType=none -o "%OUT%" --nologo -v q
if errorlevel 1 (echo publish-aot: FAILED & exit /b 1)

echo.
"%OUT%\Bash.exe" --version
for %%F in ("%OUT%\Bash.exe") do echo size: %%~zF bytes
endlocal
