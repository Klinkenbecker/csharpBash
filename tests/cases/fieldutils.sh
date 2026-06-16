echo hello | tr a-z A-Z
echo aabbcc | tr -s a-z
echo HeLLo | tr '[:upper:]' '[:lower:]'
cut -d: -f1,3 cases/cols.txt
cut -c1-3 cases/cols.txt
uniq cases/dups.txt
uniq -c cases/dups.txt
nl cases/dups.txt
echo abcdefgh | fold -w3
