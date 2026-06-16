# C#Bash

*(The GitHub repo is named `csharpBash` — GitHub doesn't allow `#` in repository names.)*

A standalone **bash interpreter for Windows, written in C#** — no WSL, no msys2, no Cygwin. It targets POSIX shell core plus the most-used bashisms, and runs the common Unix command set **in-process** so everyday scripts don't pay Windows' per-process spawn cost.

> **Provenance, stated plainly.** The architecture was human-directed; the code was written, tested, and optimised by Claude (Anthropic). It was developed under **Mercurial** — the full commit history is in [`MERCURIAL_HISTORY.md`](MERCURIAL_HISTORY.md), and the real repository ships as the `mercurial-history.hg` bundle (`hg unbundle mercurial-history.hg`). The story behind it: *It's moosh all the way down* (article link to follow).

## What it does

**Language:** pipelines; lists (`&&`, `||`, `;`, `&`); redirections including fd-aware `2>`, `>>`, `<`, `/dev/null`, `/dev/std*`, `2>&1`, `1>&2`; here-documents; command substitution `$(…)` and backticks; arithmetic `$(( … ))` and `(( … ))`; brace expansion `{a,b}` / `{1..9}`; tilde and pathname (glob) expansion; single / double / `$'…'` quoting.

**Variables & structure:** assignments, `export`, `local`, special parameters (`$?`, `$#`, `$@`, `$*`, `$0…`); indexed and associative arrays; `if` / `while` / `until` / `for` / `case`; functions; `[[ … ]]` conditionals; `set -e/-u/-x/-o pipefail`.

**Interactive & runtime:** a readline-style line editor with history (`~/.bash_history`), tab completion, and `PS1`/`PS2` prompts; startup-file sourcing (login + interactive); invocation flags (`-c`, `-l`/`--login`, `--norc`, `--rcfile`, script files, shebang dispatch); `trap` (EXIT/INT); background jobs; Ctrl-C handling.

## Embedded commands

To avoid the Windows process-spawn cost, the common AT&T System V toolset is implemented in-process:

- **Shell:** `echo` `printf` `cd` `pwd` `export` `unset` `set` `shift` `read` `test`/`[` `type` `command` `eval` `source`/`.` `local` `declare`/`typeset` `trap` `jobs` `wait` `fg` `bg` `sleep` `env` `history` `exit` `return` `break` `continue` `true` `false` `:`
- **Text:** `cat` `head` `tail` `wc` `rev` `tac` `tr` `cut` `uniq` `nl` `fold` `paste` `comm`
- **Files & dirs:** `touch` `mkdir` `rmdir` `rm` `mv` `cp` `cmp` `tee` `du` `ls` `find` `split` `basename` `dirname`
- **Search & transform:** `grep`/`egrep`/`fgrep` `sed` `sort` `od` `diff` `seq` `factor` `cal` `date` `expr` `which` `xargs` `uname` `hostname` `kill` `hash`

`grep` and `sed` default to POSIX **BRE** (`-E`/`-r` for ERE); a few tools are deliberately scoped (e.g. `ls` is one-per-line, no `-l`) — see [`ARCHITECTURE.md`](ARCHITECTURE.md) §10.

## Architecture

A classic four-stage pipeline: **lexer → parser → AST → tree-walking evaluator**.

![Pipeline](docs/pipeline.svg)

- [`ARCHITECTURE.md`](ARCHITECTURE.md) — the conceptual model (including how it maps onto GNU bash's own modules, and where Windows forces it to diverge).
- [`IMPLEMENTATION.md`](IMPLEMENTATION.md) — the class/method map and an end-to-end walk-through.
- [`DECISIONS.md`](DECISIONS.md) — the append-only design log.
- [`PROJECT_CONTEXT.md`](PROJECT_CONTEXT.md) — current state and scope.

## Build · run · test

```sh
dotnet build -c Release            # builds Bash.sln (.NET 8 SDK)
Bash/bin/Release/net8.0/Bash.exe   # run it (or: Bash.exe -c '<command>')
pwsh tests/run-tests.ps1           # 36 self-checking cases
```

The test suite's expected outputs are authored from real bash semantics, not captured from the interpreter — so a regression fails rather than being silently baked in. Windows-only.

## Layout

```
Bash/        interpreter source (Lexer/ Parser/ Evaluator/ IO/) + Bash.csproj
docs/        architecture diagrams (SVG) + bash startup reference
tests/       PowerShell test runner + cases/ and expected/
tools/       bench.csx throughput harness
*.md         README + the four project docs
mercurial-history.hg   full Mercurial history (hg bundle)
```

## On Mercurial

This was built in **Mercurial**, by preference. The git repository you're reading is a published snapshot; the genuine, commit-by-commit record lives in hg, preserved here in full as [`MERCURIAL_HISTORY.md`](MERCURIAL_HISTORY.md) and the `mercurial-history.hg` bundle — `hg unbundle mercurial-history.hg` reconstructs the lot.

## License

[MIT](LICENSE).
