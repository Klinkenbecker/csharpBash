hash -r; echo "clear rc=$?"
hash whoami; echo "found rc=$?"
hash nosuchcmd_xyz123 2>/dev/null; echo "missing rc=$?"
hash -p /custom/path/foo myfoo; echo "set rc=$?"
