# Claude Code's per-command wrapper shape (DECISIONS 2026-09-04): MSYS-form paths, a
# snapshot sourced with errors suppressed, multi-var export, a tolerated shopt, a brace
# group with redirects, eval of the command with stdin closed, then pwd into a cwd file.
snap=/tmp/csbash_snapshot_test.sh
cat > "$snap" <<'EOF'
function rg { echo "rg-shim $*"; }
alias -- ll='echo aliased'
export SNAP_LOADED=1
EOF
cwdfile=/tmp/csbash_cwd_test
source "$snap" 2>/dev/null || true && export TEMP2='C:\Temp2' TMP2='C:\Temp2' && shopt -u extglob 2>/dev/null || true && { \builtin unalias -- 'unsetenv'; \builtin unset -f -- 'unsetenv'; } >/dev/null 2>&1 || true && eval 'echo "loaded=$SNAP_LOADED"; rg a b; cd /c/Windows; echo "temp2=$TEMP2"' < /dev/null && pwd -P >| "$cwdfile"
echo "rc=$?"
[ -s "$cwdfile" ] && echo "cwdfile written"
grep -qi 'windows' "$cwdfile" && echo "cwd tracked"
rm -f "$snap" "$cwdfile"
echo "OSTYPE=$OSTYPE"
# a syntax error inside a sourced file must fail only `source`, never the caller
printf 'function bad {\n' > "$snap"
source "$snap" 2>/dev/null || true && echo "caller survived"
rm -f "$snap"
