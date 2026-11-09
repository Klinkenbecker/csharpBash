# 2026-11-07 (KNOWN_ISSUES.md's open list): the POSIX classes as GNU reads them in a UTF-8 locale, C#Bash's own (it counts
# characters, not bytes, everywhere else). Every class was ASCII: [[:alpha:]] did not match é in [[ =~ ]], globs, grep,
# sed or awk. tr keeps ASCII classes, as GNU tr (bytes) does. Expected = GNU bash 5.1, grep 3.7, sed 4.8, gawk 5.1, tr
# 8.32 via WSL under LANG=C.UTF-8 (set here; C#Bash does not read it).
export LANG=C.UTF-8
classes=( alpha digit alnum upper lower space blank punct print graph cntrl xdigit )
# 1. a readable table: =~ (R), a glob (G), grep (g)
chars=( a Z 5 é ß Σ ǅ ᾈ 中 Ａ ٣ ² ① Ⅻ ⅻ ¼ ʰ ª Ⓐ $'́' $'ः' $' ' $' ' $'　' $'\u0085' $'​' — € _ $'\t' $'\x7f' $' ' )
for ch in "${chars[@]}"; do
  printf 'U+%04X' "'$ch"
  for c in "${classes[@]}"; do
    r=; [[ $ch =~ ^[[:$c:]]$ ]] && r+=R || r+=.
    case $ch in [[:$c:]]) r+=G;; *) r+=.;; esac
    printf '%s\n' "$ch" | grep -q "^[[:$c:]]\$" && r+=g || r+=.
    printf ' %s' "$r"
  done
  echo
done
# CR, which a line tool reads as part of a CRLF line end, through =~ alone
for c in space cntrl print; do [[ $'\r' =~ ^[[:$c:]]$ ]] && echo "CR is $c" || echo "CR is not $c"; done
# 2. sed, awk, tr
echo "sed:  $(printf 'aé5٣ß\n' | sed 's/[[:alpha:]]/X/g')"
echo "awk:  $(printf 'aé5٣ß\n' | awk '{ gsub(/[[:alpha:]]/, "X"); print }')"
echo "tr:   $(printf 'aé5ß\n' | tr -d '[:alpha:]')"
echo "negated: $(printf 'aé-5 ²\n' | sed 's/[^[:alnum:]]/_/g')"
# 3. every BMP character but CR, LF, NUL, the surrogates and two that Unicode 15 assigned and glibc's tables do not
#    (U+0CF3, U+0ECE): per class, how many grep finds and a checksum of which
cd "$(mktemp -d)" || exit 1
for ((cp = 1; cp < 0x10000; cp++)); do
  ((cp == 10 || cp == 13 || cp == 0x0CF3 || cp == 0x0ECE || (cp >= 0xD800 && cp <= 0xDFFF))) && continue
  printf -v hex %04X $cp
  printf '%s \u'"$hex"'\n' "$hex"
done > bmp.txt
echo "bmp lines: $(wc -l < bmp.txt)"
for c in "${classes[@]}"; do
  grep -a "^[0-9A-F]\{4\} [[:$c:]]\$" bmp.txt | cut -c1-4 > m
  printf '%-6s %5d %s\n' $c $(wc -l < m) "$(cksum < m)"
done
cd / && rm -rf "$OLDPWD"
