namespace Bash.Evaluator;

/// <summary>Fatal evaluation error (syntax-level, not exit code).</summary>
public sealed class EvalException(string message) : Exception(message);

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
