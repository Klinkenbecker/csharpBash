# The C#Bash installer

**Shipped 2026-11-01** in GitHub release `hg-600`, beside `Bash.exe`: `csharpBash-setup.exe` and its SHA-256. Built with
InstallGen specification 2. The history of every decision here is in [`DECISIONS.md`](../DECISIONS.md) (2026-10-20 to
2026-11-01).

## What it does

- Puts `Bash.exe`, `sh.exe` and the start file `claude.cmd` in `%USERPROFILE%/.bash/bin`. `sh.exe` is a hard link to
  `Bash.exe` (one file), re-made and confirmed on every install; where a volume cannot hold a hard link, a copy, and the
  report says so.
- Asks one question, `Add [<folder>] to your PATH? [y/N]: `; an empty answer, and `--silent`, take the default, no.
  With the folder on PATH, cmd and PowerShell reach `bash` and `sh`, and GNU make runs its recipes through this `sh`.
- Run again, it repairs what differs and writes nothing else. It upgrades in place while C#Bash is running: a running
  copy is set aside and removed by a later install, and the next launch is the new build.
- Changes nothing else on the machine. No .NET runtime is needed, by the installer or by `Bash.exe`. To remove it:
  delete `%USERPROFILE%/.bash/bin`, and its PATH entry if one was added.
- `~/.bash/help.json` is not installer payload: `Bash.exe` replaces an unedited earlier built-in table itself and never
  touches an edited one.

## The start file

`claude.cmd` starts Claude Code (`%USERPROFILE%/.local/bin/claude.exe`, arguments passed through) with C#Bash as its
shell, for that session only: `BASH_PATH` = the caller's PATH, `CLAUDE_CODE_GIT_BASH_PATH` = the `Bash.exe` beside it,
`PATH` = that folder alone. It finds its own folder, so it works wherever it was installed. When Claude Code is not at
that path it says so and exits 1.

## How it is built and released

- `bash tools/installer.sh` stages the payload from the DEPLOYED `dist/Bash.exe`, `installer/claude.cmd` (CRLF) and
  `installer/csharpBash.json` (its version from the build stamp), builds with InstallGen's release gate (every warning
  refused) to `dist.installer/csharpBash-setup.exe` and its `.sha256`, then proves it with a sandboxed install: the
  stamp, `sh.exe` a confirmed hard link, the start file. InstallGen's location is this machine's:
  `tools/installgen.path` (ignored) or `INSTALLGEN`.
- `tools/release.sh` (the publish's last step) runs it and attaches the installer and its `.sha256` beside `Bash.exe`,
  then downloads both back and compares. A publish whose `Bash.exe` the newest release already carries makes no
  release, so an installer-only change ships with the next build.
- `installer/try-install.cmd` (placed beside the installer) does a real install from a Command Prompt, then checks it.

## Checked

Sandboxed: a fresh install, a re-run, a repair of a broken link, an upgrade with the old build running under both
names, a home path with a space, Claude Code absent, a Claude Code session through the start file running C#Bash, the
PATH line character for character. Real: the architect's installs on his machine (2026-11-01, "perfect!"). Not
checked: a double-click from Explorer, a downloaded copy's SmartScreen warning (it is unsigned), another machine, exFAT.

## Ruled

C#Bash owns its installer (PortableBash its own). Per-user, no elevation, everything under `~/.bash`. An upgrade
replaces `Bash.exe` in place. PATH is the user's choice, default no, in the architect's wording. Names: `claude.cmd`,
`csharpBash-setup.exe`. No license prompt (MIT). Released with each publish, on the same repository as the source.
Authenticode only if the project gets public traction.
