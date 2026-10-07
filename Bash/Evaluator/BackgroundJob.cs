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

	/// <summary>A `cmd &` job: its external children outlive the shell, as in bash. False for the
	/// job object `timeout` runs its command under, whose child must still die with the shell.</summary>
	public bool Background { get; init; }
	public Thread Thread { get; set; } = null!;
	public int ExitCode;

	/// <summary>The job's own copy of the shell (bash forks one for `cmd &amp;`). `kill %n` interrupts
	/// it, so an in-process loop ends too, not just the program it is running.</summary>
	public Evaluator? Shell { get; set; }

	/// <summary>The value of `$!` for this job.</summary>
	public int Pid => PidBase + Id;

	// Done, ChildPid and Parked are written under _gate and pulse it, so a waiter (WaitSettled)
	// blocks on a signal instead of polling.
	private readonly object _gate = new();
	private volatile bool _done, _parked;
	private volatile int _childPid;

	public bool Done { get => _done; set { lock (_gate) { _done = value; Monitor.PulseAll(_gate); } } }

	/// <summary>Real pid of the external child this job is currently running, if any
	/// (set by ExecExternal on the job's thread) — the target of `kill $!`.</summary>
	public int ChildPid { get => _childPid; set { lock (_gate) { _childPid = value; Monitor.PulseAll(_gate); } } }

	/// <summary>True while the job sits in the `sleep` builtin: it is not about to start a program,
	/// so shell exit need not wait for one.</summary>
	public bool Parked { get => _parked; set { lock (_gate) { _parked = value; Monitor.PulseAll(_gate); } } }

	/// <summary>Block until the job has started an external child, parked in `sleep`, or ended;
	/// false if <paramref name="timeoutMs"/> ran out first.</summary>
	public bool WaitSettled(int timeoutMs)
		{
		long deadline = Environment.TickCount64 + timeoutMs;
		lock (_gate)
			while (!_done && _childPid == 0 && !_parked)
				{
				long left = deadline - Environment.TickCount64;
				if (left <= 0 || !Monitor.Wait(_gate, (int)left)) return false;
				}
		return true;
		}

	public static bool IsSyntheticPid(int pid) => pid >= PidBase;
	public static int JobIdFromPid(int pid) => pid - PidBase;
	}
