namespace Bash.Parser;

/// <summary>
/// bash's report of a syntax error (parse.y: report_syntax_error, parser_error), as the lines to
/// print after the error prefix, each with the line of the text it is reported at:
/// <list type="bullet">
/// <item>a token: ``syntax error near unexpected token `fi'``, then the source line quoted;</item>
/// <item>the end of the text: `syntax error: unexpected end of file`;</item>
/// <item>an unclosed quote, `$(`, `${` or backquote: ``unexpected EOF while looking for matching `)'``
/// at its line, then the end of file.</item>
/// </list>
/// Until 2026-10-10 C#Bash printed its own text (`Expected 'fi' but got '' at 5:1`), often at the
/// end of the file rather than at the token.
/// </summary>
public static class SyntaxMessage
	{
	public static List<(int Line, string Text)> For(Exception ex, string source)
		{
		// a last line with no newline still counts, as bash reads it (`-c 'echo a &&'` ends at line 2)
		int eofLine = 1 + source.Count(c => c == '\n') + (source.Length > 0 && source[^1] != '\n' ? 1 : 0);
		const string Eof = "syntax error: unexpected end of file";
		switch (ex)
			{
			case Lexer.LexException { Missing: { } m } lx:
				return [(lx.Line, $"unexpected EOF while looking for matching `{m}'"), (eofLine, Eof)];
			case Lexer.LexException lx:
				return [(lx.Line, lx.Reason)];
			case ParseException { Unsupported: true } px:
				return [(px.Line, px.Reason)];
			case ParseException { Missing: { } m, NoEofLine: true } px:
				return [(px.Line, $"unexpected EOF while looking for matching `{m}'")];
			case ParseException { Missing: { } m } px:
				return [(px.Line, $"unexpected EOF while looking for matching `{m}'"), (eofLine, Eof)];
			case ParseException { LookingFor: { } f } px:
				return [(px.Line, $"unexpected EOF while looking for `{f}'"), (eofLine, Eof)];
			case ParseException { Cond: true, At: { } t } px:
				{
				var lines = new List<(int, string)>();
				if (px.Lead is not null) lines.Add((t.Line, px.Lead));
				lines.Add((t.Line, $"syntax error near `{Shown(t)}'"));
				lines.Add((t.Line, $"`{LineText(source, t.Line)}'"));
				return lines;
				}
			case ParseException { At: { } t } when t.Type == Lexer.TokenType.Eof:
				return [(eofLine, Eof)];
			case ParseException { At: { } t }:
				return [(t.Line, $"syntax error near unexpected token `{Shown(t)}'"), (t.Line, $"`{LineText(source, t.Line)}'")];
			case ParseException px:
				return [(px.Line, px.Reason)];
			default:
				return [(0, ex.Message)];
			}
		}

	/// <summary>How bash names the token: a newline is `newline'.</summary>
	internal static string Shown(Lexer.Token t) => t.Type == Lexer.TokenType.Newline ? "newline" : t.Value;

	private static string LineText(string source, int line)
		{
		var lines = source.Split('\n');
		return line >= 1 && line <= lines.Length ? lines[line - 1].TrimEnd('\r') : "";
		}
	}
