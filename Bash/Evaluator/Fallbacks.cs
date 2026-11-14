namespace Bash.Evaluator;

/// <summary>
/// Where a command C#Bash does not provide in-process is looked for (DECISIONS 2026-11-11, the architect's rulings
/// and "go"): the steps of `unix-tools` in `%USERPROFILE%/.bash/etc/csharpBash.config`, in the user's order. A step is
/// a folder of Unix tools, `PATH` (the shell's PATH), or `WSL` (the tool run in WSL, when nothing else has it). A step
/// left out is not used; with no file, or no `unix-tools` line, the order is `PATH;WSL`.
///
/// The file is read at most once per process, at the first lookup that needs it, never at startup (the architect:
/// "it is only consulted once on first need for a fallback"); an edit reaches the next process. A malformed line
/// warns once on stderr and its step is skipped: never fatal.
///
/// The folders and PATH make ONE search list, in the configured order, for every lookup (running, `type -a`,
/// `command -v`, `hash`, a builtin's hand-over): a folder holds files, so it is seen as PATH's are. WSL is never
/// reported by them (it has no file to name), as a not-found handler is not in bash. The search list also keys the
/// remembered lookups (CommandMap), so a changed folder list is a fresh map and nothing stale is used.
/// </summary>
internal static class Fallbacks
	{
	public const string FileName = "csharpBash.config";
	private const string PathStep = "PATH", WslStep = "WSL";

	private static readonly object _lock = new();
	private static bool _loaded;
	private static List<string> _steps = [PathStep, WslStep];   // folders (Windows form), PATH, WSL
	private static bool _hasFolder;
	private static string? _composedFor, _composed;

	/// <summary>The configuration file: %USERPROFILE%/.bash/etc/csharpBash.config (the ruled ~/.bash, beside bin/).</summary>
	public static string? ConfigFile => Unserviced.Folder is { } dir ? Path.Combine(dir, "etc", FileName) : null;

	private static void Load()
		{
		lock (_lock)
			{
			if (_loaded) return;
			_loaded = true;
			string[] lines;
			var file = ConfigFile;
			try { if (file is null || !File.Exists(file)) return; lines = File.ReadAllLines(file); }
			catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { Warn($"cannot be read: {ex.Message}"); return; }
			foreach (var raw in lines)
				{
				var line = raw.Trim();
				if (line.Length == 0 || line[0] == '#') continue;
				int eq = line.IndexOf('=');
				var key = eq < 0 ? line : line[..eq].Trim();
				if (eq < 0 || key != "unix-tools") { Warn($"unknown line '{line}' (ignored)"); continue; }
				var steps = new List<string>();
				foreach (var s in line[(eq + 1)..].Split(';'))
					{
					var step = s.Trim();
					if (step.Length == 0) continue;
					if (step.Equals(PathStep, StringComparison.OrdinalIgnoreCase)) { steps.Add(PathStep); continue; }
					if (step.Equals(WslStep, StringComparison.OrdinalIgnoreCase)) { steps.Add(WslStep); continue; }
					var dir = ShellEnvironment.TranslateForm(ShellEnvironment.TranslatePath(step));
					if (!Path.IsPathFullyQualified(dir) || !Directory.Exists(dir)) { Warn($"unix-tools: no folder '{step}' (skipped)"); continue; }
					steps.Add(dir);
					}
				_steps = steps;
				}
			_hasFolder = _steps.Any(s => s is not PathStep and not WslStep);
			}
		}

	private static void Warn(string what)
		{
		try { Console.Error.WriteLine($"C#Bash: {ShellEnvironment.ToShellPath(ConfigFile ?? FileName)}: {what}"); } catch { }
		}

	/// <summary>The search list for a command not provided in-process: the configured folders and <paramref name="path"/>
	/// in the configured order. With no folder it is <paramref name="path"/> itself (the same string, so lookups and
	/// remembered maps are as before), or empty when PATH is left out.</summary>
	public static string SearchPath(string path)
		{
		Load();
		if (!_hasFolder) return _steps.Contains(PathStep) ? path : "";
		lock (_lock)
			{
			if (ReferenceEquals(path, _composedFor) || path == _composedFor) return _composed!;
			var parts = _steps.Where(s => s != WslStep).Select(s => s == PathStep ? path : s).Where(s => s.Length > 0);
			_composed = string.Join(";", parts);
			_composedFor = path;
			return _composed;
			}
		}

	/// <summary>WSL's launcher when WSL is a step and Windows has one, else null.</summary>
	public static string? Wsl
		{
		get
			{
			Load();
			if (!_steps.Contains(WslStep)) return null;
			var exe = Path.Combine(Environment.SystemDirectory, "wsl.exe");
			return File.Exists(exe) ? exe.Replace('\\', '/') : null;
			}
		}

	/// <summary>An argument as WSL sees it: a Windows-rooted path (`F:/x`, `F:\x`, C#Bash's own form) as `/mnt/f/x`,
	/// also after a leading `--name=`; anything else as written. Not `/f/x`: that is also a sed address (`/a/d`).</summary>
	public static string ToWslArgument(string arg)
		{
		int eq = arg.StartsWith('-') ? arg.IndexOf('=') : -1;
		var head = eq < 0 ? "" : arg[..(eq + 1)];
		var value = eq < 0 ? arg : arg[(eq + 1)..];
		if (value.Length >= 3 && char.IsAsciiLetter(value[0]) && value[1] == ':' && value[2] is '/' or '\\')
			return head + "/mnt/" + char.ToLowerInvariant(value[0]) + "/" + value[3..].Replace('\\', '/');
		return arg;
		}
	}
