# The tests' MSYS (the architect, 2026-11-09; DECISIONS "the tests' MSYS pinned"). MSYS is never a requirement of
# C#Bash, its build or its deploy: a case that needs an MSYS program sources this file, from the tests folder (the
# runner's working directory):  . cases/msys_pin.inc
#   1. the pin, PortableGit 2.28.0's usr/bin (the benchmark's copy), when it holds msys-2.0.dll;
#   2. else the first PATH folder that holds msys-2.0.dll (the MSYS signature);
#   3. else none: the case prints one line, `BYPASSED: <reason>`, which the runner reports loudly, never as a failure.
# The folder found goes first on the case's PATH and is MSYS_BIN. On Linux (a reference run of a case) nothing is done.
MSYS_PIN=F:/Tools/PortableGit-2.28.0/usr/bin
case $OSTYPE in
	linux*) MSYS_BIN= ;;
	*)
		MSYS_BIN=
		if [ -n "$MSYS_PIN" ] && [ -f "$MSYS_PIN/msys-2.0.dll" ]; then
			MSYS_BIN=$MSYS_PIN
		else
			_msys_ifs=$IFS; IFS=:
			for _msys_d in $PATH; do
				[ -n "$_msys_d" ] && [ -f "$_msys_d/msys-2.0.dll" ] && { MSYS_BIN=$_msys_d; break; }
			done
			IFS=$_msys_ifs; unset _msys_ifs _msys_d
		fi
		[ -n "$MSYS_BIN" ] || { echo "BYPASSED: no MSYS (the pin $MSYS_PIN holds no msys-2.0.dll, and no PATH folder does)"; exit 0; }
		PATH="$MSYS_BIN:$PATH"
		;;
esac
