# df (2026-10-08): GNU df's format over Windows volumes. Sizes differ from machine to machine,
# so this checks the shape on C: (headers, names, layout, filters, totals, error paths); the
# numbers, rounding and every header are checked against GNU by tests/df/compare-wsl.sh.
df --output=source,target C:/
df --output=fstype C:/
df --output=file,target C:/Windows
df --total --output=source,target C:/ C:/
df C:/ | awk 'NR == 1 { $1 = $1; print }'
df -h C:/ | awk 'NR == 1 { $1 = $1; print }'
df -H C:/ | awk 'NR == 1 { $1 = $1; print }'
df -P C:/ | awk 'NR == 1 { $1 = $1; print }'
df -T C:/ | awk 'NR == 1 { $1 = $1; print }'
df -BM C:/ | awk 'NR == 1 { $1 = $1; print }'
df -B1 C:/ | awk 'NR == 1 { $1 = $1; print }'
df --output=size,used,avail,pcent C:/ | awk 'NR == 1 { $1 = $1; print }'
df -h --output=size,avail C:/ | awk 'NR == 1 { $1 = $1; print }'
df -B1 --output=size,used,avail C:/ | awk 'NR == 2 { print ($2 + $3 <= $1) ? "used + avail <= size" : "sizes wrong" }'
df -h --output=size C:/ | awk 'NR == 2 { print ($1 ~ /^[0-9.]+[KMGTPE]?$/) ? "human size" : "not human: " $1 }'
df -BM --output=avail C:/ | awk 'NR == 2 { print ($1 ~ /^[0-9]+M$/) ? "M suffix" : "no suffix: " $1 }'
df -k C:/ | awk 'NR == 2 { print NF, ($5 ~ /^[0-9]+%$/) ? "pcent" : "no pcent" }'
df C:/ | grep -c ' $'
df | grep -c '^C: '
df -l --output=source | grep -c '^//'
df -t ntfs --output=source C:/
df -x ntfs C:/; echo "rc=$?"
df /no/such/dir 2>&1; echo "rc=$?"
df '/no such' 2>&1; echo "rc=$?"
df --output=bogus C:/ 2>&1; echo "rc=$?"
df --output=size,size C:/ 2>&1; echo "rc=$?"
df -T --output=size C:/ 2>&1; echo "rc=$?"
df -B 5x C:/ 2>&1; echo "rc=$?"
df -t ntfs -x ntfs C:/ 2>&1; echo "rc=$?"
BASH_COREUTILS=builtin df -i C:/ 2>&1; echo "rc=$?"
