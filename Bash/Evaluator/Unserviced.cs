using System.Globalization;
using System.Text;

namespace Bash.Evaluator;

/// <summary>
/// The tracking log of requests C#Bash did not service itself, "a list of things to consider implementing" (the
/// architect, 2026-10-27; a `find` option reported unsupported had flown by unsaved): a builtin's unsupported option
/// (whether a PATH program then ran it or not), an awk program refused while it ran, a "not currently available"
/// feature, a construct refused when parsed, a bash builtin C#Bash lacks, and `help` on a topic it does not implement.
/// One JSON object per line in %USERPROFILE%/.bash/unserviced.jsonl, appended in one write through the shell's own
/// `>>` (shells in parallel sessions share the file). Best effort: a log that cannot be written never fails the
/// command. The full arguments are kept: the file stays in the user's profile, as bash's history does (so a secret
/// passed to an unsupported command lands there too).
/// </summary>
public static class Unserviced
	{
	/// <summary>%USERPROFILE%/.bash (the architect, 2026-10-27, for this log and help.json): the USERPROFILE variable as
	/// the shell has it (a test points it at a folder of its own, as command_map.sh does HOME), else the Windows profile;
	/// null when there is neither.</summary>
	public static string? Folder
		{
		get
			{
			var profile = Variable("USERPROFILE") is { Length: > 0 } v ? ShellEnvironment.TranslatePath(v)
			            : Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
			return string.IsNullOrEmpty(profile) ? null : Path.Combine(profile, ".bash");
			}
		}

	/// <summary>$BASH_UNSERVICED_LOG when set, `/dev/null` meaning no log (the test runners set that: a test run's
	/// deliberate unsupported options are not requests); else unserviced.jsonl in <see cref="Folder"/>.</summary>
	public static string? LogFile =>
		Variable("BASH_UNSERVICED_LOG") is { Length: > 0 } set ? (set == "/dev/null" ? null : ShellEnvironment.TranslatePath(set))
		: Folder is { } dir ? Path.Combine(dir, "unserviced.jsonl") : null;

	/// <summary>A variable as the shell has it, else as the process has it (no shell yet).</summary>
	private static string? Variable(string name) =>
		ShellEnvironment.Active is { } env ? env.Get(name) : Environment.GetEnvironmentVariable(name);

	/// <summary>Record one request: the <paramref name="tool"/> (a command, or "bash" for the shell itself), the
	/// <paramref name="feature"/> it lacked, the <paramref name="message"/> shown, the command's arguments, and the
	/// <paramref name="outcome"/>.</summary>
	public static void Log(string tool, string feature, string message, IReadOnlyList<string>? args, string outcome)
		{
		try
			{
			if (LogFile is not { } file) return;
			Directory.CreateDirectory(Path.GetDirectoryName(file)!);
			var env = ShellEnvironment.Active;
			var source = env?.Get("BASH_SOURCE") is { Length: > 0 } s ? s : env?.Get("0") ?? "";
			var sb = new StringBuilder("{");
			Field(sb, "time", DateTime.UtcNow.ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'", CultureInfo.InvariantCulture));
			Field(sb, "build", BuildInfo.Stamp);
			Field(sb, "tool", tool);
			Field(sb, "feature", feature);
			Field(sb, "message", message);
			if (args is not null)
				{
				sb.Append(",\"args\":[");
				for (int i = 0; i < args.Count; i++) { if (i > 0) sb.Append(','); Json.Quote(sb, Text(args[i]), ascii: false); }
				sb.Append(']');
				}
			Field(sb, "outcome", outcome);
			Field(sb, "source", source);
			sb.Append(",\"line\":").Append(env?.CurrentLine ?? 0).Append("}\n");
			var bytes = Encoding.UTF8.GetBytes(sb.ToString());
			using var fs = ShellFile.Append(file);
			fs.Write(bytes, 0, bytes.Length);   // one write: whole lines, whoever else appends
			}
		catch { }
		}

	private static void Field(StringBuilder sb, string name, string value)
		{
		if (sb.Length > 1) sb.Append(',');
		sb.Append('"').Append(name).Append("\":");
		Json.Quote(sb, Text(value), ascii: false);
		}

	/// <summary>A carried byte (not UTF-8) as U+FFFD: the file is UTF-8 that any JSON reader takes.</summary>
	private static string Text(string s) => ShellEncoding.ForConsole(s);
	}
