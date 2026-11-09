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

	/// <summary>
	/// <paramref name="path"/> as an extended path (`\\?\C:\x`, `\\?\UNC\host\share\x`): Win32 then takes every name as
	/// written, so a file named nul, con, aux.txt or com1, or ending in a dot or a space, is that file and not a device.
	/// MSYS reaches such files this way (Git for Windows ships a dev/nul). The full path is the parent folder's, made full,
	/// joined with the final name as written: making the whole path full maps a final `nul` to `\\.\nul`, the device
	/// (guy-bf's `rm -rf`, 2026-11-07, "The parameter is incorrect. : '\\.\nul'").
	/// </summary>
	/// <summary>Whether Windows reads the name <paramref name="name"/> as a device: CON, PRN, AUX, NUL, COM1-9 and LPT1-9
	/// (and their superscript ¹²³ forms), CONIN$, CONOUT$, in any case, with any extension (`nul.txt`) and trailing dots
	/// or spaces.</summary>
	public static bool IsDeviceName(string name)
		{
		var stem = name.TrimEnd(' ', '.');
		int dot = stem.IndexOf('.');
		if (dot >= 0) stem = stem[..dot];
		stem = stem.TrimEnd(' ');
		if (stem.Length == 3)
			return stem.Equals("CON", StringComparison.OrdinalIgnoreCase) || stem.Equals("PRN", StringComparison.OrdinalIgnoreCase)
			    || stem.Equals("AUX", StringComparison.OrdinalIgnoreCase) || stem.Equals("NUL", StringComparison.OrdinalIgnoreCase);
		if (stem.Length == 4 && (stem.StartsWith("COM", StringComparison.OrdinalIgnoreCase) || stem.StartsWith("LPT", StringComparison.OrdinalIgnoreCase)))
			return stem[3] is >= '1' and <= '9' or '¹' or '²' or '³';
		return stem.Equals("CONIN$", StringComparison.OrdinalIgnoreCase) || stem.Equals("CONOUT$", StringComparison.OrdinalIgnoreCase);
		}

	public static string Extended(string path)
		{
		var p = path.Replace('/', '\\');
		if (p.StartsWith(@"\\?\", StringComparison.Ordinal) || p.StartsWith(@"\\.\", StringComparison.Ordinal)) return p;
		int cut = p.LastIndexOf('\\');
		bool driveRelative = cut < 0 && p.Length >= 2 && p[1] == ':';
		string name = cut >= 0 ? p[(cut + 1)..] : driveRelative ? p[2..] : p;
		string full = name is "" or "." or ".."
			? Path.GetFullPath(p.Length == 0 ? "." : p)
			: Path.Join(Path.GetFullPath(cut >= 0 ? p[..(cut + 1)] : driveRelative ? p[..2] : "."), name);
		return full.StartsWith(@"\\", StringComparison.Ordinal) ? @"\\?\UNC\" + full[2..] : @"\\?\" + full;
		}

	/// <summary>
	/// A file's identity as Windows keeps it (GetFileInformationByHandle): its volume's serial number (stat's device),
	/// its file index on that volume (the inode msys and Git Bash report) and its hard-link count; null when it cannot be
	/// opened. <paramref name="follow"/> false is lstat: a symbolic link is itself. Opened for attributes only, sharing
	/// everything, so a running program or an open file is no obstacle. Until 2026-11-01 every link count was 1 and every
	/// inode 0: `[ a -ef b ]` was false for two names of one file (installer-a4, confirming an installer-made hard link).
	/// </summary>
	public static (uint Volume, ulong Index, uint Links)? Identity(string path, bool follow = true)
		{
		using var h = CreateFileW(path, FileReadAttributes, 7 /* read, write, delete */, IntPtr.Zero, 3 /* OPEN_EXISTING */,
		                          BackupSemantics | (follow ? 0u : OpenReparsePoint), IntPtr.Zero);
		if (h.IsInvalid || !GetFileInformationByHandle(h, out var i)) return null;
		return (i.VolumeSerialNumber, ((ulong)i.FileIndexHigh << 32) | i.FileIndexLow, i.NumberOfLinks);
		}

	/// <summary>A file's hard-link count, read by NAME with no handle opened where Windows can (NtQueryInformationByName,
	/// FileStatInformation: Windows 10 1809 and later, as Cygwin reads it), else through <see cref="Identity"/>'s open;
	/// 1 when neither can read it. For `ls -l`'s link column (2026-11-05: it was 1 for every file).</summary>
	public static uint LinkCount(string path)
		{
		if (s_statByName)
			{
			// the NT form of a Win32 path: \??\C:\x, \??\UNC\server\share\x
			string nt = path.StartsWith(@"\\?\", StringComparison.Ordinal) ? @"\??\" + path[4..]
			          : path.StartsWith(@"\\", StringComparison.Ordinal) ? @"\??\UNC\" + path[2..] : @"\??\" + path;
			var chars = System.Runtime.InteropServices.GCHandle.Alloc(nt, System.Runtime.InteropServices.GCHandleType.Pinned);
			var name = new UnicodeString[] { new() { Length = (ushort)(nt.Length * 2), MaximumLength = (ushort)(nt.Length * 2), Buffer = chars.AddrOfPinnedObject() } };
			var pinned = System.Runtime.InteropServices.GCHandle.Alloc(name, System.Runtime.InteropServices.GCHandleType.Pinned);
			try
				{
				var oa = new ObjectAttributes { Length = System.Runtime.CompilerServices.Unsafe.SizeOf<ObjectAttributes>(), ObjectName = pinned.AddrOfPinnedObject(), Attributes = 0x40 /* OBJ_CASE_INSENSITIVE */ };
				int status = NtQueryInformationByName(ref oa, out _, out var stat, (uint)System.Runtime.CompilerServices.Unsafe.SizeOf<FileStatInformation>(), 68 /* FileStatInformation */);
				if (status >= 0) return stat.NumberOfLinks;
				if (status is unchecked((int)0xC0000003) or unchecked((int)0xC0000002)) s_statByName = false;   // invalid info class, not implemented: an older Windows
				}
			finally { pinned.Free(); chars.Free(); }
			}
		return Identity(path, follow: false)?.Links ?? 1;
		}

	private static bool s_statByName = true;

	[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
	private struct UnicodeString { public ushort Length, MaximumLength; public IntPtr Buffer; }

	[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
	private struct ObjectAttributes { public int Length; public IntPtr RootDirectory, ObjectName; public uint Attributes; public IntPtr SecurityDescriptor, SecurityQualityOfService; }

	[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
	private struct IoStatusBlock { public IntPtr Status, Information; }

	[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
	private struct FileStatInformation
		{
		public long FileId, CreationTime, LastAccessTime, LastWriteTime, ChangeTime, AllocationSize, EndOfFile;
		public uint FileAttributes, ReparseTag, NumberOfLinks, EffectiveAccess;
		}

	[System.Runtime.InteropServices.DllImport("ntdll.dll")]
	private static extern int NtQueryInformationByName(ref ObjectAttributes attributes, out IoStatusBlock io, out FileStatInformation info, uint length, int infoClass);

	private const uint FileReadAttributes = 0x80, BackupSemantics = 0x02000000, OpenReparsePoint = 0x00200000;

	[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
	private struct ByHandleFileInformation
		{
		public uint FileAttributes, CreationLow, CreationHigh, AccessLow, AccessHigh, WriteLow, WriteHigh;
		public uint VolumeSerialNumber, FileSizeHigh, FileSizeLow, NumberOfLinks, FileIndexHigh, FileIndexLow;
		}

	[System.Runtime.InteropServices.DllImport("kernel32.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode, SetLastError = true)]
	private static extern Microsoft.Win32.SafeHandles.SafeFileHandle CreateFileW(string name, uint access, uint share, IntPtr security, uint disposition, uint flags, IntPtr template);

	[System.Runtime.InteropServices.DllImport("kernel32.dll", SetLastError = true)]
	private static extern bool GetFileInformationByHandle(Microsoft.Win32.SafeHandles.SafeFileHandle file, out ByHandleFileInformation info);

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
