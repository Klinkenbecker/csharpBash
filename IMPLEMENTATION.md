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
| `app.manifest` | The application manifest (`<ApplicationManifest>` in the csproj; the AOT publish embeds it): longPathAware, so the shell's `cd` can enter a directory over 260 characters; asInvoker; Windows 10. |
| `Lexer/Lexer.cs` | Tokeniser. |
| `Lexer/Token.cs`, `Lexer/TokenType.cs` | `Token` record (+ `HasLeadingSpace`) and token kinds. |
| `Lexer/LexException.cs` | Lex error; `Incomplete` flag drives REPL continuation. |
| `Parser/Parser.cs` | Recursive-descent parser. |
| `Parser/Nodes.cs` | AST record types (`Node`, `WordPart`, `CondExpr` hierarchies). |
| `Parser/CommandText.cs` | A simple command's text as bash prints it, from its source span (`$BASH_COMMAND`, the printer's simple commands). |
| `Parser/CommandPrinter.cs` | bash's command printer (print_cmd.c) over the AST: `declare -f`, `type`, `jobs`. |
| `Parser/ParseException.cs` | Parse error: `Incomplete` (REPL continuation), the token it failed at (`At`), and how bash words it (`Missing` closer, `[[ ]]` form, `Unsupported`, `Status`). |
| `Parser/SyntaxMessage.cs` | bash's report of a syntax error (parse.y `report_syntax_error`, `parser_error`, `cond_error`): "syntax error near unexpected token" with the source line, "syntax error: unexpected end of file", "unexpected EOF while looking for matching" a closer, and the `[[ ]]` forms. |
| `Evaluator/Evaluator.cs` | Tree-walking executor: dispatch, pipelines, redirects (device files via `DeviceRedirect`; `N>&1` copies; `ShellToken`: the shell that opened a descriptor's stream, the only one whose `n>&-` closes it, 2026-10-19), exported functions (`_exportedFunctions`: `BASH_FUNC_name%%` to children, `ImportFunctions` at startup, one definition only), external launch (`CurrentStdin`: a file the shell reads is shared with the child at its offset), jobs (`wait -n`, `jobs` in bash's format), traps, interrupts; the call-stack frames behind FUNCNAME / BASH_SOURCE / BASH_LINENO; bash's error prefix (`ErrorPrefix`, `SyntaxPrefix`) and the line base that numbers `eval` and `$( )` text from the caller's line. |
| `Evaluator/WordExpander.cs` | The expansion passes. |
| `Evaluator/ArithParser.cs` | Arithmetic precedence evaluator. |
| `Evaluator/Builtins.cs` | Dispatch (`Names`/`CoreutilNames`/`Has`/`TryExecute` with the decision-2 policy: `UnsupportedOptionException` → PATH external if present, else loud exit 2; `BASH_COREUTILS=auto\|builtin\|external`); a shell builtin's bad option is bash's message plus its usage line (`BadOption`, `Usages` from bash 5.1's `help -s`, `FlagOptions`), under the name it was called by + the small tools that stayed here (`factor` `cal` `expr` `which` `hash` `kill` `trap` `jobs`…). |
| `Evaluator/Builtins.Shell.cs` | The shell builtins proper (partial class): echo/printf, cd/pwd/pushd/popd/dirs, export/unset/readonly/declare/local, set/shopt, alias, source/eval, type/command/builtin, test/[, read/mapfile/getopts, let, kill, env, sleep, exit/return/break/continue, compgen (`Compgen`, `CompgenWords`/`CompgenPaths`/`CompgenGlob`/`CompgenSignals`; complete/compopt answer `CompletionNotAvailable`). |
| `Evaluator/Opts.cs` | `Opts.Parse`: strict GNU-style option parser for every coreutil (short clusters, attached/separate/optional values `c:`/`c::`, long `name=`/`name=?`/aliases `name:c`, `--`, `stopAtFirstOperand`, numeric `-N`); anything unknown throws `UnsupportedOptionException`. |
| `Evaluator/Builtins.Text.cs` | Text tools on `Opts`: cat head tail wc rev tac tr cut uniq nl fold paste comm seq split od sort tee base64 md5sum/sha*sum hexdump strings (GNU 2.38: `StringsScan` resyncs a byte at a time as GNU); helpers `OpenText`/`ReadLines`/`FilesOrStdin`/`IoError` (`ReadBytes` is gone, 2026-10-27). uniq (a group at a time; coreutils' -d/-u/-D switches), comm (a two-way merge; --total, -z, GNU's order check), paste (a row at a time, each `-` reading stdin in turn; `PasteDelims`), head -n N (the shared line reader, a missing newline kept) and tail -f (appended bytes through the byte layer, the next round from where the read ended) read as they go (2026-10-27); tee reads a pooled block 8 KiB at a time (measured) and holds back a high surrogate that ends a here-document chunk. tail, head, wc (`WcCount`: an ASCII block counted on its bytes, `WcAscii`), the checksums (`IncrementalHash`), and `cat`/`head -c` into a capture work on bytes through the shared layer in `Builtins.Bytes.cs` (2026-10-27); `wc -c` alone on a seekable input takes its length. |
| `Evaluator/Builtins.Bytes.cs` | The byte layer the tools share (2026-10-27): `OpenBytes` (file, device, stdin's bytes, a here-document's text), `Block` (a 64 KiB `IoBlock` rented from ArrayPool.Shared), `ByteSink` (raw, or decoded with the carrying decoder; characters rented), `PipeWindow` (a pipe's end in rented blocks, dropped once the newer ones hold enough), `RecordsStart` (a seekable input read backwards), `CopyRange`, `FromEnd`, `CopyAfter` (+N), `CopyRecords` (head -z), `CopyAll`. Rent, never allocate per call: a per-call 64 KiB and 131 KB of characters cost a tiny `wc` +10 us (measured). |
| `Evaluator/Builtins.Files.cs` | File/dir tools: basename dirname mkdir rmdir touch rm mv cp ls (long format) du cmp stat mktemp realpath readlink chmod ln truncate; `TryParseDate` delegates to `GnuDate`. |
| `Evaluator/Builtins.Find.cs` | `find` (expression tree, `-exec ;`/`+`, `-printf`, `-delete`…), `diff` (normal/unified/recursive, GNU hunk order), `xargs` (`-0 -n -I -L -P -d -a -r -t`, exit codes 123/124/126/127). |
| `Evaluator/Builtins.Search.cs` | `BreToNet`/`EreToNet`/`AppendBracket` (POSIX classes, `\<`/`\>`), `MakeRegex`; `grep` (all major flags) and `sed` (compiler + runtime: addresses, ranges, `{ }`, hold space, `s` flags, `-i[SUF]`, `-s`, `-z`). sed reads as it goes (2026-10-27): `SedInput` yields lines as the script asks (stdin through the shared reader; `$` a Peek, opening later files to look), `SedOut` owes a missing newline to the next write, `SedShared` holds stdout/stderr/the `w` files (opened once before any input) and `Quit`; the pattern and hold spaces each carry GNU's "newline was there" flag; `-i` writes a temporary file beside the original and `File.Replace`s it. |
| `Evaluator/Builtins.Sys.cs` | `date` (full `strftime`, `-d/-r/-u/-I/-R/-f`), `uname` (MSYS-style), `hostname`, `arch`, `nproc`, `tty`, `whoami`, `id`, `printenv`, `timeout` (worker thread + `RunAsJob` so an external child can be killed on expiry), `df` (Windows volumes via `GetLogicalDrives`/`GetVolumePathNameW`/`GetVolumeInformationW`/`GetDiskFreeSpaceExW`/`WNetGetConnectionW`; gnulib's `human_readable`, `humblock` and df's `get_header` ported as `GnuHuman`, `DfBlockSpec`, `DfBlockHeader`). |
| `Evaluator/Builtins.Awk.cs` | The in-process `awk` subset (DECISIONS 2026-09-04 #7): lexer (regex-vs-division by previous token), recursive-descent parser to a small AST, interpreter with awk value typing (num/str/strnum), fields/NF rebuild, `printf`, `sub`/`gsub`/`split`/`match`…; user functions (`AwkCell` frames, `return` flag, `EnsureSufficientExecutionStack` guard), output files and `getline < file` tables, `close`/`fflush`, 3-argument `match`, `--version`; unsupported constructs → `UnsupportedOptionException("awk","unsupported",…)`; gawk's fatal errors → `AwkFatalException` (exit 2). |
| `Evaluator/GnuDate.cs` | `GnuDate.TryParse`: GNU `-d` grammar subset (ISO/RFC/`Mon D YYYY`/`@epoch`/times/zones/relative items/weekdays), shared by `date -d` and `touch -d`. |
| `Evaluator/Builtins.Proc.cs` | Minimal procps: `pgrep`/`pkill` (`-f -x -l -a -c -n -o -v -P -d`, signals accepted and forceful; self excluded, `pkill` also skips loudly this shell's ancestors and any process whose command line carries the pkill invocation itself) and `ps` (`-e/-ef/aux`, `-p`, `-o pid,ppid,comm,args`). Process list via Toolhelp32, command lines via `NtQueryInformationProcess(ProcessCommandLineInformation)`. |
| `Evaluator/Printf.cs` | `PrintfFormatter`: bash `printf` semantics (flags/width/precision, `%q` `%b`, escapes, argument recycling); also used by `echo -e`. |
| `Evaluator/FileTests.cs` | The file/string primaries shared by `test`/`[` and `[[ ]]` (paths translated; device files are character devices). `IsProgramExtension` (.com .exe .bat .cmd) and `IsExecutable` (that, `.sh`, or `#!`): the one notion of "a program" for `-x`, the PATH search, `ls -F`, `find -executable`. |
| `Evaluator/ShellFile.cs` | Every file the shell opens, shared as POSIX shares it (no locking out readers, writers or `rm`); `>>` as an append-only handle (`Append`: every write lands at the end, O_APPEND; `PathOf` keeps its path, .NET names it "[Unknown]"); a path through a file is "Not a directory" (`ThroughAFile`, `NotADirectory`) (2026-10-19); and Linux's device files (`/dev/null`, `zero`, `random`, `urandom`, `stdin`, `stdout`, `stderr`, `fd/N`, `tty`): `Device`, `OpenInput`, `Exists`; `OpenTty`: `/dev/tty` only when the console has a window (`TtyAvailable`), else "No such device or address" (Claude Code's windowless console). |
| `Evaluator/Timing.cs` | The performance review's timing switch: with `BASH_TIMING_LOG` set, marks (Main, console, evaluator, ready, done; `source`, `eval` and external-program regions; since 2026-10-18 the start-up's parts: `console.out/in/writers`, `env.import/git/own/home/uid/ppid/done`) appended to that file at exit, in ms since process creation, with `icu yes|no` (whether icu.dll was needed; case icu_lazy); read by `tests/bench/claudecall.sh`. Off: one environment read. |
| `Evaluator/CommandMap.cs` | The persistent command map behind `ResolveOnPath`: hits and misses per PATH value (with PATHEXT) in `~/.bash/commands/<fnv64>.map` (`HOME`, else the Windows profile) (format line, PATH, PATHEXT, then `name<TAB>path`, empty path = a remembered miss); read on the first lookup; hits from every lookup (`Hit`), misses recorded and obeyed by `hash name` alone (`ResolveOnPath(..., probe: true)`; `EarlierMiss` ignores this process's own), final until `hash -r`/`-d`; not kept for a relative PATH entry; writes re-read, apply one change and rename a temp file; a map read is touched at most hourly, and one unused for 30 days is pruned when a new map is made. Process-wide, locked. |
| `Evaluator/ChildLauncher.cs` | The one way an external program starts (a `.bat`/`.cmd` through an explicit `cmd.exe /e:ON /v:OFF /d /c`, each argument quoted for cmd by `AppendBatchArgument` as Rust's std since CVE-2024-24576, a line break refused with 126; `FileId`: one MSYS append handle per file; 2026-10-19): exactly the three std handles chosen (a file, NUL, a pipe end, the shell's own; a file the shell reads is shared at its offset), nothing else inherited (`PROC_THREAD_ATTRIBUTE_HANDLE_LIST`); MSYS-program detection; file-pointer helpers; `WritableForMsys` (an inherited append-only std handle reopened for writing at the end for an MSYS child). The program's path goes on the command line with backslashes (cmd.exe misread a `/` path). |
| `Evaluator/Builtins.Jq.cs`, `JqParser.cs`, `JqInterp.cs`, `JqJson.cs` | The in-process `jq` subset (jq 1.6 behaviour): option handling and inputs; the filter lexer/parser; the generator interpreter with path-based assignment; ordered JSON values and the reader/writer. Outside the subset: a PATH jq, or a loud refusal. |
| `Evaluator/Glob.cs` | Shell pattern → regex (`*` `?` `[…]` classes), `Match`, and the pathname-expansion walker (`dotglob`/`nullglob`/`nocaseglob`/`globstar`). |
| `Evaluator/ConsoleMux.cs` | `ConsoleMux` (per-thread console slots + `Capture`/`Apply`), `PipeBuffer` (managed pipe with back-pressure, EOF and `BrokenPipeException`), `ChildJobs` (kill-on-close job object), `HiddenConsole` (a console-less shell -- Claude Code's Bash tool -- starts children with `CreateNoWindow` and attaches to the first one's hidden console, so no child ever opens a visible window; `StartInherited` starts a child with NO stream redirected through `CreateProcessW` + `STARTF_USESTDHANDLES` on the shell's own three std handles, because `Process.Start` would hand it its new console's handles and its output would be lost). |
| `Evaluator/ShellEnvironment.cs` | Variables (`Var`: scalar or array with its attributes, in scope frames looked up innermost-out: bash's dynamic scoping), exports, special parameters, scope/positional stack, `NativeArgument` (a Windows program's MSYS-form arguments), snapshot/restore (subshells), `TranslatePath` (inbound: MSYS `/c/…`, `/tmp`, `~` → forward-slash Windows form) and `ToShellPath`/`ShellCwd`/`FullPath` (outbound: the same form, so the two directions agree), PATH normalisation and the Git-tools augmentation. |
| `Evaluator/ShellCulture.cs` | `Pin()`, first in Main: the invariant culture as every thread's default, so numbers and dates format and parse as bash's C locale whatever the Windows user's culture (2026-10-18; ARCHITECTURE 9.4). |
| `Evaluator/ShellEncoding.cs` | (Also `LfReader.ForDescriptor` / `Release`: `read -u N`'s reader, kept between reads; what it read ahead goes back to a file before any other use of N, and a pipe's readers share it; 2026-10-19.) The shell's single encoding: UTF-8 with surrogateescape (an undecodable byte ↔ the lone surrogate U+DC00+byte), so bytes that are not text survive variables, `$( )`, pipes and files. `ByteChar` is what the `\xNN`/`\NNN` escape handlers emit; `ReadAllText`/`ReadAllLines` replace the `File.*` equivalents, which silently ignore their encoding argument on a file that starts with a BOM. Encode and decode are both overridden because an `EncoderFallback` cannot emit raw bytes. **Lines are LF-only everywhere** (a CR is data, as in GNU): `LfReader` is the shell's text reader -- over a raw stream it decodes with ONE read per fill (a `StreamReader` asked for a block keeps reading until full, which stalled pipeline stages), and its `ReadLine` is a vectorised LF scan. Its decoder is `CarryDecoder`, which carries a character cut by a block's end into the next block (Encoding's default decoder is stateless, so a character across byte 4096 became two raw bytes). Every stdin the shell installs is an `LfReader` (pipeline stages, `< file`, `<&n`, a redirected process stdin), so all readers of a stdin share one buffer and none reads past another; tools open their files through one too. `ShellEncoding.ReadLine`/`Lines`/`LineReaderFor` are the entry points; never call `TextReader.ReadLine`, which also ends a line at a lone CR. `ShellEncoding.ReadRecord` / `LfReader.ReadRecord` (2026-10-27): a record to LF or NUL and whether the separator ended it (sed, uniq, comm, paste, head). `CarryEncoder` (`GetEncoder`, 2026-10-27): holds a high surrogate that ends a chunk for the low one starting the next, so every StreamWriter and `File.WriteAllText` over the encoding keeps pairs whole. |
| `Evaluator/ShellOptions.cs` | `set` flags (`-e/-u/-x/-n/-f/-v/-C/-a/-o …`), the `shopt` set, invocation facts, `$-`. |
| `Evaluator/BackgroundJob.cs` | Background-job record; `Finished` wakes `wait -n`. |
| `Evaluator/EvalException.cs` | Runtime exception hierarchy (Exit/Return/Break/Continue/Interrupt/Eval/CommandAbort). |
| `Evaluator/NotAvailable.cs` | `NotAvailable`: "not currently available in C#Bash" plus a prefilled-GitHub-issue link (feature and build only; restricted mode refused, POSIX mode goes on), each notice logged by `Unserviced`; `BuildInfo.Stamp`, the one reader of the build stamp. |
| `Evaluator/Unserviced.cs` | `Unserviced.Log`: requests C#Bash did not service, one JSON line each in `%USERPROFILE%/.bash/unserviced.jsonl` (`Folder`: the USERPROFILE variable, else the Windows profile; `BASH_UNSERVICED_LOG` another file, `/dev/null` none), appended in one write through `ShellFile.Append`, never failing the command. Called from: `Builtins.TryExecute` (an unsupported option: fell through or failed), the awk refusal while running, `NotAvailable.Lines`, `Evaluator` (a construct refused when parsed; `NotFound` for a bash builtin C#Bash lacks), `help`. |
| `Evaluator/Builtins.Help.cs` | `help` (bash's matching: exact or glob, else prefix; -d -m -s), `--help` (`AsksForHelp`: first argument, as bash, except `: [ echo false test true`; later among a tool's options, Opts throws `HelpRequestedException`), `enable` (lists; -n/-f/-d not available); `HelpTable`: %USERPROFILE%/.bash/help.json over the built-in `help.json` resource name by name (written when missing); `BashBuiltinNames`, `NotImplemented`. |
| `help.json` | `help`'s built-in table, name to manual page URL(s), embedded as a resource; checked by `tools/check-help-urls.sh`. |
| `IO/LineEditor.cs` | Interactive key-by-key line editor. |
| `IO/History.cs` | Command history (`~/.bash_history`). |
| `IO/CompletionEngine.cs` | Tab completion. |
| `IO/PromptExpander.cs` | `PS1`/`PS2` prompt escapes; `DecodeForExpansion` for PS4 (decoded values protected from the expansion that follows). |
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
| `Tokenize(deferErrors)` | Drive the scan, produce `List<Token>`; with `deferErrors` a lexing error ends the list (an Eof) and waits in `DeferredError`. | §4 token stream |
| `Step(tokens)` / `Done` | On-demand lexing: one more step into the list a parser reads (`new Parser(lexer, src)`), so text after a top-level `exit` is never lexed. | §6 one command at a time |
| `NextToken(int)` | Emit the next token; sets `HasLeadingSpace`. | §4 word-boundary signal |
| `ConsumeDollar()` | Read `$var` / `${…}` (entire body, balancing braces/quotes) / `$(…)` / `$((…))`. | §4 `${…}` consumed whole |
| `ConsumeOperator(int)` | `\|`, `&&`, `\|\|`, `;`, redirections, `&`; bounds-safe `Peek`. | §5 operators |
| `FlushHeredocs` / `ConsumeHeredocBody` | Capture heredoc bodies after the newline. | §4 heredocs |
| `ConsumeSingleQuoted` / `ConsumeDoubleQuoted` / `ConsumeAnsiEscape` / `ConsumeHexEscape` | Quoting + `$'…'` decoding. | §4 quoting |
| `ConsumeWord` | Plain word runs; context-sensitive `{`/`}`. | §4 `{`/`}` |
| `SkipWhitespace` / `SkipComment` / `Peek` / `Advance` | Scanner primitives. | — |
| `ConstructEnd` / `CaseTracker` | Where a `$( )` (inside `"…"` or backquotes: on text) ends; `CaseTracker` follows `case WORD in` so a pattern's `)` does not count (bash's parse_comsub). The parser's `ParseCommandSubstitution` uses the same tracker on tokens. | §4 substitutions |

Raises `LexException(Incomplete)` at EOF mid-construct.

---

## 3. Parser — `Parser/Parser.cs`, `Parser/Nodes.cs`

| Method | Builds |
|--------|--------|
| `Parse()` → `ParseNext()` → `ParseList(topLevel)` | `Script` / `List` (continues through `;`, stops at `IsListTerminator`). `ParseNext` returns ONE top-level command (a line: a top-level list ends at a newline; only `&&`/`\|\|` go on), for `RunString`'s parse-one-run-one loop; `Fill` lexes on demand. |
| `DeferredSubstitution` | A `$( )` / backquote body that does not parse: a `CommandSubstitutionPart` holding a `SubstitutionSyntaxError` node, met when it runs. |
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

P1 additions: every `Node` carries `Line` (for `$LINENO`) and a `Span` into the source the parser
was given (`Parser(tokens, source)`), and every `Word` a `RawSpan` (its text as written): from these
`CommandText` renders a simple command (`$BASH_COMMAND`) and `CommandPrinter` lays out functions and
compound commands as bash's print_cmd.c does (`declare -f`, `type`, `jobs`; until 2026-10-12 a
function's verbatim source text was printed); `ArithmeticCommand` (`(( ))`) and `ArithForCommand` (`for ((;;))`)
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
| `RunString` / `SourceFile` / `ExecScript` | Parse-and-run a string / file / `Script`; `RunString` parses one top-level command, runs it, parses the next, and catches `CommandAbortException` (a bad `$( )`: status 1, next command). |
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
| `CurrentRawStdin` | The raw stdin for byte builtins; a text reader's unconsumed read-ahead (`LfReader.TakeBuffered`) comes first via `PrefixedStream`. Side effect: call it ONCE per use. |
| `ExecPipelineThreaded` / `Evaluator(Evaluator parent)` | Every pipeline stage runs on its own interpreter: a private copy constructor clones variable state (`ShellEnvironment.CloneForSubshell`, bypassing the 1.4 ms public constructor), options, functions, aliases, traps, fds, the PATH hash. A stage honours its parent's interrupt flag, and has its own cwd (next row). |
| `Lexer.EscapeMark` | U+FDD0 (a noncharacter) precedes a backslash-escaped `* ? [ { } , ~` in a word token; `Parser.ParseWordParts` emits each marked character as a `SingleQuotedPart`, so `echo \*` and `find -name \*.txt` do not glob. Glob/brace quoting is still tracked per FIELD (`fieldGlob`), a known limitation (DECISIONS 2026-10-03). |
| `Builtins.Files.StampNow` | A copy made without `-p` gets mtime and atime = now (Windows' `File.Copy` keeps the source's). |
| `WordExpander.ExpandOperand` | Expands the pattern and replacement of `${x#pat}` `${x%pat}` `${x/pat/rep}` by scanning their RAW `${…}` text: backslash escapes kept, quoted parts glob-escaped (`Glob.Escape`), unquoted expansions stay patterns; replacement gets quote removal. Segment parses are cached. `FindPatternSlash` ends the pattern at the first `/` outside quotes and expansions. Literal patterns take `Glob.TryLiteral`'s plain-string path; replacement regexes are cached. |
| `ShellEnvironment.Cwd` / `Active` / `TranslatePath` | Each shell's own working directory. The shell itself (`OwnsProcessCwd`) keeps the process cwd equal to it; a pipeline stage's copy never moves it. `Active` (thread-static) names the shell a thread runs. `TranslatePath` resolves a relative path against `Active.Cwd` only for a stage that has moved off its starting directory, and always for a `Detached` shell (a `&` job or `setsid -f` fork, inherited by its stages, subshells and in-process scripts: `Detach`, `CloneForSubshell`, `AdoptCwd`). `FullAgainst` resolves `\x` and `C:x` against a shell's own directory; `WithoutEmptyPathEntries` keeps empty PATH entries from Windows (the process PATH at startup and on every `Set("PATH")`). Dup-redirect targets are fd numbers: test the raw word, never the translated one. |
| `ExecExternal` | Self-contained fd-map + `Process.Start`; `HiddenConsole.NeedsHiding` before the start (every stream inherited AND console-less or under `2>&1`: `HiddenConsole.StartInherited` instead of `Process.Start`), `ChildJobs.Attach` then `HiddenConsole.Adopt` after it. A bare name `ResolveOnPath` misses: `FallbackProgram` (C#Bash's folder, System32, Windows) else `NotFound`, 127, never Windows' launcher search. A child's environment is exactly the exported variables (PATH cleaned of empty entries, the shell's PATH winning any casing). bash's messages for a path through a file / a directory (126), a missing interpreter (126, env's 127), the 260-character Windows limits (126). |
| `ResolveOnPath` / `HashClear` / `HashSetPath` / `HashTable` | PATH(+PATHEXT) cache (the `hash` table); behind it the persistent `CommandMap` (`HashReset` = `hash -r`, `HashDelete` = `hash -d`, `MissNote` for a remembered miss's "not found"). A bare match counts if `FileTests.IsExecutable`, else the first that cannot run (hashed, not mapped); only program extensions of PATHEXT are tried; an `IsPathName` (slash, backslash, drive) is never searched, a colon name never found. `PathEntry` decides each entry once (`~` = `$HOME`, `/f/x` translated; relative, `\x`, `C:x` answered as written and never cached); `AllOnPath` lists every runnable match (`type -a`). |
| `TryResolveScriptFile` / `ResolveInterpreter` / `RunScriptInProcess` / `IsProgramFile` | Shebang dispatch: a path, or a bare name's PATH match that is no program file (never the current directory); `$0` is the path found; the interpreter chain is capped (`MaxInterpreterDepth`). |
| `XtracePrefix` / `Xtrace` / `XtraceWord` / `XtraceDepth` / `CondTrace` | set -x as bash: PS4 decoded and expanded, its first character repeated per command-substitution level; words quoted (sh_contains_shell_metas); assignments, `[[ ]]` primaries, `(( ))`, for/arith-for/case heads traced. |
| `EnterSubshell` / `DropSubshellRedirects` / `LeaveSubshell` | An in-process `( )`, `$( )`, `<( )`: the `exec` redirects it made are disposed as it ends (after its EXIT trap). |
| `RunCommand(name,args)` | Re-entrant builtin→function→external dispatch (used by `xargs`). |

**Compound commands**: `ExecList`, `ExecIf`, `ExecWhile`, `ExecFor`, `ExecCase`,
`ExecSubshell`, `ExecWithRedirects`, `ExecFunctionDef`, `ExecFunction`, `ExecConditional`
(+ `EvalCondExpr`/`EvalCondUnary`/`EvalCondBinary`, and `GlobMatch` for `[[ ==` / `case`).

**Jobs, traps, signals** ([ARCHITECTURE §14](ARCHITECTURE.md#14-signals--jobs)):
`StartBackgroundJob`, `ListJobs`, `WaitJobs`, `FindJob`/`FindJobIn` (bash's get_job_spec: `%N`, `%%`,
`%-`, `%name`, `%?text`, matched against `BackgroundJob.Stages`), `KillJob`, `RunWithJobGroups` (`jobs -x`),
`SetTrap`/`RemoveTrap`/`RunTrap`/`RunExitTrap`,
`RequestInterrupt`/`ClearInterrupt`/`CheckInterrupt`. A job's `$!` is a synthetic pid
(`BackgroundJob.PidBase + Id`); `ChildPid` tracks the external it is running so `kill $!` works.
`ExecList` makes an and-or list that ends in `&` one job.

**P1 surface** (see DECISIONS 2026-09-04): `Aliases`, `GetFunction`/`RemoveFunction`/`HasFunction`,
`Keywords`, `Classify` (alias/keyword/function/builtin/file — backs `type`/`command -v`),
`RunCommand(name,args,skipFunctions,extraEnv,clearEnv)`, `RunBuiltin`, `GetFd` (the fd 3+ table
`_fds`, filled by `exec 3>f` / `cmd 3<f` through `RedirectScope.TrackFd`), `ExecArithmeticCommand`,
`ExecArithFor`, `ExecCondition` (errexit-suppressed), `ApplyTempAssignments` (`VAR=x builtin`),
`ExecExternalOrBuiltin` (`exec cmd`). `RunString(source, reportErrors, origin, syntaxErrorCode)`
reports `bash: origin: line N: …`. `ExecSubshell` uses `ShellEnvironment.TakeSnapshot`/`RestoreSnapshot`.
`ExecExternal` routes a child's stdio through swapped `Console` streams when an in-process redirect
is in effect (`$( )` capture, `{ … } >file`, heredoc on a loop) — the `_origOut/_origErr/_origIn`
comparison — and reports `command not found` (127; "No such file or directory" for a path name) /
`Permission denied` (126) to wherever the command's stderr was sent. A path name is launched by its full path
(`FromShellCwd`: relative ones joined to the shell's cwd), so Windows' launcher never searches for it.

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
| Text (`Builtins.Text.cs`) | `Cat` `Head` (streaming) `Tail` (`-f` polls) `Wc` (GNU widths) `Rev` `Tac` `Tr` `Cut` `Uniq` `Nl` `Fold` `Paste` `Comm` `Seq` `Split` `Od` `Sort` (`SortKey`, `-k -n -V -h -s -u -o`) `Tee` `Base64` `Checksum` `Hexdump` `Strings` (`StringsScan`, `StrToUl`) |
| File / dir (`Builtins.Files.cs`) | `Basename` `Dirname` `Mkdir` `Rmdir` `Touch` `Rm` `Mv` `Cp` `Ls` (`ModeString`/`LongLine`) `Du` `Cmp` `Stat` `Mktemp` `Realpath` `Readlink` `Chmod` `Ln` `Truncate` |
| Find / diff / xargs (`Builtins.Find.cs`) | `Find` (`FindParseOr/And/Not/Primary` → `FindNode` tree, `FindPrintf`, `FindExecPlusFlush`) `Diff` (`DiffPaths/DiffDirs/DiffFiles`) `Xargs` |
| Search (`Builtins.Search.cs`) | `Grep` `Sed` (`SedCompile`/`SedRun`/`SedSelect`/`SedExpand`) `BreToNet`/`EreToNet`/`AppendBracket`/`MakeRegex` |
| System (`Builtins.Sys.cs`) | `Date`/`Strftime` `Uname` `Arch` `Hostname` `Nproc` `Tty` `Whoami` `Id` `Printenv` `Timeout` `Df` → `DfProbe`/`DfRemoteName`/`DfPcent`/`DfBlockSpec`/`DfBlockHeader`/`GnuHuman` |
| awk (`Builtins.Awk.cs`) | `Awk` → `AwkLex`/`AwkParser`/`AwkInterp`, `AwkVal`, `AwkFormat`/`FormatG`; `print`/`printf` may target `"/dev/stderr"`/`"/dev/stdout"` (ratified 2026-09-04), nothing else |
| Processes (`Builtins.Proc.cs`) | `Pgrep`/`Pkill` → `PgrepImpl`, `Ps`, `ListProcesses`/`ReadCommandLine`/`AncestorsOf` |
| Still in `Builtins.cs` | `Factor` `Cal` `Expr` ladder `Which` `Hash` `Exec` `Trap`/`CanonSignal`/`PrintSignalTable` `Jobs`/`Fg`/`Bg` `HistoryCmd` (`Kill` and the directory stack are in `Builtins.Shell.cs`) |
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
| `PromptExpander` | `Expand(ps)` for `PS1`/`PS2` escapes (history/command numbers via callbacks); `DecodeForExpansion(ps)` for PS4. |

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

## 11. Tooling: build, tests, benchmark, deploy, publish

Every repetitive check is a script in the repository, run one way (DECISIONS 2026-10-12, "repetitive tools here need
to be contracts"). Each outside system it uses has one job: **WSL is the behavioural reference; Git for Windows is
reached only by the benchmark and the push; MSYS2 by one suite case; none of them by the shipped shell**
(DECISIONS 2026-10-19 "METHOD: the battery's reference is WSL's GNU bash", 2026-10-20 "FIXED: the tests no longer
need Git", 2026-10-20 "PROPOSED": no MSYS in C#Bash). Machine paths are set at the top of the script that uses them.

| Tool | Runs in | Builds or runs | Outside systems |
|------|---------|----------------|-----------------|
| `dotnet build -c Release Bash` | any shell | the managed build, `Bash/bin/Release/net8.0/Bash.exe`: what every fix is tested on | .NET 8 SDK |
| `tools/publish-aot.cmd [out]` | cmd (`cmd.exe /c`) | the Native AOT build, the one shipped (`dist/` by default); deletes `Bash/obj` first | .NET 8 SDK; VS 2022 C++ tools (`vcvars64.bat`, under `VS=`), `vswhere.exe` (VS Installer folder, put on PATH by the script) |
| `tests/run-tests.ps1 [-NoBuild] [-Detached] [-Interpreter exe]` | Windows PowerShell 5.1 | the suite: each `cases/*.sh` against `expected/*.out`; `-Detached` starts each case as Claude Code starts its shell | none for the runner. A case needing a program that is absent prints `SKIP: <reason>` as its only line, and the runner reports and names it: Git for Windows on PATH (`msys_redirects`, `msys_arguments`, `msys_inherited_append`, `native_args_msys`, `native_args_git`, `dev_tty_git_prompt`, `coreutils_external`), a PATH awk (`awk_fallthrough`), `F:/Tools/msys64` (`msys_append_shared`) |
| expected outputs (`tests/expected/*.out`) | by hand | most taken by running the case under GNU bash in WSL; Windows-specific ones authored | WSL |
| `tests/compat/run-compat.ps1 [-NoBuild] [-UpdateBaseline]` | Windows PowerShell 5.1 | the battery: 237 probes, the managed build against a reference bash, `baseline.txt` ratcheted (`-UpdateBaseline` only adds; a probe leaves it by hand) | WSL (`wsl.exe -e bash`), or `-Reference <bash.exe>`; C#Bash refused as its own reference |
| `tests/corpus/run-n.sh` | WSL's bash (`wsl.exe -e bash tests/corpus/run-n.sh`) | `bash -n` and the printer on 912 bash-completion scripts, GNU's verdicts measured live | WSL (its bash and `/usr/share/bash-completion`); C#Bash started through WSL interop |
| `tests/df/compare-wsl.sh` | WSL's bash | C#Bash's `df` against GNU `df` on the same volumes | WSL |
| `tests/bench/compare3.sh [--build \| --cs exe]` | Git Bash (`F:/Tools/PortableGit/bin/bash.exe tests/bench/compare3.sh`; refuses C#Bash) | the benchmark: C#Bash, Git Bash and WSL bash timed by `tools/benchtimer`; `--build` publishes AOT into `tests/bench/aot` | Git for Windows (Git Bash is a timed shell and the harness; `cygpath` for `--build`); WSL (a timed shell); `tools/benchtimer` (.NET) |
| `tests/bench/compare.sh` | Git Bash | the older two-way comparison (a reference bash against C#Bash) | Git for Windows |
| `tests/bench/claudecall.sh` | any bash with `cygpath` on PATH, so Git Bash since Git left PATH | what a Claude Code Bash call costs, and where it goes | Git for Windows (`cygpath`): not yet removed as deploy.sh's was |
| `tools/deploy.sh` | C#Bash (refuses a root that is not a drive path) | publish-aot to `dist.staging`; the suite on that binary, both modes; the battery; rename-and-swap into `dist/` | as its steps: VS C++ tools, WSL; no Git since 2026-10-20 |
| `tools/check-help-urls.sh [table]` | any bash with curl and jq (C#Bash, WSL's bash) | every page in `Bash/help.json` answers (200) and every `#anchor` is on its page; exits 1 on any failure | curl, jq, the network |
| `tools/publish.sh <msg> <token-file>` | any bash (C#Bash) | GitHub's main brought up to the committed hg tree, with `MERCURIAL_HISTORY.md` and the hg bundle | Git for Windows (`git.exe` at `GIT=`), hg |

At run time C#Bash touches Git and MSYS only as programs it may find: `ShellEnvironment` prepends Git's `usr/bin` and
`mingw64/bin` when `git.exe` is on PATH, and `ChildLauncher.IsMsysProgram` (an `msys-2.0.dll` or `cygwin1.dll` beside
the program) selects MSYS argument quoting (`AppendArgument`), no path conversion, and writable append handles
(`Access.AppendAtEnd`, `WritableForMsys`).

## 12. Maintenance notes

- **`Builtins.Names` ↔ the `TryExecute` switch must stay in sync** — `Names`/`Has` drive both
  tab completion and the evaluator's builtin-vs-external decision; a name in one but not the
  other is a latent bug.
- **No line numbers in this file** (they churn every edit). Cite class + method.
- A **structural** change (new stage/subsystem, re-wired pipeline) updates
  [`ARCHITECTURE.md`](ARCHITECTURE.md) *and* this file; a **surface** change updates only this
  file. Routine status moves go in [`PROJECT_CONTEXT.md`](PROJECT_CONTEXT.md); rationale goes in
  [`DECISIONS.md`](DECISIONS.md).
