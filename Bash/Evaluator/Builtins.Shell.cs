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
		if (i < args.Count && args[i] == "--") i++;
		if (i >= args.Count) { Console.Error.WriteLine("bash: printf: usage: printf [-v var] format [arguments]"); return 2; }
		int rc = PrintfFormatter.Format(args[i], args.Skip(i + 1).ToList(), Console.Error, out var text);
		if (target is not null) AssignVar(target, text);
		else Console.Write(text);
		return rc;
		}

	/// <summary>Assign `name` or `name[subscript]` (used by printf -v, read, getopts).</summary>
	private void AssignVar(string spec, string value)
		{
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

	private static bool IsIdentifier(string s) =>
		s.Length > 0 && (char.IsLetter(s[0]) || s[0] == '_') && s.All(c => char.IsLetterOrDigit(c) || c == '_');

	// ── cd / pwd ──────────────────────────────────────────────────────────────

	private int Cd(List<string> args)
		{
		string? target = null;
		foreach (var a in args)
			{
			if (a is "-P" or "-L" or "--" or "-e" or "-@") continue;
			if (a.StartsWith('-') && a.Length > 1) continue;
			target ??= a;
			}
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
			if (dest.Length == 0) { Console.Error.WriteLine("bash: cd: OLDPWD not set"); return 1; }
			printDir = true;
			}
		else if (target.Length == 0) return 0;
		else dest = ShellEnvironment.TranslatePath(target);

		if (!Directory.Exists(dest))
			{
			Console.Error.WriteLine(File.Exists(dest)
				? $"bash: cd: {target}: Not a directory"
				: $"bash: cd: {target}: No such file or directory");
			return 1;
			}
		var old = Directory.GetCurrentDirectory();
		try { Directory.SetCurrentDirectory(dest); }
		catch (Exception ex) { Console.Error.WriteLine($"bash: cd: {target}: {ex.Message}"); return 1; }
		_env.Set("OLDPWD", ShellEnvironment.ToShellPath(old)); _env.Export("OLDPWD");
		_env.Set("PWD", ShellEnvironment.ShellCwd()); _env.Export("PWD");
		if (printDir) Console.WriteLine(ShellEnvironment.ShellCwd());
		return 0;
		}

	/// <summary>Prints the forward-slash Windows form: a valid Windows path containing no shell
	/// escape characters (ratified 2026-09-11, refining DECISIONS 2026-09-04 decision 3).</summary>
	private static int Pwd(List<string> args)
		{
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
		bool print = false, unexport = false, funcs = false;
		var names = new List<string>();
		foreach (var a in args)
			{
			if      (a == "-p") print = true;
			else if (a == "-n") unexport = true;
			else if (a == "-f") funcs = true;
			else if (a == "--") continue;
			else names.Add(a);
			}
		if (names.Count == 0 || print)
			{
			foreach (var n in _env.VariableNames().Where(_env.IsExported).OrderBy(x => x, StringComparer.Ordinal))
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
			if (!IsIdentifier(name)) { Console.Error.WriteLine($"bash: export: `{spec}': not a valid identifier"); rc = 1; continue; }
			if (eq >= 0)
				{
				var val = spec[(eq + 1)..];
				if (append) _env.Append(name, val); else _env.Set(name, val);
				}
			if (unexport) _env.Unexport(name); else _env.Export(name);
			}
		return rc;
		}

	private int Unset(List<string> args)
		{
		bool func = false, onlyVar = false;
		int rc = 0;
		foreach (var a in args)
			{
			if (a == "-f") { func = true; continue; }
			if (a == "-v") { onlyVar = true; continue; }
			if (a is "--" or "-n") continue;
			if (func || (!onlyVar && !_env.IsSet(a) && _eval.HasFunction(a)))
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
				if (_env.IsReadonly(name)) { Console.Error.WriteLine($"bash: unset: {name}: cannot unset: readonly variable"); rc = 1; continue; }
				if (_env.IsAssoc(name)) _env.UnsetAssocElement(name, key);
				else if (key is "@" or "*") _env.Unset(name);
				else _env.UnsetArrayElement(name, (int)_eval.Expander.EvalArithmetic(key));
				continue;
				}
			if (!_env.Unset(a)) { Console.Error.WriteLine($"bash: unset: {a}: cannot unset: readonly variable"); rc = 1; }
			}
		return rc;
		}

	private int Readonly(List<string> args)
		{
		bool print = false, funcs = false, array = false, assoc = false;
		var names = new List<string>();
		foreach (var a in args)
			{
			if      (a == "-p") print = true;
			else if (a == "-f") funcs = true;
			else if (a == "-a") array = true;
			else if (a == "-A") assoc = true;
			else if (a == "--") continue;
			else names.Add(a);
			}
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
			if (!IsIdentifier(name)) { Console.Error.WriteLine($"bash: readonly: `{spec}': not a valid identifier"); return 1; }
			if (eq >= 0)
				{
				var val = spec[(eq + 1)..];
				if ((array || assoc) && val.StartsWith('(') && val.EndsWith(')')) AssignArrayLiteral(name, val, assoc, false);
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
						{ Console.Error.WriteLine($"bash: set: {optName}: invalid option name"); return 2; }
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
							{ Console.Error.WriteLine($"bash: set: {optName}: invalid option name"); return 2; }
						break;
						}
					if (!_eval.Options.Apply(f, enable))
						{ Console.Error.WriteLine($"bash: set: -{f}: invalid option"); return 2; }
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
						default: Console.Error.WriteLine($"bash: shopt: -{c}: invalid option"); return 2;
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
				if (set || unset) { if (!_eval.Options.ApplyLong(n, set)) { Console.Error.WriteLine($"bash: shopt: {n}: invalid option name"); rc0 = 1; } continue; }
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
				if (err is not null) { Console.Error.WriteLine($"bash: shopt: {err}"); rc = 1; }
				continue;
				}
			if (!_eval.Options.IsKnownShopt(n)) { Console.Error.WriteLine($"bash: shopt: {n}: invalid shell option name"); rc = 1; continue; }
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
		var items = args.Where(a => a != "-p" && a != "--").ToList();
		if (items.Count == 0)
			{
			foreach (var kv in aliases.OrderBy(k => k.Key, StringComparer.Ordinal))
				Console.WriteLine($"alias {kv.Key}='{kv.Value.Replace("'", "'\\''")}'");
			return 0;
			}
		int rc = 0;
		foreach (var a in items)
			{
			int eq = a.IndexOf('=');
			if (eq > 0) { aliases[a[..eq]] = a[(eq + 1)..]; continue; }
			if (aliases.TryGetValue(a, out var v)) Console.WriteLine($"alias {a}='{v.Replace("'", "'\\''")}'");
			else { Console.Error.WriteLine($"bash: alias: {a}: not found"); rc = 1; }
			}
		return rc;
		}

	private int Unalias(List<string> args)
		{
		var aliases = _eval.Aliases;
		int rc = 0;
		foreach (var a in args)
			{
			if (a == "-a") { aliases.Clear(); continue; }
			if (a == "--") continue;
			if (!aliases.Remove(a)) { Console.Error.WriteLine($"bash: unalias: {a}: not found"); rc = 1; }
			}
		return rc;
		}

	// ── shift / read / mapfile / getopts ──────────────────────────────────────

	private int Shift(List<string> args)
		{
		int n = 1;
		if (args.Count > 0 && !int.TryParse(args[0], out n))
			{ Console.Error.WriteLine($"bash: shift: {args[0]}: numeric argument required"); return 1; }
		var pos = _env.GetPositionals();
		if (n < 0 || n > pos.Count)
			{
			if (_eval.Options.Shopt("shift_verbose")) Console.Error.WriteLine("bash: shift: shift count out of range");
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
					default: Console.Error.WriteLine($"bash: read: -{o}: invalid option"); return 2;
					}
				}
			}
		for (; i < args.Count; i++) vars.Add(args[i]);

		if (prompt is not null && !Console.IsInputRedirected) Console.Error.Write(prompt);

		TextReader input = Console.In;
		if (fd > 2)
			{
			var s = _eval.GetFd(fd);
			if (s is null) { Console.Error.WriteLine($"bash: read: {fd}: invalid file descriptor: Bad file descriptor"); return 1; }
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
		var line = sb.ToString();
		if (delim == "\n" && line.EndsWith('\r')) line = line[..^1];

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
					if (a.StartsWith('-') && a.Length > 1) { Console.Error.WriteLine($"bash: mapfile: {a}: invalid option"); return 2; }
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
		if (trim && delim == "\n") selected = selected.Select(l => l.EndsWith('\r') ? l[..^1] : l).ToList();
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
		if (args.Count < 2) { Console.Error.WriteLine("bash: getopts: usage: getopts optstring name [arg ...]"); return 2; }
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
		if (args.Count == 0) { Console.Error.WriteLine("bash: source: filename argument required"); return 2; }
		var file = args[0];
		var path = ShellEnvironment.TranslatePath(file);
		if (!File.Exists(path) && !file.Contains('/') && !file.Contains('\\') && _eval.Options.Shopt("sourcepath"))
			{
			// bash searches PATH for a bare filename
			foreach (var d in (Environment.GetEnvironmentVariable("PATH") ?? "").Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
				{
				var cand = Path.Combine(ShellEnvironment.TranslatePath(d), file);
				if (File.Exists(cand)) { path = cand; break; }
				}
			}
		if (!File.Exists(path)) { Console.Error.WriteLine($"bash: {file}: No such file or directory"); return 1; }
		return _eval.SourceFile(path, silentIfMissing: false, positionals: args.Count > 1 ? args.Skip(1) : null);
		}

	private int DoEval(List<string> args)
		{
		var src = string.Join(" ", args);
		if (src.Trim().Length == 0) return 0;
		return _eval.RunString(src, origin: "eval", syntaxErrorCode: 1);
		}

	// ── declare / local / typeset ─────────────────────────────────────────────

	private int Local(List<string> args)
		{
		if (_env.ScopeDepth <= 1) { Console.Error.WriteLine("bash: local: can only be used in a function"); return 1; }
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

	/// <summary>Print a function definition in bash's canonical shape.</summary>
	private static string FunctionText(Parser.FunctionDef fd)
		{
		if (fd.Source is null) return $"{fd.Name} () {{ :; }}";
		var src = fd.Source;
		// normalise "function name" / "name()" headers to "name () " + body on its own line
		var body = src;
		if (body.StartsWith("function ")) body = body["function ".Length..].TrimStart();
		if (body.StartsWith(fd.Name)) body = body[fd.Name.Length..].TrimStart();
		if (body.StartsWith("()")) body = body[2..].TrimStart();
		return $"{fd.Name} () \n{body}";
		}

	/// <summary>Assign `name=(…)` from its literal text: values are lexed and expanded as words.</summary>
	private void AssignArrayLiteral(string name, string literal, bool assoc, bool append)
		{
		var inner = literal[1..^1];
		var words = Parser.Parser.ParseWords(inner);
		bool keyed = words.Count > 0 && words.All(w => w.Parts.Count > 0 && w.Parts[0] is Parser.LiteralPart lp && lp.Value.StartsWith('[') && lp.Value.Contains("]="));
		if (assoc || _env.IsAssoc(name) || (keyed && !_env.IsArray(name) && assoc))
			{
			if (!append) _env.DeclareAssoc(name);
			foreach (var w in words)
				{
				var text = _eval.Expander.ExpandToString(w);
				int close = text.IndexOf("]=", StringComparison.Ordinal);
				if (text.StartsWith('[') && close > 0) _env.SetAssocElement(name, text[1..close], text[(close + 2)..]);
				}
			return;
			}
		if (keyed)
			{
			if (!append) _env.SetArrayFromList(name, []);
			foreach (var w in words)
				{
				var text = _eval.Expander.ExpandToString(w);
				int close = text.IndexOf("]=", StringComparison.Ordinal);
				_env.SetArrayElement(name, (int)_eval.Expander.EvalArithmetic(text[1..close]), text[(close + 2)..]);
				}
			return;
			}
		var values = words.SelectMany(_eval.Expander.ExpandToFields).ToList();
		if (append) _env.AppendArrayFromList(name, values); else _env.SetArrayFromList(name, values);
		}

	private int Declare(List<string> args, bool local)
		{
		bool export = false, unexport = false, array = false, assoc = false, integer = false, unInteger = false,
		     ro = false, global = false, funcs = false, funcNames = false, print = false, nameref = false;
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
						case 'f': funcs = true; break;
						case 'F': funcNames = true; break;
						case 'p': print = true; break;
						case 'n': nameref = true; break;
						case 'l': case 'u': case 't': case 'I': case 'c': break;   // accepted, no effect
						default: Console.Error.WriteLine($"bash: declare: -{c}: invalid option"); return 2;
						}
					}
				continue;
				}
			specs.Add(arg);
			}
		if (nameref) { Console.Error.WriteLine("bash: declare: -n (nameref) is not supported by this interpreter"); return 2; }

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
				if (!_env.IsSet(n) && !_env.IsArray(n) && !_env.IsAssoc(n))
					{ if (specs.Count > 0) { Console.Error.WriteLine($"bash: declare: {n}: not found"); prc = 1; } continue; }
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
			if (!IsIdentifier(name)) { Console.Error.WriteLine($"bash: declare: `{spec}': not a valid identifier"); rc = 1; continue; }

			if (assoc) _env.DeclareAssoc(name);
			else if (array) _env.DeclareArray(name);
			if (integer) _env.MarkInteger(name, true);
			if (unInteger) _env.MarkInteger(name, false);

			if (val is not null)
				{
				if (sub is not null) AssignVar($"{name}[{sub}]", val);
				else if (val.StartsWith('(') && val.EndsWith(')')) AssignArrayLiteral(name, val, assoc, append);
				else if (append) _env.Append(name, val);
				else if (local && !global) _env.SetLocal(name, val);
				else if (global) _env.SetGlobal(name, val);
				else _env.Set(name, val);
				}
			else if (local && !global && !_env.IsArray(name) && !_env.IsAssoc(name))
				{
				// `local x` declares x in this scope (empty) without touching the outer value
				_env.SetLocal(name, "");
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
						default: Console.Error.WriteLine($"bash: type: -{c}: invalid option"); return 2;
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
				var p = n.Contains('/') || n.Contains('\\') ? (File.Exists(ShellEnvironment.TranslatePath(n)) ? n : null) : _eval.ResolveOnPath(n);
				if (p is null) rc = 1; else Console.WriteLine(p);
				continue;
				}
			var c = _eval.Classify(n, skipFunctions: noFuncs);
			if (c is null)
				{
				if (!typeOnly && !pathIfFile) Console.Error.WriteLine($"bash: type: {n}: not found");
				rc = 1; continue;
				}
			var (kind, path) = c.Value;
			if (pathIfFile) { if (kind == "file") Console.WriteLine(path); continue; }
			if (typeOnly) { Console.WriteLine(kind); continue; }
			switch (kind)
				{
				case "alias":    Console.WriteLine($"{n} is aliased to `{_eval.Aliases[n]}'"); break;
				case "keyword":  Console.WriteLine($"{n} is a shell keyword"); break;
				case "function": Console.WriteLine($"{n} is a function"); Console.WriteLine(FunctionText(_eval.GetFunction(n)!)); break;
				case "builtin":  Console.WriteLine($"{n} is a shell builtin"); break;
				case "file":     Console.WriteLine($"{n} is {path}"); break;
				}
			if (all && kind != "file")
				{
				var p = _eval.ResolveOnPath(n);
				if (p is not null) Console.WriteLine($"{n} is {p}");
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
					else { Console.Error.WriteLine($"bash: command: -{c}: invalid option"); return 2; }
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
		if (args.Count == 0 || args[^1] != "]") { Console.Error.WriteLine("bash: [: missing `]'"); return 2; }
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
		catch (EvalException ex) { Console.Error.WriteLine($"bash: {ex.Message}"); return false; }
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
			if (!int.TryParse(args[0], out code)) { Console.Error.WriteLine($"bash: exit: {args[0]}: numeric argument required"); code = 2; }
			code &= 0xFF;
			}
		throw new ExitException(code);
		}

	private int DoReturn(List<string> args)
		{
		int code = _env.LastExitCode;
		if (args.Count > 0)
			{
			if (!int.TryParse(args[0], out code)) { Console.Error.WriteLine($"bash: return: {args[0]}: numeric argument required"); code = 2; }
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
		if (args.Count == 0) { Console.Error.WriteLine("bash: let: expression expected"); return 1; }
		long last = 0;
		foreach (var a in args) last = _eval.Expander.EvalArithmetic(a);
		return last != 0 ? 0 : 1;
		}

	// ── pushd / popd / dirs ───────────────────────────────────────────────────

	private readonly List<string> _dirStack = [];

	private string DisplayDir(string d)
		{
		var home = _env.Get("HOME");
		if (home.Length > 0 && d.StartsWith(home, StringComparison.OrdinalIgnoreCase))
			return "~" + d[home.Length..];
		return d;
		}

	private void PrintDirs(bool numbered)
		{
		var all = new List<string> { ShellEnvironment.ShellCwd() };
		all.AddRange(_dirStack);
		if (numbered) for (int i = 0; i < all.Count; i++) Console.WriteLine($" {i}  {DisplayDir(all[i])}");
		else Console.WriteLine(string.Join(" ", all.Select(DisplayDir)));
		}

	private int Pushd(List<string> args)
		{
		bool quiet = false; string? target = null;
		foreach (var a in args) { if (a == "-n") quiet = true; else target ??= a; }
		var cur = Directory.GetCurrentDirectory();
		if (target is null)
			{
			if (_dirStack.Count == 0) { Console.Error.WriteLine("bash: pushd: no other directory"); return 1; }
			var top = _dirStack[0];
			if (Cd([top]) != 0) return 1;
			_dirStack[0] = cur;
			}
		else
			{
			if (Cd([target]) != 0) return 1;
			_dirStack.Insert(0, cur);
			}
		if (!quiet) PrintDirs(false);
		return 0;
		}

	private int Popd(List<string> args)
		{
		bool quiet = args.Contains("-n");
		if (_dirStack.Count == 0) { Console.Error.WriteLine("bash: popd: directory stack empty"); return 1; }
		var top = _dirStack[0];
		_dirStack.RemoveAt(0);
		if (Cd([top]) != 0) return 1;
		if (!quiet) PrintDirs(false);
		return 0;
		}

	private int Dirs(List<string> args)
		{
		if (args.Contains("-c")) { _dirStack.Clear(); return 0; }
		PrintDirs(args.Contains("-v"));
		return 0;
		}

	// ── kill ──────────────────────────────────────────────────────────────────

	private int Kill(List<string> args)
		{
		bool sigZero = false, list = false;
		var targets = new List<string>();
		for (int i = 0; i < args.Count; i++)
			{
			var a = args[i];
			if (a == "-l" || a == "-L") { list = true; continue; }
			if (a == "-0") { sigZero = true; continue; }
			if (a == "--") { targets.AddRange(args.Skip(i + 1)); break; }
			if ((a == "-s" || a == "-n") && i + 1 < args.Count) { var s = args[++i]; if (s is "0") sigZero = true; continue; }
			if (a.StartsWith('-') && a.Length > 1 && !int.TryParse(a, out _)) continue;   // -TERM, -KILL, -9: all forceful here
			if (a.StartsWith('-') && int.TryParse(a[1..], out var num)) { if (num == 0) sigZero = true; continue; }
			targets.Add(a);
			}
		if (list)
			{
			Console.WriteLine(string.Join("  ", SignalNames.Where(s => s != "EXIT" && s != "DEBUG" && s != "ERR" && s != "RETURN")));
			return 0;
			}
		if (targets.Count == 0) { Console.Error.WriteLine("bash: kill: usage: kill [-s sigspec | -n signum | -sigspec] pid | jobspec ... or kill -l [sigspec]"); return 2; }
		int rc = 0;
		foreach (var t in targets)
			{
			if (t.StartsWith('%') || (int.TryParse(t, out var maybe) && BackgroundJob.IsSyntheticPid(maybe)))
				{
				if (sigZero) continue;
				if (_eval.KillJob(t, out var err) != 0) { Console.Error.WriteLine($"bash: kill: {err}"); rc = 1; }
				continue;
				}
			if (!int.TryParse(t, out var pid)) { Console.Error.WriteLine($"bash: kill: {t}: arguments must be process or job IDs"); rc = 1; continue; }
			try
				{
				var proc = System.Diagnostics.Process.GetProcessById(pid);
				if (!sigZero) proc.Kill(true);
				}
			catch (ArgumentException) { Console.Error.WriteLine($"bash: kill: ({pid}) - No such process"); rc = 1; }
			catch (Exception ex)      { Console.Error.WriteLine($"bash: kill: ({pid}) - {ex.Message}"); rc = 1; }
			}
		return rc;
		}

	// ── env ───────────────────────────────────────────────────────────────────

	private int Env(List<string> args)
		{
		bool clear = false;
		var unset = new List<string>();
		var assigns = new List<(string, string)>();
		int i = 0;
		for (; i < args.Count; i++)
			{
			var a = args[i];
			if (a is "-i" or "--ignore-environment") clear = true;
			else if (a == "-u" && i + 1 < args.Count) unset.Add(args[++i]);
			else if (a == "-0") { }
			else if (a == "--") { i++; break; }
			else if (a.StartsWith('-') && a.Length > 1) { Console.Error.WriteLine($"env: invalid option -- '{a[1..]}'"); return 125; }
			else break;
			}
		for (; i < args.Count; i++)
			{
			var a = args[i];
			int eq = a.IndexOf('=');
			if (eq > 0 && IsIdentifier(a[..eq])) assigns.Add((a[..eq], a[(eq + 1)..]));
			else break;
			}
		if (i >= args.Count)
			{
			var env = clear ? new Dictionary<string, string>(StringComparer.Ordinal) : _env.GetExportedVars();
			foreach (var u in unset) env.Remove(u);
			foreach (var (k, v) in assigns) env[k] = v;
			foreach (var (k, v) in env) Console.WriteLine($"{k}={v}");
			return 0;
			}
		var name = args[i];
		var rest = args.Skip(i + 1).ToList();
		return _eval.RunCommand(name, rest, false, assigns, clear);
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
		// Sleep in small slices so Ctrl+C (SIGINT) can interrupt a long sleep.
		double remainingMs = total * 1000;
		while (remainingMs > 0)
			{
			_eval.CheckInterrupt();
			int slice = (int)Math.Min(50, remainingMs);
			Thread.Sleep(slice);
			remainingMs -= slice;
			}
		return 0;
		}

	// ── PATH lookup (used by which-style helpers elsewhere in this class) ──────

	private static string? FindInPath(string name)
		{
		var path = System.Environment.GetEnvironmentVariable("PATH") ?? "";
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
	}
