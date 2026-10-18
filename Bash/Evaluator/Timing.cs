using System.Diagnostics;
using System.Text;

namespace Bash.Evaluator;

/// <summary>
/// The performance review's timing switch (ratified 2026-10-16, DECISIONS "RATIFIED: the performance review
/// plan"). With BASH_TIMING_LOG naming a file, the shell notes when it reaches fixed points (Main, console set
/// up, the evaluator built, ready to run, done) and the regions it passes through (each `source`, each `eval`,
/// each external program from launch to exit), and appends them to that file when the process exits, one line
/// each: milliseconds since the process was CREATED, then the event. Without the variable: one environment
/// read at startup and a false bool tested at each point. Read by tests/bench/claudecall.sh.
/// </summary>
public static class Timing
	{
	/// <summary>True when BASH_TIMING_LOG is set: the marks are being kept.</summary>
	public static bool On { get; private set; }

	private static string _log = "";
	private static long _t0;
	private static DateTime _mainUtc;
	private static readonly List<(long At, string What)> _marks = [];

	/// <summary>Called first thing in Main.</summary>
	public static void Begin()
		{
		var log = Environment.GetEnvironmentVariable("BASH_TIMING_LOG");
		if (string.IsNullOrEmpty(log)) return;
		_t0 = Stopwatch.GetTimestamp();
		_mainUtc = DateTime.UtcNow;
		_log = log;
		On = true;
		AppDomain.CurrentDomain.ProcessExit += (_, _) => Write();
		}

	public static void Mark(string what)
		{
		if (!On) return;
		long now = Stopwatch.GetTimestamp();
		lock (_marks) _marks.Add((now, what));
		}

	private static void Write()
		{
		try
			{
			long end = Stopwatch.GetTimestamp();
			// before anything below: formatting a double and the process's local start time load ICU themselves
			bool icu = GetModuleHandleW("icu.dll") != IntPtr.Zero;
			// the OS's creation time of this process: Main's own offset from it is the runtime's start-up
			double main = (_mainUtc - Process.GetCurrentProcess().StartTime.ToUniversalTime()).TotalMilliseconds;
			double Ms(long ticks) => main + (ticks - _t0) * 1000.0 / Stopwatch.Frequency;
			var sb = new StringBuilder();
			sb.Append("pid ").Append(Environment.ProcessId).Append('\n');
			sb.Append(main.ToString("F3")).Append(" main\n");
			lock (_marks) foreach (var (at, what) in _marks) sb.Append(Ms(at).ToString("F3")).Append(' ').Append(what).Append('\n');
			// whether icu.dll (~0.7 ms a launch) was needed: it loads at the first culture-sensitive operation, and
			// a plain call has none (2026-10-18, review R4; case icu_lazy)
			sb.Append("icu ").Append(icu ? "yes" : "no").Append('\n');
			sb.Append(Ms(end).ToString("F3")).Append(" exit\n");
			File.AppendAllText(_log, sb.ToString());
			}
		catch (Exception) { }   // a timing log must never change how the shell exits
		}

	[System.Runtime.InteropServices.DllImport("kernel32.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode)]
	private static extern IntPtr GetModuleHandleW(string module);
	}
