# 2026-11-01 (c-parser-ac's report): sed converts GNU's escapes \a \f \v \cX \dNNN \oNNN \xHH before parsing, in the
# regex (s and addresses), the replacement, y's strings and a/i/c text (every line of it). `s/-/\x27/` wrote "x27".
# In a regex the produced character is raw (\x2e is any character, as GNU's manual says under "Escaping Precedence");
# in the replacement a produced & or \ is literal; in y and text a produced \ is literal. A bare \d in a regex is the
# letter d (it was .NET's digit class). Found beside it: only the last line of a/i/c text was unescaped, and a line
# ending in an escaped backslash (`a one\\`) was read as continued. Expected: GNU sed 4.8 via WSL, LC_ALL=C.
export LC_ALL=C
p() { printf '%s\n' "--- $1"; }
p "the report: \\xHH in the replacement";   echo 'a-b' | sed 's/-/\x27/'
p "up to two hex digits";                   echo 'a-b' | sed 's/-/\x41\x42/;s/b/\x414/'
p "no digits: the letter";                  echo 'a-b' | sed 's/-/\xZ/;s/a/\o/;s/b/\d/'
p "\\o and \\d take up to three digits";    echo 'a-b' | sed 's/-/\o47\d039\d0391/'
p "the value mod 256";                      echo 'a-b' | sed 's/-/\d256\o400\d999/' | od -An -tx1
p "a produced & or \\ is literal";          echo 'a-b' | sed 's/\(-\)/[\x26]<\x5c\x31>\x5c/'
p "-E too";                                 echo 'a-b' | sed -E 's/(-)/[\x5c1]/'
p "\\U applies to a produced letter";       echo 'a-b' | sed 's/-/\U\x61/'
p "\\cX";                                   echo 'a-b' | sed 's/-/\cA\cz\c?\cm\c[\c\\/' | od -An -tx1
p "\\c\\ then another escape";              echo 'a-b' | sed 's/-/\c\d/' 2>&1; echo "rc=$?"
p "...its position in s, an address, y, a"; echo a | sed 's/\c\d/x/g' 2>&1; echo a | sed '/\c\d/p' 2>&1; echo a | sed 'y/\c\d/x/' 2>&1; echo a | sed 'a x\c\d' 2>&1
p "\\c at the end of the replacement";      echo 'a-b' | sed 's/-/x\c/'
p "\\a \\f \\v";                            echo 'a-b' | sed 's/-/\a\f\v/' | od -An -tx1
p "bytes, and two that spell e-acute";      echo 'a-b' | sed 's/-/\x7e\xff/;s/b/\xc3\xa9/' | od -An -tx1
p "regex: the character is raw";            echo 'a.b' | sed 's/\x2e/X/g'; echo 'a^b' | sed 's/\x5e/X/'; echo 'a*b' | sed 's/a\x2a/X/'
p "regex: \\o, \\d, a bare \\d";            echo 'aAb' | sed 's/\o101/X/'; echo 'a1b' | sed 's/\d49/X/'; echo 'a5d' | sed 's/\d/X/'
p "regex: in a bracket; \\\\ stays a pair"; echo 'aAb' | sed 's/[\x41]/X/'; printf 'aA\\x\n' | sed 's/[\\x41]/X/g'
p "regex: \\cA, a byte, e-acute";           printf 'a\001b\n' | sed 's/\cA/X/'; printf 'a\351b\n' | sed 's/\xe9/X/'; printf 'a\303\251b\n' | sed 's/\xc3\xa9/E/'
p "regex: a trailing \\x5c, \\c";           printf 'a\\b\n' | sed 's/\x5c/X/' 2>&1; echo "rc=$?"; echo acb | sed '/\c/d' 2>&1; echo "rc=$?"
p "an address, and -E";                     printf 'A\nB\n' | sed -n '/\x42/p'; echo 'a.b' | sed -E 's/\x2e/X/g'
p "y";                                      echo 'a-b' | sed 'y/-b/\t\x42/' | od -An -c; echo 'a-b' | sed 'y/-/\x5c/'; echo 'ab' | sed 'y/b/\xc3/' | od -An -tx1
p "a one-liner";                            echo x | sed 'a hi\x41\tB' | od -An -c
p "a/i text: every line";                   echo x | sed 'a\
one\ttwo\x41\
three\x42' | od -An -c
echo x | sed '1i\
p\x5c\
q'
p "a/i text: a line before the last is unescaped too; an escaped \\ at the end ends the text"
echo x | sed 'a\
m\qn\\o\
end'
echo x | sed 'a one\\'
