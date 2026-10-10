namespace Bash.Evaluator;

/// <summary>Runtime evaluation error (bad redirect, arithmetic fault, expansion error).
/// Reported after <see cref="Prefix"/> and, at statement level, turned into a failed command.</summary>
public class EvalException(string message) : Exception(message)
	{
	/// <summary>bash's error prefix where it was raised (`script: line N: `). It is printed where it
	/// is caught, after the stack has unwound and the function's file and line are gone.</summary>
	public string? Prefix { get; } = ShellEnvironment.Active?.ErrorPrefixProvider?.Invoke();
	}

/// <summary>An error bash treats as fatal in a non-interactive shell (assignment to a
/// readonly variable, `${x:?}` with x unset): the script exits with <see cref="Code"/>;
/// an interactive shell just fails the command.</summary>
public sealed class FatalShellException(string message, int code = 1) : EvalException(message)
	{
	public int Code { get; } = code;
	/// <summary>An expansion error (`set -u` on an unset name, `${x:?}`) rather than an assignment
	/// one: bash ends a `-c` shell with 127 for these, and only fails a here-document's command.</summary>
	public bool ExpansionError { get; init; }
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
	/// <summary>The shell is ending on an expansion error (see FatalShellException.ExpansionError).</summary>
	public bool ExpansionError { get; init; }
	/// <summary>`set -e` was in force where it happened: on, and not suspended by `&&`/`||`/`if`.</summary>
	public bool ErrexitInForce { get; init; }
	}

/// <summary>Thrown when execution is interrupted by SIGINT (Ctrl+C). Exit code 130.</summary>
public sealed class InterruptException() : Exception("Interrupted");

/// <summary>
/// A redirection that could not be opened (missing directory, permission, AV block). bash reports
/// these and gives the command status 1, reserving 2 for syntax-level failures — so this carries
/// its own status rather than inheriting EvalException's 2 (2026-09-12).
/// </summary>
public sealed class RedirectException(string message, bool reported = false) : EvalException(message)
	{
	/// <summary>Already written to the stderr in effect where it failed (ApplyRedirects does that).</summary>
	public bool Reported { get; } = reported;
	}
