namespace Bash.Evaluator;

/// <summary>
/// `help`, every builtin's `--help`, and `enable`. A help request answers with the manual page(s) of what was asked,
/// looked up in %USERPROFILE%/.bash/help.json (the architect, 2026-10-27: "Any help request should point requester to
/// the specific GNU help page(s) pertaining to their request ... via a json file containing the lookup table stored in
/// %USERPROFILE%/.bash"). The default table is built in (Bash/help.json) and written there when the file is missing;
/// the file answers for every name it has, the built-in table for a name it lacks (a later build's new builtins). A
/// topic C#Bash does not implement says so beside its page, and that request is logged (<see cref="Unserviced"/>), as
/// is a request that matches no topic. Until then `help` ran Windows' help.exe (no error, Windows' answer), `X --help`
/// ran the PATH program's help (another program than the builtin that runs), and `enable` was "not found".
/// </summary>
public sealed partial class Builtins
	{
	/// <summary>bash 5.1's builtins, in its order (`compgen -b`).</summary>
	public static readonly string[] BashBuiltinNames =
		[
		".", ":", "[", "alias", "bg", "bind", "break", "builtin", "caller", "cd", "command", "compgen", "complete", "compopt",
		"continue", "declare", "dirs", "disown", "echo", "enable", "eval", "exec", "exit", "export", "false", "fc", "fg",
		"getopts", "hash", "help", "history", "jobs", "kill", "let", "local", "logout", "mapfile", "popd", "printf",
		"pushd", "pwd", "read", "readarray", "readonly", "return", "set", "shift", "shopt", "source", "suspend", "test",
		"times", "trap", "true", "type", "typeset", "ulimit", "umask", "unalias", "unset", "wait",
		];

	/// <summary>A bash builtin C#Bash lacks, or `select` / `coproc` (bash words it does not run).</summary>
	public static bool NotImplemented(string topic) =>
		topic is "select" or "coproc" || (Array.IndexOf(BashBuiltinNames, topic) >= 0 && !Has(topic));

	/// <summary>bash builtins for which `--help` is an ordinary argument (`echo --help` prints it; measured, bash 5.1).</summary>
	private static readonly HashSet<string> s_helpIsAnArgument = [":", "[", "echo", "false", "test", "true"];

	/// <summary>True when <paramref name="args"/> asks builtin <paramref name="name"/> for its help as bash does: `--help`
	/// first. Met later among a tool's options it comes as a <see cref="HelpRequestedException"/> from Opts.</summary>
	private static bool AsksForHelp(string name, List<string> args) =>
		args.Count > 0 && args[0] == "--help" && !s_helpIsAnArgument.Contains(name);

	private const string BashManual = "https://www.gnu.org/software/bash/manual/html_node/Shell-Builtin-Commands.html";

	private int Help(List<string> args)
		{
		int i = 0;
		for (; i < args.Count && args[i].Length > 1 && args[i][0] == '-'; i++)
			{
			if (args[i] == "--") { i++; break; }
			// -d -m -s choose bash's text; every form answers with the page here
			if (args[i].AsSpan(1).IndexOfAnyExcept("dms") >= 0)
				{
				Console.Error.WriteLine($"{Err}help: {args[i][..2]}: invalid option");
				Console.Error.WriteLine("help: usage: help [-dms] [pattern ...]");
				return 2;
				}
			}
		var table = HelpTable();
		if (i == args.Count)
			{
			Console.WriteLine($"bash: {BashManual}");
			Console.WriteLine("`help name' gives the page of each name that matches; help.json in %USERPROFILE%/.bash holds the table.");
			return 0;
			}
		bool found = false;
		string pattern = "";
		for (; i < args.Count; i++)
			{
			pattern = args[i];
			// bash's two passes (help.def): an exact name or a glob match; failing those, every name it begins
			var matches = table.Where(t => t.Topic == pattern || Glob.Match(pattern, t.Topic)).ToList();
			if (matches.Count == 0) matches = table.Where(t => t.Topic.StartsWith(pattern, StringComparison.Ordinal)).ToList();
			foreach (var (topic, urls) in matches) WriteHelp(topic, urls, args);
			found |= matches.Count > 0;
			}
		if (found) return 0;
		var message = $"help: no help topics match `{pattern}'.  Try `help help' or `man -k {pattern}' or `info {pattern}'.";
		Console.Error.WriteLine($"{Err}{message}");
		Unserviced.Log("help", pattern, message, args, "no help topic, exit 1");
		return 1;
		}

	/// <summary>`name --help`: the page(s) of that one name.</summary>
	private int HelpFor(string name, List<string> args)
		{
		var entry = HelpTable().FirstOrDefault(t => t.Topic == name);
		if (entry.Urls is null)
			{
			Console.Error.WriteLine($"{Err}{name}: no page for `{name}' in help.json");
			Unserviced.Log(name, "--help", $"{name}: no page in help.json", args, "no help topic, exit 1");
			return 1;
			}
		WriteHelp(name, entry.Urls, args);
		return 0;
		}

	private static void WriteHelp(string topic, List<string> urls, List<string> args)
		{
		bool missing = NotImplemented(topic);
		var label = missing ? $"{topic} (not implemented in C#Bash)" : topic;
		foreach (var url in urls) Console.WriteLine($"{label}: {url}");
		if (missing) Unserviced.Log("help", topic, $"{topic}: not implemented in C#Bash", args, "not implemented: answered with its page");
		}

	/// <summary>The SHA-256 of every earlier built-in table (each `hg cat` of `hg log -r 'file("Bash/help.json")'` but the
	/// current one). A file still byte-for-byte one of them was written by an earlier build and never edited, so it is
	/// replaced by this build's table (2026-10-29: rev 397's sent find and xargs to a findutils folder that is not
	/// online, and every profile written before kept it); an edited file is never touched. WHEN Bash/help.json CHANGES,
	/// ADD THE OUTGOING TABLE'S HASH HERE (DECISIONS 2026-10-29).</summary>
	private static readonly HashSet<string> EarlierHelpTables = new(StringComparer.Ordinal)
		{
		"70fcfdea5039e0a80fbe82b865299b81aa402218bbc9b7a6717bafc7b5364ef4",   // rev 397, 2026-10-27
		};

	/// <summary>The table, in the built-in order then the file's own names: the file's pages for every name it has, the
	/// built-in pages for the rest. The file is written from the built-in table when it is missing, and again when it is
	/// an earlier build's table unedited (<see cref="EarlierHelpTables"/>); one that does not parse is said once per
	/// request and the built-in table answers.</summary>
	private static List<(string Topic, List<string> Urls)> HelpTable()
		{
		var builtIn = BuiltInHelpJson();
		var table = Pages(Json.ParseOne(builtIn) as JObj);
		if (Unserviced.Folder is not { } dir) return table;
		var file = Path.Combine(dir, "help.json");
		try
			{
			if (!File.Exists(file)) { Directory.CreateDirectory(dir); File.WriteAllText(file, builtIn); return table; }
			var sum = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(file))).ToLowerInvariant();
			if (EarlierHelpTables.Contains(sum))
				{
				File.WriteAllText(file, builtIn);
				Console.Error.WriteLine($"help: {file}: an earlier build's table, unedited: replaced by this build's");
				return table;
				}
			var mine = Pages(Json.ParseOne(ShellEncoding.ReadAllText(file)) as JObj
			                 ?? throw new Json.ParseException("not a JSON object"));
			for (int k = 0; k < table.Count; k++)
				if (mine.FindIndex(t => t.Topic == table[k].Topic) is int at and >= 0) table[k] = mine[at];
			table.AddRange(mine.Where(t => !table.Any(b => b.Topic == t.Topic)));
			}
		catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or Json.ParseException)
			{
			Console.Error.WriteLine($"help: {file}: {ex.Message}; the built-in table answers");
			}
		return table;
		}

	/// <summary>A table's names whose value is an array of strings; any other value is not an entry.</summary>
	private static List<(string Topic, List<string> Urls)> Pages(JObj? o) =>
		o is null ? [] :
		[.. o.Pairs().Where(p => p.Value is List<object?> l && l.All(u => u is string))
		             .Select(p => (p.Key, ((List<object?>)p.Value!).Cast<string>().ToList()))];

	private static string BuiltInHelpJson()
		{
		using var s = typeof(Builtins).Assembly.GetManifestResourceStream("help.json")!;
		using var r = new StreamReader(s);
		return r.ReadToEnd();
		}

	/// <summary>bash's `enable`: list the builtins (all enabled; -a, -p, none: every one; -s: POSIX's special ones; -n:
	/// the disabled ones, none), or confirm names are builtins. Turning one off (-n NAME), loading (-f) and deleting (-d)
	/// one say they are not available, which logs them.</summary>
	private int Enable(List<string> args)
		{
		bool special = false, disabled = false;
		int i = 0;
		for (; i < args.Count && args[i].Length > 1 && args[i][0] == '-'; i++)
			{
			if (args[i] == "--") { i++; break; }
			foreach (var c in args[i].AsSpan(1))
				switch (c)
					{
					case 'a' or 'p': break;   // every builtin is enabled: -a, -p and no option list the same
					case 's': special = true; break;
					case 'n': disabled = true; break;
					case 'f' or 'd':
						foreach (var line in NotAvailable.Lines($"{Err}enable: ", c == 'f' ? "enable -f (loading a builtin)" : "enable -d (deleting a loaded builtin)"))
							Console.Error.WriteLine(line);
						return 2;
					default:
						Console.Error.WriteLine($"{Err}enable: -{c}: invalid option");
						Console.Error.WriteLine("enable: usage: enable [-a] [-dnps] [-f filename] [name ...]");
						return 2;
					}
			}
		var names = args.Skip(i).ToList();
		if (names.Count == 0)
			{
			if (disabled) return 0;
			var list = special ? s_posixSpecial.Where(Has) : Names.Distinct().Order(StringComparer.Ordinal);
			foreach (var n in list) Console.WriteLine($"enable {n}");
			return 0;
			}
		if (disabled)
			{
			foreach (var line in NotAvailable.Lines($"{Err}enable: ", "enable -n (turning a builtin off)")) Console.Error.WriteLine(line);
			return 2;
			}
		int rc = 0;
		foreach (var n in names)
			if (!Has(n)) { Console.Error.WriteLine($"{Err}enable: {n}: not a shell builtin"); rc = 1; }
		return rc;
		}

	/// <summary>POSIX's special builtins, as bash's `enable -s` lists them.</summary>
	private static readonly string[] s_posixSpecial =
		[".", ":", "break", "continue", "eval", "exec", "exit", "export", "readonly", "return", "set", "shift", "source", "times", "trap", "unset"];
	}
