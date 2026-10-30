# C#Bash installer plan

**Status: BLOCKED (2026-10-29)** on InstallGen learning to replace a file in use (Installer project, `REGISTER.md`
`INST-Q-046`). Not ratified; kept current so it is ready when that lands. DECISIONS 2026-10-29: "RULED: C#Bash owns
its installer", and the entry that blocks it.

**The goal.** Someone with Claude Code on Windows gets C#Bash without building it (no .NET SDK, no MSVC): download one
installer, run it, start Claude Code through the start file it writes.

**Ruled (do not re-open):** C#Bash owns its installer; PortableBash its own (2026-10-29). Built with InstallGen
(`E:/Claude/Installer/release`; 2026-10-20). Per-user, no elevation; everything under `~/.bash`, `~/.bash/bin/Bash.exe`
the only thing meant for PATH (2026-10-20). Authenticode only if the project gets public traction (2026-10-20). An
upgrade replaces the running `Bash.exe` in place, through InstallGen (2026-10-29), not through versioned folders.

## Phases (each done when its check is observed)

1. **A release asset per publish.** `tools/publish.sh` also attaches `dist/Bash.exe` and its SHA-256 to a GitHub
   Release named after the hg rev, on the SAME repository as the source (`Klinkenbecker/csharpBash`, its Releases
   page; ruled 2026-10-29). Step 5 pushes the source to `main`; step 6 then makes the release, tagged `hg-<rev>` on the
   commit just pushed (the tags exist on GitHub only, not in hg). The README links both (the architect, 2026-10-29):
   `releases/latest/download/Bash.exe` (always the newest) and `releases` (all, each with its SHA-256), under the
   opening description and in "Using it as Claude Code's shell" (rev 471; they work from the first release on). *Done when:* the asset downloaded from the release URL matches its SHA-256 and
   prints the published rev in `--version`. *(Does not depend on InstallGen.)* **BUILT 2026-10-29 (rev 468):**
   `tools/release.sh` (publish.sh's step 6; re-runnable alone; `--dry-run`): release `hg-<rev>` at the pushed commit,
   assets `Bash.exe` + `Bash.exe.sha256`, then downloaded back and compared. **DONE 2026-10-29:** release hg-473 (the
   first), its check observed: downloaded back matching; through the README's `releases/latest/download/Bash.exe`,
   `sha256sum -c` OK and the download runs, reporting its build (472, the deployed one).
   (The binary is the deployed one, so it names its own rev, which may be older than the published rev, never newer.)
   A publish whose Bash.exe the newest release already carries makes no release (rev 478): the page lists builds.
2. **help.json kept fresh by Bash.exe itself.** Bash.exe carries the SHA-256 of every built-in table it has shipped;
   a `~/.bash/help.json` equal to one of them is replaced by the current table, an edited one is left alone and never
   touched. *Done when:* three cases pass: an old table replaced, an edited one byte-identical after, a missing one
   written. *(Does not depend on InstallGen; help.json is NOT installer payload: check-and-repair would overwrite a
   user's edits on every re-run, `CONTRACT.md` 345-347.)* **BUILT 2026-10-29 (rev 469):** `EarlierHelpTables` in
   `Builtins.Help.cs` (rev 397's hash, the only earlier table); test `help_refresh` (all three cases plus an edited
   table's own added name still answering), red on 462 and with the refresh disabled. Not deployed yet.
3. **The manifest and the installer.** An InstallGen manifest (schema 1): place `home` fixed `%USERPROFILE%/.bash`,
   `bin` = `home:bin`; artefacts `Bash.exe` -> `bin:Bash.exe`, `Bash.exe` -> `bin:sh.exe` (the same build again, for
   `sh`, as Git for Windows ships its own; ruled 2026-10-30) and the start file (below) -> `bin:`. Built by
   `InstallGen --manifest ... --payload ... --out ...`, its `.sha256` beside it. *Done when:* under the test-redirection
   spec, a fresh install puts the three files in place and the report lists them (`sh.exe` running is the same
   in-use case as `Bash.exe`). **Released with each publish** (the architect, 2026-10-30: "include in the installer
   and release the installer when we publish"): `tools/release.sh` attaches the installer and its `.sha256` beside
   `Bash.exe`; *done when* the installer downloaded from the release matches its SHA-256 and installs the rev it names.
   Note for the `path.add` question below: with `bin` on PATH, its `sh.exe` makes GNU make run every recipe through it
   (measured 2026-10-30), as `dist/sh.exe` does here; ruled acceptable for this machine (C#Bash's mandate), not yet
   for a user's. **BLOCKED on INST-Q-046** for the upgrade
   case: *done when* a re-run with a newer Bash.exe, while a copy of the old one runs, completes; the running process
   is unaffected; a new launch prints the new rev; the report says the old copy was set aside (in INST-Q-046's form as
   refined by the architect: a `.deleted` folder on the same drive, swept at the start of the next run).
4. **The start file.** The README's `claude.cmd` (BASH_PATH = the caller's PATH; CLAUDE_CODE_GIT_BASH_PATH and PATH set
   for that one process; `%USERPROFILE%/.local/bin/claude.exe`), written by the installer: no persistent variable
   anywhere (the architect's Installer ruling, `CONTRACT-PLAN.md` 297; a global CLAUDE_CODE_GIT_BASH_PATH also loses
   to PATH, Claude Code issue #94077). *Done when:* a session started by it reports C#Bash's `$BASH_VERSION`.
5. **The README.** Which download its links lead with, once the installer exists: the architect's leaning (2026-10-29,
   NOT a ruling, "open to argument at the time"): both, the installer and the bare Bash.exe. Install = download,
   check the SHA-256, run; expect SmartScreen's warning on an unsigned download
   [recalled, not tested]; the `setx` step replaced by the start file; uninstall by hand (InstallGen v1 has none,
   `CONTRACT.md` 466): the folder, the start file, nothing else changed.

## Must NOT be decided during the work (do not derive; ask)

- The start file's name and whether `~/.bash/bin` goes on PATH (`path.add`): on PATH, `Bash.exe` could shadow other
  `bash` programs for cmd and PowerShell, and a `claude.cmd` there loses to `claude.exe` earlier on PATH.
  **Direction (the architect, 2026-10-30, Claude agrees): PATH is the user's choice, asked by the installer.** InstallGen
  cannot ask it today (`path.add` is unconditional; its prompts are for places only, CONTRACT.md "path" and
  "Installing"): it needs an InstallGen ask, a yes/no on `path.add` with a default that `--silent` takes: **filed as
  INST-Q-047** (Installer REGISTER, rev 28; the architect: "yes, concur"). The question's text names what yes does:
  `bash` and `sh` reachable from cmd and PowerShell, GNU make's recipes run through this `sh`, `bash` may shadow or be
  shadowed. Off PATH, sh.exe is found only from inside C#Bash and what it starts (its own folder is appended to PATH).
  **The default: NO, RATIFIED** (the architect, 2026-10-30; the start file needs no PATH). Do NOT re-open.
- The installer's name. (Where: the same repository's Releases, ruled 2026-10-29.)
- Anything about PortableBash's installer.
