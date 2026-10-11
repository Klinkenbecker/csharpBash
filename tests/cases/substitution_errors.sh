# 2026-10-11: a syntax error inside a command substitution is met when the substitution RUNS, as bash 5.1
# parses the body then (item 6, phase 3). `$( )` (bare, in "...", in a here-document, in a function or a
# loop): `name: command substitution: line N+1:` and the source quoted, then the whole top-level command is
# abandoned (status 1) and the next one runs; inside ( ), a pipeline stage, another $( ) or a background job
# only that one ends. Backquotes: the body is parsed in the substitution's own process: message at line N,
# the substitution fails (2) and the line carries on. `source` and `eval` are their own top level. `-n`
# reports none of it. C#Bash parsed the bodies with the enclosing line: a syntax error that stopped the
# script. Inner scripts by relative name from a temp dir. Expected output: GNU bash via WSL.
d=$(mktemp -d) && cd "$d" || exit 1
cat > cs.sh <<'EOS'
echo one
x=$(if then); echo "same line"
echo "two rc=$?"
f() { echo in-f; y=$(if then); echo after-in-f; }
f; echo "same line as f"
echo "after f rc=$?"
echo $(if then) | cat; echo "pipe same line"
echo "pipe rc=$?"
( z=$(if then); echo sub-after ); echo "sub same line"
echo "sub rc=$?"
w=`if then`; echo "bt same line"
echo "bt rc=$?"
echo "a $(if then) b"; echo "dq same line"
echo "dq rc=$?"
for i in 1 2; do echo "loop $i"; v=$(if then); done; echo "loop same line"
echo "loop rc=$?"
x=$(echo ok; if then; echo no); echo "partial same line"
echo "end"
w=`if then`; echo "bt status=$?"
x=$(echo $(if then)); echo "nested same line"
echo "nested rc=$?"
cat <<EOT
heredoc $(if then) end
EOT
echo "heredoc rc=$?"
EOS
printf 'echo s1\nx=$(if then); echo s-same\necho "s3 rc=$?"\n' > srcbad.sh
cat > cs2.sh <<'EOS'
source ./srcbad.sh; echo "after source same line rc=$?"
echo "next line"
eval 'echo e1; x=$(if then); echo e-same'; echo "after eval same line rc=$?"
eval $'echo m1\nx=$(if then); echo m-same\necho "m3 rc=$?"'; echo "after eval2 rc=$?"
f() { local r; r=$(if then); echo "in f after"; }; f & wait $!; echo "bg rc=$?"
EOS
{
"$BASH" cs.sh; echo "cs rc=$?"
"$BASH" cs2.sh; echo "cs2 rc=$?"
"$BASH" -c 'echo one; x=$(if then); echo two $?' cname; echo "-c rc=$?"
"$BASH" -n cs.sh; echo "-n rc=$?"
} 2>&1
cd / && rm -rf "$d"
