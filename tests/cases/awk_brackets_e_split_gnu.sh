# 2026-11-14 (facts-8a, three reports, bugs list): awk's `\]` and `\[` inside a bracket expression are the brackets
# (the set closed at the escaped `]`, so `[\]]` never matched); `-e PROG` (gawk's --source) runs in-process (it fell
# through to WSL's gawk, which cannot see F:/ paths); an empty separator splits one element per character (split(s,a,"")
# and FS="" gave an empty element at each end). Expected = gawk via WSL.
d=$(mktemp -d) && cd "$d" || exit 1
echo 'a]b[c' | awk '{gsub(/[\]]/,"_");print}'
echo 'a]b[c' | awk '{gsub(/[\[\]]/,"_");print}'
echo 'a]b[c' | awk '{gsub(/[]a]/,"_");print}'
echo 'x' | awk -e '{print "e:" $0}'
awk -e 'BEGIN{print "e2"}' --source 'BEGIN{print "e3"}'
awk -e 'BEGIN{print "to a file" > "out.txt"}'; cat out.txt
awk 'BEGIN{n=split("abc",a,""); print n; for(i=1;i<=n;i++) printf "[%s]", a[i]; print ""}'
echo abc | awk 'BEGIN{FS=""} {print NF, $1 $3}'
awk 'BEGIN{print split("",a,"")}'
cd / && rm -rf "$d"
