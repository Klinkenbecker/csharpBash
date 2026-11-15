# 2026-11-14 (git-9d / Wombat, bugs list): `mkdir dir/` (no -p) said "No such file or directory": the parent check
# took the operand's own name as its parent. `mkdir f/` over a file is "File exists"; a missing parent stays an error.
# Expected = GNU coreutils 8.32 via WSL.
d=$(mktemp -d) && cd "$d" || exit 1
mkdir mk1/; echo "rc=$?"
mkdir -v e//; echo "rc=$?"
mkdir a/b/ 2>&1 | sed "s/[‘’]/'/g"; echo "rc=${PIPESTATUS[0]}"
mkdir mk1/ 2>&1 | sed "s/[‘’]/'/g"; echo "rc=${PIPESTATUS[0]}"
touch f; mkdir f/ 2>&1 | sed "s/[‘’]/'/g"; echo "rc=${PIPESTATUS[0]}"
mkdir -p mk3/; echo "rc=$?"
ls -d */
cd / && rm -rf "$d"
