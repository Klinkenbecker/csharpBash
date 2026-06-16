# Manual Test Checklist — interactive (TTY-only) features

These can't be scripted (they need a real console: raw key input, signals,
cursor rendering). Run them by hand in a real terminal. The automated suite
(`run-tests.ps1`) covers everything that works in script mode.

Build & launch:

```
dotnet build -c Release Bash
bin\Release\net8.0\Bash.exe          # from the Bash\ project dir
```

Tick each box; note anything that misbehaves. Reference: real bash.

## Line editor — movement & editing
- [x] Type text; `Left`/`Right` move the caret; characters insert at the caret (not the end).
- [x] `Home` / `Ctrl+A` → start of line; `End` / `Ctrl+E` → end of line.
- [x] `Ctrl+Left` / `Ctrl+Right` jump by word.
- [x] `Backspace` deletes left of caret; `Delete` deletes under caret.
- [x] `Ctrl+U` kills to start of line; `Ctrl+K` kills to end; `Ctrl+W` deletes the word before the caret.
- [x] `Ctrl+L` clears the screen and redraws the current line.
- [x] Type a line longer than the terminal width — wrapping and caret position stay correct; backspacing across the wrap boundary works.
- [x] Typing is smooth with no cursor flicker (cursor is hidden during redraw; end-of-line typing emits the char directly without rewriting the line).

## History
- [x] `Up`/`Down` walk previous commands; editing a recalled line then `Up`/`Down` behaves sanely.
- [x] Down past the newest restores the line you were typing.
- [x] Run a few commands, exit, relaunch → history persisted (`~/.bash_history`).
- [x] `history` lists with line numbers; `history 3` shows last 3; `history -c` clears.
- [x] Consecutive duplicate commands are not stored twice.
- [x] `Ctrl+R`, type a substring → most recent match shown; `Ctrl+R` again → older match; `Enter` runs it; `Esc`/`Ctrl+G` cancels back to the original line; an arrow key accepts the match into the editor.

## Tab completion
- [x] First word: `ec`+Tab → `echo `; a PATH exe prefix completes; a defined function name completes.
- [x] Filename: `cat REA`+Tab completes a file in the cwd; a directory match ends with `/` and no trailing space.
- [x] Variable: set `FOO=1`, type `echo $F`+Tab → `$FOO` (no trailing space); `${F`+Tab likewise.
- [x] Ambiguous prefix with several matches: Tab extends to the longest common prefix; Tab again lists candidates, then redraws the prompt+line.

## Multi-line continuation (PS2)
- [x] `for i in 1 2 3` ↵ → `> ` prompt; `do` ↵ `echo $i` ↵ `done` ↵ runs the loop.
- [x] Unterminated quote: `echo "hello` ↵ → `> `; `world"` ↵ prints `hello`⏎`world`.
- [x] Backslash continuation: `echo ab\` ↵ `cd` → prints `abcd`.
- [x] `Ctrl+D` during a `> ` continuation aborts the input cleanly.

## Prompt
- [x] Default prompt is `bash-5.1$ ` (with `--norc`).
- [x] In `~/.bashrc` set `PS1='\u@\h:\w\$ '` → shows user@host:cwd with `~` for $HOME; `\W` shows just the cwd basename.
- [x] `PROMPT_COMMAND='echo tick'` runs before each prompt.
- [x] `PS2='»  '` changes the continuation prompt.

## Signals (Ctrl+C)
- [x] At an empty prompt, `Ctrl+C` clears the current line / gives a fresh prompt; the shell does NOT exit.
- [x] During `while true; do :; done`, `Ctrl+C` interrupts the loop and returns to the prompt (shell survives); `echo $?` → 130.
- [x] During `sleep 30`, `Ctrl+C` interrupts and returns to the prompt.
- [x] `trap 'echo caught' INT` then `Ctrl+C` during a loop → prints `caught`, loop continues / returns; shell survives.
- [x] `trap '' INT` → `Ctrl+C` is ignored.
- [x] `Ctrl+D` on an empty line exits the shell.

## Traps on exit
- [x] `trap 'echo bye' EXIT` then `exit` (or `Ctrl+D`) → prints `bye` once on the way out.

## Startup files
- [x] Put `echo rc-loaded` in `~/.bashrc`; launch interactively → `rc-loaded` prints before the first prompt.
- [x] `--norc` skips it; `--rcfile other` sources `other` instead.
- [x] `bash -l` (login) sources `~/.bash_profile` / `~/.profile` instead of `~/.bashrc`.

## Job control
- [x] `sleep 5 &` returns immediately and prints `[1] <id>`.

   Crashes
- [x] `jobs` lists the running job; after it finishes, `jobs` shows `Done` once then reaps it.
- [x] `wait` blocks until background jobs finish.
- [x] `fg` waits for the job (no true terminal hand-off); `bg` reports it's unsupported.
