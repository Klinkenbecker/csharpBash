# 2026-10-12: bash's command printer (print_cmd.c), as `declare -f`, `type` and `eval "$(declare -f f)"` show
# it. C#Bash printed a function's source as written. The functions below cover every construct C#Bash parses
# (no select, no coproc: C#Bash has neither); they are defined, never run. The rules are in DECISIONS
# 2026-10-12 "PRINTER P1". Expected output: GNU bash 5.1 via WSL.
f1() { echo a; echo b; }
f2() { if true; then echo y; elif false; then echo n; else echo e; fi; }
f3() { for i in 1 2 3; do echo $i; done; for ((i=0;i<3;i++)); do echo $i; done; for j; do :; done; }
f4() { while read -r l; do echo "$l"; done < file; until false; do break; done; }
f5() { case $1 in a) echo A;; b|c) echo BC;; *) echo other;; esac; }
f6() { ( cd /; ls ); { echo g; } > /dev/null 2>&1; }
f7() { a && b || c; a | b | c; a & b; a |& b; }
f8() { cat <<EOT
hello $x
EOT
echo after; }
f9() { [[ -n $x && $y == z* ]]; (( x = y + 1 )); [[ ! -f a || ( -d b ) ]]; }
f10() { local x=1 y; x+=2; arr=(a b c); echo "${arr[@]}"; declare -A m=([k]=v); }
f11() { echo $(date) `date` $((1+2)) "$(echo  "in"  )"; }
f12() { function inner { echo i; }; inner; g() ( echo sub ); }
f14() { ! true; time sleep 0; time -p true; }
f15() { echo a > f 2>&1 < g >> h; exec 3>&-; cat <<< "here"; echo x &> y; }
f16() { echo a; # comment
  echo b; }
f17() { case x in (a) echo;; esac; case x in a) ;; b) echo b; echo c;; esac; case y in *) echo d;& e) echo e;;& f) echo f;; esac; }
f18() { if true; then :; fi > out; while :; do :; done 2> err; }
f19() { echo 'single' "double $x" $'ansi\n' \" a\ b; }
f20() { x=1 y=2 cmd arg; FOO=bar; }
f21() { cat <<-EOT
	tabbed
	EOT
cat <<'Q' > f
lit $x
Q
}
f22() {
  local a
  if [ -n "$a" ]; then
    for k in "$@"; do
      case $k in
        -x) a=1 ;;
      esac
    done
  fi
}
f23() { a; b & }
f24() { { a; b; } | c; ( a ) && { b; }; }
g1() { echo x; } >&2
g2() ( echo sub )
g3() { if a && b; then c; fi; if ! cmd; then d; fi; while IFS= read -r line || [[ -n $line ]]; do echo "$line"; done; }
g4() { for x in a; do for y in b; do case $x$y in ab) if true; then echo z; fi;; esac; done; done; }
g5() { cat <<EOT | sort
b
a
EOT
}
g6() { a |
  b |
  c; a &&
  b; }
g7() { [[ $x =~ ^a(b|c)$ ]]; [[ "$y" != "q" ]]; [[ -z ${z:-} ]]; }
g8() { echo ${x} ${x:-d} ${#x} "${x//a/b}" $((x+1)) $(( y * 2 )); }
g9() { return 1; exit 2; local -r ro=1; }
g10() { echo >/dev/null 2>/dev/null 2>&1 1>&2 <&3 3<>f >|g; read <&0; }
g12() { { echo a; } 2>&1 | tee x; f() { :; }; }
g13() { "quoted cmd" 'arg'; \ls; }
g14() { [ -f "$1" ] || { echo missing >&2; return 1; }; }
g15() { case "$1" in "a b") echo q;; 'c'*) echo r;; esac; }
g16() { x=$(cat <<EOT
inner
EOT
); echo "$x"; }
g17() { echo "a
b"; }
g18() { (( i++ )); let i+=1; echo $[1+1]; }
g19() { if true; then echo a; elif false; then echo b; fi; }
function h1 { echo $'it\'s' $'tab\there'; }
h2() { :; } 2>/dev/null
h3() { if true; then cat <<EOT
body
EOT
fi; }
h4() { case $1 in a) echo a;& b) echo b;;& *) echo c;; esac; }
h5() { ( a; b; c ) > out; ( ( nested ) ); }
h6() { ! a | b; time a | b; time { a; }; }
h7() { while true; do if a; then break; fi; done; }
h8() { local x="$1" && shift || return; }
h9() { echo "$(for i in 1 2; do echo $i; done)"; }
h10() { [[ a == b ]] && echo y || echo n; }
h11() { arr+=(x y); arr[1]=z; m[k]+=v; }
h12() { case $x in
    a) echo a
       echo b
       ;;
  esac
}
declare -f
echo "=== type"
type f1 h2
echo "=== round trip"
for f in $(declare -F | awk '{print $3}'); do
	a=$(declare -f "$f"); eval "$a"
	[ "$a" = "$(declare -f "$f")" ] || echo "round trip differs: $f"
done
echo "round trip checked: $(declare -F | wc -l) functions"
