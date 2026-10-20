namespace Bash.Evaluator;

/// <summary>
/// Files opened the way a POSIX shell's are: never locking another reader, writer or `rm` out.
/// Windows' defaults lock: File.OpenRead shares reading only, so it fails on a file another
/// process is writing, and File.Create / File.Open share nothing. So `cat log` failed while
/// `server &gt; log &amp;` ran (DECISIONS 2026-10-04, the redirect rewrite). Every file the shell
/// opens for a command goes through here.
/// </summary>
public static class ShellFile
	{
	public const FileShare Share = FileShare.ReadWrite | FileShare.Delete;

	/// <summary>
	/// /dev/tty exists only when the console has a window: Windows Terminal (ConPTY) and a classic console
	/// have one, even hidden; the console Claude Code gives its shell has none (CREATE_NO_WINDOW), and nor does
	/// anything that shell starts. Reading a windowless console waits for typing that cannot come: git's
	/// credential prompt (`bash -c 'cat >/dev/tty && read -r line </dev/tty …'`, which finds C#Bash as `bash`)
	/// hung the call to its timeout. Without a window /dev/tty is bash's "No such device or address", as GNU
	/// bash with no controlling terminal and Git Bash in the same place give it at once (measured 2026-10-15,
	/// GetConsoleWindow under ConPTY, a hidden classic console, Claude Code's shell and a child of git).
	/// </summary>
	public static bool TtyAvailable => GetConsoleWindow() != IntPtr.Zero;

	/// <summary>The console (CONIN$ or CONOUT$) for /dev/tty, or "No such device or address" without a terminal.</summary>
	public static Microsoft.Win32.SafeHandles.SafeFileHandle OpenTty(string device, ChildLauncher.Access how) =>
		TtyAvailable ? ChildLauncher.Open(device, how) : throw new IOException("No such device or address");

	[System.Runtime.InteropServices.DllImport("kernel32.dll")] private static extern IntPtr GetConsoleWindow();

	public static FileStream OpenRead(string path) => NotADirectory(path, () => new(path, FileMode.Open, FileAccess.Read, Share));

	/// <summary>`&gt; file`: created, or truncated.</summary>
	public static FileStream Create(string path) => NotADirectory(path, () => new(path, FileMode.Create, FileAccess.Write, Share));

	/// <summary>True when a directory on the way to <paramref name="path"/> is a file (bash's ENOTDIR).</summary>
	public static bool ThroughAFile(string path)
		{
		try
			{
			for (var p = Path.GetDirectoryName(path); !string.IsNullOrEmpty(p); p = Path.GetDirectoryName(p))
				if (File.Exists(p)) return true;
			}
		catch { }
		return false;
		}

	/// <summary>An open whose path runs through a file fails as bash's does, "Not a directory"; .NET says the directory
	/// is not found, reported as "No such file or directory": `cat file/x` (red team F13, 2026-10-19).</summary>
	private static FileStream NotADirectory(string path, Func<FileStream> open)
		{
		// `file/.` and `file/..` open `file` and its folder, Windows dropping the dots: asked first
		if (Path.GetFileName(path) is "." or ".." && ThroughAFile(path)) throw new IOException("Not a directory");
		try { return open(); }
		catch (DirectoryNotFoundException) when (ThroughAFile(path)) { throw new IOException("Not a directory"); }
		}

	/// <summary>`&gt;&gt; file`: created if missing, written at the end.</summary>
	/// <summary>`>>`: every write lands at the end, whoever else is appending (O_APPEND): the handle has FILE_APPEND_DATA and
	/// not FILE_WRITE_DATA, as a child's `>>` handle (ChildLauncher.Access.Append). An ordinary write stream opened at the
	/// end wrote at its own offset, so two shells appending to one file wrote over each other (3000 of 6000 lines were
	/// left; red team, 2026-10-19). Unbuffered: a line is one write.</summary>
	public static FileStream Append(string path)
		{
		var fs = NotADirectory(path, () => new FileStream(ChildLauncher.Open(path, ChildLauncher.Access.Append), FileAccess.Write, bufferSize: 0));
		s_paths.AddOrUpdate(fs, Path.GetFullPath(path));
		return fs;
		}

	/// <summary>The file a stream writes: its Name, or for a stream made from a handle (Append) the path it was opened
	/// with; .NET names such a stream "[Unknown]", and a child's handle reopened by that name went to a new file.</summary>
	public static string PathOf(FileStream fs) => s_paths.TryGetValue(fs, out var p) ? p : fs.Name;
	private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<FileStream, string> s_paths = new();

	public static byte[] ReadAllBytes(string path)
		{
		using var fs = OpenInput(path);
		using var ms = new MemoryStream();
		fs.CopyTo(ms);
		return ms.ToArray();
		}

	// ── device files ─────────────────────────────────────────────────────────────
	// Linux's, which scripts take for granted and Windows has none of at those paths: until
	// 2026-10-10 `cat /dev/null`, `head -c 5 /dev/zero`, `< /dev/urandom`, `cat /dev/fd/3` and
	// `[ -e /dev/null ]` all failed ("No such file or directory"). Only the redirect code knew
	// /dev/null, /dev/stdout and /dev/stderr.

	/// <summary>The device a path names: "null", "zero", "random" (/dev/random and /dev/urandom),
	/// "tty", or "fd:N" (/dev/stdin, /dev/stdout, /dev/stderr are fd:0, fd:1, fd:2); null for any
	/// other path.</summary>
	public static string? Device(string path) => path switch
		{
		"/dev/null" => "null",
		"/dev/zero" => "zero",
		"/dev/random" or "/dev/urandom" => "random",
		"/dev/tty" => "tty",
		"/dev/stdin" => "fd:0",
		"/dev/stdout" => "fd:1",
		"/dev/stderr" => "fd:2",
		_ when path.StartsWith("/dev/fd/", StringComparison.Ordinal) && int.TryParse(path.AsSpan(8), System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out int n) && n >= 0 => $"fd:{n}",
		_ => null,
		};

	/// <summary>The descriptor number of a /dev/fd/N, /dev/stdin, /dev/stdout or /dev/stderr, else -1.</summary>
	public static int DeviceFd(string path) => Device(path) is { } d && d.StartsWith("fd:", StringComparison.Ordinal) ? int.Parse(d.AsSpan(3), System.Globalization.CultureInfo.InvariantCulture) : -1;

	/// <summary>The path with its file name as it is on disk: a PATHEXT match spells it `git.EXE`, the file
	/// is `git.exe`. The directory part is kept as given; an unreadable directory leaves the path as it is.</summary>
	public static string OnDiskName(string path)
		{
		try
			{
			var dir = Path.GetDirectoryName(path);
			var file = Path.GetFileName(path);
			if (!string.IsNullOrEmpty(dir) && file.Length > 0 && file.IndexOfAny(['*', '?']) < 0)
				{
				var onDisk = new DirectoryInfo(dir).EnumerateFiles(file).FirstOrDefault()?.Name;
				if (onDisk is not null && onDisk != file && string.Equals(onDisk, file, StringComparison.OrdinalIgnoreCase))
					return Path.Combine(dir, onDisk);
				}
			}
		catch (Exception) { }
		return path;
		}

	/// <summary>A file, a directory or a device exists at the path.</summary>
	public static bool Exists(string path) => Device(path) is not null || File.Exists(path);

	/// <summary>A file or a device, opened for reading.</summary>
	public static Stream OpenInput(string path) => Device(path) switch
		{
		null => OpenRead(path),
		"null" => Stream.Null,
		"zero" => new ZeroStream(),
		"random" => new RandomStream(),
		"tty" => new FileStream(OpenTty("CONIN$", ChildLauncher.Access.Read), FileAccess.Read),
		"fd:0" => Evaluator.CurrentRawStdin() ?? Console.OpenStandardInput(),
		var fd => ShellEnvironment.Active?.FdStream?.Invoke(DeviceFd(path)) is { CanRead: true } s ? s
		        : throw new IOException($"{path}: Bad file descriptor"),
		};

	/// <summary>Reads zeros, forever.</summary>
	private sealed class ZeroStream : Stream
		{
		public override bool CanRead => true;
		public override bool CanSeek => false;
		public override bool CanWrite => false;
		public override long Length => throw new NotSupportedException();
		public override long Position { get => 0; set => throw new NotSupportedException(); }
		public override int Read(byte[] buffer, int offset, int count) { Array.Clear(buffer, offset, count); return count; }
		public override void Flush() { }
		public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
		public override void SetLength(long value) => throw new NotSupportedException();
		public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
		}

	/// <summary>Reads cryptographic random bytes, forever (/dev/random and /dev/urandom).</summary>
	private sealed class RandomStream : Stream
		{
		public override bool CanRead => true;
		public override bool CanSeek => false;
		public override bool CanWrite => false;
		public override long Length => throw new NotSupportedException();
		public override long Position { get => 0; set => throw new NotSupportedException(); }
		public override int Read(byte[] buffer, int offset, int count)
			{ System.Security.Cryptography.RandomNumberGenerator.Fill(buffer.AsSpan(offset, count)); return count; }
		public override void Flush() { }
		public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
		public override void SetLength(long value) => throw new NotSupportedException();
		public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
		}
	}
