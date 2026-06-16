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
| `Evaluator/Builtins.cs` | All builtins + in-process coreutils + their helpers. |
| `Evaluator/ShellEnvironment.cs` | Variables, arrays, exports, scope/positional stack, `TranslatePath`. |
| `Evaluator/ShellOptions.cs` | `set -e/-u/-x/-n/-f/-o pipefail` flags. |
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
| `ExecPipeline` / `AllStagesInProcess` | Route to sequential vs threaded. |
| `ExecPipelineSequential` / `ExecuteWithStdin` | Buffered single-thread pipeline; capture stdout → next stdin. |
| `ExecPipelineViaThreads` | Thread-per-stage with `AnonymousPipe` pairs; `[ThreadStatic] _pipeStdin/_pipeStdout`. |
| `ExecSimpleCommand` | Expand args; gate on `Builtins.Has(name)\|\|_functions` → `ApplyRedirects`+run, else `ExecExternal`. |
| `ApplyRedirects` → `RedirectScope` | Console-swap redirects for in-process commands; RAII restore + dispose. |
| `CurrentRawStdout` (+ `_rawStdout`, `_capturing`) | Byte-faithful sink for builtins. |

**External commands & path** ([ARCHITECTURE §12](ARCHITECTURE.md#12-external-commands--the-path-model))

| Method | Role |
|--------|------|
| `ExecExternal` | Self-contained fd-map + `Process.Start`. |
| `ResolveOnPath` / `HashClear` / `HashSetPath` / `HashTable` | PATH(+PATHEXT) cache (the `hash` table). |
| `TryResolveScriptFile` / `ResolveInterpreter` / `RunScriptInProcess` | Shebang dispatch. |
| `RunCommand(name,args)` | Re-entrant builtin→function→external dispatch (used by `xargs`). |

**Compound commands**: `ExecList`, `ExecIf`, `ExecWhile`, `ExecFor`, `ExecCase`,
`ExecSubshell`, `ExecWithRedirects`, `ExecFunctionDef`, `ExecFunction`, `ExecConditional`
(+ `EvalCondExpr`/`EvalCondUnary`/`EvalCondBinary`, and `GlobMatch` for `[[ ==` / `case`).

**Jobs, traps, signals** ([ARCHITECTURE §14](ARCHITECTURE.md#14-signals--jobs)):
`StartBackgroundJob`, `ListJobs`, `WaitJobs`, `SetTrap`/`RemoveTrap`/`RunTrap`/`RunExitTrap`,
`RequestInterrupt`/`ClearInterrupt`/`CheckInterrupt`.

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

## 7. Builtins & coreutils — `Evaluator/Builtins.cs`

**Dispatch:** `TryExecute(name,args,out code)` is the `switch`; `Names` is the membership list
(**keep in sync with the switch**); `Has(name)` is the O(1) membership test used by the
evaluator.

| Family | Methods |
|--------|---------|
| Shell builtins | `Echo` `Printf` `Cd` `Pwd` `Export` `Unset` `Set` `Shift` `Read` `Source` `Local` `Declare` `DoEval` `Type` `Command` `Test`/`TestBracket`/`TestImpl` `Sleep` `Env` `HistoryCmd` `Trap` `Jobs` `Fg` `Bg` `DoExit`/`DoReturn`/`DoBreak`/`DoContinue` |
| Text | `Cat` `Head` `Tail` `Wc` `Rev` `Tac` `Tr` `Cut` `Uniq` `Nl` `Fold` `Paste` `Comm` |
| File / dir | `Touch` `Rmdir` `Mkdir` `Cmp` `Tee` `Rm` `Mv` `Cp` `Du` `Ls` `Find` `Split` `Basename` `Dirname` |
| Search / transform | `Grep` `Sed` `Sort` `Od` `Diff` `Seq` `Factor` `Cal` `Date` `Expr` `Which` `Xargs` `Uname` `Hostname` `Kill` |
| Shared helpers | `LinesOf` `BreToNet` `ExpandTrSet`/`TrClass` `ParseSize` `InterpretEscapes`/`UnescapeDelims` `CopyDir` `FindWalk`/`GrepCollect` `SedSub`/`SedParse`/`SedExpand`/`SedReadDelim` `Expr*` ladder `Du*` helpers `FindInPath`/`CanonSignal` |

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
3. `AllStagesInProcess` is **true** (both `echo` and `grep` are in `Builtins.Names`) →
   `ExecPipelineSequential`.
4. **Stage 0** `ExecuteWithStdin(echo $x, stdin=null, capture=true)` → `ExecSimpleCommand`:
   `WordExpander.ExpandToFields($x)` → `["cat"]`; `echo` writes `cat\n` to `Console.Out`, which
   is captured to the buffer.
5. **Stage 1 (last)** `ExecuteWithStdin(grep a >out, stdin=buffer, capture=false)`:
   `Console.In` is set to the buffered bytes; `ExecSimpleCommand` sees `grep` is a builtin →
   `ApplyRedirects([Output→out])` swaps `Console.Out` to the file `out` under a `RedirectScope`;
   `Builtins.TryExecute("grep", ["a"])` reads stdin, keeps lines matching `a`, writes to
   `Console.Out` (the file). `RedirectScope.Dispose` restores `Console.Out` and closes the file.
6. The pipeline's exit code (last stage, or worst under `pipefail`) becomes `$?`.

Touches: lexer/parser front-end, parameter expansion (§7), sequential pipeline (§9.1), builtin
dispatch (§10-arch), and the builtin Console-swap redirect path (§9.2).

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
