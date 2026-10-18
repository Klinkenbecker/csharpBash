using System.Text;

namespace Bash.Evaluator;

/// <summary>
/// The persistent command map (DECISIONS 2026-10-16 "RATIFIED: the tool-lookup plan" and "only `hash` records and
/// obeys misses"). The shell's own PATH lookups kept across processes: one small file per PATH value (with
/// PATHEXT) under ~/.bash/commands. Every lookup remembers hits (used after one existence probe;
/// gone: searched again). Misses are remembered for `hash name` alone, which obeys one an EARLIER process recorded
/// ("fail once", the architect) until `hash -r` or `hash -d name`; its own are searched again, as bash. Why:
/// Claude Code's snapshot runs `hash rg` on every call and rg is on no PATH here, so that one miss searched the
/// whole PATH every time (~15 ms of a ~31 ms call, DECISIONS "REVIEW R1 RESULT"). Not kept for a PATH with a
/// relative entry (the answer would depend on the directory). A write reads the file, applies its one change
/// and renames a temp file over it, so concurrent shells merge what they find; a lost update costs one more
/// search. A map in use is touched when read, at most hourly, and one unused for 30 days is pruned (a burst
/// of new PATH values, a test run's, cannot push out the one in use). Process-wide (pipeline stages share
/// it), so every member takes the lock.
/// </summary>
public static class CommandMap
	{
	private const string Format = "C#Bash command map 1";
	private static readonly TimeSpan TouchEvery = TimeSpan.FromHours(1), KeepUnused = TimeSpan.FromDays(30);
	private static readonly UTF8Encoding _utf8 = new(false);
	private static readonly object _lock = new();
	private static string? _key;        // PATH + "\n" + PATHEXT that _entries belong to
	private static string? _file;       // null: not kept for this PATH
	private static Dictionary<string, string> _entries = [];   // name -> path; "" is a remembered miss
	private static readonly HashSet<string> _missesHere = [];   // key + "\0" + name: misses this process recorded

	/// <summary>The remembered path of <paramref name="name"/> on this PATH, or null.</summary>
	public static string? Hit(string path, string pathExt, string name)
		{
		lock (_lock)
			{
			Select(path, pathExt);
			return _entries.TryGetValue(name, out var found) && found.Length > 0 ? found : null;
			}
		}

	/// <summary>True when an EARLIER process recorded <paramref name="name"/> as not found on this PATH (a miss
	/// this process recorded itself does not count: it searches again, as bash).</summary>
	public static bool EarlierMiss(string path, string pathExt, string name)
		{
		lock (_lock)
			{
			Select(path, pathExt);
			return _entries.TryGetValue(name, out var found) && found.Length == 0 && !_missesHere.Contains(_key + "\0" + name);
			}
		}

	/// <summary>Remember what a search found: the program's path, or "" for nothing (`hash name` only).</summary>
	public static void Record(string path, string pathExt, string name, string found)
		{
		lock (_lock)
			{
			Change(path, pathExt, name, found);
			if (found.Length == 0) _missesHere.Add(_key + "\0" + name);
			else _missesHere.Remove(_key + "\0" + name);
			}
		}

	/// <summary>Forget one name (`hash -d`, or a remembered program that has gone). True if it was there.</summary>
	public static bool Forget(string path, string pathExt, string name)
		{
		lock (_lock)
			{
			bool had = Change(path, pathExt, name, null);
			_missesHere.Remove(_key + "\0" + name);
			return had;
			}
		}

	/// <summary>`hash -r`: forget everything remembered for this PATH.</summary>
	public static void Clear(string path, string pathExt)
		{
		lock (_lock)
			{
			Select(path, pathExt);
			_entries.Clear();
			_missesHere.RemoveWhere(k => k.StartsWith(_key + "\0", StringComparison.Ordinal));
			if (_file is not null) try { File.Delete(_file); } catch { }
			}
		}

	private static bool Change(string path, string pathExt, string name, string? found)
		{
		if (name.AsSpan().IndexOfAny('\t', '\n', '\r') >= 0) return false;   // not representable in the file
		lock (_lock)
			{
			Select(path, pathExt);
			if (_file is null) return false;
			bool had = _entries.TryGetValue(name, out var old);
			if (found is not null && had && old == found) return true;
			// merge with what other shells wrote since this one read the file. A forget looks there too: the
			// name may have been written since (a long-lived shell's `hash -d` said "not found" and the entry
			// stayed, 2026-10-16)
			var merged = Load(_file, path, pathExt) ?? [];
			if (found is null)
				{
				bool inFile = merged.Remove(name);
				_entries = merged;
				if (!inFile) return had;   // nothing to write
				Write(_file, path, pathExt, merged);
				return true;
				}
			merged[name] = found;
			_entries = merged;
			Write(_file, path, pathExt, merged);
			return had;
			}
		}

	/// <summary>Point the map at this PATH's file, reading it the first time.</summary>
	private static void Select(string path, string pathExt)
		{
		var key = path + "\n" + pathExt;
		if (key == _key) return;
		_key = key;
		_entries = [];
		_file = Kept(path, pathExt) && Folder() is { } dir ? Path.Combine(dir, Fnv1a(key).ToString("x16") + ".map") : null;
		if (_file is null || Load(_file, path, pathExt, out var written) is not { } entries) return;
		_entries = entries;
		// in use: keep its time recent, so pruning by age never takes it
		if (DateTime.UtcNow - written > TouchEvery) try { File.SetLastWriteTimeUtc(_file, DateTime.UtcNow); } catch { }
		}

	/// <summary>Kept only when every entry is an absolute path: a relative one answers differently per directory.</summary>
	private static bool Kept(string path, string pathExt)
		{
		if (path.Contains('\n') || pathExt.Contains('\n')) return false;
		var dirs = path.Split(';', StringSplitOptions.RemoveEmptyEntries);
		return dirs.Length > 0 && dirs.All(Path.IsPathFullyQualified);
		}

	/// <summary>~/.bash/commands, with bash's other files (the architect: bash is not a "normal" Windows app, so
	/// not AppData): HOME as the process has it, MSYS or Windows form, else the Windows profile.</summary>
	private static string? Folder()
		{
		var home = Environment.GetEnvironmentVariable("HOME");
		home = string.IsNullOrEmpty(home) ? Environment.GetFolderPath(Environment.SpecialFolder.UserProfile) : ShellEnvironment.TranslatePath(home);
		return string.IsNullOrEmpty(home) ? null : Path.Combine(home, ".bash", "commands");
		}

	private static Dictionary<string, string>? Load(string file, string path, string pathExt) => Load(file, path, pathExt, out _);

	/// <summary>The file's entries (and when it was last written), or null when it is missing, unreadable, or for
	/// another PATH (a hash collision).</summary>
	private static Dictionary<string, string>? Load(string file, string path, string pathExt, out DateTime written)
		{
		string text;
		written = default;
		try
			{
			using var fs = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
			written = File.GetLastWriteTimeUtc(fs.SafeFileHandle);
			using var sr = new StreamReader(fs, _utf8);
			text = sr.ReadToEnd();
			}
		catch { return null; }
		var lines = text.Split('\n');
		if (lines.Length < 3 || lines[0] != Format || lines[1] != path || lines[2] != pathExt) return null;
		var entries = new Dictionary<string, string>();
		for (int i = 3; i < lines.Length; i++)
			{
			int tab = lines[i].IndexOf('\t');
			if (tab > 0) entries[lines[i][..tab]] = lines[i][(tab + 1)..];
			}
		return entries;
		}

	private static void Write(string file, string path, string pathExt, Dictionary<string, string> entries)
		{
		var sb = new StringBuilder().Append(Format).Append('\n').Append(path).Append('\n').Append(pathExt).Append('\n');
		foreach (var (name, found) in entries) sb.Append(name).Append('\t').Append(found).Append('\n');
		var tmp = file + "." + Environment.ProcessId + ".tmp";
		try
			{
			var dir = Path.GetDirectoryName(file)!;
			Directory.CreateDirectory(dir);
			bool isNew = !File.Exists(file);
			File.WriteAllText(tmp, sb.ToString(), _utf8);
			File.Move(tmp, file, overwrite: true);
			if (isNew) Prune(dir);
			}
		catch { try { File.Delete(tmp); } catch { } }   // another shell holds it: this update is lost, harmlessly
		}

	/// <summary>A new PATH value made a new file: drop the maps unused for <see cref="KeepUnused"/> (a map in use is
	/// touched when read), and the temp files a killed shell left.</summary>
	private static void Prune(string dir)
		{
		try
			{
			var now = DateTime.UtcNow;
			foreach (var f in new DirectoryInfo(dir).GetFiles("*.map"))
				if (now - f.LastWriteTimeUtc > KeepUnused) try { f.Delete(); } catch { }
			foreach (var f in new DirectoryInfo(dir).GetFiles("*.tmp"))
				if (now - f.LastWriteTimeUtc > TimeSpan.FromMinutes(5)) try { f.Delete(); } catch { }
			}
		catch { }
		}

	/// <summary>FNV-1a, 64-bit: stable across processes (string.GetHashCode is randomised per process).</summary>
	private static ulong Fnv1a(string s)
		{
		ulong h = 14695981039346656037;
		foreach (var c in s) { h ^= c; h *= 1099511628211; }
		return h;
		}
	}
