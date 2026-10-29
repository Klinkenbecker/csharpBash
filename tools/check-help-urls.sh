#!/bin/bash
# tools/check-help-urls.sh [table.json] -- every page in help's table answers, and every #anchor is on its page.
# The table (default Bash/help.json, the one built into C#Bash) maps a name to manual page URLs (DECISIONS 2026-10-27).
# Run it in any bash with curl, jq and tar (C#Bash, WSL's bash) after changing the table, and before a deploy that
# changes it. Prints one line per failing URL and a summary; exits 1 when any URL fails, 2 when it cannot run, 3 when it
# stopped because a host looked like it was refusing us, 4 when it ended at its limit with downloads left
# (HELP_CHECK_PAGES below).
#
# ONE DOWNLOAD PER MANUAL (the architect, 2026-10-29: "switch to the packages"): a GNU manual's per-node pages
# (.../software/PKG/manual/html_node/X.html) are checked against the archive GNU publishes beside them, one file per
# node, unpacked here: X.html must be at the same path in it. The archive's name is read from the manual's index page
# (.../manual/), which is kept like any download: not always PKG (findutils: find.html_node.tar.gz), and a name rule
# once passed two links whose folder does not exist online (DECISIONS 2026-10-29). So 79 of the table's 87 pages,
# under four manuals, are 8 requests, 4 once the indexes are kept. Every other page is fetched itself. Each download is
# made once (pages are shared by many names).
#
# PACED LIKE A PERSON (the architect, 2026-10-28: gnu.org silently blocked the old back-to-back run; "don't hit it too
# hard or fast ... random and not fast ... more important not to get blocked"): downloads in a shuffled order, a random
# pause of HELP_CHECK_MIN..HELP_CHECK_MAX seconds (default 20..70) before each, a browser's User-Agent, and at the
# first sign of a block (403, 429, 503) the run STOPS: no retry, no next download. ~20 downloads: about 15 minutes.
# NO ANSWER (000) IS RETRIED ONCE (the architect, 2026-10-29: "maybe the next change should be just to retry"), as a
# browser retries a dropped request once and a person then reloads: after HELP_CHECK_RETRY_MIN..MAX seconds (default
# 30..90); a second no-answer stops the run. A stop names curl's exit code (6 name lookup, 7 connect, 28 timed out,
# 35 TLS, 52/56 dropped), so the next stop says why.
# IN BATCHES (the architect, 2026-10-28: "start there and get the next N pages, and so on"): HELP_CHECK_PAGES=N makes
# at most N downloads this run, then ends with exit 4 when downloads are left (failures found so far still exit 1);
# the next run goes on from the cache below. 0 or unset: no limit.
set -u
here="$(cd "$(dirname "$0")" && pwd)"
table="${1:-$here/../Bash/help.json}"
min=${HELP_CHECK_MIN:-20}; max=${HELP_CHECK_MAX:-70}
retry_min=${HELP_CHECK_RETRY_MIN:-30}; retry_max=${HELP_CHECK_RETRY_MAX:-90}
limit=${HELP_CHECK_PAGES:-0}
case $limit in ''|*[!0-9]*) echo "check-help-urls: HELP_CHECK_PAGES is not a number: $limit" >&2; exit 2 ;; esac
agent='Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/129.0 Safari/537.36'
for t in curl jq tar; do command -v $t > /dev/null || { echo "check-help-urls: needs curl, jq and tar" >&2; exit 2; }; done
[ -f "$table" ] || { echo "check-help-urls: no table at $table" >&2; exit 2; }
# Downloads that answered 200 are kept (HELP_CHECK_CACHE, default $TEMP/check-help-urls; Windows-rooted, as tar is
# given it) and not made again, so a run that stopped resumes with the ones it did not get (a stopped run of 2026-10-28
# lost its 40 pages). Delete the folder to fetch everything afresh.
work=${HELP_CHECK_CACHE:-${TEMP:-/tmp}/check-help-urls}
mkdir -p "$work" || exit 2

# an array, not `while read`: under C#Bash a loop's input is pumped into the external curl (a known limitation)
mapfile -t urls < <(jq -r '[.[][]] | unique | .[]' "$table" | tr -d '\r')
[ ${#urls[@]} -gt 0 ] || { echo "check-help-urls: $table is not a table" >&2; exit 2; }
# shuffled (C#Bash has no shuf, and its awk no srand): a $RANDOM key, sorted on, dropped
mapfile -t sorted < <(printf '%s\n' "${urls[@]}" | sed 's/#.*//' | sort -u)
mapfile -t pages < <(for p in "${sorted[@]}"; do printf '%05d\t%s\n' "$RANDOM" "$p"; done | sort -n | cut -f2-)
[ ${#pages[@]} -gt 0 ] || { echo "check-help-urls: no pages to check" >&2; exit 2; }

# each page to its manual (its index page, then the archive that page names; rel = the file in it) or to itself
declare -A root_of rel_of seen
first=()
for page in "${pages[@]}"; do
	if [[ $page =~ ^(.*/software/[^/]+/manual/)html_node/(.+)$ ]]; then
		root_of[$page]=${BASH_REMATCH[1]}; rel_of[$page]=${BASH_REMATCH[2]}; item=${BASH_REMATCH[1]}
	else
		root_of[$page]=""; rel_of[$page]=""; item=$page
	fi
	[ -n "${seen[$item]+set}" ] || { seen[$item]=1; first+=("$item"); }
done

declare -A item_status item_file archive_of
n=0; fetched=0
stopped=""; paused=""
# get ITEM: kept from an earlier run, or downloaded now (paced, one retry on no answer; an archive unpacked whole,
# then marked: a half-unpacked folder is never taken for a kept one). Returns 1 when the run must end here.
get() {
	local item=$1 f got out hdr status curl_rc wait_s retry_s
	n=$((n + 1))
	f="$work/$(printf '%s' "$item" | md5sum | cut -c1-16)"
	case $item in *.tar.gz) f=$f.d; got="$f/.unpacked" ;; *) f=$f.html; got=$f ;; esac
	item_file[$item]=$f
	if [ -s "$got" ] || [ -f "$got" ]; then item_status[$item]=200; return 0; fi
	if [ "$limit" -gt 0 ] && [ $fetched -ge "$limit" ]; then
		paused="PAUSED after $limit downloads this run (HELP_CHECK_PAGES): run again for the rest"
		return 1
	fi
	fetched=$((fetched + 1))
	case $item in
		*.tar.gz) out=$f.tar.gz; hdr=(-H 'Accept: */*' -e "${item%/*}/") ;;   # as a browser following the index's link
		*) out=$f.part; hdr=(-H 'Accept: text/html') ;;
	esac
	wait_s=$(( min + RANDOM % (max - min + 1) ))
	echo "check-help-urls: download $n in ${wait_s}s: $item"
	sleep "$wait_s"
	status=$(curl -s -L -m 120 -A "$agent" "${hdr[@]}" -o "$out" -w '%{http_code}' "$item"); curl_rc=$?
	if [ "$status" = 000 ]; then
		retry_s=$(( retry_min + RANDOM % (retry_max - retry_min + 1) ))
		echo "check-help-urls: no answer (curl exit $curl_rc); one retry in ${retry_s}s: $item"
		sleep "$retry_s"
		status=$(curl -s -L -m 120 -A "$agent" "${hdr[@]}" -o "$out" -w '%{http_code}' "$item"); curl_rc=$?
	fi
	if [ "$status" = 200 ]; then
		case $item in
			*.tar.gz) rm -rf "$f"; mkdir -p "$f" && tar -xzf "$out" -C "$f" && : > "$f/.unpacked" || status=unpack ;;
			*) mv -f "$out" "$f" ;;
		esac
	fi
	rm -f "$f.part" "$f.tar.gz"
	item_status[$item]=$status
	case $status in
		403|429|503|000)
			stopped="STOPPED at download $n: $status from $item (curl exit $curl_rc; a refusal, or no answer twice)"
			echo "check-help-urls: $stopped"
			return 1 ;;
	esac
	return 0
}
echo "check-help-urls: ${#pages[@]} pages; fetching what earlier runs did not keep, paced"
ended=""
for item in "${first[@]}"; do get "$item" || { ended=1; break; }; done
# then each manual's archive, by the name its index page links (the first *.html_node.tar.gz)
if [ -z "$ended" ]; then
	for item in "${first[@]}"; do
		[ "${item_status[$item]:-}" = 200 ] && [[ $item == */manual/ ]] || continue
		a=$(grep -o 'href="[^"]*\.html_node\.tar\.gz"' "${item_file[$item]}" | head -1); a=${a#href=\"}; a=${a%\"}
		case $a in '') archive_of[$item]=none; continue ;; http*) ;; *) a=$item$a ;; esac
		archive_of[$item]=$a
		get "$a" || break
	done
fi

fail=0; unchecked=0
for url in "${urls[@]}"; do
	page="${url%%#*}"; anchor=""; [ "$page" != "$url" ] && anchor="${url#*#}"
	root=${root_of[$page]}; rel=${rel_of[$page]}; item=$page; from=""
	if [ -n "$root" ]; then
		item=$root
		if [ "${item_status[$root]:-}" = 200 ]; then
			a=${archive_of[$root]:-}
			if [ -z "$a" ]; then unchecked=$((unchecked + 1)); continue; fi   # the run ended before its archive
			if [ "$a" = none ]; then echo "FAIL no per-node archive on $root: $url"; fail=$((fail + 1)); continue; fi
			item=$a; from=" (from $a)"
		fi
	fi
	status=${item_status[$item]:-}
	if [ -z "$status" ] || { [ -n "$stopped" ] && [ "$status" != 200 ]; }; then unchecked=$((unchecked + 1)); continue; fi
	if [ "$status" != 200 ]; then echo "FAIL $status $url$from"; fail=$((fail + 1)); continue; fi
	f=${item_file[$item]}
	if [ -n "$root" ]; then
		f=$f/$rel
		[ -s "$f" ] || { echo "FAIL not in the manual $url (no $rel in $item)"; fail=$((fail + 1)); continue; }
	fi
	if [ -n "$anchor" ] && ! grep -q "id=\"$anchor\"" "$f"; then echo "FAIL no anchor $url"; fail=$((fail + 1)); fi
done
echo "check-help-urls: ${#urls[@]} URLs, ${#pages[@]} pages, $fetched downloads this run, $fail failed, $unchecked not checked yet${stopped:+ ($stopped)}${paused:+ ($paused)}"
[ -n "$stopped" ] && exit 3
[ $fail = 0 ] || exit 1
[ -n "$paused" ] && exit 4
exit 0
