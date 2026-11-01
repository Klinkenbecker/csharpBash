# C#Bash installer plan

**Status: BUILT 2026-11-01 (phases 3 and 4), NOT RELEASED** (the architect: "Yes, go, do"; "We're not shipping until I
say so"). `bash tools/installer.sh` builds `dist.installer/csharpBash-setup.exe` (+ `.sha256`) from the deployed
`dist/Bash.exe` with `installer/csharpBash.json` and `installer/claude.cmd`, through InstallGen 0.0.6 `--release`, and
proves it by a sandboxed install. Names as proposed and ruled with the go: start file `claude.cmd`, installer
`csharpBash-setup.exe`, PATH question (superseded 2026-11-01 by his "Add [<folder>] to your PATH? [y/N]: ", BUILT with InstallGen 0.0.8: the manifest has no `prompt`) "Add this folder to your PATH, so cmd and PowerShell can run bash and sh? GNU make
would then run its recipes through this sh." (default no); product "C#Bash 1.0.0 (hg N)"; no `license` key (MIT asks
nothing of the person installing). Left: release (his word), phase 5 (the README). Below: the history.
A REAL install, by hand from a Command Prompt: `dist.installer/try-install.cmd` (from `installer/try-install.cmd`,
placed by tools/installer.sh): runs the installer beside it, then checks Bash.exe, sh.exe (one file?) and claude.cmd.
**Was: UNBLOCKED (2026-10-31), not ratified.** It waited on InstallGen learning to replace a file in use (Installer
project, `REGISTER.md` `INST-Q-046`): BUILT in Installer rev 33 (INST-037, ratified there): InstallGen 0.0.3 and
specification 2 write each file as `<name>.installgen-new` and rename it in; a running EXE's old copy is set aside
(`%TEMP%/installgen-replaced` on the same drive) and goes when it stops; a file held open without share-delete stops
the install before anything is written, exit 8. A manifest relying on exit 8 or its wording declares `"schema": 2`.
Contract: E:/Claude/Installer/CONTRACT.md ("What version 2 adds", "Installing"). `INST-Q-047` (ask before adding to
PATH) is BUILT too (Installer rev 34, INST-039): in a `"schema": 2` manifest, `"path": { "add": "<place>:", "prompt":
"<question>", "default": "yes" | "no" }` (the words); asked after every refusing check and before anything is written,
the resolved folder printed under the question, the default on an empty answer or `--silent`, not asked when the
folder is already on PATH, "no" removes nothing; report lines "PATH: not added (declined)" / "(--silent: default no)"
/ "... was already there" / "PATH: added ...". Ours: default "no" (RATIFIED 2026-10-30). LANDED 2026-11-01 (Installer
rev 35, INST-040, InstallGen 0.0.5): version 2 is American-spelled, `artifacts`, `license`, `--accept-license` (the
version-1 names, `artefacts`, `licence`, `--accept-licence`, are refused in a version-2 manifest; `--accept-licence`
exits 64 there); InstallGen warns when a manifest carries the other version's spelling. DECISIONS 2026-10-29: "RULED: C#Bash owns its installer". Nothing blocks
it now; awaiting the architect's word on the plan. (Was: BLOCKED 2026-10-29 on INST-Q-046.)

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
3. **The manifest and the installer.** An InstallGen manifest (schema 2, with `path`: see below): place `home` fixed
   `%USERPROFILE%/.bash`, `bin` = `home:bin`; artifacts `Bash.exe` -> `bin:Bash.exe`, `Bash.exe` -> `bin:sh.exe` (the
   same build again, for `sh`, as Git for Windows ships its own; ruled 2026-10-30: two entries for ONE payload file, a
   `to` naming a path lands under that name, so the installer carries one copy; measured 2026-11-01: +44 bytes, not
   +95% as a second copy would be. ON DISK: `sh.exe` is to be a HARD LINK to `Bash.exe` that the INSTALLER re-creates
   and confirms on every install, with a copy as its fallback where a volume cannot hold one (the architect's design,
   2026-11-01; asked of installer-a4 for specification 2; the installer waits for it, nothing ships before his word).
   **BUILT by installer-a4, 2026-11-01 (INST-043, Installer rev 38, InstallGen 0.0.6, spec 2):** ONE artifact entry,
   `{ "from": "Bash.exe", "to": "bin:", "links": [ "bin:sh.exe" ] }` (replaces the two entries above); the link is
   re-made and confirmed on every install, a copy where a volume cannot hold one (exFAT: err 87; another drive: err
   17); the report says `sh.exe: hard link to Bash.exe (confirmed)`, `... already in place`, or `sh.exe: copy of
   Bash.exe (this volume cannot hold hard links, err N)`. Its end-to-end test keeps fsutil as the oracle (not `[ -ef ]`).
   Why managed (measured 2026-11-01): a hard link left alone keeps the OLD build after an upgrade's
   write-new-and-rename, and InstallGen's `links` today are folder links, left alone; a symbolic link follows upgrades
   but needs Developer Mode or elevation (worked here only because this machine has Developer Mode on).
   And the start file (below) -> `bin:`. Built by
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
   **BUILT and DONE 2026-11-01, observed:** the sandboxed fresh install puts Bash.exe, sh.exe (a hard link, confirmed)
   and claude.cmd in place and the report lists each; a re-run writes nothing ("already current", "already in place");
   the UPGRADE case: an installer of 568 installed, 568 running under BOTH names, then 583's installer: exit 0, "the
   running copy was set aside, and goes when it stops" for each, the link re-made and confirmed, both old processes
   finished (rc 0), a new launch 583 under both names; the next run removed the 2 set-aside copies. The PATH question
   as a person sees it: the text, the folder beneath, `[y/N]`, an empty answer "not added (declined)".
4. **The start file.** The README's `claude.cmd` (BASH_PATH = the caller's PATH; CLAUDE_CODE_GIT_BASH_PATH and PATH set
   for that one process; `%USERPROFILE%/.local/bin/claude.exe`), written by the installer: no persistent variable
   anywhere (the architect's Installer ruling, `CONTRACT-PLAN.md` 297; a global CLAUDE_CODE_GIT_BASH_PATH also loses
   to PATH, Claude Code issue #94077). *Done when:* a session started by it reports C#Bash's `$BASH_VERSION`.
   **BUILT 2026-11-01:** `installer/claude.cmd` finds its own folder (`%~dp0`, backslashes turned to `/`), so it works
   wherever the installer put it; it stops with a message if `claude.exe` is not at `%USERPROFILE%/.local/bin`.
   **DONE, observed:** a headless session started through the sandbox-installed start file ran `echo $BASH_VERSION
   $BASH` and answered `5.1.0-koliada F:/.../sandbox/home/bin/Bash.exe`.
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
