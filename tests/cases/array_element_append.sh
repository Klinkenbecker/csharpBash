# 2026-10-16: `name[subscript]+=value` appends to the element, as bash. It was not an assignment at all: it ran
# as a command ("r[k]+=a: command not found") and the value was lost, for indexed and associative arrays,
# literal or expanded subscripts. Under `declare -i` the append adds, as `x+=v` does. Expected = GNU via WSL.
declare -A m; m[k]+=a; m[k]+=b; n=k; m[$n]+=" c"; m["$n"]+=$(echo d); echo "[${m[k]}]"
a=(x y); a[0]+=1; i=1; a[$i]+=2; a[i+1]+=new; echo "${a[@]} (${#a[@]})"
declare -ai c=(5); c[0]+=3; c[0]+='2*2'; echo "${c[0]}"
s=abc; s[0]+=x; echo "$s"
unset u; u[3]+=z; declare -p u
f() { local -a l=(p); l[0]+=q; echo "${l[0]}"; }; f
declare -a d=(1); declare d[0]+=2; echo "${d[0]}"
b[0]=a; b[0]+=; echo "[${b[0]}]"
e[0]+=$'1\t2' 2>&1; printf '%q\n' "${e[0]}"
g() { m[k]+=v; }; declare -f g
