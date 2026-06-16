d="$TEMP/bash_du_test"; rm -rf "$d"; mkdir -p "$d/sub"
printf '12345' > "$d/a.txt"
printf '1234567890' > "$d/sub/b.txt"
cd "$d"
du -sb .
du -b a.txt
du -b sub/b.txt
