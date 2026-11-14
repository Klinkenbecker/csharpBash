# 2026-11-11 (features list 1): find -anewer REF is true when an entry's access time is later than REF's
# modification time, -cnewer when its change time is (GNU find's documented reading); a missing reference is
# -newer's error. They went to a PATH find. Expected: GNU findutils via WSL, LC_ALL=C. The -anewer lines come first:
# on NTFS reading a change time opens the file, which may move its access time.
export LC_ALL=C
d=$(mktemp -d) && cd "$d" || exit 1
for f in old new ref fut; do printf 'x\n' > "$f"; done
touch -m -d '2021-06-01' ref; touch -m -d '2099-01-01' fut
touch -a -d '2021-01-01' old; touch -a -d '2022-01-01' new; touch -a -d '2020-01-01' ref fut
echo "1 anewer ref: $(find . -type f -anewer ref | sort | tr '\n' ' ')"
echo "2 anewer fut: [$(find . -type f -anewer fut | sort | tr '\n' ' ')]"
echo "3 not anewer ref: $(find . -type f ! -anewer ref | sort | tr '\n' ' ')"
echo "4 cnewer ref: $(find . -type f -cnewer ref | sort | tr '\n' ' ')"
echo "5 cnewer fut: [$(find . -type f -cnewer fut | sort | tr '\n' ' ')]"
echo "6 $(find . -anewer nope 2>&1) rc=$?"
echo "7 $(find . -cnewer nope 2>&1) rc=$?"
echo "8 $(find . -anewer 2>&1) rc=$?"
cd / && rm -rf "$d"
