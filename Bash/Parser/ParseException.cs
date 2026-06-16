namespace Bash.Parser;

public sealed class ParseException(string message, int line, int col)
	: Exception($"{message} at {line}:{col}")
	{
	public int Line { get; } = line;
	public int Col { get; } = col;

	/// <summary>True when the parse failed by running out of input (current token
	/// was EOF) — i.e. the command is unfinished and more input may complete it.
	/// The REPL uses this to drive multi-line continuation.</summary>
	public bool Incomplete { get; init; }
	}
