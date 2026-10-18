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

	public string Expand(string ps) => ExpandVars(Decode(ps, protect: false));

	/// <summary>The backslash escapes only, decoded for the shell's own expansion to follow (PS4, as bash's
	/// decode_prompt_string with promptvars): `\$` stays escaped, and every decoded value has its `\`, `$` and `` ` ``
	/// escaped, so none of them is expanded again; `\\` is one backslash (measured against GNU). PS4's escapes printed
	/// raw (`\s-\v> `, 2026-10-17).</summary>
	public string DecodeForExpansion(string ps) => Decode(ps, protect: true);

	private string Decode(string ps, bool protect)
		{
		var sb = new StringBuilder();
		void Add(string v)
			{
			if (!protect) { sb.Append(v); return; }
			foreach (var c in v) { if (c is '\\' or '$' or '`') sb.Append('\\'); sb.Append(c); }
			}
		for (int i = 0; i < ps.Length; i++)
			{
			if (ps[i] != '\\' || i + 1 >= ps.Length) { sb.Append(ps[i]); continue; }

			char e = ps[++i];
			switch (e)
				{
				case 'u': Add(Environment.UserName); break;
				case 'h': Add(HostShort()); break;
				case 'H': Add(Environment.MachineName); break;
				case 'w': Add(Cwd(full: true)); break;
				case 'W': Add(Cwd(full: false)); break;
				case 's': Add("bash"); break;
				case 'v': Add(VersionShort()); break;
				case 'V': Add(_env.Get("BASH_VERSION")); break;
				case '$': sb.Append(protect ? "\\$" : "$"); break;   // no root concept on Windows
				case 'n': Add("\n"); break;
				case 'r': Add("\r"); break;
				case 'a': Add("\a"); break;
				case 'e': Add("\x1b"); break;
				case 't': Add(DateTime.Now.ToString("HH:mm:ss")); break;
				case 'T': Add(DateTime.Now.ToString("hh:mm:ss")); break;
				case '@': Add(DateTime.Now.ToString("hh:mm tt")); break;
				case 'A': Add(DateTime.Now.ToString("HH:mm")); break;
				case 'd': Add(DateTime.Now.ToString("ddd MMM dd")); break;
				case '!': Add(_historyNumber().ToString()); break;
				case '#': Add(_commandNumber().ToString()); break;
				case 'j': Add("0"); break;               // no job control
				case '\\': sb.Append('\\'); break;       // one backslash, which the expansion after may still use (bash: PS4='[\\$]' is `[$]`)
				case '[': break;                          // non-printing region start — strip
				case ']': break;                          // non-printing region end — strip
				case >= '0' and <= '7':                   // \nnn octal escape
					{
					int val = e - '0', n = 1;
					while (n < 3 && i + 1 < ps.Length && ps[i + 1] is >= '0' and <= '7')
						{ val = val * 8 + (ps[++i] - '0'); n++; }
					Add(((char)val).ToString());
					break;
					}
				default: sb.Append('\\').Append(e); break;
				}
			}
		return sb.ToString();
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
