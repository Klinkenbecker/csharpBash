using System.Diagnostics;
using Bash.Parser;

namespace Bash.Evaluator;

/// <summary>
/// Walks the AST and executes it.
/// Returns the exit code of the last command executed.
/// </summary>
public sealed class Evaluator
	{
	private readonly ShellEnvironment _env;
	private readonly Builtins _builtins;
	private readonly WordExpander _expander;
	public  readonly ShellOptions Options = new();

	// User-defined functions: name → body node
	private readonly Dictionary<string, Node> _functions = new(StringComparer.Ordinal);

	/// <summary>The shell environment (exposed for the line editor / completion).</summary>
	public ShellEnvironment Env => _env;

	/// <summary>Names of currently-defined functions (for tab completion).</summary>
	public IEnumerable<string> FunctionNames => _functions.Keys;

	/// <summary>Command history, set by the REPL; used by the `history` builtin. Null in script mode.</summary>
	public Bash.IO.History? History { get; set; }

	// SIGINT (Ctrl+C) handling. The REPL's CancelKeyPress handler sets the flag from
	// another thread; loop/command boundaries poll it and throw InterruptException.
	private volatile bool _interrupted;
	public void RequestInterrupt() => _interrupted = true;
	public void ClearInterrupt()   => _interrupted = false;
	public bool Interrupted => _interrupted;

	/// <summary>If a SIGINT is pending: run the INT trap if one is set (or ignore it
	/// when the trap is empty), otherwise throw InterruptException. Clears the flag.</summary>
	public void CheckInterrupt()
		{
		if (!_interrupted) return;
		_interrupted = false;
		if (_traps.TryGetValue("INT", out var cmd))
			{
			if (cmd.Length > 0) RunTrap(cmd);   // empty => ignore the signal
			return;
			}
		throw new InterruptException();
		}

	// ── traps ───────────────────────────────────────────────────────────────────
	// Only EXIT and INT actually fire (Windows lacks the rest); other sigspecs are
	// stored but never triggered. Empty command string = ignore the signal.
	private readonly Dictionary<string, string> _traps = new(StringComparer.Ordinal);
	private bool _inTrap;       // guard against trap handlers re-triggering traps
	private bool _exitTrapRan;

	public void SetTrap(string sig, string command) => _traps[sig] = command;
	public void RemoveTrap(string sig) => _traps.Remove(sig);
	public IReadOnlyDictionary<string, string> Traps => _traps;

	private void RunTrap(string command)
		{
		if (_inTrap) return;
		_inTrap = true;
		try { RunString(command, reportErrors: true); }
		finally { _inTrap = false; }
		}

	/// <summary>Run the EXIT trap exactly once (called at every shell-exit path).</summary>
	public void RunExitTrap()
		{
		if (_exitTrapRan) return;
		_exitTrapRan = true;
		if (_traps.TryGetValue("EXIT", out var cmd) && cmd.Length > 0)
			RunTrap(cmd);
		}

	public Evaluator(ShellEnvironment? env = null)
		{
		_env      = env ?? new ShellEnvironment();
		_builtins = new Builtins(_env, this);
		_expander = new WordExpander(_env, this);
		}

	// ── public entry point ────────────────────────────────────────────────────

	/// <summary>Execute a node and return its exit code.</summary>
	public int Execute(Node node)
		{
		if (Options.NoExec) return 0;

		int code = node switch
			{
			Script s                  => ExecScript(s),
			List l                    => ExecList(l),
			Pipeline p                => ExecPipeline(p),
			SimpleCommand cmd         => ExecSimpleCommand(cmd),
			ArrayElementAssign ae     => ExecArrayElementAssign(ae),
			ArrayCompoundAssign ac    => ExecArrayCompoundAssign(ac),
			BraceGroup bg             => ExecWithRedirects(bg.Body, bg.Redirects),
			Subshell ss               => ExecSubshell(ss),
			IfCommand ic              => ExecIf(ic),
			WhileCommand wc           => ExecWhile(wc),
			ForCommand fc             => ExecFor(fc),
			CaseCommand cc            => ExecCase(cc),
			FunctionDef fd            => ExecFunctionDef(fd),
			ConditionalExpression ce  => ExecConditional(ce),
			_ => throw new EvalException($"Unhandled node type: {node.GetType().Name}")
			};

		_env.LastExitCode = code;

		if (Options.ExitOnError && code != 0 && node is not IfCommand and not WhileCommand and not ForCommand)
			throw new ExitException(code);

		return code;
		}

	/// <summary>Lex, parse, and execute a source string. Re-throws ExitException so
	/// callers can honour `exit`; other errors are reported (unless suppressed) and
	/// yield exit code 1.</summary>
	public int RunString(string source, bool reportErrors = true)
		{
		try
			{
			var tokens = new Lexer.Lexer(source).Tokenize();
			var ast    = new Parser.Parser(tokens).Parse();
			return Execute(ast);
			}
		catch (ExitException) { throw; }
		catch (Lexer.LexException ex)   { if (reportErrors) Console.Error.WriteLine($"bash: {ex.Message}"); return 1; }
		catch (Parser.ParseException ex){ if (reportErrors) Console.Error.WriteLine($"bash: {ex.Message}"); return 1; }
		catch (EvalException ex)        { if (reportErrors) Console.Error.WriteLine($"bash: {ex.Message}"); return 1; }
		}

	/// <summary>Source a file (startup files, BASH_ENV). When silentIfMissing is set,
	/// a non-existent file is a no-op returning 0.</summary>
	public int SourceFile(string path, bool silentIfMissing = false)
		{
		var translated = ShellEnvironment.TranslatePath(path);
		if (!File.Exists(translated))
			{
			if (silentIfMissing) return 0;
			Console.Error.WriteLine($"bash: {path}: No such file or directory");
			return 1;
			}
		return RunString(File.ReadAllText(translated));
		}

	// ── script / list ─────────────────────────────────────────────────────────

	private int ExecScript(Script s)
		{
		int code = 0;
		foreach (var n in s.Nodes)
			{
			CheckInterrupt();
			code = RunStatement(n);
			}
		return code;
		}

	/// <summary>Execute one statement, turning a runtime EvalException (bad redirect,
	/// arithmetic error, …) into a failed command ($?=2) that the surrounding list/script
	/// continues past — matching bash, where such errors don't abort the whole script.
	/// Control-flow exceptions (exit/return/break/continue/interrupt) still propagate.</summary>
	private int RunStatement(Node n)
		{
		try { return Execute(n); }
		catch (EvalException ex) { Console.Error.WriteLine($"bash: {ex.Message}"); _env.LastExitCode = 2; return 2; }
		}

	private int ExecList(List l)
		{
		// Each item's Op is the connector that FOLLOWS it. && / || short-circuit
		// left-associatively (a && b || c == (a && b) || c) and bind tighter than
		// ; / newline / & — so a skip due to a failed && must NOT run past the next
		// ;/newline boundary. We decide per item whether to run it based on the
		// *previous* connector and the running exit code; a skipped item leaves the
		// code unchanged so it propagates to the next connector.
		var items = l.Items;
		int code = 0;
		ListOperator? prev = null;
		foreach (var (node, op) in items)
			{
			bool run = prev switch
				{
				ListOperator.And => code == 0,
				ListOperator.Or  => code != 0,
				_                => true,   // Sequential / Background / start of list
				};
			if (run)
				{
				if (op == ListOperator.Background)
					{ StartBackgroundJob(node); code = 0; }
				else
					code = RunStatement(node);
				_env.LastExitCode = code;
				}
			prev = op;
			}
		return code;
		}

	// ── background jobs ─────────────────────────────────────────────────────────
	private readonly List<BackgroundJob> _jobs = [];
	private int _nextJobId = 1;

	private void StartBackgroundJob(Node node)
		{
		var job = new BackgroundJob { Id = _nextJobId++, Command = DescribeNode(node) };
		job.Thread = new Thread(() =>
			{
			try            { job.ExitCode = Execute(node); }
			catch (ExitException) { }
			catch          { job.ExitCode = 1; }
			finally        { job.Done = true; }
			}) { IsBackground = true };
		_jobs.Add(job);
		job.Thread.Start();
		Console.Error.WriteLine($"[{job.Id}] {job.Thread.ManagedThreadId}");
		}

	/// <summary>`jobs`: list active background jobs, then reap completed ones.</summary>
	public void ListJobs()
		{
		for (int i = 0; i < _jobs.Count; i++)
			{
			var j = _jobs[i];
			string mark = i == _jobs.Count - 1 ? "+" : i == _jobs.Count - 2 ? "-" : " ";
			Console.WriteLine($"[{j.Id}]{mark}  {(j.Done ? "Done" : "Running"),-8} {j.Command}");
			}
		_jobs.RemoveAll(j => j.Done);
		}

	/// <summary>`wait`/`fg`: join the given jobs (or all), return the last exit code.</summary>
	public int WaitJobs(IReadOnlyList<string> specs, bool report = false)
		{
		var targets = specs.Count == 0
			? _jobs.ToList()
			: _jobs.Where(j => specs.Select(ParseJobSpec).Contains(j.Id)).ToList();

		int code = _env.LastExitCode;
		foreach (var j in targets)
			{
			if (report) Console.WriteLine(j.Command);
			j.Thread.Join();
			code = j.ExitCode;
			}
		_jobs.RemoveAll(j => j.Done);
		return code;
		}

	public bool HasJobs => _jobs.Count > 0;

	private static int ParseJobSpec(string s)
		{
		if (s.StartsWith('%')) s = s[1..];
		return int.TryParse(s, out var n) ? n : -1;
		}

	/// <summary>Best-effort one-line label for a job/AST node (no expansion, no side effects).</summary>
	private static string DescribeNode(Node n) => n switch
		{
		SimpleCommand c => string.Join(" ",
			(c.Name is null ? [] : new[] { LiteralWord(c.Name) }).Concat(c.Args.Select(LiteralWord))),
		Pipeline p      => string.Join(" | ", p.Commands.Select(s => DescribeNode(s.Command))),
		List l          => string.Join(" ; ", l.Items.Select(it => DescribeNode(it.Pipeline))),
		_               => n.GetType().Name
		};

	private static string LiteralWord(Word w) => string.Concat(w.Parts.Select(p => p switch
		{
		LiteralPart l         => l.Value,
		SingleQuotedPart s    => $"'{s.Value}'",
		DoubleQuotedPart d    => $"\"{string.Concat(d.Parts.Select(LiteralPartText))}\"",
		VarExpansionPart v    => "$" + v.Name,
		BraceExpansionPart b  => "${" + b.Raw + "}",
		ArithmeticExpansionPart a => "$((" + a.Expression + "))",
		_                     => ""
		}));

	private static string LiteralPartText(WordPart p) => p switch
		{
		LiteralPart l      => l.Value,
		VarExpansionPart v => "$" + v.Name,
		BraceExpansionPart b => "${" + b.Raw + "}",
		_                  => ""
		};

	// Thread-local pipe stream overrides: set by pipeline threads so ExecExternal
	// can pass real pipe handles to child ProcessStartInfo instead of Console streams.
	[ThreadStatic] private static System.IO.Stream? _pipeStdin;
	[ThreadStatic] private static System.IO.Stream? _pipeStdout;

	// Raw byte sink mirroring Console.Out, for builtins that must emit bytes faithfully
	// (cat/head/tail/tee/cmp/od). Set by a file redirect; _capturing marks $(...) capture.
	[ThreadStatic] internal static System.IO.Stream? _rawStdout;
	[ThreadStatic] internal static bool _capturing;

	/// <summary>
	/// The raw byte stream a byte-oriented builtin should write stdout to, or null when
	/// output is being text-captured (command substitution) — in which case the caller
	/// should write decoded text to Console.Out instead. Precedence: capture, then file
	/// redirect, then pipeline stage, then the real terminal.
	/// </summary>
	public static System.IO.Stream? CurrentRawStdout()
		{
		if (_capturing) return null;
		if (_rawStdout is not null) return _rawStdout;
		if (_pipeStdout is not null) return _pipeStdout;
		return Console.OpenStandardOutput();
		}

	// ── pipeline ──────────────────────────────────────────────────────────────

	private int ExecPipeline(Pipeline p)
		{
		if (p.Commands.Count == 1)
			{
			int c = Execute(p.Commands[0].Command);
			return p.Negated ? (c == 0 ? 1 : 0) : c;
			}
		// All-in-process pipelines run sequentially with buffering: builtins use the
		// process-global Console, so running stages concurrently on threads makes 3+
		// builtin stages clobber each other's redirection. Sequential avoids that race.
		// Pipelines containing an external still use threads (externals need live pipe
		// handles to avoid buffer-fill deadlock, and don't touch Console).
		if (AllStagesInProcess(p)) return ExecPipelineSequential(p);
		return ExecPipelineViaThreads(p);
		}

	private bool AllStagesInProcess(Pipeline p)
		{
		foreach (var (node, _) in p.Commands)
			{
			if (node is not SimpleCommand sc) return false;          // compound → may spawn; use threads
			if (sc.Name is null) continue;                           // assignment-only — in-process
			if (sc.Name.Parts is [LiteralPart lit]
			    && (Builtins.Names.Contains(lit.Value) || _functions.ContainsKey(lit.Value)))
				continue;
			return false;                                            // external / non-literal name
			}
		return true;
		}

	/// <summary>Run an all-in-process pipeline stage-by-stage, buffering each stage's
	/// captured stdout as the next stage's stdin. No concurrency, so no Console race.</summary>
	private int ExecPipelineSequential(Pipeline p)
		{
		var stages = p.Commands;
		byte[]? input = null;
		int last = 0, worst = 0;
		for (int i = 0; i < stages.Count; i++)
			{
			var (node, stderrToo) = stages[i];
			bool isLast = i == stages.Count - 1;
			int code = ExecuteWithStdin(node, input, captureOutput: !isLast, stderrToo, out var captured);
			if (!isLast) input = System.Text.Encoding.UTF8.GetBytes(captured ?? "");
			last = code; worst = Math.Max(worst, code);
			}
		int result = Options.PipeFail ? worst : last;
		return p.Negated ? (result == 0 ? 1 : 0) : result;
		}

	/// <summary>
	/// Runs a multi-stage pipeline by executing each stage on its own thread,
	/// wiring them together with anonymous pipes so OS-level stdin/stdout flow
	/// between processes without buffering deadlocks.
	///
	/// For each stage transition we create a pipe pair:
	///   writeEnd (server, Out) → child stdout
	///   readEnd  (client, In)  → next stage's stdin
	///
	/// Builtins and compound commands run on threads with Console streams swapped.
	/// External processes receive the pipe handles via ProcessStartInfo.
	/// </summary>
	private int ExecPipelineViaThreads(Pipeline p)
		{
		var stages = p.Commands;
		int n = stages.Count;

		// Create n-1 pipe pairs between stages
		var writePipes = new System.IO.Pipes.AnonymousPipeServerStream[n - 1];
		var readPipes  = new System.IO.Pipes.AnonymousPipeClientStream[n - 1];
		for (int i = 0; i < n - 1; i++)
			{
			writePipes[i] = new System.IO.Pipes.AnonymousPipeServerStream(
				System.IO.Pipes.PipeDirection.Out, HandleInheritability.Inheritable);
			readPipes[i] = new System.IO.Pipes.AnonymousPipeClientStream(
				System.IO.Pipes.PipeDirection.In, writePipes[i].ClientSafePipeHandle);
			}

		var exitCodes = new int[n];
		var threads   = new Thread[n];

		for (int i = 0; i < n; i++)
			{
			int idx = i; // capture
			var (stageNode, stderrToo) = stages[idx];

			// Determine this stage's stdin/stdout pipes
			System.IO.Stream? stdinPipe  = idx == 0     ? null : readPipes[idx - 1];
			System.IO.Stream? stdoutPipe = idx == n - 1 ? null : writePipes[idx];

			threads[idx] = new Thread(() =>
				{
				// Set thread-local pipe streams so ExecExternal can use real handles
				_pipeStdin  = stdinPipe;
				_pipeStdout = stdoutPipe;

				// Swap Console streams for builtins/compound commands
				System.IO.TextReader? savedIn  = null;
				System.IO.TextWriter? savedOut = null;
				System.IO.TextWriter? savedErr = null;

				if (stdinPipe is not null)
					{
					savedIn = Console.In;
					Console.SetIn(new System.IO.StreamReader(stdinPipe));
					}
				if (stdoutPipe is not null)
					{
					savedOut = Console.Out;
					Console.SetOut(new System.IO.StreamWriter(stdoutPipe) { AutoFlush = true, NewLine = "\n" });
					if (stderrToo)
						{
						savedErr = Console.Error;
						Console.SetError(new System.IO.StreamWriter(stdoutPipe) { AutoFlush = true, NewLine = "\n" });
						}
					}

				try
					{
					exitCodes[idx] = Execute(stageNode);
					}
				catch (ExitException ex) { exitCodes[idx] = ex.Code; }
				catch { exitCodes[idx] = 1; }
				finally
					{
					// Restore Console streams, then close the write-end pipe so the
					// next stage sees EOF. For external processes, the drain thread
					// also tries to close — both are wrapped in try/catch so that's safe.
					if (savedOut is not null)
						{
						var writer = Console.Out;
						Console.SetOut(savedOut);
						try { writer.Flush(); } catch { }
						}
					if (savedErr is not null)
						Console.SetError(savedErr);
					if (savedIn is not null)
						Console.SetIn(savedIn);
					// Close write end so downstream stage sees EOF
					if (stdoutPipe is not null)
						try { stdoutPipe.Close(); } catch { }
					}
				});
			threads[idx].IsBackground = true;
			}

		// Start all threads
		for (int i = 0; i < n; i++)
			threads[i].Start();

		// Wait for all
		for (int i = 0; i < n; i++)
			threads[i].Join();

		// Dispose pipes
		for (int i = 0; i < n - 1; i++)
			{
			try { writePipes[i].Dispose(); } catch { }
			try { readPipes[i].Dispose();  } catch { }
			}

		int worstCode = exitCodes.Max();
		int lastCode  = exitCodes[n - 1];
		int result    = Options.PipeFail ? worstCode : lastCode;
		return p.Negated ? (result == 0 ? 1 : 0) : result;
		}

	private int ExecuteWithStdin(Node node, byte[]? stdinData, bool captureOutput,
		bool stderrToo, out string? captured)
		{
		captured = null;

		System.IO.TextReader? oldIn  = null;
		System.IO.TextWriter? oldOut = null;
		System.IO.StringWriter? capWriter = null;

		if (stdinData != null)
			{
			oldIn = Console.In;
			Console.SetIn(new System.IO.StreamReader(new System.IO.MemoryStream(stdinData)));
			}
		bool oldCap = _capturing;
		if (captureOutput)
			{
			oldOut = Console.Out;
			capWriter = new System.IO.StringWriter { NewLine = "\n" };
			Console.SetOut(capWriter);
			if (stderrToo) Console.SetError(capWriter);
			_capturing = true;
			}

		int code;
		try { code = Execute(node); }
		finally
			{
			if (oldIn  != null) Console.SetIn(oldIn);
			if (oldOut != null) { Console.SetOut(oldOut); captured = capWriter!.ToString(); _capturing = oldCap; }
			}
		return code;
		}

	private int ExecArrayElementAssign(ArrayElementAssign ae)
		{
		var idxStr = _expander.ExpandToString(ae.Index);
		var val    = _expander.ExpandToString(ae.Value);

		if (_env.IsAssoc(ae.ArrayName))
			_env.SetAssocElement(ae.ArrayName, idxStr, val);
		else
			{
			long idx = ArithParser.Evaluate(_expander.ExpandArith(idxStr));
			_env.SetArrayElement(ae.ArrayName, (int)idx, val);
			}
		return 0;
		}

	private int ExecArrayCompoundAssign(ArrayCompoundAssign ac)
		{
		var values = ac.Values.SelectMany(w => _expander.ExpandToFields(w)).ToList();
		_env.SetArrayFromList(ac.ArrayName, values);
		return 0;
		}

	// ── simple command ────────────────────────────────────────────────────────

	private int ExecSimpleCommand(SimpleCommand cmd)
		{
		// Apply temporary variable assignments
		var tempAssign = new List<(string, string)>();
		foreach (var (name, valWord) in cmd.Assignments)
			{
			var val = _expander.ExpandToString(valWord);
			if (cmd.Name is null)
				_env.Set(name, val);
			else
				tempAssign.Add((name, val));
			}

		if (cmd.Name is null)
			return 0;

		var namStr = _expander.ExpandToString(cmd.Name);

		// -u: error on unset variable (already handled in ShellEnvironment.Get for strict mode;
		//     here we just check the command name resolved to something)
		var args = new List<string>();
		foreach (var argWord in cmd.Args)
			{
			// A word almost always expands to one field — Add directly rather than
			// AddRange (avoids the IEnumerable/ICollection/CopyTo path). Marginal win
			// in practice; the real per-iteration cost is allocation in ExpandToFields.
			var fields = _expander.ExpandToFields(argWord);
			if (fields.Count == 1) args.Add(fields[0]);
			else                   args.AddRange(fields);
			}

		// -x: xtrace
		if (Options.XTrace)
			Console.Error.WriteLine($"+ {namStr} {string.Join(" ", args)}".TrimEnd());

		// Redirects are applied in exactly one place per command type. Builtins and
		// functions run in-process, so they take ApplyRedirects' Console swap. External
		// processes write to OS handles that a Console swap can't reach (and opening the
		// same file twice throws a sharing violation), so ExecExternal owns their fd-map.
		if (Builtins.Has(namStr) || _functions.ContainsKey(namStr))
			{
			using var redirectScope = ApplyRedirects(cmd.Redirects);
			if (_builtins.TryExecute(namStr, args, out int builtinCode))
				return builtinCode;
			return ExecFunction(_functions[namStr], args);
			}

		return ExecExternal(namStr, args, tempAssign, cmd.Redirects);
		}

	// PATH-resolution cache for external commands — backs the `hash` builtin's table.
	private readonly Dictionary<string, string> _pathHash = [];

	public void HashClear() => _pathHash.Clear();
	public void HashSetPath(string name, string path) => _pathHash[name] = path;
	public IReadOnlyDictionary<string, string> HashTable => _pathHash;

	/// <summary>Resolve a bare command name against PATH (+PATHEXT on Windows). Returns the
	/// full path (cached on success), or null if it already contains a path separator or
	/// isn't found — callers then fall back to the OS launcher's own PATH search.</summary>
	public string? ResolveOnPath(string name)
		{
		if (name.Length == 0 || name.Contains('/') || name.Contains('\\')) return null;
		if (_pathHash.TryGetValue(name, out var hit)) return hit;
		var pathExt = (Environment.GetEnvironmentVariable("PATHEXT") ?? ".COM;.EXE;.BAT;.CMD")
			.Split(';', StringSplitOptions.RemoveEmptyEntries);
		var dirs = (Environment.GetEnvironmentVariable("PATH") ?? "")
			.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries);
		bool hasExt = Path.HasExtension(name);
		foreach (var d in dirs)
			{
			try
				{
				var bare = Path.Combine(d, name);
				if (File.Exists(bare)) { _pathHash[name] = bare; return bare; }
				if (!hasExt)
					foreach (var ext in pathExt)
						{
						var p = Path.Combine(d, name + ext);
						if (File.Exists(p)) { _pathHash[name] = p; return p; }
						}
				}
			catch { }
			}
		return null;
		}

	/// <summary>Run a single command (name + already-expanded args) through the normal
	/// builtin → function → external dispatch, with no redirects. Used by builtins that
	/// invoke other commands (e.g. <c>xargs</c>). Output goes to the current Console/pipe.</summary>
	public int RunCommand(string name, List<string> args)
		{
		if (_builtins.TryExecute(name, args, out int code))
			return code;
		if (_functions.TryGetValue(name, out var funcBody))
			return ExecFunction(funcBody, args);
		return ExecExternal(name, args, [], []);
		}

	// ── external process ──────────────────────────────────────────────────────

	/// <summary>
	/// True if <paramref name="name"/> names an existing script file we should run via
	/// an interpreter rather than CreateProcess: a file beginning with <c>#!</c>, or a
	/// <c>.sh</c> file. Returns the resolved path and the shebang text (after <c>#!</c>,
	/// empty when there's no shebang). Native executables (no #!, not .sh) return false
	/// so normal process launch handles them.
	/// </summary>
	private static bool TryResolveScriptFile(string name, out string path, out string shebang)
		{
		path = ""; shebang = "";
		var p = ShellEnvironment.TranslatePath(name);
		if (!File.Exists(p)) return false;

		string first;
		try { using var sr = new System.IO.StreamReader(p); first = sr.ReadLine() ?? ""; }
		catch { return false; }

		bool hasShebang = first.StartsWith("#!");
		bool isShExt    = p.EndsWith(".sh", StringComparison.OrdinalIgnoreCase);
		if (!hasShebang && !isShExt) return false;

		path    = p;
		shebang = hasShebang ? first[2..].Trim() : "";
		return true;
		}

	/// <summary>Resolve a shebang body to (interpreter, extra-args), honouring
	/// <c>/usr/bin/env CMD</c> (where the real interpreter is CMD).</summary>
	private static (string interp, List<string> args) ResolveInterpreter(string shebang)
		{
		if (shebang.Length == 0) return ("", []);
		var parts = shebang.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
		if (Path.GetFileName(parts[0]) == "env" && parts.Length > 1)
			return (parts[1], [.. parts[2..]]);
		return (parts[0], [.. parts[1..]]);
		}

	/// <summary>Run a bash/sh script in a fresh child shell (approximates a forked
	/// subprocess: inherits exported vars, isolated mutations). <c>exit</c> ends the
	/// script, not us.</summary>
	private int RunScriptInProcess(string arg0, string path, List<string> args)
		{
		var child = new Evaluator();
		foreach (var (k, v) in _env.GetExportedVars()) { child._env.Set(k, v); child._env.Export(k); }
		child._env.SetArg0(arg0);
		child._env.SetPositionals(args);
		try   { return child.RunString(File.ReadAllText(path)); }
		catch (ExitException ex) { return ex.Code; }
		}

	private int ExecExternal(string name, List<string> args,
		List<(string, string)> tempAssign, List<Redirect> redirects)
		{
		// Shebang dispatch: Windows cannot exec a #! script directly. When the command
		// names an actual script file, read its interpreter line and run it ourselves —
		// in-process when it's bash/sh (we are the shell), spawning otherwise.
		if (TryResolveScriptFile(name, out var scriptPath, out var shebang))
			{
			var (interp, interpArgs) = ResolveInterpreter(shebang);
			var ib = Path.GetFileNameWithoutExtension(interp).ToLowerInvariant();
			if (ib is "bash" or "sh" or "")
				return RunScriptInProcess(name, scriptPath, args);
			// External interpreter: <interp> [shebang args] <script> <args...>,
			// using the basename so PATH resolves it (Unix interpreter paths won't exist).
			var spawn = new List<string>(interpArgs) { scriptPath };
			spawn.AddRange(args);
			return ExecExternal(Path.GetFileName(interp), spawn, tempAssign, redirects);
			}

		var psi = new ProcessStartInfo
			{
			FileName         = ResolveOnPath(name) ?? name,   // cached full path, else OS resolves
			UseShellExecute  = false,
			WorkingDirectory = Directory.GetCurrentDirectory(),
			};

		foreach (var arg in args)
			psi.ArgumentList.Add(arg);

		foreach (var (k, v) in _env.GetExportedVars())
			psi.Environment[k] = v;
		foreach (var (k, v) in tempAssign)
			psi.Environment[k] = v;

		// Resolve redirects into a simple fd-map:
		//   fd 0 → null (inherit) | string path (file) | "pipe" (managed pipe)
		//   fd 1 → same
		//   fd 2 → same | "1" (dup to stdout)
		// We process left-to-right as bash does.

		string? stdin_path  = null;  bool stdin_devnull  = false;
		string? stdout_path = null;  bool stdout_append  = false, stdout_devnull = false;
		string? stderr_path = null;  bool stderr_append  = false, stderr_devnull = false;
		bool    stderr_to_stdout = false; // 2>&1
		bool    stdout_to_stderr = false; // 1>&2

		foreach (var r in redirects)
			{
			var raw    = _expander.ExpandToString(r.Target);
			var target = ShellEnvironment.TranslatePath(raw);
			int fd = r.Fd ?? (r.Kind is RedirectKind.Input or RedirectKind.InputDup or
			                            RedirectKind.Heredoc or RedirectKind.HeredocStrip ? 0 : 1);
			switch (r.Kind)
				{
				case RedirectKind.Input:
					if (raw == "/dev/null") { stdin_devnull = true; stdin_path = null; }
					else                    { stdin_path = target; stdin_devnull = false; }
					break;

				case RedirectKind.Output:
				case RedirectKind.Clobber:
				case RedirectKind.Append:
					{
					bool append = r.Kind == RedirectKind.Append;
					if (fd == 2)
						{
						stderr_to_stdout = false; stderr_path = null; stderr_devnull = false;
						if      (raw == "/dev/null")   stderr_devnull   = true;
						else if (raw == "/dev/stdout") stderr_to_stdout = true;
						else if (raw == "/dev/stderr") { /* stderr → stderr: inherit */ }
						else { stderr_path = target; stderr_append = append; }
						}
					else
						{
						stdout_to_stderr = false; stdout_path = null; stdout_devnull = false;
						if      (raw == "/dev/null")   stdout_devnull   = true;
						else if (raw == "/dev/stderr") stdout_to_stderr = true;
						else if (raw == "/dev/stdout") { /* stdout → stdout: inherit */ }
						else { stdout_path = target; stdout_append = append; }
						}
					break;
					}

				case RedirectKind.OutputDup:
					if (fd == 2 && raw == "1") { stderr_to_stdout = true; stderr_path = null; stderr_devnull = false; }
					if (fd == 1 && raw == "2") { stdout_to_stderr = true; stdout_path = null; stdout_devnull = false; }
					break;
				}
			}

		// Configure psi based on resolved fd-map
		System.IO.FileStream? stdinFs  = null;
		System.IO.Stream?     stdoutFs = null;   // file or Stream.Null
		System.IO.Stream?     stderrFs = null;   // file or Stream.Null

		// Pipeline overrides take precedence over file redirects for stdin/stdout
		bool hasPipeStdin  = _pipeStdin  is not null && stdin_path is null && !stdin_devnull;
		bool hasPipeStdout = _pipeStdout is not null && stdout_path is null && !stdout_devnull && !stdout_to_stderr;

		if (hasPipeStdin)
			psi.RedirectStandardInput = true;
		else if (stdin_devnull)
			psi.RedirectStandardInput = true;     // close immediately → EOF
		else if (stdin_path is not null)
			{
			psi.RedirectStandardInput = true;
			stdinFs = File.OpenRead(stdin_path);
			}

		if (hasPipeStdout)
			psi.RedirectStandardOutput = true;
		else if (stdout_devnull)
			{ psi.RedirectStandardOutput = true; stdoutFs = System.IO.Stream.Null; }
		else if (stdout_path is not null)
			{
			psi.RedirectStandardOutput = true;
			stdoutFs = stdout_append
				? File.Open(stdout_path, FileMode.Append, FileAccess.Write)
				: File.Create(stdout_path);
			}
		else if (stdout_to_stderr)
			psi.RedirectStandardOutput = true;

		// stderr destination (independent of stdout)
		if (stderr_devnull)
			{ psi.RedirectStandardError = true; stderrFs = System.IO.Stream.Null; }
		else if (stderr_path is not null)
			{
			psi.RedirectStandardError = true;
			stderrFs = stderr_append
				? File.Open(stderr_path, FileMode.Append, FileAccess.Write)
				: File.Create(stderr_path);
			}
		else if (stderr_to_stdout && (stdout_path is not null || stdout_devnull || hasPipeStdout))
			psi.RedirectStandardError = true;   // follows stdout's final destination
		// else (stderr_to_stdout with inherited stdout): both inherit the console — nothing to redirect

		Process proc;
		try
			{
			proc = Process.Start(psi)
				?? throw new EvalException($"{name}: failed to start");
			}
		catch (Exception ex) when (ex is not EvalException)
			{
			Console.Error.WriteLine($"bash: {name}: {ex.Message}");
			stdinFs?.Dispose(); stdoutFs?.Dispose();
			return 127;
			}

		try
			{
			// Feed stdin
			if (hasPipeStdin)
				{
				var src = _pipeStdin!;
				var t = new Thread(() => { src.CopyTo(proc.StandardInput.BaseStream); proc.StandardInput.Close(); });
				t.IsBackground = true;
				t.Start();
				}
			else if (stdin_devnull)
				proc.StandardInput.Close();
			else if (stdinFs is not null)
				{
				stdinFs.CopyTo(proc.StandardInput.BaseStream);
				proc.StandardInput.Close();
				}

			Thread? stdoutThread = null;
			Thread? stderrThread = null;

			// Drain stdout: pipe → next stage, file/devnull → stream, 1>&2 → console stderr.
			if (hasPipeStdout)
				{
				var dest = _pipeStdout!;
				stdoutThread = new Thread(() =>
					{
					proc.StandardOutput.BaseStream.CopyTo(dest);
					dest.Flush();
					// Signal EOF to next stage by closing the write end.
					try { dest.Close(); } catch { }
					});
				stdoutThread.IsBackground = true;
				stdoutThread.Start();
				}
			else if (stdoutFs is not null)
				{
				var dest = stdoutFs;
				stdoutThread = new Thread(() => proc.StandardOutput.BaseStream.CopyTo(dest));
				stdoutThread.IsBackground = true;
				stdoutThread.Start();
				}
			else if (stdout_to_stderr && psi.RedirectStandardOutput)
				{
				stdoutThread = new Thread(() =>
					{
					string? line;
					while ((line = proc.StandardOutput.ReadLine()) is not null)
						Console.Error.WriteLine(line);
					});
				stdoutThread.IsBackground = true;
				stdoutThread.Start();
				}

			// Drain stderr: file/devnull → stream, 2>&1 → stdout's destination.
			if (psi.RedirectStandardError)
				{
				System.IO.Stream? errDest =
					stderrFs is not null ? stderrFs
					: stderr_to_stdout   ? (stdoutFs ?? (hasPipeStdout ? _pipeStdout : null))
					: null;
				if (errDest is not null)
					{
					var dest = errDest;
					stderrThread = new Thread(() => proc.StandardError.BaseStream.CopyTo(dest));
					stderrThread.IsBackground = true;
					stderrThread.Start();
					}
				}

			proc.WaitForExit();
			stdoutThread?.Join();
			stderrThread?.Join();
			return proc.ExitCode;
			}
		finally
			{
			stdinFs?.Dispose();
			(stdoutFs as IDisposable)?.Dispose();
			(stderrFs as IDisposable)?.Dispose();
			}
		}

	// ── redirects ─────────────────────────────────────────────────────────────

	private RedirectScope ApplyRedirects(List<Redirect> redirects)
		{
		var scope = new RedirectScope();
		foreach (var r in redirects)
			{
			var rawTarget = _expander.ExpandToString(r.Target);
			var target    = ShellEnvironment.TranslatePath(rawTarget);
			int fd        = r.Fd ?? 1;
			switch (r.Kind)
				{
				case RedirectKind.Output:
				case RedirectKind.Clobber:
				case RedirectKind.Append:
					{
					bool append = r.Kind == RedirectKind.Append;
					// /dev specials
					if (rawTarget == "/dev/null")
						{
						if (fd == 2) { scope.SaveErr(); Console.SetError(System.IO.TextWriter.Null); }
						else { scope.SaveOut(); scope.SaveRaw(); Console.SetOut(System.IO.TextWriter.Null); _rawStdout = System.IO.Stream.Null; }
						break;
						}
					if (rawTarget == "/dev/stdout") { if (fd == 2) { scope.SaveErr(); Console.SetError(Console.Out); } break; }
					if (rawTarget == "/dev/stderr") { if (fd != 2) { scope.SaveOut(); Console.SetOut(Console.Error); } break; }

					System.IO.FileStream fs;
					try { fs = append ? File.Open(target, FileMode.Append, FileAccess.Write) : File.Create(target); }
					catch (Exception ex) { scope.Dispose(); throw new EvalException($"{rawTarget}: {ex.Message}"); }
					var w = new System.IO.StreamWriter(fs) { AutoFlush = true, NewLine = "\n" };
					scope.TrackInstalled(w);
					if (fd == 2) { scope.SaveErr(); Console.SetError(w); }              // 2>file → stderr
					else         { scope.SaveOut(); scope.SaveRaw(); Console.SetOut(w); _rawStdout = fs; }
					break;
					}
				case RedirectKind.Input:
					{
					scope.SaveIn();
					if (rawTarget == "/dev/null") { Console.SetIn(new System.IO.StringReader("")); break; }
					System.IO.StreamReader reader;
					try { reader = new System.IO.StreamReader(File.OpenRead(target)); }
					catch (Exception ex) { scope.Dispose(); throw new EvalException($"{rawTarget}: {ex.Message}"); }
					scope.TrackInstalled(reader);
					Console.SetIn(reader);
					break;
					}
				case RedirectKind.OutputDup when (r.Fd ?? 1) == 1 && target == "2":
					// 1>&2 — stdout follows stderr's current destination
					scope.SaveOut();
					Console.SetOut(Console.Error);
					break;
				case RedirectKind.OutputDup when (r.Fd ?? 1) == 2 && target == "1":
					// 2>&1 — stderr follows stdout's current destination. (Parsed as
					// OutputDup, fd 2, target "1"; the old InputDup case never matched.)
					scope.SaveErr();
					Console.SetError(Console.Out);
					break;
				case RedirectKind.InputDup when (r.Fd ?? 2) == 2 && target == "1":
					// (defensive) 2>&1 expressed via <& — same effect
					scope.SaveErr();
					Console.SetError(Console.Out);
					break;
				}
			}
		return scope;
		}

	// ── compound commands ─────────────────────────────────────────────────────

	private int ExecWithRedirects(Node body, List<Redirect> redirects)
		{
		using var scope = ApplyRedirects(redirects);
		return Execute(body);
		}

	private int ExecSubshell(Subshell ss)
		{
		// True subshell isolation requires a child process; for now run in-process
		// with a cloned environment frame.
		_env.PushScope([]);
		try   { return ExecWithRedirects(ss.Body, ss.Redirects); }
		finally { _env.PopScope(); }
		}

	private int ExecFunctionDef(FunctionDef fd)
		{
		_functions[fd.Name] = fd.Body;
		return 0;
		}

	private int ExecFunction(Node body, List<string> args)
		{
		_env.PushScope(args.ToArray());
		try   { return Execute(body); }
		catch (ReturnException r) { return r.Code; }
		finally { _env.PopScope(); }
		}

	private int ExecIf(IfCommand ic)
		{
		using var scope = ApplyRedirects(ic.Redirects);
		if (Execute(ic.Condition) == 0)
			return Execute(ic.Then);
		foreach (var (cond, body) in ic.Elifs)
			if (Execute(cond) == 0)
				return Execute(body);
		return ic.Else is not null ? Execute(ic.Else) : 0;
		}

	private int ExecWhile(WhileCommand wc)
		{
		using var scope = ApplyRedirects(wc.Redirects);
		int code = 0;
		while (true)
			{
			CheckInterrupt();
			int condCode = Execute(wc.Condition);
			bool condMet = wc.Until ? condCode != 0 : condCode == 0;
			if (!condMet) break;
			try   { code = Execute(wc.Body); }
			catch (BreakException b)    { if (b.Levels <= 1) break;    throw new BreakException(b.Levels - 1); }
			catch (ContinueException c) { if (c.Levels <= 1) continue; throw new ContinueException(c.Levels - 1); }
			}
		return code;
		}

	private int ExecFor(ForCommand fc)
		{
		using var scope = ApplyRedirects(fc.Redirects);
		var words = fc.Words.Count > 0
			? fc.Words.SelectMany(_expander.ExpandToFields).ToList()
			: _env.Get("@").Split(' ').ToList();

		int code = 0;
		foreach (var word in words)
			{
			CheckInterrupt();
			_env.Set(fc.Variable, word);
			try   { code = Execute(fc.Body); }
			catch (BreakException b)    { if (b.Levels <= 1) break;    throw new BreakException(b.Levels - 1); }
			catch (ContinueException c) { if (c.Levels <= 1) continue; throw new ContinueException(c.Levels - 1); }
			}
		return code;
		}

	private int ExecCase(CaseCommand cc)
		{
		using var scope = ApplyRedirects(cc.Redirects);
		var subject = _expander.ExpandToString(cc.Subject);
		foreach (var item in cc.Items)
			{
			foreach (var pat in item.Patterns)
				{
				var patStr = _expander.ExpandToString(pat);
				if (GlobMatch(patStr, subject))
					return item.Body is not null ? Execute(item.Body) : 0;
				}
			}
		return 0;
		}

	// ── [[ ]] ─────────────────────────────────────────────────────────────────

	private int ExecConditional(ConditionalExpression ce) =>
		EvalCondExpr(ce.Expr) ? 0 : 1;

	private bool EvalCondExpr(CondExpr expr) => expr switch
		{
		CondAnd a    => EvalCondExpr(a.Left) && EvalCondExpr(a.Right),
		CondOr  o    => EvalCondExpr(o.Left) || EvalCondExpr(o.Right),
		CondNot n    => !EvalCondExpr(n.Operand),
		CondUnary u  => EvalCondUnary(u.Op, _expander.ExpandToString(u.Operand)),
		CondBinary b => EvalCondBinary(b.Op,
			_expander.ExpandToString(b.Left),
			_expander.ExpandToString(b.Right)),
		CondWord w   => _expander.ExpandToString(w.Value).Length > 0,
		_ => false
		};

	private static bool EvalCondUnary(string op, string val) => op switch
		{
		"-z" => val.Length == 0,
		"-n" => val.Length > 0,
		"-f" => File.Exists(val),
		"-d" => Directory.Exists(val),
		"-e" => File.Exists(val) || Directory.Exists(val),
		"-r" => File.Exists(val),
		"-w" => File.Exists(val),
		"-x" => File.Exists(val),
		"-s" => File.Exists(val) && new FileInfo(val).Length > 0,
		"-L" => false, // symlinks — TODO
		_ => false
		};

	private static bool EvalCondBinary(string op, string left, string right)
		{
		bool numOp = long.TryParse(left, out var l) & long.TryParse(right, out var r);
		return op switch
			{
			"="  or "==" => left == right,
			"!="         => left != right,
			"<"          => string.Compare(left, right, StringComparison.Ordinal) < 0,
			">"          => string.Compare(left, right, StringComparison.Ordinal) > 0,
			"=~"         => System.Text.RegularExpressions.Regex.IsMatch(left, right),
			"-eq"        => numOp && l == r,
			"-ne"        => numOp && l != r,
			"-lt"        => numOp && l <  r,
			"-le"        => numOp && l <= r,
			"-gt"        => numOp && l >  r,
			"-ge"        => numOp && l >= r,
			_            => false
			};
		}

	// ── glob matching (case patterns + basic filename glob) ───────────────────

	public static bool GlobMatch(string pattern, string input)
		{
		// Translate shell glob to regex: * → .*, ? → ., [...] pass through
		if (pattern == "*") return true;
		var sb = new System.Text.StringBuilder("^");
		foreach (char c in pattern)
			{
			switch (c)
				{
				case '*': sb.Append(".*"); break;
				case '?': sb.Append('.');  break;
				case '.': sb.Append("\\."); break;
				default:  sb.Append(System.Text.RegularExpressions.Regex.Escape(c.ToString())); break;
				}
			}
		sb.Append('$');
		return System.Text.RegularExpressions.Regex.IsMatch(input,
			sb.ToString(), System.Text.RegularExpressions.RegexOptions.None);
		}
	}

// ── redirect scope RAII ───────────────────────────────────────────────────────

internal sealed class RedirectScope : IDisposable
	{
	private System.IO.TextWriter? _savedOut;
	private System.IO.TextWriter? _savedErr;
	private System.IO.TextReader? _savedIn;
	private System.IO.Stream? _savedRaw;
	private bool _rawSaved;

	// The writers/readers we installed — must be disposed to release file handles.
	private readonly List<IDisposable> _installed = [];

	public void SaveOut() { _savedOut ??= Console.Out; }
	public void SaveErr() { _savedErr ??= Console.Error; }
	public void SaveIn()  { _savedIn  ??= Console.In; }
	public void SaveRaw() { if (!_rawSaved) { _savedRaw = Evaluator._rawStdout; _rawSaved = true; } }

	public void TrackInstalled(IDisposable d) => _installed.Add(d);
	public IDisposable LastInstalled => _installed[^1];

	public void Dispose()
		{
		if (_savedOut != null)
			{
			var current = Console.Out;
			Console.SetOut(_savedOut);
			_savedOut = null;
			try { current.Flush(); } catch { }
			}
		if (_savedErr != null)
			{
			Console.SetError(_savedErr);
			_savedErr = null;
			}
		if (_savedIn != null)
			{
			Console.SetIn(_savedIn);
			_savedIn = null;
			}
		if (_rawSaved)
			{
			Evaluator._rawStdout = _savedRaw;
			_savedRaw = null; _rawSaved = false;
			}
		// Dispose file streams after restoring Console — order matters so
		// the Flush above goes to the real console, not the file.
		foreach (var d in _installed)
			try { d.Dispose(); } catch { }
		_installed.Clear();
		}
	}
