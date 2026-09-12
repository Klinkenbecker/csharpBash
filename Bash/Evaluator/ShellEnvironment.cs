using System.Runtime.InteropServices;

namespace Bash.Evaluator;

/// <summary>
/// Holds shell variables, arrays, exports, attributes (readonly / integer), and the
/// positional-parameter / scope stack; resolves the special parameters; owns path
/// translation. Bash-standard variables (OSTYPE, HOSTNAME, PPID, UID, SHLVL, BASH,
/// BASH_VERSINFO, PWD, IFS) are set at construction.
/// </summary>
public sealed class ShellEnvironment
	{
	// Scalar variable frames (innermost = last)
	private readonly List<Dictionary<string, string>> _frames = [new()];

	// Arrays live at global scope only (bash arrays are not local by default
	// unless explicitly declared local — we keep it simple for now).
	// Key = array name, Value = index→value map.
	private Dictionary<string, SortedDictionary<int, string>>    _arrays = new(StringComparer.Ordinal);
	private Dictionary<string, Dictionary<string, string>>        _assocs = new(StringComparer.Ordinal);

	private HashSet<string> _exports   = new(StringComparer.Ordinal);
	private HashSet<string> _readonly  = new(StringComparer.Ordinal);
	private HashSet<string> _integers  = new(StringComparer.Ordinal);
	private readonly Stack<string[]> _positionals = new();
	private string _arg0 = System.Environment.GetCommandLineArgs()[0];
	private readonly Random _random = new();
	private readonly DateTime _start = DateTime.UtcNow;

	public int LastExitCode { get; set; } = 0;

	// ── hooks the evaluator fills in ────────────────────────────────────────────
	/// <summary>`$!` — pid of the most recent background job (0 = none).</summary>
	public int LastBackgroundPid { get; set; }
	/// <summary>`$LINENO` — line of the statement being executed.</summary>
	public int CurrentLine { get; set; }
	/// <summary>`$-` — the current option flags.</summary>
	public Func<string>? FlagsProvider { get; set; }
	/// <summary>Arithmetic evaluator used for `declare -i` assignments.</summary>
	public Func<string, long>? ArithEval { get; set; }
	/// <summary>`set -a`: export every variable on assignment.</summary>
	public bool AutoExport { get; set; }

	public ShellEnvironment()
		{
		_positionals.Push([]);
		foreach (System.Collections.DictionaryEntry e in System.Environment.GetEnvironmentVariables())
			{
			var key = e.Key?.ToString() ?? "";
			var val = e.Value?.ToString() ?? "";
			// Windows spells it "Path"; bash scripts (and Claude Code's snapshot) say PATH.
			// MSYS does the same upper-casing on import.
			if (key.Equals("PATH", StringComparison.OrdinalIgnoreCase)) key = "PATH";
			if (key.Length > 0)
				{
				_frames[0][key] = val;
				_exports.Add(key);
				}
			}

		AugmentPathWithGitTools();
		AppendOwnDirectoryToPath();

		if (Get("HOME").Length == 0)
			{
			var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
			Set("HOME", home); Export("HOME");
			}

		// bash-standard shell variables (not exported unless inherited)
		var exe = Environment.ProcessPath ?? "bash";
		SetIfUnset("OSTYPE",   "msys");                 // DECISIONS 2026-09-04 (1)
		SetIfUnset("MACHTYPE", "x86_64-pc-msys");
		SetIfUnset("HOSTTYPE", "x86_64");
		SetIfUnset("HOSTNAME", Environment.MachineName.ToLowerInvariant());
		SetIfUnset("IFS",      " \t\n");
		SetIfUnset("BASH",     exe);
		SetIfUnset("OPTIND",   "1");
		SetIfUnset("OPTERR",   "1");
		SetIfUnset("PS4",      "+ ");
		if (Get("SHELL").Length == 0) { Set("SHELL", exe); Export("SHELL"); }
		int shlvl = int.TryParse(Get("SHLVL"), out var lv) ? lv + 1 : 1;
		Set("SHLVL", shlvl.ToString()); Export("SHLVL");
		var uid = WindowsUid();
		Set("UID", uid.ToString()); Set("EUID", uid.ToString());
		_readonly.Add("UID"); _readonly.Add("EUID");
		Set("PPID", ParentPid().ToString()); _readonly.Add("PPID");
		Set("PWD", ShellCwd()); Export("PWD");
		SetArrayFromList("BASH_VERSINFO", ["5", "1", "0", "0", "release", "x86_64-pc-msys"]);
		_readonly.Add("BASH_VERSINFO");
		}

	private void SetIfUnset(string name, string value) { if (Get(name).Length == 0) Set(name, value); }

	// ── path translation ──────────────────────────────────────────────────────

	/// <summary>
	/// Translate a shell-side path to the form the filesystem APIs get. SYMMETRIC with
	/// <see cref="ToShellPath"/> (ratified 2026-09-12): the shell's path form is FORWARD SLASH
	/// in both directions, so a path that goes out through `pwd` and comes back in as an
	/// argument is the same string. `~`/`~/x` → the profile dir; `/c/x` (MSYS drive form) →
	/// `C:/x`; `/tmp[/x]` → the temp dir; and any backslashes in the input are normalised to
	/// forward slashes. .NET's file APIs accept forward slashes everywhere, so nothing needs the
	/// backslash form. `/dev/...` is left alone (the redirect layer interprets it). The general
	/// Unix root mapping (`/usr`, `/etc`) is deferred — see DECISIONS.md.
	/// </summary>
	public static string TranslatePath(string path)
		{
		if (path.Length == 0) return path;

		if (path == "~" || path.StartsWith("~/") || path.StartsWith("~\\"))
			{
			var home = ToShellPath(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile));
			return path.Length == 1 ? home : home + "/" + ToShellPath(path[2..]);
			}

		if (Path.DirectorySeparatorChar != '\\') return path;

		if (path.StartsWith("/dev/")) return path;

		// /c, /c/, /c/Users/...  (drive letter, any case) → C:/Users/...
		if (path.Length >= 2 && path[0] == '/' && char.IsAsciiLetter(path[1])
		    && (path.Length == 2 || path[2] is '/' or '\\'))
			{
			var drive = char.ToUpperInvariant(path[1]) + ":/";
			return path.Length <= 3 ? drive : drive + ToShellPath(path[3..]);
			}

		// /tmp, /tmp/...
		if (path == "/tmp" || path.StartsWith("/tmp/") || path.StartsWith("/tmp\\"))
			{
			var temp = ToShellPath(Path.TrimEndingDirectorySeparator(Path.GetTempPath()));
			return path.Length == 4 ? temp : temp + "/" + ToShellPath(path[5..]);
			}

		return ToShellPath(path);
		}

	/// <summary>
	/// Convert a native Windows path into the forward-slash form the SHELL LANGUAGE is safe with,
	/// for any path the shell hands back to a script (`pwd`, `$PWD`, `dirs`, `mktemp -d`,
	/// `realpath`, `readlink`). Ratified 2026-09-11 by the architect, refining decision 3 of
	/// 2026-09-04 (which said "native Windows form" on the strength of Claude Code's cwd
	/// read-back alone):
	///
	/// backslash is an ESCAPE CHARACTER in bash, so `PWD=E:\Claude\Installer` is a value the
	/// shell's own operations misread — `${p#pat}`, globs, `[[ ]]`, `case` and every later
	/// expansion treat those backslashes as escapes. `E:/Claude/Installer` is equally valid to
	/// every Windows API and native tool, and contains no escape characters, so it satisfies both
	/// sides. `\\host\share\x` becomes `//host/share/x` (the leading pair is preserved by the
	/// straight character swap), and a bare root `E:\` becomes `E:/`.
	/// </summary>
	public static string ToShellPath(string path) => path.Replace('\\', '/');

	/// <summary>`Path.GetFullPath` in shell form — .NET hands back backslashes whatever it was
	/// given, so every absolute path the shell computes is normalised here (2026-09-12).</summary>
	public static string FullPath(string path) => ToShellPath(Path.GetFullPath(path));

	/// <summary>The working directory in shell form — what `pwd` prints and `$PWD` holds.</summary>
	public static string ShellCwd() => ToShellPath(Directory.GetCurrentDirectory());

	// ── scalar variables ──────────────────────────────────────────────────────

	public string Get(string name)
		{
		// An ordinary lower-case/underscore name is never a special or dynamic parameter and
		// never a positional: straight to the frames. Measured 2026-09-04 (A/B, 3 runs each):
		// loop 425–435 vs 431–455 ns/it — marginal, ~10 ns, kept because it is free and exact.
		if (name.Length > 0 && ((name[0] >= 'a' && name[0] <= 'z') || name[0] == '_')) goto frames;
		switch (name)
			{
			case "?":  return LastExitCode.ToString();
			case "$":  return Environment.ProcessId.ToString();
			case "!":  return LastBackgroundPid == 0 ? "" : LastBackgroundPid.ToString();
			case "-":  return FlagsProvider?.Invoke() ?? "hB";
			case "#":  return (_positionals.TryPeek(out var p) ? p.Length : 0).ToString();
			case "@":  return string.Join(" ", _positionals.TryPeek(out var pa) ? pa : []);
			case "*":  return string.Join(" ", _positionals.TryPeek(out var ps) ? ps : []);
			case "0":  return _arg0;
			case "RANDOM":  return _random.Next(0, 32768).ToString();
			case "SRANDOM": return ((uint)_random.NextInt64()).ToString();
			case "SECONDS": return ((long)(DateTime.UtcNow - _start).TotalSeconds).ToString();
			case "LINENO":  return CurrentLine.ToString();
			case "BASHPID": return Environment.ProcessId.ToString();
			case "EPOCHSECONDS":  return DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();
			case "EPOCHREALTIME": return (DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() / 1000.0).ToString("F6", System.Globalization.CultureInfo.InvariantCulture);
			}

		if (int.TryParse(name, out int idx))
			{
			var pos = _positionals.TryPeek(out var pp) ? pp : [];
			return idx > 0 && idx <= pos.Length ? pos[idx - 1] : "";
			}

		frames:
		for (int i = _frames.Count - 1; i >= 0; i--)
			if (_frames[i].TryGetValue(name, out var val))
				return val;

		return "";
		}

	/// <summary>True if the scalar is set in any frame (for `[[ -v x ]]` / `${x+y}`).</summary>
	public bool IsSet(string name)
		{
		if (name.Length == 1 && "?$!-#@*0".IndexOf(name[0]) >= 0) return true;
		if (name is "RANDOM" or "SECONDS" or "LINENO" or "BASHPID" or "EPOCHSECONDS" or "EPOCHREALTIME" or "SRANDOM") return true;
		if (int.TryParse(name, out int idx))
			{
			var pos = _positionals.TryPeek(out var pp) ? pp : [];
			return idx > 0 && idx <= pos.Length;
			}
		for (int i = _frames.Count - 1; i >= 0; i--)
			if (_frames[i].ContainsKey(name)) return true;
		return _arrays.ContainsKey(name) || _assocs.ContainsKey(name);
		}

	/// <summary>A PATH written in MSYS form (`/c/x:/usr/bin:…`, colon-separated, entries
	/// rooted at `/`) — as a Git Bash snapshot or `export PATH=/c/foo:$PATH` produces — is
	/// converted to the native `C:\x;…` form so lookups and child processes keep working.</summary>
	private static string NormalizePath(string value)
		{
		if (value.Contains(';') || !value.Contains(':')) return value;
		var parts = value.Split(':');
		if (!parts.All(p => p.Length == 0 || p.StartsWith('/') || p.Contains('\n'))) return value;
		return string.Join(";", parts.Where(p => p.Length > 0 && !p.Contains('\n')).Select(TranslatePath));
		}

	public void Set(string name, string value)
		{
		if (_readonly.Contains(name))
			throw new FatalShellException($"{name}: readonly variable");
		if (_integers.Contains(name) && ArithEval is not null)
			{
			try { value = ArithEval(value).ToString(); } catch (EvalException) { value = "0"; }
			}
		if (name == "PATH")
			{
			value = NormalizePath(value);
			// the shell's own command lookup (ResolveOnPath/FindInPath) reads the process
			// environment, so `export PATH=…` must reach it or new directories are invisible
			try { Environment.SetEnvironmentVariable("PATH", value); } catch { }
			}
		for (int i = _frames.Count - 1; i >= 0; i--)
			if (_frames[i].ContainsKey(name))
				{ _frames[i][name] = value; if (AutoExport) _exports.Add(name); return; }
		_frames[^1][name] = value;
		if (AutoExport) _exports.Add(name);
		}

	/// <summary>Set in the innermost frame only (implements 'local').</summary>
	public void SetLocal(string name, string value)
		{
		if (_readonly.Contains(name)) throw new FatalShellException($"{name}: readonly variable");
		_frames[^1][name] = value;
		}

	/// <summary>Set in the outermost (global) frame (implements 'declare -g').</summary>
	public void SetGlobal(string name, string value)
		{
		if (_readonly.Contains(name)) throw new FatalShellException($"{name}: readonly variable");
		_frames[0][name] = value;
		}

	/// <summary>Append to a scalar (var+=value); integers add arithmetically.</summary>
	public void Append(string name, string value)
		{
		if (_integers.Contains(name) && ArithEval is not null)
			{
			long cur = long.TryParse(Get(name), out var c) ? c : 0;
			long add; try { add = ArithEval(value); } catch (EvalException) { add = 0; }
			Set(name, (cur + add).ToString());
			return;
			}
		Set(name, Get(name) + value);
		}

	/// <summary>Unset a variable or array. Returns false (and does nothing) for a readonly name.</summary>
	public bool Unset(string name)
		{
		if (_readonly.Contains(name)) return false;
		foreach (var f in _frames)
			f.Remove(name);
		_arrays.Remove(name);
		_assocs.Remove(name);
		_integers.Remove(name);
		_exports.Remove(name);
		return true;
		}

	public void Export(string name) => _exports.Add(name);
	public void Unexport(string name) => _exports.Remove(name);
	public bool IsExported(string name) => _exports.Contains(name);

	public void MarkReadonly(string name) => _readonly.Add(name);
	public bool IsReadonly(string name) => _readonly.Contains(name);
	public IEnumerable<string> ReadonlyNames => _readonly;

	public void MarkInteger(string name, bool on) { if (on) _integers.Add(name); else _integers.Remove(name); }
	public bool IsInteger(string name) => _integers.Contains(name);

	/// <summary>All scalar variable names across scopes plus array names (for tab completion / `set`).</summary>
	public IEnumerable<string> VariableNames()
		{
		var names = new HashSet<string>(StringComparer.Ordinal);
		foreach (var frame in _frames)
			foreach (var k in frame.Keys) names.Add(k);
		foreach (var k in _arrays.Keys) names.Add(k);
		foreach (var k in _assocs.Keys) names.Add(k);
		return names;
		}

	// ── indexed arrays ────────────────────────────────────────────────────────

	public bool IsArray(string name) => _arrays.ContainsKey(name);

	public void DeclareArray(string name)
		{
		if (!_arrays.ContainsKey(name))
			_arrays[name] = new SortedDictionary<int, string>();
		}

	public void SetArrayElement(string name, int index, string value)
		{
		if (_readonly.Contains(name)) throw new FatalShellException($"{name}: readonly variable");
		if (!_arrays.ContainsKey(name))
			_arrays[name] = new SortedDictionary<int, string>();
		if (index < 0)
			{
			int max = _arrays[name].Count > 0 ? _arrays[name].Keys.Max() : -1;
			index = max + 1 + index;
			if (index < 0) throw new EvalException($"{name}[{index}]: bad array subscript");
			}
		_arrays[name][index] = value;
		}

	public string GetArrayElement(string name, int index)
		{
		if (_arrays.TryGetValue(name, out var arr))
			{
			if (index < 0 && arr.Count > 0) index = arr.Keys.Max() + 1 + index;
			if (arr.TryGetValue(index, out var val)) return val;
			}
		return "";
		}

	public bool HasArrayElement(string name, int index)
		{
		if (!_arrays.TryGetValue(name, out var arr)) return false;
		if (index < 0 && arr.Count > 0) index = arr.Keys.Max() + 1 + index;
		return arr.ContainsKey(index);
		}

	public void UnsetArrayElement(string name, int index)
		{
		if (_arrays.TryGetValue(name, out var arr))
			{
			if (index < 0 && arr.Count > 0) index = arr.Keys.Max() + 1 + index;
			arr.Remove(index);
			}
		}

	/// <summary>All values in index order.</summary>
	public List<string> GetArrayValues(string name) =>
		_arrays.TryGetValue(name, out var arr) ? [.. arr.Values] : [];

	/// <summary>All indices as strings.</summary>
	public List<string> GetArrayKeys(string name) =>
		_arrays.TryGetValue(name, out var arr) ? arr.Keys.Select(k => k.ToString()).ToList() : [];

	/// <summary>Index/value pairs in order (for `declare -p`).</summary>
	public IEnumerable<KeyValuePair<int, string>> GetArrayPairs(string name) =>
		_arrays.TryGetValue(name, out var arr) ? arr : [];

	public int GetArrayLength(string name) =>
		_arrays.TryGetValue(name, out var arr) ? arr.Count : 0;

	/// <summary>
	/// Assign from compound list: arr=(a b c).
	/// Replaces any existing array.
	/// </summary>
	public void SetArrayFromList(string name, List<string> values)
		{
		if (_readonly.Contains(name)) throw new FatalShellException($"{name}: readonly variable");
		var arr = new SortedDictionary<int, string>();
		for (int i = 0; i < values.Count; i++)
			arr[i] = values[i];
		_arrays[name] = arr;
		_assocs.Remove(name);
		}

	/// <summary>arr+=(x y): append after the highest index.</summary>
	public void AppendArrayFromList(string name, List<string> values)
		{
		if (_readonly.Contains(name)) throw new FatalShellException($"{name}: readonly variable");
		if (_assocs.ContainsKey(name))
			{
			// bash: appending a list to an assoc array requires [key]=value pairs; treat as error-free no-op
			return;
			}
		if (!_arrays.TryGetValue(name, out var arr)) { SetArrayFromList(name, values); return; }
		int next = arr.Count > 0 ? arr.Keys.Max() + 1 : 0;
		foreach (var v in values) arr[next++] = v;
		}

	// ── associative arrays ────────────────────────────────────────────────────

	public bool IsAssoc(string name) => _assocs.ContainsKey(name);

	public void DeclareAssoc(string name)
		{
		if (!_assocs.ContainsKey(name))
			_assocs[name] = new Dictionary<string, string>(StringComparer.Ordinal);
		_arrays.Remove(name);
		}

	public void SetAssocElement(string name, string key, string value)
		{
		if (_readonly.Contains(name)) throw new FatalShellException($"{name}: readonly variable");
		if (!_assocs.ContainsKey(name))
			_assocs[name] = new Dictionary<string, string>(StringComparer.Ordinal);
		_assocs[name][key] = value;
		}

	public string GetAssocElement(string name, string key)
		{
		if (_assocs.TryGetValue(name, out var map) && map.TryGetValue(key, out var val))
			return val;
		return "";
		}

	public bool HasAssocElement(string name, string key) =>
		_assocs.TryGetValue(name, out var map) && map.ContainsKey(key);

	public void UnsetAssocElement(string name, string key)
		{
		if (_assocs.TryGetValue(name, out var map))
			map.Remove(key);
		}

	public List<string> GetAssocValues(string name) =>
		_assocs.TryGetValue(name, out var map) ? [.. map.Values] : [];

	public List<string> GetAssocKeys(string name) =>
		_assocs.TryGetValue(name, out var map) ? [.. map.Keys] : [];

	public IEnumerable<KeyValuePair<string, string>> GetAssocPairs(string name) =>
		_assocs.TryGetValue(name, out var map) ? map : [];

	public int GetAssocLength(string name) =>
		_assocs.TryGetValue(name, out var map) ? map.Count : 0;

	// ── scopes ────────────────────────────────────────────────────────────────

	/// <summary>Set $0 (script name / shell name).</summary>
	public void SetArg0(string value) => _arg0 = value;

	/// <summary>Current positional parameters $1..$N (for "$@"/"$*" field expansion).</summary>
	public List<string> GetPositionals() => _positionals.TryPeek(out var p) ? [.. p] : [];

	/// <summary>Replace the current (top-level) positional parameters $1..$N.</summary>
	public void SetPositionals(IEnumerable<string> positionals)
		{
		if (_positionals.Count > 0) _positionals.Pop();
		_positionals.Push(positionals.ToArray());
		}

	public void PushScope(string[] positionals)
		{
		_frames.Add(new Dictionary<string, string>(StringComparer.Ordinal));
		_positionals.Push(positionals);
		}

	public void PopScope()
		{
		if (_frames.Count > 1) _frames.RemoveAt(_frames.Count - 1);
		if (_positionals.Count > 0) _positionals.Pop();
		}

	public int ScopeDepth => _frames.Count;

	// ── snapshot / restore (subshell isolation) ───────────────────────────────

	public sealed class Snapshot
		{
		internal List<Dictionary<string, string>> Frames = [];
		internal Dictionary<string, SortedDictionary<int, string>> Arrays = new();
		internal Dictionary<string, Dictionary<string, string>> Assocs = new();
		internal HashSet<string> Exports = [], Readonly = [], Integers = [];
		internal string[][] Positionals = [];
		internal string Arg0 = "";
		internal string Cwd = "";
		}

	/// <summary>Deep-copy the variable state (for `( )` / `$( )` isolation).</summary>
	public Snapshot TakeSnapshot()
		{
		var s = new Snapshot
			{
			Frames    = _frames.Select(f => new Dictionary<string, string>(f, StringComparer.Ordinal)).ToList(),
			Arrays    = _arrays.ToDictionary(kv => kv.Key, kv => new SortedDictionary<int, string>(kv.Value), StringComparer.Ordinal),
			Assocs    = _assocs.ToDictionary(kv => kv.Key, kv => new Dictionary<string, string>(kv.Value, StringComparer.Ordinal), StringComparer.Ordinal),
			Exports   = new HashSet<string>(_exports, StringComparer.Ordinal),
			Readonly  = new HashSet<string>(_readonly, StringComparer.Ordinal),
			Integers  = new HashSet<string>(_integers, StringComparer.Ordinal),
			Positionals = _positionals.ToArray(),
			Arg0      = _arg0,
			Cwd       = Directory.GetCurrentDirectory(),
			};
		return s;
		}

	public void RestoreSnapshot(Snapshot s)
		{
		_frames.Clear(); _frames.AddRange(s.Frames.Select(f => new Dictionary<string, string>(f, StringComparer.Ordinal)));
		_arrays = s.Arrays.ToDictionary(kv => kv.Key, kv => new SortedDictionary<int, string>(kv.Value), StringComparer.Ordinal);
		_assocs = s.Assocs.ToDictionary(kv => kv.Key, kv => new Dictionary<string, string>(kv.Value, StringComparer.Ordinal), StringComparer.Ordinal);
		_exports  = new HashSet<string>(s.Exports, StringComparer.Ordinal);
		_readonly = new HashSet<string>(s.Readonly, StringComparer.Ordinal);
		_integers = new HashSet<string>(s.Integers, StringComparer.Ordinal);
		_positionals.Clear();
		foreach (var p in s.Positionals.Reverse()) _positionals.Push(p);
		_arg0 = s.Arg0;
		try { if (Directory.GetCurrentDirectory() != s.Cwd && Directory.Exists(s.Cwd)) Directory.SetCurrentDirectory(s.Cwd); } catch { }
		}

	// ── exports ───────────────────────────────────────────────────────────────

	public Dictionary<string, string> GetExportedVars()
		{
		var result = new Dictionary<string, string>(StringComparer.Ordinal);
		foreach (var name in _exports)
			{
			var val = Get(name);
			result[name] = val;
			}
		return result;
		}

	// ── platform identity ─────────────────────────────────────────────────────

	/// <summary>MSYS-style uid: 0x30000 + the account's RID (so `id -u` agrees with Git Bash).</summary>
	/// <summary>
	/// Git for Windows' bash puts its own `usr/bin` and `mingw64/bin` ahead of the inherited
	/// PATH (awk, tar, xxd, nohup, perl, curl, ssh… live there). Mirror that when a Git
	/// installation is discoverable through `git.exe` on PATH, so a script that works under
	/// Git Bash finds the same externals here; in-process coreutils shadow PATH regardless.
	/// Portable installs are honoured (the root is derived from git.exe, not Program Files).
	/// </summary>
	private void AugmentPathWithGitTools()
		{
		var path = Get("PATH");
		if (path.Length == 0) return;
		var entries = path.Split(';', StringSplitOptions.RemoveEmptyEntries);
		string? root = null;
		foreach (var dir in entries)
			{
			string? candidate = null;
			try
				{
				if (!File.Exists(Path.Combine(dir, "git.exe"))) continue;
				var leaf = Path.GetFileName(dir.TrimEnd('\\', '/')).ToLowerInvariant();
				var parent = Path.GetDirectoryName(dir.TrimEnd('\\', '/'));
				if (parent is null) continue;
				if (leaf is "cmd" or "bin") candidate = parent;
				else if (leaf == "bin" || (leaf == "bin" && parent.EndsWith("mingw64", StringComparison.OrdinalIgnoreCase))) candidate = Path.GetDirectoryName(parent);
				else if (parent.EndsWith("mingw64", StringComparison.OrdinalIgnoreCase)) candidate = Path.GetDirectoryName(parent);
				}
			catch { continue; }
			if (candidate is not null && Directory.Exists(Path.Combine(candidate, "usr", "bin"))) { root = candidate; break; }
			}
		if (root is null) return;
		var extra = new List<string>();
		foreach (var sub in new[] { Path.Combine(root, "usr", "bin"), Path.Combine(root, "mingw64", "bin") })
			{
			if (!Directory.Exists(sub)) continue;
			if (entries.Any(e => string.Equals(e.TrimEnd('\\'), sub, StringComparison.OrdinalIgnoreCase))) continue;
			extra.Add(sub);
			}
		if (extra.Count == 0) return;
		Set("PATH", string.Join(";", extra) + ";" + path);
		}

	/// <summary>
	/// On a machine without Git, nothing on PATH answers to `bash`; a nested `bash -c …` or
	/// `bash script.sh` from a script would fail with 127. Appending this executable's own
	/// directory (at the END, so a real Git bash still wins when present) makes the shell
	/// reachable under its own name, as bash always is on a Unix PATH.
	/// </summary>
	private void AppendOwnDirectoryToPath()
		{
		string dir;
		try { dir = AppContext.BaseDirectory.TrimEnd('\\', '/'); } catch { return; }
		if (dir.Length == 0) return;
		var path = Get("PATH");
		var entries = path.Split(';', StringSplitOptions.RemoveEmptyEntries);
		if (entries.Any(e => string.Equals(e.TrimEnd('\\'), dir, StringComparison.OrdinalIgnoreCase))) return;
		Set("PATH", path.Length == 0 ? dir : path + ";" + dir);
		}

	private static long WindowsUid()
		{
		try
			{
			if (!OperatingSystem.IsWindows()) return 1000;
			var sid = System.Security.Principal.WindowsIdentity.GetCurrent().User?.Value ?? "";
			int dash = sid.LastIndexOf('-');
			if (dash > 0 && long.TryParse(sid[(dash + 1)..], out var rid))
				return sid.StartsWith("S-1-5-21-") ? 0x30000 + rid : 0x10000 + rid;
			}
		catch { }
		return 1000;
		}

	[StructLayout(LayoutKind.Sequential)]
	private struct ProcessBasicInformation
		{
		public IntPtr Reserved1, PebBaseAddress, Reserved2a, Reserved2b, UniqueProcessId, InheritedFromUniqueProcessId;
		}

	[DllImport("ntdll.dll")]
	private static extern int NtQueryInformationProcess(IntPtr processHandle, int processInformationClass,
		ref ProcessBasicInformation processInformation, int processInformationLength, out int returnLength);

	private static long ParentPid()
		{
		try
			{
			if (!OperatingSystem.IsWindows()) return 1;
			var pbi = new ProcessBasicInformation();
			using var me = System.Diagnostics.Process.GetCurrentProcess();
			int rc = NtQueryInformationProcess(me.Handle, 0, ref pbi, Marshal.SizeOf<ProcessBasicInformation>(), out _);
			if (rc == 0) return pbi.InheritedFromUniqueProcessId.ToInt64();
			}
		catch { }
		return 1;
		}
	}
