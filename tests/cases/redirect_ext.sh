# external-command redirects (regression: external >file used to crash the shell)
where nosuchprog_xyz123 2>/dev/null; echo "rc_dn=$?"
where nosuchprog_xyz123 2>werr.txt; echo "err_captured=$([ -s werr.txt ] && echo yes || echo no)"
whoami >wout.txt; echo "out_captured=$([ -s wout.txt ] && echo yes || echo no)"
whoami >/dev/null; echo "rc_out_dn=$?"
rm -f werr.txt wout.txt
