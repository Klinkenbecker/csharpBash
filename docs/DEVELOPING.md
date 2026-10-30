# Developing C#Bash

How it is built, how it is tested, what the tools need, and where everything lives. Using it rather than building
it: [Using C#Bash](USING.md).

## Architecture

The pipeline and the four design documents: [the README's Architecture section](../README.md#architecture).

## Build · run · test

```sh
dotnet build -c Release            # builds Bash.sln (.NET 8 SDK)
Bash/bin/Release/net8.0/Bash.exe   # run it (or: Bash.exe -c '<command>')
pwsh tests/run-tests.ps1           # 233 self-checking cases (three need the Native AOT build: -Interpreter <exe>)
pwsh tests/compat/run-compat.ps1   # 237-probe differential battery against WSL's GNU bash
```

The test suite's expected outputs are never captured from the interpreter: most are GNU bash's own output (bash 5.1, run under WSL, the reference every behaviour is matched against), the rest are authored from bash's documented behaviour where a case is Windows-specific — so a regression fails rather than being silently baked in. Windows-only.

## What the build and the tests use: WSL, Git for Windows, MSYS2

**C#Bash itself needs none of them.** The shipped `Bash.exe` is one static executable: no .NET runtime, no Git, no MSYS, no WSL. When Git for Windows *is* on PATH, C#Bash puts its `usr/bin` and `mingw64/bin` in front of it, as Git Bash does (startup, step 3 in [Using C#Bash](USING.md#startup-and-command-lookup)), and an MSYS program (one with `msys-2.0.dll` or `cygwin1.dll` beside it) gets its arguments quoted and its file handles opened the way MSYS needs. Nothing of Git or MSYS ships with C#Bash.

The development tooling is another matter. Each outside system has one job, and nothing reaches for one outside that job:

| | Used by | For |
|---|---|---|
| **WSL** (`wsl.exe -e bash`, Ubuntu: GNU bash 5.1, coreutils, gawk, jq) | the battery (`tests/compat/run-compat.ps1`), the `bash -n` corpus (`tests/corpus/run-n.sh`), the `df` check (`tests/df/compare-wsl.sh`), the benchmark's third column; and by hand, to produce a test case's expected output | **The reference.** Every behaviour is matched against GNU bash on Linux. The corpus and `df` scripts run *in* WSL's bash and start C#Bash through WSL's interop. |
| **Git for Windows** (PortableGit, `F:/Tools/PortableGit`, configured at the top of each script) | the benchmark (`tests/bench/compare3.sh`, and the older `compare.sh`), `tools/publish.sh` | **Git Bash** is one of the shells the benchmark times, and the benchmark runs in it (it refuses C#Bash). **`git.exe`** is how `publish.sh` pushes to GitHub (the project's VCS is Mercurial). Nothing else reaches Git; the development machine keeps it off PATH. |
| **Git for Windows on PATH** (`git.exe`, and the MSYS tools in its `usr/bin`) | seven suite cases: `msys_redirects`, `msys_arguments`, `msys_inherited_append`, `native_args_msys`, `native_args_git`, `dev_tty_git_prompt`, `coreutils_external` | They test how C#Bash runs MSYS programs and git. **Without them, the case prints `SKIP: <reason>`** and the runner names it in its summary: a skip, never a pass. |
| **MSYS2** (`F:/Tools/msys64`) | one suite case, `msys_append_shared` (its `ls.exe`) | Same; SKIP without it. |
| **A PATH `awk`** | one suite case, `awk_fallthrough` | What the in-process awk hands to an external awk; SKIP without one. |
| **Visual Studio 2022 C++ tools** (`vcvars64.bat`, `vswhere.exe`) | `tools/publish-aot.cmd` (and so `tools/deploy.sh` and `compare3.sh --build`) | The Native AOT link step. |

So a machine with the .NET 8 SDK and WSL runs the whole suite (with those nine skipped) and the battery. The benchmark and the push also need Git for Windows; the shipped build also needs the VS C++ tools. `tests/bench/claudecall.sh` (what a Claude Code call costs) still converts paths with `cygpath`, which comes with Git, so for now it runs only from Git Bash.

## Layout

```
Bash/        interpreter source (Lexer/ Parser/ Evaluator/ IO/) + Bash.csproj
docs/        the guides (USING, FEATURES, PERFORMANCE, DEVELOPING), architecture diagrams (SVG), bash startup
             reference, the installer plan, the module documents
tests/       PowerShell test runner + cases/ and expected/; compat/ (the differential battery); bench/compare3.sh (the benchmark;
             --build measures the committed tree against the deployed build); bench/claudecall.sh (what a Claude Code
             Bash call costs, and where it goes); corpus/run-n.sh (bash -n against GNU on 912 real scripts)
tools/       publish-aot.cmd (the ship build); deploy.sh, publish.sh and release.sh (the project's own deploy, push and
             GitHub release); benchtimer (the benchmark's launcher and clock); bench.csx, attrib.csx
*.md         README + the four project docs
mercurial-history.hg   full Mercurial history (hg bundle)
```

## On Mercurial

This was built in **Mercurial**, by preference. The git repository you're reading is a published snapshot; the genuine, commit-by-commit record lives in hg, preserved here in full as [`MERCURIAL_HISTORY.md`](../MERCURIAL_HISTORY.md) and the `mercurial-history.hg` bundle — `hg unbundle mercurial-history.hg` reconstructs the lot.
