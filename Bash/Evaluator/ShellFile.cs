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

	public static FileStream OpenRead(string path) => new(path, FileMode.Open, FileAccess.Read, Share);

	/// <summary>`&gt; file`: created, or truncated.</summary>
	public static FileStream Create(string path) => new(path, FileMode.Create, FileAccess.Write, Share);

	/// <summary>`&gt;&gt; file`: created if missing, written at the end.</summary>
	public static FileStream Append(string path) => new(path, FileMode.Append, FileAccess.Write, Share);

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
		_ when path.StartsWith("/dev/fd/") && int.TryParse(path.AsSpan(8), out int n) && n >= 0 => $"fd:{n}",
		_ => null,
		};

	/// <summary>The descriptor number of a /dev/fd/N, /dev/stdin, /dev/stdout or /dev/stderr, else -1.</summary>
	public static int DeviceFd(string path) => Device(path) is { } d && d.StartsWith("fd:") ? int.Parse(d.AsSpan(3)) : -1;

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
		"tty" => new FileStream(ChildLauncher.Open("CONIN$", ChildLauncher.Access.Read), FileAccess.Read),
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
