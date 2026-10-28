#!/bin/bash
# tools/check-help-urls.sh [table.json] -- every page in help's table answers, and every #anchor is on its page.
# The table (default Bash/help.json, the one built into C#Bash) maps a name to manual page URLs (DECISIONS 2026-10-27).
# Run it in any bash with curl and jq (C#Bash, WSL's bash) after changing the table, and before a deploy that changes it.
# Each page is fetched once (pages are shared by many names). Prints one line per failing URL and a summary; exits 1
# when any URL fails, 2 when it cannot run, 3 when it stopped because a host looked like it was refusing us.
#
# PACED LIKE A PERSON (the architect, 2026-10-28: gnu.org silently blocked the old back-to-back run; "don't hit it too
# hard or fast ... random and not fast ... more important not to get blocked"): pages in a shuffled order, a random
# pause of HELP_CHECK_MIN..HELP_CHECK_MAX seconds (default 20..70) before each fetch, a browser's User-Agent, and at the
# first sign of a block (403, 429, 503 or no answer) the run STOPS: no retry, no next page. ~87 pages: about an hour.
set -u
here="$(cd "$(dirname "$0")" && pwd)"
table="${1:-$here/../Bash/help.json}"
min=${HELP_CHECK_MIN:-20}; max=${HELP_CHECK_MAX:-70}
agent='Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/129.0 Safari/537.36'
command -v curl > /dev/null && command -v jq > /dev/null || { echo "check-help-urls: needs curl and jq" >&2; exit 2; }
[ -f "$table" ] || { echo "check-help-urls: no table at $table" >&2; exit 2; }
# Pages that answered 200 are kept (HELP_CHECK_CACHE, default $TEMP/check-help-urls) and not fetched again, so a run
# that stopped resumes with the pages it did not get (a stopped run of 2026-10-28 lost its 40 pages). Delete the folder
# to fetch everything afresh.
work=${HELP_CHECK_CACHE:-${TEMP:-/tmp}/check-help-urls}
mkdir -p "$work" || exit 2

# an array, not `while read`: under C#Bash a loop's input is pumped into the external curl (a known limitation)
mapfile -t urls < <(jq -r '[.[][]] | unique | .[]' "$table" | tr -d '\r')
[ ${#urls[@]} -gt 0 ] || { echo "check-help-urls: $table is not a table" >&2; exit 2; }
# shuffled (C#Bash has no shuf, and its awk no srand): a $RANDOM key, sorted on, dropped
mapfile -t sorted < <(printf '%s\n' "${urls[@]}" | sed 's/#.*//' | sort -u)
mapfile -t pages < <(for p in "${sorted[@]}"; do printf '%05d\t%s\n' "$RANDOM" "$p"; done | sort -n | cut -f2-)
[ ${#pages[@]} -gt 0 ] || { echo "check-help-urls: no pages to check" >&2; exit 2; }

declare -A page_status page_file
n=0
stopped=""
declare -A cached
for page in "${pages[@]}"; do   # first what earlier runs got: known whatever this run reaches
	f="$work/$(printf '%s' "$page" | md5sum | cut -c1-16).html"
	page_file[$page]=$f
	if [ -s "$f" ]; then page_status[$page]=200; cached[$page]=1; fi
done
echo "check-help-urls: ${#cached[@]} of ${#pages[@]} pages from earlier runs; fetching the rest, paced"
for page in "${pages[@]}"; do
	n=$((n + 1))
	[ -n "${cached[$page]+set}" ] && continue
	f=${page_file[$page]}
	wait_s=$(( min + RANDOM % (max - min + 1) ))
	echo "check-help-urls: page $n of ${#pages[@]} in ${wait_s}s: $page"
	sleep "$wait_s"
	status=$(curl -s -L -m 60 -A "$agent" -H 'Accept: text/html' -o "$f.part" -w '%{http_code}' "$page")
	if [ "$status" = 200 ]; then mv -f "$f.part" "$f"; else rm -f "$f.part"; fi
	page_status[$page]=$status; page_file[$page]=$f
	case $status in
		403|429|503|000)
			stopped="STOPPED at page $n of ${#pages[@]}: $status from $page (a refusal or no answer; not retried)"
			echo "check-help-urls: $stopped"
			break ;;
	esac
done

fail=0; unchecked=0
for url in "${urls[@]}"; do
	page="${url%%#*}"; anchor=""; [ "$page" != "$url" ] && anchor="${url#*#}"
	status=${page_status[$page]:-}
	if [ -z "$status" ] || { [ -n "$stopped" ] && [ "$status" != 200 ]; }; then unchecked=$((unchecked + 1)); continue; fi
	if [ "$status" != 200 ]; then echo "FAIL $status $url"; fail=$((fail + 1)); continue; fi
	if [ -n "$anchor" ] && ! grep -q "id=\"$anchor\"" "${page_file[$page]}"; then echo "FAIL no anchor $url"; fail=$((fail + 1)); fi
done
echo "check-help-urls: ${#urls[@]} URLs, ${#pages[@]} pages, $fail failed, $unchecked not checked yet${stopped:+ ($stopped)}"
[ -n "$stopped" ] && exit 3
[ $fail = 0 ]
