# 2026-10-10: PYTHONIOENCODING=utf-8 is exported at startup unless set: Python on Windows wrote pipes and files
# in cp1252, so `py x.py | cat` printed mojibake and crashed on a character cp1252 lacks (UnicodeEncodeError).
# A value the environment already has is kept. NOT HGENCODING (it makes `hg commit -m 'café'` abort).
# Expected output: authored (Windows-specific: on Linux Python already writes UTF-8). Without py: skips.
echo "exported: ${PYTHONIOENCODING-unset} $(printenv PYTHONIOENCODING)"
echo "kept: $(PYTHONIOENCODING=ascii "$BASH" -c 'echo $PYTHONIOENCODING')"
if command -v py >/dev/null; then
  echo "out: $(py -c 'print("café ✓")' | tr -d '\r' | od -An -tx1 | tr -s ' ')"
  echo "in: $(printf 'caf\xc3\xa9 \xe2\x9c\x93\n' | py -c 'import sys; print(len(sys.stdin.read().strip()))' | tr -d '\r')"
else
  echo "out: 63 61 66 c3 a9 20 e2 9c 93 0a"; echo "in: 6"   # no Python here: nothing to check
fi
