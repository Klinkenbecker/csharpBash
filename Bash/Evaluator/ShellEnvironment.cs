namespace Bash.Evaluator;

/// <summary>
/// Holds shell variables, arrays, exports, and positional parameters.
/// Phase 5: adds indexed arrays (declare -a) and associative arrays (declare -A).
/// </summary>
public sealed class ShellEnvironment
	{
	// Scalar variable frames (innermost = last)
	private readonly List<Dictionary<string, string>> _frames = [new()];

	// Arrays live at global scope only (bash arrays are not local by default
	// unless explicitly declared local — we keep it simple for now).
	// Key = array name, Value = index→value map.
	private readonly Dictionary<string, SortedDictionary<int, string>>    _arrays = new(StringComparer.Ordinal);
	private readonly Dictionary<string, Dictionary<string, string>>        _assocs = new(StringComparer.Ordinal);

	private readonly HashSet<string> _exports   = new(StringComparer.Ordinal);
	private readonly Stack<string[]> _positionals = new();
	private string _arg0 = System.Environment.GetCommandLineArgs()[0];

	public int LastExitCode { get; set; } = 0;

	public ShellEnvironment()
		{
		_positionals.Push([]);
		foreach (System.Collections.DictionaryEntry e in System.Environment.GetEnvironmentVariables())
			{
			var key = e.Key?.ToString() ?? "";
			var val = e.Value?.ToString() ?? "";
			if (key.Length > 0)
				{
				_frames[0][key] = val;
				_exports.Add(key);
				}
			}

		if (Get("HOME").Length == 0)
			{
			var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
			Set("HOME", home); Export("HOME");
			}
		}

	// ── path translation ──────────────────────────────────────────────────────

	/// <summary>
	/// Minimal path translation: ~ expansion and forward-slash → backslash.
	/// Full Unix root mapping is deferred — see DECISIONS.md.
	/// </summary>
	public static string TranslatePath(string path)
		{
		if (path.Length == 0) return path;

		if (path == "~" || path.StartsWith("~/") || path.StartsWith("~\\"))
			{
			var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
			return path.Length == 1 ? home : home + Path.DirectorySeparatorChar + path[2..].Replace('/', Path.DirectorySeparatorChar);
			}

		if (Path.DirectorySeparatorChar == '\\')
			return path.Replace('/', Path.DirectorySeparatorChar);

		return path;
		}

	// ── scalar variables ──────────────────────────────────────────────────────

	public string Get(string name)
		{
		switch (name)
			{
			case "?":  return LastExitCode.ToString();
			case "$":  return Environment.ProcessId.ToString();
			case "#":  return (_positionals.TryPeek(out var p) ? p.Length : 0).ToString();
			case "@":  return string.Join(" ", _positionals.TryPeek(out var pa) ? pa : []);
			case "*":  return string.Join(" ", _positionals.TryPeek(out var ps) ? ps : []);
			case "0":  return _arg0;
			}

		if (int.TryParse(name, out int idx))
			{
			var pos = _positionals.TryPeek(out var pp) ? pp : [];
			return idx > 0 && idx <= pos.Length ? pos[idx - 1] : "";
			}

		for (int i = _frames.Count - 1; i >= 0; i--)
			if (_frames[i].TryGetValue(name, out var val))
				return val;

		return "";
		}

	public void Set(string name, string value)
		{
		for (int i = _frames.Count - 1; i >= 0; i--)
			if (_frames[i].ContainsKey(name))
				{ _frames[i][name] = value; return; }
		_frames[^1][name] = value;
		}

	/// <summary>Set in the innermost frame only (implements 'local').</summary>
	public void SetLocal(string name, string value) =>
		_frames[^1][name] = value;

	public void Unset(string name)
		{
		foreach (var f in _frames)
			f.Remove(name);
		_arrays.Remove(name);
		_assocs.Remove(name);
		}

	public void Export(string name) => _exports.Add(name);

	/// <summary>All scalar variable names across scopes plus array names (for tab completion).</summary>
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
		if (!_arrays.ContainsKey(name))
			_arrays[name] = new SortedDictionary<int, string>();
		_arrays[name][index] = value;
		}

	public string GetArrayElement(string name, int index)
		{
		if (_arrays.TryGetValue(name, out var arr) && arr.TryGetValue(index, out var val))
			return val;
		return "";
		}

	public void UnsetArrayElement(string name, int index)
		{
		if (_arrays.TryGetValue(name, out var arr))
			arr.Remove(index);
		}

	/// <summary>All values in index order.</summary>
	public List<string> GetArrayValues(string name) =>
		_arrays.TryGetValue(name, out var arr) ? [.. arr.Values] : [];

	/// <summary>All indices as strings.</summary>
	public List<string> GetArrayKeys(string name) =>
		_arrays.TryGetValue(name, out var arr) ? arr.Keys.Select(k => k.ToString()).ToList() : [];

	public int GetArrayLength(string name) =>
		_arrays.TryGetValue(name, out var arr) ? arr.Count : 0;

	/// <summary>
	/// Assign from compound list: arr=(a b c).
	/// Replaces any existing array.
	/// </summary>
	public void SetArrayFromList(string name, List<string> values)
		{
		var arr = new SortedDictionary<int, string>();
		for (int i = 0; i < values.Count; i++)
			arr[i] = values[i];
		_arrays[name] = arr;
		}

	// ── associative arrays ────────────────────────────────────────────────────

	public bool IsAssoc(string name) => _assocs.ContainsKey(name);

	public void DeclareAssoc(string name)
		{
		if (!_assocs.ContainsKey(name))
			_assocs[name] = new Dictionary<string, string>(StringComparer.Ordinal);
		}

	public void SetAssocElement(string name, string key, string value)
		{
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

	public void UnsetAssocElement(string name, string key)
		{
		if (_assocs.TryGetValue(name, out var map))
			map.Remove(key);
		}

	public List<string> GetAssocValues(string name) =>
		_assocs.TryGetValue(name, out var map) ? [.. map.Values] : [];

	public List<string> GetAssocKeys(string name) =>
		_assocs.TryGetValue(name, out var map) ? [.. map.Keys] : [];

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
	}
