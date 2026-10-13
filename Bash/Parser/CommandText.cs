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
		var line = RenderParts(span, out var bodies);
		return bodies.Count > 0 ? line + "\n" + string.Concat(bodies) : line;
		}

	/// <summary>The command's line and, separately, its here-documents' bodies, each `body…DELIM\n` (the
	/// command printer emits them at the next newline, as bash does: after `cat <<EOT |`, before the next
	/// stage). A descriptor copy without a number gets bash's explicit one (`>&2` is `1>&2`, `<&3` is
	/// `0<&3`; measured, `declare -f`, 2026-10-12).</summary>
	public static string RenderParts(SourceSpan span, out List<string> bodies)
		{
		bodies = [];
		if (!span.IsKnown) return "";
		var text = span.Text;
		List<Token> toks;
		try { toks = new Lexer.Lexer(text).Tokenize(); }
		catch (LexException) { return string.Join(" ", text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)); }

		var pieces = new List<string>();
		var delimiters = new Queue<string>();
		// A command's span runs to the end of its here-document bodies, so it can take in the rest of its
		// line (`cat <<EOT | sort`): an operator at the top level ends the command, and after it only this
		// command's bodies count. Inside `[[ ]]` `&&` and `||` belong to the expression.
		bool cond = toks.Count > 0 && toks[0].Value == "[[";
		bool outside = false;
		int i = 0;
		while (i < toks.Count)
			{
			var t = toks[i];
			if (t.Type == TokenType.Eof) break;
			if (t.Type == TokenType.HeredocBody)
				{
				if (delimiters.Count > 0) bodies.Add(t.Value + delimiters.Dequeue() + "\n");
				i++;
				continue;
				}
			if (outside || t.Type == TokenType.Newline) { i++; continue; }
			if (!cond && t.Type is TokenType.Pipe or TokenType.PipeAmpersand or TokenType.AmpersandAmpersand or TokenType.PipePipe
			    or TokenType.Semicolon or TokenType.Ampersand)
				{ outside = true; i++; continue; }
			// a redirect, with the descriptor digit glued to it (`2>&1`, `2> f`)
			int opAt = t.Type == TokenType.Digit && i + 1 < toks.Count && IsRedirect(toks[i + 1].Type) && !toks[i + 1].HasLeadingSpace ? i + 1 : i;
			if (IsRedirect(toks[opAt].Type))
				{
				var op = toks[opAt];
				string fd = opAt > i ? t.Value : op.Type == TokenType.GreaterAmpersand ? "1" : op.Type == TokenType.LessAmpersand ? "0" : "";
				i = opAt + 1;
				var target = Word(toks, ref i, text, first: true);
				bool glued = op.Type is TokenType.GreaterAmpersand or TokenType.LessAmpersand or TokenType.LessLess or TokenType.LessLessDash;
				pieces.Add(fd + text[op.Start..op.End] + (glued ? "" : " ") + target);
				if (op.Type is TokenType.LessLess or TokenType.LessLessDash) delimiters.Enqueue(Unquote(target));
				continue;
				}
			pieces.Add(Word(toks, ref i, text, first: true));
			}
		return string.Join(" ", pieces.Where(p => p.Length > 0));
		}

	/// <summary>A word as bash prints it: as written, but `$'…'` decoded into single quotes (a `'` inside
	/// written `'\''`).</summary>
	public static string PrintWord(string raw) => StripContinuations(raw);

	private static bool IsRedirect(TokenType t) => t is TokenType.Less or TokenType.Greater or TokenType.GreaterGreater
		or TokenType.GreaterAmpersand or TokenType.LessAmpersand or TokenType.LessLess or TokenType.LessLessDash
		or TokenType.LessLessLess or TokenType.LessGreater or TokenType.GreaterPipe or TokenType.AmpersandGreater
		or TokenType.AmpersandGreaterGreater;

	/// <summary>One word: tokens glued without space; a `$( )`, `<( )`, `>( )`, `(…)` or backquoted part is
	/// copied from the source as written, to its matching close (bash keeps a substitution's text, here-
	/// documents and all); `$'…'` is shown as `'…'`.</summary>
	private static string Word(List<Token> toks, ref int i, string text, bool first, int end = int.MaxValue)
		{
		var sb = new StringBuilder();
		while (i < toks.Count && i < end)
			{
			var t = toks[i];
			if (t.Type is TokenType.Eof or TokenType.HeredocBody) break;
			if (sb.Length > 0 && (t.HasLeadingSpace || t.Type == TokenType.Newline)) break;
			if (t.Type == TokenType.Newline || (sb.Length > 0 && IsRedirect(t.Type))) break;
			// an array literal (`a=( x  y )`): its elements, one space apart, as bash prints them
			if (t.Type == TokenType.LParen && sb.Length > 0 && sb[^1] == '=' && MatchingClose(toks, i) is var aclose and > 0)
				{
				var elems = new List<string>();
				int k = i + 1;
				while (k < aclose)
					{
					if (toks[k].Type == TokenType.Newline) { k++; continue; }
					var w = Word(toks, ref k, text, first: false, end: aclose);
					if (w.Length > 0) elems.Add(w); else k++;
					}
				sb.Append('(').Append(string.Join(" ", elems)).Append(')');
				i = aclose + 1;
				continue;
				}
			if (t.Type is TokenType.DollarLParen or TokenType.LParen or TokenType.LessLParen or TokenType.GreaterLParen
			    or TokenType.DollarDollarLParen or TokenType.Backtick
			    && MatchingClose(toks, i) is var close and > 0 && Within(t, text) && Within(toks[close], text))
				{
				sb.Append(StripContinuations(text.Substring(t.Start, toks[close].End - t.Start)));
				i = close + 1;
				continue;
				}
			if (t.Type == TokenType.DollarSingleQuote)
				{
				var v = t.Value;
				if (v.IndexOf('\0') is var z and >= 0) v = v[..z];   // a NUL ends the string, as in bash's C strings
				sb.Append('\'').Append(v.Replace("'", @"'\''")).Append('\'');
				}
			else if (Within(t, text)) sb.Append(StripContinuations(text.Substring(t.Start, t.End - t.Start)));
			else sb.Append(t.Value);
			i++;
			}
		return sb.ToString();
		}

	/// <summary>Source text as bash keeps it in a word or a substitution (its reader's work): line
	/// continuations (`\` + newline) taken out, except inside single quotes; `$'…'` decoded into single
	/// quotes, except inside double quotes (`$(… $'\t' …)` keeps `'&lt;tab&gt;'`). Quoting starts afresh
	/// inside each `$( )`, so a single-quoted part in a `"$( )"` keeps its continuations (measured on the
	/// bash-completion corpus, 2026-10-12).</summary>
	private static string StripContinuations(string s)
		{
		if (!s.Contains("\\\n") && !s.Contains("$'")) return s;
		var sb = new StringBuilder(s.Length);
		var outer = new Stack<(bool Dq, int Depth)>();
		bool dq = false;
		int depth = 0;   // open `(` inside the current `$( )`
		for (int k = 0; k < s.Length; k++)
			{
			char c = s[k];
			if (c == '\'' && !dq)
				{
				int e = s.IndexOf('\'', k + 1);
				if (e < 0) e = s.Length - 1;
				sb.Append(s, k, e - k + 1);
				k = e;
				continue;
				}
			if (c == '$' && k + 1 < s.Length && s[k + 1] == '\'' && !dq)
				{
				int e = k + 2;
				while (e < s.Length && s[e] != '\'') e += s[e] == '\\' ? 2 : 1;
				if (e >= s.Length) { sb.Append(s, k, s.Length - k); break; }
				sb.Append(AnsiC(s.Substring(k, e - k + 1)));
				k = e;
				continue;
				}
			if (c == '$' && k + 1 < s.Length && s[k + 1] == '(')
				{
				outer.Push((dq, depth));
				dq = false; depth = 0;
				sb.Append("$(");
				k++;
				continue;
				}
			if (!dq && c == '(') depth++;
			else if (!dq && c == ')' && outer.Count > 0)
				{
				if (depth > 0) depth--;
				else (dq, depth) = outer.Pop();
				}
			else if (c == '"') dq = !dq;
			if (c == '\\' && k + 1 < s.Length)
				{
				if (s[k + 1] == '\n') { k++; continue; }
				sb.Append(c).Append(s[++k]);
				continue;
				}
			sb.Append(c);
			}
		return sb.ToString();
		}

	/// <summary>`$'…'` as bash prints it: decoded, single-quoted, a `'` written `'\''`, cut at a NUL.</summary>
	private static string AnsiC(string dollarQuoted)
		{
		try
			{
			var toks = new Lexer.Lexer(dollarQuoted).Tokenize();
			if (toks.Count > 0 && toks[0].Type == TokenType.DollarSingleQuote)
				{
				var v = toks[0].Value;
				if (v.IndexOf('\0') is var z and >= 0) v = v[..z];
				return "'" + v.Replace("'", @"'\''") + "'";
				}
			}
		catch (LexException) { }
		return dollarQuoted;
		}

	private static bool Within(Token t, string text) => t.Start >= 0 && t.End <= text.Length && t.End >= t.Start;

	/// <summary>The index of the token that closes the opener at <paramref name="open"/>, or -1.</summary>
	private static int MatchingClose(List<Token> toks, int open)
		{
		if (toks[open].Type == TokenType.Backtick)
			{
			for (int k = open + 1; k < toks.Count; k++) if (toks[k].Type == TokenType.Backtick) return k;
			return -1;
			}
		int depth = 0;
		for (int k = open; k < toks.Count; k++)
			{
			var ty = toks[k].Type;
			if (ty is TokenType.DollarLParen or TokenType.LParen or TokenType.LessLParen or TokenType.GreaterLParen) depth++;
			else if (ty == TokenType.DollarDollarLParen) depth += 2;
			else if (ty == TokenType.RParen && --depth == 0) return k;
			}
		return -1;
		}

	private static string Unquote(string delim) => delim.Replace("'", "").Replace("\"", "").Replace("\\", "");
	}
