# What C#Bash does

The [README](../README.md) has the highlights. This is the full list: the language, and every command it runs
in-process. How its help and its log of unsupported requests work: [Using C#Bash](USING.md).

## The language

**Language:** pipelines; lists (`&&`, `||`, `;`, `&`); redirections including fd-aware `2>`, `>>`, `<`, `&>`, `2>&1`, `1>&2`, numbered descriptors (`exec 3>f`, `exec 3>&1`, `>&3`); Linux's device files (`/dev/null`, `/dev/zero`, `/dev/urandom`, `/dev/stdin`, `/dev/stdout`, `/dev/stderr`, `/dev/fd/N`, `/dev/tty`, which is "No such device or address" where there is no terminal to type into, as in Claude Code) as redirect targets, as file arguments and under `test`; here-documents and here-strings; command substitution `$(…)` and backticks (nested, isolated like a subshell); arithmetic `$(( … ))`, `(( … ))` and `for (( ; ; ))` with assignment and `++`; brace expansion `{a,b}` / `{1..9}`; tilde and pathname (glob) expansion with `nullglob`/`dotglob`/`globstar`/`nocaseglob`; single / double / `$'…'` quoting with bash's exact backslash rules. A script, a `-c` string, `source` and `eval` are read and run one line at a time, as bash reads them: the lines before a syntax error run, and nothing after an `exit` is read (an installer's payload).

**Variables & structure:** assignments and `+=` (to an array element too: `a[k]+=v`), `export`, `local`, `readonly`, `declare -i/-a/-A/-r/-x/-p/-f`, special parameters (`$?`, `$#`, `$@`, `$*`, `$0…`, `$!`, `$-`, `PIPESTATUS`, `BASH_REMATCH`, `LINENO`, `FUNCNAME`, `BASH_SOURCE`, `BASH_LINENO`, `RANDOM`, `PPID`, `UID`, `BASH_COMMAND`); the parameter-expansion operators (`${x:-y}` `${x:n:m}` `${x#pat}` `${x/pat/rep}` `${x^^}` `${!x}` `${arr[@]:n}` …); indexed and associative arrays; `if` / `while` / `until` / `for` / `case` (with `;&` and `;;&`); functions (both syntaxes), with bash's dynamic scoping (`local` arrays and attributes included); `[[ … ]]` with `&&`/`||`/`( )`, `=~` and pattern matching; `set -e/-u/-x/-T/-o pipefail`, `shopt`; field splitting by IFS as POSIX and bash define it (`IFS=` splits nothing, `IFS=,` keeps empty fields).

**Paths:** MSYS drive-letter form (`/c/Users/…`) and `/tmp` are understood alongside native `C:\…`, and the conversion is symmetric — `pwd`, `$PWD`, `mktemp` and `realpath` hand back `E:/dir/file`, a valid Windows path containing no backslashes. That matters because backslash is the shell's *escape* character: a path with backslashes in it is a value the shell's own `${p#…}`, globs, `case` and `[[ … ]]` would misread.

**Bytes:** a shell string is a byte sequence, so `printf '\377'` emits one byte, not the UTF-8 encoding of a character, and data that is not valid UTF-8 survives variables, `$(…)`, pipes and files intact. (It survives NUL too, which real bash cannot hold.)

**Interactive & runtime:** a readline-style line editor with history (`~/.bash_history`), tab completion, and `PS1`/`PS2` prompts; startup-file sourcing (login + interactive); invocation flags (`-c`, `-l`/`--login`, `--norc`, `--rcfile`, script files, shebang dispatch); `trap` (EXIT, INT, ERR, DEBUG with `$BASH_COMMAND`, RETURN); background jobs (an and-or list ending in `&` is one job; jobspecs `%N` `%%` `%-` `%name` `%?text`); Ctrl-C handling. Restricted mode (`-r`, `--restricted`, `rbash`, `set -r`) is not implemented, so the shell refuses it rather than run unrestricted; POSIX mode (`--posix`, `set -o posix`) says it is not available and goes on. Both print a link that opens a prefilled GitHub issue to ask for the feature (nothing is sent by itself). `compgen` lists this shell's own names; programmable completion (`complete`, `compopt`) is not implemented and says so.

## Embedded commands

To avoid the Windows process-spawn cost, the common AT&T System V toolset is implemented in-process:

- **Shell:** `echo` `printf` (incl. `-v`) `cd` `pwd` `pushd` `popd` `dirs` `export` `unset` `readonly` `set` `shopt` `shift` `read` `mapfile`/`readarray` `getopts` `test`/`[` `type` `command` `builtin` `alias` `unalias` `eval` `exec` `source`/`.` `local` `declare`/`typeset` `let` `trap` `jobs` `wait` `fg` `bg` `disown` `kill` `sleep` `env` `history` `times` `umask` (stored and printed only: Windows files have no mode bits, so it does not change the files C#Bash creates) `fc` (says history editing is not supported) `compgen` `hash` (incl. `-r`, `-d`, `-p`, `-t`, `-l`) `help` `enable` (lists; `-n` not available) `exit` `return` `break` `continue` `true` `false` `:`
- **Text:** `cat` `head` `tail` `wc` `rev` `tac` `tr` `cut` `uniq` `nl` `fold` `paste` `comm` `seq` `split` `od` `sort` `tee` `base64` `md5sum`/`sha1sum`/`sha256sum`/`sha512sum` `hexdump` `strings` (GNU strings 2.38, with `-e l` for UTF-16) `yes`. They read as they go, so an input over 2 GiB or an endless pipe works (`yes | sed 3q` ends); `sort`, `diff` and `jq` hold their whole input, as they must, and `xargs` still reads all of its input before it runs anything.
- **Files & dirs:** `touch` (incl. `-d '40 days ago'`-style dates) `mkdir` `rmdir` `rm` `mv` `cp` `cmp` `du` `ls` `find` (incl. `-name`, `-type`, `-mmin`) `stat` `mktemp` `realpath` `readlink` `chmod` `ln` `truncate` `basename` `dirname`
- **Search & transform:** `grep`/`egrep`/`fgrep` (incl. `-P` with `\K`) `sed` `awk` (a documented subset, with user functions, `print > file`, `getline` and `getline < file`, 3-argument `match`) `jq` (a jq 1.6 subset, with `-R`/`-Rs`) `diff` (incl. `-u` and `--label`) `xargs` `expr` `factor` `cal`
- **System:** `date` (incl. `-d` "3 days ago"-style parsing) `uname` `hostname` `arch` `nproc` `tty` `whoami` `id` `printenv` `timeout` `nohup` `setsid` `which` `kill` `pgrep` `pkill` `ps` (Windows has no procps; `pkill -f` never kills this shell's own ancestors) `df` (every drive, mapped shares included, in GNU's format)

Every tool parses its options strictly: an option (or awk construct) that is not implemented is
**never silently ignored** — the tool reports it and, when a real external of the same name is on
PATH, re-runs the command there (`BASH_COREUTILS=builtin` forbids that, `=external` prefers it). Windows' own
programs of the same name (System32's `find.exe`, `sort.exe`, `timeout.exe`) are other tools and are never used.
`grep` and `sed` default to POSIX **BRE** (`-E`/`-r` for ERE). Process substitution `<( )` works
(temp-file emulation); `>( )` is refused with a clear message. See [`ARCHITECTURE.md`](../ARCHITECTURE.md) §10.
