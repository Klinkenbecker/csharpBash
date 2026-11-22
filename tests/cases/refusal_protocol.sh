# 2026-11-15 (RULED: the refusal protocol, DECISIONS): a program in ~/.bash/bin that cannot serve a request exits 127
# having read and written nothing; C#Bash runs the command again from scratch with the next program of that name in
# unix-tools, ~/.bash/bin left out. A home of its own (USERPROFILE), its unix-tools PATH only (no WSL). Windows-only:
# expected authored from the ruling.
d=$(mktemp -d) && cd "$d" || exit 1
w=$(pwd -W 2>/dev/null || pwd)
mkdir -p home/.bash/bin home/.bash/etc alt other
printf 'unix-tools = PATH\n' > home/.bash/etc/csharpBash.config
export USERPROFILE="$w/home"
printf '@exit /b 127\r\n' > home/.bash/bin/tool.cmd                       # refuses everything
printf '@echo alt:%%*\r\n@findstr "^"\r\n' > alt/tool.cmd                 # the next one: its arguments, then its stdin
printf '@exit /b 3\r\n' > home/.bash/bin/fails.cmd                        # an ordinary failure: never run again
printf '@echo alt-fails\r\n' > alt/fails.cmd
printf '@exit /b 127\r\n' > other/lone.cmd                                # 127 outside ~/.bash/bin: an answer
printf '@exit /b 127\r\n' > home/.bash/bin/only.cmd                       # refuses, and nothing else has the name
export PATH="$w/home/.bash/bin;$w/other;$w/alt;$PATH"
printf 'a\nb\n' | tool x y | tr -d '\r'; echo "pipe rc=${PIPESTATUS[1]}"
tool z <<< 'hello' | tr -d '\r'; echo "here-string rc=${PIPESTATUS[0]}"
tool <<END_OF_BODY | tr -d '\r'
$(echo ran >> count.txt; echo body)
END_OF_BODY
echo "here-doc rc=${PIPESTATUS[0]} body expanded $(wc -l < count.txt) time(s)"
printf 'from a file\n' > in.txt; tool < in.txt | tr -d '\r'; echo "file rc=${PIPESTATUS[0]}"
fails; echo "fails rc=$?"
lone; echo "lone rc=$?"
only 2>&1 | sed "s|^.*line [0-9]*: ||; s|$w|W|g"; echo "only rc=${PIPESTATUS[0]}"
# 2026-11-16 (RULED with Wombat, "silent only on C#Bash's marked calls"): BASH_RERUN_ON_127=1 reaches a program only
# where its 127 is run again; removed everywhere else, an inherited one too (one written on the command: below)
printf '@if defined BASH_RERUN_ON_127 (echo marked:%%BASH_RERUN_ON_127%%) else (echo unmarked)\r\n' > home/.bash/bin/mark.cmd
cp home/.bash/bin/mark.cmd other/seen.cmd
echo -n "bare: "; mark | tr -d '\r'
echo -n "by path: "; "$w/home/.bash/bin/mark.cmd" | tr -d '\r'
echo -n "outside: "; seen | tr -d '\r'
echo -n "assigned: "; BASH_RERUN_ON_127=1 seen | tr -d '\r'
export BASH_RERUN_ON_127=1; echo -n "inherited: "; seen | tr -d '\r'; unset BASH_RERUN_ON_127
exec 3< in.txt; echo -n "unreplayable: "; mark <&3 | tr -d '\r'; exec 3<&-
# 2026-11-17 (RULED, the architect: "Your position, go"; git-9d): a mark written on the command itself is the caller's
# and goes through as written, by path too and over C#Bash's own (a program's tests against the protocol); an
# inherited or exported one is still removed (above)
echo -n "assigned, by path: "; BASH_RERUN_ON_127=1 "$w/home/.bash/bin/mark.cmd" | tr -d '\r'
echo -n "assigned through env: "; env BASH_RERUN_ON_127=1 seen | tr -d '\r'
echo -n "assigned over C#Bash's: "; BASH_RERUN_ON_127=0 mark | tr -d '\r'
# bash and sh in ~/.bash/bin are C#Bash: a 127 is a script's "not found", an answer (`bash -c 'x; exit 127'` ran twice)
printf '@echo bin-%%~n0\r\n@exit /b 127\r\n' > home/.bash/bin/bash.cmd; cp home/.bash/bin/bash.cmd home/.bash/bin/sh.cmd
printf '@echo alt-%%~n0\r\n' > alt/bash.cmd; cp alt/bash.cmd alt/sh.cmd
bash -c x | tr -d '\r'; echo "bash rc=${PIPESTATUS[0]}"
sh -c x | tr -d '\r'; echo "sh rc=${PIPESTATUS[0]}"
cd / && rm -rf "$d"
