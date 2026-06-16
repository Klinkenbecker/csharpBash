namespace Bash.Lexer;

/// <param name="HasLeadingSpace">
/// True if at least one whitespace character preceded this token in the source.
/// The parser uses this to distinguish adjacent concatenation (echo"hi") from
/// separate words (echo "hi").
/// </param>
public sealed record Token(TokenType Type, string Value, int Line, int Column, bool HasLeadingSpace = false)
	{
	public static Token Eof(int line, int col) => new(TokenType.Eof, "", line, col);

	public override string ToString() => $"[{Type} {Value.Replace("\n", "\\n")!} @{Line}:{Column}]";
	}
