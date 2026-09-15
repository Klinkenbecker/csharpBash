# Bash — Implementation

> **Maintenance contract.** This document **tracks the surface texture** of the code that
> sits on top of the architecture. Update it whenever that surface moves: a class or key
> method added/renamed/removed, a changed responsibility or signature, a new builtin. It does
> **not** record *why* (that's [`DECISIONS.md`](DECISIONS.md)) or the conceptual model (that's
> [`ARCHITECTURE.md`](ARCHITECTURE.md) — locked except on structural change). To stay
> low-churn this file names **classes and methods, never line numbers.** Section numbers below
> mirror the subsystems in [`ARCHITECTURE.md`](ARCHITECTURE.md).

---

## 1. Source-tree map

> Paths below are relative to the `Bash/` project directory (the interpreter source).

| Path | Role |
|------|------|
| `Program.cs` | Entry point; flag parsing; the three run modes (`-c`, script, REPL); startup-file sourcing; continuation-line accumulation. |
| `Lexer/Lexer.cs` | Tokeniser. |
| `Lexer/Token.cs`, `Lexer/TokenType.cs` | `Token` record (+ `HasLeadingSpace`) and token kinds. |
| `Lexer/LexException.cs` | Lex error; `Incomplete` flag drives REPL continuation. |
| `Parser/Parser.cs` | Recursive-descent parser. |
| `Parser/Nodes.cs` | AST record types (`Node`, `WordPart`, `CondExpr` hierarchies). |
| `Parser/ParseException.cs` | Parse error; `Incomplete` flag. |
| `Evaluator/Evaluator.cs` | Tree-walking executor: dispatch, pipelines, redirects, external launch, jobs, traps, interrupts. |
| `Evaluator/WordExpander.cs` | The expansion passes. |
| `Evaluator/ArithParser.cs` | Arithmetic precedence evaluator. |
| `Evaluator/Builtins.cs` | Dispatch (`Names`/`CoreutilNames`/`Has`/`TryExecute` with the decision-2 policy: `UnsupportedOptionException` → PATH external if present, else loud exit 2; `BASH_COREUTILS=auto\|builtin\|external`) + the small tools that stayed here (`factor` `cal` `expr` `which` `hash` `kill` `trap` `jobs`…). |
| `Evaluator/Builtins.Shell.cs` | The shell builtins proper (partial class): echo/printf, cd/pwd/pushd/popd/dirs, export/unset/readonly/declare/local, set/shopt, alias, source/eval, type/command/builtin, test/[, read/mapfile/getopts, let, kill, env, sleep, exit/return/break/continue. |
| `Evaluator/Opts.cs` | `Opts.Parse`: strict GNU-style option parser for every coreutil (short clusters, attached/separate/optional values `c:`/`c::`, long `name=`/`name=?`/aliases `name:c`, `--`, `stopAtFirstOperand`, numeric `-N`); anything unknown throws `UnsupportedOptionException`. |
| `Evaluator/Builtins.Text.cs` | Text tools on `Opts`: cat head tail wc rev tac tr cut uniq nl fold paste comm seq split od sort tee base64 md5sum/sha*sum hexdump; helpers `OpenText`/`ReadLines`/`ReadBytes`/`FilesOrStdin`/`IoError`. |
| `Evaluator/Builtins.Files.cs` | File/dir tools: basename dirname mkdir rmdir touch rm mv cp ls (long format) du cmp stat mktemp realpath readlink chmod ln truncate; `TryParseDate` delegates to `GnuDate`. |
| `Evaluator/Builtins.Find.cs` | `find` (expression tree, `-exec ;`/`+`, `-printf`, `-delete`…), `diff` (normal/unified/recursive, GNU hunk order), `xargs` (`-0 -n -I -L -P -d -a -r -t`, exit codes 123/124/126/127). |
| `Evaluator/Builtins.Search.cs` | `BreToNet`/`EreToNet`/`AppendBracket` (POSIX classes, `\<`/`\>`), `MakeRegex`; `grep` (all major flags) and `sed` (compiler + runtime: addresses, ranges, `{ }`, hold space, `s` flags, `-i[SUF]`, `-s`, `-z`). |
| `Evaluator/Builtins.Sys.cs` | `date` (full `strftime`, `-d/-r/-u/-I/-R/-f`), `uname` (MSYS-style), `hostname`, `arch`, `nproc`, `tty`, `whoami`, `id`, `printenv`, `timeout` (worker thread + `RunAsJob` so an external child can be killed on expiry). |
| `Evaluator/Builtins.Awk.cs` | The in-process `awk` subset (DECISIONS 2026-09-04 #7): lexer (regex-vs-division by previous token), recursive-descent parser to a small AST, interpreter with awk value typing (num/str/strnum), fields/NF rebuild, `printf`, `sub`/`gsub`/`split`/`match`…; unsupported constructs → `UnsupportedOptionException("awk","unsupported",…)`. |
| `Evaluator/GnuDate.cs` | `GnuDate.TryParse`: GNU `-d` grammar subset (ISO/RFC/`Mon D YYYY`/`@epoch`/times/zones/relative items/weekdays), shared by `date -d` and `touch -d`. |
| `Evaluator/Builtins.Proc.cs` | Minimal procps: `pgrep`/`pkill` (`-f -x -l -a -c -n -o -v -P -d`, signals accepted and forceful; self excluded, `pkill` also skips loudly this shell's ancestors and any process whose command line carries the pkill invocation itself) and `ps` (`-e/-ef/aux`, `-p`, `-o pid,ppid,comm,args`). Process list via Toolhelp32, command lines via `NtQueryInformationProcess(ProcessCommandLineInformation)`. |
| `Evaluator/Printf.cs` | `PrintfFormatter`: bash `printf` semantics (flags/width/precision, `%q` `%b`, escapes, argument recycling); also used by `echo -e`. |
| `Evaluator/FileTests.cs` | The file/string primaries shared by `test`/`[` and `[[ ]]` (paths translated). |
| `Evaluator/Glob.cs` | Shell pattern → regex (`*` `?` `[…]` classes), `Match`, and the pathname-expansion walker (`dotglob`/`nullglob`/`nocaseglob`/`globstar`). |
| `Evaluator/ConsoleMux.cs` | `ConsoleMux` (per-thread console slots + `Capture`/`Apply`), `PipeBuffer` (managed pipe with back-pressure, EOF and `BrokenPipeException`), `ChildJobs` (kill-on-close job object), `HiddenConsole` (a console-less shell -- Claude Code's Bash tool -- starts children with `CreateNoWindow` and attaches to the first one's hidden console, so no child ever opens a visible window). |
| `Evaluator/ShellEnvironment.cs` | Variables, arrays, exports, readonly/integer attributes, special parameters, scope/positional stack, snapshot/restore (subshells), `TranslatePath` (inbound: MSYS `/c/…`, `/tmp`, `~` → forward-slash Windows form) and `ToShellPath`/`ShellCwd`/`FullPath` (outbound: the same form, so the two directions agree), PATH normalisation and the Git-tools augmentation. |
| `Evaluator/ShellEncoding.cs` | The shell's single encoding: UTF-8 with surrogateescape (an undecodable byte ↔ the lone surrogate U+DC00+byte), so bytes that are not text survive variables, `$( )`, pipes and files. `ByteChar` is what the `\xNN`/`\NNN` escape handlers emit; `ReadAllText`/`ReadAllLines` replace the `File.*` equivalents, which silently ignore their encoding argument on a file that starts with a BOM. Encode and decode are both overridden because an `EncoderFallback` cannot emit raw bytes. |
| `Evaluator/ShellOptions.cs` | `set` flags (`-e/-u/-x/-n/-f/-v/-C/-a/-o …`), the `shopt` set, invocation facts, `$-`. |
| `Evaluator/BackgroundJob.cs` | Background-job record. |
| `Evaluator/EvalException.cs` | Runtime exception hierarchy (Exit/Return/Break/Continue/Interrupt/Eval). |
| `IO/LineEditor.cs` | Interactive key-by-key line editor. |
| `IO/History.cs` | Command history (`~/.bash_history`). |
| `IO/CompletionEngine.cs` | Tab completion. |
| `IO/PromptExpander.cs` | `PS1`/`PS2` prompt escapes. |
| `AstPrinter.cs` | Debug AST dumper. |

### Module dependencies

```
Program.cs
   │  drives
   ├─ Lexer ──► Parser ──► (Nodes)            front-end
   └─ Evaluator                                back-end
        ├─ WordExpander ─► ArithParser
        ├─ Builtins  ──► (Evaluator.RunCommand, re-entrant)
        ├─ ShellEnvironment
        └─ ShellOptions
   IO/ (interactive only): LineEditor ─► History, CompletionEngine ; PromptExpander
```

`Evaluator`, `WordExpander`, `Builtins`, and `CompletionEngine` hold back-references to each
other (an `Evaluator` owns a `WordExpander`, a `Builtins`, and a `ShellEnvironment`; the
expander and builtins call back into the evaluator). Construction is wired in the `Evaluator`
constructor.

---

## 2. Lexer — `Lexer/Lexer.cs`

| Method | Responsibility | Architecture concept |
|--------|----------------|----------------------|
| `Tokenize()` | Drive the scan, produce `List<Token>`. | §4 token stream |
| `NextToken(int)` | Emit the next token; sets `HasLeadingSpace`. | §4 word-boundary signal |
| `ConsumeDollar()` | Read `$var` / `${…}` (entire body, balancing braces/quotes) / `$(…)` / `$((…))`. | §4 `${…}` consumed whole |
| `ConsumeOperator(int)` | `\|`, `&&`, `\|\|`, `;`, redirections, `&`; bounds-safe `Peek`. | §5 operators |
| `FlushHeredocs` / `ConsumeHeredocBody` | Capture heredoc bodies after the newline. | §4 heredocs |
| `ConsumeSingleQuoted` / `ConsumeDoubleQuoted` / `ConsumeAnsiEscape` / `ConsumeHexEscape` | Quoting + `$'…'` decoding. | §4 quoting |
| `ConsumeWord` | Plain word runs; context-sensitive `{`/`}`. | §4 `{`/`}` |
| `SkipWhitespace` / `SkipComment` / `Peek` / `Advance` | Scanner primitives. | — |

Raises `LexException(Incomplete)` at EOF mid-construct.

---

## 3. Parser — `Parser/Parser.cs`, `Parser/Nodes.cs`

| Method | Builds |
|--------|--------|
| `Parse()` → `ParseList()` | `Script` / `List` (continues through `;`, stops at `IsListTerminator`). |
| `ParsePipeline()` | `Pipeline` (and negation `!`). |
| `ParseCommand()` → `ParseCompoundCommandBody()` | dispatch to simple vs compound. |
| `ParseSimpleCommand()` | `SimpleCommand`; detects assignments, `arr=(…)` (`IsArrayCompoundAssign`), `arr[i]=v` (`IsArrayElementAssign`). |
| `ParseIf/ParseWhile/ParseFor/ParseCase/ParseBraceGroup/ParseSubshell` | the compound nodes. |
| `ParseConditionalExpression` → `ParseCondExpr/And/Not/Primary` | `[[ … ]]` → `CondExpr` tree. |
| `ParseRedirects` / `TryParseRedirect` | `Redirect` list (optional fd, `RedirectKind`). |
| `ParseWord` → `ParseWordParts` | `Word` = `List<WordPart>`; fuses tokens while no leading space. |
| `ParseDoubleQuotedInterior` / `ParseBraceExpansion` / `ParseCommandSubstitution` / `ParseArithmeticExpansion` / `ParseBacktickSubstitution` | the `WordPart` subtypes. |

`Nodes.cs` defines the three record hierarchies diagrammed in
[ARCHITECTURE §5](ARCHITECTURE.md#5-grammar--ast). Note `BraceExpansionPart` is the node for a
**`${…}` parameter expansion** (its `Raw` is the interior); literal `{a,b}` brace expansion is
performed later in `WordExpander`, not as this node.

P1 additions: every `Node` carries `Line` (for `$LINENO`); `FunctionDef.Source` keeps the
definition's verbatim text (for `declare -f`), sliced from the source the parser was given
(`Parser(tokens, source)`); `ArithmeticCommand` (`(( ))`) and `ArithForCommand` (`for ((;;))`)
come from the lexer's `ArithCommand` token; `ArrayCompoundAssign.Append` is `arr+=(…)`; an
assignment name ending in `+` is `var+=value`. A heredoc's `Redirect.Target` is the **body**
(bound by `TryParseRedirect`, which pulls the `HeredocBody` token out of the stream);
`RedirectKind` gained `HereString`, `OutputBoth`, `AppendBoth`. `ParseRegexOperand` gathers
the raw right side of `=~`; `[[ ]]` accepts `&&`/`||`/`( )` as tokens. `ParseWords` (static)
lets `declare -a x=(…)` expand an array literal passed as an argument (folded into one
literal word by `CollectParenText`). `Parse()` throws on a token it cannot consume instead of
looping.

---

## 4. Evaluator — `Evaluator/Evaluator.cs`

**Dispatch & lifecycle**

| Method | Role |
|--------|------|
| `Execute(Node)` | The type-switch over `Node` subtypes ([ARCHITECTURE §6](ARCHITECTURE.md#6-evaluation-model)). |
| `RunString` / `SourceFile` / `ExecScript` | Parse-and-run a string / file / `Script`. |
| `RunStatement(Node)` | Per-statement `EvalException` isolation → `bash: …`, `$?`, continue. |

**Pipelines & I/O** ([ARCHITECTURE §9](ARCHITECTURE.md#9-execution--io-model))

| Method | Role |
|--------|------|
| `ExecPipeline` → `ExecPipelineThreaded` | Thread per stage over `PipeBuffer`s; stage stdio via `ConsoleMux` slots; last stage inherits the caller's stdout; reader-close → `BrokenPipeException` (141) upstream; `PIPESTATUS`/`pipefail`/`!`. |
| `ExecSimpleCommand` | Expand args (aliases, `+=`, `$( )` status); `exec` special case; gate on `Builtins.Has(name)\|\|_functions` → `ApplyRedirects`+`ApplyTempAssignments`+run, else `ExecExternal`. |
| `ApplyRedirects` → `RedirectScope` | Sets the thread's `ConsoleMux` slots (and fd 3+ via `TrackFd`) for in-process commands; RAII restore + dispose. |
| `CurrentRawStdout` (+ `ConsoleMux.Raw`, `ConsoleMux.Capturing`) | Byte-faithful sink for builtins. |

**External commands & path** ([ARCHITECTURE §12](ARCHITECTURE.md#12-external-commands--the-path-model))

| Method | Role |
|--------|------|
| `ExecExternal` | Self-contained fd-map + `Process.Start`; `HiddenConsole.NeedsHiding` before the start, `ChildJobs.Attach` then `HiddenConsole.Adopt` after it. |
| `ResolveOnPath` / `HashClear` / `HashSetPath` / `HashTable` | PATH(+PATHEXT) cache (the `hash` table). |
| `TryResolveScriptFile` / `ResolveInterpreter` / `RunScriptInProcess` | Shebang dispatch. |
| `RunCommand(name,args)` | Re-entrant builtin→function→external dispatch (used by `xargs`). |

**Compound commands**: `ExecList`, `ExecIf`, `ExecWhile`, `ExecFor`, `ExecCase`,
`ExecSubshell`, `ExecWithRedirects`, `ExecFunctionDef`, `ExecFunction`, `ExecConditional`
(+ `EvalCondExpr`/`EvalCondUnary`/`EvalCondBinary`, and `GlobMatch` for `[[ ==` / `case`).

**Jobs, traps, signals** ([ARCHITECTURE §14](ARCHITECTURE.md#14-signals--jobs)):
`StartBackgroundJob`, `ListJobs`, `WaitJobs`, `FindJob`, `KillJob`, `SetTrap`/`RemoveTrap`/`RunTrap`/`RunExitTrap`,
`RequestInterrupt`/`ClearInterrupt`/`CheckInterrupt`. A job's `$!` is a synthetic pid
(`BackgroundJob.PidBase + Id`); `ChildPid` tracks the external it is running so `kill $!` works.

**P1 surface** (see DECISIONS 2026-09-04): `Aliases`, `GetFunction`/`RemoveFunction`/`HasFunction`,
`Keywords`, `Classify` (alias/keyword/function/builtin/file — backs `type`/`command -v`),
`RunCommand(name,args,skipFunctions,extraEnv,clearEnv)`, `RunBuiltin`, `GetFd` (the fd 3+ table
`_fds`, filled by `exec 3>f` / `cmd 3<f` through `RedirectScope.TrackFd`), `ExecArithmeticCommand`,
`ExecArithFor`, `ExecCondition` (errexit-suppressed), `ApplyTempAssignments` (`VAR=x builtin`),
`ExecExternalOrBuiltin` (`exec cmd`). `RunString(source, reportErrors, origin, syntaxErrorCode)`
reports `bash: origin: line N: …`. `ExecSubshell` uses `ShellEnvironment.TakeSnapshot`/`RestoreSnapshot`.
`ExecExternal` routes a child's stdio through swapped `Console` streams when an in-process redirect
is in effect (`$( )` capture, `{ … } >file`, heredoc on a loop) — the `_origOut/_origErr/_origIn`
comparison — and reports `command not found` (127) / `Permission denied` (126) to wherever the
command's stderr was sent.

---

## 5. WordExpander — `Evaluator/WordExpander.cs`

| Method | Role |
|--------|------|
| `ExpandToFields(Word)` | The full ordered pipeline ([ARCHITECTURE §7](ARCHITECTURE.md#7-word-expansion-pipeline)); per-field quoted-glob tracking; fast paths. |
| `ExpandToString(Word)` | Concatenate parts, no split/glob. |
| `ExpandToPath(Word)` | `ExpandToString` then `TranslatePath`. |
| `ExpandPart(WordPart)` | Per-`WordPart` dispatch. |
| `ExpandBraceParam` | `${…}` parameter expansion incl. `${arr[@]}`, `${#x}`, `${!x}`, `${x:-y}`, substrings. |
| `ExpandVarsInArith` | Substitute variables (incl. **bare** identifiers) before `ArithParser`. |

---

## 6. ArithParser — `Evaluator/ArithParser.cs`

`Evaluate(string)` → `ArithParserState` precedence ladder:
`ParseTernary → ParseOr → ParseAnd → ParseBitOr → ParseBitXor → ParseBitAnd → ParseEquality
→ ParseRelational → ParseShift → ParseAddSub → ParseMulDiv → ParsePower → ParseUnary →
ParsePrimary` (`ParseNumber` handles hex/octal). Operates on `long`.

---

## 7. Builtins & coreutils — `Evaluator/Builtins*.cs`

**Dispatch:** `TryExecute(name,args,out code)` is the `switch`; `Names` is the membership list
(**keep in sync with the switch**); `Has(name)` is the O(1) membership test used by the
evaluator.

| Family | Methods |
|--------|---------|
| Shell builtins | `Echo` `Printf` `Cd` `Pwd` `Export` `Unset` `Set` `Shift` `Read` `Source` `Local` `Declare` `DoEval` `Type` `Command` `Test`/`TestBracket`/`TestImpl` `Sleep` `Env` `HistoryCmd` `Trap` `Jobs` `Fg` `Bg` `DoExit`/`DoReturn`/`DoBreak`/`DoContinue` |
| Text (`Builtins.Text.cs`) | `Cat` `Head` (streaming) `Tail` (`-f` polls) `Wc` (GNU widths) `Rev` `Tac` `Tr` `Cut` `Uniq` `Nl` `Fold` `Paste` `Comm` `Seq` `Split` `Od` `Sort` (`SortKey`, `-k -n -V -h -s -u -o`) `Tee` `Base64` `Checksum` `Hexdump` |
| File / dir (`Builtins.Files.cs`) | `Basename` `Dirname` `Mkdir` `Rmdir` `Touch` `Rm` `Mv` `Cp` `Ls` (`ModeString`/`LongLine`) `Du` `Cmp` `Stat` `Mktemp` `Realpath` `Readlink` `Chmod` `Ln` `Truncate` |
| Find / diff / xargs (`Builtins.Find.cs`) | `Find` (`FindParseOr/And/Not/Primary` → `FindNode` tree, `FindPrintf`, `FindExecPlusFlush`) `Diff` (`DiffPaths/DiffDirs/DiffFiles`) `Xargs` |
| Search (`Builtins.Search.cs`) | `Grep` `Sed` (`SedCompile`/`SedRun`/`SedSelect`/`SedExpand`) `BreToNet`/`EreToNet`/`AppendBracket`/`MakeRegex` |
| System (`Builtins.Sys.cs`) | `Date`/`Strftime` `Uname` `Arch` `Hostname` `Nproc` `Tty` `Whoami` `Id` `Printenv` `Timeout` |
| awk (`Builtins.Awk.cs`) | `Awk` → `AwkLex`/`AwkParser`/`AwkInterp`, `AwkVal`, `AwkFormat`/`FormatG`; `print`/`printf` may target `"/dev/stderr"`/`"/dev/stdout"` (ratified 2026-09-04), nothing else |
| Processes (`Builtins.Proc.cs`) | `Pgrep`/`Pkill` → `PgrepImpl`, `Ps`, `ListProcesses`/`ReadCommandLine`/`AncestorsOf` |
| Still in `Builtins.cs` | `Factor` `Cal` `Expr` ladder `Which` `Hash` `Exec` `Kill` `Trap`/`CanonSignal` `Jobs`/`Fg`/`Bg` `HistoryCmd` |
| Shared helpers | `OpenText`/`ReadLines`/`ReadBytes`/`FilesOrStdin`/`IoError`/`ParseCount` (Text) · `P` path helper (Files) · `ExpandTrSet`/`TrClass` `InterpretEscapes`/`UnescapeDelims` `CopyDir` `FindInPath` |

**Option policy (DECISIONS 2026-09-04 #2):** every tool parses with `Opts.Parse` and throws
`UnsupportedOptionException` for anything it does not implement; `TryExecute` catches it and,
unless `BASH_COREUTILS=builtin`, re-runs the command as a PATH external when one resolves,
otherwise prints the message plus a one-line explanation and returns 2. `BASH_COREUTILS=external`
skips the in-process tool entirely when a PATH external exists.

Regex dialect (`BreToNet`) and re-entrancy (`Xargs` → `RunCommand`) are described in
[ARCHITECTURE §10](ARCHITECTURE.md#10-in-process-coreutils).

---

## 8. Environment & options

`ShellEnvironment`: `Get`/`Set`/`SetLocal`/`Unset`/`Export`/`VariableNames`;
arrays `DeclareArray`/`SetArrayElement`/`GetArrayValues`/`GetArrayKeys`/`GetArrayLength`/
`UnsetArrayElement`/`SetArrayFromList`; assoc `DeclareAssoc`/`SetAssocElement`/… ; scope stack
`PushScope`/`PopScope`/`GetPositionals`/`SetPositionals`/`SetArg0`; `GetExportedVars`; static
`TranslatePath`. `LastExitCode` is `$?`.

`ShellOptions`: booleans for `set -e/-u/-x/-n/-f` and `-o pipefail`, read across the evaluator.

---

## 9. Interactive layer — `IO/`

| Class | Surface |
|-------|---------|
| `LineEditor` | `ReadLine(prompt)`; `DoComplete()`; key handling + self-correcting redraw. |
| `History` | `Add`, `Clear`, `Count`, indexer, `Items`, `MaxEntries`. |
| `CompletionEngine` | `Complete(buffer,cursor)` → `CompletionResult(Start,Length,Matches,…)`. |
| `PromptExpander` | `Expand(ps)` for `PS1`/`PS2` escapes (history/command numbers via callbacks). |

`Program.cs` wires these together in REPL mode and owns flag parsing + startup sourcing
(`SourceLogin`, `SourceInteractiveRc`, `EndsWithLineContinuation`).

---

## 10. End-to-end walk-through

Tracing `echo $x | grep a > out` (assume `x=cat`):

1. **REPL** (`Program.cs`) reads the line; `new Lexer(src).Tokenize()` →
   `new Parser(tokens).Parse()` yields `Script[ List[ Pipeline[ SimpleCommand(echo,[$x]),
   SimpleCommand(grep,[a], redirects:[Output→out]) ] ] ]`.
2. `Execute(Script)` → `ExecScript` → `RunStatement(List)` → `ExecList` → `ExecPipeline`.
3. `ExecPipelineThreaded` creates one `PipeBuffer` and two stage threads, each starting
   with a copy of the caller's `ConsoleMux` state.
4. **Stage 0** (thread A): its `ConsoleMux` Out slot is a writer over the pipe's write end;
   `ExecSimpleCommand`: `WordExpander.ExpandToFields($x)` → `["cat"]`; `echo` writes `cat\n`
   to `Console.Out` → the multiplexer → the pipe. The thread's `finally` closes the write end
   (EOF downstream).
5. **Stage 1 (last)** (thread B): its In slot is a reader over the pipe's read end; the Out
   slot is the caller's (the terminal). `ExecSimpleCommand` sees `grep` is a builtin →
   `ApplyRedirects([Output→out])` sets the thread's Out slot to the file `out` under a
   `RedirectScope`; `Builtins.TryExecute("grep", ["a"])` reads `Console.In` (the pipe), keeps
   lines matching `a`, writes to `Console.Out` (the file). `RedirectScope.Dispose` restores the
   slot and closes the file; the thread's `finally` closes the pipe's read end.
6. Both threads are joined; `PIPESTATUS` is set; the pipeline's exit code (last stage, or
   worst under `pipefail`) becomes `$?`.

Touches: lexer/parser front-end, parameter expansion (§7), threaded pipeline (§9.1), the
console multiplexer (§9.0), builtin dispatch (§10-arch), and the builtin redirect path (§9.2).

---

## 11. Maintenance notes

- **`Builtins.Names` ↔ the `TryExecute` switch must stay in sync** — `Names`/`Has` drive both
  tab completion and the evaluator's builtin-vs-external decision; a name in one but not the
  other is a latent bug.
- **No line numbers in this file** (they churn every edit). Cite class + method.
- A **structural** change (new stage/subsystem, re-wired pipeline) updates
  [`ARCHITECTURE.md`](ARCHITECTURE.md) *and* this file; a **surface** change updates only this
  file. Routine status moves go in [`PROJECT_CONTEXT.md`](PROJECT_CONTEXT.md); rationale goes in
  [`DECISIONS.md`](DECISIONS.md).
