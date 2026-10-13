# 2026-10-12: case's ;& (run the next branch's commands too) and ;;& (go on testing the next branches), as
# bash; C#Bash rejected both as syntax errors. Outside case they stay syntax errors. Expected: GNU bash via WSL.
case a in a) echo 1;& b) echo 2;; c) echo 3;; esac; echo "rc=$?"
case a in a) echo x;;& a*) echo y;;& z) echo z;; esac; echo "rc=$?"
case q in a) echo no;& b) echo no2;; esac; echo "nomatch rc=$?"
case a in a) false;& b) ;; esac; echo "fall into empty rc=$?"
case a in a) echo f1;& b) echo f2;& c) echo f3;; d) echo never;; esac
case b in a) echo A;;& b) echo B;& c) echo C;;& d) echo D;; *) echo star;; esac
x=$(case a in a) echo in-sub;& b) echo fell;; esac); echo "[$x]"
f() { case $1 in a) echo fa;;& *) echo fany;; esac; }; f a; f z
eval 'echo a ;& echo b' 2>&1 | sed 's/^.*line [0-9]*: //'
