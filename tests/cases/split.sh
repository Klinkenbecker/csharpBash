printf 'l1\nl2\nl3\nl4\nl5\n' > sp_in.txt
split -l 2 sp_in.txt sp_
echo "[aa]"; cat sp_aa
echo "[ab]"; cat sp_ab
echo "[ac]"; cat sp_ac
split -b 4 sp_in.txt b_
echo "[b_aa nbytes]"; wc -c < b_aa
rm -f sp_in.txt sp_aa sp_ab sp_ac b_aa b_ab b_ac b_ad
