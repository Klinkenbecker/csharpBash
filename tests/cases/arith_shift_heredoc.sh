# 2026-10-28: `<<` inside an unquoted `$(( ))` is the shift operator, not a here-document. The lexer queued a here-document
# for it, so the value came out right but the lines after it were read as the body and the next line failed ("syntax
# error near unexpected token"). Each form below is followed by a line that must run; real here-documents next to
# arithmetic still work. Expected: GNU bash 5.1 via WSL.
x=$(( (3) << 1 )); echo "a: $x"
echo "after a"
x=$(( 3 << 1 )); echo "b: $x"
echo "after b"
echo "c: $(( (1+1) << (1+1) ))" $(( 1 << 4 ))
echo "after c"
declare d=$(( (3)<<2 )); echo "d: $d"
echo "after d"
echo "e: $(( $(echo 2) << 1 ))" $(( $(echo 2) << 2 ))
echo "after e"
(( y = (1) << 3 )); echo "f: $y"
y=1; (( y <<= 2 )); echo "g: $y"
for (( i = 1; i < (1) << 3; i <<= 1 )); do printf '%s ' $i; done; echo "h"
cat <<END
i: a here-document after arithmetic lines: $(( 1 << 5 ))
END
echo $(( 2 + 1 )) <<END2
this body is stdin to echo, not output
END2
cat <<-END3
	j: a stripped here-document $(( (2) << 1 ))
	END3
echo "the end"
