echo "arg0=$0"
echo "count=$#"
echo "first=$1 second=$2"
echo "all=$@"
for a in "$@"; do echo "  item: $a"; done
