# Developing C#Bash

How it is built, how it is tested, what the tools need, and where everything lives. Using it rather than building
it: [Using C#Bash](USING.md).

## Architecture

The pipeline and the four design documents: [the README's Architecture section](../README.md#architecture).

## Build · run · test

```sh
dotnet build -c Release            # builds Bash.sln (.NET 10 SDK)
Bash/bin/Release/net10.0/Bash.exe   # run it (or: Bash.exe -c '<command>')
bash tests/run-tests.sh                    # 338 self-checking cases (six need the Native AOT build: five run a lone copy of the shell, icu_lazy checks how AOT loads ICU; --interpreter <exe>; --detached as Claude Code starts a shell)
bash tests/compat/run-compat.sh            # 237-probe differential battery against WSL's GNU bash
```

The test suite's expected outputs are never captured from the interpreter: most are GNU bash's own output (bash 5.1, run under WSL, the reference every behavior is matched against), the rest are authored from bash's documented behavior where a case is Windows-specific — so a regression fails rather than being silently baked in. Windows-only.

## The supported toolchain

C#Bash is a public project and builds only with publicly available tools that cost nothing. On a
clean Windows 10 or 11 (x64), two downloads, both from Microsoft:

1. **The .NET 10 SDK** (https://dotnet.microsoft.com/download/dotnet/10.0). `global.json` asks for 10.0.404 or a later
   10.0 feature band. MIT-licensed; the runtime parts that a single-file or Native AOT build carries are under the .NET
   Library License.
2. **Build Tools for Visual Studio 2022**, the C++ tools and a Windows SDK only (no IDE). Unattended, as an
   administrator, with the bootstrapper from https://aka.ms/vs/17/release/vs_buildtools.exe:

   ```
   vs_buildtools.exe --quiet --wait --norestart --nocache --add Microsoft.VisualStudio.Component.VC.Tools.x86.x64 --add Microsoft.VisualStudio.Component.Windows11SDK.26100
   ```

   Name the two components: `--add Microsoft.VisualStudio.Workload.VCTools` alone installs no linker (both are only
   recommended in that workload). Any later or larger Visual Studio holding the same two components serves too.

**The license condition:** Build Tools is free to download, and Microsoft licenses it as a supplement to Visual
Studio, so its use rests on a Visual Studio Community entitlement. Community's terms let "any number of your users ...
develop and test applications released under Open Source Initiative (OSI) approved open source software licenses";
C#Bash is MIT. Nothing here is open source but the .NET SDK, and nothing costs money. There is no open-source route
today: .NET's Native AOT runtime libraries are built with Microsoft's C++ compiler and link only with its C runtime.

`tools/publish-aot.cmd` finds the newest released install holding the C++ tools through `vswhere`, runs its
`vcvars64.bat`, and links with exactly that install (`-p:IlcUseEnvironmentalTools=true`); a prerelease Visual Studio is
never used. Not yet proven on a clean machine.

## What the build and the tests use: WSL, Git® for Windows, MSYS2

**C#Bash itself needs none of them.** The shipped `Bash.exe` is one static executable: no .NET runtime, no PortableGit, no MSYS, no WSL. An MSYS program (one with `msys-2.0.dll` or `cygwin1.dll` beside it) gets its arguments quoted and its file handles opened the way MSYS needs. Nothing of PortableGit or MSYS ships with C#Bash.

The development tooling is another matter. Each outside system has one job, and nothing reaches for one outside that job:

| | Used by | For |
|---|---|---|
| **WSL** (`wsl.exe -e bash`, Ubuntu: GNU bash 5.1, coreutils, gawk, jq) | the battery (`tests/compat/run-compat.sh`), the `bash -n` corpus (`tests/corpus/run-n.sh`), the `df` check (`tests/df/compare-wsl.sh`), the benchmark's third column, the `fallback_chain` case's WSL part (a `SKIP` line without WSL); and by hand, to produce a test case's expected output | **The reference.** Every behavior is matched against GNU bash on Linux. The corpus and `df` scripts run *in* WSL's bash and start C#Bash through WSL's interop. |
| **Git® for Windows** (PortableGit, configured at the top of each script: the benchmark pins 2.28.0 in the folder its `GITBASH` line names, `publish.sh` uses the one its `GIT` line names) | the benchmark (`tests/bench/compare3.sh`, and the older `compare.sh`), `tools/publish.sh` | **Git® Bash** (PortableGit's bash) is one of the shells the benchmark times; either PortableGit's bash or C#Bash may run it, and its table names which (C#Bash is the reference way since 2026-11-14). **`git.exe`** is how `publish.sh` pushes to GitHub (the project's VCS is Mercurial). Nothing else reaches PortableGit; the development machine keeps it off PATH. |
| **MSYS**, optional (`tests/cases/msys_pin.inc`: pinned to that PortableGit 2.28.0's `usr/bin`; else the first PATH folder holding `msys-2.0.dll`) | nine suite cases: `coreutils_external`, `device_names`, `msys_arguments`, `msys_append_shared`, `msys_inherited_append`, `msys_redirects`, `native_args_msys`, `rm_reserved_names`, `awk_fallthrough` | They test how C#Bash runs MSYS programs. **Without an MSYS, the case prints `BYPASSED: <reason>`**: shown in red, named on its own summary line (`!!! N BYPASSED, NOT TESTED`), repeated by `deploy.sh`, never a failure. |
| **git on PATH**, optional | two suite cases: `native_args_git`, `dev_tty_git_prompt` | How C#Bash runs git. Without it, the case prints `SKIP: <reason>`: a skip, never a pass. |
| **The Visual Studio C++ tools**: any edition (Build Tools for Visual Studio is enough) with the components `Microsoft.VisualStudio.Component.VC.Tools.x86.x64` and a Windows SDK (`...Windows11SDK.26100`); `vswhere.exe` comes with the installer | `tools/publish-aot.cmd` (and so `tools/deploy.sh` and `compare3.sh --build`), which finds the newest released install holding the C++ tools through vswhere | The Native AOT link step. |

So a machine with the .NET 10 SDK and WSL runs the whole suite (the MSYS cases bypassed and the git cases skipped when those are absent) and the battery. The push also needs PortableGit; the benchmark uses PortableGit's bash and WSL when they are there ([below](#running-the-benchmark)); the shipped build also needs the VS C++ tools. `tests/bench/claudecall.sh` (what a Claude Code call costs) still converts paths with `cygpath`, which comes with PortableGit, so for now it runs only from PortableGit's bash.

## Running the benchmark

`tests/bench/compare3.sh` times C#Bash (the deployed `dist/Bash.exe`) against PortableGit's bash and WSL's GNU bash. Either of
those two may be missing: its column is left out, with a note. With neither, the benchmark is bypassed: a loud
`BYPASSED` line, exit 0, nothing timed and nothing appended.

It uses:
- **`tools/benchtimer`**, built: `dotnet build tools/benchtimer -c Release -o tools/benchtimer/bin`. A launch that runs past
  300 s is killed with every process under it and its cell reads `TIMEOUT`; the run goes on. It starts and times
  every shell, and runs the sieve baseline.
- **PortableGit's bash**, optional: Git® for Windows v2.28.0.windows.1's 64-bit PortableGit (46,902,448 bytes, SHA-256
  `0cd682188b76eeb3a5da3a466d4095d2ccd892e07aae5871c45bf8c43cdb3b13`), unpacked whole into a folder of its own,
  the one `GITBASH` names at the top of compare3.sh. Nothing is installed and nothing goes on PATH. Any version
  runs, shown under the column title; `4.4.23(1)-release` is the one every earlier table used.
- **WSL**, optional: `wsl.exe` with a Linux distribution whose bash is GNU bash, able to see the repository under
  `/mnt/<drive>` (a WSL that cannot see it stops the run).
- **`C:/Windows/System32/HOSTNAME.EXE`**, the launch baseline (part of Windows).

Run it from the repository root, on a quiet machine, either way:

```sh
dist/Bash.exe tests/bench/compare3.sh                              # C#Bash runs it: the reference way (2026-11-14)
<PortableGit-2.28.0>/bin/bash.exe tests/bench/compare3.sh    # Git Bash runs it too (rows agree within a few percent)
```

At a publish, once README's table is updated from the run, `bash tools/perf-history.sh --add` appends the deployed
build's rows to `docs/perf-history.tsv` and redraws `docs/perf-history.svg`, the advantage graph at the top of
[Performance](PERFORMANCE.md); `tools/publish.sh` refuses to publish without it. Each run appends its table, with the host that ran it, to `tests/bench/results.md`. `SIEVE_REF` and `LAUNCH_REF` at the
top of compare3.sh are this machine's quiet references; on another machine set your own from a few quiet runs of
`tools/benchtimer/bin/benchtimer.exe --sieve 9` and `benchtimer.exe 100 C:/Windows/System32/HOSTNAME.EXE`, or leave them
empty (the baseline lines then show no comparison).

## Layout

```
Bash/        interpreter source (Lexer/ Parser/ Evaluator/ IO/) + Bash.csproj
docs/        the guides (USING, FEATURES, PERFORMANCE, DEVELOPING), architecture diagrams (SVG), bash startup
             reference, the installer plan, the module documents
tests/       the test runner (run-tests.sh, cases.tsv) + cases/ and expected/; compat/ (the differential battery); bench/compare3.sh (the benchmark;
             --build measures the committed tree against the deployed build); bench/claudecall.sh (what a Claude Code
             Bash call costs, and where it goes); corpus/run-n.sh (bash -n against GNU on 912 real scripts)
tools/       publish-aot.cmd (the ship build); deploy.sh, publish.sh, release.sh and installer.sh (the project's own deploy, push,
             GitHub release and installer; the installer's manifest and start file are in installer/); benchtimer (the
             benchmark's launcher and clock, and its --sieve baseline); bench.csx, attrib.csx
*.md         README + the four project docs
mercurial-history.hg   full Mercurial history (hg bundle)
```

## On Mercurial

This was built in **Mercurial**, by preference. The git repository you're reading is a published snapshot; the genuine, commit-by-commit record lives in hg, preserved here in full as [`MERCURIAL_HISTORY.md`](../MERCURIAL_HISTORY.md) and the `mercurial-history.hg` bundle — `hg unbundle mercurial-history.hg` reconstructs the lot.
