# 2026-10-20: moved here from native_args (2026-10-10). An MSYS program reads /c/x itself, so it gets the argument as
# written, never converted (native_args checks the conversion for Windows programs). Needs an MSYS echo.exe on PATH
# (Git's usr/bin); SKIP without one (native_args printed "msys ok" then, a pass that could not fail). Authored.
m=$(type -P echo.exe) && [ -f "$(dirname "$m")/msys-2.0.dll" ] || { echo "SKIP: needs an MSYS echo.exe on PATH"; exit 0; }
r=$("$m" /c/Windows)
if [ "$r" = /c/Windows ]; then echo "msys ok"; else echo "msys BAD [$r] want [/c/Windows]"; fi
# 2026-11-07: nor its environment (a Windows program's is converted, native_env); Git's usr/bin has printenv beside echo
r=$(CSBT_M=/c/Windows "$(dirname "$m")/printenv.exe" CSBT_M)
if [ "$r" = /c/Windows ]; then echo "msys env ok"; else echo "msys env BAD [$r] want [/c/Windows]"; fi
