# 2026-10-10: an awk string's \xNN and \NNN escapes above 0x7F are BYTES, as in gawk: "\xc3\xa9" is é (one
# character in a UTF-8 locale), "\377" is the byte ff. They were characters: "\xc3\xa9" wrote c3 83 c2 a9
# and "\377" wrote c3 bf. Expected output: gawk 5.1 via WSL, the same script; BASH_COREUTILS=builtin.
export BASH_COREUTILS=builtin
awk 'BEGIN{printf "\xc3\xa9|\377|\n"}' | od -An -tx1
awk 'BEGIN{s = "\xc3\xa9"; print length(s), (s == "é"), toupper(s)}'
awk 'BEGIN{s = "\303\251"; print s, length(s)}'
awk 'BEGIN{printf "%s", "\xff\xfe"}' | od -An -tx1
awk 'BEGIN{print "\x41\102\x7e"}'
awk 'BEGIN{print "a\xe2\x9c\x93b", length("a\xe2\x9c\x93b")}'
awk 'BEGIN{print "\xe2\x9c"}' | od -An -tx1
awk 'BEGIN{s = "\xc3"; t = "\xa9"; print s t, length(s t)}'
# -v name=value and name=value operands take the same octal and \x escapes (they took only \n \t … \/)
awk -v s='\101\303\251' 'BEGIN{print s, length(s)}'
awk -v s='\x41\xc3\xa9|\377' 'BEGIN{print s}' | od -An -tx1
echo | awk '{print v, length(v)}' v='\303\251'
awk -F'\x2c' '{print $2}' <<< 'a,b,c'
