using System.Text;

namespace Bash.Evaluator;

/// <summary>
/// The shell builtins proper (as opposed to the in-process coreutils in Builtins.cs):
/// echo/printf, cd/pwd, variables and attributes (export/unset/declare/local/readonly),
/// set/shopt, alias, source/eval, type/command/builtin, test/[, read/mapfile/getopts,
/// pushd/popd/dirs, let, kill, env, sleep. Each returns an exit code.
/// </summary>
public sealed partial class Builtins
	{
	// ── echo ──────────────────────────────────────────────────────────────────

	/// <summary>`echo [-neE] args`. Backslashes are literal unless -e (or `shopt -s xpg_echo`).</summary>
	private int Echo(List<string> args)
		{
		bool noNewline = false;
		bool interpret = _eval.Options.XpgEcho;
		int start = 0;
		for (; start < args.Count; start++)
			{
			var a = args[start];
			if (a.Length < 2 || a[0] != '-' || !a[1..].All(c => c is 'n' or 'e' or 'E')) break;
			foreach (var c in a[1..])
				{
				if (c == 'n') noNewline = true;
				else if (c == 'e') interpret = true;
				else interpret = false;
				}
			}
		var text = string.Join(" ", args.Skip(start));
		bool halt = false;
		if (interpret) text = InterpretEscapes(text, out halt);
		if (noNewline || halt) Console.Write(text);
		else                   Console.WriteLine(text);
		return 0;
		}

	/// <summary>echo -e style escape processing (\n \t \\ \0nnn \xHH \c …).</summary>
	private static string InterpretEscapes(string s) => InterpretEscapes(s, out _);

	private static string InterpretEscapes(string s, out bool halt)
		{
		var sb = new StringBuilder();
		halt = false;
		int i = 0;
		while (i < s.Length)
			{
			if (s[i] == '\\' && i + 1 < s.Length)
				{
				i = PrintfFormatter.AppendEscape(s, i + 1, sb, out halt, inArg: true);
				if (halt) break;
				}
			else sb.Append(s[i++]);
			}
		return sb.ToString();
		}

	// ── printf ────────────────────────────────────────────────────────────────

	private int Printf(List<string> args)
		{
		string? target = null;
		int i = 0;
		if (args.Count > 0 && args[0] == "-v" && args.Count > 1) { target = args[1]; i = 2; }
		else if (args.Count > 0 && args[0].StartsWith("-v") && args[0].Length > 2) { target = args[0][2..]; i = 1; }
		else if (args.Count == 1 && args[0] == "-v") return BadOption("-v", "option requires an argument");
		if (i < args.Count && args[i] == "--") i++;
		// a format that starts with `-` is an option, as in bash (`printf '-%s'` is `-%`: invalid option)
		else if (i < args.Count && args[i].Length > 1 && args[i][0] == '-') return BadOption($"-{args[i][1]}");
		if (i >= args.Count) { PrintUsage("printf"); return 2; }
		// the formatter's messages (`printf: zz: invalid number`) get the builtin's prefix; seq -f
		// shares the formatter and, standing in for a program, has none
		var errors = new StringWriter();
		int rc = PrintfFormatter.Format(args[i], args.Skip(i + 1).ToList(), errors, out var text);
		foreach (var line in errors.ToString().Split('\n', StringSplitOptions.RemoveEmptyEntries))
			Console.Error.WriteLine(Err + line.TrimEnd('\r'));
		if (target is not null) AssignVar(target, text);
		else Console.Write(text);
		return rc;
		}

	/// <summary>Assign `name` or `name[subscript]` (used by printf -v, read, getopts).</summary>
	private void AssignVar(string spec, string value)
		{
		value = ShellEncoding.Canonical(value);   // `printf -v x '\xc3\xa9'` is é, as an assignment is
		int lb = spec.IndexOf('[');
		if (lb > 0 && spec.EndsWith(']'))
			{
			var name = spec[..lb]; var sub = spec[(lb + 1)..^1];
			if (_env.IsAssoc(name)) _env.SetAssocElement(name, sub, value);
			else _env.SetArrayElement(name, (int)_eval.Expander.EvalArithmetic(sub), value);
			return;
			}
		_env.Set(spec, value);
		}

	/// <summary>`declare name[sub]+=value`: the element append, as the statement `name[sub]+=value`.</summary>
	private void AppendElement(string name, string sub, string value)
		{
		value = ShellEncoding.Canonical(value);
		if (_env.IsAssoc(name)) _env.AppendAssocElement(name, sub, value);
		else _env.AppendArrayElement(name, (int)_eval.Expander.EvalArithmetic(sub), value);
		}

	private static bool IsIdentifier(string s) =>
		s.Length > 0 && (char.IsLetter(s[0]) || s[0] == '_') && s.All(c => char.IsLetterOrDigit(c) || c == '_');

	// ── cd / pwd ──────────────────────────────────────────────────────────────

	/// <summary>`cd`; its messages carry the name it ran under: pushd and popd change directory through it, and
	/// bash says `pushd: x: No such file or directory` (it said `cd:` until 2026-10-12).</summary>
	private int Cd(List<string> args)
		{
		// options only before the first operand, and only bash's (-L -P -e -@; -P's physical walk is
		// moot without symlinked cwds); a second operand is an error. Until 2026-10-10 any `-X`
		// anywhere was skipped and extra operands ignored.
		int first = FlagOptions(args, "LPe@", []);
		if (first < 0) return 2;
		if (args.Count - first > 1) { Console.Error.WriteLine($"{Err}{_called}: too many arguments"); return 1; }
		string? target = first < args.Count ? args[first] : null;
		bool printDir = false;
		string dest;
		if (target is null || target == "~")
			{
			var home = _env.Get("HOME");
			dest = home.Length > 0 ? ShellEnvironment.TranslatePath(home) : Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
			}
		else if (target == "-")
			{
			dest = _env.Get("OLDPWD");
			if (dest.Length == 0) { Console.Error.WriteLine($"{Err}{_called}: OLDPWD not set"); return 1; }
			dest = ShellEnvironment.TranslatePath(dest);   // a relative OLDPWD is this shell's (a job's), not the process cwd's
			printDir = true;
			}
		else if (target.Length == 0) return 0;
		else dest = ShellEnvironment.TranslatePath(target);

		if (!Directory.Exists(dest))
			{
			Console.Error.WriteLine(File.Exists(dest)
				? $"{Err}{_called}: {target}: Not a directory"
				: $"{Err}{_called}: {target}: No such file or directory");
			return 1;
			}
		var old = _env.Cwd;
		try { _env.ChangeCwd(dest); }   // this shell's own cwd; the process cwd only if this shell owns it
		catch (Exception ex) { Console.Error.WriteLine($"{Err}{_called}: {target}: {ex.Message}"); return 1; }
		_env.Set("OLDPWD", old); _env.Export("OLDPWD");
		_env.Set("PWD", _env.Cwd); _env.Export("PWD");
		if (printDir) Console.WriteLine(_env.Cwd);
		return 0;
		}

	/// <summary>Prints the forward-slash Windows form: a valid Windows path containing no shell
	/// escape characters (ratified 2026-09-11, refining DECISIONS 2026-09-04 decision 3).</summary>
	private int Pwd(List<string> args)
		{
		if (FlagOptions(args, "LP", []) < 0) return 2;   // operands are ignored, as in bash
		Console.WriteLine(ShellEnvironment.ShellCwd());
		return 0;
		}

	// ── export / unset / readonly ─────────────────────────────────────────────

	private static string EscapeDq(string v)
		{
		var sb = new StringBuilder();
		foreach (var c in v)
			{
			if (c is '"' or '\\' or '$' or '`') sb.Append('\\');
			sb.Append(c);
			}
		return sb.ToString();
		}

	private int Export(List<string> args)
		{
		// options only before the first name, as in bash (`export X=1 -n`: `-n` is a bad identifier)
		var flags = new HashSet<char>();
		int first = FlagOptions(args, "fnp", flags);
		if (first < 0) return 2;
		bool print = flags.Contains('p'), unexport = flags.Contains('n'), funcs = flags.Contains('f');
		var names = args.Skip(first).ToList();
		if (names.Count == 0 || print)
			{
			foreach (var n in _env.ExportedNames.OrderBy(x => x, StringComparer.Ordinal))
				Console.WriteLine(_env.IsSet(n) ? $"declare -x {n}=\"{EscapeDq(_env.Get(n))}\"" : $"declare -x {n}");
			return 0;
			}
		int rc = 0;
		foreach (var spec in names)
			{
			if (funcs) continue;                        // exported functions: accepted, no effect
			int eq = spec.IndexOf('=');
			var name = eq < 0 ? spec : spec[..eq];
			bool append = name.EndsWith('+');
			if (append) name = name[..^1];
			if (!IsIdentifier(name)) { Console.Error.WriteLine($"{Err}export: `{spec}': not a valid identifier"); rc = 1; continue; }
			if (eq >= 0)
				{
				var val = spec[(eq + 1)..];
				// `export v=(a b)` assigns an array, as `declare` does (it stored the text `(a b)` until 2026-10-12)
				if (ArrayLiteralValue(val, false, out var lit)) AssignArrayLiteral(name, lit, _env.IsAssoc(name), append);
				else if (append) _env.Append(name, val); else _env.Set(name, val);
				}
			if (unexport) _env.Unexport(name); else _env.Export(name);
			}
		return rc;
		}

	private int Unset(List<string> args)
		{
		var flags = new HashSet<char>();
		int first = FlagOptions(args, "fvn", flags);
		if (first < 0) return 2;
		bool func = flags.Contains('f'), onlyVar = flags.Contains('v');
		int rc = 0;
		foreach (var a in args.Skip(first))
			{
			if (func || (!onlyVar && !_env.IsDeclared(a) && _eval.HasFunction(a)))
				{
				_eval.RemoveFunction(a);
				continue;
				}
			int lb = a.IndexOf('[');
			int rb = a.LastIndexOf(']');
			if (lb > 0 && rb > lb)
				{
				var name = a[..lb];
				var key  = a[(lb + 1)..rb];
				if (_env.IsReadonly(name)) { Console.Error.WriteLine($"{Err}unset: {name}: cannot unset: readonly variable"); rc = 1; continue; }
				if (_env.IsAssoc(name)) _env.UnsetAssocElement(name, key);
				else if (key is "@" or "*") _env.Unset(name);
				else _env.UnsetArrayElement(name, (int)_eval.Expander.EvalArithmetic(key));
				continue;
				}
			if (!_env.Unset(a)) { Console.Error.WriteLine($"{Err}unset: {a}: cannot unset: readonly variable"); rc = 1; }
			}
		return rc;
		}

	private int Readonly(List<string> args)
		{
		var flags = new HashSet<char>();
		int first = FlagOptions(args, "aAfp", flags);
		if (first < 0) return 2;
		bool print = flags.Contains('p'), funcs = flags.Contains('f'), array = flags.Contains('a'), assoc = flags.Contains('A');
		var names = args.Skip(first).ToList();
		if (names.Count == 0 || print)
			{
			foreach (var n in _env.ReadonlyNames.OrderBy(x => x, StringComparer.Ordinal))
				Console.WriteLine(DeclareLine(n));
			return 0;
			}
		foreach (var spec in names)
			{
			if (funcs) continue;
			int eq = spec.IndexOf('=');
			var name = eq < 0 ? spec : spec[..eq];
			if (!IsIdentifier(name)) { Console.Error.WriteLine($"{Err}readonly: `{spec}': not a valid identifier"); return 1; }
			if (eq >= 0)
				{
				var val = spec[(eq + 1)..];
				// `readonly r=(x y)` is an array without -a too, as in bash (it set nothing until 2026-10-12)
				if (ArrayLiteralValue(val, array || assoc, out var lit)) AssignArrayLiteral(name, lit, assoc, false);
				else _env.Set(name, val);
				}
			_env.MarkReadonly(name);
			}
		return 0;
		}

	// ── set / shopt ───────────────────────────────────────────────────────────

	private static string QuoteForSet(string v)
		{
		if (v.Length == 0) return "''";
		if (v.All(c => char.IsLetterOrDigit(c) || "_-./:@%+=,".IndexOf(c) >= 0)) return v;
		if (v.Any(c => c < ' ')) return PrintfFormatter.ShellQuote(v);
		return "'" + v.Replace("'", "'\\''") + "'";
		}

	private int Set(List<string> args)
		{
		if (args.Count == 0)
			{
			foreach (var n in _env.VariableNames().OrderBy(x => x, StringComparer.Ordinal))
				{
				if (_env.IsArray(n))
					Console.WriteLine($"{n}=({string.Join(" ", _env.GetArrayPairs(n).Select(p => $"[{p.Key}]=\"{EscapeDq(p.Value)}\""))})");
				else if (_env.IsAssoc(n))
					Console.WriteLine($"{n}=({string.Join(" ", _env.GetAssocPairs(n).Select(p => $"[{p.Key}]=\"{EscapeDq(p.Value)}\""))} )");
				else
					Console.WriteLine($"{n}={QuoteForSet(_env.Get(n))}");
				}
			return 0;
			}

		int i = 0;
		while (i < args.Count)
			{
			var arg = args[i];
			if (arg == "--" || arg == "-")
				{
				_env.SetPositionals(args.Skip(i + 1));
				return 0;
				}
			if (arg.Length >= 2 && (arg[0] == '-' || arg[0] == '+'))
				{
				bool enable = arg[0] == '-';
				if (arg is "-o" or "+o")
					{
					if (i + 1 >= args.Count)
						{
						foreach (var (name, on) in _eval.Options.LongOptions())
							Console.WriteLine(enable ? $"{name,-15}\t{(on ? "on" : "off")}" : $"set {(on ? "-o" : "+o")} {name}");
						return 0;
						}
					var optName = args[++i];
					if (!_eval.Options.ApplyLong(optName, enable))
						{ Console.Error.WriteLine($"{Err}set: {optName}: invalid option name"); return 2; }
					if (enable && optName == "posix") NotAvailable.PosixMode($"{Err}set: ", "set -o posix");
					i++;
					_env.AutoExport = _eval.Options.AllExport;
					continue;
					}
				bool consumedNext = false;
				for (int k = 1; k < arg.Length; k++)
					{
					char f = arg[k];
					if (f == 'o')
						{
						// -euo pipefail / -eo pipefail: the option name is the rest of this
						// cluster or the next argument
						string? optName = k + 1 < arg.Length ? arg[(k + 1)..] : (i + 1 < args.Count ? args[i + 1] : null);
						if (k + 1 >= arg.Length && optName is not null) consumedNext = true;
						if (optName is null)
							{
							foreach (var (name, on) in _eval.Options.LongOptions())
								Console.WriteLine(enable ? $"{name,-15}\t{(on ? "on" : "off")}" : $"set {(on ? "-o" : "+o")} {name}");
							break;
							}
						if (!_eval.Options.ApplyLong(optName, enable))
							{ Console.Error.WriteLine($"{Err}set: {optName}: invalid option name"); return 2; }
						if (enable && optName == "posix") NotAvailable.PosixMode($"{Err}set: ", "set -o posix");
						break;
						}
					if (f == 'r' && enable)
						{
						// `set -r` is bash's restricted mode, which C#Bash does not have. Going on unrestricted
						// would remove what the script asked for, so the shell (a subshell: that one) ends
						// here, status 2: the run-time form of refusing to start (DECISIONS 2026-10-11).
						// It said "invalid option" and the script went on unrestricted.
						foreach (var line in NotAvailable.Lines($"{Err}set: ", "restricted mode (set -r)")) Console.Error.WriteLine(line);
						throw new ExitException(2);
						}
					if (!_eval.Options.Apply(f, enable))
						{ return BadOption($"-{f}"); }
					}
				_env.AutoExport = _eval.Options.AllExport;
				i += consumedNext ? 2 : 1;
				continue;
				}
			_env.SetPositionals(args.Skip(i));
			return 0;
			}
		return 0;
		}

	private int Shopt(List<string> args)
		{
		bool set = false, unset = false, print = false, quiet = false, setO = false;
		var names = new List<string>();
		foreach (var a in args)
			{
			if (a.Length > 1 && a[0] == '-' && a != "--")
				{
				foreach (var c in a[1..])
					{
					switch (c)
						{
						case 's': set = true; break;
						case 'u': unset = true; break;
						case 'p': print = true; break;
						case 'q': quiet = true; break;
						case 'o': setO = true; break;
						default: return BadOption($"-{c}");
						}
					}
				}
			else if (a != "--") names.Add(a);
			}

		if (setO)
			{
			// operate on the `set -o` option set
			if (names.Count == 0)
				{
				foreach (var (name, on) in _eval.Options.LongOptions())
					if ((set && on) || (unset && !on) || (!set && !unset))
						Console.WriteLine(print ? $"set {(on ? "-o" : "+o")} {name}" : $"{name,-15}\t{(on ? "on" : "off")}");
				return 0;
				}
			int rc0 = 0;
			foreach (var n in names)
				{
				if (set || unset)
					{
					if (!_eval.Options.ApplyLong(n, set)) { Console.Error.WriteLine($"{Err}shopt: {n}: invalid option name"); rc0 = 1; }
					else if (set && n == "posix") NotAvailable.PosixMode($"{Err}shopt: ", "shopt -so posix");
					continue;
					}
				bool on = _eval.Options.LongOptions().Any(o => o.Name == n && o.On);
				if (!quiet) Console.WriteLine(print ? $"set {(on ? "-o" : "+o")} {n}" : $"{n,-15}\t{(on ? "on" : "off")}");
				if (!on) rc0 = 1;
				}
			return rc0;
			}

		if (names.Count == 0)
			{
			foreach (var n in ShellOptions.KnownShopts)
				{
				bool on = _eval.Options.Shopt(n);
				if ((set && !on) || (unset && on)) continue;
				if (!quiet) Console.WriteLine(print ? $"shopt {(on ? "-s" : "-u")} {n}" : $"{n,-15}\t{(on ? "on" : "off")}");
				}
			return 0;
			}

		int rc = 0;
		foreach (var n in names)
			{
			if (set || unset)
				{
				var err = _eval.Options.SetShopt(n, set);
				if (err is not null) { Console.Error.WriteLine($"{Err}shopt: {err}"); rc = 1; }
				continue;
				}
			if (!_eval.Options.IsKnownShopt(n)) { Console.Error.WriteLine($"{Err}shopt: {n}: invalid shell option name"); rc = 1; continue; }
			bool on = _eval.Options.Shopt(n);
			if (!quiet) Console.WriteLine(print ? $"shopt {(on ? "-s" : "-u")} {n}" : $"{n,-15}\t{(on ? "on" : "off")}");
			if (!on) rc = 1;
			}
		return rc;
		}

	// ── alias / unalias ───────────────────────────────────────────────────────

	private int Alias(List<string> args)
		{
		var aliases = _eval.Aliases;
		var flags = new HashSet<char>();
		int first = FlagOptions(args, "p", flags);
		if (first < 0) return 2;
		var items = args.Skip(first).ToList();
		// bash: -p, or no names, prints them all; with -p the names are still processed
		if (items.Count == 0 || flags.Contains('p'))
			{
			foreach (var kv in aliases.OrderBy(k => k.Key, StringComparer.Ordinal))
				Console.WriteLine($"alias {kv.Key}='{kv.Value.Replace("'", "'\\''")}'");
			if (items.Count == 0) return 0;
			}
		int rc = 0;
		foreach (var a in items)
			{
			int eq = a.IndexOf('=');
			if (eq > 0) { _eval.SaveAliasesForSubshell(); aliases[a[..eq]] = a[(eq + 1)..]; continue; }
			if (aliases.TryGetValue(a, out var v)) Console.WriteLine($"alias {a}='{v.Replace("'", "'\\''")}'");
			else { Console.Error.WriteLine($"{Err}alias: {a}: not found"); rc = 1; }
			}
		return rc;
		}

	private int Unalias(List<string> args)
		{
		var aliases = _eval.Aliases;
		var flags = new HashSet<char>();
		int first = FlagOptions(args, "a", flags);
		if (first < 0) return 2;
		_eval.SaveAliasesForSubshell();
		if (flags.Contains('a')) { aliases.Clear(); return 0; }   // bash: -a removes all, names or not
		if (first >= args.Count) { PrintUsage("unalias"); return 2; }
		int rc = 0;
		foreach (var a in args.Skip(first))
			if (!aliases.Remove(a)) { Console.Error.WriteLine($"{Err}unalias: {a}: not found"); rc = 1; }
		return rc;
		}

	// ── shift / read / mapfile / getopts ──────────────────────────────────────

	private int Shift(List<string> args)
		{
		int n = 1;
		if (args.Count > 0 && !int.TryParse(args[0], out n))
			{ Console.Error.WriteLine($"{Err}shift: {args[0]}: numeric argument required"); return 1; }
		var pos = _env.GetPositionals();
		if (n < 0 || n > pos.Count)
			{
			if (_eval.Options.Shopt("shift_verbose")) Console.Error.WriteLine(Err + "shift: shift count out of range");
			return 1;
			}
		_env.SetPositionals(pos.Skip(n));
		return 0;
		}

	private const char EscMark = '';   // marks a backslash-escaped char during `read` so IFS won't split on it

	/// <summary>bash IFS field splitting for `read`: leading/trailing IFS whitespace is
	/// dropped, runs of IFS whitespace separate fields, a non-whitespace IFS character is a
	/// single separator, and the last of <paramref name="max"/> fields takes the remainder.</summary>
	private static List<string> SplitIfs(string s, string ifs, int max)
		{
		if (ifs.Length == 0) return [s];
		var ws = new HashSet<char>(ifs.Where(c => c is ' ' or '\t' or '\n'));
		var nw = new HashSet<char>(ifs.Where(c => !(c is ' ' or '\t' or '\n')));
		var fields = new List<string>();
		int i = 0, n = s.Length;
		while (i < n && ws.Contains(s[i])) i++;
		while (i < n)
			{
			if (fields.Count == max - 1)
				{
				var rest = s[i..];
				int e = rest.Length;
				while (e > 0 && ws.Contains(rest[e - 1])) e--;
				fields.Add(rest[..e]);
				return fields;
				}
			var sb = new StringBuilder();
			while (i < n && !ws.Contains(s[i]) && !nw.Contains(s[i]))
				{
				if (s[i] == EscMark && i + 1 < n) { sb.Append(EscMark).Append(s[i + 1]); i += 2; continue; }
				sb.Append(s[i++]);
				}
			fields.Add(sb.ToString());
			while (i < n && ws.Contains(s[i])) i++;
			if (i < n && nw.Contains(s[i]))
				{
				i++;
				while (i < n && ws.Contains(s[i])) i++;
				if (i >= n) fields.Add("");
				}
			}
		return fields;
		}

	private static string Unmark(string s) => s.Replace(EscMark.ToString(), "");

	// One reader per numbered descriptor, so successive `read -u 3` calls continue where
	// the previous one stopped instead of re-buffering from the stream.
	private readonly Dictionary<Stream, StreamReader> _fdReaders = new(ReferenceEqualityComparer.Instance);

	private int Read(List<string> args)
		{
		bool raw = false;
		string? arrayName = null, prompt = null;
		string delim = "\n";
		int nchars = -1, fd = 0;
		var vars = new List<string>();
		int i = 0;
		for (; i < args.Count; i++)
			{
			var a = args[i];
			if (a == "--") { i++; break; }
			if (a.Length < 2 || a[0] != '-') break;
			for (int k = 1; k < a.Length; k++)
				{
				char o = a[k];
				string? OptArg()
					{
					if (k + 1 < a.Length) { var v = a[(k + 1)..]; k = a.Length; return v; }
					return i + 1 < args.Count ? args[++i] : null;
					}
				switch (o)
					{
					case 'r': raw = true; break;
					case 's': case 'e': break;
					case 'i': OptArg(); break;
					case 'a': arrayName = OptArg(); break;
					case 'd': { var d = OptArg(); delim = d is null || d.Length == 0 ? "\0" : d[..1]; break; }
					case 'n': case 'N': { var v = OptArg(); nchars = int.TryParse(v, out var nn) ? nn : 0; break; }
					case 'p': prompt = OptArg(); break;
					case 't': OptArg(); break;     // timeouts: accepted, not enforced on redirected input
					case 'u': { var v = OptArg(); int.TryParse(v, out fd); break; }
					default: return BadOption($"-{o}");
					}
				}
			}
		for (; i < args.Count; i++) vars.Add(args[i]);

		if (prompt is not null && !Console.IsInputRedirected) Console.Error.Write(prompt);

		TextReader input = Console.In;
		if (fd > 2)
			{
			var s = _eval.GetFd(fd);
			if (s is null) { Console.Error.WriteLine($"{Err}read: {fd}: invalid file descriptor: Bad file descriptor"); return 1; }
			input = _fdReaders.TryGetValue(s, out var rd) ? rd : (_fdReaders[s] = new StreamReader(s, ShellEncoding.Utf8, false, 1024, leaveOpen: true));
			}

		var sb = new StringBuilder();
		bool eof = false;
		if (nchars >= 0)
			{
			for (int c = 0; c < nchars; c++)
				{
				int ch = input.Read();
				if (ch < 0) { eof = true; break; }
				sb.Append((char)ch);
				}
			}
		else
			{
			while (true)
				{
				int ch = input.Read();
				if (ch < 0) { eof = true; break; }
				char c = (char)ch;
				if (!raw && c == '\\')
					{
					int nx = input.Read();
					if (nx < 0) { eof = true; break; }
					if ((char)nx == '\n') continue;            // line continuation
					sb.Append(EscMark).Append((char)nx);       // escaped char: literal, not a separator
					continue;
					}
				if (c == delim[0]) break;
				sb.Append(c);
				}
			}
		var line = sb.ToString();   // a CR before the LF is data, as in bash (DECISIONS 2026-10-03)

		var ifs = _env.IsSet("IFS") ? _env.Get("IFS") : " \t\n";
		if (arrayName is not null)
			{
			_env.SetArrayFromList(arrayName, SplitIfs(line, ifs, int.MaxValue).Select(Unmark).ToList());
			}
		else if (vars.Count == 0)
			_env.Set("REPLY", Unmark(line));
		else
			{
			var parts = SplitIfs(line, ifs, vars.Count);
			for (int v = 0; v < vars.Count; v++)
				AssignVar(vars[v], v < parts.Count ? Unmark(parts[v]) : "");
			}
		return eof ? 1 : 0;
		}

	private int Mapfile(List<string> args)
		{
		string delim = "\n";
		int count = 0, origin = 0, skip = 0;
		bool trim = false;
		string name = "MAPFILE";
		for (int i = 0; i < args.Count; i++)
			{
			var a = args[i];
			string Next() => i + 1 < args.Count ? args[++i] : "";
			switch (a)
				{
				case "-t": trim = true; break;
				case "-d": { var d = Next(); delim = d.Length == 0 ? "\0" : d[..1]; break; }
				case "-n": int.TryParse(Next(), out count); break;
				case "-O": int.TryParse(Next(), out origin); break;
				case "-s": int.TryParse(Next(), out skip); break;
				case "-u": case "-C": case "-c": Next(); break;
				case "--": break;
				default:
					if (a.StartsWith('-') && a.Length > 1) return BadOption($"-{a[1]}");
					name = a; break;
				}
			}
		var lines = new List<string>();
		var sb = new StringBuilder();
		while (true)
			{
			int ch = Console.In.Read();
			if (ch < 0) { if (sb.Length > 0) lines.Add(sb.ToString()); break; }
			sb.Append((char)ch);
			if ((char)ch == delim[0])
				{
				lines.Add(trim ? sb.ToString()[..^1] : sb.ToString());
				sb.Clear();
				if (count > 0 && lines.Count >= skip + count) break;
				}
			}
		var selected = lines.Skip(skip).ToList();
		if (count > 0) selected = selected.Take(count).ToList();
		if (origin == 0) _env.SetArrayFromList(name, selected);
		else
			{
			_env.DeclareArray(name);
			for (int k = 0; k < selected.Count; k++) _env.SetArrayElement(name, origin + k, selected[k]);
			}
		return 0;
		}

	private int _getoptsCharIdx;
	private int _getoptsLastOptind = -1;

	private int Getopts(List<string> args)
		{
		if (args.Count > 0 && args[0].Length > 1 && args[0][0] == '-' && args[0] != "--") return BadOption($"-{args[0][1]}");
		if (args.Count < 2) { PrintUsage("getopts"); return 2; }
		var optstring = args[0];
		var varName = args[1];
		var argv = args.Count > 2 ? args.Skip(2).ToList() : _env.GetPositionals();
		bool silent = optstring.StartsWith(':');
		if (silent) optstring = optstring[1..];
		int optind = int.TryParse(_env.Get("OPTIND"), out var oi) ? oi : 1;
		if (optind < 1) optind = 1;
		if (optind != _getoptsLastOptind) _getoptsCharIdx = 0;

		void Store(int ind) { _env.Set("OPTIND", ind.ToString()); _getoptsLastOptind = ind; }

		if (optind > argv.Count) { _env.Set(varName, "?"); Store(optind); return 1; }
		var cur = argv[optind - 1];
		if (_getoptsCharIdx == 0)
			{
			if (cur == "--") { _env.Set(varName, "?"); Store(optind + 1); return 1; }
			if (cur.Length < 2 || cur[0] != '-') { _env.Set(varName, "?"); Store(optind); return 1; }
			_getoptsCharIdx = 1;
			}
		char opt = cur[_getoptsCharIdx++];
		bool endOfCluster = _getoptsCharIdx >= cur.Length;
		int pos = optstring.IndexOf(opt);
		if (opt == ':' || pos < 0)
			{
			_env.Set(varName, "?");
			if (silent) _env.Set("OPTARG", opt.ToString());
			else { _env.Unset("OPTARG"); Console.Error.WriteLine($"{_env.Get("0")}: illegal option -- {opt}"); }
			if (endOfCluster) { optind++; _getoptsCharIdx = 0; }
			Store(optind);
			return 0;
			}
		bool wantsArg = pos + 1 < optstring.Length && optstring[pos + 1] == ':';
		if (wantsArg)
			{
			string? val;
			if (!endOfCluster) { val = cur[_getoptsCharIdx..]; optind++; _getoptsCharIdx = 0; }
			else
				{
				optind++; _getoptsCharIdx = 0;
				if (optind <= argv.Count) { val = argv[optind - 1]; optind++; } else val = null;
				}
			if (val is null)
				{
				if (silent) { _env.Set(varName, ":"); _env.Set("OPTARG", opt.ToString()); }
				else { _env.Set(varName, "?"); _env.Unset("OPTARG"); Console.Error.WriteLine($"{_env.Get("0")}: option requires an argument -- {opt}"); }
				Store(optind);
				return 0;
				}
			_env.Set(varName, opt.ToString()); _env.Set("OPTARG", val);
			Store(optind);
			return 0;
			}
		_env.Set(varName, opt.ToString()); _env.Unset("OPTARG");
		if (endOfCluster) { optind++; _getoptsCharIdx = 0; }
		Store(optind);
		return 0;
		}

	// ── source / eval ─────────────────────────────────────────────────────────

	private int Source(List<string> args)
		{
		// `--` ends the options; source has none (bash: `source -Z` is an invalid option, `source --`
		// still needs a file; `--` used to be read as the file name)
		int first = args.Count > 0 && args[0] == "--" ? 1 : 0;
		if (first == 0 && args.Count > 0 && args[0].Length > 1 && args[0][0] == '-') return BadOption($"-{args[0][1]}");
		if (first >= args.Count) { Console.Error.WriteLine($"{Err}{_called}: filename argument required"); PrintUsage(_called); return 2; }
		args = args.Skip(first).ToList();
		var file = args[0];
		var path = ShellEnvironment.TranslatePath(file);
		if (!file.Contains('/') && !file.Contains('\\') && _eval.Options.Shopt("sourcepath"))
			{
			// bash searches PATH for a bare filename FIRST, and the current directory only when PATH has none (not
			// in POSIX mode); the cwd's came first until 2026-10-17, so a planted file there was sourced instead
			foreach (var d in ShellEnvironment.CurrentPath.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
				{
				var cand = Path.Combine(ShellEnvironment.TranslatePath(d), file);
				if (File.Exists(cand)) { path = cand; file = cand; break; }
				}
			}
		if (!File.Exists(path)) { Console.Error.WriteLine($"{Err}{file}: No such file or directory"); return 1; }
		// named as given (BASH_SOURCE, the error prefix), as bash does; SourceFile translates it
		return _eval.SourceFile(file, silentIfMissing: false, positionals: args.Count > 1 ? args.Skip(1) : null);
		}

	private int DoEval(List<string> args)
		{
		var src = string.Join(" ", args);
		if (src.Trim().Length == 0) return 0;
		// its lines continue the eval command's (bash: $LINENO and the error prefix; they
		// restarted at 1 until 2026-10-10)
		if (!Timing.On) return _eval.RunEval(src);
		Timing.Mark("eval+");
		try { return _eval.RunEval(src); }
		finally { Timing.Mark("eval-"); }
		}

	/// <summary>`source` under the timing switch: its file's run as one region.</summary>
	private int SourceTimed(List<string> args)
		{
		Timing.Mark("source+ " + (args.Count > 1 && args[0] == "--" ? args[1] : args.Count > 0 ? args[0] : ""));
		try { return Source(args); }
		finally { Timing.Mark("source-"); }
		}

	// ── declare / local / typeset ─────────────────────────────────────────────

	private int Local(List<string> args)
		{
		if (_env.ScopeDepth <= 1) { Console.Error.WriteLine(Err + "local: can only be used in a function"); return 1; }
		return Declare(args, local: true);
		}

	private int Declare(List<string> args) => Declare(args, local: false);

	private string DeclareLine(string name)
		{
		var flags = new StringBuilder();
		if (_env.IsArray(name))    flags.Append('a');
		if (_env.IsAssoc(name))    flags.Append('A');
		if (_env.IsInteger(name))  flags.Append('i');
		if (_env.IsReadonly(name)) flags.Append('r');
		if (_env.IsExported(name)) flags.Append('x');
		var f = flags.Length == 0 ? "--" : "-" + flags;
		if (_env.IsArray(name))
			return $"declare {f} {name}=({string.Join(" ", _env.GetArrayPairs(name).Select(p => $"[{p.Key}]=\"{EscapeDq(p.Value)}\""))})";
		if (_env.IsAssoc(name))
			return $"declare {f} {name}=({string.Join(" ", _env.GetAssocPairs(name).Select(p => $"[{p.Key}]=\"{EscapeDq(p.Value)}\""))} )";
		if (!_env.IsSet(name)) return $"declare {f} {name}";
		return $"declare {f} {name}=\"{EscapeDq(_env.Get(name))}\"";
		}

	/// <summary>A function as bash prints it (`declare -f`, `type`): bash's command printer
	/// (<see cref="Parser.CommandPrinter"/>). Until 2026-10-12 its source text as written.</summary>
	private static string FunctionText(Parser.FunctionDef fd) => Parser.CommandPrinter.Function(fd);

	/// <summary>Whether a declaration builtin's value is an array literal (bash 5.1, measured 2026-10-12): the
	/// one the parser folded from an unquoted `name=(…)` (marked, see Lexer.ArrayLiteralMark), or, under
	/// `-a` / `-A`, any value in parentheses, quoted or expanded too (`declare -a x='(a b)'`). Otherwise a
	/// value that looks like `(…)` is a string (`local s="(x y)"`); it became an array until then.</summary>
	private static bool ArrayLiteralValue(string val, bool arrayFlag, out string literal)
		{
		if (val.Length > 0 && val[0] == Lexer.Lexer.ArrayLiteralMark) { literal = val[1..]; return true; }
		literal = val;
		return arrayFlag && val.StartsWith('(') && val.EndsWith(')');
		}

	/// <summary>Assign `name=(…)` from its literal text: values are lexed and expanded as words.</summary>
	private void AssignArrayLiteral(string name, string literal, bool assoc, bool append) =>
		PrepareArrayLiteral(name, literal, assoc, append)();

	/// <summary>Expand `name=(…)`'s words NOW and return what assigns them: `local a=("${a[@]}" x)` reads the
	/// outer `a` before the local one exists (bash; measured 2026-10-12).</summary>
	private Action PrepareArrayLiteral(string name, string literal, bool assoc, bool append)
		{
		var inner = literal[1..^1];
		var words = Parser.Parser.ParseWords(inner);
		bool keyed = words.Count > 0 && words.All(w => w.Parts.Count > 0 && w.Parts[0] is Parser.LiteralPart lp && lp.Value.StartsWith('[') && lp.Value.Contains("]="));
		var texts = keyed || assoc || _env.IsAssoc(name) ? words.Select(w => _eval.Expander.ExpandToString(w)).ToList() : null;
		var fields = keyed ? null : words.SelectMany(_eval.Expander.ExpandToFields).ToList();
		return () =>
			{
			if ((assoc || _env.IsAssoc(name)) && texts is not null)
				{
				if (!append) _env.DeclareAssoc(name);
				foreach (var text in texts)
					{
					int close = text.IndexOf("]=", StringComparison.Ordinal);
					if (text.StartsWith('[') && close > 0) _env.SetAssocElement(name, text[1..close], text[(close + 2)..]);
					}
				return;
				}
			if (keyed && texts is not null)
				{
				if (!append) _env.SetArrayFromList(name, []);
				foreach (var text in texts)
					{
					int close = text.IndexOf("]=", StringComparison.Ordinal);
					_env.SetArrayElement(name, (int)_eval.Expander.EvalArithmetic(text[1..close]), text[(close + 2)..]);
					}
				return;
				}
			var values = fields ?? [];
			if (append) _env.AppendArrayFromList(name, values); else _env.SetArrayFromList(name, values);
			};
		}

	private int Declare(List<string> args, bool local)
		{
		bool export = false, unexport = false, array = false, assoc = false, integer = false, unInteger = false,
		     ro = false, global = false, funcs = false, funcNames = false, print = false, nameref = false,
		     traceOn = false, traceOff = false, funcOn = false;
		var specs = new List<string>();
		foreach (var arg in args)
			{
			if (arg == "--") continue;
			if (arg.Length > 1 && (arg[0] == '-' || arg[0] == '+') && !arg.Contains('='))
				{
				bool on = arg[0] == '-';
				foreach (var c in arg[1..])
					{
					switch (c)
						{
						case 'x': if (on) export = true; else unexport = true; break;
						case 'a': array = true; break;
						case 'A': assoc = true; break;
						case 'i': if (on) integer = true; else unInteger = true; break;
						case 'r': ro = true; break;
						case 'g': global = true; break;
						case 'f': funcs = true; if (on) funcOn = true; break;
						case 'F': funcNames = true; break;
						case 'p': print = true; break;
						case 'n': nameref = true; break;
						case 't': if (on) traceOn = true; else traceOff = true; break;   // a function's trace attribute; nothing for a variable
						case 'l': case 'u': case 'I': case 'c': break;   // accepted, no effect
						default: return BadOption($"-{c}");
						}
					}
				continue;
				}
			specs.Add(arg);
			}
		if (nameref) { Console.Error.WriteLine(Err + "declare: -n (nameref) is not supported by this interpreter"); return 2; }

		// `declare -ft NAME` / `declare -f +t NAME`: the function inherits (or no longer) the DEBUG and
		// RETURN traps, as under -T, and nothing is printed. It printed the function and ignored -t until
		// 2026-10-11. Function mode needs -f ON: in `declare +ft f` the t is a VARIABLE's, which means
		// nothing (bash: no output, no change; measured).
		if ((traceOn || traceOff) && specs.Count > 0 && !print)
			{
			if (!funcOn) return 0;
			int trc = 0;
			foreach (var n in specs)
				if (!_eval.SetFunctionTrace(n, traceOn)) trc = 1;
			return trc;
			}

		// ── function listing ──
		if (funcs || funcNames)
			{
			int frc = 0;
			var names = specs.Count > 0 ? specs : _eval.FunctionNames.OrderBy(n => n, StringComparer.Ordinal).ToList();
			foreach (var n in names)
				{
				var fd = _eval.GetFunction(n);
				if (fd is null) { frc = 1; continue; }
				if (funcNames) Console.WriteLine(specs.Count > 0 ? n : $"declare -f {n}");
				else Console.WriteLine(FunctionText(fd));
				}
			return frc;
			}

		// ── print ──
		if (print || specs.Count == 0)
			{
			int prc = 0;
			var names = specs.Count > 0 ? specs : _env.VariableNames().OrderBy(n => n, StringComparer.Ordinal).ToList();
			foreach (var n in names)
				{
				bool known = specs.Count > 0 ? _env.IsDeclared(n) : _env.IsSet(n) || _env.IsArray(n) || _env.IsAssoc(n);
				if (!known)
					{ if (specs.Count > 0) { Console.Error.WriteLine($"{Err}declare: {n}: not found"); prc = 1; } continue; }
				if (specs.Count == 0 && (array || assoc || integer || export || ro))
					{
					if (array && !_env.IsArray(n)) continue;
					if (assoc && !_env.IsAssoc(n)) continue;
					if (integer && !_env.IsInteger(n)) continue;
					if (export && !_env.IsExported(n)) continue;
					if (ro && !_env.IsReadonly(n)) continue;
					}
				Console.WriteLine(DeclareLine(n));
				}
			return prc;
			}

		// ── declare / assign ──
		// `local`, and `declare` / `typeset` inside a function, make the variable local to the call, with its
		// arrays and attributes; `-g` and the top level make it global (bash). Until 2026-10-12 only `local`'s
		// scalars were local: arrays and attributes went shell-wide (DECISIONS "VARIABLE SCOPE").
		bool scopeLocal = (local || _env.ScopeDepth > 1) && !global;
		int rc = 0;
		foreach (var spec in specs)
			{
			int eq = spec.IndexOf('=');
			var name = eq < 0 ? spec : spec[..eq];
			bool append = name.EndsWith('+');
			if (append) name = name[..^1];
			string? val = eq < 0 ? null : spec[(eq + 1)..];
			int lb = name.IndexOf('[');
			string? sub = null;
			if (lb > 0 && name.EndsWith(']')) { sub = name[(lb + 1)..^1]; name = name[..lb]; }
			if (!IsIdentifier(name)) { Console.Error.WriteLine($"{Err}declare: `{spec}': not a valid identifier"); rc = 1; continue; }

			// an array literal's words are expanded before the variable is declared (the right side sees the
			// outer value: `local a=("${a[@]}" x)`); then the local exists before its attributes and value
			Action? assignLiteral = val is not null && sub is null && ArrayLiteralValue(val, array || assoc, out var lit)
				? PrepareArrayLiteral(name, lit, assoc, append) : null;
			// `local x`: declared here, UNSET, hiding the outer x. A plain `local x=v` is declared by SetLocal
			// itself (one lookup on the path every function call takes)
			bool plainValue = val is not null && sub is null && assignLiteral is null && !append && !array && !assoc && !integer && !unInteger;
			if (scopeLocal && !plainValue) _env.DeclareLocal(name);

			if (assoc) _env.DeclareAssoc(name);
			else if (array) _env.DeclareArray(name);
			if (integer) _env.MarkInteger(name, true);
			if (unInteger) _env.MarkInteger(name, false);

			if (val is not null)
				{
				if (sub is not null && append) AppendElement(name, sub, val);   // `declare a[0]+=v` (was a plain assignment)
				else if (sub is not null) AssignVar($"{name}[{sub}]", val);
				else if (assignLiteral is not null) assignLiteral();
				else if (append) _env.Append(name, val);
				else if (scopeLocal) _env.SetLocal(name, val);
				else if (global) _env.SetGlobal(name, val);
				else _env.Set(name, val);
				}

			if (export) _env.Export(name);
			if (unexport) _env.Unexport(name);
			if (ro) _env.MarkReadonly(name);
			}
		return rc;
		}

	// ── type / command / builtin ──────────────────────────────────────────────

	private int Type(List<string> args)
		{
		bool typeOnly = false, pathOnly = false, pathIfFile = false, all = false, noFuncs = false;
		var names = new List<string>();
		foreach (var a in args)
			{
			if (a == "--") continue;
			if (a.Length > 1 && a[0] == '-')
				{
				foreach (var c in a[1..])
					{
					switch (c)
						{
						case 't': typeOnly = true; break;
						case 'P': pathOnly = true; break;
						case 'p': pathIfFile = true; break;
						case 'a': all = true; break;
						case 'f': noFuncs = true; break;
						default: return BadOption($"-{c}");
						}
					}
				continue;
				}
			names.Add(a);
			}
		int rc = 0;
		foreach (var n in names)
			{
			if (pathOnly)
				{
				// a path found on PATH prints in the shell's forward-slash form; a path given prints as given
				var p = n.Contains('/') || n.Contains('\\') ? (File.Exists(ShellEnvironment.TranslatePath(n)) ? n : null) : Shown(_eval.ResolveOnPath(n));
				if (p is null) rc = 1; else Console.WriteLine(p);
				continue;
				}
			var c = _eval.Classify(n, skipFunctions: noFuncs);
			if (c is null)
				{
				if (!typeOnly && !pathIfFile) Console.Error.WriteLine($"{Err}type: {n}: not found");
				rc = 1; continue;
				}
			var (kind, path) = c.Value;
			path = Shown(path);
			if (pathIfFile) { if (kind == "file") Console.WriteLine(path); continue; }
			if (typeOnly) { Console.WriteLine(kind); continue; }
			switch (kind)
				{
				case "alias":    Console.WriteLine($"{n} is aliased to `{_eval.Aliases[n]}'"); break;
				case "keyword":  Console.WriteLine($"{n} is a shell keyword"); break;
				case "function": Console.WriteLine($"{n} is a function"); Console.WriteLine(FunctionText(_eval.GetFunction(n)!)); break;
				case "builtin":  Console.WriteLine($"{n} is a shell builtin"); break;
				case "file":     if (!all) Console.WriteLine($"{n} is {path}"); break;
				}
			if (all)
				{
				// every match on PATH that can run, in order, as bash: C#Bash showed one, even one that could not run
				// ("type: x: not found" in bash), and none after a builtin's line (2026-10-17)
				var files = n.Contains('/') || n.Contains('\\') ? [path!] : _eval.AllOnPath(n).Select(f => Shown(f)!).ToList();
				foreach (var f in files) Console.WriteLine($"{n} is {f}");
				if (kind == "file" && files.Count == 0) { Console.Error.WriteLine($"{Err}type: {n}: not found"); rc = 1; }
				}
			}
		return rc;
		}

	private int Command(List<string> args)
		{
		bool verbose = false, veryVerbose = false;
		int i = 0;
		for (; i < args.Count; i++)
			{
			var a = args[i];
			if (a == "--") { i++; break; }
			if (a.Length > 1 && a[0] == '-')
				{
				foreach (var c in a[1..])
					{
					if (c == 'v') verbose = true;
					else if (c == 'V') veryVerbose = true;
					else if (c == 'p') { }
					else { return BadOption($"-{c}"); }
					}
				continue;
				}
			break;
			}
		if (i >= args.Count) return 0;
		if (verbose || veryVerbose)
			{
			int rc = 0;
			for (; i < args.Count; i++)
				{
				var n = args[i];
				var c = _eval.Classify(n);
				if (c is null) { rc = 1; continue; }
				var (kind, path) = c.Value;
				path = Shown(path);
				if (veryVerbose)
					{
					switch (kind)
						{
						case "alias":    Console.WriteLine($"{n} is aliased to `{_eval.Aliases[n]}'"); break;
						case "keyword":  Console.WriteLine($"{n} is a shell keyword"); break;
						case "function": Console.WriteLine($"{n} is a function"); break;
						case "builtin":  Console.WriteLine($"{n} is a shell builtin"); break;
						case "file":     Console.WriteLine($"{n} is {path}"); break;
						}
					}
				else
					{
					switch (kind)
						{
						case "alias": Console.WriteLine($"alias {n}='{_eval.Aliases[n]}'"); break;
						case "file":  Console.WriteLine(path); break;
						default:      Console.WriteLine(n); break;
						}
					}
				}
			return rc;
			}
		var name = args[i];
		var rest = args.Skip(i + 1).ToList();
		return _eval.RunCommand(name, rest, skipFunctions: true, [], false);
		}

	private int BuiltinCmd(List<string> args)
		{
		if (args.Count == 0) return 0;
		return _eval.RunBuiltin(args[0], args.Skip(1).ToList());
		}

	// ── test / [ ──────────────────────────────────────────────────────────────

	private int Test(List<string> args) => TestExpr(args) ? 0 : 1;

	private int TestBracket(List<string> args)
		{
		if (args.Count == 0 || args[^1] != "]") { Console.Error.WriteLine(Err + "[: missing `]'"); return 2; }
		return TestExpr(args, args.Count - 1) ? 0 : 1;
		}

	private static readonly HashSet<string> TestUnaryOps =
		["-z", "-n", "-e", "-a", "-f", "-d", "-s", "-r", "-w", "-x", "-L", "-h", "-p", "-S", "-b", "-c",
		 "-k", "-u", "-g", "-N", "-O", "-G", "-t", "-v", "-o", "-R"];
	private static readonly HashSet<string> TestBinaryOps =
		["=", "==", "!=", "<", ">", "-eq", "-ne", "-lt", "-le", "-gt", "-ge", "-nt", "-ot", "-ef"];

	private sealed class TestParser(Builtins owner, List<string> a, int count)
		{
		private readonly Builtins _o = owner;
		private readonly List<string> _a = a;
		private readonly int _n = count;   // arguments in play (excludes a trailing `]`)
		private int _i;
		private bool Has => _i < _n;
		private string Peek => _i < _n ? _a[_i] : "";
		private string Next() => _a[_i++];
		private int Remaining => _n - _i;

		public bool Parse()
			{
			if (_n == 0) return false;
			bool v = Or();
			if (Has) throw new EvalException($"test: {Peek}: unexpected argument");
			return v;
			}

		private bool Or()
			{
			bool l = And();
			while (Has && Peek == "-o") { Next(); bool r = And(); l = l || r; }
			return l;
			}

		private bool And()
			{
			bool l = Not();
			while (Has && Peek == "-a" && Remaining >= 2) { Next(); bool r = Not(); l = l && r; }
			return l;
			}

		private bool Not()
			{
			if (Has && Peek == "!" && Remaining >= 2) { Next(); return !Not(); }
			return Primary();
			}

		private bool Primary()
			{
			if (!Has) throw new EvalException("test: argument expected");
			if (Peek == "(" && Remaining >= 3)
				{
				Next();
				bool v = Or();
				if (Has && Peek == ")") Next(); else throw new EvalException("test: `)' expected");
				return v;
				}
			var first = Next();
			// binary takes precedence when three operands are available and the middle is an operator
			if (Remaining >= 2 && TestBinaryOps.Contains(Peek))
				{
				var op = Next(); var right = Next();
				return _o.TestBinary(op, first, right);
				}
			if (TestUnaryOps.Contains(first) && Has)
				{
				var operand = Next();
				return _o.TestUnary(first, operand);
				}
			return first.Length > 0;
			}
		}

	private bool TestExpr(List<string> args) => TestExpr(args, args.Count);

	/// <summary>Evaluate the first <paramref name="n"/> arguments as a test expression.
	/// The POSIX 1/2/3-argument forms (bash special-cases them too) are answered without
	/// building a parser — that is the shape of every `[ $i -lt N ]` loop condition.</summary>
	private bool TestExpr(List<string> args, int n)
		{
		try
			{
			switch (n)
				{
				case 0: return false;
				case 1: return args[0].Length > 0;
				case 2:
					if (args[0] == "!") return args[1].Length == 0;
					if (TestUnaryOps.Contains(args[0])) return TestUnary(args[0], args[1]);
					break;
				case 3:
					if (TestBinaryOps.Contains(args[1])) return TestBinary(args[1], args[0], args[2]);
					break;
				}
			return new TestParser(this, args, n).Parse();
			}
		catch (EvalException ex) { Console.Error.WriteLine($"{ex.Prefix ?? Err}{ex.Message}"); return false; }
		}

	private bool TestUnary(string op, string val)
		{
		switch (op)
			{
			case "-v": return _env.IsSet(val);
			case "-o": return _eval.Options.LongOptions().Any(o => o.Name == val && o.On);
			case "-R": return false;
			}
		return FileTests.Unary(op, val) ?? throw new EvalException($"test: {op}: unary operator expected");
		}

	private bool TestBinary(string op, string a, string b)
		{
		switch (op)
			{
			case "=": case "==": return a == b;
			case "!=": return a != b;
			case "<":  return string.CompareOrdinal(a, b) < 0;
			case ">":  return string.CompareOrdinal(a, b) > 0;
			case "-eq": return TestInt(a) == TestInt(b);
			case "-ne": return TestInt(a) != TestInt(b);
			case "-lt": return TestInt(a) <  TestInt(b);
			case "-le": return TestInt(a) <= TestInt(b);
			case "-gt": return TestInt(a) >  TestInt(b);
			case "-ge": return TestInt(a) >= TestInt(b);
			}
		return FileTests.Binary(op, a, b) ?? throw new EvalException($"test: {op}: binary operator expected");
		}

	private static long TestInt(string s) =>
		long.TryParse(s.Trim(), out var v) ? v : throw new EvalException($"test: {s}: integer expression expected");

	// ── exit / return / break / continue ─────────────────────────────────────

	private int DoExit(List<string> args)
		{
		int code = _env.LastExitCode;
		if (args.Count > 0)
			{
			if (!int.TryParse(args[0], out code)) { Console.Error.WriteLine($"{Err}exit: {args[0]}: numeric argument required"); code = 2; }
			code &= 0xFF;
			}
		throw new ExitException(code);
		}

	private int DoReturn(List<string> args)
		{
		int code = _env.LastExitCode;
		if (args.Count > 0)
			{
			if (!int.TryParse(args[0], out code)) { Console.Error.WriteLine($"{Err}return: {args[0]}: numeric argument required"); code = 2; }
			code &= 0xFF;
			}
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

	// ── let ───────────────────────────────────────────────────────────────────

	private int Let(List<string> args)
		{
		if (args.Count == 0) { Console.Error.WriteLine(Err + "let: expression expected"); return 1; }
		long last = 0;
		foreach (var a in args) last = _eval.Expander.EvalArithmetic(a);
		return last != 0 ? 0 : 1;
		}

	// ── pushd / popd / dirs ───────────────────────────────────────────────────

	private readonly List<string> _dirStack = [];

	/// <summary>A pipeline stage's builtins start from its parent's state: the `pushd` stack (as a
	/// subshell inherits it). getopts position and fd readers start fresh.</summary>
	internal void InheritFrom(Builtins parent) => _dirStack.AddRange(parent._dirStack);

	/// <summary>An entry as `dirs` shows it: HOME at its start becomes `~` when `/` or the end follows and
	/// HOME is longer than `/` (bash's polite_directory_format; the case is ignored, as Windows ignores it).
	/// Until 2026-10-12 HOME=/home/u turned /home/ux into `~x`.</summary>
	private string DisplayDir(string d)
		{
		var home = _env.Get("HOME");
		if (home.Length > 1 && d.StartsWith(home, StringComparison.OrdinalIgnoreCase) && (d.Length == home.Length || d[home.Length] == '/'))
			return "~" + d[home.Length..];
		return d;
		}

	/// <summary>The stack as bash numbers it: 0 is the current directory, 1… the saved ones (`_dirStack`).</summary>
	private List<string> FullStack() => [_env.Cwd, .. _dirStack];

	/// <summary>What pushd and popd print after a change: `dirs`.</summary>
	private void PrintDirs() => Console.WriteLine(string.Join(" ", FullStack().Select(DisplayDir)));

	/// <summary>A `+N` / `-N` operand of pushd, popd or dirs: its N, or null when it is not of that form or
	/// N is not a number (then bash's "invalid number" and the usage line, status 2 in <paramref name="rc"/>;
	/// `-x` and `dirs -lv` are such). One option letter is the caller's to match first.</summary>
	private long? StackOperand(string a, out int rc)
		{
		rc = 0;
		if (a.Length == 0 || a[0] is not ('+' or '-')) return null;
		if (long.TryParse(a.AsSpan(1), out long n)) return n;
		rc = BadOption(a, "invalid number");
		return null;
		}

	/// <summary>bash's pushd_error: an empty stack says so; otherwise the operand is out of range.</summary>
	private int StackError(string word)
		{
		Console.Error.WriteLine(_dirStack.Count == 0 ? $"{Err}{_called}: directory stack empty" : $"{Err}{_called}: {word}: directory stack index out of range");
		return 1;
		}

	/// <summary>bash 5.1's pushd.def. No operand: exchange the current directory and entry 1. `+N` / `-N`:
	/// rotate the stack so that entry N, counted from the left / the right of `dirs`, is the current
	/// directory. A directory: cd to it and save the old one as entry 1. `-n` does no cd: a directory is
	/// saved as entry 1 as written, and a rotation drops the entry it would have changed to (bash does).
	/// The stack is printed after a change, with `-n` too, but not after a `-n` rotation. Until 2026-10-12
	/// `+N` / `-N` were refused and `-n` meant only "print nothing".</summary>
	private int Pushd(List<string> args)
		{
		int i = args.Count > 0 && args[0] == "--" ? 1 : 0;
		bool skipopt = i == 1;
		if (i == args.Count)
			{
			if (_dirStack.Count == 0) { Console.Error.WriteLine(Err + "pushd: no other directory"); return 1; }
			var cur = _env.Cwd;
			if (Cd([_dirStack[0]]) != 0) return 1;
			_dirStack[0] = cur;
			PrintDirs();
			return 0;
			}
		bool nocd = false;
		long? rotate = null;
		for (; !skipopt && i < args.Count; i++)
			{
			var a = args[i];
			if (a == "-n") { nocd = true; continue; }
			if (a == "--") { i++; break; }
			if (a == "-") break;   // `pushd -` is `cd -`
			if (StackOperand(a, out int rc) is { } n)
				{
				long num = a[0] == '-' ? _dirStack.Count - n : n;
				if (num < 0 || num > _dirStack.Count) return StackError(a);
				rotate = num;
				continue;
				}
			if (rc != 0) return rc;
			break;
			}
		if (rotate is { } k)
			{
			if (k == 0)
				{
				if (nocd) return 0;
				if (Cd([_env.Cwd]) != 0) return 1;
				PrintDirs();
				return 0;
				}
			// the whole stack turns left k places; entry k becomes the current directory, and with -n it is
			// dropped, the current directory staying (bash rotates first and changes directory after)
			var all = FullStack();
			var turned = all.Skip((int)k).Concat(all.Take((int)k)).ToList();
			_dirStack.Clear();
			_dirStack.AddRange(turned.Skip(1));
			if (nocd) return 0;
			if (Cd([turned[0]]) != 0) return 1;
			PrintDirs();
			return 0;
			}
		if (i >= args.Count) return 0;   // `pushd -n`
		if (args.Count - i > 1) { Console.Error.WriteLine(Err + "pushd: too many arguments"); return 1; }
		var dir = args[i];
		if (nocd) _dirStack.Insert(0, dir);
		else
			{
			var old = _env.Cwd;
			if (Cd([dir]) != 0) return 1;
			_dirStack.Insert(0, old);
			}
		PrintDirs();
		return 0;
		}

	/// <summary>bash 5.1's popd.def. No operand or `+0`: cd to entry 1 and remove it. `+N` / `-N`: remove
	/// entry N, counted from the left / the right of `dirs`, without changing directory. `-n`: remove
	/// entry 1 without the cd. The stack is printed after. Until 2026-10-12 `+N` / `-N` were refused
	/// and `-n` meant only "print nothing".</summary>
	private int Popd(List<string> args)
		{
		bool nocd = false, fromRight = false;
		long which = 0;
		string word = "";
		foreach (var a in args)
			{
			if (a == "-n") { nocd = true; continue; }
			if (a == "--") break;
			if (StackOperand(a, out int rc) is { } n) { which = n; fromRight = a[0] == '-'; word = a; continue; }
			if (rc != 0) return rc;
			if (a.Length > 0) { Console.Error.WriteLine($"{Err}popd: {a}: invalid argument"); PrintUsage("popd"); return 2; }
			break;
			}
		int count = _dirStack.Count;
		if (which > count || which < -count || (count == 0 && which == 0)) return StackError(word);
		long k = fromRight ? count - which : which;
		if (k == 0)
			{
			if (!nocd && Cd([_dirStack[0]]) != 0) return 1;
			_dirStack.RemoveAt(0);
			}
		else if (k < 0 || k > count) return StackError(word);
		else _dirStack.RemoveAt((int)k - 1);
		PrintDirs();
		return 0;
		}

	/// <summary>bash 5.1's dirs: `-c` clears; `+N` / `-N` prints entry N from the left / the right
	/// (out of range: "N: directory stack index out of range", without the sign); `-v` numbers one per
	/// line, `-p` one per line, `-l` without `~`. Each option is its own word (`dirs -lv` is an invalid
	/// number). Until 2026-10-12 `+N` / `-N` were refused and `-p` and `-l` did nothing.</summary>
	private int Dirs(List<string> args)
		{
		bool longForm = false, clear = false, perLine = false, numbered = false, fromRight = false;
		long? index = null;
		string digits = "";
		foreach (var a in args)
			{
			if (a == "-l") longForm = true;
			else if (a == "-c") clear = true;
			else if (a == "-v") numbered = true;
			else if (a == "-p") perLine = true;
			else if (a == "--") break;
			else if (StackOperand(a, out int rc) is { } n) { index = n; fromRight = a[0] == '-'; digits = a[1..]; }
			else if (rc != 0) return rc;
			else return BadOption(a);   // bash: `dirs foo` is an invalid option
			}
		if (clear) { _dirStack.Clear(); return 0; }
		var all = FullStack();
		string Entry(int e) => longForm ? all[e] : DisplayDir(all[e]);
		if (index is { } want)
			{
			long k = fromRight ? all.Count - 1 - want : want;
			if (want < 0 || k < 0 || k >= all.Count) { Console.Error.WriteLine($"{Err}dirs: {digits}: directory stack index out of range"); return 1; }
			Console.WriteLine(numbered ? $"{k,2}  {Entry((int)k)}" : Entry((int)k));
			return 0;
			}
		if (numbered) for (int e = 0; e < all.Count; e++) Console.WriteLine($"{e,2}  {Entry(e)}");
		else if (perLine) for (int e = 0; e < all.Count; e++) Console.WriteLine(Entry(e));
		else Console.WriteLine(string.Join(" ", Enumerable.Range(0, all.Count).Select(Entry)));
		return 0;
		}

	// ── kill ──────────────────────────────────────────────────────────────────

	private int Kill(List<string> args)
		{
		// the signal is checked as bash checks it (trap's table): a bad spec is an error and nothing is
		// signalled; until 2026-10-10 `kill -Z 1` went on to kill. Any good one but 0 is forceful here.
		int? Signal(string spec) =>
			CanonSignal(spec) is { } c && c is not ("DEBUG" or "ERR" or "RETURN") ? (c == "EXIT" ? 0 : Array.IndexOf(LinuxSignals, c)) : null;
		int BadSignal(string spec) { Console.Error.WriteLine($"{Err}kill: {spec}: invalid signal specification"); return 1; }
		// bash's kill.def: `-l`/`-L`; `-s SIG`, `-n NUM`, `-sSIG`, `-nNUM`; `--`; the first other `-WORD`
		// is the signal (a later one is an operand: a process group). The signal is checked only when
		// killing, so `kill -l -9` lists. Until 2026-10-12 every word after `-l` was an operand.
		bool list = false;
		string? sigspec = null;
		int i = 0;
		for (; i < args.Count; i++)
			{
			var a = args[i];
			if (a is "-l" or "-L") { list = true; continue; }
			if (a is "-s" or "-n")
				{
				if (i + 1 >= args.Count) { Console.Error.WriteLine($"{Err}kill: {a}: option requires an argument"); return 1; }   // bash: no usage line, 1
				sigspec = args[++i];
				continue;
				}
			if (a.Length > 2 && a[0] == '-' && ((a[1] == 's' && char.IsAsciiLetter(a[2])) || (a[1] == 'n' && char.IsAsciiDigit(a[2]))))
				{ sigspec = a[2..]; continue; }
			if (a == "--") { i++; break; }
			if (a.Length > 0 && a[0] == '-' && sigspec is null) { sigspec = a[1..]; continue; }   // -TERM, -9, -SIGKILL
			break;
			}
		var targets = args.Skip(i).ToList();
		if (list)
			{
			if (targets.Count == 0) { PrintSignalTable(); return 0; }
			// kill -l N names a signal (N may be an exit status, 128+N; 0 is EXIT; 32 and 33 have no name and
			// print nothing); kill -l NAME numbers it (EXIT 0, DEBUG 65, ERR 66, RETURN 67, as bash numbers them)
			int lrc = 0;
			foreach (var t in targets)
				{
				if (int.TryParse(t, out int n))
					{
					if (n > 128) n -= 128;
					if (n is < 0 or > 64) lrc = BadSignal(t);
					else if (n == 0) Console.WriteLine("EXIT");
					else if (n is not (32 or 33)) Console.WriteLine(LinuxSignals[n]);
					}
				else if (CanonSignal(t) is { } c)
					Console.WriteLine(c switch { "EXIT" => 0, "DEBUG" => 65, "ERR" => 66, "RETURN" => 67, _ => Array.IndexOf(LinuxSignals, c) });
				else lrc = BadSignal(t);
				}
			return lrc;
			}
		if (sigspec is not null && Signal(sigspec) is null) return BadSignal(sigspec);
		bool sigZero = sigspec is not null && Signal(sigspec) == 0;
		if (targets.Count == 0) { PrintUsage("kill"); return 2; }
		int rc = 0;
		foreach (var t in targets)
			{
			if (t.StartsWith('%') || (int.TryParse(t, out var maybe) && BackgroundJob.IsSyntheticPid(maybe)))
				{
				if (sigZero) continue;
				if (_eval.KillJob(t, out var err) != 0) { Console.Error.WriteLine($"{Err}kill: {err}"); rc = 1; }
				continue;
				}
			if (!int.TryParse(t, out var pid)) { Console.Error.WriteLine($"{Err}kill: {t}: arguments must be process or job IDs"); rc = 1; continue; }
			try
				{
				var proc = System.Diagnostics.Process.GetProcessById(pid);
				if (!sigZero) proc.Kill(true);
				}
			catch (ArgumentException) { Console.Error.WriteLine($"{Err}kill: ({pid}) - No such process"); rc = 1; }
			catch (Exception ex)      { Console.Error.WriteLine($"{Err}kill: ({pid}) - {ex.Message}"); rc = 1; }
			}
		return rc;
		}

	// ── env ───────────────────────────────────────────────────────────────────

	/// <summary>GNU env. Until 2026-10-08 its options reached only part of what it runs: a
	/// NAME=VALUE whose name was not a shell identifier (`STDOUT.OutputLevel=trace`, `X-Y=2`) became
	/// the command; `-u` was applied only when env printed; `-i` did not reach a builtin
	/// (`env -i printenv` printed everything); `-uNAME`, `--unset=NAME`, `-` and `-0` were refused or
	/// ignored.</summary>
	private int Env(List<string> args)
		{
		bool clear = false, nul = false;
		var unset = new List<string>();
		var assigns = new List<(string, string)>();
		int i = 0;
		for (; i < args.Count; i++)
			{
			var a = args[i];
			if (a is "-i" or "--ignore-environment" or "-") clear = true;
			else if (a is "-u" or "--unset" && i + 1 < args.Count) unset.Add(args[++i]);
			else if (a.StartsWith("--unset=")) unset.Add(a["--unset=".Length..]);
			else if (a.StartsWith("-u") && a.Length > 2) unset.Add(a[2..]);
			else if (a is "-0" or "--null") nul = true;
			else if (a == "--") { i++; break; }
			else if (a.StartsWith('-') && a.Length > 1) { Console.Error.WriteLine($"env: invalid option -- '{a[1..]}'"); return 125; }
			else break;
			}
		// every leading operand containing '=' is an assignment, whatever its name (GNU); only the
		// shell's own `NAME=value cmd` prefix is limited to identifiers
		for (; i < args.Count; i++)
			{
			var a = args[i];
			int eq = a.IndexOf('=');
			if (eq < 0) break;
			if (eq > 0) assigns.Add((a[..eq], a[(eq + 1)..]));   // "=x" is accepted and sets nothing (no empty name on Windows)
			}
		if (i >= args.Count)
			{
			var env = clear ? new Dictionary<string, string>(StringComparer.Ordinal) : _env.GetExportedVars();
			foreach (var u in unset) env.Remove(u);
			foreach (var (k, v) in assigns) env[k] = v;
			string term = nul ? "\0" : "\n";
			foreach (var (k, v) in env) Console.Out.Write($"{k}={v}{term}");
			return 0;
			}
		var name = args[i];
		var rest = args.Skip(i + 1).ToList();
		return _eval.RunCommand(name, rest, false, assigns, clear, unset);
		}

	// ── yes ───────────────────────────────────────────────────────────────────

	/// <summary>Infinite producer; ends when the consumer closes the pipe (BrokenPipeException
	/// from the pipe write, bash's SIGPIPE) or on Ctrl+C.</summary>
	private int Yes(List<string> args)
		{
		var line = args.Count > 0 ? string.Join(" ", args) : "y";
		long n = 0;
		while (true)
			{
			Console.Out.WriteLine(line);
			if ((++n & 0x3FF) == 0) { Console.Out.Flush(); _eval.CheckInterrupt(); }
			}
		}

	// ── sleep ─────────────────────────────────────────────────────────────────

	private int Sleep(List<string> args)
		{
		double total = 0;
		foreach (var a in args)
			{
			var s = a.Trim();
			double mult = 1;
			if (s.Length > 0 && char.IsLetter(s[^1]))
				{
				mult = s[^1] switch { 's' => 1, 'm' => 60, 'h' => 3600, 'd' => 86400, _ => 1 };
				s = s[..^1];
				}
			if (s == "infinity" || s == "inf") { total = double.MaxValue; break; }
			if (!double.TryParse(s, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var secs))
				{ Console.Error.WriteLine($"sleep: invalid time interval '{a}'"); return 1; }
			total += secs * mult;
			}
		// One timed wait that an interrupt (Ctrl+C, `timeout`) ends at once. It was 50 ms slices of
		// Thread.Sleep polling the flag (the architect: never sleep-poll for synchronization).
		bool forever = total >= int.MaxValue / 1000.0;
		long deadline = forever ? long.MaxValue : Environment.TickCount64 + (long)(total * 1000);
		var interrupts = _eval.InterruptHandles();
		var job = Evaluator.CurrentJob;
		if (job is not null) job.Parked = true;   // shell exit need not wait for this job
		try
			{
			while (true)
				{
				_eval.CheckInterrupt();   // throws, or ran the INT trap: sleep on, as before
				long left = deadline - Environment.TickCount64;
				if (left <= 0) break;
				WaitHandle.WaitAny(interrupts, forever ? System.Threading.Timeout.Infinite : (int)Math.Min(left, int.MaxValue));
				}
			}
		finally { if (job is not null) job.Parked = false; }
		return 0;
		}

	/// <summary>A path the shell found (PATH lookup) as `type`, `command -v`, `which` and `hash` print it:
	/// the shell's forward-slash form (DECISIONS 2026-09-11/12). The lookup itself keeps the native
	/// form, which is what launches the program. Backslash paths until 2026-10-08.</summary>
	[return: System.Diagnostics.CodeAnalysis.NotNullIfNotNull(nameof(path))]
	internal static string? Shown(string? path)
		{
		if (path is null) return null;
		// the file's name as it is on disk: a PATHEXT match spells it git.EXE, the file is git.exe
		return ShellEnvironment.ToShellPath(ShellFile.OnDiskName(path));
		}

	// ── PATH lookup (used by which-style helpers elsewhere in this class) ──────

	private static string? FindInPath(string name)
		{
		var path = ShellEnvironment.CurrentPath;   // the shell's own PATH (2026-10-11)
		var exts = System.Environment.GetEnvironmentVariable("PATHEXT") ?? ".COM;.EXE;.BAT;.CMD";
		foreach (var dir in path.Split(Path.PathSeparator))
			{
			if (dir.Length == 0) continue;
			string full;
			try { full = Path.Combine(ShellEnvironment.TranslatePath(dir), name); } catch { continue; }
			if (File.Exists(full)) return full;
			foreach (var ext in exts.Split(';'))
				{
				var withExt = full + ext;
				if (File.Exists(withExt)) return withExt;
				}
			}
		return null;
		}

	// ── compgen ─────────────────────────────────────────────────────────────────

	private const string CompgenUsage = "compgen: usage: compgen [-abcdefgjksuv] [-o option] [-A action] [-G globpat] [-W wordlist] [-F function] [-C command] [-X filterpat] [-P prefix] [-S suffix] [word]";

	/// <summary>bash's `-A` action names, in the order bash generates them (pcomplete.c's CA_ bits).</summary>
	private static readonly string[] CompgenActions =
		["alias", "arrayvar", "binding", "builtin", "command", "directory", "disabled", "enabled", "export", "file",
		 "function", "group", "helptopic", "hostname", "job", "keyword", "running", "service", "setopt", "shopt",
		 "signal", "stopped", "user", "variable"];

	/// <summary>Actions C#Bash cannot generate: readline bindings, users, groups, host and service names (the
	/// greenlit scope, 2026-10-12), and help topics (there is no `help`; the architect, 2026-10-15).</summary>
	private static readonly HashSet<string> CompgenUnavailable = ["binding", "group", "helptopic", "hostname", "service", "user"];

	/// <summary>The reserved words in bash's own order (`compgen -k`).</summary>
	private static readonly string[] BashKeywordOrder =
		["if", "then", "else", "elif", "fi", "case", "esac", "for", "select", "while", "until", "do", "done", "in",
		 "function", "time", "{", "}", "!", "[[", "]]", "coproc"];

	/// <summary>
	/// `compgen` as bash 5.1's (daq-d1, 2026-10-12; scope greenlit 2026-10-12). The actions run in bash's order,
	/// then `-G`, then `-W`; each keeps the names that start with the word (`-G` ignores it, `-f`/`-d` complete it
	/// as a path); `-X` removes the matches of its pattern (`&` is the word, a leading `!` keeps them instead);
	/// `-P`/`-S` go on last. Status 1 when nothing came out, 2 for a bad option or action. It describes THIS shell
	/// (the architect, 2026-10-15: "C#Bash is really _only_ about its builtins"): `-b` lists every C#Bash builtin,
	/// the in-process tools included, and `-c` lists aliases, keywords, functions and builtins, with no scan of
	/// PATH (deferred). `-o`, `-F`, `-C` and the unavailable actions answer with the not-available notice.
	/// </summary>
	private int Compgen(List<string> args)
		{
		var actions = new HashSet<string>(StringComparer.Ordinal);
		string? glob = null, wordList = null, filter = null, prefix = null, suffix = null, missing = null;
		int i = 0;
		for (; i < args.Count; i++)
			{
			var a = args[i];
			if (a == "--") { i++; break; }
			if (a.Length < 2 || a[0] != '-') break;
			for (int k = 1; k < a.Length; k++)
				{
				char c = a[k];
				if ("abcdefgjksuv".Contains(c))
					{
					actions.Add(c switch
						{
						'a' => "alias", 'b' => "builtin", 'c' => "command", 'd' => "directory", 'e' => "export", 'f' => "file",
						'g' => "group", 'j' => "job", 'k' => "keyword", 's' => "service", 'u' => "user", _ => "variable",
						});
					continue;
					}
				if (!"AGWXPSoFC".Contains(c))
					{
					Console.Error.WriteLine($"{Err}compgen: -{c}: invalid option");
					Console.Error.WriteLine(CompgenUsage);
					return 2;
					}
				// the option's argument: the rest of this word, else the next word
				string? v = k + 1 < a.Length ? a[(k + 1)..] : i + 1 < args.Count ? args[++i] : null;
				k = a.Length;
				if (v is null)
					{
					Console.Error.WriteLine($"{Err}compgen: -{c}: option requires an argument");
					Console.Error.WriteLine(CompgenUsage);
					return 2;
					}
				switch (c)
					{
					case 'A':
						if (Array.IndexOf(CompgenActions, v) < 0) { Console.Error.WriteLine($"{Err}compgen: {v}: invalid action name"); return 2; }
						actions.Add(v);
						break;
					case 'G': glob = v; break;
					case 'W': wordList = v; break;
					case 'X': filter = v; break;
					case 'P': prefix = v; break;
					case 'S': suffix = v; break;
					case 'o': missing ??= $"compgen -o {v}"; break;
					case 'F': missing ??= "compgen -F (completion functions)"; break;
					case 'C': missing ??= "compgen -C (completion commands)"; break;
					}
				}
			}
		string word = i < args.Count ? args[i] : "";
		missing ??= CompgenActions.FirstOrDefault(n => actions.Contains(n) && CompgenUnavailable.Contains(n)) is { } u ? $"compgen -A {u}" : null;
		if (missing is not null)
			{
			foreach (var line in NotAvailable.Lines($"{Err}compgen: ", missing)) Console.Error.WriteLine(line);
			return 2;
			}
		if (actions.Count == 0 && glob is null && wordList is null) return 0;   // nothing asked for (bash)

		bool Starts(string s) => s.StartsWith(word, StringComparison.Ordinal);
		IEnumerable<string> Sorted(IEnumerable<string> names) => names.Where(Starts).Order(StringComparer.Ordinal);
		var list = new List<string>();
		foreach (var act in CompgenActions)
			{
			if (!actions.Contains(act)) continue;
			switch (act)
				{
				case "alias":     list.AddRange(Sorted(_eval.Aliases.Keys)); break;
				case "arrayvar":  list.AddRange(Sorted(_env.VariablesWithValues().Where(n => _env.IsArray(n) || _env.IsAssoc(n)))); break;
				case "builtin" or "enabled": list.AddRange(Sorted(Names)); break;
				case "command":
					list.AddRange(Sorted(_eval.Aliases.Keys));
					list.AddRange(BashKeywordOrder.Where(Starts));
					list.AddRange(Sorted(_eval.FunctionNames));
					list.AddRange(Sorted(Names));
					break;
				case "directory": list.AddRange(CompgenPaths(word, directoriesOnly: true)); break;
				case "export":    list.AddRange(Sorted(_env.VariablesWithValues().Where(_env.IsExported))); break;
				case "file":      list.AddRange(CompgenPaths(word, directoriesOnly: false)); break;
				case "function":  list.AddRange(Sorted(_eval.FunctionNames)); break;
				case "job" or "running":   // the newest job first, each by its first word (bash's it_init_joblist)
					foreach (var j in _eval.JobTable.OrderByDescending(j => j.Id))
						{
						if (act == "running" && j.Done) continue;
						var text = j.Stages.Count > 0 ? j.Stages[0] : j.Command;
						var first = text.TrimStart().Split([' ', '\t', '\n'], 2)[0];
						if (Starts(first)) list.Add(first);
						}
					break;
				case "keyword":   list.AddRange(BashKeywordOrder.Where(Starts)); break;
				case "setopt":    list.AddRange(_eval.Options.LongOptions().Select(o => o.Name).Where(Starts)); break;
				case "shopt":     list.AddRange(ShellOptions.KnownShopts.Where(Starts)); break;
				case "signal":    list.AddRange(CompgenSignals().Where(Starts)); break;
				case "variable":  list.AddRange(Sorted(_env.VariablesWithValues())); break;
				// "disabled" and "stopped": C#Bash disables no builtin and stops no job
				}
			}
		if (glob is not null) list.AddRange(CompgenGlob(glob));
		if (wordList is not null) list.AddRange(CompgenWords(wordList).Where(Starts));
		if (filter is not null)
			{
			bool keep = filter.StartsWith('!');
			var pattern = new StringBuilder();
			for (int k = keep ? 1 : 0; k < filter.Length; k++)
				{
				if (filter[k] == '\\' && k + 1 < filter.Length && filter[k + 1] == '&') { pattern.Append('&'); k++; }
				else if (filter[k] == '&') pattern.Append(word);
				else pattern.Append(filter[k]);
				}
			var p = pattern.ToString();
			list = [.. list.Where(s => Glob.Match(p, s) == keep)];
			}
		foreach (var s in list) Console.WriteLine($"{prefix}{s}{suffix}");
		return list.Count > 0 ? 0 : 1;
		}

	/// <summary>`compgen -A signal`: EXIT, the 64 Linux signals by bash's names (32 and 33 are SIGJUNK), then the
	/// shell's own DEBUG, ERR and RETURN.</summary>
	private static IEnumerable<string> CompgenSignals()
		{
		yield return "EXIT";
		for (int n = 1; n < LinuxSignals.Length; n++) yield return n is 32 or 33 ? $"SIGJUNK({n})" : "SIG" + LinuxSignals[n];
		yield return "DEBUG"; yield return "ERR"; yield return "RETURN";
		}

	/// <summary>`compgen -f` / `-d`: the word completed as a path, its directory part kept as written; dot files
	/// included, `.` and `..` never; in the directory's own order (bash's readdir order, unsorted).</summary>
	private static IEnumerable<string> CompgenPaths(string word, bool directoriesOnly)
		{
		int slash = word.LastIndexOf('/');
		string dirPart = slash >= 0 ? word[..(slash + 1)] : "", leaf = slash >= 0 ? word[(slash + 1)..] : word;
		var dir = ShellEnvironment.TranslatePath(dirPart.Length == 0 ? "." : dirPart);
		if (!Directory.Exists(dir)) yield break;
		IEnumerable<string> entries;
		try { entries = [.. Directory.EnumerateFileSystemEntries(dir)]; }
		catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { yield break; }
		foreach (var e in entries)
			{
			var name = Path.GetFileName(e);
			if (!name.StartsWith(leaf, StringComparison.Ordinal) || (directoriesOnly && !Directory.Exists(e))) continue;
			yield return dirPart + name;
			}
		}

	/// <summary>`compgen -G`: the pattern's pathname expansion, nothing when it matches nothing.</summary>
	private IEnumerable<string> CompgenGlob(string pattern)
		{
		if (pattern.IndexOfAny(['*', '?', '[']) < 0)
			return ShellFile.Exists(ShellEnvironment.TranslatePath(pattern)) || Directory.Exists(ShellEnvironment.TranslatePath(pattern)) ? [pattern] : [];
		var o = _eval.Options;
		try { return Glob.Expand(pattern, o.DotGlob, o.NoCaseGlob, o.GlobStar); }
		catch { return []; }
		}

	/// <summary>`compgen -W`: the list split at IFS's characters where they are not quoted, then each word
	/// expanded as bash's expand_words_shellexp does: braces, tilde, parameters, command substitution,
	/// arithmetic, word splitting and quote removal, never pathname expansion (`-W '*'` stays `*`).</summary>
	private List<string> CompgenWords(string list)
		{
		string ifs = _env.IsSet("IFS") ? _env.Get("IFS") : " \t\n";
		var pieces = new List<string>();
		var cur = new StringBuilder();
		char quote = '\0';   // ' or " while inside one
		int depth = 0;       // inside $( … ), ${ … } or ` … `
		for (int k = 0; k < list.Length; k++)
			{
			char c = list[k];
			if (c == '\\' && quote != '\'' && k + 1 < list.Length) { cur.Append(c).Append(list[++k]); continue; }
			if (quote != '\0')
				{
				cur.Append(c);
				if (c == quote && (quote == '\'' || depth == 0)) quote = '\0';
				continue;
				}
			if (c is '\'' or '"' && depth == 0) { quote = c; cur.Append(c); continue; }
			if (c == '$' && k + 1 < list.Length && list[k + 1] is '(' or '{') { depth++; cur.Append(c).Append(list[++k]); continue; }
			if (depth > 0)
				{
				if (c is '(' or '{') depth++;
				else if (c is ')' or '}') depth--;
				cur.Append(c);
				continue;
				}
			if (ifs.Contains(c)) { if (cur.Length > 0) pieces.Add(cur.ToString()); cur.Clear(); continue; }
			// not a delimiter here: keep characters the shell's grammar would split on as part of the word
			if (c is ' ' or '\t' or '\n' or '&' or '|' or ';' or '<' or '>' or '(' or ')') cur.Append('\\');
			cur.Append(c);
			}
		if (cur.Length > 0) pieces.Add(cur.ToString());

		var result = new List<string>();
		bool noGlob = _eval.Options.NoGlob;
		_eval.Options.NoGlob = true;
		try
			{
			foreach (var piece in pieces)
				foreach (var w in Parser.Parser.ParseWords(piece))
					result.AddRange(_eval.Expander.ExpandToFields(w));
			}
		finally { _eval.Options.NoGlob = noGlob; }
		return result;
		}

	/// <summary>`complete` and `compopt`: programmable completion, which only an interactive bash uses, is not
	/// in C#Bash (the greenlit compgen scope, 2026-10-12).</summary>
	private int CompletionNotAvailable(string name)
		{
		foreach (var line in NotAvailable.Lines($"{Err}{name}: ", $"{name} (programmable completion)")) Console.Error.WriteLine(line);
		return 2;
		}
	}
