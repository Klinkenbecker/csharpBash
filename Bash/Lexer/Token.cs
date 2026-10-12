namespace Bash.Lexer;

/// <param name="HasLeadingSpace">
/// True if at least one whitespace character preceded this token in the source.
/// The parser uses this to distinguish adjacent concatenation (echo"hi") from
/// separate words (echo "hi").
/// </param>
public sealed record Token(TokenType Type, string Value, int Line, int Column, bool HasLeadingSpace = false)
	{
	public static Token Eof(int line, int col) => new(TokenType.Eof, "", line, col);

	/// <summary>Where the token's text starts and ends in the lexer's source (-1 when unknown): what
	/// `$BASH_COMMAND` is rendered from (DECISIONS 2026-10-11, DEBUG and RETURN traps).</summary>
	public int Start { get; init; } = -1;
	public int End { get; init; } = -1;

	public override string ToString() => $"[{Type} {Value.Replace("\n", "\\n")!} @{Line}:{Column}]";
	}
