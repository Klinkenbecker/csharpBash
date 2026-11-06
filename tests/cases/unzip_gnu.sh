# 2026-11-05 (koliada-net-b9; the architect: "add unzip with 'most used' options (that are not complex) - priority
# addition"): unzip in-process, UnZip 6.00's forms, messages (padded by bytes), streams and exit codes; every line ends
# in LF (UnxUtils' unzip.exe, first on PATH, wrote CRLF, so `unzip -l x | grep 'dll$'` found nothing). The archives
# are made by Python's zipfile (DOS times; ut.zip has an extended timestamp; bad.zip a wrong CRC; infl.zip invalid
# deflated data; evil.zip ../ and / names) and embedded as base64.
# Expected: UnZip 6.00 (Debian) via WSL, from tests/: wsl.exe -e bash cases/unzip_gnu.sh < /dev/null > expected/unzip_gnu.out
w=$(mktemp -d) && cd "$w" || exit 1
base64 -d > t.zip <<'EOF'
UEsDBBQAAAAIAOtwZVj/XcXEDgAAAAwAAAAFAAAAYS50eHTLSM3Jyecqzy/KSeECAFBLAwQUAAAA
AADrcGVYAAAAAAAAAAAAAAAABAAAAGRpci9QSwMEFAAAAAAA63BlWK+muqYGAAAABgAAAAkAAABk
aXIvYi50eHRicmF2bwpQSwMEFAAAAAgA63BlWNNWshkPAAAAkAEAAA0AAABkaXIvc3ViL2MudHh0
S85ILMrJTOVKHqUHBQ0AUEsDBBQAAAAAAOtwZVgAAAAAAAAAAAAAAAAJAAAAZW1wdHkudHh0UEsD
BBQAAAgIAOtwZVg57i0ACQAAAAcAAAANAAAAZGlyL8OpdMOpLnR4dCsuzc1NLeICAFBLAQIUAxQA
AAAIAOtwZVj/XcXEDgAAAAwAAAAFAAAAAAAAAAAAAACkgQAAAABhLnR4dFBLAQIUAxQAAAAAAOtw
ZVgAAAAAAAAAAAAAAAAEAAAAAAAAAAAAEADtQTEAAABkaXIvUEsBAhQDFAAAAAAA63BlWK+muqYG
AAAABgAAAAkAAAAAAAAAAAAAAKSBUwAAAGRpci9iLnR4dFBLAQIUAxQAAAAIAOtwZVjTVrIZDwAA
AJABAAANAAAAAAAAAAAAAACkgYAAAABkaXIvc3ViL2MudHh0UEsBAhQDFAAAAAAA63BlWAAAAAAA
AAAAAAAAAAkAAAAAAAAAAAAAAKSBugAAAGVtcHR5LnR4dFBLAQIUAxQAAAgIAOtwZVg57i0ACQAA
AAcAAAANAAAAAAAAAAAAAACkgeEAAABkaXIvw6l0w6kudHh0UEsFBgAAAAAGAAYASQEAABUBAAAA
AA==
EOF
base64 -d > evil.zip <<'EOF'
UEsDBBQAAAAAAOtwZVh6zT+3BQAAAAUAAAALAAAALi4vZXZpbC50eHRldmlsClBLAwQUAAAAAADr
cGVYH5NKDQQAAAAEAAAACAAAAC9hYnMudHh0YWJzClBLAwQUAAAAAADrcGVYQeORYgMAAAADAAAA
DwAAAG9rLy4uLy4uL3VwLnR4dHVwClBLAQIUAxQAAAAAAOtwZVh6zT+3BQAAAAUAAAALAAAAAAAA
AAAAAACkgQAAAAAuLi9ldmlsLnR4dFBLAQIUAxQAAAAAAOtwZVgfk0oNBAAAAAQAAAAIAAAAAAAA
AAAAAACkgS4AAAAvYWJzLnR4dFBLAQIUAxQAAAAAAOtwZVhB45FiAwAAAAMAAAAPAAAAAAAAAAAA
AACkgVgAAABvay8uLi8uLi91cC50eHRQSwUGAAAAAAMAAwCsAAAAiAAAAAAA
EOF
base64 -d > long.zip <<'EOF'
UEsDBBQAAAAIAOtwZViO2CmBvQQAAIfWEgAgAAAAYV9yYXRoZXJfbG9uZ19maWxlX25hbWVfaGVy
ZS50eHTtwYEAAAAAwyDt4V/hAFUBAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA
AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA
AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA
AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA
AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA
AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA
AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA
AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA
AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA
AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA
AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA
AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA
AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA
AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA
AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA
AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA
AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA
AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA
AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA
AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA
AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA
AAAAAAAAAAAAAAAAAAAAAAAAAHAYUEsDBBQAAAAIAH2/nycmTAu3GAEAAAAEAAAFAAAAYi5iaW5j
YGRiZmFlY+fg5OLm4eXjFxAUEhYRFROXkJSSlpGVk1dQVFJWUVVT19DU0tbR1dM3MDQyNjE1M7ew
tLK2sbWzd3B0cnZxdXP38PTy9vH18w8IDAoOCQ0Lj4iMio6JjYtPSExKTklNS8/IzMrOyc3LLygs
Ki4pLSuvqKyqrqmtq29obGpuaW1r7+js6u7p7eufMHHS5ClTp02fMXPW7Dlz581fsHDR4iVLly1f
sXLV6jVr163fsHHT5i1bt23fsXPX7j179+0/cPDQ4SNHjx0/cfLU6TNnz52/cPHS5StXr12/cfPW
7Tt3791/8PDR4ydPnz1/8fLV6zdv373/8PHT5y9fv33/8fPX7z9///1nGPX/qP9HsP8BUEsBAhQD
FAAAAAgA63BlWI7YKYG9BAAAh9YSACAAAAAAAAAAAAAAAKSBAAAAAGFfcmF0aGVyX2xvbmdfZmls
ZV9uYW1lX2hlcmUudHh0UEsBAhQDFAAAAAgAfb+fJyZMC7cYAQAAAAQAAAUAAAAAAAAAAAAAAKSB
+wQAAGIuYmluUEsFBgAAAAACAAIAgQAAADYGAAAAAA==
EOF
base64 -d > ut.zip <<'EOF'
UEsDBBQAAAAAAOtwZVhFJv0GAwAAAAMAAAAFAAkAdS50eHRVVAUAAaVdDV51dApQSwECFAMUAAAA
AADrcGVYRSb9BgMAAAADAAAABQAJAAAAAAAAAAAApIEAAAAAdS50eHRVVAUAAaVdDV5QSwUGAAAA
AAEAAQA8AAAALwAAAAAA
EOF
base64 -d > bad.zip <<'EOF'
UEsDBBQAAAAIAOtwZVj/XcXEDgAAAAwAAAAFAAAAYS50eHTLSM3Jyecqzy/KSeECAFBLAwQUAAAA
AADrcGVYAAAAAAAAAAAAAAAABAAAAGRpci9QSwMEFAAAAAAA63BlWK+muqYGAAAABgAAAAkAAABk
aXIvYi50eHRCcmF2bwpQSwMEFAAAAAgA63BlWNNWshkPAAAAkAEAAA0AAABkaXIvc3ViL2MudHh0
S85ILMrJTOVKHqUHBQ0AUEsDBBQAAAAAAOtwZVgAAAAAAAAAAAAAAAAJAAAAZW1wdHkudHh0UEsD
BBQAAAgIAOtwZVg57i0ACQAAAAcAAAANAAAAZGlyL8OpdMOpLnR4dCsuzc1NLeICAFBLAQIUAxQA
AAAIAOtwZVj/XcXEDgAAAAwAAAAFAAAAAAAAAAAAAACkgQAAAABhLnR4dFBLAQIUAxQAAAAAAOtw
ZVgAAAAAAAAAAAAAAAAEAAAAAAAAAAAAEADtQTEAAABkaXIvUEsBAhQDFAAAAAAA63BlWK+muqYG
AAAABgAAAAkAAAAAAAAAAAAAAKSBUwAAAGRpci9iLnR4dFBLAQIUAxQAAAAIAOtwZVjTVrIZDwAA
AJABAAANAAAAAAAAAAAAAACkgYAAAABkaXIvc3ViL2MudHh0UEsBAhQDFAAAAAAA63BlWAAAAAAA
AAAAAAAAAAkAAAAAAAAAAAAAAKSBugAAAGVtcHR5LnR4dFBLAQIUAxQAAAgIAOtwZVg57i0ACQAA
AAcAAAANAAAAAAAAAAAAAACkgeEAAABkaXIvw6l0w6kudHh0UEsFBgAAAAAGAAYASQEAABUBAAAA
AA==
EOF
base64 -d > infl.zip <<'EOF'
UEsDBBQAAAAIAOtwZVjTVrIZDwAAAJABAAAFAAAAYy50eHT/zkgsyslM5UoepQcFDQBQSwECFAMU
AAAACADrcGVY01ayGQ8AAACQAQAABQAAAAAAAAAAAAAApIEAAAAAYy50eHRQSwUGAAAAAAEAAQAz
AAAAMgAAAAAA
EOF
base64 -d > notzip.zip <<'EOF'
bm90IGEgemlwCg==
EOF

r() { echo "--- unzip ${*:2}"; "$@" 2>err; echo "[rc=$?]"; sed 's/^/ERR: /' err; }
ids() { stat -c '%n %s %Y' "$@"; }

echo "== list"
r unzip -l t.zip
r unzip -l t
r unzip -lq t.zip
r unzip -qql t.zip
r unzip -l t.zip 'dir/[bé]*' '*.TXT'
r unzip -l t.zip '*/*.txt'
r unzip -l t.zip nosuch
r unzip -qql t.zip nosuch
r unzip -l long.zip
r unzip -l ut.zip
r unzip -l evil.zip
echo "the reporter's anchor: $(unzip -l long.zip | grep -c 'bin$') line(s) end in bin, $(unzip -l long.zip | grep -c $'\r') CR"

echo "== extract"
mkdir x1 && cd x1
r unzip ../t.zip
find . | LC_ALL=C sort
ids a.txt dir/b.txt dir/sub/c.txt empty.txt dir/été.txt; stat -c '%n %Y' dir   # a folder's size is the system's
cat a.txt dir/b.txt; head -2 dir/sub/c.txt
r unzip ../t.zip < /dev/null
printf 'n\ny\n' | r unzip ../t.zip a.txt empty.txt
printf 'N\n' | r unzip ../t.zip
printf 'A\n' | r unzip ../t.zip a.txt dir/b.txt
printf 'x\nn\n' | r unzip ../t.zip a.txt
printf 'r\nnew.txt\n' | r unzip ../t.zip a.txt
cat new.txt
r unzip -n ../t.zip
r unzip -o ../t.zip a.txt
r unzip -o ../t.zip dir/
cd ..
r unzip -q t.zip -d q
find q | LC_ALL=C sort
r unzip -d d2 t.zip a.txt
r unzip -o -dD1/ t.zip a.txt
r unzip -o -d ./D3// t.zip a.txt
r unzip -o t.zip -d m a.txt
r unzip -oq t.zip -d '' empty.txt
r unzip -o t.zip -d

echo "== members, -x, -j"
r unzip t.zip 'dir/*' -d m2
r unzip t.zip -x 'dir/*' -d x
r unzip -o t.zip -x nosuch -d x3
r unzip -o t.zip a.txt -d D4 -x a.txt
r unzip -o t.zip nosuch a.txt -d n1
r unzip -q t.zip nosuch -d n2
r unzip -j t.zip -d j
find j | LC_ALL=C sort
r unzip -oj t.zip 'dir/*' -d jj
find jj | LC_ALL=C sort

echo "== pipe"
r unzip -p t.zip a.txt dir/b.txt
r unzip -p t.zip nosuch
echo "binary: $(unzip -p long.zip b.bin | cksum) $(unzip -p long.zip b.bin | od -An -tx1 | sed -n 1p)"

echo "== test"
r unzip -t t.zip
r unzip -t t.zip a.txt
r unzip -t t.zip 'dir/*'
r unzip -tq t.zip
r unzip -tqq t.zip
r unzip -t t.zip nosuch
r unzip -tq t.zip nosuch
r unzip -t long.zip

echo "== damage, paths, times, errors"
r unzip -t bad.zip
r unzip -o bad.zip dir/b.txt -d bad
r unzip -p bad.zip dir/b.txt
r unzip -t infl.zip
r unzip -o infl.zip -d i1
r unzip -p infl.zip
r unzip -q -o bad.zip dir/b.txt -d b2
r unzip -tq bad.zip
r unzip -o t.zip a.txt '*.TXT' -d w1
r unzip -t t.zip a.txt '*.TXT'
r unzip -p t.zip a.txt '*.TXT'
r unzip evil.zip -d e
find e | LC_ALL=C sort
r unzip -q evil.zip -d e2
r unzip -o ut.zip -d ut
ids ut/u.txt
r unzip -o long.zip -d lg
ids lg/a_rather_long_file_name_here.txt lg/b.bin
r unzip nosuch
r unzip notzip.zip
cp notzip.zip notzip.exe && chmod 755 notzip.exe   # executable (the bit; on Windows the extension): UnZip adds a note
r unzip notzip.exe
cd / && rm -rf "$w"
