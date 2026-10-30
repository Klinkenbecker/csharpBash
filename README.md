# C#Bash - A bash designed for Windows and Claude

You don't need WSL, MSYS2, Cygwin or .NET: it is one program. It also runs nearly 80 of the everyday Unix commands itself (`grep`, `sed`, `awk`, `ls`, `find`, `sort` and the rest), so a script doesn't start a new Windows process for every command in it.

It was built to be the shell Claude Code uses on Windows. Claude Code starts a fresh shell for every tool call, and that is exactly what C#Bash is fast at.

**Download:** [the latest `Bash.exe`](https://github.com/Klinkenbecker/csharpBash/releases/latest/download/Bash.exe) (one file, no .NET runtime needed) · [all releases](https://github.com/Klinkenbecker/csharpBash/releases), each with its SHA-256.

> **Provenance, stated plainly.** The architecture was human-directed; the code was written, tested, and optimised by Claude (Anthropic). It was developed under **Mercurial**: the full commit history is in [`MERCURIAL_HISTORY.md`](MERCURIAL_HISTORY.md), and the real repository ships as the `mercurial-history.hg` bundle (`hg unbundle mercurial-history.hg`). The story behind it: [*It's moosh all the way down*](https://www.linkedin.com/pulse/its-moosh-all-way-down-guy-mcilroy-mer9c/).

**Money talks, bullshit walks:**

| benchmark (seconds) | C#Bash | Git Bash | WSL bash | vs Git | vs WSL |
|---|---:|---:|---:|---:|---:|
| `loop` — 200 k `while` iterations | **0.153** | 1.490 | 0.400 | 9.7× | 2.6× |
| `arith` — 200 k `$(( ))` evaluations | **0.242** | 2.064 | 0.615 | 8.5× | 2.5× |
| `func` — 100 k function calls | **0.211** | 2.301 | 0.573 | 10.9× | 2.7× |
| `loop_big` — 2 M iterations | **1.418** | 15.673 | 3.440 | 11.1× | 2.4× |
| `coreutils` — 900 `basename`/`dirname`/`wc` calls | **0.053** | 14.566 | 0.802 | **275×** | 15.1× |
| `pipeline` — `grep\|sed\|sort\|uniq\|awk` over 50 k lines | **0.081** | 0.248 | 0.116 | 3.1× | 1.4× |
| `find` — walk 400 files | **0.015** | 0.132 | 0.209 | 8.8× | 13.9× |
| startup — `bash -c 'exit 0'` | **0.0086** | 0.0275 | 0.1087 | 3.2× | 12.6× |

Every row gives the same output in all three shells, so a fast wrong answer can't count as a win. Where the speed
comes from:

- **No new process per command.** The `coreutils` row runs 900 small commands. Under Git Bash that is 900 Windows
  processes; under C#Bash it is none.
- **It starts fast:** 8.6 ms, against Git Bash's 27.5 ms or WSL's 108.7 ms. Claude Code pays that on every call.
- **It remembers where programs are.** On Windows, finding a program on a long PATH is slow, and not finding one is
  slower still. C#Bash remembers both, so a simple Claude Code call takes about 11 ms in all.

How it is measured, the caveats, and where each millisecond of a call goes: [Performance](docs/PERFORMANCE.md).

## Architecture

A classic four-stage pipeline: **lexer → parser → AST → tree-walking evaluator**.

![Pipeline](docs/pipeline.svg)

- [`ARCHITECTURE.md`](ARCHITECTURE.md) — the conceptual model (including how it maps onto GNU bash's own modules, and where Windows forces it to diverge).
- [`IMPLEMENTATION.md`](IMPLEMENTATION.md) — the class/method map and an end-to-end walk-through.
- [`DECISIONS.md`](DECISIONS.md) — the append-only design log.
- [`PROJECT_CONTEXT.md`](PROJECT_CONTEXT.md) — current state and scope.

## Use it with Claude Code

1. Download `Bash.exe` (above) and put it in a folder of its own, say `~/.bash/bin` (`%USERPROFILE%\.bash\bin`).
2. Point Claude Code at it;
   -  Command Prompt: `setx CLAUDE_CODE_GIT_BASH_PATH "%USERPROFILE%\.bash\bin\Bash.exe"`.
   -  PowerShell: `setx CLAUDE_CODE_GIT_BASH_PATH $env:USERPROFILE\.bash\bin\Bash.exe`.
3. Restart Claude Code.

To go back to Git Bash, remove the variable and restart Claude Code. If Git for Windows is on your PATH, Claude Code may pick Git
Bash anyway (Claude Code issue #94077); the start file in the guide avoids that, and keeps Claude Code's own PATH
short. Building it yourself, the start file, and tips: [Using C#Bash](docs/USING.md).

## What you get

- **bash itself:** pipelines, redirections, here-documents, `$( )`, arithmetic, indexed and associative arrays,
  functions with bash's scoping, `[[ ]]`, `case`, traps, background jobs, `set -euo pipefail`. Behaviour is matched
  against GNU bash 5.1.
- **The everyday tools, built in:** text (`grep`, `sed`, `awk`, `sort`, `cut`, `tr`, `head`, `tail`), files (`ls`,
  `find`, `cp`, `mv`, `rm`, `stat`), and `jq`, `diff`, `xargs`, `date` and more. They stream, so a 2 GiB file or an
  endless pipe is fine.
- **Windows paths both ways:** `/c/Users/you` and `C:/Users/you` both work, and paths come back with forward slashes,
  which a script can safely take apart.
- **Bytes stay bytes:** data that isn't valid UTF-8 survives variables, `$( )`, pipes and files unchanged.
- **Help that points at the manual:** `help grep` or `grep --help` gives you the GNU manual page for it.
- **An interactive shell too:** line editing, history, tab completion, prompts.

Everything, in detail: [Features](docs/FEATURES.md).

## What it won't do

- **Quietly ignore an option it doesn't support.** It says so, and if the real program is on your PATH, it runs that
  instead. Each such request goes into a log in your own profile (nothing is sent anywhere), which is our list of what
  to add next.
- **A few bash features are missing,** and say so rather than pretend: restricted mode, POSIX mode, programmable
  completion (`complete`), and `>( )`.
- **Windows' own limits still apply:** a program whose path is over 260 characters can't be started, and a file name
  can't end in a dot or a space.

## How it's tested

208 test cases, most of them checked against GNU bash's own output (bash 5.1 under WSL), never against C#Bash's, so a
bug fails a test instead of becoming its expected answer. A battery of 237 probes runs the same commands through both
shells and compares them. Building and testing it yourself, and how the source is laid out: [Developing C#Bash](docs/DEVELOPING.md).

## License

[MIT](LICENSE).
