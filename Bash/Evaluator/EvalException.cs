namespace Bash.Evaluator;

/// <summary>Runtime evaluation error (bad redirect, arithmetic fault, expansion error).
/// Reported as `bash: …` and, at statement level, turned into a failed command.</summary>
public class EvalException(string message) : Exception(message);

/// <summary>An error bash treats as fatal in a non-interactive shell (assignment to a
/// readonly variable, `${x:?}` with x unset): the script exits with <see cref="Code"/>;
/// an interactive shell just fails the command.</summary>
public sealed class FatalShellException(string message, int code = 1) : EvalException(message)
	{
	public int Code { get; } = code;
	}

/// <summary>Thrown by 'return' inside a function.</summary>
public sealed class ReturnException(int code) : Exception
	{
	public int Code { get; } = code;
	}

/// <summary>Thrown by 'break' inside a loop.</summary>
public sealed class BreakException(int levels = 1) : Exception
	{
	public int Levels { get; } = levels;
	}

/// <summary>Thrown by 'continue' inside a loop.</summary>
public sealed class ContinueException(int levels = 1) : Exception
	{
	public int Levels { get; } = levels;
	}

/// <summary>Thrown by 'exit'.</summary>
public sealed class ExitException(int code) : Exception
	{
	public int Code { get; } = code;
	}

/// <summary>Thrown when execution is interrupted by SIGINT (Ctrl+C). Exit code 130.</summary>
public sealed class InterruptException() : Exception("Interrupted");

/// <summary>
/// A redirection that could not be opened (missing directory, permission, AV block). bash reports
/// these and gives the command status 1, reserving 2 for syntax-level failures — so this carries
/// its own status rather than inheriting EvalException's 2 (2026-09-12).
/// </summary>
public sealed class RedirectException(string message) : EvalException(message);
