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

	// User-defined functions: name → definition (body + verbatim source for declare -f)
	private readonly Dictionary<string, FunctionDef> _functions = new(StringComparer.Ordinal);

	/// <summary>Alias table (`alias`/`unalias`); expanded only under `shopt -s expand_aliases`.</summary>
	public Dictionary<string, string> Aliases { get; } = new(StringComparer.Ordinal);

	/// <summary>The shell environment (exposed for the line editor / completion).</summary>
	public ShellEnvironment Env => _env;

	/// <summary>The word expander (arithmetic, patterns) for builtins that need it.</summary>
	public WordExpander Expander => _expander;

	/// <summary>Names of currently-defined functions (for tab completion).</summary>
	public IEnumerable<string> FunctionNames => _functions.Keys;
	public FunctionDef? GetFunction(string name) => _functions.TryGetValue(name, out var f) ? f : null;
	public bool RemoveFunction(string name) => _functions.Remove(name);
	public bool HasFunction(string name) => _functions.ContainsKey(name);

	public static readonly HashSet<string> Keywords =
		["if", "then", "else", "elif", "fi", "for", "while", "until", "do", "done", "case", "esac",
		 "in", "function", "select", "time", "{", "}", "[[", "]]", "!", "coproc"];

	// Call stacks that back $FUNCNAME and $BASH_SOURCE
	private readonly List<string> _funcStack = [];
	private readonly List<string> _sourceStack = [];

	// Redirects made permanent by `exec >file` (kept alive on purpose)
	private readonly List<RedirectScope> _permanentScopes = [];

	// File descriptors 3+ opened by `exec 3>file` / `cmd 3<file` for in-process commands
	private readonly Dictionary<int, System.IO.Stream> _fds = [];
	public System.IO.Stream? GetFd(int fd) => _fds.TryGetValue(fd, out var s) ? s : null;

	// errexit is suppressed inside && / || lists, if/while conditions, and negations
	private int _errexitSuppress;

	// The background job a thread belongs to (so ExecExternal can record the child pid)
	[ThreadStatic] private static BackgroundJob? _currentJob;

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
		_env.FlagsProvider = () => Options.FlagString();
		_env.ArithEval     = e => _expander.EvalArithmetic(e);
		}

	// ── public entry point ────────────────────────────────────────────────────

	/// <summary>Execute a node and return its exit code.</summary>
	public int Execute(Node node)
		{
		// `<( )` temp files created while expanding this node's words/redirects live
		// until the node has finished (DECISIONS 2026-09-04 #6)
		int mark = WordExpander.ProcSubMark();
		try { return ExecuteCore(node); }
		finally { WordExpander.ReleaseProcSubs(mark); }
		}

	private bool _inErrTrap;

	private int ExecuteCore(Node node)
		{
		if (Options.NoExec) return 0;
		if (node.Line > 0) _env.CurrentLine = node.Line;

		int code = node switch
			{
			Script s                  => ExecScript(s),
			List l                    => ExecList(l),
			Pipeline p                => ExecPipeline(p),
			SimpleCommand cmd         => ExecSimpleCommand(cmd),
			ArrayElementAssign ae     => ExecArrayElementAssign(ae),
			ArrayCompoundAssign ac    => ExecArrayCompoundAssign(ac),
			ArithmeticCommand am      => ExecArithmeticCommand(am),
			BraceGroup bg             => ExecWithRedirects(bg.Body, bg.Redirects),
			Subshell ss               => ExecSubshell(ss),
			IfCommand ic              => ExecIf(ic),
			WhileCommand wc           => ExecWhile(wc),
			ForCommand fc             => ExecFor(fc),
			ArithForCommand af        => ExecArithFor(af),
			CaseCommand cc            => ExecCase(cc),
			FunctionDef fd            => ExecFunctionDef(fd),
			ConditionalExpression ce  => ExecConditional(ce),
			_ => throw new EvalException($"Unhandled node type: {node.GetType().Name}")
			};

		_env.LastExitCode = code;

		// trap ERR: after a failing command/pipeline, under the same exemptions as errexit
		// (not inside && / || / ! / if-while conditions), never re-entrantly
		if (code != 0 && _errexitSuppress == 0 && !_inErrTrap
		    && node is SimpleCommand or Pipeline or ConditionalExpression or ArithmeticCommand
		    && node is not Pipeline { Negated: true }          // `! cmd` is exempt, as for errexit
		    && (_funcStack.Count == 0 || Options.ErrTrace)   // not inherited by functions unless -E
		    && _traps.TryGetValue("ERR", out var errCmd) && errCmd.Length > 0)
			{
			_inErrTrap = true;
			try { RunTrap(errCmd); }
			finally { _inErrTrap = false; _env.LastExitCode = code; }
			}

		if (Options.ExitOnError && code != 0 && _errexitSuppress == 0
		    && node is not IfCommand and not WhileCommand and not ForCommand and not ArithForCommand
		    and not Script and not List)
			throw new ExitException(code);

		return code;
		}

	/// <summary>Lex, parse, and execute a source string. Re-throws ExitException so
	/// callers can honour `exit`; a syntax error is reported as
	/// `bash: origin: line N: message` and yields exit code 2 (bash's convention); a
	/// runtime error yields 1.</summary>
	public int RunString(string source, bool reportErrors = true, string origin = "bash", int syntaxErrorCode = 2)
		{
		try
			{
			var tokens = new Lexer.Lexer(source).Tokenize();
			var ast    = new Parser.Parser(tokens, source).Parse();
			return Execute(ast);
			}
		catch (ExitException) { throw; }
		catch (Lexer.LexException ex)   { if (reportErrors) Console.Error.WriteLine($"bash: {origin}: line {ex.Line}: {ex.Message}"); _env.LastExitCode = syntaxErrorCode; return syntaxErrorCode; }
		catch (Parser.ParseException ex){ if (reportErrors) Console.Error.WriteLine($"bash: {origin}: line {ex.Line}: {ex.Message}"); _env.LastExitCode = syntaxErrorCode; return syntaxErrorCode; }
		catch (FatalShellException ex)  { if (reportErrors) Console.Error.WriteLine($"bash: {ex.Message}"); _env.LastExitCode = ex.Code; return ex.Code; }
		catch (EvalException ex)        { if (reportErrors) Console.Error.WriteLine($"bash: {ex.Message}"); _env.LastExitCode = 1; return 1; }
		// Last-resort guard (2026-09-11): a filesystem/permission failure anywhere in the evaluator
		// is reported as a shell error, never an unhandled exception that kills the process and
		// loses the script's prior output. BrokenPipeException is excluded — it derives from
		// IOException and carries the SIGPIPE semantics the pipeline model needs.
		catch (Exception ex) when (ex is not BrokenPipeException
		                            && ex is IOException or UnauthorizedAccessException
		                                or NotSupportedException or System.Security.SecurityException)
			{ if (reportErrors) Console.Error.WriteLine($"bash: {origin}: {ex.Message}"); _env.LastExitCode = 1; return 1; }
		}

	/// <summary>Source a file (startup files, BASH_ENV, `source`/`.`). When silentIfMissing
	/// is set, a non-existent file is a no-op returning 0. A syntax error inside the file
	/// is contained: it fails the `source` command (status 2) and the caller continues.</summary>
	public int SourceFile(string path, bool silentIfMissing = false, IEnumerable<string>? positionals = null)
		{
		var translated = ShellEnvironment.TranslatePath(path);
		if (!File.Exists(translated))
			{
			if (silentIfMissing) return 0;
			Console.Error.WriteLine($"bash: {path}: No such file or directory");
			return 1;
			}
		_sourceStack.Insert(0, path);
		_env.SetArrayFromList("BASH_SOURCE", [.. _sourceStack]);
		List<string>? savedPos = null;
		if (positionals is not null) { savedPos = _env.GetPositionals(); _env.SetPositionals(positionals); }
		int savedLine = _env.CurrentLine;
		try
			{
			try { return RunString(ShellEncoding.ReadAllText(translated), origin: path); }
			catch (ReturnException r) { return r.Code; }      // `return` at file level ends the source
			// a locked/blocked/vanished source file is an error, not a dead shell (2026-09-12)
			catch (Exception ex) when (ex is IOException and not BrokenPipeException
			                            or UnauthorizedAccessException or NotSupportedException
			                            or System.Security.SecurityException)
				{ Console.Error.WriteLine($"bash: {path}: {ex.Message}"); return 1; }
			}
		finally
			{
			_env.CurrentLine = savedLine;
			if (savedPos is not null) _env.SetPositionals(savedPos);
			_sourceStack.RemoveAt(0);
			_env.SetArrayFromList("BASH_SOURCE", [.. _sourceStack]);
			}
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
	/// A <see cref="FatalShellException"/> (readonly assignment, ${x:?}) ends a
	/// non-interactive shell. Control-flow exceptions still propagate.</summary>
	private int RunStatement(Node n)
		{
		try { return Execute(n); }
		catch (FatalShellException ex)
			{
			Console.Error.WriteLine($"bash: {ex.Message}");
			if (!Options.Interactive) throw new ExitException(ex.Code);
			_env.LastExitCode = ex.Code; return ex.Code;
			}
		// bash: a redirection failure is status 1; 2 is reserved for syntax-level errors
		catch (RedirectException ex) { Console.Error.WriteLine($"bash: {ex.Message}"); _env.LastExitCode = 1; return 1; }
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
					{
					// a command followed by && or || is not subject to errexit
					bool cond = op is ListOperator.And or ListOperator.Or;
					if (cond) _errexitSuppress++;
					try { code = RunStatement(node); }
					finally { if (cond) _errexitSuppress--; }
					}
				_env.LastExitCode = code;
				}
			prev = op;
			}
		if (Options.ExitOnError && code != 0 && _errexitSuppress == 0) throw new ExitException(code);
		return code;
		}

	// ── background jobs ─────────────────────────────────────────────────────────
	private readonly List<BackgroundJob> _jobs = [];
	private int _nextJobId = 1;

	private void StartBackgroundJob(Node node)
		{
		var job = new BackgroundJob { Id = _nextJobId++, Command = DescribeNode(node) };
		var stdio = ConsoleMux.Capture();   // the job writes wherever its parent was writing
		job.Thread = new Thread(() =>
			{
			ConsoleMux.Apply(stdio);
			_currentJob = job;
			try            { job.ExitCode = Execute(node); }
			catch (ExitException ex) { job.ExitCode = ex.Code; }
			catch          { job.ExitCode = 1; }
			finally        { job.Done = true; }
			}) { IsBackground = true };
		_jobs.Add(job);
		_env.LastBackgroundPid = job.Pid;
		job.Thread.Start();
		if (Options.Interactive) Console.Error.WriteLine($"[{job.Id}] {job.Pid}");
		}

	/// <summary>Find a job by `%n` spec, job number, or synthetic pid.</summary>
	private BackgroundJob? FindJob(string spec)
		{
		if (spec.StartsWith('%')) spec = spec[1..];
		if (spec is "%" or "+") return _jobs.LastOrDefault();
		if (spec == "-") return _jobs.Count >= 2 ? _jobs[^2] : null;
		if (!int.TryParse(spec, out var n)) return null;
		if (BackgroundJob.IsSyntheticPid(n)) n = BackgroundJob.JobIdFromPid(n);
		return _jobs.FirstOrDefault(j => j.Id == n);
		}

	/// <summary>`kill` on a job: kills the external child it is running, if any.</summary>
	public int KillJob(string spec, out string? error)
		{
		error = null;
		var job = FindJob(spec);
		if (job is null) { error = $"{spec}: no such job"; return 1; }
		int pid = job.ChildPid;
		if (pid > 0)
			{
			try { System.Diagnostics.Process.GetProcessById(pid).Kill(true); } catch { }
			return 0;
			}
		if (job.Done) return 0;
		error = $"{spec}: job runs in-process and has no child to signal";
		return 1;
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
		int code = 0;
		if (specs.Count == 0)
			{
			foreach (var j in _jobs.ToList()) { if (report) Console.WriteLine(j.Command); j.Thread.Join(); }
			}
		else
			{
			foreach (var spec in specs)
				{
				if (spec.StartsWith('-')) continue;   // -n / -f: accepted, no effect
				var j = FindJob(spec);
				if (j is null)
					{
					if (int.TryParse(spec.TrimStart('%'), out var pid) && !BackgroundJob.IsSyntheticPid(pid))
						{
						// a real pid: wait for that process if it is still around
						try { System.Diagnostics.Process.GetProcessById(pid).WaitForExit(); code = 0; continue; }
						catch { }
						}
					Console.Error.WriteLine($"bash: wait: {spec}: no such job");
					code = 127; continue;
					}
				if (report) Console.WriteLine(j.Command);
				j.Thread.Join();
				code = j.ExitCode;
				}
			}
		_jobs.RemoveAll(j => j.Done);
		return code;
		}

	public bool HasJobs => _jobs.Count > 0;

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

	/// <summary>
	/// The raw byte stream a byte-oriented builtin should write stdout to, or null when
	/// output is being text-captured (command substitution) — in which case the caller
	/// should write decoded text to Console.Out instead. Precedence: capture, then file
	/// redirect, then pipeline stage, then the real terminal.
	/// </summary>
	public static System.IO.Stream? CurrentRawStdout()
		{
		if (ConsoleMux.Capturing) return null;
		if (ConsoleMux.Raw is not null) return ConsoleMux.Raw;
		if (ConsoleMux.PipeOut is not null) return ConsoleMux.PipeOut;
		return Console.OpenStandardOutput();
		}

	/// <summary>Byte-faithful view of the current stdin for byte builtins (cat, head/tail -c, wc -c,
	/// cmp, od, tee, checksums…): the pipe or `&lt; file` behind this thread's stdin, the process's
	/// own stdin when nothing was installed, or null when stdin is text (here-doc/here-string),
	/// in which case the caller reads <c>Console.In</c>. Read one or the other, never both.</summary>
	public static System.IO.Stream? CurrentRawStdin()
		{
		if (ConsoleMux.RawIn is not null) return ConsoleMux.RawIn;
		if (ConsoleMux.InSlot is null && Console.IsInputRedirected) return Console.OpenStandardInput();
		return null;
		}

	// ── pipeline ──────────────────────────────────────────────────────────────

	private int ExecPipeline(Pipeline p)
		{
		if (p.Commands.Count == 1)
			{
			int c;
			if (p.Negated) { _errexitSuppress++; try { c = Execute(p.Commands[0].Command); } finally { _errexitSuppress--; } }
			else c = Execute(p.Commands[0].Command);
			_env.SetArrayFromList("PIPESTATUS", [c.ToString()]);
			return p.Negated ? (c == 0 ? 1 : 0) : c;
			}
		_errexitSuppress++;
		try { return ExecPipelineThreaded(p); }
		finally { _errexitSuppress--; }
		}

	/// <summary>
	/// Every stage runs on its own thread — builtins, compound commands and externals
	/// alike — joined by <see cref="PipeBuffer"/>s. A stage's Console.In/Out are its pipe
	/// ends (per-thread, via <see cref="ConsoleMux"/>); the last stage inherits the caller's
	/// stdout (so `x=$(a | b)` captures and `{ a | b; } >f` writes the file). When a stage
	/// finishes it closes its read end, so an upstream producer's next write raises
	/// <see cref="BrokenPipeException"/> (status 141) — bash's SIGPIPE — which also ends an
	/// external producer (ExecExternal kills it). No fork: stages share the shell's
	/// variables, which bash would isolate in subshells (documented).
	/// </summary>
	private int ExecPipelineThreaded(Pipeline p)
		{
		var stages = p.Commands;
		int n = stages.Count;
		var pipes = new PipeBuffer[n - 1];
		for (int i = 0; i < n - 1; i++) pipes[i] = new PipeBuffer();

		var exitCodes = new int[n];
		var threads   = new Thread[n];
		var parent    = ConsoleMux.Capture();
		var utf8      = ShellEncoding.Utf8;

		for (int i = 0; i < n; i++)
			{
			int idx = i;
			var (stageNode, stderrToo) = stages[idx];
			threads[idx] = new Thread(() =>
				{
				ConsoleMux.Apply(parent);
				System.IO.StreamReader? reader = null;
				System.IO.StreamWriter? writer = null;
				if (idx > 0)
					{
					reader = new System.IO.StreamReader(pipes[idx - 1].ReadEnd, utf8, false, 4096);
					ConsoleMux.SetIn(reader);
					ConsoleMux.PipeIn = pipes[idx - 1].ReadEnd;
					ConsoleMux.RawIn = pipes[idx - 1].ReadEnd;   // byte builtins read the pipe directly
					}
				else ConsoleMux.PipeIn = null;
				if (idx < n - 1)
					{
					writer = new System.IO.StreamWriter(pipes[idx].WriteEnd, utf8, 4096) { AutoFlush = true, NewLine = "\n" };
					ConsoleMux.SetOut(writer);
					ConsoleMux.Raw = pipes[idx].WriteEnd;
					ConsoleMux.Capturing = false;
					ConsoleMux.PipeOut = pipes[idx].WriteEnd;
					if (stderrToo) ConsoleMux.SetErr(writer);
					}
				else ConsoleMux.PipeOut = null;

				try { exitCodes[idx] = Execute(stageNode); }
				catch (ExitException ex)      { exitCodes[idx] = ex.Code; }
				catch (BrokenPipeException)   { exitCodes[idx] = 141; }
				catch (InterruptException)    { exitCodes[idx] = 130; }
				catch (FatalShellException ex){ Console.Error.WriteLine($"bash: {ex.Message}"); exitCodes[idx] = ex.Code; }
				catch (EvalException ex)      { Console.Error.WriteLine($"bash: {ex.Message}"); exitCodes[idx] = 2; }
				catch (IOException)           { exitCodes[idx] = 141; }
				catch (Exception)             { exitCodes[idx] = 1; }
				finally
					{
					try { ConsoleMux.Out.Flush(); } catch { }
					if (idx < n - 1) pipes[idx].CloseWrite();          // EOF downstream
					if (idx > 0)     pipes[idx - 1].CloseRead();       // SIGPIPE upstream
					}
				}) { IsBackground = true, Name = $"pipe-stage-{idx}" };
			}

		foreach (var t in threads) t.Start();
		foreach (var t in threads) t.Join();

		_env.SetArrayFromList("PIPESTATUS", exitCodes.Select(c => c.ToString()).ToList());
		int worstCode = exitCodes.Max();
		int lastCode  = exitCodes[n - 1];
		int result    = Options.PipeFail ? worstCode : lastCode;
		return p.Negated ? (result == 0 ? 1 : 0) : result;
		}

	private int ExecArrayElementAssign(ArrayElementAssign ae)
		{
		var idxStr = _expander.ExpandToString(ae.Index);
		var val    = _expander.ExpandToString(ae.Value);

		if (_env.IsAssoc(ae.ArrayName))
			_env.SetAssocElement(ae.ArrayName, idxStr, val);
		else
			{
			long idx = _expander.EvalArithmetic(idxStr);
			_env.SetArrayElement(ae.ArrayName, (int)idx, val);
			}
		return 0;
		}

	private int ExecArrayCompoundAssign(ArrayCompoundAssign ac)
		{
		// [key]=value elements target an associative (or explicit-index) array
		if (ac.Values.Count > 0 && ac.Values.All(w => w.Parts.Count > 0 && w.Parts[0] is LiteralPart lp && lp.Value.StartsWith('[') && lp.Value.Contains("]=")))
			{
			if (!ac.Append && !_env.IsAssoc(ac.ArrayName)) _env.SetArrayFromList(ac.ArrayName, []);
			foreach (var w in ac.Values)
				{
				var text = _expander.ExpandToString(w);
				int close = text.IndexOf("]=", StringComparison.Ordinal);
				var key = text[1..close]; var val = text[(close + 2)..];
				if (_env.IsAssoc(ac.ArrayName)) _env.SetAssocElement(ac.ArrayName, key, val);
				else _env.SetArrayElement(ac.ArrayName, (int)_expander.EvalArithmetic(key), val);
				}
			return 0;
			}
		var values = ac.Values.SelectMany(w => _expander.ExpandToFields(w)).ToList();
		if (ac.Append) _env.AppendArrayFromList(ac.ArrayName, values);
		else _env.SetArrayFromList(ac.ArrayName, values);
		return 0;
		}

	private int ExecArithmeticCommand(ArithmeticCommand am)
		{
		using var scope = am.Redirects.Count > 0 ? ApplyRedirects(am.Redirects) : null;   // no redirects: no scope (measured: ~72 B + 3 objects per command)
		long v = _expander.EvalArithmetic(am.Expression);
		return v != 0 ? 0 : 1;
		}

	// ── simple command ────────────────────────────────────────────────────────

	private int ExecSimpleCommand(SimpleCommand cmd)
		{
		// Assignments: a bare assignment statement reports the status of the last
		// command substitution it ran (x=$(false) → $?=1), otherwise 0.
		var tempAssign = new List<(string, string)>();
		_expander.ResetSubstStatus();
		foreach (var (rawName, valWord) in cmd.Assignments)
			{
			bool append = rawName.EndsWith('+');
			var name = append ? rawName[..^1] : rawName;
			var val = _expander.ExpandToString(valWord);
			if (cmd.Name is null)
				{
				if (append) _env.Append(name, val); else _env.Set(name, val);
				}
			else
				tempAssign.Add((name, append ? _env.Get(name) + val : val));
			}

		if (cmd.Name is null)
			{
			// `$(< file)` — a lone input redirect inside a substitution: the file's contents
			if (cmd.Assignments.Count == 0 && ConsoleMux.Capturing
			    && cmd.Redirects is [{ Kind: RedirectKind.Input } only])
				{
				var path = _expander.ExpandToPath(only.Target);
				try { Console.Out.Write(ShellEncoding.ReadAllText(path)); return 0; }
				catch (Exception ex) { Console.Error.WriteLine($"bash: {_expander.ExpandToString(only.Target)}: {ex.Message}"); return 1; }
				}
			if (cmd.Redirects.Count > 0)
				{
				// redirects with no command (`> file` truncates it) — apply and release
				using var _ = ApplyRedirects(cmd.Redirects);
				}
			return _expander.LastSubstStatus ?? 0;
			}

		var namStr = _expander.ExpandToString(cmd.Name);
		var args = new List<string>();

		// Alias expansion (only with `shopt -s expand_aliases`): the alias text replaces
		// the command word; its first word is the new command, the rest lead the args.
		if (Options.ExpandAliases && cmd.Name.Parts is [LiteralPart] && Aliases.TryGetValue(namStr, out var aliasText))
			{
			var seen = new HashSet<string>(StringComparer.Ordinal) { namStr };
			while (true)
				{
				var tokens = new Lexer.Lexer(aliasText).Tokenize();
				var parsed = new Parser.Parser(tokens, aliasText).Parse();
				if (parsed.Nodes is [SimpleCommand ac] && ac.Name is not null)
					{
					namStr = _expander.ExpandToString(ac.Name);
					args.AddRange(ac.Args.SelectMany(_expander.ExpandToFields));
					foreach (var (n, v) in ac.Assignments) tempAssign.Add((n, _expander.ExpandToString(v)));
					if (!seen.Add(namStr) || !Aliases.TryGetValue(namStr, out aliasText)) break;
					continue;
					}
				break;
				}
			}

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
			Console.Error.WriteLine($"{_env.Get("PS4")}{namStr} {string.Join(" ", args)}".TrimEnd());

		// `exec` with only redirects makes them permanent for the rest of the shell;
		// `exec cmd` replaces the shell: run it and exit with its status.
		if (namStr == "exec")
			{
			if (args.Count == 0)
				{
				_permanentScopes.Add(ApplyRedirects(cmd.Redirects));
				return 0;
				}
			int ai = 0;
			while (ai < args.Count && args[ai].StartsWith('-') && args[ai].Length > 1)
				{
				if (args[ai] == "-a" && ai + 1 < args.Count) ai += 2;      // -a name: argv[0] override (ignored)
				else if (args[ai] is "-c" or "-l") ai++;
				else break;
				}
			var execArgs = args.Skip(ai + 1).ToList();
			int rc = ExecExternalOrBuiltin(args[ai], execArgs, tempAssign, cmd.Redirects);
			throw new ExitException(rc);
			}

		// Redirects are applied in exactly one place per command type. Builtins and
		// functions run in-process, so they take ApplyRedirects' Console swap. External
		// processes write to OS handles that a Console swap can't reach (and opening the
		// same file twice throws a sharing violation), so ExecExternal owns their fd-map.
		if (Builtins.Has(namStr) || _functions.ContainsKey(namStr))
			{
			using var redirectScope = cmd.Redirects.Count > 0 ? ApplyRedirects(cmd.Redirects) : null;   // no redirects: no scope (measured: ~72 B + 3 objects per command)
			using var tempScope = ApplyTempAssignments(tempAssign);
			if (_builtins.TryExecute(namStr, args, out int builtinCode))
				return builtinCode;
			return ExecFunction(_functions[namStr], args);
			}

		return ExecExternal(namStr, args, tempAssign, cmd.Redirects);
		}

	/// <summary>`VAR=x builtin` / `VAR=x func`: set for the duration, then restore.</summary>
	private IDisposable? ApplyTempAssignments(List<(string, string)> tempAssign)
		{
		if (tempAssign.Count == 0) return null;
		var saved = new List<(string name, string? old, bool wasExported)>();
		foreach (var (n, v) in tempAssign)
			{
			saved.Add((n, _env.IsSet(n) ? _env.Get(n) : null, _env.IsExported(n)));
			_env.Set(n, v); _env.Export(n);
			}
		return new TempScope(() =>
			{
			foreach (var (n, old, exp) in saved)
				{
				if (old is null) _env.Unset(n); else { _env.Set(n, old); if (!exp) _env.Unexport(n); }
				}
			});
		}

	private sealed class TempScope(Action onDispose) : IDisposable
		{
		public void Dispose() => onDispose();
		}

	/// <summary>For `exec cmd`: an external if one resolves, else the builtin of that name.</summary>
	private int ExecExternalOrBuiltin(string name, List<string> args, List<(string, string)> tempAssign, List<Redirect> redirects)
		{
		if (ResolveOnPath(name) is null && Builtins.Has(name))
			{
			_permanentScopes.Add(ApplyRedirects(redirects));
			return _builtins.TryExecute(name, args, out int code) ? code : 127;
			}
		return ExecExternal(name, args, tempAssign, redirects);
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
	public int RunCommand(string name, List<string> args) => RunCommand(name, args, false, [], false);

	/// <summary>Run a command on the calling thread with <paramref name="job"/> as the current
	/// job, so an external child it spawns is recorded in <see cref="BackgroundJob.ChildPid"/>
	/// (used by `timeout` to kill the child on expiry).</summary>
	public int RunAsJob(BackgroundJob job, string name, List<string> args)
		{
		var prev = _currentJob;
		_currentJob = job;
		try { return RunCommand(name, args); }
		finally { _currentJob = prev; }
		}

	/// <summary>Dispatch with control over function lookup (`command`), extra environment
	/// (`env VAR=x cmd`) and a cleared environment (`env -i`).</summary>
	public int RunCommand(string name, List<string> args, bool skipFunctions,
		List<(string, string)> extraEnv, bool clearEnv)
		{
		if (!skipFunctions && _functions.TryGetValue(name, out var fd))
			{
			using var t = ApplyTempAssignments(extraEnv);
			return ExecFunction(fd, args);
			}
		if (Builtins.Has(name))
			{
			using var t = ApplyTempAssignments(extraEnv);
			return _builtins.TryExecute(name, args, out int code) ? code : 127;
			}
		return ExecExternal(name, args, extraEnv, [], clearEnv);
		}

	/// <summary>Run the PATH external `name` (never a builtin or function) with the current
	/// in-process redirects in effect — used when a coreutil defers an unsupported option.</summary>
	public int RunExternal(string name, List<string> args) => ExecExternal(name, args, [], []);

	/// <summary>`builtin name args`: the builtin only.</summary>
	public int RunBuiltin(string name, List<string> args)
		{
		if (!Builtins.Has(name)) { Console.Error.WriteLine($"bash: builtin: {name}: not a shell builtin"); return 1; }
		return _builtins.TryExecute(name, args, out int code) ? code : 1;
		}

	/// <summary>Kind of a command name for `type -t` / `command -v`: alias, keyword, function,
	/// builtin, file (with its path), or null.</summary>
	public (string kind, string? path)? Classify(string name, bool skipAliases = false, bool skipFunctions = false)
		{
		if (!skipAliases && Aliases.ContainsKey(name)) return ("alias", null);
		if (Keywords.Contains(name)) return ("keyword", null);
		if (!skipFunctions && _functions.ContainsKey(name)) return ("function", null);
		if (Builtins.Has(name)) return ("builtin", null);
		if (name.Contains('/') || name.Contains('\\'))
			{
			var p = ShellEnvironment.TranslatePath(name);
			return File.Exists(p) ? ("file", name) : null;
			}
		var found = ResolveOnPath(name);
		return found is not null ? ("file", found) : null;
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
		try { using var sr = new System.IO.StreamReader(p, ShellEncoding.Utf8); first = sr.ReadLine() ?? ""; }
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
		try   { return child.RunString(ShellEncoding.ReadAllText(path)); }
		catch (ExitException ex) { return ex.Code; }
		}

	private int ExecExternal(string name, List<string> args,
		List<(string, string)> tempAssign, List<Redirect> redirects, bool clearEnv = false)
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
			return ExecExternal(Path.GetFileName(interp), spawn, tempAssign, redirects, clearEnv);
			}

		var fileName = name.Contains('/') || name.Contains('\\')
			? ShellEnvironment.TranslatePath(name)
			: ResolveOnPath(name) ?? name;   // cached full path, else OS resolves
		var psi = new ProcessStartInfo
			{
			FileName         = fileName,
			UseShellExecute  = false,
			WorkingDirectory = Directory.GetCurrentDirectory(),
			};

		foreach (var arg in args)
			psi.ArgumentList.Add(arg);

		if (clearEnv) psi.Environment.Clear();
		else
			foreach (var (k, v) in _env.GetExportedVars())
				psi.Environment[k] = v;
		foreach (var (k, v) in tempAssign)
			psi.Environment[k] = v;

		// Resolve redirects into a simple fd-map:
		//   fd 0 → null (inherit) | string path (file) | text (heredoc/here-string) | "pipe"
		//   fd 1 → same
		//   fd 2 → same | "1" (dup to stdout)
		// We process left-to-right as bash does.

		string? stdin_path  = null;  bool stdin_devnull  = false;  string? stdin_text = null;
		string? stdout_path = null;  bool stdout_append  = false, stdout_devnull = false;
		string? stderr_path = null;  bool stderr_append  = false, stderr_devnull = false;
		bool    stderr_to_stdout = false; // 2>&1
		bool    stdout_to_stderr = false; // 1>&2
		System.IO.Stream? stdin_borrowed = null, stdout_borrowed = null, stderr_borrowed = null;   // <&3 / >&3 / 2>&3

		foreach (var r in redirects)
			{
			if (r.Kind is RedirectKind.Heredoc or RedirectKind.HeredocStrip)
				{ stdin_text = _expander.ExpandToString(r.Target); stdin_path = null; stdin_devnull = false; continue; }
			if (r.Kind == RedirectKind.HereString)
				{ stdin_text = _expander.ExpandToString(r.Target) + "\n"; stdin_path = null; stdin_devnull = false; continue; }

			var raw    = _expander.ExpandToString(r.Target);
			var target = ShellEnvironment.TranslatePath(raw);
			int fd = r.Fd ?? (r.Kind is RedirectKind.Input or RedirectKind.InputDup ? 0 : 1);
			switch (r.Kind)
				{
				case RedirectKind.Input:
					stdin_text = null;
					if (raw == "/dev/null") { stdin_devnull = true; stdin_path = null; }
					else                    { stdin_path = target; stdin_devnull = false; }
					break;

				case RedirectKind.OutputBoth:
				case RedirectKind.AppendBoth:
					{
					bool append = r.Kind == RedirectKind.AppendBoth;
					stdout_to_stderr = false; stdout_path = null; stdout_devnull = false;
					stderr_path = null; stderr_devnull = false;
					if (raw == "/dev/null") { stdout_devnull = true; stderr_devnull = true; }
					else { stdout_path = target; stdout_append = append; stderr_to_stdout = true; }
					break;
					}

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
					else if (fd <= 1)
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
					if (fd == 2 && raw == "-") { stderr_devnull = true; }
					if (fd == 1 && raw == "-") { stdout_devnull = true; }
					if (int.TryParse(raw, out var dupSrc) && dupSrc > 2 && _fds.TryGetValue(dupSrc, out var dupStream))
						{
						if (fd == 1) { stdout_borrowed = dupStream; stdout_path = null; stdout_devnull = false; stdout_to_stderr = false; }
						if (fd == 2) { stderr_borrowed = dupStream; stderr_path = null; stderr_devnull = false; stderr_to_stdout = false; }
						}
					break;
				case RedirectKind.InputDup:
					if (fd == 0 && int.TryParse(raw, out var inSrc) && inSrc > 2 && _fds.TryGetValue(inSrc, out var inStream))
						{ stdin_borrowed = inStream; stdin_path = null; stdin_devnull = false; stdin_text = null; }
					if (fd == 0 && raw == "-") { stdin_devnull = true; }
					break;
				}
			}

		// Configure psi based on resolved fd-map
		System.IO.FileStream? stdinFs  = null;
		System.IO.Stream?     stdoutFs = null;   // file or Stream.Null
		System.IO.Stream?     stderrFs = null;   // file or Stream.Null

		// Pipeline overrides take precedence over file redirects for stdin/stdout
		bool hasPipeStdin  = ConsoleMux.PipeIn  is not null && stdin_path is null && !stdin_devnull && stdin_text is null && stdin_borrowed is null;
		bool hasPipeStdout = ConsoleMux.PipeOut is not null && stdout_path is null && !stdout_devnull && !stdout_to_stderr && stdout_borrowed is null;
		bool stdoutOwned = true, stderrOwned = true;   // borrowed fd streams must not be disposed here

		// An in-process redirect in effect around this command (command substitution,
		// `{ … } >file`, `while …; done <<EOF`): route the child's stdio through the swapped
		// Console streams so the output lands where bash would put it.
		bool outSwapped = !hasPipeStdout && stdout_path is null && !stdout_devnull && !stdout_to_stderr && stdout_borrowed is null
		                  && ConsoleMux.OutSwapped;
		bool errSwapped = !stderr_devnull && stderr_path is null && stderr_borrowed is null && !stderr_to_stdout
		                  && ConsoleMux.ErrSwapped;
		bool inSwapped  = !hasPipeStdin && stdin_path is null && !stdin_devnull && stdin_text is null && stdin_borrowed is null
		                  && ConsoleMux.InSwapped;
		TextWriter? swappedOut = outSwapped ? ConsoleMux.Out : null;
		TextWriter? swappedErr = errSwapped ? ConsoleMux.Err : null;
		System.IO.Stream? swappedRaw = outSwapped && !ConsoleMux.Capturing ? ConsoleMux.Raw : null;

		if (hasPipeStdin)
			psi.RedirectStandardInput = true;
		else if (stdin_devnull || stdin_text is not null || stdin_borrowed is not null || inSwapped)
			psi.RedirectStandardInput = true;     // close immediately → EOF / feed text / copy fd
		else if (stdin_path is not null)
			{
			psi.RedirectStandardInput = true;
			try { stdinFs = File.OpenRead(stdin_path); }
			catch (Exception ex) { Console.Error.WriteLine($"bash: {name}: {ex.Message}"); return 1; }
			}

		if (hasPipeStdout)
			psi.RedirectStandardOutput = true;
		else if (stdout_devnull)
			{ psi.RedirectStandardOutput = true; stdoutFs = System.IO.Stream.Null; }
		else if (stdout_borrowed is not null)
			{ psi.RedirectStandardOutput = true; stdoutFs = stdout_borrowed; stdoutOwned = false; }
		else if (stdout_path is not null)
			{
			psi.RedirectStandardOutput = true;
			try
				{
				stdoutFs = stdout_append
					? File.Open(stdout_path, FileMode.Append, FileAccess.Write)
					: File.Create(stdout_path);
				}
			catch (Exception ex) { Console.Error.WriteLine($"bash: {stdout_path}: {ex.Message}"); stdinFs?.Dispose(); return 1; }
			}
		else if (stdout_to_stderr || outSwapped)
			psi.RedirectStandardOutput = true;

		// stderr destination (independent of stdout)
		if (errSwapped)
			psi.RedirectStandardError = true;
		else if (stderr_devnull)
			{ psi.RedirectStandardError = true; stderrFs = System.IO.Stream.Null; }
		else if (stderr_borrowed is not null)
			{ psi.RedirectStandardError = true; stderrFs = stderr_borrowed; stderrOwned = false; }
		else if (stderr_path is not null)
			{
			psi.RedirectStandardError = true;
			try
				{
				stderrFs = stderr_append
					? File.Open(stderr_path, FileMode.Append, FileAccess.Write)
					: File.Create(stderr_path);
				}
			catch (Exception ex) { Console.Error.WriteLine($"bash: {stderr_path}: {ex.Message}"); stdinFs?.Dispose(); if (stdoutOwned) (stdoutFs as IDisposable)?.Dispose(); return 1; }
			}
		else if (stderr_to_stdout && (stdout_path is not null || stdout_devnull || hasPipeStdout || stdout_borrowed is not null || outSwapped))
			psi.RedirectStandardError = true;   // follows stdout's final destination
		// else (stderr_to_stdout with inherited stdout): both inherit the console — nothing to redirect

		Process proc;
		bool brokenPipe = false;
		try
			{
			proc = Process.Start(psi)
				?? throw new EvalException($"{name}: failed to start");
			}
		catch (Exception ex) when (ex is not EvalException)
			{
			// bash-style diagnostics, routed to wherever this command's stderr was sent
			// (so `cmd 2>/dev/null` really is silent).
			string msg = ex is System.ComponentModel.Win32Exception w && w.NativeErrorCode is 2 or 3
				? $"bash: {name}: command not found"
				: ex is System.ComponentModel.Win32Exception w2 && w2.NativeErrorCode is 5 or 193
					? $"bash: {name}: Permission denied"
					: $"bash: {name}: {ex.Message}";
			if (!stderr_devnull)
				{
				if (stderr_path is not null)
					{ try { File.AppendAllText(stderr_path, msg + "\n"); } catch { } }
				else if (stderr_to_stdout && stdoutFs is not null)
					{ var b = ShellEncoding.Utf8.GetBytes(msg + "\n"); stdoutFs.Write(b, 0, b.Length); }
				else if (stderr_to_stdout && (hasPipeStdout || ConsoleMux.Capturing))
					Console.Out.WriteLine(msg);
				else
					Console.Error.WriteLine(msg);
				}
			stdinFs?.Dispose(); (stdoutFs as IDisposable)?.Dispose(); (stderrFs as IDisposable)?.Dispose();
			return ex is System.ComponentModel.Win32Exception w3 && w3.NativeErrorCode is 5 or 193 ? 126 : 127;
			}

		if (_currentJob is not null) _currentJob.ChildPid = proc.Id;
		ChildJobs.Attach(proc);   // dies with us (host timeout kills) - best effort

		try
			{
			// Feed stdin
			if (hasPipeStdin)
				{
				var src = ConsoleMux.PipeIn!;
				var t = new Thread(() => { try { src.CopyTo(proc.StandardInput.BaseStream); } catch { } try { proc.StandardInput.Close(); } catch { } });
				t.IsBackground = true;
				t.Start();
				}
			else if (stdin_text is not null)
				{
				var t = new Thread(() =>
					{
					try
						{
						var bytes = ShellEncoding.Utf8.GetBytes(stdin_text);
						proc.StandardInput.BaseStream.Write(bytes, 0, bytes.Length);
						proc.StandardInput.BaseStream.Flush();
						}
					catch { }
					try { proc.StandardInput.Close(); } catch { }
					});
				t.IsBackground = true;
				t.Start();
				}
			else if (stdin_devnull)
				proc.StandardInput.Close();
			else if (inSwapped)
				{
				var src = ConsoleMux.In;
				var t = new Thread(() =>
					{
					try
						{
						var buf = new char[4096];
						int n;
						while ((n = src.Read(buf, 0, buf.Length)) > 0)
							{ proc.StandardInput.Write(buf, 0, n); proc.StandardInput.Flush(); }
						}
					catch { }
					try { proc.StandardInput.Close(); } catch { }
					});
				t.IsBackground = true;
				t.Start();
				}
			else if (stdin_borrowed is not null)
				{
				var src = stdin_borrowed;
				var t = new Thread(() => { try { src.CopyTo(proc.StandardInput.BaseStream); } catch { } try { proc.StandardInput.Close(); } catch { } });
				t.IsBackground = true;
				t.Start();
				}
			else if (stdinFs is not null)
				{
				var t = new Thread(() => { try { stdinFs.CopyTo(proc.StandardInput.BaseStream); } catch { } try { proc.StandardInput.Close(); } catch { } });
				t.IsBackground = true;
				t.Start();
				}

			Thread? stdoutThread = null;
			Thread? stderrThread = null;

			// Drain stdout: pipe → next stage, file/devnull → stream, 1>&2 → console stderr.
			if (hasPipeStdout)
				{
				var dest = ConsoleMux.PipeOut!;
				stdoutThread = new Thread(() =>
					{
					try
						{
						var buf = new byte[8192];
						int n;
						while ((n = proc.StandardOutput.BaseStream.Read(buf, 0, buf.Length)) > 0)
							dest.Write(buf, 0, n);
						dest.Flush();
						}
					catch (BrokenPipeException)
						{
						// the consumer went away: SIGPIPE the producer
						try { proc.Kill(true); } catch { }
						brokenPipe = true;
						}
					catch { }
					// EOF for the next stage is signalled by the stage thread's CloseWrite
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
			else if (outSwapped)
				{
				// in-process redirect: bytes to the raw sink when there is one (a file), else
				// text into the swapped Console.Out (a $( ) capture or a pipe stage writer)
				var rawDest = swappedRaw;
				var textDest = swappedOut!;
				stdoutThread = new Thread(() =>
					{
					try
						{
						if (rawDest is not null) { proc.StandardOutput.BaseStream.CopyTo(rawDest); rawDest.Flush(); }
						else
							{
							var buf = new char[4096];
							int n;
							while ((n = proc.StandardOutput.Read(buf, 0, buf.Length)) > 0)
								lock (textDest) { textDest.Write(buf, 0, n); }
							textDest.Flush();
							}
						}
					catch { }
					});
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

			// Drain stderr: file/devnull → stream, 2>&1 → stdout's destination, swapped
			// Console.Error → that writer.
			if (psi.RedirectStandardError)
				{
				System.IO.Stream? errDest =
					stderrFs is not null ? stderrFs
					: stderr_to_stdout   ? (stdoutFs ?? (hasPipeStdout ? ConsoleMux.PipeOut : swappedRaw))
					: null;
				TextWriter? errText = errSwapped ? swappedErr : (stderr_to_stdout && outSwapped && swappedRaw is null ? swappedOut : null);
				if (errDest is not null)
					{
					var dest = errDest;
					stderrThread = new Thread(() => { try { proc.StandardError.BaseStream.CopyTo(dest); dest.Flush(); } catch { } });
					stderrThread.IsBackground = true;
					stderrThread.Start();
					}
				else if (errText is not null)
					{
					var dest = errText;
					stderrThread = new Thread(() =>
						{
						try
							{
							var buf = new char[4096];
							int n;
							while ((n = proc.StandardError.Read(buf, 0, buf.Length)) > 0)
								lock (dest) { dest.Write(buf, 0, n); }
							dest.Flush();
							}
						catch { }
						});
					stderrThread.IsBackground = true;
					stderrThread.Start();
					}
				}

			proc.WaitForExit();
			stdoutThread?.Join();
			stderrThread?.Join();
			return brokenPipe ? 141 : proc.ExitCode;
			}
		finally
			{
			if (_currentJob is not null) _currentJob.ChildPid = 0;
			stdinFs?.Dispose();
			if (stdoutOwned) (stdoutFs as IDisposable)?.Dispose(); else { try { stdoutFs?.Flush(); } catch { } }
			if (stderrOwned) (stderrFs as IDisposable)?.Dispose(); else { try { stderrFs?.Flush(); } catch { } }
			}
		}

	// ── redirects ─────────────────────────────────────────────────────────────

	/// <summary>bash wording for a redirect that could not be opened: a directory target reports
	/// "Is a directory" rather than the platform permission error it actually raises.</summary>
	private static string RedirectMessage(string target, Exception ex)
		{
		try { if (Directory.Exists(target)) return "Is a directory"; } catch { }
		return Builtins.IoError(ex);
		}

	private RedirectScope ApplyRedirects(List<Redirect> redirects)
		{
		var scope = new RedirectScope();
		foreach (var r in redirects)
			{
			// here-documents / here-strings feed stdin directly
			if (r.Kind is RedirectKind.Heredoc or RedirectKind.HeredocStrip or RedirectKind.HereString)
				{
				var text = _expander.ExpandToString(r.Target);
				if (r.Kind == RedirectKind.HereString) text += "\n";
				scope.SaveIn(); scope.SaveRawIn();
				ConsoleMux.SetIn(new System.IO.StringReader(text));
				ConsoleMux.RawIn = null;   // text source: byte builtins fall back to Console.In
				continue;
				}

			var rawTarget = _expander.ExpandToString(r.Target);
			var target    = ShellEnvironment.TranslatePath(rawTarget);
			int fd        = r.Fd ?? 1;
			switch (r.Kind)
				{
				case RedirectKind.OutputBoth:
				case RedirectKind.AppendBoth:
					{
					bool append = r.Kind == RedirectKind.AppendBoth;
					if (rawTarget == "/dev/null")
						{
						scope.SaveErr(); ConsoleMux.SetErr(System.IO.TextWriter.Null);
						scope.SaveOut(); scope.SaveRaw(); ConsoleMux.SetOut(System.IO.TextWriter.Null); ConsoleMux.Raw = System.IO.Stream.Null;
						break;
						}
					System.IO.FileStream fs;
					try { fs = append ? File.Open(target, FileMode.Append, FileAccess.Write) : File.Create(target); }
					catch (Exception ex) { scope.Dispose(); throw new RedirectException($"{rawTarget}: {RedirectMessage(target, ex)}"); }
					// ShellEncoding, not the default: StreamWriter's default UTF-8 THROWS on a lone
					// surrogate, so `printf '\xff' > file` died with an encoder error (2026-09-12).
					var w = new System.IO.StreamWriter(fs, ShellEncoding.Utf8) { AutoFlush = true, NewLine = "\n" };
					scope.TrackInstalled(w);
					scope.SaveOut(); scope.SaveRaw(); ConsoleMux.SetOut(w); ConsoleMux.Raw = fs;
					scope.SaveErr(); ConsoleMux.SetErr(w);
					break;
					}
				case RedirectKind.Output:
				case RedirectKind.Clobber:
				case RedirectKind.Append:
					{
					bool append = r.Kind == RedirectKind.Append;
					// /dev specials
					if (rawTarget == "/dev/null")
						{
						if (fd == 2) { scope.SaveErr(); ConsoleMux.SetErr(System.IO.TextWriter.Null); }
						else { scope.SaveOut(); scope.SaveRaw(); ConsoleMux.SetOut(System.IO.TextWriter.Null); ConsoleMux.Raw = System.IO.Stream.Null; }
						break;
						}
					if (rawTarget == "/dev/stdout") { if (fd == 2) { scope.SaveErr(); ConsoleMux.SetErr(ConsoleMux.Out); } break; }
					if (rawTarget == "/dev/stderr") { if (fd != 2) { scope.SaveOut(); ConsoleMux.SetOut(ConsoleMux.Err); } break; }
					if (fd > 2)
						{
						// 3>file: open a numbered descriptor for later `>&3` / `read -u 3`
						System.IO.FileStream nfs;
						try { nfs = append ? File.Open(target, FileMode.Append, FileAccess.Write) : File.Create(target); }
						catch (Exception ex) { scope.Dispose(); throw new RedirectException($"{rawTarget}: {RedirectMessage(target, ex)}"); }
						scope.TrackFd(_fds, fd, nfs);
						break;
						}

					if (Options.NoClobber && r.Kind == RedirectKind.Output && File.Exists(target))
						{ scope.Dispose(); throw new EvalException($"{rawTarget}: cannot overwrite existing file"); }
					System.IO.FileStream fs;
					try { fs = append ? File.Open(target, FileMode.Append, FileAccess.Write) : File.Create(target); }
					catch (Exception ex) { scope.Dispose(); throw new RedirectException($"{rawTarget}: {RedirectMessage(target, ex)}"); }
					// ShellEncoding, not the default: StreamWriter's default UTF-8 THROWS on a lone
					// surrogate, so `printf '\xff' > file` died with an encoder error (2026-09-12).
					var w = new System.IO.StreamWriter(fs, ShellEncoding.Utf8) { AutoFlush = true, NewLine = "\n" };
					scope.TrackInstalled(w);
					if (fd == 2) { scope.SaveErr(); ConsoleMux.SetErr(w); }              // 2>file → stderr
					else         { scope.SaveOut(); scope.SaveRaw(); ConsoleMux.SetOut(w); ConsoleMux.Raw = fs; }
					break;
					}
				case RedirectKind.Input:
					{
					int ifd = r.Fd ?? 0;
					if (ifd > 0)
						{
						// 3<file: open a numbered descriptor for `<&3` / `read -u 3`
						System.IO.FileStream ifs;
						try { ifs = File.OpenRead(target); }
						catch (Exception ex) { scope.Dispose(); throw new RedirectException($"{rawTarget}: {RedirectMessage(target, ex)}"); }
						scope.TrackFd(_fds, ifd, ifs);
						break;
						}
					scope.SaveIn(); scope.SaveRawIn();
					if (rawTarget == "/dev/null") { ConsoleMux.SetIn(new System.IO.StringReader("")); ConsoleMux.RawIn = System.IO.Stream.Null; break; }
					System.IO.FileStream inFs;
					try { inFs = File.OpenRead(target); }
					catch (Exception ex) { scope.Dispose(); throw new RedirectException($"{rawTarget}: {RedirectMessage(target, ex)}"); }
					var reader = new System.IO.StreamReader(inFs, ShellEncoding.Utf8);   // buffers nothing until first read
					scope.TrackInstalled(reader);
					ConsoleMux.SetIn(reader);
					ConsoleMux.RawIn = inFs;   // byte builtins read the file directly
					break;
					}
				case RedirectKind.OutputDup:
					{
					int ofd = r.Fd ?? 1;
					if (target == "-")
						{
						// n>&- closes n
						if (ofd == 1) { scope.SaveOut(); scope.SaveRaw(); ConsoleMux.SetOut(System.IO.TextWriter.Null); ConsoleMux.Raw = System.IO.Stream.Null; }
						else if (ofd == 2) { scope.SaveErr(); ConsoleMux.SetErr(System.IO.TextWriter.Null); }
						else scope.TrackFd(_fds, ofd, null);
						break;
						}
					if (!int.TryParse(target, out int src)) { scope.Dispose(); throw new EvalException($"{rawTarget}: ambiguous redirect"); }
					if (ofd == 1 && src == 2) { scope.SaveOut(); ConsoleMux.SetOut(ConsoleMux.Err); break; }          // 1>&2
					if (ofd == 2 && src == 1) { scope.SaveErr(); ConsoleMux.SetErr(ConsoleMux.Out); break; }          // 2>&1
					if (src > 2)
						{
						if (!_fds.TryGetValue(src, out var s)) { scope.Dispose(); throw new EvalException($"{src}: Bad file descriptor"); }
						if (ofd == 1)
							{
							var w = new System.IO.StreamWriter(s, ShellEncoding.Utf8, 1024, leaveOpen: true) { AutoFlush = true, NewLine = "\n" };
							scope.TrackInstalled(w);
							scope.SaveOut(); scope.SaveRaw(); ConsoleMux.SetOut(w); ConsoleMux.Raw = s;
							}
						else if (ofd == 2)
							{
							var w = new System.IO.StreamWriter(s, ShellEncoding.Utf8, 1024, leaveOpen: true) { AutoFlush = true, NewLine = "\n" };
							scope.TrackInstalled(w);
							scope.SaveErr(); ConsoleMux.SetErr(w);
							}
						else scope.TrackFd(_fds, ofd, s, shared: true);      // 4>&3
						}
					break;
					}
				case RedirectKind.InputDup:
					{
					int ifd = r.Fd ?? 0;
					if (target == "-")
						{
						if (ifd == 0) { scope.SaveIn(); scope.SaveRawIn(); ConsoleMux.SetIn(new System.IO.StringReader("")); ConsoleMux.RawIn = System.IO.Stream.Null; }
						else scope.TrackFd(_fds, ifd, null);
						break;
						}
					if (!int.TryParse(target, out int src)) { scope.Dispose(); throw new EvalException($"{rawTarget}: ambiguous redirect"); }
					if (ifd == 2 && src == 1) { scope.SaveErr(); ConsoleMux.SetErr(ConsoleMux.Out); break; }          // 2<&1 (defensive)
					if (ifd == 0 && src > 2)
						{
						if (!_fds.TryGetValue(src, out var s)) { scope.Dispose(); throw new EvalException($"{src}: Bad file descriptor"); }
						var rd = new System.IO.StreamReader(s, ShellEncoding.Utf8, false, 1024, leaveOpen: true);
						scope.TrackInstalled(rd);
						scope.SaveIn(); scope.SaveRawIn(); ConsoleMux.SetIn(rd); ConsoleMux.RawIn = s;
						}
					break;
					}
				}
			}
		return scope;
		}

	// ── compound commands ─────────────────────────────────────────────────────

	private int ExecWithRedirects(Node body, List<Redirect> redirects)
		{
		using var scope = redirects.Count > 0 ? ApplyRedirects(redirects) : null;   // no redirects: no scope (measured: ~72 B + 3 objects per command)
		return Execute(body);
		}

	private int ExecSubshell(Subshell ss)
		{
		// In-process subshell (no fork on Windows): variables, cwd, positionals and
		// attributes are snapshotted and restored, and `exit` ends only the subshell.
		// Not isolated: shell options, traps, functions, aliases (documented).
		var snap = _env.TakeSnapshot();
		try   { return ExecWithRedirects(ss.Body, ss.Redirects); }
		catch (ExitException ex) { return ex.Code; }
		catch (FatalShellException ex) { Console.Error.WriteLine($"bash: {ex.Message}"); return ex.Code; }   // fatal to the subshell only
		finally { _env.RestoreSnapshot(snap); }
		}

	private int ExecFunctionDef(FunctionDef fd)
		{
		if (_env.IsReadonly(fd.Name) && _functions.ContainsKey(fd.Name))
			throw new EvalException($"{fd.Name}: readonly function");
		_functions[fd.Name] = fd;
		return 0;
		}

	private int ExecFunction(FunctionDef fd, List<string> args)
		{
		_env.PushScope(args.ToArray());
		_funcStack.Insert(0, fd.Name);
		_env.SetArrayFromList("FUNCNAME", [.. _funcStack]);
		try
			{
			if (fd.Redirects.Count > 0) return ExecWithRedirects(fd.Body, fd.Redirects);
			return Execute(fd.Body);
			}
		catch (ReturnException r) { return r.Code; }
		finally
			{
			_env.PopScope();
			_funcStack.RemoveAt(0);
			_env.SetArrayFromList("FUNCNAME", [.. _funcStack]);
			}
		}

	private int ExecArithFor(ArithForCommand af)
		{
		using var scope = af.Redirects.Count > 0 ? ApplyRedirects(af.Redirects) : null;   // no redirects: no scope (measured: ~72 B + 3 objects per command)
		if (af.Init.Length > 0) _expander.EvalArithmetic(af.Init);
		int code = 0;
		while (true)
			{
			CheckInterrupt();
			if (af.Condition.Length > 0 && _expander.EvalArithmetic(af.Condition) == 0) break;
			try   { code = Execute(af.Body); }
			catch (BreakException b)    { if (b.Levels <= 1) break;    throw new BreakException(b.Levels - 1); }
			catch (ContinueException c) { if (c.Levels > 1) throw new ContinueException(c.Levels - 1); }
			if (af.Step.Length > 0) _expander.EvalArithmetic(af.Step);
			}
		return code;
		}

	/// <summary>Run a condition (if/while/until) with errexit suppressed.</summary>
	private int ExecCondition(Node cond)
		{
		_errexitSuppress++;
		try { return Execute(cond); }
		finally { _errexitSuppress--; }
		}

	private int ExecIf(IfCommand ic)
		{
		using var scope = ic.Redirects.Count > 0 ? ApplyRedirects(ic.Redirects) : null;   // no redirects: no scope (measured: ~72 B + 3 objects per command)
		if (ExecCondition(ic.Condition) == 0)
			return Execute(ic.Then);
		foreach (var (cond, body) in ic.Elifs)
			if (ExecCondition(cond) == 0)
				return Execute(body);
		return ic.Else is not null ? Execute(ic.Else) : 0;
		}

	private int ExecWhile(WhileCommand wc)
		{
		using var scope = wc.Redirects.Count > 0 ? ApplyRedirects(wc.Redirects) : null;   // no redirects: no scope (measured: ~72 B + 3 objects per command)
		int code = 0;
		while (true)
			{
			CheckInterrupt();
			int condCode = ExecCondition(wc.Condition);
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
		using var scope = fc.Redirects.Count > 0 ? ApplyRedirects(fc.Redirects) : null;   // no redirects: no scope (measured: ~72 B + 3 objects per command)
		var words = fc.Words.Count > 0
			? fc.Words.SelectMany(_expander.ExpandToFields).ToList()
			: _env.GetPositionals();

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
		using var scope = cc.Redirects.Count > 0 ? ApplyRedirects(cc.Redirects) : null;   // no redirects: no scope (measured: ~72 B + 3 objects per command)
		var subject = _expander.ExpandToString(cc.Subject);
		foreach (var item in cc.Items)
			{
			foreach (var pat in item.Patterns)
				{
				var patStr = _expander.ExpandToPattern(pat);
				if (Glob.Match(patStr, subject, Options.NoCaseMatch))
					return item.Body is not null ? Execute(item.Body) : 0;
				}
			}
		return 0;
		}

	// ── [[ ]] ─────────────────────────────────────────────────────────────────

	private int ExecConditional(ConditionalExpression ce)
		{
		_errexitSuppress++;
		try { return EvalCondExpr(ce.Expr) ? 0 : 1; }
		finally { _errexitSuppress--; }
		}

	private bool EvalCondExpr(CondExpr expr) => expr switch
		{
		CondAnd a    => EvalCondExpr(a.Left) && EvalCondExpr(a.Right),
		CondOr  o    => EvalCondExpr(o.Left) || EvalCondExpr(o.Right),
		CondNot n    => !EvalCondExpr(n.Operand),
		CondUnary u  => EvalCondUnary(u.Op, _expander.ExpandToString(u.Operand)),
		CondBinary b => EvalCondBinary(b),
		CondWord w   => _expander.ExpandToString(w.Value).Length > 0,
		_ => false
		};

	private bool EvalCondUnary(string op, string val)
		{
		switch (op)
			{
			case "-v":
				{
				int lb = val.IndexOf('[');
				if (lb > 0 && val.EndsWith(']'))
					{
					var n = val[..lb]; var sub = val[(lb + 1)..^1];
					if (sub is "@" or "*") return _env.GetArrayLength(n) > 0 || _env.GetAssocLength(n) > 0;
					if (_env.IsAssoc(n)) return _env.HasAssocElement(n, sub);
					return _env.HasArrayElement(n, (int)_expander.EvalArithmetic(sub));
					}
				return _env.IsSet(val);
				}
			case "-o":
				return Options.LongOptions().Any(o => o.Name == val && o.On);
			case "-R": return false;
			}
		return FileTests.Unary(op, val) ?? throw new EvalException($"{op}: unary operator expected");
		}

	private bool EvalCondBinary(CondBinary b)
		{
		string left = _expander.ExpandToString(b.Left);
		switch (b.Op)
			{
			case "=" or "==":
				return Glob.Match(_expander.ExpandToPattern(b.Right), left, Options.NoCaseMatch);
			case "!=":
				return !Glob.Match(_expander.ExpandToPattern(b.Right), left, Options.NoCaseMatch);
			case "=~":
				{
				var pattern = _expander.ExpandToRegex(b.Right);
				System.Text.RegularExpressions.Match m;
				try
					{
					m = System.Text.RegularExpressions.Regex.Match(left, pattern,
						Options.NoCaseMatch ? System.Text.RegularExpressions.RegexOptions.IgnoreCase : System.Text.RegularExpressions.RegexOptions.None);
					}
				catch (ArgumentException) { throw new EvalException($"invalid regular expression: {pattern}"); }
				if (m.Success)
					{
					var groups = new List<string>();
					for (int i = 0; i < m.Groups.Count; i++) groups.Add(m.Groups[i].Value);
					_env.SetArrayFromList("BASH_REMATCH", groups);
					}
				else _env.SetArrayFromList("BASH_REMATCH", []);
				return m.Success;
				}
			}
		string right = _expander.ExpandToString(b.Right);
		switch (b.Op)
			{
			case "<": return string.Compare(left, right, StringComparison.Ordinal) < 0;
			case ">": return string.Compare(left, right, StringComparison.Ordinal) > 0;
			case "-eq": case "-ne": case "-lt": case "-le": case "-gt": case "-ge":
				{
				long l = _expander.EvalArithmetic(left.Length == 0 ? "0" : left);
				long r = _expander.EvalArithmetic(right.Length == 0 ? "0" : right);
				return b.Op switch
					{
					"-eq" => l == r, "-ne" => l != r, "-lt" => l < r,
					"-le" => l <= r, "-gt" => l > r, _ => l >= r,
					};
				}
			}
		return FileTests.Binary(b.Op, left, right) ?? throw new EvalException($"{b.Op}: binary operator expected");
		}

	// ── glob matching (case patterns + basic filename glob) ───────────────────

	public static bool GlobMatch(string pattern, string input) => Glob.Match(pattern, input);
	}

// ── redirect scope RAII ───────────────────────────────────────────────────────

internal sealed class RedirectScope : IDisposable
	{
	// Saved ConsoleMux *slots* (null = the real stream), restored on dispose.
	private System.IO.TextWriter? _savedOut;
	private System.IO.TextWriter? _savedErr;
	private System.IO.TextReader? _savedIn;
	private bool _outSaved, _errSaved, _inSaved;
	private System.IO.Stream? _savedRaw;
	private bool _rawSaved;
	private System.IO.Stream? _savedRawIn;
	private bool _rawInSaved;

	// The writers/readers we installed — must be disposed to release file handles.
	private readonly List<IDisposable> _installed = [];

	// Numbered descriptors (3+) this scope opened or replaced: restored on dispose.
	private readonly List<(Dictionary<int, System.IO.Stream> table, int fd, System.IO.Stream? previous, System.IO.Stream? opened, bool shared)> _fds = [];

	/// <summary>Install <paramref name="stream"/> as descriptor <paramref name="fd"/> (null
	/// closes it). The previous stream comes back when the scope is disposed; the new one
	/// is closed then, unless <paramref name="shared"/> (a dup of another descriptor).</summary>
	public void TrackFd(Dictionary<int, System.IO.Stream> table, int fd, System.IO.Stream? stream, bool shared = false)
		{
		table.TryGetValue(fd, out var prev);
		_fds.Add((table, fd, prev, stream, shared));
		if (stream is null)
			{
			// n>&- : close the descriptor now (its owner scope may be permanent)
			table.Remove(fd);
			if (prev is not null) { try { prev.Flush(); prev.Dispose(); } catch { } }
			}
		else table[fd] = stream;
		}

	public void SaveOut() { if (!_outSaved) { _savedOut = ConsoleMux.OutSlot; _outSaved = true; } }
	public void SaveErr() { if (!_errSaved) { _savedErr = ConsoleMux.ErrSlot; _errSaved = true; } }
	public void SaveIn()  { if (!_inSaved)  { _savedIn  = ConsoleMux.InSlot;  _inSaved  = true; } }
	public void SaveRaw() { if (!_rawSaved) { _savedRaw = ConsoleMux.Raw; _rawSaved = true; } }
	public void SaveRawIn() { if (!_rawInSaved) { _savedRawIn = ConsoleMux.RawIn; _rawInSaved = true; } }

	public void TrackInstalled(IDisposable d) => _installed.Add(d);
	public IDisposable LastInstalled => _installed[^1];

	public void Dispose()
		{
		if (_outSaved)
			{
			var current = ConsoleMux.Out;
			ConsoleMux.SetOut(_savedOut);
			_savedOut = null; _outSaved = false;
			try { current.Flush(); } catch { }
			}
		if (_errSaved)
			{
			ConsoleMux.SetErr(_savedErr);
			_savedErr = null; _errSaved = false;
			}
		if (_inSaved)
			{
			ConsoleMux.SetIn(_savedIn);
			_savedIn = null; _inSaved = false;
			}
		if (_rawSaved)
			{
			ConsoleMux.Raw = _savedRaw;
			_savedRaw = null; _rawSaved = false;
			}
		if (_rawInSaved)
			{
			ConsoleMux.RawIn = _savedRawIn;
			_savedRawIn = null; _rawInSaved = false;
			}
		// Dispose file streams after restoring Console — order matters so
		// the Flush above goes to the real console, not the file.
		foreach (var d in _installed)
			try { d.Dispose(); } catch { }
		_installed.Clear();
		// Restore numbered descriptors in reverse order of installation.
		for (int i = _fds.Count - 1; i >= 0; i--)
			{
			var (table, fd, prev, opened, shared) = _fds[i];
			if (opened is not null && !shared) { try { opened.Flush(); opened.Dispose(); } catch { } }
			if (prev is null) table.Remove(fd); else table[fd] = prev;
			}
		_fds.Clear();
		}
	}
