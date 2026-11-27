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
				if (eq > 0 && key == "access-time")
					{
					// `access-time = default | preserve` (the architect, 2026-11-14): whether reading a file's details (stat,
					// ls -l) leaves its access time alone; default is Windows' and Git Bash's (it moves)
					var val = line[(eq + 1)..].Trim();
					if (val == "preserve") _preserveAccessTime = true;
					else if (val == "default") _preserveAccessTime = false;
					else Warn($"access-time: '{val}' is not default or preserve (default kept)");
					continue;
					}
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

	/// <summary>The search list for a command not provided in-process: this shell's own folder FIRST, always, then the
	/// configured steps in their order (the folders, and PATH without this shell's folder). The steps are fallbacks (the
	/// architect, 2026-11-11 and 2026-11-24: unix-tools is "the 'fallback' _only_ location"), and a nested bash or sh is
	/// this shell (USING's startup step 3): a folder named before PATH was searched ahead of this shell's own folder, so
	/// `bash -c` ran the folder's bash (bugs list 7), and so did a PATH a script had reordered (`PATH=/x:$PATH`, /x
	/// holding a bash). Off PATH too: OwnDir is prepended here since 956; before 956 a name the list missed still reached this folder through
	/// FallbackProgram (the own folder, then System32 and the Windows folder), so "off PATH, not searched" was never so. When this shell's folder
	/// leads PATH and no folder is configured, as every start leaves it, it is <paramref name="path"/> itself (the same
	/// string, so lookups and remembered maps are as before).</summary>
	public static string SearchPath(string path)
		{
		Load();
		bool hasPath = _steps.Contains(PathStep);
		lock (_lock)
			{
			if (ReferenceEquals(path, _composedFor) || path == _composedFor) return _composed!;
			var entries = ShellEnvironment.PathDirs(path);
			string composed;
			if (!_hasFolder && hasPath && entries.Count > 0 && IsOwnDir(entries[0])) composed = path;
			else
				{
				string rest = string.Join(";", entries.Where(e => !IsOwnDir(e)));
				composed = string.Join(";", _steps.Where(s => s != WslStep).Select(s => s == PathStep ? rest : s).Prepend(OwnDir).Where(s => s.Length > 0));
				}
			_composed = composed;
			_composedFor = path;
			return composed;
			}
		}

	/// <summary>This shell's own folder, the running program's, in the shell's form.</summary>
	private static string OwnDir => s_ownDir ??= ShellEnvironment.ToShellPath(AppContext.BaseDirectory.TrimEnd('\\', '/'));
	private static string? s_ownDir;

	/// <summary>Whether a PATH entry (either form) is this shell's own folder, the running program's.</summary>
	private static bool IsOwnDir(string entry)
		{
		static string Key(string p) => p.Replace('/', '\\').TrimEnd('\\');
		s_ownKey ??= Key(AppContext.BaseDirectory);
		return s_ownKey.Length > 0 && string.Equals(Key(ShellEnvironment.TranslateForm(entry)), s_ownKey, StringComparison.OrdinalIgnoreCase);
		}
	private static string? s_ownKey;

	private static bool _preserveAccessTime;

	/// <summary>`access-time = preserve`: reading a file's details leaves its access time alone (ShellFile.OpenForDetails).
	/// Read with the rest of the file, at its first need.</summary>
	public static bool PreserveAccessTime { get { Load(); return _preserveAccessTime; } }

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
