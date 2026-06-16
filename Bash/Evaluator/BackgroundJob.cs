namespace Bash.Evaluator;

/// <summary>
/// A background job started with `&`. Because this interpreter has no fork, a job is
/// a thread running the command's AST in-process, sharing the shell environment and
/// console with the foreground — fine for external commands and simple pipelines,
/// but internal state mutation races the foreground (documented limitation).
/// </summary>
public sealed class BackgroundJob
	{
	public int Id { get; init; }
	public string Command { get; init; } = "";
	public Thread Thread { get; set; } = null!;
	public volatile bool Done;
	public int ExitCode;
	}
