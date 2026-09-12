namespace Bash.IO;

/// <summary>
/// Command history with optional file persistence (default ~/.bash_history).
/// Consecutive duplicate and empty lines are not stored, matching bash's default
/// HISTCONTROL=ignoredups-ish behaviour. The in-memory list and the file are both
/// capped to <see cref="MaxEntries"/> (from HISTSIZE). Navigation state is owned by
/// the caller (the line editor) via <see cref="Count"/> and the indexer.
/// </summary>
public sealed class History
	{
	private readonly List<string> _items = [];
	private readonly string? _file;

	/// <summary>Maximum retained entries (HISTSIZE). Older entries are dropped.</summary>
	public int MaxEntries { get; }

	public History(string? file = null, int maxEntries = 500)
		{
		_file = file;
		MaxEntries = Math.Max(1, maxEntries);
		if (_file is not null && File.Exists(_file))
			{
			try
				{
				foreach (var line in Bash.Evaluator.ShellEncoding.ReadAllLines(_file))
					if (line.Length > 0) _items.Add(line);
				}
			catch { /* unreadable history is non-fatal */ }
			TrimToMax();
			}
		}

	public int Count => _items.Count;

	public string this[int i] => _items[i];

	public IReadOnlyList<string> Items => _items;

	public void Add(string line)
		{
		if (line.Length == 0) return;
		if (_items.Count > 0 && _items[^1] == line) return;   // skip consecutive dups
		_items.Add(line);
		bool trimmed = TrimToMax();
		if (_file is not null)
			{
			try
				{
				// When trimming occurred the file must be rewritten to stay capped;
				// otherwise a cheap append is enough.
				if (trimmed) File.WriteAllLines(_file, _items);
				else File.AppendAllText(_file, line + "\n");
				}
			catch { /* unwritable history is non-fatal */ }
			}
		}

	public void Clear()
		{
		_items.Clear();
		if (_file is not null)
			{
			try { File.WriteAllText(_file, "", Bash.Evaluator.ShellEncoding.Utf8); }
			catch { /* non-fatal */ }
			}
		}

	private bool TrimToMax()
		{
		bool trimmed = false;
		while (_items.Count > MaxEntries) { _items.RemoveAt(0); trimmed = true; }
		return trimmed;
		}
	}
