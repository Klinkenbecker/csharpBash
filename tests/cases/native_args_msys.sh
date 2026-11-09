# 2026-10-20: moved here from native_args (2026-10-10). An MSYS program reads /c/x itself, so it gets the argument as
# written, never converted (native_args checks the conversion for Windows programs). Needs MSYS's echo.exe and
# printenv.exe (cases/msys_pin.inc). Authored.
. cases/msys_pin.inc
m=$MSYS_BIN/echo.exe
r=$("$m" /c/Windows)
if [ "$r" = /c/Windows ]; then echo "msys ok"; else echo "msys BAD [$r] want [/c/Windows]"; fi
# 2026-11-07: nor its environment (a Windows program's is converted, native_env); Git's usr/bin has printenv beside echo
r=$(CSBT_M=/c/Windows "$(dirname "$m")/printenv.exe" CSBT_M)
if [ "$r" = /c/Windows ]; then echo "msys env ok"; else echo "msys env BAD [$r] want [/c/Windows]"; fi
