# The C#Bash module contract: specification version 1

> **MOVED 2026-10-20 to PortableBash:** the live copy is `F:/Koliada/Tools/PortableBash/docs/module-spec.md`. This copy is frozen as
> of C#Bash hg rev 391 and kept only because DECISIONS entries here cite it.

> **Status: DRAFT of version 1** (phase 1 of `docs/v2-plan.md`). It becomes version 1 when the architect ratifies it;
> until then nothing in it is promised. The design and its reasons are in `docs/module-contract.md`; this document is
> the contract itself, and a module author needs nothing else.

## 1. What a module is

A **module** is one Windows x64 executable that does two jobs:

- **run by C#Bash**, it is loaded into the shell's own process as a library, and the shell calls its exports. Nothing
  in it runs before the first call: not `main`, not any initialiser;
- **run by itself** (from cmd, PowerShell, another program), its `main` runs and behaves the same.

A module provides one or more **utilities** (`xxd`, `awk`…), each found by the name it is invoked by. A module is
either an **implementation** (it does the work) or a **bridge** (it runs programs the machine already has, and holds
what those programs need).

Modules are written in **Zig** (the declarations in section 9 are the reference). Any language that produces an
executable meeting section 6 conforms.

## 2. Versions

- This specification has a version, **1**. It changes only by getting a new number, and only by being **added to**:
  nothing is ever removed or given a new meaning.
- **Every structure starts with its own `size`.** A newer shell can hand an older module a longer structure; an older
  shell can hand a newer module a shorter one. Read a field only if `size` covers it.
- A module states the versions it implements (`spec_min`, `spec_max`). The shell **refuses a module that states none**,
  and may refuse one whose `spec_min` is newer than it knows. Version 1 is the floor: nothing below it exists.
- A module has its own version too (`version`), independent of this one.

## 3. Exports

All exports use the C calling convention (Windows x64). Names are exact.

| Export | Called | Returns |
|---|---|---|
| `csb_module_info()` | once, after loading | a pointer to the module's static `ModuleInfo` (section 4) |
| `csb_provides(const ProvidesQuery *)` | when the shell builds a provider list | 1: yes, 0: no |
| `csb_run(const Call *)` | for each invocation of one of its utilities | the exit status (section 5.6) |

## 4. Describing the module

`ModuleInfo` gives the versions implemented, the kind (`0` implementation, `1` bridge), an `id` (lowercase letters,
digits, `-`; unique among installed modules), a `version`, and the utilities it provides, each with a `rank`: its
default position among modules of the same kind, lower first.

`csb_provides` answers a question asked at run time, because a bridge's answer depends on what is installed:
`what = 0` "do you provide the utility `name`?", `what = 1` "will you run the program at the Windows path `name`?".
The query carries the stage's `PATH` value. The shell remembers the answers (section 7.4); answer from the question
alone, the same way each time.

## 5. A call

The shell calls `csb_run` with a `Call`: the utility's name as invoked, its arguments, three streams, the stage's
working directory, the cancel flag, and the **host table** of functions the module uses for everything the shell does.

### 5.1 Arguments

`argv` holds the arguments after the name, **exactly as the shell expanded them**: quoting, variables, globs and word
splitting are done. Each is a byte string, UTF-8 with any other byte kept. Never re-split or re-glob them.

### 5.2 Streams

`stdin`, `stdout`, `stderr` are `Stream`s, used only through the host:
- `read`: up to `cap` bytes; returns the count, 0 at the end, or `-errno`;
- `read_line`: the next line **including its LF** when it has one (the last line may lack it); returns its length, 0
  at the end, or `-errno`. The bytes stay valid until the next read on that stream. `read` and `read_line` may be
  mixed on one stream: the shell keeps one reader, so no byte is read twice or lost;
- `write`: returns the count written or `-errno`. On `-EPIPE` the reader has gone: stop writing and return 141;
- `os_handle`: a Windows handle for the stream, for a module that starts a process and must hand it real handles.
  A file is shared at its current offset; a pipe is pumped by the shell (and what the pump moved but the process never
  read is lost to the next reader). Valid until `csb_run` returns. Ask only if a process needs it.

Each stream says whether it is a terminal (`flags` bit 0) and, if so, its width in columns.

### 5.3 Files

Every operand that names a file goes through the shell, **once**:
- `resolve(operand)`: a `Resolved` that is either **a Windows path** (absolute, UTF-16, against the stage's working
  directory, with MSYS forms, `~` and `/tmp` translated), **a shell stream** (`/dev/stdin`, `/dev/fd/N`, `/dev/tty`,
  `/dev/null`, a process substitution: open it with `open`), or **an error** (`errno`, e.g. ENOTDIR for a path through
  a file);
- `open(operand, mode)`: a `Stream` for reading or writing, with the shell's sharing and `>>` rules; close it with
  `close`.

On a resolved path the module does ordinary operations itself (exists, enumerate, delete, rename, times, attributes,
links) with Windows calls. It never resolves a path by itself and never uses the process's current directory.

### 5.4 Environment

`getenv(name)` and `env_next` (enumeration) read the stage's exported variables, temporary assignments included
(`LC_ALL=C sort`). Values stay valid until `csb_run` returns.

### 5.5 Running a command

`run(argv, in, out, err)` runs a command by the shell's own rules (its providers, its lookup), with the given streams
(null: the call's own). Returns 0 and the command's status, or `-errno` (`-ENOENT`: not found).

### 5.6 The exit status

- **0-255:** the utility's exit status.
- **`DECLINED` (-2):** "not this one: ask the next provider". **Legal only before the module has read, written,
  opened for writing or run anything in this call.** Declining later is a defect: the shell reports it and uses 2.
- Any other negative value is a defect: the shell reports it and uses 2.

### 5.7 Cancellation

`*cancel` becomes nonzero, and `cancel_event` is signalled, when the call should stop (Ctrl-C, `timeout`). Check it in
any loop that can run long; stop promptly and return 130. The shell applies the status it needs (124 for `timeout`).

### 5.8 Errors

Codes are Linux `errno` values, so a module prints GNU's messages: EPERM 1, ENOENT 2, EIO 5, EBADF 9, EACCES 13,
EEXIST 17, ENOTDIR 20, EISDIR 21, EINVAL 22, ENOSPC 28, EPIPE 32, ENAMETOOLONG 36, ENOTEMPTY 39, ELOOP 40.

### 5.9 Lifetimes and threads

Memory the host returns stays valid until `csb_run` returns, unless section 5 says less. Call host functions only from
the thread that called `csb_run`. Threads a module starts must end before `csb_run` returns.

## 6. Rules a module must keep

1. **Imports from `kernel32.dll` and `ntdll.dll` only.** The shell binds a module's imports itself (Windows does not,
   for an executable loaded as a library) and refuses a module importing anything else, loudly.
2. **Nothing may depend on `main` or any start-up code having run**: no initialisation outside the exports.
3. **No thread-local variables**: their set-up does not run when the shell loads the module.
4. **Reentrant:** two calls may run at once on two threads (`cat f | xxd | xxd -r`): no mutable global state, no
   process current directory, no process console.
5. **Locale C:** bytes in, bytes out; no culture-dependent formatting.
6. **Minimal entry:** a plain `pub fn main() void` over Windows calls, without Zig's `std.Io` runtime (its start-up
   shape is what a Defender heuristic flagged in the Installer project, INST-030), built
   `zig build-exe -O ReleaseSmall -fstrip -rdynamic` with the toolchain version pinned.
7. **Run standalone, it behaves the same**: `main` builds its own host table over its three handles and its own path
   rules (the same translations as section 5.3), and calls the same code.

A module runs in the shell's process: a fault in it ends the shell.

## 7. Providers

### 7.1 The default order for a name

1. the shell's own built-in, if it has one;
2. implementation modules providing the name, by `rank`, then `id`;
3. bridges providing the name, by `rank`, then `id`;
4. a program of that name on `PATH`.

Each declines in turn (5.6) until one runs. The shell's own builtins (`cd`, `export`, `read`, `trap`, `set`, `source`,
`eval`, `echo`, `printf`, `test`, `hash`, `type`, `jobs`, `kill`…) are never given to a module.

### 7.2 Overriding it

- **`~/.bash/providers`**, lasting: one line per name, `name = provider provider …`, `#` starting a comment.
- **`BASH_PROVIDERS`**, for a session or one command: `name=provider,provider;name=…`. **It wins over the file.**
- A provider is `internal`, `module:<id>`, `bridge:<id>` or `path`. Only a coreutil may be given a module ahead of
  `internal`.
- `BASH_COREUTILS=builtin` means `internal` only; `external` means everything but `internal`; `auto` the default.

`type -a name` shows the list in force.

### 7.3 Programs run by their path

A command naming a program by its path (`/usr/bin/sed`) is offered to each bridge (`csb_provides`, `what = 1`); the
first to say yes runs it, else the shell starts it as a program.

### 7.4 What the shell remembers

Answers and resolutions are kept in the command map (`~/.bash/commands`), keyed by the provider setup (the installed
modules, their versions, the order): changing any of them starts a fresh map. A remembered module is used after one
existence check. "No provider for this name" is remembered only through `hash`, as a missing command is.

## 8. Installing and trusting modules

- **Layout** (`~` is `HOME`, else the Windows profile): `~/.bash/bin/Bash.exe` (the only file meant for `PATH`);
  `~/.bash/modules/` (modules, and `SIGNATURES`); `~/.bash/providers` (the user's; an installer never overwrites it);
  `~/.bash/commands/` (the command map; a cache). Modules beside `Bash.exe` are also found, with a `SIGNATURES` beside
  them.
- **`SIGNATURES`:** UTF-8, LF line ends; one line per module, `<sha256, lowercase hex>  <file name>`; then a last line
  `signature ecdsa-p256-sha256 <base64 of the 64-byte r||s>`, the signature over every byte before that line. The
  public key is built into `Bash.exe`.
- **The shell loads a module only if `SIGNATURES` lists it with its hash and the signature verifies**, and refuses it
  loudly otherwise. The check is made once per module version (path, size, write time, hash) and remembered in the
  command map.

## 9. The declarations (Zig, the reference)

```zig
// csb.zig: contract version 1. Every struct begins with `size`; read a field only if `size` covers it.
pub const SPEC_VERSION: u32 = 1;
pub const DECLINED: i32 = -2;

pub const Bytes = extern struct { ptr: ?[*]const u8, len: usize };

pub const KIND_IMPLEMENTATION: u32 = 0;
pub const KIND_BRIDGE: u32 = 1;

pub const Utility = extern struct {
    name: [*:0]const u8, // UTF-8
    rank: i32, // among modules of the same kind; lower first
};

pub const ModuleInfo = extern struct {
    size: u32,
    spec_min: u32,
    spec_max: u32,
    kind: u32,
    id: [*:0]const u8,
    version: [*:0]const u8,
    utility_count: u32,
    reserved: u32,
    utilities: [*]const Utility,
};

pub const PROVIDES_NAME: u32 = 0;
pub const PROVIDES_PATH: u32 = 1;

pub const ProvidesQuery = extern struct {
    size: u32,
    spec_version: u32,
    what: u32, // PROVIDES_NAME or PROVIDES_PATH
    reserved: u32,
    name: Bytes, // a utility name, or a Windows path
    path_var: Bytes, // the stage's PATH value
};

pub const STREAM_TERMINAL: u32 = 1;

pub const Stream = extern struct {
    size: u32,
    flags: u32, // STREAM_TERMINAL
    width: u32, // columns, when a terminal
    reserved: u32,
    ctx: ?*anyopaque, // the shell's; never looked into
};

pub const RESOLVED_PATH: u32 = 0;
pub const RESOLVED_STREAM: u32 = 1;
pub const RESOLVED_ERROR: u32 = 2;

pub const Resolved = extern struct {
    size: u32,
    kind: u32, // RESOLVED_*
    errno: i32, // when RESOLVED_ERROR
    reserved: u32,
    path: ?[*:0]const u16, // when RESOLVED_PATH: absolute, valid until csb_run returns
};

pub const OPEN_READ: u32 = 1;
pub const OPEN_WRITE: u32 = 2; // created if missing, truncated
pub const OPEN_APPEND: u32 = 4; // created if missing, every write at the end
pub const OPEN_READ_WRITE: u32 = 8; // created if missing, not truncated

pub const Host = extern struct {
    size: u32,
    spec_version: u32,
    read: *const fn (call: *const Call, s: *Stream, buf: [*]u8, cap: usize) callconv(.c) isize,
    read_line: *const fn (call: *const Call, s: *Stream, line: *Bytes) callconv(.c) isize,
    write: *const fn (call: *const Call, s: *Stream, buf: [*]const u8, len: usize) callconv(.c) isize,
    os_handle: *const fn (call: *const Call, s: *Stream) callconv(.c) ?*anyopaque,
    resolve: *const fn (call: *const Call, operand: Bytes, out: *Resolved) callconv(.c) i32,
    open: *const fn (call: *const Call, operand: Bytes, mode: u32, out: **Stream) callconv(.c) i32,
    close: *const fn (call: *const Call, s: *Stream) callconv(.c) i32,
    getenv: *const fn (call: *const Call, name: Bytes, value: *Bytes) callconv(.c) i32, // 1 found, 0 not
    env_next: *const fn (call: *const Call, cursor: *usize, name: *Bytes, value: *Bytes) callconv(.c) i32, // 1, 0 at end
    run: *const fn (call: *const Call, argc: u32, argv: [*]const Bytes, in: ?*Stream, out: ?*Stream, err: ?*Stream, status: *i32) callconv(.c) i32,
};

pub const Call = extern struct {
    size: u32,
    spec_version: u32,
    host: *const Host,
    utility: Bytes, // the name it was invoked by
    argc: u32,
    reserved: u32,
    argv: [*]const Bytes,
    stdin: *Stream,
    stdout: *Stream,
    stderr: *Stream,
    cwd: Bytes, // the stage's working directory (for messages; resolve does the work)
    cancel: *const volatile u32,
    cancel_event: ?*anyopaque, // a Windows event, signalled with *cancel
};

// The three exports a module defines:
//   export fn csb_module_info() *const ModuleInfo
//   export fn csb_provides(q: *const ProvidesQuery) c_int
//   export fn csb_run(call: *const Call) c_int
```

## 10. What version 1 does not cover

Authenticode signatures (only if the project gets public traction); platforms other than Windows x64; modules standing
in for the shell's own builtins; sharing the shell's .NET runtime (parked).

## 11. Checklist: every ruled and ratified item, and where it is

| Item (DECISIONS 2026-10-20) | Here |
|---|---|
| The two hot paths are the criteria; work done by the shell is not repeated | 5.1, 5.2 (lines from the shell's reader), 5.3 (resolve once), 5.4, 7.4 |
| Providers per name, ordered, overridable defaults; declined means the next | 7.1, 7.2, 5.6 |
| The override: the file `~/.bash/providers` and a variable, the variable winning | 7.2 |
| The internal / external / MSYS split; built-ins first; shell builtins never given away | 7.1, 7.2 |
| MSYS as a bridge; programs run by their path go to the bridges | 1, 4, 7.3 |
| Streams: callbacks, the OS handle on request | 5.2 |
| Files, option c: `resolve` + `open`; ordinary operations by the module | 5.3 |
| Zig modules, one plain EXE loaded directly, imports bound by the shell | 1, 6.1, 6.6, 9 |
| Rules: no entry-point work, no TLS, kernel32 only, minimal entry, reentrant | 6 |
| Resolutions remembered in the command map, keyed by the provider setup | 7.4 |
| Signing: one signed file; Authenticode only on public traction | 8, 10 |
| Everything under `~/.bash`: `bin/Bash.exe` alone, `modules/`, `providers`, `commands/` | 8 |
| Versioned, add-only, sized structures, bounded compatibility | 2 |
| Runtime sharing parked | 10 |
