#!/bin/bash
# tools/check-help-urls.sh [table.json] -- every page in help's table answers, and every #anchor is on its page.
# The table (default Bash/help.json, the one built into C#Bash) maps a name to manual page URLs (DECISIONS 2026-10-27).
# Run it in any bash with curl and jq (C#Bash, WSL's bash) after changing the table, and before a deploy that changes it.
# Each page is fetched once (pages are shared by many names). Prints one line per failing URL and a summary; exits 1
# when any URL fails, 2 when it cannot run. A host that does not answer is a failure: re-run before believing a page moved.
set -u
here="$(cd "$(dirname "$0")" && pwd)"
table="${1:-$here/../Bash/help.json}"
command -v curl > /dev/null && command -v jq > /dev/null || { echo "check-help-urls: needs curl and jq" >&2; exit 2; }
[ -f "$table" ] || { echo "check-help-urls: no table at $table" >&2; exit 2; }
work=$(mktemp -d) || exit 2
trap 'rm -rf "$work"' EXIT

declare -A page_status page_file
# an array, not `while read`: under C#Bash a loop's input is pumped into the external curl (a known limitation)
mapfile -t urls < <(jq -r '[.[][]] | unique | .[]' "$table" | tr -d '\r')
[ ${#urls[@]} -gt 0 ] || { echo "check-help-urls: $table is not a table" >&2; exit 2; }
fail=0; n=0
for url in "${urls[@]}"; do
	n=$((n + 1))
	page="${url%%#*}"; anchor=""; [ "$page" != "$url" ] && anchor="${url#*#}"
	if [ -z "${page_status[$page]+set}" ]; then
		f="$work/p$n"
		page_status[$page]=$(curl -s -L -m 60 -o "$f" -w '%{http_code}' "$page")
		page_file[$page]=$f
	fi
	status=${page_status[$page]}
	if [ "$status" != 200 ]; then echo "FAIL $status $url"; fail=$((fail + 1)); continue; fi
	if [ -n "$anchor" ] && ! grep -q "id=\"$anchor\"" "${page_file[$page]}"; then echo "FAIL no anchor $url"; fail=$((fail + 1)); fi
done
echo "check-help-urls: $n URLs, ${#page_status[@]} pages, $fail failed"
[ $fail = 0 ]
