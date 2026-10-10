# 2026-10-08: an unquoted \\ in a case / [[ == ]] / [[ =~ ]] pattern is a literal backslash. The lexer left
# it as a lone \, which the pattern then read as an escape: case 'a\b' in *\\*) did not match, *\\* matched
# 'a*', and [[ $x =~ \\ ]] was an invalid regex. Lines 19-29 guard every other use of \\ in a word (it
# must still be one backslash). Expected output: GNU bash via WSL.
m() { echo "$1 $2"; }
case 'a\b' in *\\*) m 1 match ;; *) m 1 no ;; esac
case 'a*'  in *\\*) m 2 match ;; *) m 2 no ;; esac
[[ 'a\b' == *\\* ]] && m 3 match || m 3 no
[[ 'a\b' == a\\b ]] && m 4 match || m 4 no
p='a\b\c'
echo "5 [${p//\\//}]"
echo "6 [${p#*\\}]"
echo "7 [${p%\\*}]"
echo "8 [$(echo a\\b)]"
case '\' in \\) m 9 match ;; *) m 9 no ;; esac
[[ 'a\b' = *[\\]* ]] && m 10 match || m 10 no
echo "11 [${p//[\\]/-}]"
x='\'; [[ $x == \\ ]] && m 12 match || m 12 no
case 'a\b' in *'\'*) m 13 match ;; *) m 13 no ;; esac
[[ 'a\b' =~ \\ ]] && m 14 match || m 14 no
case 'a\*' in a\\\*) m 15 match ;; *) m 15 no ;; esac
case 'a\x' in a\\?) m 16 match ;; *) m 16 no ;; esac
echo "17 [${p/\\/+}]"
w='C:\Users\guy\x'; echo "18 [${w//\\//}]"
echo "19 [$(echo a\\b)]"
x=a\\b; echo "20 [$x]"
x=\\; echo "21 [$x]"
printf '22 [%s]\n' a\\nb
echo "23 [$(echo \\\\host\\share)]"
arr=(a\\b c); echo "24 [${arr[0]}] [${#arr[@]}]"
echo "25 [$(echo {a\\,b})]"
echo "26 [$(echo a\\)]"
f() { echo "27 [$1]"; }; f x\\y
for w in p\\q; do echo "28 [$w]"; done
echo "29 [$(printf a\\tb | od -c | head -1 | tr -s ' ')]"
