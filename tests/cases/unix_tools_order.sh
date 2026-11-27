# 2026-11-24 (the architect: "the reasoning is correct, all the tests should measre every variation of the reasoning"):
# this shell's own folder (~/.bash/bin) is primary and unix-tools is "the 'fallback' _only_ location" (his words; RULED
# 2026-11-11, 2026-11-15; USING startup step 3). Each variation, in the installed layout built here (the shell copied as
# ~/.bash/bin/Bash.exe and sh.exe in a home of its own): (A) a folder named before PATH, (B) after it, (C) none. Its own
# bash, sh and programs come first in every one; a refusing program of its own is rerun by the FIRST fallback in the
# configured order; a name only the folder has, or the folder and PATH both, follows the configured order; a script
# that reorders PATH keeps its own folder first, and one that takes it off PATH still runs its own bash. AOT only (a lone copy
# of the managed build cannot run). Authored (Windows-specific).
P=$(mktemp -d) || exit 1
B="$P/.bash/bin"; mkdir -p "$B" "$P/.bash/etc" "$P/tools" "$P/pathdir" "$P/x"
cp "$BASH" "$B/Bash.exe" && cp "$BASH" "$B/sh.exe" || exit 1
mk() { printf '@echo %s\r\n' "$3" > "$1/$2.cmd"; }
mk "$B" owntool OWN-owntool; printf '@exit /b 127\r\n' > "$B/refuser.cmd"
for t in bash sh owntool refuser onlyfolder both; do mk "$P/tools" $t FOLDER-$t; done
for t in refuser both onlypath; do mk "$P/pathdir" $t PATH-$t; done
mk "$P/x" bash X-bash
probe='who='"'"'case $BASH_VERSION in *koliada*) echo own-$0;; *) echo other-$0 $BASH_VERSION;; esac'"'"'; bash -c "$who" bash; sh -c "$who" sh; bash -c "exit 127"; echo "bash exit 127: rc=$?"; sh -c "exit 127"; echo "sh exit 127: rc=$?"; owntool; refuser; onlyfolder 2>/dev/null || echo "onlyfolder: not found"
	both; onlypath; type -a both | sed "s|^both is ||" | tr "\n" " "; echo
	PATH="$1/x;$PATH"; bash -c "$who" bash-after-a-reorder
	PATH="$1/pathdir;C:/Windows/System32"; bash -c "$who" bash-off-PATH 2>/dev/null || echo "bash-off-PATH: not found"'
for c in "A $P/tools;PATH" "B PATH;$P/tools" "C PATH"; do
	echo "== ${c%% *}: unix-tools = ${c#* }" | sed "s|$P|P|g"
	printf 'unix-tools = %s\n' "${c#* }" > "$P/.bash/etc/csharpBash.config"
	PATH="$P/pathdir;C:/Windows/System32" USERPROFILE="$P" "$B/Bash.exe" -c "$probe" _ "$P" < /dev/null 2>&1 | tr -d '\r' | sed "s|$P|P|g"
done
cd / && rm -rf "$P"
