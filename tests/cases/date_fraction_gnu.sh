# 2026-11-09 (git-b3): a clock time's fraction of a second, in `touch -d` and `date -d`, was dropped:
# `touch -d '2026-01-01 01:02:03.123456789' f` stored .000000000 (GNU keeps the fraction). Windows and .NET keep 100 ns,
# so digits past the 7th are dropped (KNOWN_ISSUES); this case uses at most 7, which both keep. Expected: GNU via WSL.
d=$(mktemp -d) && cd "$d" || exit 1
touch -d '2026-01-01 01:02:03.1234567Z' f; date -u -r f '+touch: %F %T.%N'
touch -d '2026-01-01T01:02:03.5Z' g; date -u -r g '+touch iso: %F %T.%N'
date -u -d '2026-01-01 01:02:03.25Z' '+date: %T.%N'
date -u -d '2026-01-01 01:02:03Z' '+whole: %T.%N'
date -u -d '@1767229323.75' '+epoch: %T.%N'
cd / && rm -rf "$d"
