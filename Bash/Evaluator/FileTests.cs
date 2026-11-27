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
			{
			if (dev.StartsWith("fd:", StringComparison.Ordinal))
				{
				// a /dev/fd/N or /dev/std* tests as what its descriptor is, as GNU's stat of it sees it (bugs list 14,
				// wombat-3a, 2026-11-27: -p was false on a pipe, -f and -s on a file)
				var (kind, size) = Describe(ShellFile.DeviceFd(p));
				return op switch
					{
					"-e" or "-a" or "-r" or "-w" or "-O" or "-G" => true,
					"-c" => kind == FdKind.Char,
					"-p" => kind == FdKind.Pipe,
					"-f" => kind == FdKind.File,
					"-s" => kind == FdKind.File && size > 0,
					"-d" or "-x" or "-L" or "-h" or "-S" or "-b" or "-k" or "-u" or "-g" or "-N" => false,
					_ => null,
					};
				}
			return op switch
				{
				"-e" or "-a" or "-r" or "-w" or "-O" or "-G" or "-c" => true,
				"-f" or "-d" or "-s" or "-x" or "-L" or "-h" or "-p" or "-S" or "-b" or "-k" or "-u" or "-g" or "-N" => false,
				_ => null,
				};
			}
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

	private enum FdKind { Unknown, Char, Pipe, File }

	/// <summary>Where a write to /dev/stdin (or /dev/fd/0) goes, as GNU reopens descriptor 0 for writing (bugs list 10,
	/// guy-94, 2026-11-24: it was "No such file or directory" whatever stdin was): "/dev/null", "/dev/tty", or the file's
	/// own path; null for a pipe, a here-document or what cannot be told, and the write then fails as before.</summary>
	public static string? StdinForWriting()
		{
		switch (ConsoleMux.RawIn)
			{
			case { } s when ReferenceEquals(s, Stream.Null): return "/dev/null";
			case FileStream { CanSeek: true } fs: return fs.Name;
			case FileStream fs: return ChildLauncher.HandleType(fs.SafeFileHandle.DangerousGetHandle()).Type == 2 ? "/dev/tty" : null;
			case { } s when PipeBuffer.IsEnd(s) || s is System.IO.Pipes.PipeStream: return StdinIsPipe;
			case null when ConsoleMux.InSlot is HereDocReader: return StdinIsPipe;
			case null when !ConsoleMux.InSwapped:
				{
				var h = ChildLauncher.StdHandle(0);
				return ChildLauncher.HandleType(h).Type switch
					{
					1 => ChildLauncher.HandlePath(h),
					2 => ChildLauncher.IsConsole(h) ? "/dev/tty" : "/dev/null",
					3 => StdinIsPipe,
					_ => null,
					};
				}
			default: return null;
			}
		}

	/// <summary><see cref="StdinForWriting"/>'s answer for a pipe or a here-document: `> /dev/stdin` is refused, "Permission
	/// denied", status 1 (bugs list 10's pipe case; the architect's go on Claude's position, 2026-11-27). GNU writes into the
	/// pipe through /proc; Windows cannot open a pipe's writing end from its reading end, so one answer for every pipe.</summary>
	public static readonly string StdinIsPipe = "\0pipe";

	/// <summary>What descriptor <paramref name="fd"/> is, with a file's size. This thread's stdin, stdout or stderr as
	/// installed: a C#Bash pipe and a `$( )` are pipes, /dev/null a character device, `&lt; file` a file, a here-document
	/// or here-string a pipe up to 65,536 bytes and a file beyond (bash 5.1's, measured 2026-11-27); else the process's
	/// own standard handle (a console or NUL is a character device). A numbered descriptor by its stream.</summary>
	private static (FdKind Kind, long Size) Describe(int fd)
		{
		switch (fd)
			{
			case 0:
				if (ConsoleMux.RawIn is { } rawIn) return OfStream(rawIn);
				if (ConsoleMux.InSlot is HereDocReader h) { long n = h.Bytes; return n <= 65536 ? (FdKind.Pipe, 0) : (FdKind.File, n); }
				return ConsoleMux.InSwapped ? (FdKind.Unknown, 0) : OfHandle(ChildLauncher.StdHandle(0));
			case 1:
				if (!ConsoleMux.OutSwapped) return OfHandle(ChildLauncher.StdHandle(1));
				return ConsoleMux.Raw is { } raw ? OfStream(raw) : ConsoleMux.Capturing ? (FdKind.Pipe, 0) : (FdKind.Unknown, 0);
			case 2:
				if (!ConsoleMux.ErrSwapped) return OfHandle(ChildLauncher.StdHandle(2));
				if (ReferenceEquals(ConsoleMux.ErrSlot, ConsoleMux.OutSlot)) return Describe(1);   // 2>&1
				return ConsoleMux.ErrSlot switch
					{
					var w when ReferenceEquals(w, TextWriter.Null) => (FdKind.Char, 0),
					StreamWriter sw => OfStream(sw.BaseStream),
					_ => (FdKind.Unknown, 0),
					};
			default:
				return ShellEnvironment.Active?.FdStream?.Invoke(fd) is { } s ? OfStream(s) : (FdKind.Unknown, 0);
			}
		}

	/// <summary>A stream's kind. A seekable FileStream is a file by its length (its handle is not exposed, which would
	/// slow every later read of it); an unseekable one (the console's CONIN$) by its handle.</summary>
	private static (FdKind Kind, long Size) OfStream(Stream s) => s switch
		{
		_ when ReferenceEquals(s, Stream.Null) => (FdKind.Char, 0),
		FileStream { CanSeek: true } fs => (FdKind.File, fs.Length),
		FileStream fs => OfHandle(fs.SafeFileHandle.DangerousGetHandle()),
		System.IO.Pipes.PipeStream => (FdKind.Pipe, 0),
		_ when PipeBuffer.IsEnd(s) => (FdKind.Pipe, 0),
		_ => (FdKind.Unknown, 0),
		};

	private static (FdKind Kind, long Size) OfHandle(IntPtr h) => ChildLauncher.HandleType(h) switch
		{
		(1, var size) => (FdKind.File, size),
		(2, _) => (FdKind.Char, 0),
		(3, _) => (FdKind.Pipe, 0),
		_ => (FdKind.Unknown, 0),
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
				// one file: the same volume and file index, as bash compares device and inode, so two names of one hard
				// link are one file (it compared full paths: false for those, 2026-11-01); the paths when one cannot be opened
				try
					{
					if (ShellFile.Identity(pa) is { } ia && ShellFile.Identity(pb) is { } ib) return ia.Volume == ib.Volume && ia.Index == ib.Index;
					return string.Equals(Path.GetFullPath(pa), Path.GetFullPath(pb), StringComparison.OrdinalIgnoreCase) && (File.Exists(pa) || Directory.Exists(pa));
					}
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
		try { return ShellFile.StartsWithShebang(path); }
		catch { return false; }
		}
	}
