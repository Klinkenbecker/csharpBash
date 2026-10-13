# 2026-10-12: variables follow bash's dynamic scoping (the variable-scope plan, V1). An assignment to a name no
# scope holds goes to the GLOBAL scope (C#Bash kept a variable created in a function only until it returned);
# `local` and `declare` in a function hold scalars, arrays and their attributes (-i -r -x) in that call only
# (C#Bash's arrays and attributes were shell-wide); a callee sees and changes its caller's locals; `unset`
# follows bash. Out of scope: namerefs, localvar_inherit/localvar_unset. Expected output: GNU bash 5.1 via WSL.
p() { printf '%s\n' "$*"; }
# 1 plain assignment in a function is global
f1() { g1=1; ga1=(a b); }; f1; p "1 g1=[$g1] ga1=[${ga1[*]}]"
# 2 local array hides and restores a global array
a2=(G1 G2); f2() { local a2=(L1); p "2in [${a2[*]}] n=${#a2[@]}"; }; f2; p "2out [${a2[*]}] n=${#a2[@]}"
# 3 local array with no global leaves nothing
f3() { local a3=(x y); }; f3; p "3 [${a3[*]}] set=${a3+yes}"
# 4 local -a / declare -a / declare in function, assoc too
f4() { local -a b4; b4[2]=z; declare c4=(m); local -A d4=([k]=v); declare -A e4; e4[q]=r; }; f4; p "4 [${b4[*]}] [${c4[*]}] [${d4[k]}] [${e4[q]}]"
# 5 dynamic scope: a callee sees and changes the caller's local array
f5b() { p "5callee [${a5[*]}]"; a5[1]=CH; a5+=(AP); }; f5() { local a5=(c1 c2); f5b; p "5caller [${a5[*]}]"; }; a5=(GLOBAL); f5; p "5out [${a5[*]}]"
# 6 local scalar hides a global array, then becomes a local array
a6=(G); f6() { local a6; p "6a set=${a6+yes} n=${#a6[@]}"; a6[1]=x; p "6b [${a6[*]}]"; }; f6; p "6out [${a6[*]}]"
# 7 local a=("${a[@]}" new): the right side sees the outer value
a7=(o1 o2); f7() { local a7=("${a7[@]}" new); p "7in [${a7[*]}]"; }; f7; p "7out [${a7[*]}]"
# 8 declare -g in a function is global
f8() { declare -g a8=(gl); local a9=(lo); declare -ga a10; a10[3]=t; }; f8; p "8 [${a8[*]}] [${a9[*]}] [${a10[3]}]"
# 9 recursion: each call its own array
f9() { local a=("$1"); [ "$1" -lt 3 ] && f9 $(($1+1)); p "9 depth=$1 a=[${a[*]}]"; }; f9 1
# 10 mapfile / read -a into a declared local
f10() { local m10 r10; mapfile -t m10 < <(printf 'l1\nl2\n'); read -ra r10 <<< "w1 w2"; p "10in [${m10[*]}] [${r10[*]}]"; }; f10; p "10out [${m10[*]}] [${r10[*]}]"
# 11 attributes stay in the function: local -i -r -x
f11() { local -i i11=2+3; local -r r11=ro; local -x x11=ex; p "11in $i11 $r11 $x11"; }; f11; r11=rw; p "11out i=[$i11] r=[$r11] x=[$x11] $(env | grep -c '^x11=')"
# 12 unset of a local array inside the function
a12=(G); f12() { local a12=(L); unset a12; p "12in set=${a12+yes} [${a12[*]}]"; }; f12; p "12out [${a12[*]}]"
# 13 declare -p inside and outside
a13=(g); f13() { local a13=(l1 l2); declare -p a13; }; f13; declare -p a13
# 14 a subshell in a function, and $( )
f14() { local a14=(s); ( a14+=(t); p "14sub [${a14[*]}]" ); x=$(p "${a14[*]}"); p "14in [${a14[*]}] [$x]"; }; f14; p "14out set=${a14+yes}"
# 15 global array changed inside a function (no local) persists
a15=(1); f15() { a15+=(2); a15[5]=6; }; f15; p "15 [${a15[*]}] n=${#a15[@]}"
# 16 local -n (nameref) not covered; local array of the same name as a function parameter
f16() { local -a args=("$@"); p "16 ${#args[@]} [${args[1]}]"; }; f16 x "y z"; p "16out set=${args+yes}"
f20() { read -r r20 <<< "rv"; for i20 in a b; do :; done; printf -v pv20 '%s' pv; getopts ab: o20 -a; (( n20 = 7 )); let l20=8; : ${d20:=dv}; x20+=ap; }; f20
p "20 read=[$r20] for=[$i20] printf=[$pv20] getopts=[$o20] arith=[$n20] let=[$l20] default=[$d20] append=[$x20]"
x21=0; f21() { x21=1; }; f21; p "21 existing global updated [$x21]"
f22() { local l22=L; f22b; p "22caller [$l22]"; }; f22b() { l22=fromcallee; n22=new; }; f22; p "22out l22=[${l22-unset}] n22=[$n22]"
f23() { local -i i23=2+3; i23+=1; p "23 [$i23]"; }; f23; p "23out [${i23-unset}]"
f24() { local -r r24=ro; p "24in [$r24]"; }; f24; r24=rw; p "24out [$r24]"
f25() { local -x x25=ex; p "25in children see: $(env | grep -c '^x25=')"; }; f25; p "25out [${x25-unset}] children: $(env | grep -c '^x25=')"
a26=(G); f26() { local a26=(L); unset a26; p "26in set=${a26+yes} [${a26[*]}]"; }; f26; p "26out [${a26[*]}]"
v27=G; f27() { local v27=L; f27b; p "27caller after callee unset: [${v27-unset}]"; }; f27b() { unset v27; }; f27; p "27out [$v27]"
f28() { local v28; v28=set; p "28in [$v28]"; }; v28=G; f28; p "28out [$v28]"
f29() { declare v29=D; typeset w29=T; }; f29; p "29 [${v29-unset}] [${w29-unset}]"
declare -A m30=([g]=1); f30() { local -A m30=([l]=2); p "30in ${!m30[*]}"; }; f30; p "30out ${!m30[*]}"
f35() { arr35[2]=x; }; f35; p "35 n=${#arr35[@]} [${arr35[2]}]"
f36() { export e36=v; }; f36; p "36 [$e36] children: $(env | grep -c '^e36=')"
f37() { readonly ro37=v; }; f37; p "37 [$ro37]"; ( ro37=x ) 2>/dev/null; p "37 assignment refused: rc=$?"
declare -i g38; f38() { g38=3+4; }; f38; p "38 [$g38]"
f39() { local IFS=,; set -- $(echo a,b); p "39in $#"; }; f39; [ "$IFS" = $' \t\n' ] && p "39out IFS is the default again"
local l40=x 2>&1 | sed 's/^.*line [0-9]*: //'; p "40 rc=${PIPESTATUS[0]}"
