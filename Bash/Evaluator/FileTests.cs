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
		// Linux's device files exist, are character devices and are readable and writable (they used
		// to test as missing: `[ -e /dev/null ]` was false until 2026-10-10). A /dev/fd/N and
		// /dev/std* is a character device when its descriptor is one: the console, or /dev/null.
		if (ShellFile.Device(p) is { } dev)
			return op switch
				{
				"-e" or "-a" or "-r" or "-w" or "-O" or "-G" => true,
				"-c" => !dev.StartsWith("fd:", StringComparison.Ordinal) || FdIsCharDevice(ShellFile.DeviceFd(p)),
				"-f" or "-d" or "-s" or "-x" or "-L" or "-h" or "-p" or "-S" or "-b" or "-k" or "-u" or "-g" or "-N" => false,
				_ => null,
				};
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
				if (!int.TryParse(val, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var fd)) return false;
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

	/// <summary>Whether descriptor <paramref name="fd"/> is a character device: the console, or /dev/null.</summary>
	private static bool FdIsCharDevice(int fd) => fd switch
		{
		0 => ReferenceEquals(ConsoleMux.RawIn, Stream.Null) || (ConsoleMux.RawIn is null && !ConsoleMux.InSwapped && !Console.IsInputRedirected),
		1 => ReferenceEquals(ConsoleMux.Out, TextWriter.Null) || (!ConsoleMux.OutSwapped && !Console.IsOutputRedirected),
		2 => ReferenceEquals(ConsoleMux.Err, TextWriter.Null) || (!ConsoleMux.ErrSwapped && !Console.IsErrorRedirected),
		_ => false,
		};

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

	/// <summary>An extension Windows' launcher starts: .com, .exe, .bat, .cmd. Not every PATHEXT one: `.js`, `.vbs`, `.wsf`,
	/// `.msc` run only through a file association, so a `zz.js` on PATH stopped the search for `zz` with "Permission
	/// denied" before a later `zz.exe`, and `[ -x zz.js ]` was true; Git Bash passes over it and says false, and bash
	/// passes over any match it cannot execute (the architect: "Yes make the change", 2026-10-18).</summary>
	public static bool IsProgramExtension(string ext) =>
		ext.Equals(".exe", StringComparison.OrdinalIgnoreCase) || ext.Equals(".com", StringComparison.OrdinalIgnoreCase)
		|| ext.Equals(".bat", StringComparison.OrdinalIgnoreCase) || ext.Equals(".cmd", StringComparison.OrdinalIgnoreCase);

	/// <summary>Windows notion of executable: a program extension (<see cref="IsProgramExtension"/>), a `.sh` file, or a
	/// `#!` script.</summary>
	public static bool IsExecutable(string path)
		{
		var ext = Path.GetExtension(path);
		if (ext.Length > 0)
			{
			if (IsProgramExtension(ext)) return true;
			if (ext.Equals(".sh", StringComparison.OrdinalIgnoreCase)) return true;
			}
		try
			{
			using var fs = ShellFile.OpenRead(path);
			return fs.Length >= 2 && fs.ReadByte() == '#' && fs.ReadByte() == '!';
			}
		catch { return false; }
		}
	}
