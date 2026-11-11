# 2026-11-10 (git-b3): cat -A / -v passed bytes 0x80-0xFF through raw; GNU shows them in M- notation (M-^? for 0xFF,
# M-^@ for 0x80, a UTF-8 é as M-CM-)). Control characters were already right. Expected: GNU via WSL.
printf 'b\xff\x80c\x01\n' | cat -A
printf 'caf\xc3\xa9\tx\r\n' | cat -A
printf 'caf\xc3\xa9\tx\n' | cat -v
printf '\x9b\xa0\xfe\n' | cat -v
