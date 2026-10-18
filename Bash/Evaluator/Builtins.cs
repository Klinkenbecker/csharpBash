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

	/// <summary>A shell builtin's error prefix, bash's `script: line N: ` (Evaluator.ErrorPrefix).
	/// The in-process utilities (ls, sed, ...) stand in for programs and keep their own `ls: `.</summary>
	private string Err => _eval.ErrorPrefix;

	// ── dispatch ──────────────────────────────────────────────────────────────

	/// <summary>All builtin names, for tab completion. Keep in sync with the dispatch switch.</summary>
	public static readonly IReadOnlyList<string> Names =
		[
		"echo", "printf", "cd", "pwd", "export", "unset", "set", "shift", "read",
		"true", "false", ":", "exit", "return", "break", "continue", "source", ".",
		"local", "declare", "typeset", "eval", "type", "command", "test", "[",
		"sleep", "env", "history", "fc", "umask", "times", "trap", "jobs", "wait", "fg", "bg", "disown", "nohup", "setsid",
		"shopt", "alias", "unalias", "builtin", "readonly", "exec", "let",
		"pushd", "popd", "dirs", "mapfile", "readarray", "getopts", "yes", "compgen", "complete", "compopt",
		"basename", "dirname", "seq", "mkdir", "cat", "head", "tail", "wc", "rev", "tac",
		"tr", "cut", "uniq", "nl", "fold",
		"touch", "rmdir", "cmp", "tee", "comm", "paste", "rm", "mv", "cp",
		"uname", "hostname", "factor", "cal", "date", "kill", "expr", "du", "df", "od", "sort", "split", "find", "ls", "xargs", "diff", "hash",
		"grep", "egrep", "fgrep", "which", "sed",
		"base64", "md5sum", "sha1sum", "sha256sum", "sha512sum", "hexdump", "strings",
		"stat", "mktemp", "realpath", "readlink", "chmod", "ln", "truncate",
		"timeout", "id", "whoami", "nproc", "printenv", "tty", "arch", "awk",
		"pgrep", "pkill", "ps", "jq",
		];

	/// <summary>The in-process coreutils (as opposed to shell builtins): these follow the
	/// unsupported-option policy (DECISIONS 2026-09-04 #2) and can defer to a PATH external.</summary>
	public static readonly HashSet<string> CoreutilNames =
		[
		"basename", "dirname", "seq", "mkdir", "cat", "head", "tail", "wc", "rev", "tac",
		"tr", "cut", "uniq", "nl", "fold", "touch", "rmdir", "cmp", "tee", "comm", "paste", "rm", "mv", "cp",
		"uname", "hostname", "factor", "cal", "date", "kill", "expr", "du", "df", "od", "sort", "split", "find", "ls", "xargs", "diff",
		"grep", "egrep", "fgrep", "which", "sed", "yes", "sleep", "env",
		"base64", "md5sum", "sha1sum", "sha256sum", "sha512sum", "hexdump", "strings",
		"stat", "mktemp", "realpath", "readlink", "chmod", "ln", "truncate",
		"timeout", "id", "whoami", "nproc", "printenv", "tty", "arch", "awk",
		"pgrep", "pkill", "ps", "jq",
		];

	private static readonly HashSet<string> _nameSet = [.. Names];

	/// <summary>True if <paramref name="name"/> dispatches to an in-process builtin.
	/// Pure membership test (no execution) so the caller can choose a redirect strategy
	/// before running anything.</summary>
	public static bool Has(string name) => _nameSet.Contains(name);

	/// <summary>The arguments for the PATH program a builtin hands over to. `awk` gets `-v BINMODE=3` first:
	/// on Windows the PATH awk is Git's gawk, which strips every CR in text mode, and the reference (GNU bash
	/// on Linux) never does (msp430-a2, measured 2026-10-12: `gawk '{print}'` 0 CR, `-v BINMODE=3` 3). A
	/// `-v BINMODE` of the script's own comes later and wins.</summary>
	private static List<string> ExternalArgs(string name, List<string> args) =>
		name == "awk" ? ["-v", "BINMODE=3", .. args] : args;

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
			// the PATH program only: RunCommand dispatched the builtin of the same name, which came
			// straight back here, until the stack overflowed and the shell died (every build to rev
			// 209; DECISIONS 2026-10-11)
			exitCode = _eval.RunExternal(name, ExternalArgs(name, args));
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
				exitCode = _eval.RunExternal(name, ExternalArgs(name, args));
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

	/// <summary>The name the running builtin was called by: `readarray` and `mapfile`, `typeset` and
	/// `declare` share code, and bash's messages name the one called.</summary>
	private string _called = "";

	/// <summary>bash's usage lines (`help -s`, bash 5.1), printed after an invalid option as bash's
	/// builtin_usage does: `read: usage: read [-ers] …`, with no script prefix.</summary>
	internal static readonly Dictionary<string, string> Usages = new(StringComparer.Ordinal)
		{
		["read"] = "read [-ers] [-a array] [-d delim] [-i text] [-n nchars] [-N nchars] [-p prompt] [-t timeout] [-u fd] [name ...]",
		["mapfile"] = "mapfile [-d delim] [-n count] [-O origin] [-s count] [-t] [-u fd] [-C callback] [-c quantum] [array]",
		["readarray"] = "readarray [-d delim] [-n count] [-O origin] [-s count] [-t] [-u fd] [-C callback] [-c quantum] [array]",
		["declare"] = "declare [-aAfFgiIlnrtux] [-p] [name[=value] ...]",
		["typeset"] = "typeset [-aAfFgiIlnrtux] [-p] name[=value] ...",
		["local"] = "local [option] name[=value] ...",
		["export"] = "export [-fn] [name[=value] ...] or export -p",
		["readonly"] = "readonly [-aAf] [name[=value] ...] or readonly -p",
		["unset"] = "unset [-f] [-v] [-n] [name ...]",
		["type"] = "type [-afptP] name [name ...]",
		["command"] = "command [-pVv] command [arg ...]",
		["hash"] = "hash [-lr] [-p pathname] [-dt] [name ...]",
		["set"] = "set [-abefhkmnptuvxBCHP] [-o option-name] [--] [arg ...]",
		["shopt"] = "shopt [-pqsu] [-o] [optname ...]",
		["disown"] = "disown [-h] [-ar] [jobspec ... | pid ...]",
		["printf"] = "printf [-v var] format [arguments]",
		["cd"] = "cd [-L|[-P [-e]] [-@]] [dir]",
		["pwd"] = "pwd [-LP]",
		["wait"] = "wait [-fn] [-p var] [id ...]",
		["jobs"] = "jobs [-lnprs] [jobspec ...] or jobs -x command [args]",
		["trap"] = "trap [-lp] [[arg] signal_spec ...]",
		["alias"] = "alias [-p] [name[=value] ... ]",
		["unalias"] = "unalias [-a] name [name ...]",
		["pushd"] = "pushd [-n] [+N | -N | dir]",
		["popd"] = "popd [-n] [+N | -N]",
		["dirs"] = "dirs [-clpv] [+N] [-N]",
		["history"] = "history [-c] [-d offset] [n] or history -anrw [filename] or history -ps arg [arg...]",
		["fc"] = "fc [-e ename] [-lnr] [first] [last] or fc -s [pat=rep] [command]",
		["umask"] = "umask [-p] [-S] [mode]",
		["times"] = "times",
		["source"] = "source filename [arguments]",
		["getopts"] = "getopts optstring name [arg ...]",
		["kill"] = "kill [-s sigspec | -n signum | -sigspec] pid | jobspec ... or kill -l [sigspec]",
		["."] = ". filename [arguments]",
		};

	/// <summary>bash's usage line for a builtin, on stderr.</summary>
	internal static void PrintUsage(string name)
		{
		if (Usages.TryGetValue(name, out var u)) Console.Error.WriteLine($"{name}: usage: {u}");
		}

	/// <summary>bash's report of an option the called builtin does not have: `name: -Z: invalid option`
	/// and the usage line; status 2. Until 2026-10-10 most builtins printed no usage line, and many
	/// took the option as an operand or ignored it (`printf -Z`, `cd -Z`, `export -Z`).</summary>
	private int BadOption(string opt, string message = "invalid option")
		{
		Console.Error.WriteLine($"{Err}{_called}: {opt}: {message}");
		PrintUsage(_called);
		return 2;
		}

	/// <summary>The leading options of a builtin that takes only flag letters (`pwd -LP`, `unset -fv`):
	/// each letter must be in <paramref name="valid"/>, `--` ends them, `-` alone is an operand.
	/// Returns the index of the first operand, or -1 after reporting an invalid one.</summary>
	private int FlagOptions(List<string> args, string valid, HashSet<char> seen)
		{
		int i = 0;
		for (; i < args.Count; i++)
			{
			var a = args[i];
			if (a == "--") return i + 1;
			if (a.Length < 2 || a[0] != '-') return i;
			foreach (char c in a.AsSpan(1))
				{
				if (valid.IndexOf(c) < 0) { BadOption($"-{c}"); return -1; }
				seen.Add(c);
				}
			}
		return i;
		}

	private int Dispatch(string name, List<string> args)
		{
		string caller = _called;   // `command`, `builtin`, `eval` run builtins inside builtins
		_called = name;
		try { return DispatchCore(name, args); }
		finally { _called = caller; }
		}

	private int DispatchCore(string name, List<string> args)
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
			"compgen" => Compgen(args),
			"complete" or "compopt" => CompletionNotAvailable(name),
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
			"source"  => Timing.On ? SourceTimed(args) : Source(args),
			"."       => Timing.On ? SourceTimed(args) : Source(args),
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
			"fc"      => Fc(args),
			"umask"   => Umask(args),
			"times"   => Times(args),
			"trap"    => Trap(args),
			"jobs"    => Jobs(args),
			"wait"    => Wait(args),
			"fg"      => Fg(args),
			"bg"      => Bg(args),
			"disown"  => _eval.Disown(args),
			"nohup"   => _eval.Nohup(args),    // not coreutils-deferrable: a PATH nohup.exe would land
			"setsid"  => _eval.Setsid(args),   // in the kill-on-close job, defeating the point
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
			"df"      => Df(args),   // Windows volumes in GNU's format (DECISIONS 2026-10-08)
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
			"strings" => Strings(args),
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
			"jq"      => Jq(args),   // in-process subset; the rest defers to a PATH jq (DECISIONS 2026-10-05)
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

	// uname / hostname / date / timeout / id / whoami / nproc / printenv / tty / arch / df: Builtins.Sys.cs

	private static int Factor(List<string> args)
		{
		var nums = new List<long>();
		var ops = args.Where(a => !a.StartsWith('-')).ToList();
		if (ops.Count > 0) foreach (var a in ops) { if (long.TryParse(a, out var v)) nums.Add(v); }
		else { string? line; while ((line = ShellEncoding.ReadLine(Console.In)) is not null)
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
		var pathExt = (Environment.GetEnvironmentVariable("PATHEXT") ?? ".COM;.EXE;.BAT;.CMD").Split(';', StringSplitOptions.RemoveEmptyEntries)
			.Where(FileTests.IsProgramExtension).ToArray();   // a `.js` is no program (2026-10-18)
		var dirs = ShellEnvironment.CurrentPath.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries);
		int rc = 0;
		foreach (var name in names)
			{
			bool found = false, hasExt = Path.HasExtension(name);
			foreach (var d in dirs)
				{
				// a relative PATH entry is looked in from this shell's directory (a job's), shown as written; `~/x` and
				// `/f/x` as translated
				var (at, rel) = Evaluator.PathEntry(d);
				var shownDir = rel ? d : at;
				var bare = Path.Combine(shownDir, name);
				if (File.Exists(Path.Combine(at, name))) { Console.WriteLine(Shown(bare)); found = true; if (!all) break; }
				if (!hasExt)
					foreach (var ext in pathExt)
						{
						var p = Path.Combine(shownDir, name + ext);
						if (File.Exists(Path.Combine(at, name + ext))) { Console.WriteLine(Shown(p)); found = true; if (!all) break; }
						}
				if (found && !all) break;
				}
			if (!found) rc = 1;
			}
		return rc;
		}

	// hash: remember/inspect external-command paths, in bash's formats. `hash` prints the `hits
	// command` table; `-r` clears it; `-p PATH NAME` sets one; `-d NAME` forgets one; `-t NAME`
	// prints the path (`NAME<tab>path` for several); `-l` prints it as `builtin hash -p` lines;
	// `hash NAME...` resolves & caches (rc 1 if any not found; a builtin name is skipped). Backed
	// by Evaluator's PATH cache. Until 2026-10-08: paths only, no hits, `-t`/`-l`/`-d` ignored.
	private int Hash(List<string> args)
		{
		bool table = false, list = false, delete = false, reset = false;
		string? setPath = null;
		var names = new List<string>();
		for (int i = 0; i < args.Count; i++)
			{
			var a = args[i];
			if (a == "--") { names.AddRange(args.Skip(i + 1)); break; }
			if (a.Length > 1 && a[0] == '-')
				{
				foreach (var c in a[1..])
					switch (c)
						{
						case 'r': reset = true; break;
						case 't': table = true; break;
						case 'l': list = true; break;
						case 'd': delete = true; break;
						case 'p':
							if (i + 1 >= args.Count) { Console.Error.WriteLine(Err + "hash: -p: option requires an argument"); return 2; }
							setPath = args[++i]; break;
						default: return BadOption($"-{c}");
						}
				continue;
				}
			names.Add(a);
			}
		if (reset) _eval.HashReset();   // remembered lookups too (CommandMap)
		var hashTable = _eval.HashTable;
		if (names.Count == 0)
			{
			if (reset || setPath is not null) return 0;
			if (hashTable.Count == 0) { Console.WriteLine("hash: hash table empty"); return 0; }
			if (list) { foreach (var kv in hashTable) Console.WriteLine($"builtin hash -p {Shown(kv.Value)} {kv.Key}"); return 0; }
			Console.WriteLine("hits\tcommand");
			foreach (var kv in hashTable) Console.WriteLine($"{_eval.HashHits(kv.Key),4}\t{Shown(kv.Value)}");
			return 0;
			}
		int rc = 0;
		foreach (var name in names)
			{
			if (setPath is not null) { _eval.HashSetPath(name, setPath); continue; }
			if (delete)
				{
				if (!_eval.HashDelete(name)) { Console.Error.WriteLine($"{Err}hash: {name}: not found"); rc = 1; }
				continue;
				}
			if (table)
				{
				if (!hashTable.TryGetValue(name, out var path)) { Console.Error.WriteLine($"{Err}hash: {name}: not found"); rc = 1; continue; }
				_eval.HashHit(name);   // bash counts a -t lookup as a hit
				Console.WriteLine(names.Count > 1 ? $"{name}\t{Shown(path)}" : Shown(path));
				continue;
				}
			if (Has(name) || _eval.HasFunction(name)) continue;   // builtins and functions are not hashed
			bool known = hashTable.ContainsKey(name);
			var hit = _eval.ResolveOnPath(name, probe: true);
			// a match that cannot run is no answer for `hash` (bash: "not found", and it is not kept), though running
			// it hashes it (2026-10-17)
			if (hit is not null && !FileTests.IsExecutable(ShellEnvironment.TranslatePath(hit))) { _eval.HashDelete(name); hit = null; }
			if (hit is null) { Console.Error.WriteLine($"{Err}hash: {name}: not found{_eval.MissNote(name)}"); rc = 1; }
			// a match in a relative PATH entry is not remembered (its answer moves with the cwd): nothing to reset
			else if (!known && hashTable.TryGetValue(name, out var hashed)) _eval.HashSetPath(name, hashed);
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

	/// <summary>`jobs [-lnprs] [jobspec …]` lists (<see cref="Evaluator.ListJobs"/>); `jobs -x command [args]`
	/// runs the command with each jobspec replaced (<see cref="Evaluator.RunWithJobGroups"/>). As bash's
	/// jobs.def, `-x` must come before `-l`, `-p` and `-n` (`jobs -lx` is an error, `jobs -xl` is not).
	/// `-x` was refused until 2026-10-12.</summary>
	private int Jobs(List<string> args)
		{
		int first = FlagOptions(args, "lnprsx", []);
		if (first < 0) return 2;
		bool execute = false, form = false;
		foreach (var a in args.Take(first))
			if (a != "--")
				foreach (char c in a.AsSpan(1))
					{
					if (c is 'l' or 'p' or 'n') form = true;
					else if (c == 'x')
						{
						if (form) { Console.Error.WriteLine($"{Err}jobs: no other options allowed with `-x'"); return 1; }
						execute = true;
						}
					}
		return execute ? _eval.RunWithJobGroups(args.Skip(first).ToList()) : _eval.ListJobs(args);
		}

	/// <summary>`wait`: bash's -f, -n and -p VAR are checked here (what they do is not implemented: -n
	/// does not yet wait for just one job); anything else is an invalid option.</summary>
	private int Wait(List<string> args)
		{
		for (int i = 0; i < args.Count && args[i].Length > 1 && args[i][0] == '-' && args[i] != "--"; i++)
			foreach (char c in args[i].AsSpan(1))
				{
				if (c == 'p') { if (i + 1 >= args.Count) return BadOption("-p", "option requires an argument"); i++; break; }
				if (c is not ('f' or 'n')) return BadOption($"-{c}");
				}
		return _eval.WaitJobs(args);
		}

	// `fg %n` — we cannot truly give a background thread terminal control, so this
	// waits for the job to finish (and echoes its command), which is the closest
	// faithful behaviour available without fork/job-control process groups.
	private int Fg(List<string> args)
		{
		if (!_eval.HasJobs) { Console.Error.WriteLine(Err + "fg: no current job"); return 1; }
		return _eval.WaitJobs(args, report: true);
		}

	// `bg` — nothing to resume: we never suspend jobs (no SIGTSTP on Windows).
	private int Bg(List<string> args)
		{
		Console.Error.WriteLine(Err + "bg: job suspension is not supported on this platform");
		return 1;
		}

	// ── trap ────────────────────────────────────────────────────────────────────

	/// <summary>`kill -l` and `trap -l`: bash's numbered table of <see cref="LinuxSignals"/>, five to a row,
	/// `%2d) SIGNAME` joined by tabs (the last row keeps its tab); 32 and 33 have no name and are left out.
	/// Until 2026-10-12 both printed a short line of nine names.</summary>
	private static void PrintSignalTable()
		{
		var sb = new System.Text.StringBuilder();
		int col = 0;
		for (int n = 1; n < LinuxSignals.Length; n++)
			{
			if (n is 32 or 33) continue;
			sb.Append($"{n,2}) SIG{LinuxSignals[n]}").Append(++col % 5 == 0 ? '\n' : '\t');
			}
		if (col % 5 != 0) sb.Append('\n');
		Console.Write(sb.ToString());
		}

	/// <summary>Linux's signal numbers (x86_64, as bash 5.1 there names them): 1-31 by name, 32 and
	/// 33 unnamed, 34-64 the real-time range RTMIN, RTMIN+1 … RTMIN+15, RTMAX-14 … RTMAX. Windows
	/// raises none but INT; a script may still set and list traps for any of them.</summary>
	private static readonly string[] LinuxSignals =
		["", "HUP", "INT", "QUIT", "ILL", "TRAP", "ABRT", "BUS", "FPE", "KILL", "USR1", "SEGV", "USR2", "PIPE",
		 "ALRM", "TERM", "STKFLT", "CHLD", "CONT", "STOP", "TSTP", "TTIN", "TTOU", "URG", "XCPU", "XFSZ",
		 "VTALRM", "PROF", "WINCH", "IO", "PWR", "SYS", "32", "33", "RTMIN",
		 .. Enumerable.Range(1, 15).Select(k => $"RTMIN+{k}"), .. Enumerable.Range(1, 14).Select(k => $"RTMAX-{15 - k}"), "RTMAX"];

	/// <summary>A trap's place in `trap -p` (bash: EXIT, the signals by number, DEBUG, ERR, RETURN).</summary>
	private static int TrapOrder(string canon) => canon switch
		{
		"EXIT" => 0, "DEBUG" => 65, "ERR" => 66, "RETURN" => 67,
		_ => Array.IndexOf(LinuxSignals, canon) is var i and > 0 ? i : 68,
		};

	/// <summary>How `trap -p` names it: SIGINT, SIGRTMIN+3; EXIT, DEBUG, ERR, RETURN and 32, 33 bare.</summary>
	private static string TrapDisplay(string canon) =>
		canon is "EXIT" or "DEBUG" or "ERR" or "RETURN" or "32" or "33" ? canon : "SIG" + canon;

	private int Trap(List<string> args)
		{
		// leading options as bash's internal_getopt reads them: -l, -p, `--` ends them, any other
		// letter is an invalid option. Until 2026-10-10 `trap -- 'cmd' SIG` (the form `trap -p`
		// prints) took `--` as the command and the command as a signal, and `trap -Z` as an action.
		bool listSigs = false, print = false;
		int first = 0;
		for (; first < args.Count; first++)
			{
			var a = args[first];
			if (a == "--") { first++; break; }
			if (a.Length < 2 || a[0] != '-') break;
			foreach (char c in a.AsSpan(1))
				if (c == 'l') listSigs = true; else if (c == 'p') print = true; else return BadOption($"-{c}");
			}
		var ops = args.Skip(first).ToList();

		if (listSigs) { PrintSignalTable(); return 0; }   // operands and -p are ignored (bash)

		// trap / trap -p [SIG...] : print the traps (the named ones only, when given), in bash's order
		// and spelling; until 2026-10-10 `-p` ignored its operands and printed INT, not SIGINT
		if (print || ops.Count == 0)
			{
			string Line(string sig, string cmd) => $"trap -- '{cmd.Replace("'", @"'\''")}' {TrapDisplay(sig)}";
			if (ops.Count == 0)
				{
				foreach (var (sig, cmd) in _eval.Traps.OrderBy(t => TrapOrder(t.Key))) Console.WriteLine(Line(sig, cmd));
				return 0;
				}
			// with operands: each in turn, as given (a repeat prints again; an unset one prints nothing)
			int prc = 0;
			var set = _eval.Traps.ToDictionary(t => t.Key, t => t.Value);
			foreach (var s in ops)
				{
				if (CanonSignal(s) is not { } c) { Console.Error.WriteLine($"{Err}trap: {s}: invalid signal specification"); prc = 1; }
				else if (set.TryGetValue(c, out var cmd)) Console.WriteLine(Line(c, cmd));
				}
			return prc;
			}

		// bash's trap.def: a first operand that is a signal NUMBER, or a lone operand that is a signal,
		// means every operand is a signal to reset (`trap 2 15`, `trap INT`); otherwise it is the
		// action (`-` resets, '' ignores) and the signals follow, at least one
		var action = ops[0];
		bool reset = (action.All(char.IsAsciiDigit) && CanonSignal(action) is not null)
		          || (ops.Count == 1 && action != "-" && CanonSignal(action) is not null);
		List<string> sigs;
		if (reset) sigs = ops;
		else
			{
			sigs = ops.Skip(1).ToList();
			if (sigs.Count == 0) { PrintUsage("trap"); return 2; }
			reset = action == "-";
			}
		// a bad spec is reported and the others are still set or reset (it used to stop at the first)
		int rc = 0;
		foreach (var s in sigs)
			{
			if (CanonSignal(s) is not { } c) { Console.Error.WriteLine($"{Err}trap: {s}: invalid signal specification"); rc = 1; }
			else if (reset) _eval.RemoveTrap(c);
			else _eval.SetTrap(c, action);
			}
		return rc;
		}

	/// <summary>Normalise a sigspec to its canonical name (INT, RTMIN+3, EXIT …), or null when bash would
	/// say "invalid signal specification": a name of <see cref="LinuxSignals"/> in any case with or
	/// without SIG, RTMIN+n / RTMAX-n, a number 0-64, EXIT, DEBUG, ERR, RETURN. Until 2026-10-10 any
	/// alphanumeric word was accepted (`trap x BADSIG`, `trap x 65`). `SIG` goes only before a signal's
	/// name, as bash's decode_signal: `SIGEXIT`, `SIGERR`, `SIG2` are invalid (accepted until 2026-10-12).</summary>
	private static string? CanonSignal(string s)
		{
		var u = s.ToUpperInvariant();
		if (u.StartsWith("SIG"))
			return u[3..] is var name && !(name is "EXIT" or "DEBUG" or "ERR" or "RETURN" || name.StartsWith("SIG") || name.All(char.IsAsciiDigit)) ? CanonSignal(name) : null;
		if (u is "EXIT" or "0") return "EXIT";
		if (u is "DEBUG" or "ERR" or "RETURN") return u;
		if (u.Length > 0 && u.All(char.IsAsciiDigit))
			return int.TryParse(u, out int n) && n is >= 1 and <= 64 ? LinuxSignals[n] : null;
		// RTMIN+n (n 0-30) and RTMAX-n (n 0-29, as bash's decode_signal) name a number
		if (u.StartsWith("RTMIN+") && int.TryParse(u[6..], out int a) && a is >= 0 and <= 30) return LinuxSignals[34 + a];
		if (u.StartsWith("RTMAX-") && int.TryParse(u[6..], out int b) && b is >= 0 and < 30) return LinuxSignals[64 - b];
		int i = Array.IndexOf(LinuxSignals, u);
		return i >= 1 && u is not ("32" or "33") ? u : null;
		}

	// ── history ─────────────────────────────────────────────────────────────────

	private int HistoryCmd(List<string> args)
		{
		// bash's option letters are checked first, in a script too (`history -Z` was status 0)
		if (args.Count > 0 && args[0].Length > 1 && args[0][0] == '-' && args[0] != "--")
			foreach (char c in args[0].AsSpan(1))
				if ("cdanrwps".IndexOf(c) < 0) return BadOption($"-{c}");
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

	/// <summary>`fc`: bash's history editor. C#Bash keeps no editable history, so after bash's option
	/// check every form is reported as not supported. Until 2026-10-10 `fc` was not a builtin and ran
	/// Windows' fc.exe ("FC: Insufficient number of file specifications").</summary>
	private int Fc(List<string> args)
		{
		for (int i = 0; i < args.Count; i++)
			{
			var a = args[i];
			if (a == "--" || a.Length < 2 || a[0] != '-' || char.IsAsciiDigit(a[1])) break;   // `fc -2` is an offset
			for (int k = 1; k < a.Length; k++)
				{
				char c = a[k];
				if (c == 'e')
					{
					if (k == a.Length - 1 && ++i >= args.Count) return BadOption("-e", "option requires an argument");
					break;   // the rest of the word, or the next one, is the editor
					}
				if ("lnrs".IndexOf(c) < 0) return BadOption($"-{c}");
				}
			}
		Console.Error.WriteLine($"{Err}fc: history editing is not supported by this interpreter");
		return 2;
		}

	// ── times ───────────────────────────────────────────────────────────────────

	/// <summary>`times`: the shell's user and system CPU, then its children's, as bash prints them
	/// (`0m0.019s 0m0.005s`, fractions truncated). Operands are ignored, as in bash.</summary>
	private int Times(List<string> args)
		{
		if (FlagOptions(args, "", []) < 0) return 2;
		var self = System.Diagnostics.Process.GetCurrentProcess();
		var (cu, cs) = Evaluator.ChildCpuTicks();
		Console.WriteLine($"{Clock(self.UserProcessorTime.Ticks)} {Clock(self.PrivilegedProcessorTime.Ticks)}");
		Console.WriteLine($"{Clock(cu)} {Clock(cs)}");
		return 0;

		static string Clock(long ticks)
			{
			long ms = ticks / TimeSpan.TicksPerMillisecond;
			return $"{ms / 60000}m{ms / 1000 % 60}.{ms % 1000:D3}s";
			}
		}

	// ── umask ───────────────────────────────────────────────────────────────────

	/// <summary>`umask [-p] [-S] [mode]`, bash 5.1's umask.def: octal (at most 07777, kept as the 9
	/// permission bits) or symbolic clauses (`u=rwx,g=rx,o=`, `g-w`, `a+r`, `=r`), applied all or
	/// none. Stored per shell (a subshell's change stays there) and printed; it has NO effect on
	/// files C#Bash creates, since Windows files have no mode bits to clear (DECISIONS 2026-10-10).</summary>
	private int Umask(List<string> args)
		{
		var flags = new HashSet<char>();
		int first = FlagOptions(args, "pS", flags);
		if (first < 0) return 2;
		bool symbolic = flags.Contains('S');
		if (first < args.Count)
			{
			var mode = args[first];   // further operands are ignored, as in bash
			int mask;
			if (mode.Length > 0 && char.IsAsciiDigit(mode[0]))
				{
				mask = 0;
				foreach (char c in mode)
					{
					if (c is < '0' or > '7' || (mask = mask * 8 + (c - '0')) > 0xFFF)
						{ Console.Error.WriteLine($"{Err}umask: {mode}: octal number out of range"); return 1; }
					}
				}
			else
				{
				int? bits = SymbolicMode(mode, ~_env.Umask & 0x1FF);
				if (bits is null) return 1;
				mask = ~bits.Value;
				}
			_env.Umask = mask & 0x1FF;
			if (symbolic) Console.WriteLine(SymbolicUmask(_env.Umask));
			return 0;
			}
		if (flags.Contains('p')) Console.Write(symbolic ? "umask -S " : "umask ");
		Console.WriteLine(symbolic ? SymbolicUmask(_env.Umask) : "0" + Convert.ToString(_env.Umask, 8).PadLeft(3, '0'));
		return 0;
		}

	/// <summary>bash's parse_symbolic_mode, on the permitted bits (the complement of the mask): clauses
	/// `[ugoa]*[+-=][rwx]*` separated by commas. Null after reporting a bad operator or character.</summary>
	private int? SymbolicMode(string mode, int bits)
		{
		const int U = 0x1C0, G = 0x038, O = 0x007, All = U | G | O;
		int s = 0;
		while (true)
			{
			int who = 0, perm = 0;
			for (; s < mode.Length && "agou".IndexOf(mode[s]) >= 0; s++)
				who |= mode[s] switch { 'u' => U, 'g' => G, 'o' => O, _ => All };
			char op = s < mode.Length ? mode[s++] : '\0';
			if (op is not ('+' or '-' or '='))
				{
				Console.Error.WriteLine($"{Err}umask: `{(op == '\0' ? "" : op.ToString())}': invalid symbolic mode operator");
				return null;
				}
			for (; s < mode.Length && "rwx".IndexOf(mode[s]) >= 0; s++)
				perm |= mode[s] switch { 'r' => 0x124, 'w' => 0x092, _ => 0x049 };
			if (s < mode.Length && mode[s] != ',')
				{
				Console.Error.WriteLine($"{Err}umask: `{mode[s]}': invalid symbolic mode character");
				return null;
				}
			if (who != 0) perm &= who;
			switch (op)
				{
				case '+': bits |= perm; break;
				case '-': bits &= ~perm; break;
				default: bits &= ~(who == 0 ? All : who); bits |= perm; break;
				}
			if (s >= mode.Length) return bits;
			s++;   // past the ','
			}
		}

	/// <summary>`u=rwx,g=rx,o=rx`: the permissions a mask leaves.</summary>
	private static string SymbolicUmask(int mask)
		{
		static string Class(int m, int shift) =>
			((m >> shift & 4) == 0 ? "r" : "") + ((m >> shift & 2) == 0 ? "w" : "") + ((m >> shift & 1) == 0 ? "x" : "");
		return $"u={Class(mask, 6)},g={Class(mask, 3)},o={Class(mask, 0)}";
		}
	}
