namespace Bash.Evaluator;

/// <summary>
/// A background job started with `&`. Because this interpreter has no fork, a job is
/// a thread running the command's AST in-process, sharing the shell environment and
/// console with the foreground — fine for external commands and simple pipelines,
/// but internal state mutation races the foreground (documented limitation).
/// </summary>
public sealed class BackgroundJob
	{
	/// <summary>Synthetic "pid" base: job ids are offset into a range no real Windows
	/// process id reaches, so `$!`, `wait $!` and `kill $!` can tell a job from a pid.</summary>
	public const int PidBase = 0x40000000;

	public int Id { get; init; }
	public string Command { get; init; } = "";
	public Thread Thread { get; set; } = null!;
	public volatile bool Done;
	public int ExitCode;

	/// <summary>The value of `$!` for this job.</summary>
	public int Pid => PidBase + Id;

	/// <summary>Real pid of the external child this job is currently running, if any
	/// (set by ExecExternal on the job's thread) — the target of `kill $!`.</summary>
	public volatile int ChildPid;

	public static bool IsSyntheticPid(int pid) => pid >= PidBase;
	public static int JobIdFromPid(int pid) => pid - PidBase;
	}
