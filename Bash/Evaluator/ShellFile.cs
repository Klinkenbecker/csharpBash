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
		using var fs = OpenRead(path);
		using var ms = new MemoryStream();
		fs.CopyTo(ms);
		return ms.ToArray();
		}
	}
