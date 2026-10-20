# 2026-10-19 (red team F9, and beside it): a script run as a command takes its #! line's options and env -S.
# `#!/usr/bin/env -S bash -e` was "/usr/bin/env: '-S': No such file or directory", 127; and the options of a bash or
# sh script run in-process were dropped: `#!/bin/bash -e` went on past a failure, rc 0; `#!/bin/sh -x` traced
# nothing. Expected: GNU bash 5.1 via WSL (scripts made executable there), byte-identical.
d=$(mktemp -d) && cd "$d" || exit 1
printf '#!/bin/bash -e\nfalse\necho "after false"\n' > e
printf '#!/usr/bin/env -S bash -eu\necho "body-ran $1"\necho "$unset_var"\necho "after unset"\n' > s
printf '#!/bin/sh -x\necho traced\n' > x
printf '#!/usr/bin/env -S bash -e\necho "body-ran $1"\n' > rt
printf '#!/usr/bin/env -S bash -o pipefail\nfalse | true; echo "pipefail rc=$?"\n' > p
printf '#!/usr/bin/env -Sbash\necho "joined -S ran"\n' > j
printf '#!/bin/bash\nfalse\necho "no options: goes on"\n' > n
chmod +x e s x rt p j n
for f in e s x rt p j n; do ./$f a1 2>&1 | sed "s#^\./$f: line#$f: line#"; echo "$f rc=${PIPESTATUS[0]}"; done
cd / && rm -rf "$d"
