# 2026-11-12 (daq-a1's report): after // a # or % starting the pattern is the pattern's own character, as GNU;
# C#Bash read it as an anchor, so ${p//%LOCALAPPDATA%/R} replaced nothing (${q//#/-} and ${r//%/+} too).
# After a single / they stay anchors. Expected = GNU bash via WSL.
p='%LOCALAPPDATA%/Koliada/x'
echo "1: [${p//%LOCALAPPDATA%/R}]"
echo "2: [R${p#%LOCALAPPDATA%}]"
echo "3: [${p/%x/END}]"
echo "4: [${p/#%LOC/S}]"
q='#a#b#'
echo "5: [${q//#/-}]"
echo "6: [${q/#\#a/X}]"
echo "7: [${q//#a/Y}]"
r='a%b%c'
echo "8: [${r//%/+}]"
echo "9: [${r//%b%/=}]"
a=(%x %y); echo "10: [${a[@]//%/@}]"
