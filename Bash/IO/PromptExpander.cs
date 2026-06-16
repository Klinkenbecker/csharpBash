using System.Text;
using Bash.Evaluator;

namespace Bash.IO;

/// <summary>
/// Expands a bash prompt string (PS1/PS2) — first the backslash escapes
/// (\u \h \w \$ …), then promptvars ($VAR / ${VAR}). Command substitution in
/// prompts ($(...)) is not yet supported. \[ and \] (non-printing-region markers)
/// are stripped: the line editor measures prompt width from the real cursor
/// position, so the markers are unnecessary here.
/// </summary>
public sealed class PromptExpander(ShellEnvironment env, Func<int> historyNumber, Func<int> commandNumber)
	{
	private readonly ShellEnvironment _env = env;
	private readonly Func<int> _historyNumber = historyNumber;
	private readonly Func<int> _commandNumber = commandNumber;

	public string Expand(string ps)
		{
		var sb = new StringBuilder();
		for (int i = 0; i < ps.Length; i++)
			{
			if (ps[i] != '\\' || i + 1 >= ps.Length) { sb.Append(ps[i]); continue; }

			char e = ps[++i];
			switch (e)
				{
				case 'u': sb.Append(Environment.UserName); break;
				case 'h': sb.Append(HostShort()); break;
				case 'H': sb.Append(Environment.MachineName); break;
				case 'w': sb.Append(Cwd(full: true)); break;
				case 'W': sb.Append(Cwd(full: false)); break;
				case 's': sb.Append("bash"); break;
				case 'v': sb.Append(VersionShort()); break;
				case 'V': sb.Append(_env.Get("BASH_VERSION")); break;
				case '$': sb.Append('$'); break;          // no root concept on Windows
				case 'n': sb.Append('\n'); break;
				case 'r': sb.Append('\r'); break;
				case 'a': sb.Append('\a'); break;
				case 'e': sb.Append('\x1b'); break;
				case 't': sb.Append(DateTime.Now.ToString("HH:mm:ss")); break;
				case 'T': sb.Append(DateTime.Now.ToString("hh:mm:ss")); break;
				case '@': sb.Append(DateTime.Now.ToString("hh:mm tt")); break;
				case 'A': sb.Append(DateTime.Now.ToString("HH:mm")); break;
				case 'd': sb.Append(DateTime.Now.ToString("ddd MMM dd")); break;
				case '!': sb.Append(_historyNumber()); break;
				case '#': sb.Append(_commandNumber()); break;
				case 'j': sb.Append('0'); break;          // no job control
				case '\\': sb.Append('\\'); break;
				case '[': break;                          // non-printing region start — strip
				case ']': break;                          // non-printing region end — strip
				case >= '0' and <= '7':                   // \nnn octal escape
					{
					int val = e - '0', n = 1;
					while (n < 3 && i + 1 < ps.Length && ps[i + 1] is >= '0' and <= '7')
						{ val = val * 8 + (ps[++i] - '0'); n++; }
					sb.Append((char)val);
					break;
					}
				default: sb.Append('\\').Append(e); break;
				}
			}
		return ExpandVars(sb.ToString());
		}

	// ── promptvars: $VAR / ${VAR} ────────────────────────────────────────────────

	private string ExpandVars(string s)
		{
		var sb = new StringBuilder();
		int i = 0;
		while (i < s.Length)
			{
			if (s[i] == '$' && i + 1 < s.Length)
				{
				if (s[i + 1] == '{')
					{
					int end = s.IndexOf('}', i + 2);
					if (end > 0) { sb.Append(_env.Get(s[(i + 2)..end])); i = end + 1; continue; }
					}
				else if (char.IsLetter(s[i + 1]) || s[i + 1] == '_')
					{
					int j = i + 1;
					while (j < s.Length && (char.IsLetterOrDigit(s[j]) || s[j] == '_')) j++;
					sb.Append(_env.Get(s[(i + 1)..j]));
					i = j;
					continue;
					}
				}
			sb.Append(s[i++]);
			}
		return sb.ToString();
		}

	// ── helpers ──────────────────────────────────────────────────────────────────

	private static string HostShort()
		{
		var h = Environment.MachineName;
		int dot = h.IndexOf('.');
		return dot >= 0 ? h[..dot] : h;
		}

	private string VersionShort()
		{
		// \v is major.minor of BASH_VERSION (e.g. "5.1" from "5.1.0-koliada").
		var v = _env.Get("BASH_VERSION");
		int first = v.IndexOf('.');
		if (first < 0) return v;
		int second = v.IndexOf('.', first + 1);
		return second < 0 ? v : v[..second];
		}

	private string Cwd(bool full)
		{
		string cwd = Directory.GetCurrentDirectory();
		if (!full) return Path.GetFileName(cwd) is { Length: > 0 } leaf ? leaf : cwd;

		var home = _env.Get("HOME");
		if (home.Length > 0 && cwd.StartsWith(home, StringComparison.OrdinalIgnoreCase))
			cwd = "~" + cwd[home.Length..];
		return cwd.Replace('\\', '/');
		}
	}
