# 2026-10-12 (found testing adc-framework-d8's rm): sed as GNU sed. A last input line without a newline is
# written without one, by every command that writes the pattern space (C#Bash added the newline). D with a
# newline in the pattern space restarts the cycle without autoprinting, as d does, the append queue still
# flushed (C#Bash autoprinted, so N;P;D printed every line after the first twice). Expected output: GNU sed via WSL.
o() { od -c | sed -n '1,2p' | cut -c9-; }
printf 'abc' | sed 's/a/x/' | o
printf 'l1\nl2' | sed -n p | o
printf 'q' | sed '' | o
printf 'a' | sed p | o
printf 'a' | sed 'a\
foo' | o
printf 'a\nb' | sed '$d' | o
printf 'x\ny' | sed -n '1p' | o
printf 'x' | sed '=' | o
printf 'a\nb' | sed -z 's/a/A/' | o
d=$(mktemp -d); printf 'k1\nk2' > "$d/f"; sed -i 's/k/K/' "$d/f"; o < "$d/f"; printf 'm' > "$d/g"; sed '' "$d/f" "$d/g" | o; rm -rf "$d"
printf 'a\nb' | sed 'N;P;D' | o
printf 'a\nb\n' | sed 'N;P;D' | o
printf 'a\nb\nc' | sed 'N;P;D' | o
printf 'a\nb' | sed 'N' | o
printf 'a\nb' | sed '$!N;P;D' | o
printf 'a' | sed 'N' | o
printf '1\n2\n3\n' | sed 'N;D' | o
printf '1\n2\n3\n' | sed -e '1{N;a X' -e '}' -e 'D' | o
printf '1\n2\n3\n' | sed -e '$!N;=;D' | o
printf '1\n2\n' | sed -n 'N;l;D' | o
printf 'a\nb\nc\n' | sed '$!N;P;D' | o
