using Bash.Evaluator;

namespace Bash.IO;

/// <summary>
/// Result of a completion attempt: replace buffer[Start..Start+Length] with one of
/// Matches. SpaceOnSingle controls whether a trailing space is appended when a single
/// match is accepted (true for commands/files, false for variables which usually continue).
/// </summary>
public readonly record struct CompletionResult(int Start, int Length, List<string> Matches, bool SpaceOnSingle = true);

/// <summary>
/// Tab completion. Completes the first word of a command against builtins,
/// functions, and PATH executables; every other word (and any word containing a
/// path separator) against the filesystem. Matching is case-insensitive, as is
/// conventional on Windows.
/// </summary>
public sealed class CompletionEngine(ShellEnvironment env, Bash.Evaluator.Evaluator eval)
	{
	private readonly ShellEnvironment _env = env;
	private readonly Bash.Evaluator.Evaluator _eval = eval;

	private static readonly char[] CommandSeparators = ['|', '&', ';', '(', '{'];
	private static readonly string[] ExeExtensions   = [".exe", ".cmd", ".bat", ".com", ".ps1"];

	public CompletionResult Complete(string buffer, int cursor)
		{
		// Token under the cursor: from the last unescaped space up to the cursor.
		int start = cursor;
		while (start > 0 && !char.IsWhiteSpace(buffer[start - 1])) start--;
		string token = buffer[start..cursor];

		// Variable reference: complete the name after the last $ (optionally ${).
		int dollar = token.LastIndexOf('$');
		if (dollar >= 0)
			{
			string after = token[(dollar + 1)..];
			bool brace = after.StartsWith('{');
			string partial = brace ? after[1..] : after;
			if (IsNameChars(partial))
				return CompleteVariable(start, token, dollar, brace, partial);
			}

		bool commandPosition = IsCommandPosition(buffer, start);
		var matches = commandPosition && !HasPathSeparator(token)
			? CompleteCommand(token)
			: CompleteFilename(token);

		return new CompletionResult(start, cursor - start, matches);
		}

	// ── variable completion ─────────────────────────────────────────────────────

	private CompletionResult CompleteVariable(int start, string token, int dollar, bool brace, string partial)
		{
		string prefix = token[..dollar] + "$" + (brace ? "{" : "");
		var matches = new List<string>();
		foreach (var name in _env.VariableNames())
			if (name.StartsWith(partial, StringComparison.Ordinal))
				matches.Add(prefix + name);
		matches.Sort(StringComparer.Ordinal);
		return new CompletionResult(start, token.Length, matches, SpaceOnSingle: false);
		}

	private static bool IsNameChars(string s)
		{
		foreach (char c in s)
			if (!(char.IsLetterOrDigit(c) || c == '_')) return false;
		return true;
		}

	// ── command position detection ──────────────────────────────────────────────

	private static bool IsCommandPosition(string buffer, int tokenStart)
		{
		// Walk back over whitespace; a command starts the line or follows a separator.
		int i = tokenStart - 1;
		while (i >= 0 && char.IsWhiteSpace(buffer[i])) i--;
		if (i < 0) return true;
		return Array.IndexOf(CommandSeparators, buffer[i]) >= 0;
		}

	private static bool HasPathSeparator(string token) =>
		token.Contains('/') || token.Contains('\\') || token.StartsWith('~') || token.StartsWith('.');

	// ── command completion ──────────────────────────────────────────────────────

	private List<string> CompleteCommand(string token)
		{
		var set = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);

		foreach (var n in Builtins.Names)
			if (Prefix(n, token)) set.Add(n);
		foreach (var n in _eval.FunctionNames)
			if (Prefix(n, token)) set.Add(n);

		var path = _env.Get("PATH");
		foreach (var dir in path.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
			{
			if (!Directory.Exists(dir)) continue;
			try
				{
				foreach (var file in Directory.EnumerateFiles(dir))
					{
					var name = Path.GetFileName(file);
					var ext  = Path.GetExtension(file);
					if (ExeExtensions.Contains(ext, StringComparer.OrdinalIgnoreCase) && Prefix(name, token))
						set.Add(name);
					}
				}
			catch { /* unreadable PATH dir — skip */ }
			}

		return [.. set];
		}

	// ── filename completion ─────────────────────────────────────────────────────

	private List<string> CompleteFilename(string token)
		{
		// Split into a directory part (kept verbatim in the result) and the partial
		// leaf we are matching. The leaf comparison is on the on-disk filename.
		int slash = Math.Max(token.LastIndexOf('/'), token.LastIndexOf('\\'));
		string dirPart  = slash >= 0 ? token[..(slash + 1)] : "";
		string partial  = slash >= 0 ? token[(slash + 1)..] : token;

		string searchDir = dirPart.Length > 0
			? ShellEnvironment.TranslatePath(dirPart)
			: ".";

		List<string> result = [];
		if (!Directory.Exists(searchDir)) return result;

		try
			{
			foreach (var entry in Directory.EnumerateFileSystemEntries(searchDir))
				{
				var name = Path.GetFileName(entry);
				// Hide dotfiles unless the partial explicitly began with a dot.
				if (name.StartsWith('.') && !partial.StartsWith('.')) continue;
				if (!Prefix(name, partial)) continue;
				bool isDir = Directory.Exists(entry);
				result.Add(dirPart + name + (isDir ? "/" : ""));
				}
			}
		catch { /* unreadable dir — skip */ }

		result.Sort(StringComparer.OrdinalIgnoreCase);
		return result;
		}

	private static bool Prefix(string candidate, string token) =>
		candidate.StartsWith(token, StringComparison.OrdinalIgnoreCase);
	}
