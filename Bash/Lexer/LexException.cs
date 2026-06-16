namespace Bash.Lexer;

public sealed class LexException(string message, int line, int col)
	: Exception($"{message} at {line}:{col}")
	{
	public int Line { get; } = line;
	public int Col { get; } = col;

	/// <summary>True when lexing failed on unterminated input (e.g. an unclosed
	/// quote) — more input may complete it. Drives REPL multi-line continuation.</summary>
	public bool Incomplete { get; init; }
	}
