namespace Bash.Evaluator;

/// <summary>
/// Implements shell builtins.  Each method returns an exit code (0 = success).
/// The evaluator calls <see cref="TryExecute"/> first; if it returns false
/// the command is an external process.
/// </summary>
public sealed class Builtins(ShellEnvironment env, Evaluator eval)
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
		"basename", "dirname", "seq", "mkdir", "cat", "head", "tail", "wc", "rev", "tac",
		"tr", "cut", "uniq", "nl", "fold",
		"touch", "rmdir", "cmp", "tee", "comm", "paste", "rm", "mv", "cp",
		"uname", "hostname", "factor", "cal", "date", "kill", "expr", "du", "od", "sort", "split", "find", "ls", "xargs", "diff", "hash",
		"grep", "egrep", "fgrep", "which", "sed"
		];

	private static readonly HashSet<string> _nameSet = [.. Names];

	/// <summary>True if <paramref name="name"/> dispatches to an in-process builtin.
	/// Pure membership test (no execution) so the caller can choose a redirect strategy
	/// before running anything.</summary>
	public static bool Has(string name) => _nameSet.Contains(name);

	public bool TryExecute(string name, List<string> args, out int exitCode)
		{
		exitCode = name switch
			{
			"echo"    => Echo(args),
			"printf"  => Printf(args),
			"cd"      => Cd(args),
			"pwd"     => Pwd(),
			"export"  => Export(args),
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
			_         => -1
			};
		if (exitCode == -1) { exitCode = 0; return false; }
		return true;
		}

	// ── echo ──────────────────────────────────────────────────────────────────

	private static int Echo(List<string> args)
		{
		bool noNewline = false;
		bool interpret = false;
		int start = 0;

		for (; start < args.Count && args[start].StartsWith('-'); start++)
			{
			var flag = args[start];
			if (flag == "-n") { noNewline = true; }
			else if (flag == "-e") { interpret = true; }
			else if (flag == "-E") { interpret = false; }
			else break; // not a flag
			}

		var text = string.Join(" ", args.Skip(start));
		if (interpret) text = InterpretEscapes(text);
		if (noNewline) Console.Write(text);
		else           Console.WriteLine(text);
		return 0;
		}

	private static string InterpretEscapes(string s)
		{
		var sb = new System.Text.StringBuilder();
		int i = 0;
		while (i < s.Length)
			{
			if (s[i] == '\\' && i + 1 < s.Length)
				{
				i++;
				sb.Append(s[i] switch
					{
					'n'  => '\n', 't' => '\t', 'r' => '\r',
					'a'  => '\a', 'b' => '\b', 'e' => '\x1b',
					'\\' => '\\', '0' => '\0',
					_    => s[i]
					});
				}
			else
				sb.Append(s[i]);
			i++;
			}
		return sb.ToString();
		}

	// ── printf ────────────────────────────────────────────────────────────────

	private static int Printf(List<string> args)
		{
		if (args.Count == 0) return 0;
		var fmt = args[0];
		var fmtArgs = args.Skip(1).ToList();
		// Minimal printf: %s, %d, %f, %%, \n, \t
		var sb = new System.Text.StringBuilder();
		int ai = 0;
		int i = 0;
		while (i < fmt.Length)
			{
			if (fmt[i] == '\\' && i + 1 < fmt.Length)
				{
				i++;
				sb.Append(fmt[i] switch { 'n' => '\n', 't' => '\t', '\\' => '\\', _ => fmt[i] });
				}
			else if (fmt[i] == '%' && i + 1 < fmt.Length)
				{
				i++;
				var arg = ai < fmtArgs.Count ? fmtArgs[ai++] : "";
				switch (fmt[i])
					{
					case 's': sb.Append(arg); break;
					case 'd': sb.Append(long.TryParse(arg, out var l) ? l.ToString() : "0"); break;
					case 'f': sb.Append(double.TryParse(arg, out var d) ? d.ToString("F6") : "0.000000"); break;
					case '%': sb.Append('%'); break;
					default:  sb.Append('%'); sb.Append(fmt[i]); break;
					}
				}
			else
				sb.Append(fmt[i]);
			i++;
			}
		Console.Write(sb);
		return 0;
		}

	// ── cd ────────────────────────────────────────────────────────────────────

	private int Cd(List<string> args)
		{
		string target;
		if (args.Count == 0 || args[0] == "~")
			target = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
		else if (args[0] == "-")
			target = _env.Get("OLDPWD");
		else
			target = ShellEnvironment.TranslatePath(args[0]);

		bool printDir = args.Count > 0 && args[0] == "-";

		if (!Directory.Exists(target))
			{
			Console.Error.WriteLine($"cd: {target}: No such file or directory");
			return 1;
			}
		_env.Set("OLDPWD", Directory.GetCurrentDirectory());
		Directory.SetCurrentDirectory(target);
		_env.Set("PWD", Directory.GetCurrentDirectory());
		if (printDir) Console.WriteLine(Directory.GetCurrentDirectory());
		return 0;
		}

	// ── pwd ───────────────────────────────────────────────────────────────────

	private static int Pwd() { Console.WriteLine(Directory.GetCurrentDirectory()); return 0; }

	// ── export ────────────────────────────────────────────────────────────────

	private int Export(List<string> args)
		{
		if (args.Count == 0)
			{
			// list all exports
			foreach (System.Collections.DictionaryEntry e in System.Environment.GetEnvironmentVariables())
				Console.WriteLine($"declare -x {e.Key}=\"{e.Value}\"");
			return 0;
			}
		foreach (var arg in args)
			{
			int eq = arg.IndexOf('=');
			if (eq > 0)
				{
				_env.Set(arg[..eq], arg[(eq + 1)..]);
				_env.Export(arg[..eq]);
				}
			else
				_env.Export(arg);
			}
		return 0;
		}

	// ── unset ─────────────────────────────────────────────────────────────────

	private int Unset(List<string> args)
		{
		foreach (var a in args)
			{
			int lb = a.IndexOf('[');
			int rb = a.IndexOf(']');
			if (lb > 0 && rb > lb)
				{
				var name = a[..lb];
				var key  = a[(lb + 1)..rb];
				if (_env.IsAssoc(name))
					_env.UnsetAssocElement(name, key);
				else if (int.TryParse(key, out int idx))
					_env.UnsetArrayElement(name, idx);
				}
			else
				_env.Unset(a);
			}
		return 0;
		}

	// ── set ───────────────────────────────────────────────────────────────────

	private int Set(List<string> args)
		{
		if (args.Count == 0)
			{
			foreach (System.Collections.DictionaryEntry e in System.Environment.GetEnvironmentVariables())
				Console.WriteLine($"{e.Key}={e.Value}");
			return 0;
			}

		int i = 0;
		while (i < args.Count)
			{
			var arg = args[i++];
			if (arg == "--") break; // end of options; rest are positional params

			if (arg.Length >= 2 && arg[0] is '-' or '+')
				{
				bool enable = arg[0] == '-';

				// -o option-name
				if (arg == "-o" || arg == "+o")
					{
					enable = arg[0] == '-';
					if (i < args.Count)
						_eval.Options.ApplyLong(args[i++], enable);
					continue;
					}

				foreach (char flag in arg[1..])
					_eval.Options.Apply(flag, enable);
				}
			}

		return 0;
		}

	// ── shift ─────────────────────────────────────────────────────────────────

	private static int Shift(List<string> args)
		{
		// Positional param shifting handled by environment; stub for now
		return 0;
		}

	// ── read ──────────────────────────────────────────────────────────────────

	private int Read(List<string> args)
		{
		// read [-r] [-p prompt] VAR...
		bool raw = false;
		string prompt = "";
		var vars = new List<string>();

		for (int i = 0; i < args.Count; i++)
			{
			if (args[i] == "-r") { raw = true; continue; }
			if (args[i] == "-p" && i + 1 < args.Count) { prompt = args[++i]; continue; }
			vars.Add(args[i]);
			}

		if (prompt.Length > 0) Console.Write(prompt);

		var line = Console.ReadLine();
		if (line is null) return 1;
		if (!raw) line = line.Replace("\\n", "\n"); // minimal escape processing

		var ifs = _env.Get("IFS");
		if (ifs.Length == 0) ifs = " \t\n";
		var parts = line.Split(ifs.ToCharArray(), vars.Count > 1 ? vars.Count : int.MaxValue,
			StringSplitOptions.None);

		for (int i = 0; i < vars.Count; i++)
			_env.Set(vars[i], i < parts.Length ? parts[i] : "");

		if (vars.Count == 0)
			_env.Set("REPLY", line);

		return 0;
		}

	// ── exit / return / break / continue ─────────────────────────────────────

	private int DoExit(List<string> args)
		{
		int code = args.Count > 0 && int.TryParse(args[0], out var c) ? c : _env.LastExitCode;
		throw new ExitException(code);
		}

	private static int DoReturn(List<string> args)
		{
		int code = args.Count > 0 && int.TryParse(args[0], out var c) ? c : 0;
		throw new ReturnException(code);
		}

	private static int DoBreak(List<string> args)
		{
		int levels = args.Count > 0 && int.TryParse(args[0], out var c) ? c : 1;
		throw new BreakException(levels);
		}

	private static int DoContinue(List<string> args)
		{
		int levels = args.Count > 0 && int.TryParse(args[0], out var c) ? c : 1;
		throw new ContinueException(levels);
		}

	// ── source / . ───────────────────────────────────────────────────────────

	private int Source(List<string> args)
		{
		if (args.Count == 0) { Console.Error.WriteLine("source: filename required"); return 1; }
		var path = ShellEnvironment.TranslatePath(args[0]);
		if (!File.Exists(path)) { Console.Error.WriteLine($"source: {path}: file not found"); return 1; }
		var src = File.ReadAllText(path);
		var tokens = new Lexer.Lexer(src).Tokenize();
		var script = new Parser.Parser(tokens).Parse();
		return _eval.Execute(script);
		}

	// ── local / declare ───────────────────────────────────────────────────────

	private int Local(List<string> args)
		{
		foreach (var arg in args)
			{
			int eq = arg.IndexOf('=');
			if (eq > 0) _env.SetLocal(arg[..eq], arg[(eq + 1)..]);
			else        _env.SetLocal(arg, _env.Get(arg));
			}
		return 0;
		}

	private int Declare(List<string> args)
		{
		bool export = false, array = false, assoc = false;
		var names = new List<string>();

		foreach (var arg in args)
			{
			if (arg == "-x") { export = true; continue; }
			if (arg == "-a") { array  = true; continue; }
			if (arg == "-A") { assoc  = true; continue; }
			if (arg.StartsWith('-')) continue;
			names.Add(arg);
			}

		if (names.Count == 0)
			{
			foreach (System.Collections.DictionaryEntry e in System.Environment.GetEnvironmentVariables())
				Console.WriteLine($"declare -x {e.Key}=\"{e.Value}\"");
			return 0;
			}

		foreach (var spec in names)
			{
			int eq = spec.IndexOf('=');
			var name = eq < 0 ? spec : spec[..eq];
			var val  = eq < 0 ? null : spec[(eq + 1)..];

			if (assoc)       _env.DeclareAssoc(name);
			else if (array)  _env.DeclareArray(name);

			if (val is not null) _env.Set(name, val);
			if (export) _env.Export(name);
			}
		return 0;
		}

	// ── eval ──────────────────────────────────────────────────────────────────

	private int DoEval(List<string> args)
		{
		var src = string.Join(" ", args);
		var tokens = new Lexer.Lexer(src).Tokenize();
		var script = new Parser.Parser(tokens).Parse();
		return _eval.Execute(script);
		}

	// ── type / command ────────────────────────────────────────────────────────

	private int Type(List<string> args)
		{
		foreach (var name in args)
			{
			if (TryExecute(name, [], out _))
				Console.WriteLine($"{name} is a shell builtin");
			else
				{
				var found = FindInPath(name);
				if (found is not null) Console.WriteLine($"{name} is {found}");
				else Console.WriteLine($"{name}: not found");
				}
			}
		return 0;
		}

	private static int Command(List<string> args)
		{
		// stub — just prints path
		if (args.Count > 0)
			{
			var found = FindInPath(args[0]);
			if (found is not null) { Console.WriteLine(found); return 0; }
			}
		return 1;
		}

	private static string? FindInPath(string name)
		{
		var path = System.Environment.GetEnvironmentVariable("PATH") ?? "";
		var exts = System.Environment.GetEnvironmentVariable("PATHEXT") ?? ".COM;.EXE;.BAT;.CMD";
		foreach (var dir in path.Split(Path.PathSeparator))
			{
			// exact match first
			var full = Path.Combine(dir, name);
			if (File.Exists(full)) return full;
			// try extensions (Windows)
			foreach (var ext in exts.Split(';'))
				{
				var withExt = full + ext;
				if (File.Exists(withExt)) return withExt;
				}
			}
		return null;
		}

	// ── test / [ ─────────────────────────────────────────────────────────────

	private static int Test(List<string> args) => TestImpl(args) ? 0 : 1;

	private static int TestBracket(List<string> args)
		{
		// Strip trailing ]
		if (args.Count > 0 && args[^1] == "]")
			args = args[..^1];
		return TestImpl(args) ? 0 : 1;
		}

	private static bool TestImpl(List<string> args)
		{
		if (args.Count == 0) return false;
		if (args[0] == "!" && args.Count > 1) return !TestImpl(args.GetRange(1, args.Count - 1));
		if (args.Count == 1) return args[0].Length > 0;

		if (args.Count == 2 && args[0].StartsWith('-'))
			{
			var op  = args[0];
			var val = args[1];
			return op switch
				{
				"-z" => val.Length == 0,
				"-n" => val.Length > 0,
				"-f" => File.Exists(val),
				"-d" => Directory.Exists(val),
				"-e" => File.Exists(val) || Directory.Exists(val),
				"-r" => File.Exists(val),   // simplified
				"-w" => File.Exists(val),
				"-x" => File.Exists(val),
				"-s" => File.Exists(val) && new FileInfo(val).Length > 0,
				_ => false
				};
			}

		if (args.Count == 3)
			{
			var a = args[0]; var op = args[1]; var b = args[2];
			bool numOp = long.TryParse(a, out var la) & long.TryParse(b, out var lb);
			return op switch
				{
				"="  or "==" => a == b,
				"!="         => a != b,
				"<"          => string.Compare(a, b, StringComparison.Ordinal) < 0,
				">"          => string.Compare(a, b, StringComparison.Ordinal) > 0,
				"-eq"        => numOp && la == lb,
				"-ne"        => numOp && la != lb,
				"-lt"        => numOp && la <  lb,
				"-le"        => numOp && la <= lb,
				"-gt"        => numOp && la >  lb,
				"-ge"        => numOp && la >= lb,
				_ => false
				};
			}

		return false;
		}

	// ── sleep ─────────────────────────────────────────────────────────────────

	private int Sleep(List<string> args)
		{
		if (args.Count > 0 && double.TryParse(args[0], out var secs))
			{
			// Sleep in small slices so Ctrl+C (SIGINT) can interrupt a long sleep.
			int remaining = (int)(secs * 1000);
			while (remaining > 0)
				{
				_eval.CheckInterrupt();
				int slice = Math.Min(50, remaining);
				Thread.Sleep(slice);
				remaining -= slice;
				}
			}
		return 0;
		}

	// ── env ───────────────────────────────────────────────────────────────────

	private int Env(List<string> args)
		{
		if (args.Count == 0)
			{
			foreach (var (k, v) in _env.GetExportedVars())
				Console.WriteLine($"{k}={v}");
			return 0;
			}
		return 0;
		}

	// ── in-process coreutils (avoid a ~10ms process spawn each) ─────────────────
	// Scope is deliberately bash-adjacent: path/number/dir plumbing the shell half
	// does in syntax already. Heavy text tools (grep/sed/awk) stay external.

	private static int Basename(List<string> args)
		{
		if (args.Count == 0) { Console.Error.WriteLine("basename: missing operand"); return 1; }
		var path = args[0].TrimEnd('/', '\\');
		if (path.Length == 0) { Console.WriteLine("/"); return 0; }            // input was all slashes
		int slash = Math.Max(path.LastIndexOf('/'), path.LastIndexOf('\\'));
		var name  = slash >= 0 ? path[(slash + 1)..] : path;
		if (args.Count > 1 && args[1].Length > 0 && name.Length > args[1].Length
		    && name.EndsWith(args[1], StringComparison.Ordinal))
			name = name[..^args[1].Length];
		Console.WriteLine(name);
		return 0;
		}

	private static int Dirname(List<string> args)
		{
		if (args.Count == 0) { Console.Error.WriteLine("dirname: missing operand"); return 1; }
		var raw  = args[0];
		var path = raw.TrimEnd('/', '\\');
		if (path.Length == 0) { Console.WriteLine("/"); return 0; }            // all slashes
		int slash = Math.Max(path.LastIndexOf('/'), path.LastIndexOf('\\'));
		if (slash < 0)  { Console.WriteLine("."); return 0; }                  // no dir component
		if (slash == 0) { Console.WriteLine(path[..1]); return 0; }            // "/x" -> "/"
		Console.WriteLine(path[..slash]);
		return 0;
		}

	private static int Seq(List<string> args)
		{
		var ns = new List<long>();
		foreach (var a in args) if (long.TryParse(a, out var v)) ns.Add(v);    // unsupported flags ignored
		if (ns.Count == 0) { Console.Error.WriteLine("seq: missing operand"); return 1; }
		long first = 1, incr = 1, last;
		if      (ns.Count == 1) { last = ns[0]; }
		else if (ns.Count == 2) { first = ns[0]; last = ns[1]; }
		else                    { first = ns[0]; incr = ns[1]; last = ns[2]; }
		if (incr == 0) { Console.Error.WriteLine("seq: zero increment"); return 1; }
		if (incr > 0) for (long i = first; i <= last; i += incr) Console.WriteLine(i);
		else          for (long i = first; i >= last; i += incr) Console.WriteLine(i);
		return 0;
		}

	private static int Mkdir(List<string> args)
		{
		bool parents = false;
		var dirs = new List<string>();
		foreach (var a in args)
			{ if (a == "-p") parents = true; else if (!a.StartsWith('-')) dirs.Add(a); }
		if (dirs.Count == 0) { Console.Error.WriteLine("mkdir: missing operand"); return 1; }

		int rc = 0;
		foreach (var d in dirs)
			{
			var p = ShellEnvironment.TranslatePath(d);
			try
				{
				if (!parents && Directory.Exists(p))
					{ Console.Error.WriteLine($"mkdir: cannot create directory '{d}': File exists"); rc = 1; continue; }
				if (!parents)
					{
					var parent = Path.GetDirectoryName(p);
					if (!string.IsNullOrEmpty(parent) && !Directory.Exists(parent))
						{ Console.Error.WriteLine($"mkdir: cannot create directory '{d}': No such file or directory"); rc = 1; continue; }
					}
				Directory.CreateDirectory(p);
				}
			catch (Exception ex)
				{ Console.Error.WriteLine($"mkdir: cannot create directory '{d}': {ex.Message}"); rc = 1; }
			}
		return rc;
		}

	// cat: byte-faithful for file operands (copies raw bytes through redirects/pipes via
	// CurrentRawStdout — NOT the text Console path, which re-encodes). '-'/no-args read
	// stdin (text path for now). When output is $(...)-captured, falls back to text.
	private static int Cat(List<string> args)
		{
		var files = new List<string>();
		foreach (var a in args) if (a == "-" || !a.StartsWith('-')) files.Add(a);
		if (files.Count == 0) files.Add("-");

		var raw = Evaluator.CurrentRawStdout();
		if (raw is not null) Console.Out.Flush();   // order bytes after any prior text
		int rc = 0;
		foreach (var f in files)
			{
			try
				{
				if (f == "-")
					{
					var s = Console.In.ReadToEnd();
					if (raw is not null) { var b = System.Text.Encoding.UTF8.GetBytes(s); raw.Write(b, 0, b.Length); }
					else Console.Out.Write(s);
					continue;
					}
				var p = ShellEnvironment.TranslatePath(f);
				if (Directory.Exists(p)) { Console.Error.WriteLine($"cat: {f}: Is a directory"); rc = 1; continue; }
				if (raw is not null) { using var fs = File.OpenRead(p); fs.CopyTo(raw); }
				else                 Console.Out.Write(File.ReadAllText(p));
				}
			catch (FileNotFoundException)      { Console.Error.WriteLine($"cat: {f}: No such file or directory"); rc = 1; }
			catch (DirectoryNotFoundException) { Console.Error.WriteLine($"cat: {f}: No such file or directory"); rc = 1; }
			catch (Exception ex)               { Console.Error.WriteLine($"cat: {f}: {ex.Message}"); rc = 1; }
			}
		raw?.Flush();
		return rc;
		}

	// Read all lines of one source ("-" or no path = stdin), line terminators stripped.
	private static List<string> LinesOf(string src)
		{
		var list = new List<string>();
		if (src == "-")
			{ string? l; while ((l = Console.In.ReadLine()) is not null) list.Add(l); return list; }
		using var r = new StreamReader(ShellEnvironment.TranslatePath(src));
		string? line; while ((line = r.ReadLine()) is not null) list.Add(line);
		return list;
		}

	// Parse a head/tail line count from args (-n N, -nN, -N); returns count + file operands.
	private static int LineCountArg(List<string> args, int def, out List<string> files)
		{
		int n = def; files = [];
		for (int i = 0; i < args.Count; i++)
			{
			var a = args[i];
			if      (a == "-n" && i + 1 < args.Count) int.TryParse(args[++i], out n);
			else if (a.Length > 2 && a.StartsWith("-n")) int.TryParse(a[2..], out n);
			else if (a.Length > 1 && a[0] == '-' && char.IsDigit(a[1])) int.TryParse(a[1..], out n);
			else if (a == "-" || !a.StartsWith('-')) files.Add(a);
			}
		if (files.Count == 0) files.Add("-");
		return n;
		}

	private static int Head(List<string> args)
		{
		int n = LineCountArg(args, 10, out var files);
		bool multi = files.Count > 1; int rc = 0; bool first = true;
		foreach (var f in files)
			{
			try
				{
				if (multi) { if (!first) Console.WriteLine(); Console.WriteLine($"==> {f} <=="); }
				first = false;
				var lines = LinesOf(f);
				for (int i = 0; i < Math.Min(n, lines.Count); i++) Console.WriteLine(lines[i]);
				}
			catch (Exception ex) { Console.Error.WriteLine($"head: {f}: {ex.Message}"); rc = 1; }
			}
		return rc;
		}

	private static int Tail(List<string> args)
		{
		int n = LineCountArg(args, 10, out var files);
		bool multi = files.Count > 1; int rc = 0; bool first = true;
		foreach (var f in files)
			{
			try
				{
				if (multi) { if (!first) Console.WriteLine(); Console.WriteLine($"==> {f} <=="); }
				first = false;
				var lines = LinesOf(f);
				for (int i = Math.Max(0, lines.Count - n); i < lines.Count; i++) Console.WriteLine(lines[i]);
				}
			catch (Exception ex) { Console.Error.WriteLine($"tail: {f}: {ex.Message}"); rc = 1; }
			}
		return rc;
		}

	private static int Wc(List<string> args)
		{
		bool cl = false, cw = false, cc = false;
		var files = new List<string>();
		foreach (var a in args)
			{
			if (a.Length > 1 && a[0] == '-')
				foreach (char c in a[1..]) { if (c == 'l') cl = true; else if (c == 'w') cw = true; else if (c is 'c' or 'm') cc = true; }
			else files.Add(a);
			}
		if (!cl && !cw && !cc) { cl = cw = cc = true; }
		bool multi = files.Count > 1;
		if (files.Count == 0) files.Add("-");
		long tl = 0, tw = 0, tc = 0; int rc = 0;

		void Emit(long l, long w, long c, string name)
			{
			var parts = new List<string>();
			if (cl) parts.Add($"{l,7}");
			if (cw) parts.Add($"{w,7}");
			if (cc) parts.Add($"{c,7}");
			Console.WriteLine(string.Join("", parts) + (name.Length > 0 ? " " + name : ""));
			}

		foreach (var f in files)
			{
			try
				{
				string s; long bytes;
				if (f == "-") { s = Console.In.ReadToEnd(); bytes = System.Text.Encoding.UTF8.GetByteCount(s); }
				else { var p = ShellEnvironment.TranslatePath(f); s = File.ReadAllText(p); bytes = new FileInfo(p).Length; }
				long lines = s.Count(ch => ch == '\n');
				long words = s.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).Length;
				tl += lines; tw += words; tc += bytes;
				Emit(lines, words, bytes, f == "-" ? "" : f);
				}
			catch (Exception ex) { Console.Error.WriteLine($"wc: {f}: {ex.Message}"); rc = 1; }
			}
		if (multi) Emit(tl, tw, tc, "total");
		return rc;
		}

	private static int Rev(List<string> args)
		{
		var files = args.Where(a => a == "-" || !a.StartsWith('-')).ToList();
		if (files.Count == 0) files.Add("-");
		int rc = 0;
		foreach (var f in files)
			try { foreach (var line in LinesOf(f)) Console.WriteLine(new string(line.Reverse().ToArray())); }
			catch (Exception ex) { Console.Error.WriteLine($"rev: {f}: {ex.Message}"); rc = 1; }
		return rc;
		}

	private static int Tac(List<string> args)
		{
		var files = args.Where(a => a == "-" || !a.StartsWith('-')).ToList();
		if (files.Count == 0) files.Add("-");
		int rc = 0;
		foreach (var f in files)
			try { var lines = LinesOf(f); for (int i = lines.Count - 1; i >= 0; i--) Console.WriteLine(lines[i]); }
			catch (Exception ex) { Console.Error.WriteLine($"tac: {f}: {ex.Message}"); rc = 1; }
		return rc;
		}

	// tr: translate/delete/squeeze chars on stdin. Supports ranges (a-z), \escapes,
	// and [:classes:]. -d delete, -s squeeze. (No complement -c yet.)
	private static int Tr(List<string> args)
		{
		bool del = false, squeeze = false;
		var sets = new List<string>();
		foreach (var a in args)
			{
			if (a.Length > 1 && a[0] == '-' && a.Skip(1).All(c => c is 'd' or 's'))
				foreach (char c in a[1..]) { if (c == 'd') del = true; else squeeze = true; }
			else sets.Add(a);
			}
		string s1 = sets.Count > 0 ? ExpandTrSet(sets[0]) : "";
		string s2 = sets.Count > 1 ? ExpandTrSet(sets[1]) : "";
		string input = Console.In.ReadToEnd();
		var sb = new System.Text.StringBuilder(input.Length);

		if (del)
			{
			var drop = new HashSet<char>(s1);
			var sq   = squeeze ? new HashSet<char>(s2) : null;
			char? last = null;
			foreach (char c in input)
				{
				if (drop.Contains(c)) continue;
				if (sq != null && last == c && sq.Contains(c)) continue;
				sb.Append(c); last = c;
				}
			}
		else
			{
			var map = new Dictionary<char, char>();
			for (int i = 0; i < s1.Length; i++)
				map[s1[i]] = i < s2.Length ? s2[i] : (s2.Length > 0 ? s2[^1] : s1[i]);
			char? last = null;
			foreach (char c in input)
				{
				char o = map.TryGetValue(c, out var m) ? m : c;
				if (squeeze && last == o && map.ContainsKey(c)) continue;
				sb.Append(o); last = o;
				}
			}
		Console.Out.Write(sb.ToString());
		return 0;
		}

	private static string ExpandTrSet(string s)
		{
		var sb = new System.Text.StringBuilder();
		int i = 0;
		while (i < s.Length)
			{
			if (s[i] == '\\' && i + 1 < s.Length)
				{ sb.Append(s[i + 1] switch { 'n' => '\n', 't' => '\t', 'r' => '\r', '\\' => '\\', '0' => '\0', var x => x }); i += 2; }
			else if (s[i] == '[' && i + 1 < s.Length && s[i + 1] == ':')
				{
				int end = s.IndexOf(":]", i, StringComparison.Ordinal);
				if (end > 0) { sb.Append(TrClass(s[(i + 2)..end])); i = end + 2; }
				else { sb.Append(s[i++]); }
				}
			else if (i + 2 < s.Length && s[i + 1] == '-')
				{ for (char c = s[i]; c <= s[i + 2]; c++) sb.Append(c); i += 3; }
			else sb.Append(s[i++]);
			}
		return sb.ToString();
		}

	private static string TrClass(string cls) => cls switch
		{
		"upper" => "ABCDEFGHIJKLMNOPQRSTUVWXYZ",
		"lower" => "abcdefghijklmnopqrstuvwxyz",
		"digit" => "0123456789",
		"alpha" => "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz",
		"alnum" => "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz",
		"space" => " \t\n\r\f\v",
		"blank" => " \t",
		_        => ""
		};

	// cut: -f fields (with -d delim, default tab) or -c chars; LIST = n, n-m, n-, -m, csv.
	private static int Cut(List<string> args)
		{
		char delim = '\t'; string? fieldList = null, charList = null;
		var files = new List<string>();
		for (int i = 0; i < args.Count; i++)
			{
			var a = args[i];
			if      (a == "-d" && i + 1 < args.Count) delim = args[++i].Length > 0 ? args[i][0] : '\t';
			else if (a.StartsWith("-d") && a.Length > 2) delim = a[2];
			else if (a == "-f" && i + 1 < args.Count) fieldList = args[++i];
			else if (a.StartsWith("-f") && a.Length > 2) fieldList = a[2..];
			else if (a == "-c" && i + 1 < args.Count) charList = args[++i];
			else if (a.StartsWith("-c") && a.Length > 2) charList = a[2..];
			else if (a == "-" || !a.StartsWith('-')) files.Add(a);
			}
		if (files.Count == 0) files.Add("-");
		var ranges = ParseRanges(charList ?? fieldList ?? "1-");
		int rc = 0;
		foreach (var f in files)
			try
				{
				foreach (var line in LinesOf(f))
					{
					if (charList != null)
						{
						var sb = new System.Text.StringBuilder();
						for (int k = 0; k < line.Length; k++) if (InRanges(ranges, k + 1)) sb.Append(line[k]);
						Console.WriteLine(sb.ToString());
						}
					else
						{
						if (!line.Contains(delim)) { Console.WriteLine(line); continue; }   // -f, no delim: whole line
						var parts = line.Split(delim);
						var sel = new List<string>();
						for (int k = 0; k < parts.Length; k++) if (InRanges(ranges, k + 1)) sel.Add(parts[k]);
						Console.WriteLine(string.Join(delim, sel));
						}
					}
				}
			catch (Exception ex) { Console.Error.WriteLine($"cut: {f}: {ex.Message}"); rc = 1; }
		return rc;
		}

	private static List<(int lo, int hi)> ParseRanges(string list)
		{
		var r = new List<(int, int)>();
		foreach (var part in list.Split(','))
			{
			int dash = part.IndexOf('-');
			if (dash < 0) { if (int.TryParse(part, out var v)) r.Add((v, v)); }
			else
				{
				int lo = dash == 0 ? 1 : int.Parse(part[..dash]);
				int hi = dash == part.Length - 1 ? int.MaxValue : int.Parse(part[(dash + 1)..]);
				r.Add((lo, hi));
				}
			}
		return r;
		}

	private static bool InRanges(List<(int lo, int hi)> r, int k)
		{
		foreach (var (lo, hi) in r) if (k >= lo && k <= hi) return true;
		return false;
		}

	// uniq: collapse ADJACENT equal lines. -c prefix count, -d only dups, -u only uniques.
	private static int Uniq(List<string> args)
		{
		bool count = false, only_d = false, only_u = false;
		var files = new List<string>();
		foreach (var a in args)
			{
			if (a.Length > 1 && a[0] == '-')
				foreach (char c in a[1..]) { if (c == 'c') count = true; else if (c == 'd') only_d = true; else if (c == 'u') only_u = true; }
			else files.Add(a);
			}
		if (files.Count == 0) files.Add("-");
		int rc = 0;
		foreach (var f in files)
			try
				{
				var lines = LinesOf(f);
				int i = 0;
				while (i < lines.Count)
					{
					int j = i; while (j < lines.Count && lines[j] == lines[i]) j++;
					int n = j - i;
					bool show = only_d ? n > 1 : only_u ? n == 1 : true;
					if (show) Console.WriteLine(count ? $"{n,7} {lines[i]}" : lines[i]);
					i = j;
					}
				}
			catch (Exception ex) { Console.Error.WriteLine($"uniq: {f}: {ex.Message}"); rc = 1; }
		return rc;
		}

	// nl: number non-empty lines ("%6d\t"), blank lines pass through un-numbered.
	private static int Nl(List<string> args)
		{
		var files = args.Where(a => a == "-" || !a.StartsWith('-')).ToList();
		if (files.Count == 0) files.Add("-");
		int n = 0, rc = 0;
		foreach (var f in files)
			try { foreach (var line in LinesOf(f)) { if (line.Length == 0) Console.WriteLine(); else Console.WriteLine($"{++n,6}\t{line}"); } }
			catch (Exception ex) { Console.Error.WriteLine($"nl: {f}: {ex.Message}"); rc = 1; }
		return rc;
		}

	// fold: hard-wrap lines to width (-w, default 80).
	private static int Fold(List<string> args)
		{
		int w = 80; var files = new List<string>();
		for (int i = 0; i < args.Count; i++)
			{
			var a = args[i];
			if      (a == "-w" && i + 1 < args.Count) int.TryParse(args[++i], out w);
			else if (a.StartsWith("-w") && a.Length > 2) int.TryParse(a[2..], out w);
			else if (a.Length > 1 && a[0] == '-' && char.IsDigit(a[1])) int.TryParse(a[1..], out w);
			else if (a == "-" || !a.StartsWith('-')) files.Add(a);
			}
		if (w < 1) w = 1;
		if (files.Count == 0) files.Add("-");
		int rc = 0;
		foreach (var f in files)
			try
				{
				foreach (var line in LinesOf(f))
					{
					if (line.Length == 0) { Console.WriteLine(); continue; }
					for (int i = 0; i < line.Length; i += w) Console.WriteLine(line.Substring(i, Math.Min(w, line.Length - i)));
					}
				}
			catch (Exception ex) { Console.Error.WriteLine($"fold: {f}: {ex.Message}"); rc = 1; }
		return rc;
		}

	private static int Touch(List<string> args)
		{
		var files = args.Where(a => !a.StartsWith('-')).ToList();
		if (files.Count == 0) { Console.Error.WriteLine("touch: missing file operand"); return 1; }
		int rc = 0;
		foreach (var f in files)
			try
				{
				var p = ShellEnvironment.TranslatePath(f);
				if (File.Exists(p))            File.SetLastWriteTime(p, DateTime.Now);
				else if (Directory.Exists(p))  Directory.SetLastWriteTime(p, DateTime.Now);
				else                           File.Create(p).Dispose();
				}
			catch (Exception ex) { Console.Error.WriteLine($"touch: {f}: {ex.Message}"); rc = 1; }
		return rc;
		}

	private static int Rmdir(List<string> args)
		{
		var dirs = args.Where(a => !a.StartsWith('-')).ToList();
		if (dirs.Count == 0) { Console.Error.WriteLine("rmdir: missing operand"); return 1; }
		int rc = 0;
		foreach (var d in dirs)
			{
			var p = ShellEnvironment.TranslatePath(d);
			try
				{
				if (!Directory.Exists(p))
					{ Console.Error.WriteLine($"rmdir: failed to remove '{d}': No such file or directory"); rc = 1; }
				else if (Directory.EnumerateFileSystemEntries(p).Any())
					{ Console.Error.WriteLine($"rmdir: failed to remove '{d}': Directory not empty"); rc = 1; }
				else Directory.Delete(p, false);
				}
			catch (Exception ex) { Console.Error.WriteLine($"rmdir: failed to remove '{d}': {ex.Message}"); rc = 1; }
			}
		return rc;
		}

	// cmp: byte-compare two files. Silent + exit 0 if identical, message + 1 if differ, 2 on error.
	private static int Cmp(List<string> args)
		{
		var files = args.Where(a => !a.StartsWith('-')).ToList();
		if (files.Count < 2) { Console.Error.WriteLine("cmp: missing operand"); return 2; }
		try
			{
			var a = File.ReadAllBytes(ShellEnvironment.TranslatePath(files[0]));
			var b = File.ReadAllBytes(ShellEnvironment.TranslatePath(files[1]));
			int min = Math.Min(a.Length, b.Length); long line = 1;
			for (int i = 0; i < min; i++)
				{
				if (a[i] != b[i]) { Console.WriteLine($"{files[0]} {files[1]} differ: byte {i + 1}, line {line}"); return 1; }
				if (a[i] == (byte)'\n') line++;
				}
			if (a.Length != b.Length)
				{ Console.Error.WriteLine($"cmp: EOF on {(a.Length < b.Length ? files[0] : files[1])}"); return 1; }
			return 0;
			}
		catch (Exception ex) { Console.Error.WriteLine($"cmp: {ex.Message}"); return 2; }
		}

	// tee: copy stdin to stdout and each file (-a append). Text path.
	private static int Tee(List<string> args)
		{
		bool append = false; var files = new List<string>();
		foreach (var a in args) { if (a == "-a") append = true; else if (!a.StartsWith('-')) files.Add(a); }
		string input = Console.In.ReadToEnd();
		Console.Out.Write(input);
		int rc = 0;
		foreach (var f in files)
			try
				{
				var p = ShellEnvironment.TranslatePath(f);
				if (append) File.AppendAllText(p, input); else File.WriteAllText(p, input);
				}
			catch (Exception ex) { Console.Error.WriteLine($"tee: {f}: {ex.Message}"); rc = 1; }
		return rc;
		}

	// comm: 3-column compare of two SORTED files; -1/-2/-3 suppress the respective column.
	private static int Comm(List<string> args)
		{
		bool s1 = false, s2 = false, s3 = false; var files = new List<string>();
		foreach (var a in args)
			{
			if (a.Length > 1 && a[0] == '-' && a.Skip(1).All(c => c is '1' or '2' or '3'))
				foreach (char c in a[1..]) { if (c == '1') s1 = true; else if (c == '2') s2 = true; else s3 = true; }
			else files.Add(a);
			}
		if (files.Count < 2) { Console.Error.WriteLine("comm: missing operand"); return 1; }
		var A = LinesOf(files[0]); var B = LinesOf(files[1]);
		string p2 = s1 ? "" : "\t";
		string p3 = (s1 ? "" : "\t") + (s2 ? "" : "\t");
		void C1(string l) { if (!s1) Console.WriteLine(l); }
		void C2(string l) { if (!s2) Console.WriteLine(p2 + l); }
		void C3(string l) { if (!s3) Console.WriteLine(p3 + l); }
		int i = 0, j = 0;
		while (i < A.Count && j < B.Count)
			{
			int c = string.CompareOrdinal(A[i], B[j]);
			if      (c < 0) C1(A[i++]);
			else if (c > 0) C2(B[j++]);
			else            { C3(A[i++]); j++; }
			}
		while (i < A.Count) C1(A[i++]);
		while (j < B.Count) C2(B[j++]);
		return 0;
		}

	// paste: merge corresponding lines of files side by side (-d delims, cycled; default tab).
	private static int Paste(List<string> args)
		{
		string delims = "\t"; var files = new List<string>();
		for (int i = 0; i < args.Count; i++)
			{
			var a = args[i];
			if      (a == "-d" && i + 1 < args.Count) delims = UnescapeDelims(args[++i]);
			else if (a.StartsWith("-d") && a.Length > 2) delims = UnescapeDelims(a[2..]);
			else if (a == "-" || !a.StartsWith('-')) files.Add(a);
			}
		if (delims.Length == 0) delims = "\t";
		if (files.Count == 0) files.Add("-");
		var cols = files.Select(LinesOf).ToList();
		int max = cols.Max(c => c.Count);
		for (int r = 0; r < max; r++)
			{
			var sb = new System.Text.StringBuilder();
			for (int c = 0; c < cols.Count; c++)
				{
				if (c > 0) sb.Append(delims[(c - 1) % delims.Length]);
				if (r < cols[c].Count) sb.Append(cols[c][r]);
				}
			Console.WriteLine(sb.ToString());
			}
		return 0;
		}

	private static string UnescapeDelims(string s)
		{
		var sb = new System.Text.StringBuilder();
		for (int i = 0; i < s.Length; i++)
			if (s[i] == '\\' && i + 1 < s.Length) { sb.Append(s[++i] switch { 't' => '\t', 'n' => '\n', '\\' => '\\', '0' => '\0', var x => x }); }
			else sb.Append(s[i]);
		return sb.ToString();
		}

	// ── destructive file ops (wave 3) — glob already expanded by the shell ──────

	private static int Rm(List<string> args)
		{
		bool recursive = false, force = false;
		var paths = new List<string>();
		foreach (var a in args)
			{
			if (a.Length > 1 && a[0] == '-' && a != "--" && a.Skip(1).All(c => "rRfi".Contains(c)))
				foreach (char c in a[1..]) { if (c is 'r' or 'R') recursive = true; else if (c == 'f') force = true; }
			else if (a != "--") paths.Add(a);
			}
		if (paths.Count == 0) { if (!force) Console.Error.WriteLine("rm: missing operand"); return force ? 0 : 1; }
		int rc = 0;
		foreach (var f in paths)
			{
			var p = ShellEnvironment.TranslatePath(f);
			try
				{
				var full = Path.GetFullPath(p);
				// Footgun guard: never recursively delete a filesystem root (/, C:\, …).
				if (recursive && string.Equals(full, Path.GetPathRoot(full), StringComparison.OrdinalIgnoreCase))
					{ Console.Error.WriteLine($"rm: refusing to remove root directory '{f}'"); rc = 1; continue; }
				if (Directory.Exists(p))
					{
					if (!recursive) { Console.Error.WriteLine($"rm: cannot remove '{f}': Is a directory"); rc = 1; }
					else Directory.Delete(p, true);
					}
				else if (File.Exists(p)) File.Delete(p);
				else if (!force) { Console.Error.WriteLine($"rm: cannot remove '{f}': No such file or directory"); rc = 1; }
				}
			catch (Exception ex) { if (!force) { Console.Error.WriteLine($"rm: cannot remove '{f}': {ex.Message}"); rc = 1; } }
			}
		return rc;
		}

	private static int Mv(List<string> args)
		{
		var ps = new List<string>();
		foreach (var a in args)
			{ if (!(a.Length > 1 && a[0] == '-' && a.Skip(1).All(c => "fin".Contains(c)))) ps.Add(a); }
		if (ps.Count < 2) { Console.Error.WriteLine("mv: missing destination operand"); return 1; }
		var dst = ps[^1]; var srcs = ps.GetRange(0, ps.Count - 1);
		var dstP = ShellEnvironment.TranslatePath(dst);
		bool dstIsDir = Directory.Exists(dstP);
		if (srcs.Count > 1 && !dstIsDir) { Console.Error.WriteLine($"mv: target '{dst}' is not a directory"); return 1; }
		int rc = 0;
		foreach (var s in srcs)
			{
			var sp = ShellEnvironment.TranslatePath(s);
			try
				{
				var target = dstIsDir ? Path.Combine(dstP, Path.GetFileName(sp.TrimEnd('/', '\\'))) : dstP;
				if (Directory.Exists(sp))
					{
					if (Directory.Exists(target)) { Console.Error.WriteLine($"mv: cannot overwrite directory '{target}'"); rc = 1; continue; }
					try { Directory.Move(sp, target); }
					catch (IOException) { CopyDir(sp, target); Directory.Delete(sp, true); }   // cross-volume
					}
				else if (File.Exists(sp)) File.Move(sp, target, overwrite: true);
				else { Console.Error.WriteLine($"mv: cannot stat '{s}': No such file or directory"); rc = 1; }
				}
			catch (Exception ex) { Console.Error.WriteLine($"mv: cannot move '{s}': {ex.Message}"); rc = 1; }
			}
		return rc;
		}

	private static int Cp(List<string> args)
		{
		bool recursive = false;
		var ps = new List<string>();
		foreach (var a in args)
			{
			if (a.Length > 1 && a[0] == '-' && a.Skip(1).All(c => "rRfp".Contains(c)))
				foreach (char c in a[1..]) { if (c is 'r' or 'R') recursive = true; }
			else ps.Add(a);
			}
		if (ps.Count < 2) { Console.Error.WriteLine("cp: missing destination operand"); return 1; }
		var dst = ps[^1]; var srcs = ps.GetRange(0, ps.Count - 1);
		var dstP = ShellEnvironment.TranslatePath(dst);
		bool dstIsDir = Directory.Exists(dstP);
		if (srcs.Count > 1 && !dstIsDir) { Console.Error.WriteLine($"cp: target '{dst}' is not a directory"); return 1; }
		int rc = 0;
		foreach (var s in srcs)
			{
			var sp = ShellEnvironment.TranslatePath(s);
			try
				{
				var target = dstIsDir ? Path.Combine(dstP, Path.GetFileName(sp.TrimEnd('/', '\\'))) : dstP;
				if (Directory.Exists(sp))
					{
					if (!recursive) { Console.Error.WriteLine($"cp: -r not specified; omitting directory '{s}'"); rc = 1; }
					else CopyDir(sp, target);
					}
				else if (File.Exists(sp)) File.Copy(sp, target, overwrite: true);
				else { Console.Error.WriteLine($"cp: cannot stat '{s}': No such file or directory"); rc = 1; }
				}
			catch (Exception ex) { Console.Error.WriteLine($"cp: cannot copy '{s}': {ex.Message}"); rc = 1; }
			}
		return rc;
		}

	private static void CopyDir(string src, string dst)
		{
		Directory.CreateDirectory(dst);
		foreach (var f in Directory.GetFiles(src)) File.Copy(f, Path.Combine(dst, Path.GetFileName(f)), true);
		foreach (var d in Directory.GetDirectories(src)) CopyDir(d, Path.Combine(dst, Path.GetFileName(d)));
		}

	// ── system / number utilities (wave 4a) ─────────────────────────────────────

	private static int Uname(List<string> args)
		{
		bool s = false, n = false, r = false, m = false;
		foreach (var a in args)
			if (a.StartsWith('-'))
				foreach (char c in a[1..])
					switch (c) { case 'a': s = n = r = m = true; break; case 's': s = true; break;
					             case 'n': n = true; break; case 'r': r = true; break; case 'm': m = true; break; }
		if (!s && !n && !r && !m) s = true;   // default: -s
		var parts = new List<string>();
		if (s) parts.Add("Windows_NT");
		if (n) parts.Add(Environment.MachineName);
		if (r) parts.Add(Environment.OSVersion.Version.ToString());
		if (m) parts.Add(System.Runtime.InteropServices.RuntimeInformation.OSArchitecture switch
			{ System.Runtime.InteropServices.Architecture.X64 => "x86_64",
			  System.Runtime.InteropServices.Architecture.Arm64 => "aarch64",
			  var x => x.ToString().ToLowerInvariant() });
		Console.WriteLine(string.Join(" ", parts));
		return 0;
		}

	private static int Hostname(List<string> args) { Console.WriteLine(Environment.MachineName); return 0; }

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

	// BreToNet: translate a POSIX Basic Regular Expression to .NET (ERE-like) syntax, so
	// the classic `\(...\)`, `\{m,n\}`, `\+`, `\?`, `\|` idioms work and bare ( ) { } + ? |
	// are treated as literals (as BRE requires). Backreferences \1..\9 and escapes like
	// \. \* \\ pass through unchanged. Used by sed/grep in their default (non -E) mode.
	private static string BreToNet(string pat)
		{
		var sb = new System.Text.StringBuilder(pat.Length + 8);
		for (int i = 0; i < pat.Length; i++)
			{
			char c = pat[i];
			if (c == '\\' && i + 1 < pat.Length)
				{
				char n = pat[++i];
				if (n is '(' or ')' or '{' or '}' or '+' or '?' or '|') sb.Append(n);   // \X → special
				else { sb.Append('\\'); sb.Append(n); }                                 // keep escape
				}
			else if (c is '(' or ')' or '{' or '}' or '+' or '?' or '|') { sb.Append('\\'); sb.Append(c); }  // bare → literal
			else sb.Append(c);
			}
		return sb.ToString();
		}

	// sed: SCOPED stream editor. Supports -n, -e SCRIPT (repeatable), -r/-E (use ERE: bare
	// ()/{}/+/?/| are operators), and per-line commands: [addr]s/re/repl/flags (g/p/i/N),
	// [addr]d, [addr]p, where addr is a line number, $, or /regex/. Replacement honours
	// & and \1..\9 (and \n \t \\ \&). Reads files or stdin; NO in-place (-i). Address
	// ranges (addr1,addr2) and advanced commands (a/c/i/y/hold space) are NOT supported.
	private sealed class SedCmd
		{
		public int AddrKind;   // 0 none, 1 line#, 2 last, 3 regex
		public int AddrNum;
		public System.Text.RegularExpressions.Regex? AddrRe;
		public char Cmd;
		public System.Text.RegularExpressions.Regex? SRe;
		public string SRepl = "";
		public bool SGlobal, SPrint;
		public int SNth;
		}

	private static int Sed(List<string> args)
		{
		bool noAuto = false, ere = false;
		var scripts = new List<string>();
		var files = new List<string>();
		bool scriptTaken = false;
		for (int i = 0; i < args.Count; i++)
			{
			var a = args[i];
			if      (a == "-n") noAuto = true;
			else if (a == "-e" && i + 1 < args.Count) { scripts.Add(args[++i]); scriptTaken = true; }
			else if (a == "-r" || a == "-E") ere = true;
			else if (a.StartsWith('-') && a != "-") { }   // ignore unknown flags
			else if (!scriptTaken) { scripts.Add(a); scriptTaken = true; }
			else files.Add(a);
			}
		var cmds = new List<SedCmd>();
		try { foreach (var s in scripts) SedParse(s, cmds, ere); }
		catch (Exception ex) { Console.Error.WriteLine($"sed: {ex.Message}"); return 2; }

		IEnumerable<string> lines;
		int rc = 0;
		if (files.Count == 0)
			{
			var acc = new List<string>(); string? l;
			while ((l = Console.In.ReadLine()) is not null) acc.Add(l);
			lines = acc;
			}
		else
			{
			var acc = new List<string>();
			foreach (var f in files)
				{
				try { acc.AddRange(File.ReadAllLines(ShellEnvironment.TranslatePath(f))); }
				catch (Exception ex) { Console.Error.WriteLine($"sed: {f}: {ex.Message}"); rc = 2; }
				}
			lines = acc;
			}

		var arr = lines as List<string> ?? [.. lines];
		for (int i = 0; i < arr.Count; i++)
			{
			string? ps = arr[i];
			int lineNo = i + 1; bool isLast = i == arr.Count - 1;
			bool autoprint = !noAuto;
			foreach (var cmd in cmds)
				{
				bool addrOk = cmd.AddrKind switch
					{
					1 => lineNo == cmd.AddrNum,
					2 => isLast,
					3 => cmd.AddrRe!.IsMatch(ps!),
					_ => true
					};
				if (!addrOk) continue;
				if (cmd.Cmd == 'd') { autoprint = false; ps = null; break; }
				if (cmd.Cmd == 'p') { Console.WriteLine(ps); continue; }
				if (cmd.Cmd == 's')
					{
					var (res, changed) = SedSub(cmd, ps!);
					ps = res;
					if (changed && cmd.SPrint) Console.WriteLine(ps);
					}
				}
			if (autoprint && ps is not null) Console.WriteLine(ps);
			}
		return rc;
		}

	private static void SedParse(string s, List<SedCmd> cmds, bool ere)
		{
		int p = 0;
		while (p < s.Length)
			{
			while (p < s.Length && (s[p] == ';' || s[p] == '\n' || s[p] == ' ' || s[p] == '\t')) p++;
			if (p >= s.Length) break;
			var c = new SedCmd();
			if (char.IsDigit(s[p])) { int n = 0; while (p < s.Length && char.IsDigit(s[p])) n = n * 10 + (s[p++] - '0'); c.AddrKind = 1; c.AddrNum = n; }
			else if (s[p] == '$') { c.AddrKind = 2; p++; }
			else if (s[p] == '/') { p++; var re = SedReadDelim(s, ref p, '/'); c.AddrKind = 3; c.AddrRe = new System.Text.RegularExpressions.Regex(ere ? re : BreToNet(re)); }
			while (p < s.Length && (s[p] == ' ' || s[p] == '\t')) p++;
			if (p >= s.Length) throw new Exception("missing command");
			char cmd = s[p++];
			c.Cmd = cmd;
			if (cmd == 's')
				{
				if (p >= s.Length) throw new Exception("unterminated `s' command");
				char delim = s[p++];
				string pat = SedReadDelim(s, ref p, delim);
				c.SRepl = SedReadDelim(s, ref p, delim);
				bool ic = false;
				while (p < s.Length && (char.IsLetterOrDigit(s[p])))
					{
					char f = s[p++];
					if (f == 'g') c.SGlobal = true;
					else if (f == 'p') c.SPrint = true;
					else if (f == 'i' || f == 'I') ic = true;
					else if (char.IsDigit(f)) c.SNth = c.SNth * 10 + (f - '0');
					}
				c.SRe = new System.Text.RegularExpressions.Regex(ere ? pat : BreToNet(pat), ic ? System.Text.RegularExpressions.RegexOptions.IgnoreCase : System.Text.RegularExpressions.RegexOptions.None);
				}
			else if (cmd != 'd' && cmd != 'p') throw new Exception($"unknown command: `{cmd}'");
			cmds.Add(c);
			}
		}

	// Read up to (and consume) the next unescaped delim; \<delim> becomes a literal delim,
	// other backslash escapes are preserved for the regex / replacement to interpret.
	private static string SedReadDelim(string s, ref int p, char delim)
		{
		var sb = new System.Text.StringBuilder();
		while (p < s.Length && s[p] != delim)
			{
			if (s[p] == '\\' && p + 1 < s.Length)
				{
				if (s[p + 1] == delim) { sb.Append(delim); p += 2; continue; }
				sb.Append(s[p]); sb.Append(s[p + 1]); p += 2; continue;
				}
			sb.Append(s[p]); p++;
			}
		if (p < s.Length && s[p] == delim) p++;
		return sb.ToString();
		}

	private static (string result, bool changed) SedSub(SedCmd cmd, string input)
		{
		var matches = cmd.SRe!.Matches(input);
		if (matches.Count == 0) return (input, false);
		int target = cmd.SNth == 0 ? 1 : cmd.SNth;
		var sb = new System.Text.StringBuilder();
		int last = 0, idx = 0; bool changed = false;
		foreach (System.Text.RegularExpressions.Match mt in matches)
			{
			idx++;
			bool doRepl = cmd.SGlobal ? idx >= target : idx == target;
			sb.Append(input, last, mt.Index - last);
			if (doRepl) { sb.Append(SedExpand(cmd.SRepl, mt)); changed = true; }
			else sb.Append(mt.Value);
			last = mt.Index + mt.Length;
			}
		sb.Append(input, last, input.Length - last);
		return (sb.ToString(), changed);
		}

	// Expand a sed replacement: & = whole match, \1..\9 = groups, \n \t \\ \& literals.
	private static string SedExpand(string repl, System.Text.RegularExpressions.Match m)
		{
		var sb = new System.Text.StringBuilder();
		for (int i = 0; i < repl.Length; i++)
			{
			char c = repl[i];
			if (c == '&') { sb.Append(m.Value); continue; }
			if (c == '\\' && i + 1 < repl.Length)
				{
				char n = repl[++i];
				if (char.IsDigit(n)) { int g = n - '0'; if (g < m.Groups.Count) sb.Append(m.Groups[g].Value); }
				else sb.Append(n switch { 'n' => '\n', 't' => '\t', '&' => '&', '\\' => '\\', _ => n });
				continue;
				}
			sb.Append(c);
			}
		return sb.ToString();
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

	// grep: line matcher. Flags -i/-v/-n/-c/-l/-w/-F/-E/-r/-q/-h/-H, -e PATTERN. Reads
	// files or stdin. Default patterns are BRE (translated via BreToNet: \(...\), \{m,n\},
	// \+ \? \| are operators; bare ()/{}/+/?/| literal); -E uses ERE (.NET) directly; -F is
	// literal. Returns 0 (match), 1 (no match), 2 (error).
	private static int Grep(List<string> args)
		{
		bool ignoreCase = false, invert = false, lineNum = false, countOnly = false, listFiles = false,
			 wordMatch = false, fixedStr = false, recursive = false, quiet = false, noFilename = false, withFilename = false, ere = false;
		string? pattern = null;
		var files = new List<string>();
		for (int i = 0; i < args.Count; i++)
			{
			var a = args[i];
			if (a == "-e" && i + 1 < args.Count) { pattern = args[++i]; continue; }
			if (a.Length > 1 && a[0] == '-' && a != "--")
				{
				foreach (char c in a[1..]) switch (c)
					{
					case 'i': ignoreCase = true; break;
					case 'v': invert = true; break;
					case 'n': lineNum = true; break;
					case 'c': countOnly = true; break;
					case 'l': listFiles = true; break;
					case 'w': wordMatch = true; break;
					case 'F': fixedStr = true; break;
					case 'E': ere = true; break;
					case 'r': case 'R': recursive = true; break;
					case 'q': quiet = true; break;
					case 'h': noFilename = true; break;
					case 'H': withFilename = true; break;
					default: break;
					}
				continue;
				}
			if (pattern is null) pattern = a;
			else files.Add(a);
			}
		if (pattern is null) { Console.Error.WriteLine("grep: no pattern"); return 2; }

		string pat = fixedStr ? System.Text.RegularExpressions.Regex.Escape(pattern) : ere ? pattern : BreToNet(pattern);
		if (wordMatch) pat = $@"\b(?:{pat})\b";
		System.Text.RegularExpressions.Regex re;
		try { re = new System.Text.RegularExpressions.Regex(pat, ignoreCase ? System.Text.RegularExpressions.RegexOptions.IgnoreCase : System.Text.RegularExpressions.RegexOptions.None); }
		catch (Exception ex) { Console.Error.WriteLine($"grep: {ex.Message}"); return 2; }

		var inputs = new List<(string name, bool isStdin)>();
		if (recursive)
			foreach (var f in files.Count == 0 ? ["."] : files) GrepCollect(f, inputs);
		else if (files.Count == 0) inputs.Add(("(standard input)", true));
		else foreach (var f in files) inputs.Add((f, false));

		bool showName = withFilename || (!noFilename && (inputs.Count > 1 || recursive));
		bool anyMatch = false, errored = false;
		foreach (var (name, isStdin) in inputs)
			{
			string[] lines;
			if (isStdin)
				{
				var acc = new List<string>(); string? l;
				while ((l = Console.In.ReadLine()) is not null) acc.Add(l);
				lines = [.. acc];
				}
			else
				{
				try { lines = File.ReadAllLines(ShellEnvironment.TranslatePath(name)); }
				catch (Exception ex) { Console.Error.WriteLine($"grep: {name}: {ex.Message}"); errored = true; continue; }
				}
			int count = 0, lineNo = 0; bool fileHadMatch = false;
			foreach (var line in lines)
				{
				lineNo++;
				bool m = re.IsMatch(line);
				if (invert) m = !m;
				if (!m) continue;
				anyMatch = true; fileHadMatch = true; count++;
				if (quiet) return 0;
				if (listFiles || countOnly) continue;
				var sb = new System.Text.StringBuilder();
				if (showName) sb.Append(name).Append(':');
				if (lineNum) sb.Append(lineNo).Append(':');
				sb.Append(line);
				Console.WriteLine(sb.ToString());
				}
			if (countOnly) { var sb = new System.Text.StringBuilder(); if (showName) sb.Append(name).Append(':'); sb.Append(count); Console.WriteLine(sb.ToString()); }
			else if (listFiles && fileHadMatch) Console.WriteLine(name);
			}
		return errored ? 2 : anyMatch ? 0 : 1;
		}
	private static void GrepCollect(string path, List<(string, bool)> inputs)
		{
		var fs = ShellEnvironment.TranslatePath(path);
		if (File.Exists(fs)) { inputs.Add((path, false)); return; }
		if (!Directory.Exists(fs)) { Console.Error.WriteLine($"grep: {path}: No such file or directory"); return; }
		string[] entries;
		try { entries = Directory.GetFileSystemEntries(fs); }
		catch (Exception ex) { Console.Error.WriteLine($"grep: {path}: {ex.Message}"); return; }
		Array.Sort(entries, StringComparer.Ordinal);
		foreach (var e in entries) GrepCollect($"{path}/{Path.GetFileName(e)}", inputs);
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

	// diff: LCS-based comparison of two files, GNU "normal" format (NcM / NdM / NaM hunks
	// with `< ` / `---` / `> ` lines). -q brief ("Files X and Y differ"), -i ignore case.
	// Line-based (File.ReadAllLines strips EOLs; trailing-newline diffs not reported).
	// Returns 0 (same), 1 (differ), 2 (error).
	private static int Diff(List<string> args)
		{
		bool brief = false, ignoreCase = false;
		var files = new List<string>();
		foreach (var a in args)
			{
			if      (a == "-q") brief = true;
			else if (a == "-i") ignoreCase = true;
			else if (!a.StartsWith('-') || a == "-") files.Add(a);
			}
		if (files.Count < 2) { Console.Error.WriteLine("diff: missing operand"); return 2; }
		string[] a1, b1;
		try
			{
			a1 = File.ReadAllLines(ShellEnvironment.TranslatePath(files[0]));
			b1 = File.ReadAllLines(ShellEnvironment.TranslatePath(files[1]));
			}
		catch (Exception ex) { Console.Error.WriteLine($"diff: {ex.Message}"); return 2; }

		bool Eq(string x, string y) => ignoreCase ? string.Equals(x, y, StringComparison.OrdinalIgnoreCase) : x == y;

		int n = a1.Length, m = b1.Length;
		var c = new int[n + 1, m + 1];
		for (int ii = n - 1; ii >= 0; ii--)
			for (int jj = m - 1; jj >= 0; jj--)
				c[ii, jj] = Eq(a1[ii], b1[jj]) ? c[ii + 1, jj + 1] + 1 : Math.Max(c[ii + 1, jj], c[ii, jj + 1]);

		// LCS alignment as matched index pairs, plus an (n,m) sentinel.
		var matches = new List<(int x, int y)>();
		{
		int x = 0, y = 0;
		while (x < n && y < m)
			{
			if (Eq(a1[x], b1[y])) { matches.Add((x, y)); x++; y++; }
			else if (c[x + 1, y] >= c[x, y + 1]) x++;
			else y++;
			}
		}
		matches.Add((n, m));

		bool differ = false;
		int prevA = 0, prevB = 0;
		foreach (var (mx, my) in matches)
			{
			int delS = prevA, delE = mx - 1, insS = prevB, insE = my - 1;
			bool hasDel = delE >= delS, hasIns = insE >= insS;
			if (hasDel || hasIns)
				{
				differ = true;
				if (!brief)
					{
					string ra = delS == delE ? $"{delS + 1}" : $"{delS + 1},{delE + 1}";
					string rb = insS == insE ? $"{insS + 1}" : $"{insS + 1},{insE + 1}";
					if (hasDel && hasIns)
						{
						Console.WriteLine($"{ra}c{rb}");
						for (int x = delS; x <= delE; x++) Console.WriteLine($"< {a1[x]}");
						Console.WriteLine("---");
						for (int y = insS; y <= insE; y++) Console.WriteLine($"> {b1[y]}");
						}
					else if (hasDel)
						{
						Console.WriteLine($"{ra}d{insS}");
						for (int x = delS; x <= delE; x++) Console.WriteLine($"< {a1[x]}");
						}
					else
						{
						Console.WriteLine($"{delS}a{rb}");
						for (int y = insS; y <= insE; y++) Console.WriteLine($"> {b1[y]}");
						}
					}
				}
			prevA = mx + 1; prevB = my + 1;
			}

		if (differ && brief) Console.WriteLine($"Files {files[0]} and {files[1]} differ");
		return differ ? 1 : 0;
		}

	// xargs: read whitespace- (or NUL-, -0) separated items from stdin and run the given
	// command (default echo) with them appended. -n N items per invocation; -I TOKEN runs
	// once per item, substituting TOKEN in the command words. Dispatches via _eval.
	private int Xargs(List<string> args)
		{
		int maxArgs = 0; bool nullSep = false; string? replace = null;
		int i = 0;
		for (; i < args.Count; i++)
			{
			var a = args[i];
			if      (a == "-n" && i + 1 < args.Count) int.TryParse(args[++i], out maxArgs);
			else if (a.StartsWith("-n") && a.Length > 2) int.TryParse(a[2..], out maxArgs);
			else if (a == "-0" || a == "--null") nullSep = true;
			else if (a == "-I" && i + 1 < args.Count) { replace = args[++i]; maxArgs = 1; }
			else if (a.StartsWith("-I") && a.Length > 2) { replace = a[2..]; maxArgs = 1; }
			else break;   // first non-option word starts the command
			}
		var cmd = args.GetRange(i, args.Count - i);
		if (cmd.Count == 0) cmd = ["echo"];

		var input = Console.In.ReadToEnd();
		var items = nullSep
			? input.Split('\0', StringSplitOptions.RemoveEmptyEntries)
			: input.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);

		int rc = 0;
		if (replace is not null)
			{
			foreach (var item in items)
				{
				var line = new List<string>(cmd.Count);
				foreach (var c in cmd) line.Add(c.Replace(replace, item));
				rc = _eval.RunCommand(line[0], line.GetRange(1, line.Count - 1));
				}
			}
		else if (items.Length == 0)
			rc = _eval.RunCommand(cmd[0], cmd.GetRange(1, cmd.Count - 1));
		else
			{
			int batch = maxArgs > 0 ? maxArgs : items.Length;
			for (int k = 0; k < items.Length; k += batch)
				{
				var line = new List<string>(cmd);
				for (int j = k; j < Math.Min(k + batch, items.Length); j++) line.Add(items[j]);
				rc = _eval.RunCommand(line[0], line.GetRange(1, line.Count - 1));
				}
			}
		return rc;
		}

	// ls: SCOPED — one entry per line (never columns), names only. -a (incl . ..),
	// -A (incl dotfiles but not . ..), -r reverse, -d list dir itself, -1 (no-op).
	// -l is intentionally NOT implemented: owner/group/perms/mtime are platform-specific
	// and not byte-faithfully reproducible on Windows. Sort is Ordinal (ASCII, case-
	// sensitive) — differs from GNU's locale/case-insensitive collation.
	private static int Ls(List<string> args)
		{
		bool all = false, almostAll = false, reverse = false, dirSelf = false;
		var operands = new List<string>();
		foreach (var a in args)
			{
			if (a.Length > 1 && a[0] == '-' && a != "--")
				foreach (char c in a[1..]) switch (c)
					{
					case 'a': all = true; break;
					case 'A': almostAll = true; break;
					case 'r': reverse = true; break;
					case 'd': dirSelf = true; break;
					case '1': break;   // already one-per-line
					default: break;
					}
			else operands.Add(a);
			}
		if (operands.Count == 0) operands.Add(".");
		bool header = operands.Count > 1 && !dirSelf;
		bool first = true; int rc = 0;
		foreach (var op in operands)
			{
			var fs = ShellEnvironment.TranslatePath(op);
			if (Directory.Exists(fs) && !dirSelf)
				{
				if (header) { if (!first) Console.WriteLine(); Console.WriteLine($"{op}:"); }
				first = false;
				var names = new List<string>();
				if (all) { names.Add("."); names.Add(".."); }
				try
					{
					foreach (var e in Directory.GetFileSystemEntries(fs))
						{
						var n = Path.GetFileName(e);
						if (!all && !almostAll && n.StartsWith('.')) continue;
						names.Add(n);
						}
					}
				catch (Exception ex) { Console.Error.WriteLine($"ls: cannot open '{op}': {ex.Message}"); rc = 2; continue; }
				names.Sort(StringComparer.Ordinal);
				if (reverse) names.Reverse();
				foreach (var n in names) Console.WriteLine(n);
				}
			else if (File.Exists(fs) || Directory.Exists(fs))
				{ Console.WriteLine(op); first = false; }
			else
				{ Console.Error.WriteLine($"ls: cannot access '{op}': No such file or directory"); rc = 2; }
			}
		return rc;
		}

	// find: [path...] [-maxdepth N] [-name GLOB | -iname GLOB] [-type f|d] [-print].
	// Pre-order walk; prints paths (forward-slash, start-path-prefixed) like GNU find.
	// Unknown predicates are ignored (best-effort, not a full expression engine).
	private static int Find(List<string> args)
		{
		var paths = new List<string>();
		string? namePat = null; bool iname = false; char typeFilter = '\0'; int maxDepth = int.MaxValue;
		int i = 0;
		for (; i < args.Count && !args[i].StartsWith('-') && args[i] != "!" && args[i] != "("; i++)
			paths.Add(args[i]);
		for (; i < args.Count; i++)
			{
			switch (args[i])
				{
				case "-name":     if (i + 1 < args.Count) { namePat = args[++i]; iname = false; } break;
				case "-iname":    if (i + 1 < args.Count) { namePat = args[++i]; iname = true; } break;
				case "-type":     if (i + 1 < args.Count && args[++i].Length > 0) typeFilter = args[i][0]; break;
				case "-maxdepth": if (i + 1 < args.Count) int.TryParse(args[++i], out maxDepth); break;
				default: break;   // -print and unknown predicates: ignore
				}
			}
		if (paths.Count == 0) paths.Add(".");
		foreach (var p in paths) FindWalk(p.TrimEnd('/'), 0, maxDepth, namePat, iname, typeFilter);
		return 0;
		}
	private static void FindWalk(string display, int depth, int maxDepth, string? namePat, bool iname, char typeFilter)
		{
		var fsPath = ShellEnvironment.TranslatePath(display);
		bool isDir = Directory.Exists(fsPath);
		bool isFile = File.Exists(fsPath);
		if (!isDir && !isFile) { Console.Error.WriteLine($"find: '{display}': No such file or directory"); return; }

		bool typeOk = typeFilter switch { 'f' => isFile, 'd' => isDir, _ => true };
		bool nameOk = namePat is null
			|| Evaluator.GlobMatch(iname ? namePat.ToLowerInvariant() : namePat,
			                       iname ? Path.GetFileName(display).ToLowerInvariant() : Path.GetFileName(display));
		if (typeOk && nameOk) Console.WriteLine(display);

		if (!isDir || depth >= maxDepth) return;
		string[] entries;
		try { entries = Directory.GetFileSystemEntries(fsPath); }
		catch (Exception ex) { Console.Error.WriteLine($"find: '{display}': {ex.Message}"); return; }
		Array.Sort(entries, StringComparer.Ordinal);
		foreach (var e in entries)
			FindWalk($"{display}/{Path.GetFileName(e)}", depth + 1, maxDepth, namePat, iname, typeFilter);
		}

	// split: -l N lines/piece (default 1000) or -b N[k|m|g] bytes/piece. Operands:
	// [file [prefix]] (prefix default "x"). Output pieces are prefix+aa, +ab, ...
	private static int Split(List<string> args)
		{
		int lines = 1000; long byteSize = 0; bool byBytes = false;
		string? file = null, prefix = null;
		for (int i = 0; i < args.Count; i++)
			{
			var a = args[i];
			if      (a == "-l" && i + 1 < args.Count) { byBytes = false; int.TryParse(args[++i], out lines); }
			else if (a.StartsWith("-l") && a.Length > 2) { byBytes = false; int.TryParse(a[2..], out lines); }
			else if (a == "-b" && i + 1 < args.Count) { byBytes = true; byteSize = ParseSize(args[++i]); }
			else if (a.StartsWith("-b") && a.Length > 2) { byBytes = true; byteSize = ParseSize(a[2..]); }
			else if (a == "-" || !a.StartsWith('-')) { if (file is null) file = a; else prefix = a; }
			}
		prefix ??= "x";
		byte[] data;
		try { data = file is null || file == "-" ? System.Text.Encoding.UTF8.GetBytes(Console.In.ReadToEnd()) : File.ReadAllBytes(ShellEnvironment.TranslatePath(file)); }
		catch (Exception ex) { Console.Error.WriteLine($"split: {ex.Message}"); return 1; }

		int idx = 0;
		if (byBytes && byteSize > 0)
			{
			for (long off = 0; off < data.Length; off += byteSize)
				SplitWrite(prefix, idx++, data, (int)off, (int)Math.Min(byteSize, data.Length - off));
			}
		else
			{
			int start = 0, count = 0;
			for (int i = 0; i < data.Length; i++)
				{
				if (data[i] != (byte)'\n') continue;
				if (++count == lines) { SplitWrite(prefix, idx++, data, start, i - start + 1); start = i + 1; count = 0; }
				}
			if (start < data.Length) SplitWrite(prefix, idx++, data, start, data.Length - start);
			}
		return 0;
		}
	private static void SplitWrite(string prefix, int idx, byte[] data, int off, int len)
		{
		string suffix = $"{(char)('a' + idx / 26)}{(char)('a' + idx % 26)}";
		using var fs = File.Create(ShellEnvironment.TranslatePath(prefix + suffix));
		fs.Write(data, off, len);
		}
	private static long ParseSize(string s)
		{
		if (s.Length == 0) return 0;
		long mult = char.ToLowerInvariant(s[^1]) switch { 'k' => 1024, 'm' => 1024 * 1024, 'g' => 1024L * 1024 * 1024, _ => 1 };
		var num = mult == 1 ? s : s[..^1];
		return long.TryParse(num, out var v) ? v * mult : 0;
		}

	// sort: -n numeric, -r reverse, -u unique, -f fold-case, -k FIELD (single field, not
	// N-to-EOL), -t DELIM. Reads file operands or stdin.
	private static int Sort(List<string> args)
		{
		bool numeric = false, reverse = false, unique = false, fold = false;
		int keyField = 0; char? delim = null;
		var files = new List<string>();
		for (int i = 0; i < args.Count; i++)
			{
			var a = args[i];
			if (a == "-") { files.Add(a); continue; }
			if (a.StartsWith('-') && a.Length > 1)
				{
				if      (a == "-k" && i + 1 < args.Count) int.TryParse(args[++i].Split('.')[0], out keyField);
				else if (a.StartsWith("-k")) int.TryParse(a[2..].Split('.')[0], out keyField);
				else if (a == "-t" && i + 1 < args.Count) delim = args[++i].Length > 0 ? args[i][0] : (char?)null;
				else if (a.StartsWith("-t") && a.Length > 2) delim = a[2];
				else foreach (char c in a[1..]) { if (c == 'n') numeric = true; else if (c == 'r') reverse = true; else if (c == 'u') unique = true; else if (c == 'f') fold = true; }
				}
			else files.Add(a);
			}
		var lines = new List<string>();
		try
			{
			if (files.Count == 0) { string? l; while ((l = Console.In.ReadLine()) is not null) lines.Add(l); }
			else foreach (var f in files) lines.AddRange(LinesOf(f));
			}
		catch (Exception ex) { Console.Error.WriteLine($"sort: {ex.Message}"); return 1; }

		string Key(string s)
			{
			if (keyField <= 0) return s;
			var parts = delim is char d ? s.Split(d) : s.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
			return keyField <= parts.Length ? parts[keyField - 1] : "";
			}
		int Cmp(string x, string y)
			{
			string kx = Key(x), ky = Key(y);
			int c = numeric
				? (double.TryParse(kx, out var nx) ? nx : 0).CompareTo(double.TryParse(ky, out var ny) ? ny : 0)
				: string.Compare(kx, ky, fold ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);
			return reverse ? -c : c;
			}
		lines.Sort(Cmp);
		string? last = null;
		foreach (var l in lines)
			{
			if (unique && last is not null && (fold ? string.Equals(l, last, StringComparison.OrdinalIgnoreCase) : l == last)) continue;
			Console.WriteLine(l); last = l;
			}
		return 0;
		}

	// od: octal/hex/char dump. -c chars, -tx1 hex bytes, -b octal bytes, default octal
	// 2-byte words; -An suppresses the offset column. Reads file bytes (stdin via text).
	private static int Od(List<string> args)
		{
		bool cMode = false, hex = false, octB = false, noAddr = false;
		var files = new List<string>();
		foreach (var a in args)
			{
			if (a == "-c") cMode = true;
			else if (a == "-b") octB = true;
			else if (a == "-x" || a.StartsWith("-tx")) hex = true;
			else if (a == "-An" || (a.StartsWith("-A") && a.Contains('n'))) noAddr = true;
			else if (a == "-" || !a.StartsWith('-')) files.Add(a);
			}
		byte[] data;
		try { data = files.Count == 0 || files[0] == "-" ? System.Text.Encoding.UTF8.GetBytes(Console.In.ReadToEnd()) : File.ReadAllBytes(ShellEnvironment.TranslatePath(files[0])); }
		catch (Exception ex) { Console.Error.WriteLine($"od: {ex.Message}"); return 1; }
		for (int off = 0; off < data.Length; off += 16)
			{
			var sb = new System.Text.StringBuilder();
			if (!noAddr) sb.Append(Convert.ToString(off, 8).PadLeft(7, '0'));
			int end = Math.Min(off + 16, data.Length);
			if (cMode)       for (int i = off; i < end; i++) sb.Append(' ').Append(OdChar(data[i]).PadLeft(3));
			else if (hex)    for (int i = off; i < end; i++) sb.Append(' ').Append(data[i].ToString("x2"));
			else if (octB)   for (int i = off; i < end; i++) sb.Append(' ').Append(Convert.ToString(data[i], 8).PadLeft(3, '0'));
			else             for (int i = off; i < end; i += 2) { int w = data[i] | (i + 1 < end ? data[i + 1] << 8 : 0); sb.Append(' ').Append(Convert.ToString(w, 8).PadLeft(6, '0')); }
			Console.WriteLine(sb.ToString());
			}
		if (!noAddr && data.Length > 0) Console.WriteLine(Convert.ToString(data.Length, 8).PadLeft(7, '0'));
		return 0;
		}
	private static string OdChar(byte b) => b switch
		{
		0 => "\\0", 7 => "\\a", 8 => "\\b", 9 => "\\t", 10 => "\\n", 11 => "\\v", 12 => "\\f", 13 => "\\r",
		_ => b >= 32 && b < 127 ? ((char)b).ToString() : Convert.ToString(b, 8).PadLeft(3, '0')
		};

	// du: disk usage. -s summary, -b apparent bytes (else 1K blocks, ceil), -h human.
	private static int Du(List<string> args)
		{
		bool summary = false, bytes = false, human = false;
		var paths = new List<string>();
		foreach (var a in args)
			{ if (a.StartsWith('-')) foreach (char c in a[1..]) { if (c == 's') summary = true; else if (c == 'b') bytes = true; else if (c == 'h') human = true; } else paths.Add(a); }
		if (paths.Count == 0) paths.Add(".");
		int rc = 0;
		foreach (var pth in paths)
			{
			var p = ShellEnvironment.TranslatePath(pth);
			try
				{
				if (File.Exists(p)) DuPrint(new FileInfo(p).Length, pth, bytes, human);
				else if (Directory.Exists(p)) { if (summary) DuPrint(DuDirSize(p), pth, bytes, human); else DuWalk(p, pth, bytes, human); }
				else { Console.Error.WriteLine($"du: cannot access '{pth}': No such file or directory"); rc = 1; }
				}
			catch (Exception ex) { Console.Error.WriteLine($"du: {pth}: {ex.Message}"); rc = 1; }
			}
		return rc;
		}
	private static long DuDirSize(string p)
		{
		long t = 0;
		foreach (var f in Directory.GetFiles(p)) t += new FileInfo(f).Length;
		foreach (var d in Directory.GetDirectories(p)) t += DuDirSize(d);
		return t;
		}
	private static long DuWalk(string fs, string disp, bool bytes, bool human)
		{
		long total = 0;
		foreach (var f in Directory.GetFiles(fs)) total += new FileInfo(f).Length;
		foreach (var d in Directory.GetDirectories(fs)) total += DuWalk(d, disp + "/" + Path.GetFileName(d), bytes, human);
		DuPrint(total, disp, bytes, human);
		return total;
		}
	private static void DuPrint(long size, string path, bool bytes, bool human)
		{
		string v = human ? DuHuman(size) : bytes ? size.ToString() : ((size + 1023) / 1024).ToString();
		Console.WriteLine($"{v}\t{path}");
		}
	private static string DuHuman(long n)
		{
		string[] u = ["", "K", "M", "G", "T"]; double d = n; int i = 0;
		while (d >= 1024 && i < u.Length - 1) { d /= 1024; i++; }
		return i == 0 ? $"{n}" : (d < 10 ? $"{d:0.0}{u[i]}" : $"{Math.Ceiling(d)}{u[i]}");
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

	// date: print current time; +FORMAT uses strftime-style specifiers. Setting the
	// clock is not supported. (Output is time-dependent — only escapes are testable.)
	private static int Date(List<string> args)
		{
		var now = DateTime.Now;
		var fmt = args.FirstOrDefault(a => a.StartsWith('+'));
		if (fmt is null) { Console.WriteLine(now.ToString("ddd MMM d HH:mm:ss yyyy")); return 0; }
		var f = fmt[1..];
		var sb = new System.Text.StringBuilder();
		for (int i = 0; i < f.Length; i++)
			{
			if (f[i] == '%' && i + 1 < f.Length)
				sb.Append(f[++i] switch
					{
					'Y' => now.ToString("yyyy"), 'y' => now.ToString("yy"),
					'm' => now.ToString("MM"),   'd' => now.ToString("dd"),
					'H' => now.ToString("HH"),   'M' => now.ToString("mm"), 'S' => now.ToString("ss"),
					'p' => now.ToString("tt"),   'A' => now.ToString("dddd"), 'a' => now.ToString("ddd"),
					'B' => now.ToString("MMMM"), 'b' => now.ToString("MMM"),
					'j' => now.DayOfYear.ToString("D3"),
					'u' => (((int)now.DayOfWeek + 6) % 7 + 1).ToString(), 'w' => ((int)now.DayOfWeek).ToString(),
					'e' => now.Day.ToString().PadLeft(2), 'T' => now.ToString("HH:mm:ss"), 'F' => now.ToString("yyyy-MM-dd"),
					's' => ((DateTimeOffset)now).ToUnixTimeSeconds().ToString(),
					'n' => "\n", 't' => "\t", '%' => "%",
					var x => "%" + x
					});
			else sb.Append(f[i]);
			}
		Console.WriteLine(sb.ToString());
		return 0;
		}

	// kill: terminate by PID. Windows has no graceful per-signal delivery, so any signal
	// is a forceful Process.Kill; -0 only tests existence. Job specs (%n) unsupported.
	private static int Kill(List<string> args)
		{
		bool sigZero = false; var pids = new List<int>();
		foreach (var a in args)
			{
			if (a == "-0") sigZero = true;
			else if (a.StartsWith('%')) Console.Error.WriteLine($"kill: {a}: job-spec kill not supported");
			else if (a.StartsWith('-')) { /* signal name/number — all map to forceful kill */ }
			else if (int.TryParse(a, out var pid)) pids.Add(pid);
			}
		if (pids.Count == 0) { Console.Error.WriteLine("kill: usage: kill [-s sig] pid ..."); return 1; }
		int rc = 0;
		foreach (var pid in pids)
			{
			try
				{
				var proc = System.Diagnostics.Process.GetProcessById(pid);
				if (!sigZero) proc.Kill();
				}
			catch (ArgumentException) { Console.Error.WriteLine($"kill: ({pid}) - No such process"); rc = 1; }
			catch (Exception ex)      { Console.Error.WriteLine($"kill: ({pid}) - {ex.Message}"); rc = 1; }
			}
		return rc;
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
