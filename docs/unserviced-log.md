# The request log: `~/.bash/unserviced.jsonl`

One file per machine, `%USERPROFILE%/.bash/unserviced.jsonl`, where every program of the `~/.bash` family records a
request it did not service: an option, a feature or a construct it lacks. It is the list of unmet needs that decides
what is built next. Nothing is sent anywhere; the file stays in the user's profile. Two programs write it today: C#Bash
(`Bash.exe`, `sh.exe`) and Wombat (`git.exe`).

This spec was agreed between the two programs' sessions on 2026-11-07 (the architect: "you and wombat figure it out").
A change to it is agreed the same way and recorded in both projects' DECISIONS.md.

## Where

- `%USERPROFILE%/.bash/unserviced.jsonl`, the folder made when missing. USERPROFILE is read from the writer's
  environment (a test points it at a scratch folder).
- `BASH_UNSERVICED_LOG`, when set and not empty, names another file instead: a Windows path or an MSYS one (`/c/x`
  means `C:/x`). The value `/dev/null` turns logging off; the C#Bash test runners set it, which silences every writer
  run inside them.

## What a line is

One JSON object per line: UTF-8 without a BOM, ending in a single LF. Characters outside ASCII are written as
themselves, not as `\u` escapes; a byte that is not UTF-8 is written as U+FFFD. The fields, in this order:

| Field | Type | Meaning |
|---|---|---|
| `time` | string | UTC, `yyyy-MM-ddTHH:mm:ss.fffZ` |
| `build` | string | the writer's own build stamp (C#Bash: `1.0.0+hg.<hash> <rev>`) |
| `tool` | string | what was asked: a command (`awk`, `jq`), `bash` for the shell itself, `git` for Wombat |
| `feature` | string | the short name of what was lacking (`--rawfile`, `def (function definitions)`, `rebase --onto`) |
| `message` | string | exactly what the user was shown |
| `args` | array of strings | the full arguments of the request (may be absent when there were none to give) |
| `outcome` | string | what happened, e.g. `failed, exit 2`, `ran the PATH program`, `no help topic, exit 1` |
| `source` | string | where the request came from: the calling script (`$BASH_SOURCE`, else `$0`); `""` when the writer cannot know it |
| `line` | number | the line in `source`; `0` when unknown |

A reader takes the fields by name and ignores any it does not know, so a field can be added at the end later without
breaking one.

## How to write it

- Open for appending only (Windows: `FILE_APPEND_DATA`, not `FILE_WRITE_DATA`; POSIX: `O_APPEND`), sharing read, write
  and delete, creating the file when missing.
- Write each line in ONE write call. Appends then land whole at the end, so lines from several programs and sessions
  never interleave (a local disk; a network share gives no such promise).
- Best effort: a log that cannot be written never changes the command's own output or exit status.
- A writer that knows an argument carries a secret writes `***` in its place, in both `args` and `message`. Wombat: the
  value of any config key ending in `extraheader` (any case), given as `key=value` (after `-c` or alone) or as the
  argument that follows the key (`git config <key> <value>`); and the `user:password@` part of any URL. C#Bash: none yet
  (its builtins' refused options rarely carry one); a candidate for the same rules.

## What it records, and what it does not

- Recorded: a request refused (an unsupported option, a feature not available, a construct refused when parsed, a
  builtin C#Bash lacks, a help topic not found), whether or not another program then ran it.
- Not recorded: a wrong answer the program does not know it is giving. Those are bugs; a build's known ones are in
  `~/.bash/KNOWN_ISSUES.md`.
- The full arguments are kept, as `~/.bash_history` keeps commands: a secret passed to a refused command lands here too.
