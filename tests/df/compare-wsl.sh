#!/bin/bash
# Manual check (needs WSL): C#Bash's df against GNU df, which reads the same Windows volumes
# through WSL's /mnt/<d> mounts. Every case runs in both; stdout, stderr and exit status must
# agree after the normalisation below. Prints one line per case and a count; exits 1 on any diff.
#
#   tests/df/compare-wsl.sh [C#Bash exe]        (default: dist/Bash.exe)
#
# Run it from Git Bash, the reference shell, not from C#Bash (whose own defects would then be in
# the harness). On this machine:
#   F:/Tools/PortableGit/usr/bin/_bash.exe -lc '"$BASH" /f/Koliada/Tools/bash/tests/df/compare-wsl.sh [exe]'
#
# Drives: quiet local NTFS volumes (DF_DRIVES, default "g u w z"), so the numbers do not move
# between the two runs; if a case differs only in a number, run it again before believing it.
# The differences by design, normalised away: GNU's source `G:\` is our `G:`, its mount point
# `/mnt/g` is our `G:/`, its type `9p` (drvfs) is our `ntfs`. In a case, @D is the drive list
# (/mnt/g ... for GNU, G:/ ... for C#Bash) and @T the type (9p / ntfs). GNU runs with LC_ALL=C,
# so its messages quote with ASCII apostrophes, as C#Bash's tools do.
# Not comparable this way: -l (GNU counts a source containing ':' as remote), network and
# removable drives (WSL mounts neither), and --output file columns that are not last.

export MSYS2_ARG_CONV_EXCL='*'   # Git Bash must pass /mnt/g, /nope and G:/ to native programs as written
here=$(cd "$(dirname "$0")" && pwd)
CS=${1:-$here/../../dist/Bash.exe}
read -r -a drives <<< "${DF_DRIVES:-g u w z}"
gnuD="" csD=""
for d in "${drives[@]}"; do
	D=$(printf '%s' "$d" | tr a-z A-Z)
	gnuD="$gnuD /mnt/$d"; csD="$csD $D:/"
done

norm() {
	local args=() d D
	for d in "${drives[@]}"; do
		D=$(printf '%s' "$d" | tr a-z A-Z)
		args+=(-e "s|$D:\\\\ |$D:  |g" -e "s|$D:\\\\\$|$D:|" -e "s|/mnt/$d |$D:/    |g" -e "s|/mnt/$d\$|$D:/|")
	done
	sed "${args[@]}" -e 's|9p  |ntfs|g' -e 's|9p$|ntfs|' -e "s|'9p'|'ntfs'|g"
}

cases=(
	"@D" "-h @D" "-H @D" "--si @D" "-k @D" "-P @D" "-P -h @D" "-P -k @D" "-T @D" "-T -h @D" "-hT @D"
	"-B1 @D" "-BK @D" "-BM @D" "-BG @D" "-BT @D" "-BKB @D" "-BMB @D" "-BMiB @D" "-B4K @D" "-B512 @D" "-B1M @D"
	"-B 1000 @D" "-B1536 @D" "--block-size=1K @D" "--block-size=human-readable @D" "--block-size=si @D"
	"-h -k @D" "-k -h @D" "-BM -h @D" "-h -BM @D" "-P -BM @D"
	"--total @D" "--total -h @D" "--total -T @D" "--total -P @D"
	"--output=source,size,used,avail,pcent,target @D" "--output=source,fstype,size,target @D"
	"--output=size,used,avail @D" "--output=pcent @D" "--output=source,avail -h @D" "--output=size -BM @D"
	"--output=target,avail @D" "--output=avail,target @D" "--output=source --output=size @D"
	"--total --output=avail,size @D" "--total --output=target,avail @D" "--total --output=source,used @D"
	"--output=size,source @D" "--output=file @D"
	"-t @T @D" "-x @T @D" "-t @T -x @T @D" "-T -t @T @D"
	"-B x @D" "-B 5x @D" "-B 0 @D" "-BY @D" "--block-size=Kx @D" "-B '' @D"
	"--output=foo @D" "--output=size,size @D" "-T --output=size @D" "-P --output=size @D"
	"/nope" "/nope @D" "@D /nope" "'/no pe'"
)
envcases=(
	"DF_BLOCK_SIZE=1M|@D" "BLOCK_SIZE=human-readable|@D" "BLOCKSIZE=1K|@D" "DF_BLOCK_SIZE=1M|-k @D"
	"DF_BLOCK_SIZE=1M|-P @D" "POSIXLY_CORRECT=1|-P @D" "POSIXLY_CORRECT=1|@D" "DF_BLOCK_SIZE=bogus|@D"
)

tmp=$(mktemp -d)
pass=0 fail=0
run() {   # $1 = env assignment (may be empty), $2 = case
	local envs=$1 c=$2 g s
	g=${c//@D/$gnuD}; g=${g//@T/9p}
	s=${c//@D/$csD};  s=${s//@T/ntfs}
	wsl.exe -e bash -c "cd / && env LC_ALL=C $envs df $g" > "$tmp/g.out" 2> "$tmp/g.err" < /dev/null; local grc=$?
	"$CS" -c "${envs:+export $envs; }df $s" > "$tmp/c.out" 2> "$tmp/c.err" < /dev/null; local crc=$?
	norm < "$tmp/g.out" > "$tmp/g.norm"
	grep -v '^wsl: ' "$tmp/g.err" | norm > "$tmp/g.errn"   # wsl.exe's own "Failed to translate" startup noise
	if cmp -s "$tmp/g.norm" "$tmp/c.out" && cmp -s "$tmp/g.errn" "$tmp/c.err" && [ "$grc" = "$crc" ]; then
		pass=$((pass + 1)); echo "ok    ${envs:+$envs }df $c"
	else
		fail=$((fail + 1)); echo "DIFF  ${envs:+$envs }df $c   (rc gnu $grc, c# $crc)"
		diff "$tmp/g.norm" "$tmp/c.out" | sed 's/^/        out /'
		diff "$tmp/g.errn" "$tmp/c.err" | sed 's/^/        err /'
	fi
}
for c in "${cases[@]}"; do run "" "$c"; done
for e in "${envcases[@]}"; do run "${e%%|*}" "${e#*|}"; done
rm -rf "$tmp"
echo "$pass passed, $fail differ  (C#Bash: $("$CS" --version | tail -1))"
[ "$fail" -eq 0 ]
