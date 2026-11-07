# 2026-11-05: what the in-process iconv does not convert goes to the PATH iconv, and says so (none on this machine's
# PATH: BASH_COREUTILS=builtin makes the message the same everywhere); -l lists what it converts, in glibc's form
# (glibc lists hundreds); a Windows INF (UTF-16LE with a BOM and CRLF) read as installer-a4 needed.
# Expected: authored (checked by hand).
r() { echo "--- $*"; BASH_COREUTILS=builtin "$@" 2>&1 < /dev/null; echo "[rc=$?]"; }
r iconv -f UCS-2 -t UTF-8
r iconv -f UTF-8 -t ASCII//TRANSLIT
r iconv -f UTF-8 -t EBCDIC-US
iconv -l | head -4; echo "names: $(iconv -l | wc -l)"
echo "the INF case: $(printf '\377\376[\000V\000e\000r\000s\000i\000o\000n\000]\000\r\000\n\000' | iconv -f UTF-16 -t UTF-8 | od -An -c | tr -s ' ')"
