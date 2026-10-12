using System.Text;
using Bash.Lexer;

namespace Bash.Parser;

/// <summary>
/// bash's rendering of a command for `$BASH_COMMAND` (its print_cmd.c), from the command's source text,
/// made only when the variable is read (DECISIONS 2026-10-11, DEBUG and RETURN traps). Measured against
/// bash 5.1: words as written with one space between them (`echo   a` is `echo a`); a redirect as
/// `> target`, but a descriptor copy and a here-document's operator take no space (`2>&1`, `<<EOT`); a
/// here-document's body follows the command (`cat <<EOT\nhd\nEOT\n`); `$'s'` shows as `'s'`. Text inside
/// `$( )`, `$(( ))`, backquotes or a group is kept as written.
/// </summary>
public static class CommandText
	{
	public static string Render(SourceSpan span)
		{
		if (!span.IsKnown) return "";
		var text = span.Text;
		List<Token> toks;
		try { toks = new Lexer.Lexer(text).Tokenize(); }
		catch (LexException) { return string.Join(" ", text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)); }

		var pieces = new List<string>();
		var bodies = new StringBuilder();
		var delimiters = new Queue<string>();
		int i = 0;
		while (i < toks.Count)
			{
			var t = toks[i];
			if (t.Type == TokenType.Eof) break;
			if (t.Type == TokenType.Newline) { i++; continue; }
			if (t.Type == TokenType.HeredocBody)
				{
				var delim = delimiters.Count > 0 ? delimiters.Dequeue() : "";
				bodies.Append('\n').Append(t.Value).Append(delim);
				i++;
				continue;
				}
			// a redirect, with the descriptor digit glued to it (`2>&1`, `2> f`)
			int opAt = t.Type == TokenType.Digit && i + 1 < toks.Count && IsRedirect(toks[i + 1].Type) && !toks[i + 1].HasLeadingSpace ? i + 1 : i;
			if (IsRedirect(toks[opAt].Type))
				{
				var op = toks[opAt];
				string fd = opAt > i ? t.Value : "";
				i = opAt + 1;
				var target = Word(toks, ref i, text, first: true);
				bool glued = op.Type is TokenType.GreaterAmpersand or TokenType.LessAmpersand or TokenType.LessLess or TokenType.LessLessDash;
				pieces.Add(fd + text[op.Start..op.End] + (glued ? "" : " ") + target);
				if (op.Type is TokenType.LessLess or TokenType.LessLessDash) delimiters.Enqueue(Unquote(target));
				continue;
				}
			pieces.Add(Word(toks, ref i, text, first: true));
			}
		var line = string.Join(" ", pieces.Where(p => p.Length > 0));
		return bodies.Length > 0 ? line + bodies + "\n" : line;
		}

	private static bool IsRedirect(TokenType t) => t is TokenType.Less or TokenType.Greater or TokenType.GreaterGreater
		or TokenType.GreaterAmpersand or TokenType.LessAmpersand or TokenType.LessLess or TokenType.LessLessDash
		or TokenType.LessLessLess or TokenType.LessGreater or TokenType.GreaterPipe or TokenType.AmpersandGreater
		or TokenType.AmpersandGreaterGreater;

	/// <summary>One word: tokens glued without space, and everything inside an open `$( )`, `(`, or
	/// backquote whatever its spacing; as written, except `$'…'` shown as `'…'`.</summary>
	private static string Word(List<Token> toks, ref int i, string text, bool first)
		{
		var sb = new StringBuilder();
		int depth = 0;
		bool inBackquote = false;
		while (i < toks.Count)
			{
			var t = toks[i];
			if (t.Type is TokenType.Eof or TokenType.HeredocBody) break;
			bool nested = depth > 0 || inBackquote;
			if (!nested && sb.Length > 0 && (t.HasLeadingSpace || t.Type == TokenType.Newline)) break;
			if (!nested && (t.Type == TokenType.Newline || (sb.Length > 0 && IsRedirect(t.Type)))) break;
			if (nested && t.HasLeadingSpace) sb.Append(' ');
			if (t.Type == TokenType.DollarSingleQuote) sb.Append('\'').Append(t.Value).Append('\'');
			else if (t.Start >= 0 && t.End <= text.Length && t.End >= t.Start) sb.Append(text, t.Start, t.End - t.Start);
			else sb.Append(t.Value);
			if (t.Type is TokenType.DollarLParen or TokenType.LParen or TokenType.LessLParen or TokenType.GreaterLParen) depth++;
			else if (t.Type == TokenType.DollarDollarLParen) depth += 2;
			else if (t.Type == TokenType.RParen && depth > 0) depth--;
			else if (t.Type == TokenType.Backtick) inBackquote = !inBackquote;
			i++;
			}
		return sb.ToString();
		}

	private static string Unquote(string delim) => delim.Replace("'", "").Replace("\"", "").Replace("\\", "");
	}
