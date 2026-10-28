# C#Bash 2.0: the plan (PROPOSED, for ratification)

> **MOVED 2026-10-20 to PortableBash:** the live copy is `F:/Koliada/Tools/PortableBash/docs/plan.md`. This copy is frozen as
> of C#Bash hg rev 391 and kept only because DECISIONS entries here cite it.

> **Status:** written 2026-10-20 on the architect's "write the plan, start the plan step 1; go". Phase 1 (documents
> only) is under way on that word; **phases 2-5 wait for the architect to ratify this plan.** Design and rationale:
> `docs/module-contract.md` (ratified as the basis of contract version 1); the record: DECISIONS 2026-10-20.

## The criteria every phase is judged by (RULED)

1. **The exec hot path** (bash to the utility running) and **the file hot path** (a request to the data being used):
   the gold standard.
2. Then **evolution**; C-source portability distant last.
3. **Work the shell has already done is never repeated by a module.**

## Where the work happens (RATIFIED)

Phase 1 is documents, on `default`. From phase 2, code goes on the hg named branch **`2.0`** (`InformationalVersion`
2.0.0); `default` stays the 1.x line, deployed from `dist/` and published, and is merged into `2.0` regularly
(`DECISIONS.md` with hg's `:union` tool). `tools/deploy.sh` and `tools/publish.sh` refuse the `2.0` branch until 2.0
is released.

## Phases: each with the observation that says it is done

### Phase 1: contract version 1, as a specification

`docs/module-spec.md`: standalone (a module author needs nothing else), precise to the byte (exports, structures,
callbacks, codes, rules), versioned as the Installer's contract is.
**Done when:** (a) every RULED and RATIFIED item of DECISIONS 2026-10-20 on modules appears in it (its own checklist,
with a cross-reference each); (b) its Zig declarations compile (`zig build-obj` on the spec's own code); (c) proven in
phase 3: the first module is written from the spec alone, without reading the shell's code.

### Phase 2: the `2.0` branch, and the loader in the shell

The branch and its version; the deploy and publish guards; the loader (LoadLibrary, import binding, exports, the
contract-version check); provider lists and their resolution in the command map (a new map format, a new file name);
`~/.bash/providers` and its variable; `type -a` showing the list. Until phase 5, modules load only from a directory a
development variable names (no signature checks exist yet).
**Done when:** (a) the 1.x suite passes unchanged on `2.0`, console and `-Detached`; (b) a suite case loads the spike
module through the shell's real path and calls it; (c) a suite case fails loudly if Windows starts binding an EXE's
imports itself or stops relocating it (the spike checker's logic); (d) no exec-hot-path regression for built-ins:
`compare3.sh --build` startup and `claudecall.sh --build` within noise of the deployed 1.x; (e) `deploy.sh` and
`publish.sh` refuse the `2.0` branch (run there, they stop).

### Phase 3: the first Zig module, end to end

**`xxd`** (Claude's choice: C#Bash has no `xxd`, the battery's probe 210 fails for want of one, and it exercises byte
streams, file operands, options and standalone use, without being large). Built from `docs/module-spec.md` alone.
**Done when:** (a) its output is byte-identical to GNU `xxd` (WSL) on a case set; (b) standalone (`xxd.exe`) and
in-process give the same bytes; (c) first use in-process measured at ~0.3 ms or less; (d) battery probe 210 passes; (e)
it declines what it does not support, before any output (a case shows the next provider runs once).

### Phase 4: the MSYS bridge

A Zig bridge module; the core's MSYS code removed (`IsMsysProgram` and its callers, MSYS quoting, the `NativeArgument`
exclusion, `AppendAtEnd` / `OneMsysAppend` / `WritableForMsys`, awk's `BINMODE=3`); the MSYS cases moved to the bridge's
own suite (SKIP where no MSYS installation is present).
**Done when:** (a) the core suite passes with that code gone; (b) every MSYS case that passes on 1.x passes through the
bridge on 2.0 on a machine with MSYS; (c) a program run by its path (`/usr/bin/sed`) reaches the bridge.

### Phase 5: the installer, and signing

An InstallGen manifest for `~/.bash` (`bin/Bash.exe`, `modules/`, the signatures file; `providers` never overwritten);
the signing tool (a key pair; the installer's build signs; the public key built into Bash.exe); the shell's check, once
per module version, remembered in the command map.
**Done when:** (a) an install into a fresh profile runs bash and loads a module; (b) a modified module is refused
loudly (the tamper control), and an unsigned one; (c) reinstalling keeps `providers`; (d) Defender reports nothing on
install.

## Not to be decided during the work (do NOT derive)

- Which further utilities become modules (after `xxd`, the architect's call).
- Authenticode (only on public traction: RULED).
- Sharing a runtime with Bash.exe (PARKED for the architect's alternate-runtime project).
- Any change to a built-in's behaviour (that is 1.x work, on `default`).
- How 2.0 is released and when `2.0` merges back to `default`.

## What the architect supplies to make this binding

Ratification as an act ("ratified", or "still discussing"); a steer labelled preference or requirement; an answer, or
an explicit deferral, for anything that blocks a phase.
