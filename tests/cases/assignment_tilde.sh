# 2026-10-08: tilde expansion in assignment values. A `~` at the start of the value or after an
# unquoted `:`, followed by `/`, `:` or the end, is the home directory (x=~/y, PATH=$PATH:~/bin).
# It was never expanded: x=~/y stayed `~/y`. Expected output: GNU bash via WSL.
HOME=/home/u
x=~/y; echo "1  [$x]"
x=~; echo "2  [$x]"
x=a:~/b:~; echo "3  [$x]"
p2=/p:~/bin; echo "4  [$p2]"
x=$HOME:~/c; echo "5  [$x]"
x="~/q"; echo "6  [$x]"
x=\~/q; echo "7  [$x]"
x=a~b; echo "8  [$x]"
x=~nosuchuser42; echo "9  [$x]"
x=~"/q"; echo "10 [$x]"
f() { local y=~/z; echo "11 [$y]"; }; f
export e=~/e; echo "12 [$e]"
declare -a arr; arr[0]=~/a; echo "13 [${arr[0]}]"
arr2=(~/a ~/b); echo "14 [${arr2[*]}]"
x=~/y env | grep -c '^x=/home/u/y$'
y=pre~/x; echo "16 [$y]"
x+=:~/w; echo "17 [$x]"
