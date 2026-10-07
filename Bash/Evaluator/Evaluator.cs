using Microsoft.Win32.SafeHandles;
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
	public bool RemoveFunction(string name)
		{
		if (!_functions.ContainsKey(name)) return false;
		SaveFunctionsForSubshell();
		return _functions.Remove(name);
		}
	public bool HasFunction(string name) => _functions.ContainsKey(name);

	// ── isolation for in-process subshells ────────────────────────────────────
	// `( )`, `$( )` and `<( )` run on this interpreter with the variables snapshotted
	// (ShellEnvironment.TakeSnapshot). Everything else a subshell changes must vanish with it too,
	// as in bash: functions (a `rev` defined in one `$( )` replaced the utility for the rest of the
	// script, rev 109), options and shopts (`( set -e )` left -e on), traps and aliases.
	// Options are saved by value on entry (a few field reads). The tables are copy-on-write: saved
	// only on the first change inside a level, so a `$( )` that changes none allocates nothing.
	private int _subshellDepth;

	private sealed class SavedTables
		{
		public int Depth;
		public Dictionary<string, FunctionDef>? Functions;
		public Dictionary<string, string>? Traps, Aliases;
		public bool ExitTrapSet;   // the subshell set its own EXIT trap: it fires as the subshell ends
		}
	private readonly Stack<SavedTables> _savedTables = new();

	internal readonly struct SubshellMark
		{
		internal readonly int Depth;
		internal readonly ShellOptions.State Options;
		internal SubshellMark(int depth, ShellOptions.State options) { Depth = depth; Options = options; }
		}

	/// <summary>Enter an in-process subshell. Pair with <see cref="SubshellExitTrap"/> (while its
	/// output is still redirected) and then <see cref="LeaveSubshell"/>.</summary>
	internal SubshellMark EnterSubshell() => new(++_subshellDepth, Options.Save());

	/// <summary>Run the EXIT trap the subshell <paramref name="mark"/> set for itself, if any.
	/// An `exit` inside the trap propagates, as it would from the subshell's last command.</summary>
	internal void SubshellExitTrap(SubshellMark mark)
		{
		if (_savedTables.TryPeek(out var top) && top.Depth == mark.Depth && top.ExitTrapSet
		    && _traps.TryGetValue("EXIT", out var cmd) && cmd.Length > 0)
			{
			top.ExitTrapSet = false;   // once
			RunTrap(cmd);
			}
		}

	/// <summary>Leave the subshell <paramref name="mark"/>: put back the options it started with
	/// and every table it changed.</summary>
	internal void LeaveSubshell(SubshellMark mark)
		{
		if (_savedTables.TryPeek(out var top) && top.Depth == mark.Depth)
			{
			_savedTables.Pop();
			if (top.Functions is not null) Refill(_functions, top.Functions);
			if (top.Traps is not null)     Refill(_traps, top.Traps);
			if (top.Aliases is not null)   Refill(Aliases, top.Aliases);
			}
		Options.Restore(mark.Options);
		_subshellDepth = mark.Depth - 1;

		static void Refill<T>(Dictionary<string, T> table, Dictionary<string, T> saved)
			{
			table.Clear();
			foreach (var kv in saved) table[kv.Key] = kv.Value;
			}
		}

	private SavedTables? TablesForThisLevel()
		{
		if (_subshellDepth == 0) return null;
		if (_savedTables.TryPeek(out var top) && top.Depth == _subshellDepth) return top;
		var s = new SavedTables { Depth = _subshellDepth };
		_savedTables.Push(s);
		return s;
		}

	private void SaveFunctionsForSubshell()
		{
		if (TablesForThisLevel() is { } s) s.Functions ??= new(_functions, StringComparer.Ordinal);
		}

	/// <summary>Call before the `alias`/`unalias` builtins change <see cref="Aliases"/>.</summary>
	internal void SaveAliasesForSubshell()
		{
		if (TablesForThisLevel() is { } s) s.Aliases ??= new(Aliases, StringComparer.Ordinal);
		}

	private void SaveTrapsForSubshell(string sig)
		{
		if (TablesForThisLevel() is not { } s) return;
		s.Traps ??= new(_traps, StringComparer.Ordinal);
		if (sig == "EXIT") s.ExitTrapSet = true;
		}

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
	internal static BackgroundJob? CurrentJob => _currentJob;

	/// <summary>Command history, set by the REPL; used by the `history` builtin. Null in script mode.</summary>
	public Bash.IO.History? History { get; set; }

	// SIGINT (Ctrl+C) handling. The REPL's CancelKeyPress handler (or `timeout`) sets the flag from
	// another thread; loop/command boundaries check it and throw InterruptException. A builtin that
	// blocks (`sleep`, `tail -f`) WAITS on InterruptHandles instead of polling the flag (the
	// architect, 2026-10-04: never Thread.Sleep for synchronization).
	private volatile bool _interrupted;
	private readonly ManualResetEventSlim _interruptEvent = new(false);
	public void RequestInterrupt() { _interrupted = true;  _interruptEvent.Set(); }
	public void ClearInterrupt()   { _interrupted = false; _interruptEvent.Reset(); }
	public bool Interrupted => _interrupted || (_parent?.Interrupted ?? false);

	/// <summary>Signalled when this shell, or the shell whose pipeline it is a stage of, is
	/// interrupted. Wait on these, then call <see cref="CheckInterrupt"/>.</summary>
	internal WaitHandle[] InterruptHandles()
		{
		var list = new List<WaitHandle>(2);
		for (var e = this; e is not null; e = e._parent) list.Add(e._interruptEvent.WaitHandle);
		return [.. list];
		}

	// The shell whose pipeline this interpreter is a stage of (null for the shell itself)
	private readonly Evaluator? _parent;

	/// <summary>If a SIGINT is pending: run the INT trap if one is set (or ignore it
	/// when the trap is empty), otherwise throw InterruptException. Clears the flag.</summary>
	public void CheckInterrupt()
		{
		if (!_interrupted)
			{
			// a pipeline stage's own interpreter: an interrupt of the shell running the pipeline
			// (Ctrl+C, `timeout`) ends the stage, as SIGINT ends every process of the pipeline
			if (_parent is { Interrupted: true }) throw new InterruptException();
			return;
			}
		ClearInterrupt();
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

	public void SetTrap(string sig, string command) { SaveTrapsForSubshell(sig); _traps[sig] = command; }
	public void RemoveTrap(string sig) { SaveTrapsForSubshell(sig); _traps.Remove(sig); }
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
		SettleBackgroundJobs();   // every exit path passes here
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

	/// <summary>
	/// A pipeline stage's own interpreter. Bash runs every stage of a pipeline in a subshell, so
	/// nothing a stage sets -- variables, positionals, functions, options, traps, aliases --
	/// reaches the parent or a sibling stage. Stages used to share this shell's state on their
	/// threads: `echo x | read v` left v=x, and a `$( )` in one stage restored its snapshot over
	/// another stage's `$1` (lost parameters, lost output; DECISIONS 2026-10-03). ~2.5 µs a stage.
	/// Each has its own cwd (DECISIONS 2026-10-03). Jobs, getopts position and `exec` redirects
	/// start fresh, as in a bash subshell.
	/// Also a background job's own shell (`cmd &amp;`, DECISIONS 2026-10-05): then
	/// <paramref name="sharesInterrupts"/> is false, because an asynchronous job ignores the
	/// terminal's Ctrl+C in bash, while a pipeline stage ends with its shell.
	/// </summary>
	private Evaluator(Evaluator parent, bool sharesInterrupts = true)
		{
		_parent   = sharesInterrupts ? parent : null;
		_env      = parent._env.CloneForSubshell();
		Options   = parent.Options.Clone();
		_builtins = new Builtins(_env, this);
		_expander = new WordExpander(_env, this);
		_env.FlagsProvider = () => Options.FlagString();
		_env.ArithEval     = e => _expander.EvalArithmetic(e);
		_builtins.InheritFrom(parent._builtins);
		foreach (var kv in parent._functions) _functions[kv.Key] = kv.Value;
		foreach (var kv in parent.Aliases)    Aliases[kv.Key]    = kv.Value;
		foreach (var kv in parent._traps)     _traps[kv.Key]     = kv.Value;
		foreach (var kv in parent._fds)       _fds[kv.Key]       = kv.Value;
		foreach (var kv in parent._pathHash)  _pathHash[kv.Key]  = kv.Value;
		_funcStack.AddRange(parent._funcStack);
		_sourceStack.AddRange(parent._sourceStack);
		_errexitSuppress = parent._errexitSuppress;
		History = parent.History;
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

		int code;
		try
			{
			code = node switch
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
			}
		catch (RedirectException ex)
			{
			// this node's own redirect failed: it fails with status 1, as in bash, and whatever
			// encloses it carries on (it used to abandon the whole statement)
			if (!ex.Reported) Console.Error.WriteLine($"bash: {ex.Message}");
			code = 1;
			}

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
		var callerEnv = ShellEnvironment.Active;   // this thread runs THIS shell's code (its cwd) from here
		ShellEnvironment.Active = _env;
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
		finally { ShellEnvironment.Active = callerEnv; }
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
		catch (RedirectException ex) { if (!ex.Reported) Console.Error.WriteLine($"bash: {ex.Message}"); _env.LastExitCode = 1; return 1; }
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
	/// <summary>bash's job number: one past the highest still in the table, so once `wait` has
	/// emptied it the next job is %1 again. A counter that never reset made `kill %1` after a
	/// loop of jobs "no such job".</summary>
	private int NextJobId() => _jobs.Count == 0 ? 1 : _jobs.Max(j => j.Id) + 1;

	private void StartBackgroundJob(Node node)
		{
		var job = new BackgroundJob { Id = NextJobId(), Command = DescribeNode(node), Background = true };
		var stdio = ConsoleMux.Capture();   // the job writes wherever its parent was writing
		// Its own copy of the shell, taken NOW, as bash forks one at `&`. Jobs ran on this shell's own
		// state: `for f in *; do gzip "$f" & done` read $f after the loop had moved on (the last file,
		// every time), and a job's `cd` or assignment changed the parent (DECISIONS 2026-10-05).
		var shell = new Evaluator(this, sharesInterrupts: false);
		job.Shell = shell;
		job.Thread = new Thread(() =>
			{
			ConsoleMux.Apply(stdio);
			ShellEnvironment.Active = shell._env;
			_currentJob = job;
			try            { job.ExitCode = shell.Execute(node); }
			catch (ExitException ex) { job.ExitCode = ex.Code; }
			catch (InterruptException) { job.ExitCode = 143; }   // `kill %n`: 128 + SIGTERM
			catch          { job.ExitCode = 1; }
			finally        { job.Done = true; }
			}) { IsBackground = true };
		_jobs.Add(job);
		_env.LastBackgroundPid = job.Pid;
		job.Thread.Start();
		if (Options.Interactive) Console.Error.WriteLine($"[{job.Id}] {job.Pid}");
		}

	// ── children that outlive the shell (DECISIONS 2026-10-04, option b) ──────
	// Every external child goes into the kill-on-close job (P2), so a host that kills the shell on
	// a timeout takes its foreground work with it. A background job's children (`server &`) and
	// those started under `nohup`/`setsid` are left out of it and outlive the shell, as in bash.

	[ThreadStatic] private static bool t_detach;

	private static bool ChildOutlivesShell => t_detach || _currentJob is { Background: true };

	/// <summary>`nohup cmd [args]`: run it here, its children outliving the shell. GNU's terminal
	/// rules: stdin from a terminal reads /dev/null, stdout to a terminal appends to nohup.out, stderr
	/// to a terminal follows stdout. Under Claude Code none of them is a terminal.</summary>
	public int Nohup(List<string> args)
		{
		if (args.Count > 0 && args[0] == "--") args = args[1..];
		if (args.Count == 0) { Console.Error.WriteLine("nohup: missing operand\nTry 'nohup --help' for more information."); return 125; }
		bool inTty  = !ConsoleMux.InSwapped && ConsoleMux.PipeIn is null && ConsoleMux.RawIn is null && !Console.IsInputRedirected;
		bool outTty = !ConsoleMux.OutSwapped && ConsoleMux.PipeOut is null && !Console.IsOutputRedirected;
		bool errTty = !ConsoleMux.ErrSwapped && !Console.IsErrorRedirected;
		var redirects = new List<Redirect>();
		if (inTty) redirects.Add(new Redirect(0, RedirectKind.Input, Word.Literal("/dev/null")));
		if (outTty)
			{
			Console.Error.WriteLine(inTty ? "nohup: ignoring input and appending output to 'nohup.out'" : "nohup: appending output to 'nohup.out'");
			redirects.Add(new Redirect(1, RedirectKind.Append, Word.Literal("nohup.out")));
			}
		if (errTty) redirects.Add(new Redirect(2, RedirectKind.OutputDup, Word.Literal("1")));
		using var scope = redirects.Count > 0 ? ApplyRedirects(redirects) : null;
		return RunDetached(args[0], args[1..]);
		}

	/// <summary>`setsid [-w] [-f] cmd [args]`: its children outlive the shell. As in a bash without
	/// job control (a script, a Claude Code call) the command runs in the foreground; `-f` -- or an
	/// interactive shell -- starts it and returns at once, `-w` waits.</summary>
	public int Setsid(List<string> args)
		{
		bool fork = Options.Interactive, wait = false;
		while (args.Count > 0 && args[0].StartsWith('-') && args[0].Length > 1)
			{
			var a = args[0]; args = args[1..];
			if (a == "--") break;
			if (a is "-f" or "--fork") fork = true;
			else if (a is "-w" or "--wait") wait = true;
			else if (a is "-c" or "--ctty") { }
			else { Console.Error.WriteLine($"setsid: invalid option -- '{a.TrimStart('-')}'"); return 1; }
			}
		if (args.Count == 0) { Console.Error.WriteLine("setsid: no command specified"); return 1; }
		if (!fork || wait) return RunDetached(args[0], args[1..]);
		var job = new BackgroundJob { Id = 0, Command = string.Join(' ', args), Background = true };
		var stdio = ConsoleMux.Capture();
		var name = args[0]; var rest = args[1..];
		var shell = new Evaluator(this, sharesInterrupts: false);   // its own copy, like a `&` job
		job.Shell = shell;
		job.Thread = new Thread(() =>
			{
			ConsoleMux.Apply(stdio);
			ShellEnvironment.Active = shell._env;
			try { shell.RunAsJob(job, name, rest); } catch { }
			finally { job.Done = true; }
			}) { IsBackground = true };
		job.Thread.Start();
		AwaitSpawn(job, 1000);   // the program must exist before we return: the shell may exit next
		return 0;
		}

	private int RunDetached(string name, List<string> args)
		{
		var prev = t_detach;
		t_detach = true;
		try { return RunCommand(name, args); }
		finally { t_detach = prev; }
		}

	/// <summary>`disown [-a] [-r] [-h] [jobspec…]`: remove jobs from the table. A job's external
	/// children already outlive the shell (option b), so `-h` changes nothing here.</summary>
	public int Disown(List<string> args)
		{
		bool all = false, running = false, keep = false;
		var specs = new List<string>();
		foreach (var a in args)
			{
			if (a.StartsWith('-') && a.Length > 1 && !a.StartsWith("-%"))
				foreach (var c in a[1..]) { if (c == 'a') all = true; else if (c == 'r') running = true; else if (c == 'h') keep = true;
				                            else { Console.Error.WriteLine($"bash: disown: -{c}: invalid option"); return 2; } }
			else specs.Add(a);
			}
		if (keep) return 0;
		if (all || running) { _jobs.RemoveAll(j => !running || !j.Done); return 0; }
		if (specs.Count == 0)
			{
			if (_jobs.Count == 0) { Console.Error.WriteLine("bash: disown: current: no such job"); return 1; }
			_jobs.RemoveAt(_jobs.Count - 1);
			return 0;
			}
		int rc = 0;
		foreach (var s in specs)
			{
			var j = FindJob(s);
			if (j is null) { Console.Error.WriteLine($"bash: disown: {s}: no such job"); rc = 1; }
			else _jobs.Remove(j);
			}
		return rc;
		}

	/// <summary>Wait (bounded) until <paramref name="job"/> has started its external child, ended,
	/// or parked in `sleep`.</summary>
	private static void AwaitSpawn(BackgroundJob job, int budgetMs) => job.WaitSettled(budgetMs);

	/// <summary>At shell exit: bash has FORKED `server &` before its next line, so the process
	/// exists even if the shell exits at once. Here a job is a thread, so give any job that has not
	/// yet started its program a moment to do so (bounded, 1 s in all), or it would never start.
	/// A job parked in `sleep` is not waited for: it would cost the full second at every exit
	/// (measured 1.07 s vs 0.013 s for `sleep 30 &`). A builtin-only loop still can.</summary>
	private void SettleBackgroundJobs()
		{
		var sw = Stopwatch.StartNew();
		foreach (var j in _jobs.ToList())
			AwaitSpawn(j, (int)Math.Max(0, 1000 - sw.ElapsedMilliseconds));
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
		// the job's own shell first, so its loop does not go on to the next command; then the program
		// it is running, if any
		if (!job.Done) job.Shell?.RequestInterrupt();
		int pid = job.ChildPid;
		if (pid > 0)
			{
			try { System.Diagnostics.Process.GetProcessById(pid).Kill(true); } catch { }
			return 0;
			}
		if (job.Done || job.Shell is not null) return 0;
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
	/// in which case the caller reads <c>Console.In</c>. A text read of the same stdin may have
	/// read ahead (`{ read -r x; cat; } &lt; f`), so what it buffered and did not consume comes
	/// first -- the stream used to start past it and the rest of the file was lost (2026-10-03).</summary>
	public static System.IO.Stream? CurrentRawStdin()
		{
		var raw = ConsoleMux.RawIn
		       ?? (ConsoleMux.InSlot is null && Console.IsInputRedirected ? Console.OpenStandardInput() : null);
		if (raw is null) return null;
		if (ConsoleMux.In is LfReader lf && lf.TakeBuffered() is { Length: > 0 } pending)
			return new PrefixedStream(ShellEncoding.Utf8.GetBytes(pending), raw);
		return raw;
		}

	// ── pipeline ──────────────────────────────────────────────────────────────

	// CPU time of the external children this process has waited for (bash's `time` counts the
	// shell's own CPU plus its children's; Windows counts a process's own only). Ticks.
	private static long s_childUserTicks, s_childSysTicks;

	internal static void AddChildCpu(Process p)
		{
		try
			{
			System.Threading.Interlocked.Add(ref s_childUserTicks, p.UserProcessorTime.Ticks);
			System.Threading.Interlocked.Add(ref s_childSysTicks, p.PrivilegedProcessorTime.Ticks);
			}
		catch { }   // a process whose times cannot be read: counted as zero
		}

	/// <summary>`time [-p] pipeline`: run it, then report real/user/sys on the shell's stderr in
	/// TIMEFORMAT (bash's default when unset; nothing when set but empty). DECISIONS 2026-10-04.</summary>
	private int ExecTimedPipeline(Pipeline p)
		{
		var self = Process.GetCurrentProcess();
		var u0 = self.UserProcessorTime.Ticks + System.Threading.Interlocked.Read(ref s_childUserTicks);
		var s0 = self.PrivilegedProcessorTime.Ticks + System.Threading.Interlocked.Read(ref s_childSysTicks);
		var sw = Stopwatch.StartNew();
		int rc = ExecPipeline(p with { Timed = false });
		double real = sw.Elapsed.TotalSeconds;
		self.Refresh();
		double user = (self.UserProcessorTime.Ticks + System.Threading.Interlocked.Read(ref s_childUserTicks) - u0) / (double)TimeSpan.TicksPerSecond;
		double sys  = (self.PrivilegedProcessorTime.Ticks + System.Threading.Interlocked.Read(ref s_childSysTicks) - s0) / (double)TimeSpan.TicksPerSecond;
		string? fmt = p.TimePosix ? "real %2R\nuser %2U\nsys %2S"
		            : _env.IsSet("TIMEFORMAT") ? _env.Get("TIMEFORMAT")
		            : "\nreal\t%3lR\nuser\t%3lU\nsys\t%3lS";
		if (fmt.Length > 0)
			{
			var err = ConsoleMux.Err;
			string report;
			try { report = FormatTime(fmt, real, user, sys) + "\n"; }
			catch (TimeFormatException ex) { report = $"bash: TIMEFORMAT: `{ex.Char}': invalid format character\n"; }
			lock (err) { err.Write(report); err.Flush(); }
			}
		return rc;
		}

	private sealed class TimeFormatException(char c) : Exception { public char Char { get; } = c; }

	/// <summary>bash's TIMEFORMAT: `%%`, and `%[p][l]R|U|S` (p = 0-3 decimals, default 3; l = MmSS.FFFs),
	/// `%P` = CPU percentage (user+sys)/real. Fractions are truncated, as bash does.</summary>
	private static string FormatTime(string fmt, double real, double user, double sys)
		{
		var sb = new System.Text.StringBuilder();
		for (int i = 0; i < fmt.Length; i++)
			{
			char c = fmt[i];
			if (c != '%' || i + 1 >= fmt.Length) { sb.Append(c); continue; }
			int j = i + 1;
			if (fmt[j] == '%') { sb.Append('%'); i = j; continue; }
			int prec = 3; bool lng = false;
			if (char.IsAsciiDigit(fmt[j])) { prec = Math.Min(3, fmt[j] - '0'); j++; }
			if (j < fmt.Length && fmt[j] == 'l') { lng = true; j++; }
			if (j >= fmt.Length) { sb.Append(fmt, i, fmt.Length - i); break; }
			double? v = fmt[j] switch { 'R' => real, 'U' => user, 'S' => sys, _ => null };
			if (fmt[j] == 'P')
				{
				sb.Append((real > 0 ? (user + sys) * 100 / real : 0).ToString("F2", System.Globalization.CultureInfo.InvariantCulture));
				i = j;
				continue;
				}
			if (v is null) throw new TimeFormatException(fmt[j]);   // bash: an error, and no report
			sb.Append(lng ? LongSeconds(v.Value, prec) : Truncated(v.Value, prec));
			i = j;
			}
		return sb.ToString();
		}

	private static string Truncated(double seconds, int prec)
		{
		double scale = Math.Pow(10, prec);
		double t = Math.Floor(Math.Max(0, seconds) * scale) / scale;
		return t.ToString("F" + prec, System.Globalization.CultureInfo.InvariantCulture);
		}

	private static string LongSeconds(double seconds, int prec)
		{
		seconds = Math.Max(0, seconds);
		long minutes = (long)(seconds / 60);
		return $"{minutes}m{Truncated(seconds - minutes * 60, prec)}s";
		}

	private int ExecPipeline(Pipeline p)
		{
		if (p.Timed) return ExecTimedPipeline(p);
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
	/// external producer (ExecExternal kills it). Each stage runs on its OWN interpreter, a
	/// copy of this shell's state, as bash runs each stage in a subshell (DECISIONS 2026-10-03).
	/// </summary>
	private int ExecPipelineThreaded(Pipeline p)
		{
		var stages = p.Commands;
		int n = stages.Count;
		var pipes = new PipeBuffer[n - 1];
		for (int i = 0; i < n - 1; i++) pipes[i] = new PipeBuffer();
		var shells = new Evaluator[n];   // built here, before any stage runs: the parent is idle
		for (int i = 0; i < n; i++) shells[i] = new Evaluator(this);

		var exitCodes = new int[n];
		var threads   = new Thread[n];
		var parent    = ConsoleMux.Capture();
		var utf8      = ShellEncoding.Utf8;
		var job       = _currentJob;   // `a | b &`, `nohup f` (f runs a pipeline): every stage's children outlive the shell
		var detach    = t_detach;

		for (int i = 0; i < n; i++)
			{
			int idx = i;
			var (stageNode, stderrToo) = stages[idx];
			threads[idx] = new Thread(() =>
				{
				ConsoleMux.Apply(parent);
				ShellEnvironment.Active = shells[idx]._env;   // the stage's own cwd for every relative path it resolves
				_currentJob = job;
				t_detach = detach;
				TextReader? reader = null;
				System.IO.StreamWriter? writer = null;
				if (idx > 0)
					{
					// LF-only lines by block scan, one buffer for every command of this stage
					reader = new LfReader(pipes[idx - 1].ReadEnd);
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

				try { exitCodes[idx] = shells[idx].Execute(stageNode); }
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
		// [key]=value elements target an associative (or explicit-index) array. The key may span
		// word parts -- ["$k"]=v, [$i]=v -- so each element is split structurally, then the key
		// and the value are expanded separately (a key containing "]=" stays intact).
		if (ac.Values.Count > 0 && ac.Values.All(w => w.Parts.Count > 0 && w.Parts[0] is LiteralPart lp && lp.Value.StartsWith('[')
		                                              && w.TrySplitIndexedAssignment(1, out _, out _)))
			{
			if (!ac.Append && !_env.IsAssoc(ac.ArrayName)) _env.SetArrayFromList(ac.ArrayName, []);
			foreach (var w in ac.Values)
				{
				w.TrySplitIndexedAssignment(1, out var keyWord, out var valWord);
				var key = _expander.ExpandToString(keyWord); var val = _expander.ExpandToString(valWord);
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

	/// <summary>A command word that is not a plain literal, expanded like any word: its first field is
	/// the command, the rest lead <paramref name="args"/>. Out of line, so the hot path stays small.</summary>
	[System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
	private string ExpandCommandWord(Word name, List<string> args, out bool vanished)
		{
		var fields = _expander.ExpandToFields(name);
		vanished = fields.Count == 0;
		if (vanished) return "";
		for (int i = 1; i < fields.Count; i++) args.Add(fields[i]);
		return fields[0];
		}

	/// <summary>The command word and every argument expanded to nothing: no command runs, but its
	/// redirects are still made, as for a command with no name.</summary>
	[System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
	private int NoCommandLeft(SimpleCommand cmd)
		{
		if (cmd.Redirects.Count > 0) { using var _ = ApplyRedirects(cmd.Redirects); }
		return _expander.LastSubstStatus ?? 0;
		}

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

		// The command word is expanded like any other word -- field splitting, "$@", globs, braces --
		// and its first field is the command: `f() { "$@"; }`, `c="ls -l"; $c`. It was expanded as ONE
		// string, so those ran "echo a b" as a command name (every build back to rev 80). A plain
		// literal name, nearly every command, keeps the cheap path.
		var args = new List<string>();
		bool nameVanished = false;   // `$empty` / `"$@"` with no arguments: the first argument is the command
		string namStr = cmd.Name.IsPlainLiteral   // cached on the node: loops reuse it
			? _expander.ExpandToString(cmd.Name)
			: ExpandCommandWord(cmd.Name, args, out nameVanished);

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
		if (nameVanished)
			{
			if (args.Count == 0) return NoCommandLeft(cmd);
			namStr = args[0];
			args.RemoveAt(0);
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
		// Bash's lookup order: function, then builtin, then PATH. A function named after a builtin
		// utility (`rev(){ ...; }`) must shadow it, as `type` (Classify) already reports; until
		// 2026-10-04 the builtin was tried first and the function never ran.
		_functions.TryGetValue(namStr, out var fn);
		if (fn is not null || Builtins.Has(namStr))
			{
			using var redirectScope = cmd.Redirects.Count > 0 ? ApplyRedirects(cmd.Redirects) : null;   // no redirects: no scope (measured: ~72 B + 3 objects per command)
			using var tempScope = ApplyTempAssignments(tempAssign);
			if (fn is not null) return ExecFunction(fn, args);
			return _builtins.TryExecute(namStr, args, out int builtinCode) ? builtinCode : 127;
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
		child._env.AdoptCwd(_env);   // starts where we are; its `cd` must not move us (it did, 2026-10-03)
		child._env.Set("PWD", child._env.Cwd);
		try   { return child.RunString(ShellEncoding.ReadAllText(path)); }
		catch (ExitException ex) { return ex.Code; }
		finally { _env.ReassertProcessCwd(); }   // the child may have moved the process cwd we own
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
				{
				// in-process, so it takes its redirects and `VAR=x` the way a builtin does. Both were
				// dropped: `./s.sh > out` printed to the terminal, `FOO=1 ./s.sh` saw no FOO (2026-10-04)
				using var scope = redirects.Count > 0 ? ApplyRedirects(redirects) : null;
				using var temp  = ApplyTempAssignments(tempAssign);
				return RunScriptInProcess(name, scriptPath, args);
				}
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
			WorkingDirectory = _env.Cwd,   // this shell's own cwd (a pipeline stage's may differ from the process's)
			};

		foreach (var arg in args)
			psi.ArgumentList.Add(arg);

		if (clearEnv) psi.Environment.Clear();
		else
			foreach (var (k, v) in _env.GetExportedVars())
				psi.Environment[k] = v;
		foreach (var (k, v) in tempAssign)
			psi.Environment[k] = v;

		// ── the child's std handles (DECISIONS 2026-10-04: the real file handle) ─────────────
		// Each of fd 0-2 starts where this shell's own stream points NOW -- the newest installation
		// wins: a `$( )` or `{ } > f` inside a pipeline stage used to lose to the stage's pipe, so
		// `echo "$(ext)" | cat` captured nothing (found 2026-10-04). The writers are taken HERE: the
		// console slots are per thread, and on a pump thread `Console.Error` is the real stderr.
		// The redirects then apply
		// LEFT TO RIGHT as bash's dup2 applies them: `2>&1 >f` leaves stderr on the OLD stdout. A file
		// or /dev/null is a handle the child holds itself, so it outlives the shell and another
		// command can read it meanwhile. Only an in-process end -- a pipeline stage, a `$( )` capture,
		// an in-process `{ } > f`, a here-doc, an `exec 3>` stream -- needs a pipe and a pump here.
		var opened    = new List<SafeFileHandle>();   // files and NUL opened here; closed once the child holds its own
		var handedOff = new List<FileStream>();       // the shell's own redirect files the child writes too
		SafeFileHandle? nul = null;
		SafeFileHandle Nul() => nul ??= Keep(ChildLauncher.Open("NUL", ChildLauncher.Access.Null));
		SafeFileHandle Keep(SafeFileHandle h) { opened.Add(h); return h; }

		// The shell's own file redirect (`nohup cmd > log`: the redirect belongs to the builtin;
		// `{ ext; } > f`; `exec 3>f`) is handed to the child as its OWN append handle to that file, not
		// pumped: `nohup server > log 2>&1 &` must outlive the shell. The shell flushes first and moves
		// to the end after the child, so `{ echo a; ext; echo b; } > f` keeps a, ext, b in order.
		ChildFd? HandOff(FileStream fs, TextWriter? text)
			{
			try
				{
				text?.Flush(); fs.Flush();
				var h = Keep(ChildLauncher.Open(fs.Name, ChildLauncher.Access.Append));
				handedOff.Add(fs);
				return new HandleFd(h);
				}
			catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ObjectDisposedException)
				{ return null; }   // keep the pump
			}
		// Where an in-process writer leads, for a child. Judged by the WRITER, not ConsoleMux.Raw: `1>&2`
		// and `>/dev/stderr` swap the writer and leave Raw on an outer file.
		ChildFd Destination(TextWriter text, Stream? raw)
			{
			if (ReferenceEquals(text, TextWriter.Null)) return new HandleFd(Nul());   // in-process > /dev/null
			var sw = text as StreamWriter;
			if (sw?.BaseStream is FileStream fs && HandOff(fs, sw) is { } f) return f;
			if (sw is not null && raw is not null && !ConsoleMux.Capturing && ReferenceEquals(sw.BaseStream, raw))
				return new OutPumpFd(raw, null, ReferenceEquals(raw, ConsoleMux.PipeOut));   // bytes: a pipeline stage
			return new OutPumpFd(null, text, false);                                       // text: a `$( )` capture
			}

		var fds = new ChildFd[3];
		fds[0] = CurrentStdin();
		fds[1] = ConsoleMux.OutSwapped ? Destination(ConsoleMux.Out, ConsoleMux.Raw) : new InheritFd(1);
		fds[2] = !ConsoleMux.ErrSwapped ? new InheritFd(2)
		       : ReferenceEquals(ConsoleMux.Err, ConsoleMux.Out) ? fds[1]   // in-process 2>&1, or `|&`: one destination, one pipe
		       : Destination(ConsoleMux.Err, null);

		ChildFd OpenTarget(string raw, string target, ChildLauncher.Access how)
			{
			try { return new HandleFd(Keep(ChildLauncher.Open(target, how))); }
			catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
				{ throw new RedirectException($"{raw}: {RedirectMessage(target, ex)}"); }
			}
		ChildFd OutputTarget(string raw, string target, bool append, bool clobber)
			{
			if (raw == "/dev/null")   return new HandleFd(Nul());
			if (raw == "/dev/stdout") return fds[1];
			if (raw == "/dev/stderr") return fds[2];
			if (Options.NoClobber && !append && !clobber && File.Exists(target))
				throw new RedirectException($"{raw}: cannot overwrite existing file");
			return OpenTarget(raw, target, append ? ChildLauncher.Access.Append : ChildLauncher.Access.Truncate);
			}

		try
			{
			foreach (var r in redirects)
				{
				if (r.Kind is RedirectKind.Heredoc or RedirectKind.HeredocStrip)
					{ fds[0] = new InPumpFd(null, null, _expander.ExpandToString(r.Target)); continue; }
				if (r.Kind == RedirectKind.HereString)
					{ fds[0] = new InPumpFd(null, null, _expander.ExpandToString(r.Target) + "\n"); continue; }

				var raw    = _expander.ExpandToString(r.Target);
				var target = ShellEnvironment.TranslatePath(raw);
				int fd     = r.Fd ?? (r.Kind is RedirectKind.Input or RedirectKind.InputDup or RedirectKind.ReadWrite ? 0 : 1);
				switch (r.Kind)
					{
					case RedirectKind.Input:
					case RedirectKind.ReadWrite:
						{
						var t = raw == "/dev/null" ? new HandleFd(Nul())
						      : OpenTarget(raw, target, r.Kind == RedirectKind.ReadWrite ? ChildLauncher.Access.ReadWrite : ChildLauncher.Access.Read);
						if (fd <= 2) fds[fd] = t;   // a Windows child has fds 0-2 only
						break;
						}
					case RedirectKind.OutputBoth:
					case RedirectKind.AppendBoth:
						fds[1] = OutputTarget(raw, target, r.Kind == RedirectKind.AppendBoth, clobber: false);
						fds[2] = fds[1];
						break;
					case RedirectKind.Output:
					case RedirectKind.Clobber:
					case RedirectKind.Append:
						{
						var t = OutputTarget(raw, target, r.Kind == RedirectKind.Append, r.Kind == RedirectKind.Clobber);
						if (fd <= 2) fds[fd] = t;   // `3>f` still creates f, as in bash
						break;
						}
					case RedirectKind.OutputDup:
					case RedirectKind.InputDup:
						{
						if (fd > 2) break;
						// the target of a dup is an fd number or "-": test the RAW word (see ApplyRedirects)
						if (raw == "-") { fds[fd] = new HandleFd(Nul()); break; }   // n>&- closes n; NUL is the nearest a Windows child has
						if (!int.TryParse(raw, out int src)) throw new RedirectException($"{raw}: ambiguous redirect");
						if (src <= 2) { fds[fd] = fds[src]; break; }
						if (!_fds.TryGetValue(src, out var s) || s is null) throw new RedirectException($"{src}: Bad file descriptor");
						fds[fd] = fd == 0 ? new InPumpFd(s, null, null)
						        : (s is FileStream sfs ? HandOff(sfs, null) : null) ?? new OutPumpFd(s, null, false);
						break;
						}
					}
				}
			}
		catch (RedirectException ex)
			{
			// reported where this command's stderr points by then, as bash does in the child after
			// fork (so `cmd 2>/dev/null >/no/dir/f` is silent), and the command does not run
			ReportTo(fds[2], $"bash: {ex.Message}");
			foreach (var h in opened) h.Dispose();
			return 1;
			}

		// one pipe per distinct in-process end: `2>&1` into a capture is ONE pipe, so the order holds
		var pipes = new Dictionary<ChildFd, (SafeFileHandle Shell, SafeFileHandle Child)>();
		IntPtr HandleFor(ChildFd f) => f switch
			{
			InheritFd i => ChildLauncher.StdHandle(i.Fd),
			HandleFd h  => h.Handle.DangerousGetHandle(),
			_           => (pipes.TryGetValue(f, out var p) ? p : pipes[f] = ChildLauncher.Pipe(childReads: f is InPumpFd)).Child.DangerousGetHandle(),
			};

		Process proc;
		bool hideConsole = HiddenConsole.NeedsHiding;   // console-less shell: no window per child
		// A child that outlives the shell leaves any C#Bash job above us (a nested shell), and gets a
		// hidden console of its OWN unless ours is a real terminal: a hidden console's host dies with
		// the job of the shell that made it (DECISIONS 2026-10-05).
		bool detached   = ChildOutlivesShell;
		bool ownConsole = detached && HiddenConsole.ConsoleMayDieWithAShell;
		try
			{
			proc = ChildLauncher.Start(psi, HandleFor(fds[0]), HandleFor(fds[1]), HandleFor(fds[2]),
			                           noWindow: hideConsole || ownConsole, breakaway: detached);
			}
		catch (System.ComponentModel.Win32Exception ex)
			{
			// bash-style diagnostics, routed to wherever this command's stderr was sent
			// (so `cmd 2>/dev/null` really is silent)
			bool denied = ex.NativeErrorCode is 5 or 193;
			ReportTo(fds[2], ex.NativeErrorCode is 2 or 3 ? $"bash: {name}: command not found"
			                 : denied ? $"bash: {name}: Permission denied" : $"bash: {name}: {ex.Message}");
			foreach (var p in pipes.Values) { p.Shell.Dispose(); p.Child.Dispose(); }
			foreach (var h in opened) h.Dispose();
			return denied ? 126 : 127;
			}
		finally
			{
			foreach (var p in pipes.Values) p.Child.Dispose();   // the child holds its own pipe ends: EOF reaches us when it exits
			}

		if (_currentJob is not null) _currentJob.ChildPid = proc.Id;
		if (!detached) ChildJobs.Attach(proc);   // dies with us (host timeout kills) - best effort
		if (hideConsole && !detached) HiddenConsole.Adopt(proc);   // never share a detached child's console

		bool brokenPipe = false;
		var pumps = new List<Thread>(2);
		try
			{
			foreach (var (end, pipe) in pipes)
				{
				var shellEnd = new FileStream(pipe.Shell, end is InPumpFd ? FileAccess.Write : FileAccess.Read, 4096, false);
				Thread t = end switch
					{
					InPumpFd i  => new Thread(() => FeedChild(i, shellEnd)),
					OutPumpFd o => new Thread(() => { if (DrainChild(o, shellEnd)) { brokenPipe = true; try { proc.Kill(true); } catch { } } }),
					_           => throw new InvalidOperationException(),
					};
				t.IsBackground = true;
				t.Start();
				if (end is OutPumpFd) pumps.Add(t);   // a feeder may block on a child that stopped reading: not joined
				}

			proc.WaitForExit();
			foreach (var t in pumps) t.Join();
			foreach (var fs in handedOff) { try { fs.Seek(0, SeekOrigin.End); } catch { } }   // after what the child wrote
			AddChildCpu(proc);   // for `time`'s user/sys
			return brokenPipe ? 141 : proc.ExitCode;
			}
		finally
			{
			if (_currentJob is not null) _currentJob.ChildPid = 0;
			// The files are closed only now, after the child: the last close of a file it rewrote,
			// done by the child's exit, cost +0.65 ms a launch (measured, AOT, best of 21).
			foreach (var h in opened) h.Dispose();
			}
		}

	// ── a child's std handle: where it points ───────────────────────────────────────────
	private abstract record ChildFd;
	/// <summary>This shell's own std handle (0, 1 or 2), passed straight to the child.</summary>
	private sealed record InheritFd(int Fd) : ChildFd;
	/// <summary>A file or NUL opened for this command.</summary>
	private sealed record HandleFd(SafeFileHandle Handle) : ChildFd;
	/// <summary>An in-process destination: bytes into <c>Raw</c> (a pipeline stage when
	/// <c>PipeStage</c>: a closed reader is SIGPIPE), else text into <c>Text</c>.</summary>
	private sealed record OutPumpFd(Stream? Raw, TextWriter? Text, bool PipeStage) : ChildFd;
	/// <summary>An in-process source: bytes from <c>Raw</c>, text from <c>Text</c>, or a literal (a here-doc).</summary>
	private sealed record InPumpFd(Stream? Raw, TextReader? Text, string? Literal) : ChildFd;

	/// <summary>The child's stdin by default: a pipeline stage's pipe, an in-process `&lt; file` or
	/// here-doc, or this process's own stdin -- with whatever a `read` before us buffered and did
	/// not consume first. That used to be lost: `printf 'a\nb\n' | { read x; ext; }` gave ext nothing.</summary>
	private static ChildFd CurrentStdin()
		{
		var lf = ConsoleMux.In as LfReader;
		var pending = lf?.TakeBuffered();
		Stream Prefixed(Stream raw) => pending is { Length: > 0 } ? new PrefixedStream(ShellEncoding.Utf8.GetBytes(pending), raw) : raw;
		if (ConsoleMux.RawIn is { } raw) return new InPumpFd(Prefixed(raw), null, null);
		if (ConsoleMux.InSwapped)        return new InPumpFd(null, ConsoleMux.In, null);   // a text source
		if (pending is { Length: > 0 })  return new InPumpFd(Prefixed(Console.OpenStandardInput()), null, null);
		return new InheritFd(0);
		}


	/// <summary>Write a diagnostic line to where a child's fd points (the child never ran).</summary>
	private static void ReportTo(ChildFd f, string line)
		{
		try
			{
			switch (f)
				{
				case InheritFd { Fd: 1 }: Console.Out.WriteLine(line); break;
				case InheritFd:           Console.Error.WriteLine(line); break;
				case HandleFd h:          ChildLauncher.Write(h.Handle, ShellEncoding.Utf8.GetBytes(line + "\n")); break;
				case OutPumpFd { Raw: { } raw }: { var b = ShellEncoding.Utf8.GetBytes(line + "\n"); raw.Write(b, 0, b.Length); raw.Flush(); break; }
				case OutPumpFd { Text: { } text }: lock (text) { text.WriteLine(line); text.Flush(); } break;
				}
			}
		catch { }
		}

	/// <summary>Copy an in-process source into the child's stdin, then close it (EOF).</summary>
	private static void FeedChild(InPumpFd src, FileStream toChild)
		{
		try
			{
			if (src.Literal is not null)
				{
				var bytes = ShellEncoding.Utf8.GetBytes(src.Literal);
				toChild.Write(bytes, 0, bytes.Length);
				}
			else if (src.Raw is not null) src.Raw.CopyTo(toChild);
			else if (src.Text is not null)
				{
				using var w = new StreamWriter(toChild, ShellEncoding.Utf8, 4096, leaveOpen: true);
				var buf = new char[4096];
				int n;
				while ((n = src.Text.Read(buf, 0, buf.Length)) > 0) { w.Write(buf, 0, n); w.Flush(); }
				}
			}
		catch { }   // the child stopped reading
		try { toChild.Dispose(); } catch { }
		}

	/// <summary>Copy the child's output into an in-process destination until EOF. Returns true when
	/// the destination is a pipeline stage whose reader has gone (SIGPIPE).</summary>
	private static bool DrainChild(OutPumpFd dest, FileStream fromChild)
		{
		try
			{
			if (dest.Raw is not null)
				{
				var buf = new byte[8192];
				int n;
				while ((n = fromChild.Read(buf, 0, buf.Length)) > 0) dest.Raw.Write(buf, 0, n);
				dest.Raw.Flush();
				}
			else if (dest.Text is not null)
				{
				// byte-transparent text: the shell's own encoding (DECISIONS 2026-10-03)
				using var r = new StreamReader(fromChild, ShellEncoding.Utf8, false, 4096, leaveOpen: true);
				var buf = new char[4096];
				int n;
				while ((n = r.Read(buf, 0, buf.Length)) > 0)
					lock (dest.Text) { dest.Text.Write(buf, 0, n); dest.Text.Flush(); }
				}
			}
		catch (BrokenPipeException) when (dest.PipeStage) { return true; }
		catch { }
		finally { try { fromChild.Dispose(); } catch { } }
		return false;
		}

	// ── redirects ─────────────────────────────────────────────────────────────

	/// <summary>bash wording for a redirect that could not be opened: a directory target reports
	/// "Is a directory" rather than the platform permission error it actually raises.</summary>
	private static string RedirectMessage(string target, Exception ex)
		{
		try { if (Directory.Exists(target)) return "Is a directory"; } catch { }
		return Builtins.IoError(ex);
		}

	/// <summary>A redirect of this command failed: report it NOW, on the stderr in effect with this
	/// command's earlier redirects still applied (so `cmd 2>/dev/null >/bad` is silent, as in bash),
	/// then undo them. Execute fails just this command with status 1 and the script carries on;
	/// an unhandled throw used to abandon the whole statement, e.g. the rest of a `{ }` group.</summary>
	private static RedirectException RedirectFailed(RedirectScope scope, string message)
		{
		try { Console.Error.WriteLine($"bash: {message}"); } catch { }
		scope.Dispose();
		return new RedirectException(message, reported: true);
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
					try { fs = append ? ShellFile.Append(target) : ShellFile.Create(target); }
					catch (Exception ex) { throw RedirectFailed(scope, $"{rawTarget}: {RedirectMessage(target, ex)}"); }
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
						try { nfs = append ? ShellFile.Append(target) : ShellFile.Create(target); }
						catch (Exception ex) { throw RedirectFailed(scope, $"{rawTarget}: {RedirectMessage(target, ex)}"); }
						scope.TrackFd(_fds, fd, nfs);
						break;
						}

					if (Options.NoClobber && r.Kind == RedirectKind.Output && File.Exists(target))
						{ throw RedirectFailed(scope, $"{rawTarget}: cannot overwrite existing file"); }
					System.IO.FileStream fs;
					try { fs = append ? ShellFile.Append(target) : ShellFile.Create(target); }
					catch (Exception ex) { throw RedirectFailed(scope, $"{rawTarget}: {RedirectMessage(target, ex)}"); }
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
						try { ifs = ShellFile.OpenRead(target); }
						catch (Exception ex) { throw RedirectFailed(scope, $"{rawTarget}: {RedirectMessage(target, ex)}"); }
						scope.TrackFd(_fds, ifd, ifs);
						break;
						}
					scope.SaveIn(); scope.SaveRawIn();
					if (rawTarget == "/dev/null") { ConsoleMux.SetIn(new System.IO.StringReader("")); ConsoleMux.RawIn = System.IO.Stream.Null; break; }
					System.IO.FileStream inFs;
					try { inFs = ShellFile.OpenRead(target); }
					catch (Exception ex) { throw RedirectFailed(scope, $"{rawTarget}: {RedirectMessage(target, ex)}"); }
					var reader = new LfReader(inFs);   // buffers nothing until first read; no BOM sniffing; LF-only lines
					scope.TrackInstalled(reader);
					ConsoleMux.SetIn(reader);
					ConsoleMux.RawIn = inFs;   // byte builtins read the file directly
					break;
					}
				case RedirectKind.OutputDup:
					{
					int ofd = r.Fd ?? 1;
					// the target of a dup is an fd number or "-", never a path: test the RAW word (a
					// pipeline stage that changed directory resolves paths, which turned "1" into
					// "<cwd>/1" -- "ambiguous redirect", caught by the suite 2026-10-03)
					if (rawTarget == "-")
						{
						// n>&- closes n
						if (ofd == 1) { scope.SaveOut(); scope.SaveRaw(); ConsoleMux.SetOut(System.IO.TextWriter.Null); ConsoleMux.Raw = System.IO.Stream.Null; }
						else if (ofd == 2) { scope.SaveErr(); ConsoleMux.SetErr(System.IO.TextWriter.Null); }
						else scope.TrackFd(_fds, ofd, null);
						break;
						}
					if (!int.TryParse(rawTarget, out int src)) { throw RedirectFailed(scope, $"{rawTarget}: ambiguous redirect"); }
					if (ofd == 1 && src == 2) { scope.SaveOut(); ConsoleMux.SetOut(ConsoleMux.Err); break; }          // 1>&2
					if (ofd == 2 && src == 1) { scope.SaveErr(); ConsoleMux.SetErr(ConsoleMux.Out); break; }          // 2>&1
					if (src > 2)
						{
						if (!_fds.TryGetValue(src, out var s)) { throw RedirectFailed(scope, $"{src}: Bad file descriptor"); }
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
					if (rawTarget == "-")   // an fd number or "-", never a path (see OutputDup)
						{
						if (ifd == 0) { scope.SaveIn(); scope.SaveRawIn(); ConsoleMux.SetIn(new System.IO.StringReader("")); ConsoleMux.RawIn = System.IO.Stream.Null; }
						else scope.TrackFd(_fds, ifd, null);
						break;
						}
					if (!int.TryParse(rawTarget, out int src)) { throw RedirectFailed(scope, $"{rawTarget}: ambiguous redirect"); }
					if (ifd == 2 && src == 1) { scope.SaveErr(); ConsoleMux.SetErr(ConsoleMux.Out); break; }          // 2<&1 (defensive)
					if (ifd == 0 && src > 2)
						{
						if (!_fds.TryGetValue(src, out var s)) { throw RedirectFailed(scope, $"{src}: Bad file descriptor"); }
						var rd = new LfReader(s, leaveOpen: true);
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
		// In-process subshell (no fork on Windows): variables, cwd, positionals, attributes,
		// functions, options, traps and aliases are restored, and `exit` ends only the subshell.
		// An EXIT trap it set fires as it ends, inside its own redirects.
		var snap = _env.TakeSnapshot();
		var mark = EnterSubshell();
		try
			{
			using var scope = ss.Redirects.Count > 0 ? ApplyRedirects(ss.Redirects) : null;
			try   { return Execute(ss.Body); }
			finally { SubshellExitTrap(mark); }
			}
		catch (ExitException ex) { return ex.Code; }
		catch (FatalShellException ex) { Console.Error.WriteLine($"bash: {ex.Message}"); return ex.Code; }   // fatal to the subshell only
		finally { _env.RestoreSnapshot(snap); LeaveSubshell(mark); }
		}

	private int ExecFunctionDef(FunctionDef fd)
		{
		if (_env.IsReadonly(fd.Name) && _functions.ContainsKey(fd.Name))
			throw new EvalException($"{fd.Name}: readonly function");
		SaveFunctionsForSubshell();
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
