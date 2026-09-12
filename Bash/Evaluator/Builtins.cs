namespace Bash.Evaluator;

/// <summary>
/// Implements shell builtins.  Each method returns an exit code (0 = success).
/// The evaluator calls <see cref="TryExecute"/> first; if it returns false
/// the command is an external process.
/// </summary>
public sealed partial class Builtins(ShellEnvironment env, Evaluator eval)
	{
	private readonly ShellEnvironment _env = env;
	private readonly Evaluator _eval = eval;

	// ── dispatch ──────────────────────────────────────────────────────────────

	/// <summary>All builtin names, for tab completion. Keep in sync with the dispatch switch.</summary>
	public static readonly IReadOnlyList<string> Names =
		[
		"echo", "printf", "cd", "pwd", "export", "unset", "set", "shift", "read",
		"true", "false", ":", "exit", "return", "break", "continue", "source", ".",
		"local", "declare", "typeset", "eval", "type", "command", "test", "[",
		"sleep", "env", "history", "trap", "jobs", "wait", "fg", "bg",
		"shopt", "alias", "unalias", "builtin", "readonly", "exec", "let",
		"pushd", "popd", "dirs", "mapfile", "readarray", "getopts", "yes",
		"basename", "dirname", "seq", "mkdir", "cat", "head", "tail", "wc", "rev", "tac",
		"tr", "cut", "uniq", "nl", "fold",
		"touch", "rmdir", "cmp", "tee", "comm", "paste", "rm", "mv", "cp",
		"uname", "hostname", "factor", "cal", "date", "kill", "expr", "du", "od", "sort", "split", "find", "ls", "xargs", "diff", "hash",
		"grep", "egrep", "fgrep", "which", "sed",
		"base64", "md5sum", "sha1sum", "sha256sum", "sha512sum", "hexdump",
		"stat", "mktemp", "realpath", "readlink", "chmod", "ln", "truncate",
		"timeout", "id", "whoami", "nproc", "printenv", "tty", "arch", "awk",
		"pgrep", "pkill", "ps",
		];

	/// <summary>The in-process coreutils (as opposed to shell builtins): these follow the
	/// unsupported-option policy (DECISIONS 2026-09-04 #2) and can defer to a PATH external.</summary>
	public static readonly HashSet<string> CoreutilNames =
		[
		"basename", "dirname", "seq", "mkdir", "cat", "head", "tail", "wc", "rev", "tac",
		"tr", "cut", "uniq", "nl", "fold", "touch", "rmdir", "cmp", "tee", "comm", "paste", "rm", "mv", "cp",
		"uname", "hostname", "factor", "cal", "date", "kill", "expr", "du", "od", "sort", "split", "find", "ls", "xargs", "diff",
		"grep", "egrep", "fgrep", "which", "sed", "yes", "sleep", "env",
		"base64", "md5sum", "sha1sum", "sha256sum", "sha512sum", "hexdump",
		"stat", "mktemp", "realpath", "readlink", "chmod", "ln", "truncate",
		"timeout", "id", "whoami", "nproc", "printenv", "tty", "arch", "awk",
		"pgrep", "pkill", "ps",
		];

	private static readonly HashSet<string> _nameSet = [.. Names];

	/// <summary>True if <paramref name="name"/> dispatches to an in-process builtin.
	/// Pure membership test (no execution) so the caller can choose a redirect strategy
	/// before running anything.</summary>
	public static bool Has(string name) => _nameSet.Contains(name);

	public bool TryExecute(string name, List<string> args, out int exitCode)
		{
		// Unsupported-option policy for coreutils (DECISIONS 2026-09-04 #2):
		//   BASH_COREUTILS=auto     (default) builtin; on an unsupported option, defer to a PATH
		//                           external of the same name if one exists, else fail loudly.
		//   BASH_COREUTILS=builtin  builtin only; unsupported → loud error (exit 2).
		//   BASH_COREUTILS=external always prefer a PATH external for coreutil names.
		var policy = _env.Get("BASH_COREUTILS");
		if (policy == "external" && CoreutilNames.Contains(name) && _eval.ResolveOnPath(name) is not null)
			{
			exitCode = _eval.RunCommand(name, args, true, [], false);
			return true;
			}
		try
			{
			exitCode = Dispatch(name, args);
			}
		catch (UnsupportedOptionException ex)
			{
			if (policy != "builtin" && _eval.ResolveOnPath(name) is not null)
				{
				exitCode = _eval.RunExternal(name, args);
				return true;
				}
			Console.Error.WriteLine(ex.Message);
			Console.Error.WriteLine(policy == "builtin"
				? $"{ex.Tool}: (C#Bash: not implemented in-process; BASH_COREUTILS=builtin forbids deferring to an external)"
				: $"{ex.Tool}: (C#Bash: not implemented in-process and no external '{ex.Tool}' on PATH)");
			exitCode = 2;
			return true;
			}
		// A filesystem failure inside a builtin is an ordinary error, not a reason to kill the
		// shell. Until 2026-09-11 an unguarded open anywhere in a tool (found: `split` with a
		// missing output directory) escaped as an unhandled exception and took the process down,
		// losing every line of output the script had already produced. Control-flow exceptions
		// (Exit/Return/Break/Continue/Interrupt/BrokenPipe/Eval/FatalShell) are not caught here.
		// NOTE: BrokenPipeException derives from IOException and MUST escape — it is the SIGPIPE
		// emulation the pipeline model depends on (`yes | head` → 141).
		catch (Exception ex) when (ex is not BrokenPipeException
		                            && ex is IOException or UnauthorizedAccessException
		                                or ArgumentException or NotSupportedException
		                                or System.Security.SecurityException)
			{
			Console.Error.WriteLine($"{name}: {IoError(ex)}");
			exitCode = 1;
			return true;
			}
		if (exitCode == -1) { exitCode = 0; return false; }
		return true;
		}

	private int Dispatch(string name, List<string> args)
		{
		return name switch
			{
			"echo"    => Echo(args),
			"printf"  => Printf(args),
			"cd"      => Cd(args),
			"pwd"     => Pwd(args),
			"export"  => Export(args),
			"shopt"   => Shopt(args),
			"alias"   => Alias(args),
			"unalias" => Unalias(args),
			"builtin" => BuiltinCmd(args),
			"readonly"=> Readonly(args),
			"exec"    => Exec(args),
			"let"     => Let(args),
			"pushd"   => Pushd(args),
			"popd"    => Popd(args),
			"dirs"    => Dirs(args),
			"mapfile" => Mapfile(args),
			"readarray" => Mapfile(args),
			"getopts" => Getopts(args),
			"yes"     => Yes(args),
			"unset"   => Unset(args),
			"set"     => Set(args),
			"shift"   => Shift(args),
			"read"    => Read(args),
			"true"    => 0,
			"false"   => 1,
			":"       => 0,
			"exit"    => DoExit(args),
			"return"  => DoReturn(args),
			"break"   => DoBreak(args),
			"continue"=> DoContinue(args),
			"source"  => Source(args),
			"."       => Source(args),
			"local"   => Local(args),
			"declare" => Declare(args),
			"typeset" => Declare(args),
			"eval"    => DoEval(args),
			"type"    => Type(args),
			"command" => Command(args),
			"test"    => Test(args),
			"["       => TestBracket(args),
			"sleep"   => Sleep(args),
			"env"     => Env(args),
			"history" => HistoryCmd(args),
			"trap"    => Trap(args),
			"jobs"    => Jobs(args),
			"wait"    => _eval.WaitJobs(args),
			"fg"      => Fg(args),
			"bg"      => Bg(args),
			"basename"=> Basename(args),
			"dirname" => Dirname(args),
			"seq"     => Seq(args),
			"mkdir"   => Mkdir(args),
			"cat"     => Cat(args),
			"head"    => Head(args),
			"tail"    => Tail(args),
			"wc"      => Wc(args),
			"rev"     => Rev(args),
			"tac"     => Tac(args),
			"tr"      => Tr(args),
			"cut"     => Cut(args),
			"uniq"    => Uniq(args),
			"nl"      => Nl(args),
			"fold"    => Fold(args),
			"touch"   => Touch(args),
			"rmdir"   => Rmdir(args),
			"cmp"     => Cmp(args),
			"tee"     => Tee(args),
			"comm"    => Comm(args),
			"paste"   => Paste(args),
			"rm"      => Rm(args),
			"mv"      => Mv(args),
			"cp"      => Cp(args),
			"uname"   => Uname(args),
			"hostname"=> Hostname(args),
			"factor"  => Factor(args),
			"cal"     => Cal(args),
			"date"    => Date(args),
			"kill"    => Kill(args),
			"expr"    => Expr(args),
			"du"      => Du(args),
			"od"      => Od(args),
			"sort"    => Sort(args),
			"split"   => Split(args),
			"find"    => Find(args),
			"ls"      => Ls(args),
			"xargs"   => Xargs(args),
			"diff"    => Diff(args),
			"hash"    => Hash(args),
			"grep"    => Grep(args),
			"egrep"   => Grep(["-E", .. args]),
			"fgrep"   => Grep(["-F", .. args]),
			"which"   => Which(args),
			"sed"     => Sed(args),
			"base64"  => Base64(args),
			"md5sum" or "sha1sum" or "sha256sum" or "sha512sum" => Checksum(name, args),
			"hexdump" => Hexdump(args),
			"stat"    => Stat(args),
			"mktemp"  => Mktemp(args),
			"realpath"=> Realpath(args),
			"readlink"=> Readlink(args),
			"chmod"   => Chmod(args),
			"ln"      => Ln(args),
			"truncate"=> Truncate(args),
			"timeout" => Timeout(args),
			"id"      => Id(args),
			"whoami"  => Whoami(args),
			"nproc"   => Nproc(args),
			"printenv"=> Printenv(args),
			"tty"     => Tty(args),
			"arch"    => Arch(args),
			"awk"     => Awk(args),
			"pgrep"   => Pgrep(args),
			"pkill"   => Pkill(args),
			"ps"      => Ps(args),
			_         => -1
			};
		}

	// ── in-process coreutils (avoid a ~10ms process spawn each) ─────────────────
	// Scope is deliberately bash-adjacent: path/number/dir plumbing the shell half
	// does in syntax already. Heavy text tools (grep/sed/awk) stay external.

	// ── system / number utilities (wave 4a) ─────────────────────────────────────

	// uname / hostname / date / timeout / id / whoami / nproc / printenv / tty / arch: Builtins.Sys.cs

	private static int Factor(List<string> args)
		{
		var nums = new List<long>();
		var ops = args.Where(a => !a.StartsWith('-')).ToList();
		if (ops.Count > 0) foreach (var a in ops) { if (long.TryParse(a, out var v)) nums.Add(v); }
		else { string? line; while ((line = Console.In.ReadLine()) is not null)
			foreach (var t in line.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)) if (long.TryParse(t, out var v)) nums.Add(v); }
		foreach (var orig in nums)
			{
			var sb = new System.Text.StringBuilder($"{orig}:");
			long n = orig;
			if (n >= 2)
				{
				for (long d = 2; d * d <= n; d++) while (n % d == 0) { sb.Append(' ').Append(d); n /= d; }
				if (n > 1) sb.Append(' ').Append(n);
				}
			Console.WriteLine(sb.ToString());
			}
		return 0;
		}

	// which: locate a command in PATH (+PATHEXT). PATH-only like the real tool (does NOT
	// report shell builtins — use `type` for that). -a lists all matches. rc 1 if any
	// name is unresolved.
	private static int Which(List<string> args)
		{
		bool all = false;
		var names = new List<string>();
		foreach (var a in args) { if (a == "-a") all = true; else if (!a.StartsWith('-') || a == "-") names.Add(a); }
		var pathExt = (Environment.GetEnvironmentVariable("PATHEXT") ?? ".COM;.EXE;.BAT;.CMD").Split(';', StringSplitOptions.RemoveEmptyEntries);
		var dirs = (Environment.GetEnvironmentVariable("PATH") ?? "").Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries);
		int rc = 0;
		foreach (var name in names)
			{
			bool found = false, hasExt = Path.HasExtension(name);
			foreach (var d in dirs)
				{
				var bare = Path.Combine(d, name);
				if (File.Exists(bare)) { Console.WriteLine(bare); found = true; if (!all) break; }
				if (!hasExt)
					foreach (var ext in pathExt)
						{
						var p = Path.Combine(d, name + ext);
						if (File.Exists(p)) { Console.WriteLine(p); found = true; if (!all) break; }
						}
				if (found && !all) break;
				}
			if (!found) rc = 1;
			}
		return rc;
		}

	// hash: remember/inspect external-command paths. `hash` prints the table; `hash -r`
	// clears it; `hash -p PATH NAME` sets one; `hash NAME...` resolves & caches (rc 1 if
	// any not found). Backed by Evaluator's PATH cache (also a fast-path for ExecExternal).
	private int Hash(List<string> args)
		{
		if (args.Count == 0)
			{
			var table = _eval.HashTable;
			if (table.Count == 0) { Console.WriteLine("hash: hash table empty"); return 0; }
			foreach (var kv in table) Console.WriteLine(kv.Value);
			return 0;
			}
		if (args[0] == "-r") { _eval.HashClear(); return 0; }
		if (args[0] == "-p" && args.Count >= 3) { _eval.HashSetPath(args[2], args[1]); return 0; }
		int rc = 0;
		foreach (var name in args)
			{
			if (name.StartsWith('-')) continue;
			if (_eval.ResolveOnPath(name) is null) { Console.Error.WriteLine($"hash: {name}: not found"); rc = 1; }
			}
		return rc;
		}

	// expr: evaluate an expression whose tokens are separate args. Precedence (low→high):
	// | , & , relational, + - , * / % , : (anchored regex match), primary.
	private static int Expr(List<string> args)
		{
		if (args.Count == 0) { Console.Error.WriteLine("expr: missing operand"); return 2; }
		int i = 0; string res;
		try { res = ExprOr(args, ref i); if (i != args.Count) throw new Exception("syntax error"); }
		catch (Exception ex) { Console.Error.WriteLine($"expr: {ex.Message}"); return 2; }
		Console.WriteLine(res);
		return (res.Length == 0 || res == "0") ? 1 : 0;
		}

	private static bool ExprTruthy(string s) => s.Length > 0 && s != "0";
	private static long ExprInt(string s) => long.TryParse(s, out var v) ? v : throw new Exception("non-integer argument");

	private static string ExprOr(List<string> a, ref int i)
		{
		var l = ExprAnd(a, ref i);
		while (i < a.Count && a[i] == "|") { i++; var r = ExprAnd(a, ref i); if (!ExprTruthy(l)) l = r; }
		return l;
		}
	private static string ExprAnd(List<string> a, ref int i)
		{
		var l = ExprRel(a, ref i);
		while (i < a.Count && a[i] == "&") { i++; var r = ExprRel(a, ref i); l = ExprTruthy(l) && ExprTruthy(r) ? l : "0"; }
		return l;
		}
	private static string ExprRel(List<string> a, ref int i)
		{
		var l = ExprAdd(a, ref i);
		while (i < a.Count && a[i] is "=" or "!=" or "<" or "<=" or ">" or ">=")
			{
			var op = a[i++]; var r = ExprAdd(a, ref i);
			int c = long.TryParse(l, out var li) && long.TryParse(r, out var ri)
				? li.CompareTo(ri) : string.CompareOrdinal(l, r);
			bool ok = op switch { "=" => c == 0, "!=" => c != 0, "<" => c < 0, "<=" => c <= 0, ">" => c > 0, ">=" => c >= 0, _ => false };
			l = ok ? "1" : "0";
			}
		return l;
		}
	private static string ExprAdd(List<string> a, ref int i)
		{
		var l = ExprMul(a, ref i);
		while (i < a.Count && a[i] is "+" or "-")
			{ var op = a[i++]; var r = ExprMul(a, ref i); l = (op == "+" ? ExprInt(l) + ExprInt(r) : ExprInt(l) - ExprInt(r)).ToString(); }
		return l;
		}
	private static string ExprMul(List<string> a, ref int i)
		{
		var l = ExprMatch(a, ref i);
		while (i < a.Count && a[i] is "*" or "/" or "%")
			{
			var op = a[i++]; var r = ExprMatch(a, ref i); long x = ExprInt(l), y = ExprInt(r);
			if (op != "*" && y == 0) throw new Exception("division by zero");
			l = (op switch { "*" => x * y, "/" => x / y, _ => x % y }).ToString();
			}
		return l;
		}
	private static string ExprMatch(List<string> a, ref int i)
		{
		var l = ExprPrimary(a, ref i);
		while (i < a.Count && a[i] == ":") { i++; var r = ExprPrimary(a, ref i); l = ExprDoMatch(l, r); }
		return l;
		}
	private static string ExprPrimary(List<string> a, ref int i)
		{
		if (i >= a.Count) throw new Exception("syntax error");
		var t = a[i];
		if (t == "(")      { i++; var v = ExprOr(a, ref i); if (i >= a.Count || a[i] != ")") throw new Exception("expecting ')'"); i++; return v; }
		if (t == "length" && i + 1 < a.Count) { i++; return a[i++].Length.ToString(); }
		if (t == "substr" && i + 3 < a.Count)
			{ i++; var s = a[i++]; long p = ExprInt(a[i++]), n = ExprInt(a[i++]);
			  if (p < 1 || p > s.Length || n <= 0) return ""; int st = (int)p - 1; return s.Substring(st, (int)Math.Min(n, s.Length - st)); }
		if (t == "index" && i + 2 < a.Count) { i++; var s = a[i++]; var c = a[i++]; return (s.IndexOfAny(c.ToCharArray()) + 1).ToString(); }
		if (t == "match" && i + 2 < a.Count) { i++; var s = a[i++]; var r = a[i++]; return ExprDoMatch(s, r); }
		i++; return t;
		}
	private static string ExprDoMatch(string s, string regex)
		{
		var pat = regex.Replace("\\(", "(").Replace("\\)", ")").Replace("\\{", "{").Replace("\\}", "}").Replace("\\+", "+").Replace("\\?", "?");
		try
			{
			var m = System.Text.RegularExpressions.Regex.Match(s, "^(?:" + pat + ")");
			if (m.Groups.Count > 1) return m.Success ? m.Groups[1].Value : "";
			return m.Success ? m.Length.ToString() : "0";
			}
		catch { return "0"; }
		}

	/// <summary>`exec cmd` reached through RunCommand (e.g. from xargs): run and end the shell.
	/// The common forms (`exec >file`, `exec cmd` as a statement) are handled in the evaluator.</summary>
	private int Exec(List<string> args)
		{
		if (args.Count == 0) return 0;
		int code = _eval.RunCommand(args[0], args.Skip(1).ToList(), true, [], false);
		throw new ExitException(code);
		}

	// cal: current month (no args) or a given `month year` (2 args). Whole-year not supported.
	private static int Cal(List<string> args)
		{
		var nums = args.Where(a => int.TryParse(a, out _)).Select(int.Parse).ToList();
		var now = DateTime.Now;
		int month = now.Month, year = now.Year;
		if (nums.Count >= 2) { month = nums[0]; year = nums[1]; }
		if (month < 1 || month > 12) { Console.Error.WriteLine("cal: invalid month"); return 1; }

		var first = new DateTime(year, month, 1);
		int days = DateTime.DaysInMonth(year, month);
		int startCol = (int)first.DayOfWeek;   // Sunday = 0
		string title = $"{first:MMMM} {year}";
		int pad = Math.Max(0, (20 - title.Length) / 2);
		Console.WriteLine(new string(' ', pad) + title);
		Console.WriteLine("Su Mo Tu We Th Fr Sa");
		var sb = new System.Text.StringBuilder();
		for (int i = 0; i < startCol; i++) sb.Append("   ");
		for (int day = 1; day <= days; day++)
			{
			sb.Append($"{day,2}");
			if ((startCol + day - 1) % 7 == 6) { Console.WriteLine(sb.ToString()); sb.Clear(); }
			else sb.Append(' ');
			}
		if (sb.Length > 0) Console.WriteLine(sb.ToString().TrimEnd());
		return 0;
		}

	// ── job control ─────────────────────────────────────────────────────────────

	private int Jobs(List<string> args) { _eval.ListJobs(); return 0; }

	// `fg %n` — we cannot truly give a background thread terminal control, so this
	// waits for the job to finish (and echoes its command), which is the closest
	// faithful behaviour available without fork/job-control process groups.
	private int Fg(List<string> args)
		{
		if (!_eval.HasJobs) { Console.Error.WriteLine("fg: no current job"); return 1; }
		return _eval.WaitJobs(args, report: true);
		}

	// `bg` — nothing to resume: we never suspend jobs (no SIGTSTP on Windows).
	private int Bg(List<string> args)
		{
		Console.Error.WriteLine("bg: job suspension is not supported on this platform");
		return 1;
		}

	// ── trap ────────────────────────────────────────────────────────────────────

	private static readonly string[] SignalNames =
		["EXIT", "HUP", "INT", "QUIT", "KILL", "TERM", "DEBUG", "ERR", "RETURN"];

	private int Trap(List<string> args)
		{
		// trap            / trap -p : print current traps
		if (args.Count == 0 || args[0] == "-p")
			{
			foreach (var (sig, cmd) in _eval.Traps)
				Console.WriteLine($"trap -- '{cmd}' {sig}");
			return 0;
			}

		// trap -l : list signal names
		if (args[0] == "-l")
			{
			Console.WriteLine(string.Join("  ", SignalNames));
			return 0;
			}

		// trap - SIG... : reset the named signals to default
		if (args[0] == "-")
			{
			foreach (var s in args.Skip(1))
				if (CanonSignal(s) is { } c) _eval.RemoveTrap(c);
			return 0;
			}

		// trap 'cmd' SIG...   (cmd may be '' to ignore the signal)
		var action = args[0];
		var sigs   = args.Skip(1).ToList();
		if (sigs.Count == 0) { Console.Error.WriteLine("trap: usage: trap [-lp] [[arg] signal_spec ...]"); return 2; }
		foreach (var s in sigs)
			{
			if (CanonSignal(s) is { } c) _eval.SetTrap(c, action);
			else { Console.Error.WriteLine($"trap: {s}: invalid signal specification"); return 1; }
			}
		return 0;
		}

	/// <summary>Normalise a sigspec (SIGINT/INT/2/exit/0) to a canonical name, or null.</summary>
	private static string? CanonSignal(string s)
		{
		var u = s.ToUpperInvariant();
		if (u.StartsWith("SIG")) u = u[3..];
		return u switch
			{
			"EXIT" or "0"  => "EXIT",
			"HUP"  or "1"  => "HUP",
			"INT"  or "2"  => "INT",
			"QUIT" or "3"  => "QUIT",
			"KILL" or "9"  => "KILL",
			"TERM" or "15" => "TERM",
			"DEBUG"        => "DEBUG",
			"ERR"          => "ERR",
			"RETURN"       => "RETURN",
			_              => u.Length > 0 && u.All(c => char.IsLetterOrDigit(c)) ? u : null
			};
		}

	// ── history ─────────────────────────────────────────────────────────────────

	private int HistoryCmd(List<string> args)
		{
		var hist = _eval.History;
		if (hist is null) return 0;   // no history in script mode

		// history -c : clear the list.
		if (args.Count > 0 && args[0] == "-c") { hist.Clear(); return 0; }

		var items = hist.Items;
		int show = items.Count;
		// history N : show only the last N entries.
		if (args.Count > 0 && int.TryParse(args[0], out var n) && n >= 0)
			show = Math.Min(n, items.Count);

		int start = items.Count - show;
		int width = items.Count.ToString().Length;
		for (int i = start; i < items.Count; i++)
			Console.WriteLine($"{(i + 1).ToString().PadLeft(width + 4)}  {items[i]}");
		return 0;
		}
	}
