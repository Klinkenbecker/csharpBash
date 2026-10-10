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

	/// <summary>The message without its position.</summary>
	public string Reason { get; } = message;

	/// <summary>The token the parse failed at. bash's report names it ("near unexpected token
	/// `fi'"), or says "unexpected end of file" when it is the end (SyntaxMessage).</summary>
	public Lexer.Token? At { get; init; }

	/// <summary>An unclosed construct: the character bash says it was looking for (`)', `` ` ``).</summary>
	public char? Missing { get; init; }

	/// <summary>Not a syntax error but a construct this interpreter does not support: reported in its
	/// own words.</summary>
	public bool Unsupported { get; init; }

	/// <summary>A `[[ ]]` error (parse.y, cond_error): an optional first line (<see cref="Lead"/>), then
	/// "syntax error near `X'" (not "near unexpected token") and the source line.</summary>
	public bool Cond { get; init; }
	public string? Lead { get; init; }

	/// <summary>The end of the text came inside a construct bash names without "matching":
	/// "unexpected EOF while looking for `]]'".</summary>
	public string? LookingFor { get; init; }

	/// <summary>No "unexpected end of file" line after the "looking for matching" one (bash, for an
	/// array literal `x=(a b`).</summary>
	public bool NoEofLine { get; init; }

	/// <summary>The status this error gives, when not the caller's (bash: 1 for an array literal's).</summary>
	public int? Status { get; init; }
	}
