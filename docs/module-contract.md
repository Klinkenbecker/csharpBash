# C#Bash 2.0 utility modules: the contract (DRAFT 3, RATIFIED as the basis of version 1)

> **MOVED 2026-10-20 to PortableBash:** the live copy is `F:/Koliada/Tools/PortableBash/docs/module-contract.md`. This copy is frozen as
> of C#Bash hg rev 391 and kept only because DECISIONS entries here cite it.

> **Status: RATIFIED 2026-10-20 as the basis of contract version 1** (DECISIONS "RATIFIED: contract draft 3"), Zig
> modules and the one-file form included. Section 6 records what has been decided since and what is still open.
> RULED and RATIFIED items cite DECISIONS (all 2026-10-20); the rest is Claude's position, with what would change it.
> Drafts 0-2: hg revs 379, 380, 384. Draft 3: modules in Zig, one plain EXE loaded directly (measured on Windows 10
> and 11). Work happens on the hg branch `2.0` (RATIFIED); `default` stays 1.x.

## 0. How options are chosen (RULED)

**The gold standard is two hot paths**, and every option is judged against them first:
- **the exec path:** from bash to the utility running;
- **the file path:** from a request to the data being used.

Other factors are secondary, in this order: **evolution** first, C-source portability distant last.
**Work the shell has already done is never repeated by a module** (the architect).

## 1. The model

A utility name has an **ordered list of providers** (RULED), all under this one contract, defaults declared by the
modules and overridable by the user. Asking the next provider is what "declined" means (3.6).

| Provider | What it is | First use in a process (measured; diagnostic, not the benchmark) |
|---|---|---|
| **internal** (RULED) | the ~70 built-ins, compiled into Bash.exe | 0 |
| **external module** | **a Zig program that is also its own module: one plain EXE**, loaded by the shell, or run standalone | in-process 0.22 ms (load ~0.08-0.18, binding its imports ~0.025, first call ~0.016); as a program 2.3 ms |
| **MSYS bridge** (RULED) | a module (Zig) that runs the machine's MSYS programs and holds every MSYS rule (section 5) | its program's start |
| **plain program** | found on PATH, started as today | an external EXE: 6.8-7.1 ms |

**Why Zig (Claude's position on the architect's proposal):** on the exec path a Zig module costs ~0.22 ms against
~0.75 ms for a C# Native AOT module, which starts its own .NET runtime at its first call in every process
(~0.6 ms, measured with a no-op; it cannot share Bash.exe's, which exports nothing; sharing is PARKED for the
architect's alternate-runtime project). A Zig module is ~50 KB, carries no runtime, and needs no packaging: **the tool
is its EXE**. It fits "moosh all the way down": the code is judged by its tests and measurements, not by reading it.
The built-ins stay C#, compiled in: nothing is rewritten. *Would change it:* the loader behaviour below changing
(then the measured packaged form returns, still Zig).

**How one EXE serves both (measured, Windows 10 19045 and Windows 11 26300):** Windows maps and relocates an EXE
loaded with LoadLibrary but does **not** bind its imports and runs no entry point; the shell binds them (walk the
import table, GetProcAddress, write each slot), then calls the exports. Run standalone, the EXE's `main` calls the
same code. Undocumented behaviour, measured on both; a test in the 2.0 suite fails loudly if it changes.

## 2. The shape of the contract

Borrowed from the Installer's contract (E:/Claude/Installer/CONTRACT.md): **versioned and only ever added to**
(nothing removed or repurposed; every structure carries its own size); **two independent version numbers**, the
contract's and each module's; **compatibility bounded at both ends** (a module states the contract versions it
implements; the shell refuses one that states none and may refuse one newer than it knows).

## 3. What crosses the boundary

### 3.0 What a module is (from the measurements)

- one Zig source, built `-O ReleaseSmall -fstrip -rdynamic`, toolchain pinned (as the Installer pins 0.16.0);
- a **minimal entry**: `pub fn main() void`, raw Win32 externs, no `std.Io` runtime (the Installer's INST-030: the fat
  runtime tripped Defender; the minimal shape did not, and nothing in it needs `main` to have run);
- **loaded by the shell, nothing runs before the first call:** no initialisation in `main` that the exports rely on;
  **no thread-local storage** (its set-up does not run either);
- **imports from kernel32 only** (or what the shell has already loaded): binding shell32 cost 1.4 ms and 2.7 ms of a
  standalone start, measured; the contract's callbacks supply nearly everything else.

### 3.1 Entry points

- `csb_module_info`: contract versions, the module's version and id, its kind (implementation or bridge), and the
  default position it asks for in each name's provider list;
- `csb_provides(name or path)`: answered at run time ("do you provide `awk`?", "will you run this file?");
- `csb_run(const csb_call *)`: one sized structure; one run entry serves every utility a module provides.

### 3.2 Arguments: the expanded words, untouched

An array of byte strings (UTF-8, any other byte kept), exactly as the shell expanded them: never a command line to
re-tokenise. Conversion is the module's business: an implementation module translates through `resolve` (3.4); the
MSYS bridge converts nothing. Temporary assignments arrive in the environment (3.5). Standalone, `main` splits Windows'
command line itself (kernel32 only).

### 3.3 Streams (RATIFIED: callbacks, the OS handle on request)

Callbacks, because in-process stages are managed pipes and all readers of one stdin share one `LfReader` (case
`stdin_sharing`). The read side offers **lines as well as bytes** (the shell has already split them). A module that
starts a process asks for a stream's OS handle; files are shared at their offset, a pipe is pumped. Standalone, `main`
supplies callbacks over its own three handles.

### 3.4 Files (RATIFIED: option c)

**`resolve(operand)`** (a Windows path, "a shell stream", or an error such as Not a directory, against the stage's own
cwd) and **`open(operand, mode)`** through the shell; ordinary operations (stat, enumerate, delete, times, links) by the
module on resolved paths. **Standalone, the module has its own Zig implementation of the path rules** (MSYS forms, `~`,
`/tmp`, device names): a second implementation beside the shell's C# one, kept honest by one conformance suite run
against both. *Would change it:* that suite showing drift that cannot be held.

### 3.5 Environment

Read through callbacks against the stage's exported variables, not copied on every call.

### 3.6 Exit status, and declining

One reserved value means **declined: ask the next provider**; legal only before any output or input (a program never
runs twice; a refusal met while running is loud, 2).

### 3.7 Cancellation

A cancel flag the module polls, and an event it can wait on.

### 3.8 Running a command

A run callback (argv, streams, cwd, environment in; status out) by the shell's own rules. Standalone, `main` starts a
process.

### 3.9 Concurrency

One module may be called on two threads at once (`cat f | grep a | grep b`): reentrant, no process-global mutable
state, no process cwd.

### 3.10 Terminal facts and locale

Per stream: whether it is a terminal, and the width. Locale: always C.

### 3.11 Provider order (RULED: overridable defaults)

Each module declares its default position; the user overrides per name; `BASH_COREUTILS` becomes a special case;
`type -a` shows the list. *Open:* the override's form.

### 3.12 Programs run by their path (RULED: to the bridge)

The core asks the bridges `csb_provides(path)`; the MSYS bridge's answer is today's `IsMsysProgram`.

### 3.13 Resolutions are remembered (the architect's suggestion; Claude's position, to confirm)

In the command map (`~/.bash/commands`): the key gains the provider setup (modules, versions, order); an entry names a
provider (a module EXE, the bridge and a program's path, or a plain program) with the one existence check on use; a
bridge folder is remembered per folder; "no provider for X" is recorded and obeyed by `hash` alone (the ratified miss
rule); a new map format gets a new file name. **No module cache:** a module is loaded from where it is installed.

## 4. Granularity

A Zig module carries no runtime, so a utility per module costs only its own load (~0.1-0.2 ms, the first time in a
process). **Position:** one utility per module by default, several where they share code; the contract allows either.

## 5. The MSYS bridge: what leaves the core

`ChildLauncher.IsMsysProgram` (its `csb_provides(path)`); `AppendArgument`'s MSYS quoting; the MSYS exclusion in
`NativeArgument`; `Access.AppendAtEnd`, `OneMsysAppend`, `WritableForMsys`; awk's `-v BINMODE=3`; possibly the
start-up's Git `usr/bin` and `mingw64/bin` prepend. Its tests (`msys_*`, `native_args_msys`, `coreutils_external`,
`awk_fallthrough`) become the bridge's own suite; the core's needs no MSYS. C#Bash ships no MSYS.

## 6. Decided since, and still open

1. RATIFIED: this draft, Zig and the one-file form included.
2. RATIFIED: the cache design (3.13).
3. RULED: the override's form (3.11) is both, the file `~/.bash/providers` for lasting choices and a variable for a
   session or a command, the variable winning.
4. DECIDED (Claude's call, delegated; the exec hot path for Claude's commands): modules stand **after** the built-ins by
   default (a built-in's decline goes to a module at ~0.22 ms instead of a PATH program at ~7 ms); a module goes ahead
   of a built-in only by explicit user override, for the coreutils except `kill`; the shell builtins proper never.
5. RULED: one signed file of signatures per installation (each module's SHA-256, written and signed by the installer,
   the public key in Bash.exe), checked once per module version and remembered in the command map; not our own
   resource (it would sign itself); Authenticode if the project gets public traction.
6. RULED: everything installs under `~/.bash` (HOME, else the Windows profile), per user, with an installer built with
   the architect's Installer project (InstallGen); modules are also looked for beside Bash.exe. Layout (Claude's, put
   to the architect): `bin/Bash.exe` alone (the only thing for PATH: module EXEs named find.exe, sort.exe, timeout.exe
   would shadow System32's for cmd and PowerShell); `modules/` (module EXEs and the signatures file); `providers` (the
   override file, the user's); `commands/` (the command map, a cache).

## 7. Not measured yet

Memory per loaded module; callback overhead in a tight read loop; `csb_provides` for a bridge scanning folders; a
regex engine in Zig for modules that need one (Zig's standard library has none, recalled).

## 8. The spike kit

`E:/Claude/Temp/csbash-zig-module-test/`: `run.cmd` runs the module (`zigtool_k32.exe`, source `zigtool_k32.zig`) as a
program and three times as a library under `zigmod-check.exe` (source `zigmod-check.c`), and writes
`result-<COMPUTERNAME>.txt`. Both machines: PASS.
