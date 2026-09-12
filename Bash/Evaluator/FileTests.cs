namespace Bash.Evaluator;

/// <summary>
/// The file/string primaries shared by `test`/`[` and `[[ ]]`. Paths go through
/// <see cref="ShellEnvironment.TranslatePath"/> so `/c/x`, `/tmp/x` and `~/x` work.
/// Returns null for an operator it doesn't know so the caller can handle it.
/// </summary>
public static class FileTests
	{
	public static bool? Unary(string op, string val)
		{
		switch (op)
			{
			case "-z": return val.Length == 0;
			case "-n": return val.Length > 0;
			}
		var p = ShellEnvironment.TranslatePath(val);
		bool isFile = File.Exists(p), isDir = Directory.Exists(p);
		switch (op)
			{
			case "-e": case "-a": return isFile || isDir;
			case "-f": return isFile;
			case "-d": return isDir;
			case "-s": return isFile && new FileInfo(p).Length > 0;
			case "-r": return isFile || isDir;
			case "-w": return isDir || (isFile && !new FileInfo(p).IsReadOnly);
			case "-x": return isDir || (isFile && IsExecutable(p));
			case "-L": case "-h":
				{
				if (!isFile && !isDir) return false;
				try { return (File.GetAttributes(p) & FileAttributes.ReparsePoint) != 0; } catch { return false; }
				}
			case "-p": case "-S": case "-b": case "-c": case "-k": case "-u": case "-g": case "-N": return false;
			case "-O": case "-G": return isFile || isDir;
			case "-t":
				{
				if (!int.TryParse(val, out var fd)) return false;
				return fd switch
					{
					0 => !Console.IsInputRedirected,
					1 => !Console.IsOutputRedirected,
					2 => !Console.IsErrorRedirected,
					_ => false,
					};
				}
			}
		return null;
		}

	public static bool? Binary(string op, string a, string b)
		{
		switch (op)
			{
			case "-nt":
				{
				var pa = ShellEnvironment.TranslatePath(a); var pb = ShellEnvironment.TranslatePath(b);
				bool ea = File.Exists(pa) || Directory.Exists(pa), eb = File.Exists(pb) || Directory.Exists(pb);
				if (!ea) return false;
				if (!eb) return true;
				return File.GetLastWriteTimeUtc(pa) > File.GetLastWriteTimeUtc(pb);
				}
			case "-ot":
				{
				var pa = ShellEnvironment.TranslatePath(a); var pb = ShellEnvironment.TranslatePath(b);
				bool ea = File.Exists(pa) || Directory.Exists(pa), eb = File.Exists(pb) || Directory.Exists(pb);
				if (!eb) return false;
				if (!ea) return true;
				return File.GetLastWriteTimeUtc(pa) < File.GetLastWriteTimeUtc(pb);
				}
			case "-ef":
				{
				var pa = ShellEnvironment.TranslatePath(a); var pb = ShellEnvironment.TranslatePath(b);
				try { return string.Equals(Path.GetFullPath(pa), Path.GetFullPath(pb), StringComparison.OrdinalIgnoreCase) && (File.Exists(pa) || Directory.Exists(pa)); }
				catch { return false; }
				}
			}
		return null;
		}

	/// <summary>Windows notion of executable: a PATHEXT extension, a `.sh` file, or a `#!` script.</summary>
	public static bool IsExecutable(string path)
		{
		var ext = Path.GetExtension(path);
		if (ext.Length > 0)
			{
			var pathExt = (Environment.GetEnvironmentVariable("PATHEXT") ?? ".COM;.EXE;.BAT;.CMD")
				.Split(';', StringSplitOptions.RemoveEmptyEntries);
			foreach (var e in pathExt) if (string.Equals(e, ext, StringComparison.OrdinalIgnoreCase)) return true;
			if (ext.Equals(".sh", StringComparison.OrdinalIgnoreCase)) return true;
			}
		try
			{
			using var fs = File.OpenRead(path);
			return fs.Length >= 2 && fs.ReadByte() == '#' && fs.ReadByte() == '!';
			}
		catch { return false; }
		}
	}
