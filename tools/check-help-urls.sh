#!/bin/bash
# tools/check-help-urls.sh [--again] [table.json] -- every page in help's table answers, and every #anchor is on its page.
# The table (default Bash/help.json, the one built into C#Bash) maps a name to manual page URLs (DECISIONS 2026-10-27).
# Run it in any bash with curl, jq, tar and sha256sum (C#Bash, WSL's bash) after changing the table, and before a deploy
# that changes it. Prints one line per failing URL and a summary; exits 1 when any URL fails (or robots.txt does not let
# it be checked), 2 when it cannot run, 3 when it stopped because a host looked like it was refusing us, 4 when it ended
# at its limit with downloads left (HELP_CHECK_PAGES below).
#
# PROMISED TO GNU.ORG (the architect's mail to its webmasters, 2026-10-29; the ban on our address was lifted on that
# condition): one download per manual, not a page each; checked only when the link table changes; a User-Agent naming
# this tool and its repository; robots.txt followed. Each is below; keep them.
#
# ONLY WHEN THE TABLE CHANGES: a run in which every URL passed records the table's SHA-256 in the cache, and a later
# run of the same table says when it passed and does nothing. --again checks it anyway.
#
# ONE DOWNLOAD PER MANUAL (the architect, 2026-10-29: "switch to the packages"): a GNU manual's per-node pages
# (.../software/PKG/manual/html_node/X.html) are checked against the archive GNU publishes beside them, one file per
# node, unpacked here: X.html must be at the same path in it. The archive's name is read from the manual's index page
# (.../manual/), which is kept like any download: not always PKG (findutils: find.html_node.tar.gz), and a name rule
# once passed two links whose folder does not exist online (DECISIONS 2026-10-29). So 79 of the table's 87 pages,
# under four manuals, are 8 requests, 4 once the indexes are kept. Every other page is fetched itself. Each download is
# made once (pages are shared by many names).
#
# WHO WE ARE: the User-Agent is `csharpBash-help-check/1.0 (+https://github.com/Klinkenbecker/csharpBash)`; robots.txt
# groups are matched against its product token, `csharpBash-help-check` (RFC 9309 allows only letters, `_` and `-`
# there, so not "C#"). Until 2026-10-29 it claimed to be Chrome.
# ROBOTS.TXT (RFC 9309): read from a host once a run, just before the first request this run makes to it (a run that
# needs nothing from a host never asks it). The group for our token, else `*`; the longest matching Allow or Disallow
# wins, Allow on a tie, `*` and `$` understood; no rule matching: allowed. A robots.txt that is missing (4xx): all
# allowed; one the server fails to give (5xx): nothing allowed; 403/429/503 or no answer twice: the run stops, as for
# any request. A disallowed download is not made and its URLs are reported NOT CHECKED. A Crawl-delay longer than the
# pause below is waited instead.
#
# PACED LIKE A PERSON (the architect, 2026-10-28: gnu.org silently blocked the old back-to-back run; "don't hit it too
# hard or fast ... random and not fast ... more important not to get blocked"): downloads in a shuffled order, a random
# pause of HELP_CHECK_MIN..HELP_CHECK_MAX seconds (default 20..70) before each request, and at the first sign of a block
# (403, 429, 503) the run STOPS: no retry, no next request. ~20 requests: about 15 minutes.
# NO ANSWER (000) IS RETRIED ONCE (the architect, 2026-10-29: "maybe the next change should be just to retry"), as a
# browser retries a dropped request once and a person then reloads: after HELP_CHECK_RETRY_MIN..MAX seconds (default
# 30..90); a second no-answer stops the run. A stop names curl's exit code (6 name lookup, 7 connect, 28 timed out,
# 35 TLS, 52/56 dropped), so the next stop says why.
# IN BATCHES (the architect, 2026-10-28: "start there and get the next N pages, and so on"): HELP_CHECK_PAGES=N makes
# at most N requests this run, then ends with exit 4 when downloads are left (failures found so far still exit 1);
# the next run goes on from the cache below. 0 or unset: no limit.
set -u
here="$(cd "$(dirname "$0")" && pwd)"
again=""; table=""
for a in "$@"; do
	case $a in
		--again) again=1 ;;
		-*) echo "check-help-urls: unknown option $a (usage: check-help-urls.sh [--again] [table.json])" >&2; exit 2 ;;
		*) table=$a ;;
	esac
done
table=${table:-$here/../Bash/help.json}
min=${HELP_CHECK_MIN:-20}; max=${HELP_CHECK_MAX:-70}
retry_min=${HELP_CHECK_RETRY_MIN:-30}; retry_max=${HELP_CHECK_RETRY_MAX:-90}
limit=${HELP_CHECK_PAGES:-0}
case $limit in ''|*[!0-9]*) echo "check-help-urls: HELP_CHECK_PAGES is not a number: $limit" >&2; exit 2 ;; esac
agent='csharpBash-help-check/1.0 (+https://github.com/Klinkenbecker/csharpBash)'
token=csharpbash-help-check   # robots.txt's product token, lower case
for t in curl jq tar sha256sum; do command -v $t > /dev/null || { echo "check-help-urls: needs curl, jq, tar and sha256sum" >&2; exit 2; }; done
[ -f "$table" ] || { echo "check-help-urls: no table at $table" >&2; exit 2; }
# Downloads that answered 200 are kept (HELP_CHECK_CACHE, default $TEMP/check-help-urls; Windows-rooted, as tar is
# given it) and not made again, so a run that stopped resumes with the ones it did not get (a stopped run of 2026-10-28
# lost its 40 pages). Delete the folder to fetch everything afresh.
work=${HELP_CHECK_CACHE:-${TEMP:-/tmp}/check-help-urls}
mkdir -p "$work" || exit 2

tsum=$(sha256sum < "$table" | cut -c1-64)
passed="$work/passed-$tsum"
if [ -z "$again" ] && [ -f "$passed" ]; then
	echo "check-help-urls: this table (SHA-256 ${tsum:0:12}) passed on $(cat "$passed"); nothing to do (--again checks it anyway)"
	exit 0
fi

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

declare -A item_status item_file archive_of robots_rules robots_delay
n=0; fetched=0
stopped=""; paused=""

# room: whether this run may make another request (HELP_CHECK_PAGES); says why not
room() {
	if [ "$limit" -gt 0 ] && [ $fetched -ge "$limit" ]; then
		paused="PAUSED after $limit requests this run (HELP_CHECK_PAGES): run again for the rest"
		return 1
	fi
}
# fetch URL OUT [curl options]: one request, paced (a Crawl-delay longer than the pause is waited instead), retried once
# on no answer; sets status and curl_rc. Returns 1, the run stopped, on a refusal or a second no-answer.
fetch() {
	local url=$1 out=$2 host=${1#*://} wait_s retry_s; host=${host%%/*}; shift 2
	fetched=$((fetched + 1))
	wait_s=$(( min + RANDOM % (max - min + 1) ))
	[ "${robots_delay[$host]:-0}" -gt "$wait_s" ] && wait_s=${robots_delay[$host]}
	echo "check-help-urls: request $fetched in ${wait_s}s: $url"
	sleep "$wait_s"
	status=$(curl -s -L -m 120 -A "$agent" "$@" -o "$out" -w '%{http_code}' "$url"); curl_rc=$?
	if [ "$status" = 000 ]; then
		retry_s=$(( retry_min + RANDOM % (retry_max - retry_min + 1) ))
		echo "check-help-urls: no answer (curl exit $curl_rc); one retry in ${retry_s}s: $url"
		sleep "$retry_s"
		status=$(curl -s -L -m 120 -A "$agent" "$@" -o "$out" -w '%{http_code}' "$url"); curl_rc=$?
	fi
	case $status in
		403|429|503|000)
			stopped="STOPPED at request $fetched: $status from $url (curl exit $curl_rc; a refusal, or no answer twice)"
			echo "check-help-urls: $stopped"
			return 1 ;;
	esac
}
# parse_robots HOST < robots.txt: keep, as "A|D<TAB>pattern" lines, the rules of the groups for our token, else of `*`
parse_robots() {
	local host=$1 line key val v k ua=0 mine=0 star=0 ours=0 mr="" sr="" md="" sd=""
	local -a lines
	mapfile -t lines
	for line in "${lines[@]}"; do
		line=${line%%#*}; line=${line%$'\r'}
		[[ $line == *:* ]] || continue
		key=${line%%:*}; key=${key//[[:space:]]/}; key=${key,,}
		val=${line#*:}; val=${val#"${val%%[![:space:]]*}"}; val=${val%"${val##*[![:space:]]}"}
		case $key in
			user-agent)
				[ $ua = 1 ] || { mine=0; star=0; }   # a user-agent line after rules starts a new group
				ua=1; v=${val,,}
				if [ "$v" = '*' ]; then star=1; elif [ -n "$v" ] && [[ $token == "$v"* ]]; then mine=1; ours=1; fi ;;
			allow|disallow)
				ua=0; [ "$key" = allow ] && k=A || k=D
				[ $mine = 1 ] && mr+="$k"$'\t'"$val"$'\n'
				[ $star = 1 ] && sr+="$k"$'\t'"$val"$'\n' ;;
			crawl-delay)
				ua=0; [ $mine = 1 ] && md=$val; [ $star = 1 ] && sd=$val ;;
		esac
	done
	if [ $ours = 1 ]; then robots_rules[$host]=$mr; v=$md; else robots_rules[$host]=$sr; v=$sd; fi
	[[ $v =~ ^[0-9]+ ]] && robots_delay[$host]=${BASH_REMATCH[0]}
	return 0
}
# robots URL: the robots.txt of URL's host, once a run. Returns 1 when the run must stop.
robots() {
	local scheme=${1%%://*} host=${1#*://} out="$work/robots.$$"; host=${host%%/*}
	[ -n "${robots_rules[$host]+set}" ] && return 0
	room || return 1
	fetch "$scheme://$host/robots.txt" "$out" -H 'Accept: text/plain' || { rm -f "$out"; return 1; }
	case $status in
		200) parse_robots "$host" < "$out" ;;
		4*) robots_rules[$host]="" ;;                       # none: everything allowed (RFC 9309 2.3.1.3)
		*) robots_rules[$host]=$'D\t/\n' ;;                 # the server could not give it: nothing allowed (2.3.1.4)
	esac
	rm -f "$out"
}
# allowed URL: RFC 9309's longest match among its host's rules, Allow on a tie; no rule matching: allowed
allowed() {
	local rest=${1#*://} host path pat glob kind line best=-1 verdict=A
	local -a rules
	host=${rest%%/*}; case $rest in */*) path=/${rest#*/} ;; *) path=/ ;; esac
	mapfile -t rules <<< "${robots_rules[$host]:-}"
	for line in "${rules[@]}"; do
		kind=${line%%$'\t'*}; pat=${line#*$'\t'}
		[ -n "$pat" ] && [ "$pat" != "$line" ] || continue   # an empty Disallow allows everything: no rule
		glob=${pat%'$'}; glob=${glob//\\/\\\\}; glob=${glob//\?/\\?}; glob=${glob//\[/\\[}
		[[ $pat == *'$' ]] || glob=$glob'*'
		# shellcheck disable=SC2053
		if [[ $path == $glob ]] && { [ ${#pat} -gt $best ] || { [ ${#pat} -eq $best ] && [ "$kind" = A ]; }; }; then
			best=${#pat}; verdict=$kind
		fi
	done
	[ "$verdict" = A ]
}
# get ITEM: kept from an earlier run, or downloaded now (robots.txt first; an archive unpacked whole, then marked: a
# half-unpacked folder is never taken for a kept one). Returns 1 when the run must end here.
get() {
	local item=$1 f got out hdr
	n=$((n + 1))
	f="$work/$(printf '%s' "$item" | md5sum | cut -c1-16)"
	case $item in *.tar.gz) f=$f.d; got="$f/.unpacked" ;; *) f=$f.html; got=$f ;; esac
	item_file[$item]=$f
	if [ -s "$got" ] || [ -f "$got" ]; then item_status[$item]=200; return 0; fi
	robots "$item" || return 1
	if ! allowed "$item"; then
		item_status[$item]=robots
		local host=${item#*://}; host=${host%%/*}
		echo "check-help-urls: $host's robots.txt does not allow us $item: not fetched"
		return 0
	fi
	room || return 1
	case $item in
		*.tar.gz) out=$f.tar.gz; hdr=(-H 'Accept: */*' -e "${item%/*}/") ;;   # as a browser following the index's link
		*) out=$f.part; hdr=(-H 'Accept: text/html') ;;
	esac
	fetch "$item" "$out" "${hdr[@]}" || { item_status[$item]=$status; rm -f "$f.part" "$f.tar.gz"; return 1; }
	if [ "$status" = 200 ]; then
		case $item in
			*.tar.gz) rm -rf "$f"; mkdir -p "$f" && tar -xzf "$out" -C "$f" && : > "$f/.unpacked" || status=unpack ;;
			*) mv -f "$out" "$f" ;;
		esac
	fi
	rm -f "$f.part" "$f.tar.gz"
	item_status[$item]=$status
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

fail=0; unchecked=0; blocked=0
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
	if [ "$status" = robots ]; then echo "NOT CHECKED (robots.txt) $url$from"; blocked=$((blocked + 1)); continue; fi
	if [ -z "$status" ] || { [ -n "$stopped" ] && [ "$status" != 200 ]; }; then unchecked=$((unchecked + 1)); continue; fi
	if [ "$status" != 200 ]; then echo "FAIL $status $url$from"; fail=$((fail + 1)); continue; fi
	f=${item_file[$item]}
	if [ -n "$root" ]; then
		f=$f/$rel
		[ -s "$f" ] || { echo "FAIL not in the manual $url (no $rel in $item)"; fail=$((fail + 1)); continue; }
	fi
	if [ -n "$anchor" ] && ! grep -q "id=\"$anchor\"" "$f"; then echo "FAIL no anchor $url"; fail=$((fail + 1)); fi
done
echo "check-help-urls: ${#urls[@]} URLs, ${#pages[@]} pages, $fetched requests this run, $fail failed, $blocked not allowed by robots.txt, $unchecked not checked yet${stopped:+ ($stopped)}${paused:+ ($paused)}"
[ -n "$stopped" ] && exit 3
[ $fail = 0 ] && [ $blocked = 0 ] || exit 1
[ -n "$paused" ] && exit 4
[ $unchecked = 0 ] && date '+%Y-%m-%d %H:%M' > "$passed"
exit 0
