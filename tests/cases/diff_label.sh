# 2026-10-12 (adc-framework-d8): `diff -u --label OLD --label NEW` names the files in the headers, with no
# timestamp, as GNU diff; the in-process diff accepted --label and printed the real names and times. Expected:
# GNU diff via WSL.
d=$(mktemp -d) && cd "$d" || exit 1
printf 'a\nb\nc\n' > l1.txt; printf 'a\nB\nc\n' > l2.txt
diff -u --label X1 --label Y1 l1.txt l2.txt; echo "rc=$?"
diff -u --label=X2 --label=Y2 l1.txt l2.txt | head -2
diff -U1 --label old/f --label new/f l1.txt l2.txt; echo "rc=$?"
diff -u --label only l1.txt l2.txt | head -2 | sed 's/\t.*/\t<stamp>/'
diff -u --label X l1.txt l1.txt; echo "same rc=$?"
cd / && rm -rf "$d"
