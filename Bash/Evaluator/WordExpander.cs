using Bash.Parser;

namespace Bash.Evaluator;

/// <summary>
/// Expands a <see cref="Word"/> to a string or list of fields.
/// Phase 4: adds tilde expansion, glob expansion, brace expansion,
/// AnsiCQuoted and HeredocBody parts.
/// </summary>
public sealed class WordExpander(ShellEnvironment env, Evaluator eval)
	{
	private readonly ShellEnvironment _env = env;
	private readonly Evaluator _eval = eval;

	// ── public API ────────────────────────────────────────────────────────────

	/// <summary>Expand a word to a single string (no word-splitting or globbing).</summary>
	public string ExpandToString(Word word) =>
		string.Concat(word.Parts.Select(ExpandPart));

	/// <summary>Expand a word to a single string then translate it as a filesystem path.</summary>
	public string ExpandToPath(Word word) =>
		ShellEnvironment.TranslatePath(ExpandToString(word));

	/// <summary>
	/// Expand a word to a list of fields:
	///   1. Tilde expansion on leading ~
	///   2. Parameter/command/arithmetic expansion
	///   3. Word splitting on IFS (unquoted expansions only)
	///   4. Brace expansion
	///   5. Glob expansion
	/// Returns at least one element.
	/// </summary>
	public List<string> ExpandToFields(Word word)
		{
		// Fast path: a single literal with no brace/glob metacharacters expands to
		// itself as one field. Literals are never word-split, so this is exact — and
		// it skips the StringBuilder + field-builder allocation for the most common
		// argument shape (keywords, numbers, `]`, plain command/arg words).
		if (word.Parts is [LiteralPart lit] && !HasBraceOrGlob(lit.Value))
			return [lit.Value];

		var ifs = _env.Get("IFS");
		if (ifs.Length == 0) ifs = " \t\n";
		char ifsFirst = ifs.Length > 0 ? ifs[0] : ' ';

		// Fast path: a single bare $var / ${simple} (not $@/$*) under default IFS that
		// needs neither word-splitting nor glob/brace expansion → its value as one
		// field, or zero fields if empty/unset. Skips StringBuilder + field-builder.
		if (word.Parts is [BraceExpansionPart bp] && bp.Raw is not ("@" or "*")
		    && IsSimpleParam(bp.Raw) && (ifs == " \t\n"))
			{
			var val = _env.Get(bp.Raw);
			if (val.Length == 0) return [];
			if (!HasBraceOrGlob(val) && !HasWhitespace(val)) return [val];
			}

		var fields    = new List<string>();
		var fieldGlob = new List<bool>();   // per field: has an UNQUOTED brace/glob metachar?
		var current   = new System.Text.StringBuilder();
		bool open     = false;   // current holds pending content to be flushed as a field
		bool curGlob  = false;   // current field has an unquoted metachar

		void Flush() { fields.Add(current.ToString()); fieldGlob.Add(curGlob); current.Clear(); open = false; curGlob = false; }

		void AppendText(string text, bool quoted)
			{
			if (quoted)
				{ current.Append(text); open = true; }   // quoted: never glob/brace-eligible
			else
				{
				foreach (char c in text)
					{
					if (ifs.Contains(c))
						{ if (open) Flush(); }
					else
						{
						current.Append(c); open = true;
						if (c is '{' or '}' or '*' or '?' or '[') curGlob = true;
						}
					}
				}
			}

		// Append an array @/* expansion. Quoted @ yields one field per element
		// (first joins the pending field, last stays open for trailing text);
		// quoted * joins all elements with the first IFS char; unquoted forms are
		// space-joined and then word-split like any other unquoted expansion.
		void AppendArray(List<string> elems, bool star, bool quoted)
			{
			if (elems.Count == 0) return;
			if (quoted && star)
				{ AppendText(string.Join(ifsFirst.ToString(), elems), true); return; }
			if (!quoted)
				{
				for (int i = 0; i < elems.Count; i++)
					{ if (i > 0) AppendText(" ", false); AppendText(elems[i], false); }
				return;
				}
			current.Append(elems[0]); open = true;
			for (int i = 1; i < elems.Count; i++)
				{
				Flush();
				current.Append(elems[i]); open = true;
				}
			}

		void Process(WordPart part, bool quoted)
			{
			switch (part)
				{
				case DoubleQuotedPart d:
					// An empty "" anchors one empty field; otherwise descend with
					// quoting on (a quoted empty-array part still contributes nothing).
					if (d.Parts.Count == 0) AppendText("", true);
					else foreach (var sub in d.Parts) Process(sub, true);
					break;
				// "$@"/"$*" (and ${@}/${*}) split like an array: one field per
				// positional parameter for @, single IFS-joined field for quoted *.
				case VarExpansionPart { Name: "@" or "*" } vp:
					AppendArray(_env.GetPositionals(), vp.Name == "*", quoted);
					break;
				case BraceExpansionPart { Raw: "@" or "*" } bp:
					AppendArray(_env.GetPositionals(), bp.Raw == "*", quoted);
					break;
				case BraceExpansionPart b when TryArrayExpansion(b.Raw, out var elems, out bool star):
					AppendArray(elems, star, quoted);
					break;
				default:
					// Single-quoted, ANSI-C, and heredoc parts are inherently quoted
					// regardless of surrounding context — never word-split them.
					AppendText(ExpandPart(part),
						quoted || part is SingleQuotedPart or AnsiCQuotedPart or HeredocBodyPart);
					break;
				}
			}

		foreach (var p in word.Parts) Process(p, false);
		if (open) Flush();

		// A word that expanded to nothing (unquoted empty/unset, or an empty
		// "${arr[@]}") yields zero fields — callers SelectMany over the result,
		// so `for x in "${empty[@]}"` correctly iterates zero times. An explicit
		// empty string ("" / '') is anchored above and survives as one field.
		if (fields.Count == 0) return [];

		// Steps 4 & 5: brace then glob — ONLY on fields with an unquoted metachar.
		// Quoted text (echo '*', echo "{a,b}") is literal. Fields without a metachar
		// (the common case) pass straight through with no allocation.
		if (!fieldGlob.Contains(true)) return fields;

		var braced = new List<string>(); var bracedGlob = new List<bool>();
		for (int k = 0; k < fields.Count; k++)
			{
			if (fieldGlob[k] && fields[k].Contains('{'))
				foreach (var e in ExpandBraces(fields[k])) { braced.Add(e); bracedGlob.Add(true); }
			else { braced.Add(fields[k]); bracedGlob.Add(fieldGlob[k]); }
			}

		var result = new List<string>();
		for (int k = 0; k < braced.Count; k++)
			{
			if (bracedGlob[k] && ContainsGlob(braced[k])) result.AddRange(GlobExpand(braced[k]));
			else result.Add(braced[k]);
			}
		return result;
		}

	private static bool HasBraceOrGlob(string s)
		{
		foreach (char c in s) if (c is '{' or '}' or '*' or '?' or '[') return true;
		return false;
		}

	private static bool HasWhitespace(string s)
		{
		foreach (char c in s) if (c is ' ' or '\t' or '\n') return true;
		return false;
		}

	/// <summary>
	/// Recognise the array-spanning forms ${arr[@]}, ${arr[*]}, ${!arr[@]},
	/// ${!arr[*]} for both indexed and associative arrays. Returns the element
	/// (or key) list so the caller can produce one field per element.
	/// </summary>
	private bool TryArrayExpansion(string raw, out List<string> elems, out bool star)
		{
		elems = [];
		star  = false;
		bool keys = raw.StartsWith('!');
		var body  = keys ? raw[1..] : raw;
		if      (body.EndsWith("[@]")) star = false;
		else if (body.EndsWith("[*]")) star = true;
		else return false;
		var name = body[..^3];
		if (name.Length == 0) return false;
		if (keys)
			{
			if (_env.IsArray(name)) { elems = _env.GetArrayKeys(name);  return true; }
			if (_env.IsAssoc(name)) { elems = _env.GetAssocKeys(name);  return true; }
			}
		else
			{
			if (_env.IsArray(name)) { elems = _env.GetArrayValues(name); return true; }
			if (_env.IsAssoc(name)) { elems = _env.GetAssocValues(name); return true; }
			}
		// A plain identifier with no array/assoc defined is an empty array — yields
		// zero fields, matching bash. Non-identifier names (e.g. "#arr" from
		// ${#arr[@]}) are left for ExpandBraceParam to handle.
		return IsIdentifier(name);
		}

	private static bool IsIdentifier(string s)
		{
		if (s.Length == 0 || !(char.IsLetter(s[0]) || s[0] == '_')) return false;
		foreach (char c in s)
			if (!(char.IsLetterOrDigit(c) || c == '_')) return false;
		return true;
		}

	/// <summary>True for a bare name, positional ($1), or special parameter ($# $@ …)
	/// with no operator or subscript — the fast-path for ExpandBraceParam.</summary>
	private static bool IsSimpleParam(string raw)
		{
		if (raw.Length == 1 && "@*#?$!-".IndexOf(raw[0]) >= 0) return true;
		char c0 = raw[0];
		if (!(char.IsLetterOrDigit(c0) || c0 == '_')) return false;
		foreach (char c in raw)
			if (!(char.IsLetterOrDigit(c) || c == '_')) return false;
		return true;
		}

	// ── part expansion ────────────────────────────────────────────────────────

	private string ExpandPart(WordPart part) => part switch
		{
		LiteralPart l              => l.Value,
		SingleQuotedPart s         => s.Value,
		AnsiCQuotedPart a          => a.Value,   // already decoded by lexer
		HeredocBodyPart h          => h.Body,
		DoubleQuotedPart d         => string.Concat(d.Parts.Select(ExpandPart)),
		TildePart t                => ExpandTilde(t.Suffix),
		VarExpansionPart v         => _env.Get(v.Name),
		BraceExpansionPart b       => ExpandBraceParam(b.Raw),
		CommandSubstitutionPart cs => RunSubstitution(cs.Command),
		ArithmeticExpansionPart a  => EvalArithmetic(a.Expression).ToString(),
		_ => ""
		};

	// ── tilde expansion ───────────────────────────────────────────────────────

	private string ExpandTilde(string suffix)
		{
		// ~  or ~/path  →  $HOME/path
		// ~user — not supported yet, return as-is
		var home = _env.Get("HOME");
		if (home.Length == 0)
			home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

		if (suffix.Length == 0) return home;

		// suffix starts with / or \ already
		return home + suffix;
		}

	// ── ${VAR...} parameter expansion ────────────────────────────────────────

	private string ExpandBraceParam(string raw)
		{
		if (raw.Length == 0) return "";

		// Fast path: a bare name ($var, $1) or special parameter ($#, $@, $?, …) with
		// no operator/subscript. This is the overwhelmingly common case in hot loops,
		// so skip the operator-detection predicates below and look up directly.
		if (IsSimpleParam(raw)) return _env.Get(raw);

		// ${#arr[@]} or ${#arr[*]} — array length
		if (raw.StartsWith('#') && (raw.EndsWith("[@]") || raw.EndsWith("[*]")))
			{
			var n = raw[1..^3];
			if (_env.IsArray(n)) return _env.GetArrayLength(n).ToString();
			if (_env.IsAssoc(n)) return _env.GetAssocLength(n).ToString();
			return "0";
			}

		// ${#VAR} — string length
		if (raw[0] == '#' && raw.Length > 1 && !raw.Contains('['))
			return _env.Get(raw[1..]).Length.ToString();

		// ${arr[@]}, ${arr[*]} — all array values
		// (must exclude the !-prefixed keys form, handled below)
		if (!raw.StartsWith('!') && (raw.EndsWith("[@]") || raw.EndsWith("[*]")))
			{
			var n = raw[..^3];
			if (_env.IsArray(n)) return string.Join(" ", _env.GetArrayValues(n));
			if (_env.IsAssoc(n)) return string.Join(" ", _env.GetAssocValues(n));
			return "";
			}

		// ${!arr[@]}, ${!arr[*]} — array keys/indices
		if (raw.StartsWith('!') && (raw.EndsWith("[@]") || raw.EndsWith("[*]")))
			{
			var n = raw[1..^3];
			if (_env.IsArray(n)) return string.Join(" ", _env.GetArrayKeys(n));
			if (_env.IsAssoc(n)) return string.Join(" ", _env.GetAssocKeys(n));
			return "";
			}

		// ${arr[n]} — single element
		int lb = raw.IndexOf('[');
		int rb = raw.IndexOf(']');
		if (lb > 0 && rb > lb)
			{
			var arrName = raw[..lb];
			var idxStr  = raw[(lb + 1)..rb];
			if (_env.IsArray(arrName))
				{
				long idx = ArithParser.Evaluate(ExpandVarsInArith(idxStr));
				return _env.GetArrayElement(arrName, (int)idx);
				}
			if (_env.IsAssoc(arrName))
				// Associative subscripts are not arithmetic: a bare word is a
				// literal key, but $var / ${var} still undergo parameter expansion.
				return _env.GetAssocElement(arrName, ExpandSubscript(idxStr));
			}

		// ${VAR:-word}, ${VAR:=word}, ${VAR:+word}, ${VAR:?word}
		foreach (var op in new[] { ":-", ":=", ":+", ":?", "-", "=", "+", "?" })
			{
			int idx = raw.IndexOf(op, StringComparison.Ordinal);
			if (idx <= 0) continue;
			var varName = raw[..idx];
			var word    = raw[(idx + op.Length)..];
			var val     = _env.Get(varName);
			bool empty  = val.Length == 0;

			switch (op.TrimStart(':'))
				{
				case "-": return empty ? word : val;
				case "=":
					if (empty) { _env.Set(varName, word); return word; }
					return val;
				case "+": return empty ? "" : word;
				case "?":
					if (empty) throw new EvalException(
						$"{varName}: {(word.Length > 0 ? word : "parameter null or not set")}");
					return val;
				}
			}

		// ${VAR%pat}, ${VAR%%pat}, ${VAR#pat}, ${VAR##pat}
		foreach (var op in new[] { "%%", "%", "##", "#" })
			{
			int idx = raw.IndexOf(op, StringComparison.Ordinal);
			if (idx <= 0) continue;
			var varName = raw[..idx];
			var pat     = raw[(idx + op.Length)..];
			var val     = _env.Get(varName);
			return op switch
				{
				"#"  => StripPrefix(val, pat, greedy: false),
				"##" => StripPrefix(val, pat, greedy: true),
				"%"  => StripSuffix(val, pat, greedy: false),
				"%%" => StripSuffix(val, pat, greedy: true),
				_    => val
				};
			}

		// ${VAR//pat/rep}  ${VAR/pat/rep}
		if (raw.Contains("//"))
			{
			int slash   = raw.IndexOf("//", StringComparison.Ordinal);
			var varName = raw[..slash];
			var rest    = raw[(slash + 2)..];
			int sep     = rest.IndexOf('/');
			var pat     = sep < 0 ? rest : rest[..sep];
			var rep     = sep < 0 ? ""   : rest[(sep + 1)..];
			return _env.Get(varName).Replace(pat, rep, StringComparison.Ordinal);
			}
		if (raw.Contains('/'))
			{
			int slash   = raw.IndexOf('/');
			var varName = raw[..slash];
			var rest    = raw[(slash + 1)..];
			int sep     = rest.IndexOf('/');
			var pat     = sep < 0 ? rest : rest[..sep];
			var rep     = sep < 0 ? ""   : rest[(sep + 1)..];
			var val     = _env.Get(varName);
			int hit     = val.IndexOf(pat, StringComparison.Ordinal);
			return hit < 0 ? val : val[..hit] + rep + val[(hit + pat.Length)..];
			}

		// Plain ${VAR}
		return _env.Get(raw);
		}

	// ── brace expansion  {a,b,c}  {1..5}  {a..z} ────────────────────────────

	/// <summary>
	/// Expand shell brace expressions in a fully-expanded string field.
	/// e.g. "pre{a,b}suf" → ["preasuf","prebsuf"]
	///      "{1..3}"      → ["1","2","3"]
	/// </summary>
	private static List<string> ExpandBraces(string s)
		{
		// Find the first unescaped {
		int open = FindBraceOpen(s);
		if (open < 0) return [s];

		// Find matching }
		int close = FindBraceClose(s, open);
		if (close < 0) return [s];

		string pre  = s[..open];
		string body = s[(open + 1)..close];
		string post = s[(close + 1)..];

		// Range expansion {x..y} or {x..y..incr}
		var rangeParts = body.Split("..");
		if (rangeParts.Length >= 2)
			{
			var rangeItems = ExpandRange(rangeParts);
			if (rangeItems is not null)
				return rangeItems.SelectMany(r => ExpandBraces(pre + r + post)).ToList();
			}

		// Comma list — must split respecting nested braces
		var items = SplitBraceItems(body);
		if (items.Count < 2) return [s]; // no comma → not a brace expansion

		return items.SelectMany(item => ExpandBraces(pre + item + post)).ToList();
		}

	private static int FindBraceOpen(string s)
		{
		for (int i = 0; i < s.Length; i++)
			if (s[i] == '{') return i;
		return -1;
		}

	private static int FindBraceClose(string s, int openIdx)
		{
		int depth = 1;
		for (int i = openIdx + 1; i < s.Length; i++)
			{
			if (s[i] == '{') depth++;
			else if (s[i] == '}') { depth--; if (depth == 0) return i; }
			}
		return -1;
		}

	private static List<string> SplitBraceItems(string body)
		{
		var items = new List<string>();
		int depth = 0, start = 0;
		for (int i = 0; i < body.Length; i++)
			{
			if      (body[i] == '{') depth++;
			else if (body[i] == '}') depth--;
			else if (body[i] == ',' && depth == 0)
				{ items.Add(body[start..i]); start = i + 1; }
			}
		items.Add(body[start..]);
		return items;
		}

	private static List<string>? ExpandRange(string[] parts)
		{
		string from = parts[0], to = parts[1];
		int step = parts.Length >= 3 && int.TryParse(parts[2], out var s) ? Math.Abs(s) : 1;
		if (step == 0) step = 1;

		// Numeric range
		if (long.TryParse(from, out var iFrom) && long.TryParse(to, out var iTo))
			{
			var result = new List<string>();
			int width = Math.Max(from.Length, to.Length);
			bool pad = (from.StartsWith('0') && from.Length > 1) ||
			           (to.StartsWith('0')   && to.Length   > 1);
			long dir = iTo >= iFrom ? 1 : -1;
			for (long v = iFrom; dir > 0 ? v <= iTo : v >= iTo; v += dir * step)
				result.Add(pad ? v.ToString().PadLeft(width, '0') : v.ToString());
			return result;
			}

		// Character range
		if (from.Length == 1 && to.Length == 1)
			{
			var result = new List<string>();
			int dir = to[0] >= from[0] ? 1 : -1;
			for (int v = from[0]; dir > 0 ? v <= to[0] : v >= to[0]; v += dir * step)
				result.Add(((char)v).ToString());
			return result;
			}

		return null;
		}

	// ── glob expansion ────────────────────────────────────────────────────────

	/// <summary>
	/// If a field contains unquoted glob characters (* ? [) and matches at
	/// least one filesystem entry, returns the sorted list of matches.
	/// Otherwise returns the original field unchanged.
	/// </summary>
	private static List<string> GlobExpand(string field)
		{
		if (!ContainsGlob(field)) return [field];

		var translated = ShellEnvironment.TranslatePath(field);
		try
			{
			var dir     = Path.GetDirectoryName(translated) ?? ".";
			var pattern = Path.GetFileName(translated);
			if (dir.Length == 0) dir = ".";

			if (!Directory.Exists(dir)) return [field];

			var matches = Directory.GetFileSystemEntries(dir, pattern)
				.OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
				.Where(m => pattern.StartsWith('.') || !Path.GetFileName(m).StartsWith('.'))
				.ToList();

			return matches.Count > 0 ? matches : [field];
			}
		catch
			{
			return [field];
			}
		}

	private static bool ContainsGlob(string s)
		{
		foreach (char c in s)
			if (c is '*' or '?' or '[') return true;
		return false;
		}

	// ── command substitution ──────────────────────────────────────────────────

	private string RunSubstitution(Node command)
		{
		var captured = new System.Text.StringBuilder();
		var oldOut = Console.Out;
		bool oldCap = Evaluator._capturing;
		using var sw = new System.IO.StringWriter(captured) { NewLine = "\n" };
		Console.SetOut(sw);
		Evaluator._capturing = true;   // byte-builtins must write text into the capture
		try   { _eval.Execute(command); }
		finally { Console.SetOut(oldOut); Evaluator._capturing = oldCap; }
		return captured.ToString().TrimEnd('\n');
		}

	// ── arithmetic expansion ──────────────────────────────────────────────────

	public long EvalArithmetic(string expr)
		{
		expr = ExpandVarsInArith(expr.Trim());
		return ArithParser.Evaluate(expr);
		}

	public string ExpandArith(string expr) => ExpandVarsInArith(expr.Trim());

	private string ExpandVarsInArith(string expr)
		{
		var sb = new System.Text.StringBuilder();
		int i = 0;
		while (i < expr.Length)
			{
			// $VAR explicit expansion
			if (expr[i] == '$' && i + 1 < expr.Length)
				{
				i++;
				int start = i;
				while (i < expr.Length && (char.IsLetterOrDigit(expr[i]) || expr[i] == '_')) i++;
				sb.Append(_env.Get(expr[start..i]));
				}
			// numeric literal — copy verbatim (incl. 0x… hex) so the letter scan below
			// doesn't grab the hex suffix and treat it as a variable (0xff -> 0 + $ff).
			else if (char.IsAsciiDigit(expr[i]))
				{
				int start = i;
				if (expr[i] == '0' && i + 1 < expr.Length && (expr[i + 1] is 'x' or 'X'))
					{ i += 2; while (i < expr.Length && Uri.IsHexDigit(expr[i])) i++; }
				else
					while (i < expr.Length && char.IsAsciiDigit(expr[i])) i++;
				sb.Append(expr[start..i]);
				}
			// bare identifier — expand as variable (bash arithmetic semantics)
			else if (char.IsLetter(expr[i]) || expr[i] == '_')
				{
				int start = i;
				while (i < expr.Length && (char.IsLetterOrDigit(expr[i]) || expr[i] == '_')) i++;
				var name = expr[start..i];
				var val = _env.Get(name);
				sb.Append(val.Length > 0 ? val : "0");
				}
			else
				sb.Append(expr[i++]);
			}
		return sb.ToString();
		}

	/// <summary>
	/// Expand an associative-array subscript: $name and ${...} undergo parameter
	/// expansion; everything else is kept literal (a bare word is a literal key,
	/// unlike an arithmetic indexed subscript).
	/// </summary>
	private string ExpandSubscript(string s)
		{
		var sb = new System.Text.StringBuilder();
		int i = 0;
		while (i < s.Length)
			{
			if (s[i] == '$' && i + 1 < s.Length && s[i + 1] == '{')
				{
				i += 2;
				int depth = 1, start = i;
				while (i < s.Length && depth > 0)
					{
					if      (s[i] == '{') depth++;
					else if (s[i] == '}') { depth--; if (depth == 0) break; }
					i++;
					}
				sb.Append(ExpandBraceParam(s[start..i]));
				if (i < s.Length) i++;   // skip closing }
				}
			else if (s[i] == '$' && i + 1 < s.Length)
				{
				i++;
				int start = i;
				while (i < s.Length && (char.IsLetterOrDigit(s[i]) || s[i] == '_')) i++;
				sb.Append(_env.Get(s[start..i]));
				}
			else
				sb.Append(s[i++]);
			}
		return sb.ToString();
		}

	// ── pattern helpers (for ${VAR#pat} etc.) ────────────────────────────────

	private static string StripPrefix(string val, string pat, bool greedy)
		{
		if (greedy)
			{
			for (int i = val.Length; i >= 0; i--)
				if (val[..i] == pat || GlobMatchAt(pat, val, 0, i))
					return val[i..];
			}
		else
			{
			for (int i = 0; i <= val.Length; i++)
				if (val[..i] == pat || GlobMatchAt(pat, val, 0, i))
					return val[i..];
			}
		return val;
		}

	private static string StripSuffix(string val, string pat, bool greedy)
		{
		if (greedy)
			{
			for (int i = 0; i <= val.Length; i++)
				if (val[i..] == pat || GlobMatchAt(pat, val, i, val.Length))
					return val[..i];
			}
		else
			{
			for (int i = val.Length; i >= 0; i--)
				if (val[i..] == pat || GlobMatchAt(pat, val, i, val.Length))
					return val[..i];
			}
		return val;
		}

	private static bool GlobMatchAt(string pat, string s, int start, int end) =>
		Evaluator.GlobMatch(pat, s[start..end]);
	}
