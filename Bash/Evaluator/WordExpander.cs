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

	/// <summary>Exit status of the most recent `$( )` / backtick substitution, or null when
	/// none has run since <see cref="ResetSubstStatus"/>. A bare assignment `x=$(cmd)`
	/// reports this as its own status, as bash does.</summary>
	public int? LastSubstStatus { get; private set; }
	public void ResetSubstStatus() => LastSubstStatus = null;

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
			if (bracedGlob[k] && ContainsGlob(braced[k]) && !_eval.Options.NoGlob) result.AddRange(GlobExpand(braced[k]));
			else result.Add(braced[k]);
			}
		return result;
		}

	/// <summary>Expand a word into a shell *pattern* for `case` / `[[ == ]]` / `${x#pat}`:
	/// unquoted parts keep their glob metacharacters, quoted parts have them escaped so
	/// they match literally.</summary>
	public string ExpandToPattern(Word word)
		{
		var sb = new System.Text.StringBuilder();
		foreach (var p in word.Parts)
			{
			switch (p)
				{
				case SingleQuotedPart s: sb.Append(EscapeGlob(s.Value)); break;
				case AnsiCQuotedPart a:  sb.Append(EscapeGlob(a.Value)); break;
				case DoubleQuotedPart d: sb.Append(EscapeGlob(string.Concat(d.Parts.Select(ExpandPart)))); break;
				default: sb.Append(ExpandPart(p)); break;
				}
			}
		return sb.ToString();
		}

	/// <summary>Expand a word into a regex for `[[ =~ ]]`: quoted parts are literal.</summary>
	public string ExpandToRegex(Word word)
		{
		var sb = new System.Text.StringBuilder();
		foreach (var p in word.Parts)
			{
			switch (p)
				{
				case SingleQuotedPart s: sb.Append(System.Text.RegularExpressions.Regex.Escape(s.Value)); break;
				case AnsiCQuotedPart a:  sb.Append(System.Text.RegularExpressions.Regex.Escape(a.Value)); break;
				case DoubleQuotedPart d: sb.Append(System.Text.RegularExpressions.Regex.Escape(string.Concat(d.Parts.Select(ExpandPart)))); break;
				default: sb.Append(ExpandPart(p)); break;
				}
			}
		return sb.ToString();
		}

	private static string EscapeGlob(string s)
		{
		var sb = new System.Text.StringBuilder();
		foreach (char c in s) { if (c is '*' or '?' or '[' or '\\' or ']') sb.Append('\\'); sb.Append(c); }
		return sb.ToString();
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
		// ${arr[@]:1:2} / ${arr[@]/a/b} / ${arr[@]#x} … — element-wise operators: expand
		// through ExpandBraceParam and split the result on spaces (elements with embedded
		// spaces are a known approximation here).
		int at = body.IndexOf("[@]", StringComparison.Ordinal);
		if (at < 0) at = body.IndexOf("[*]", StringComparison.Ordinal);
		if (at > 0 && at + 3 < body.Length && !keys)
			{
			star = body[at + 1] == '*';
			var joined = ExpandBraceParam(raw);
			elems = joined.Length == 0 ? [] : joined.Split(' ').ToList();
			return true;
			}
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
		ProcessSubstitutionPart ps => RunProcessSubstitution(ps.Command),
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

		// ── ${#…} lengths ──────────────────────────────────────────────────────────
		if (raw[0] == '#' && raw.Length > 1)
			{
			var body = raw[1..];
			if (body.EndsWith("[@]") || body.EndsWith("[*]"))
				{
				var n = body[..^3];
				if (_env.IsArray(n)) return _env.GetArrayLength(n).ToString();
				if (_env.IsAssoc(n)) return _env.GetAssocLength(n).ToString();
				return "0";
				}
			int lb0 = body.IndexOf('[');
			if (lb0 > 0 && body.EndsWith(']'))
				return ElementValue(body[..lb0], body[(lb0 + 1)..^1]).Length.ToString();
			if (IsSimpleParam(body)) return _env.Get(body).Length.ToString();
			// ${#x:-y} etc. is not valid bash; fall through to treat as a name
			}

		// ── ${!…} indirection / keys / prefix lists ──────────────────────────────
		if (raw[0] == '!' && raw.Length > 1)
			{
			var body = raw[1..];
			if (body.EndsWith("[@]") || body.EndsWith("[*]"))
				{
				var n = body[..^3];
				if (_env.IsArray(n)) return string.Join(" ", _env.GetArrayKeys(n));
				if (_env.IsAssoc(n)) return string.Join(" ", _env.GetAssocKeys(n));
				return "";
				}
			if (body.EndsWith('@') || body.EndsWith('*'))
				{
				var prefix = body[..^1];
				if (IsIdentifier(prefix))
					return string.Join(" ", _env.VariableNames().Where(v => v.StartsWith(prefix, StringComparison.Ordinal)).OrderBy(v => v, StringComparer.Ordinal));
				}
			// ${!name} / ${!name<op>…}: resolve the name, then re-expand with the operator
			int opAt = FindOperatorStart(body);
			var target = _env.Get(opAt < 0 ? body : body[..opAt]);
			if (opAt < 0) return ExpandBraceParam(target.Length == 0 ? "" : target);
			return ExpandBraceParam(target + body[opAt..]);
			}

		// ── split into parameter (name + optional subscript) and operator ─────────
		int opStart = FindOperatorStart(raw);
		string param = opStart < 0 ? raw : raw[..opStart];
		string op    = opStart < 0 ? "" : raw[opStart..];

		string name = param; string? sub = null;
		int lb = param.IndexOf('[');
		if (lb > 0 && param.EndsWith(']')) { name = param[..lb]; sub = param[(lb + 1)..^1]; }

		bool isArrayAll = sub is "@" or "*";

		// values the operators act on
		string ValueOf()
			{
			if (sub is null) return _env.Get(name);
			if (isArrayAll)
				{
				if (_env.IsArray(name)) return string.Join(" ", _env.GetArrayValues(name));
				if (_env.IsAssoc(name)) return string.Join(" ", _env.GetAssocValues(name));
				return "";
				}
			return ElementValue(name, sub);
			}
		bool IsSetParam()
			{
			if (sub is null) return _env.IsSet(name);
			if (isArrayAll) return _env.GetArrayLength(name) > 0 || _env.GetAssocLength(name) > 0;
			if (_env.IsAssoc(name)) return _env.HasAssocElement(name, ExpandSubscript(sub));
			return _env.HasArrayElement(name, (int)EvalArithmetic(sub));
			}

		if (op.Length == 0) return ValueOf();

		// element-wise application for ${arr[@]<op>}
		List<string>? elems = null;
		if (isArrayAll)
			{
			if (_env.IsArray(name)) elems = _env.GetArrayValues(name);
			else if (_env.IsAssoc(name)) elems = _env.GetAssocValues(name);
			else elems = [];
			}

		// ${x:-w} ${x:=w} ${x:+w} ${x:?w} and the colon-less forms
		if (op[0] == ':' && op.Length > 1 && op[1] is '-' or '=' or '+' or '?'
		    || op[0] is '-' or '=' or '+' or '?')
			{
			bool colon = op[0] == ':';
			char kind  = colon ? op[1] : op[0];
			var word   = ExpandOperatorWord(op[(colon ? 2 : 1)..]);
			var val    = ValueOf();
			bool unsetOrNull = colon ? val.Length == 0 : !IsSetParam();
			switch (kind)
				{
				case '-': return unsetOrNull ? word : val;
				case '=':
					if (unsetOrNull) { if (sub is null) _env.Set(name, word); return word; }
					return val;
				case '+': return unsetOrNull ? "" : word;
				case '?':
					if (unsetOrNull) throw new FatalShellException(
						$"{param}: {(word.Length > 0 ? word : "parameter null or not set")}");
					return val;
				}
			}

		// ${x:offset[:length]} substring
		if (op[0] == ':')
			{
			var spec = op[1..];
			int colon = FindTopLevelColon(spec);
			var offStr = colon < 0 ? spec : spec[..colon];
			var lenStr = colon < 0 ? null : spec[(colon + 1)..];
			long off = offStr.Trim().Length == 0 ? 0 : EvalArithmetic(offStr);
			long? len = lenStr is null || lenStr.Trim().Length == 0 ? null : EvalArithmetic(lenStr);
			if (elems is not null)
				{
				var list = elems;
				if (name == "@" ) list = _env.GetPositionals();
				int start = off < 0 ? Math.Max(0, list.Count + (int)off) : (int)Math.Min(off, list.Count);
				int count = len is null ? list.Count - start : len < 0 ? Math.Max(0, list.Count + (int)len - start) : (int)Math.Min(len.Value, list.Count - start);
				return string.Join(" ", list.Skip(start).Take(Math.Max(0, count)));
				}
			if (name is "@" or "*")
				{
				var pos = _env.GetPositionals();
				var list = new List<string> { _env.Get("0") }; list.AddRange(pos);
				int start = off < 0 ? Math.Max(0, list.Count + (int)off) : (int)Math.Min(off, list.Count);
				int count = len is null ? list.Count - start : (int)Math.Min(len.Value, list.Count - start);
				return string.Join(" ", list.Skip(start).Take(Math.Max(0, count)));
				}
			var s = ValueOf();
			int st = off < 0 ? Math.Max(0, s.Length + (int)off) : (int)Math.Min(off, s.Length);
			if (len is null) return s[st..];
			if (len < 0) { int end = s.Length + (int)len; return end <= st ? "" : s[st..end]; }
			return s.Substring(st, (int)Math.Min(len.Value, s.Length - st));
			}

		// ${x^^pat} ${x,,pat} ${x^pat} ${x,pat} case modification
		if (op[0] is '^' or ',')
			{
			bool all = op.Length > 1 && op[1] == op[0];
			bool upper = op[0] == '^';
			var pat = op[(all ? 2 : 1)..];
			string Apply(string s)
				{
				if (s.Length == 0) return s;
				var chars = s.ToCharArray();
				int n = all ? chars.Length : 1;
				for (int i = 0; i < n; i++)
					{
					if (pat.Length > 0 && !Glob.Match(pat, chars[i].ToString())) continue;
					chars[i] = upper ? char.ToUpperInvariant(chars[i]) : char.ToLowerInvariant(chars[i]);
					}
				return new string(chars);
				}
			return elems is not null ? string.Join(" ", elems.Select(Apply)) : Apply(ValueOf());
			}

		// ${x@Q} ${x@U} ${x@u} ${x@L} ${x@E} ${x@a} ${x@A}
		if (op[0] == '@' && op.Length == 2)
			{
			string Xf(string s) => op[1] switch
				{
				'Q' => PrintfFormatter.ShellQuote(s).Length == 0 ? "''" : QuoteForQ(s),
				'U' => s.ToUpperInvariant(),
				'u' => s.Length > 0 ? char.ToUpperInvariant(s[0]) + s[1..] : s,
				'L' => s.ToLowerInvariant(),
				'E' => s,
				'a' => Attrs(name),
				'A' => $"{name}={QuoteForQ(s)}",
				_   => s,
				};
			return elems is not null ? string.Join(" ", elems.Select(Xf)) : Xf(ValueOf());
			}

		// ${x#pat} ${x##pat} ${x%pat} ${x%%pat}
		if (op[0] is '#' or '%')
			{
			bool greedy = op.Length > 1 && op[1] == op[0];
			var pat = op[(greedy ? 2 : 1)..];
			string Apply(string s) => op[0] == '#'
				? StripPrefix(s, pat, greedy)
				: StripSuffix(s, pat, greedy);
			return elems is not null ? string.Join(" ", elems.Select(Apply)) : Apply(ValueOf());
			}

		// ${x/pat/rep} ${x//pat/rep} ${x/#pat/rep} ${x/%pat/rep}
		if (op[0] == '/')
			{
			bool all = op.Length > 1 && op[1] == '/';
			var rest = op[(all ? 2 : 1)..];
			char anchor = '\0';
			if (rest.Length > 0 && rest[0] is '#' or '%') { anchor = rest[0]; rest = rest[1..]; }
			int sep = FindUnescapedSlash(rest);
			var pat = sep < 0 ? rest : rest[..sep];
			var rep = sep < 0 ? ""   : rest[(sep + 1)..];
			string Apply(string s) => PatternReplace(s, pat, rep, all, anchor);
			return elems is not null ? string.Join(" ", elems.Select(Apply)) : Apply(ValueOf());
			}

		throw new EvalException($"${{{raw}}}: bad substitution");
		}

	private string QuoteForQ(string s) => s.Length == 0 ? "''" : "'" + s.Replace("'", "'\\''") + "'";

	/// <summary>The word of `${x:-word}` / `${x:=word}` / `${x:+word}` / `${x:?word}` undergoes
	/// tilde, parameter, command and arithmetic expansion and quote removal (`${1+"$@"}`,
	/// `${dir:-$HOME}`, `${msg:-"a b"}`). Fields are joined with spaces here — the single
	/// value the caller needs — so an unquoted-vs-quoted `$@` distinction is not preserved.</summary>
	private string ExpandOperatorWord(string word)
		{
		if (word.Length == 0) return word;
		if (word.IndexOfAny(['$', '"', '\'', '`', '~', '\\']) < 0) return word;
		try
			{
			var words = Parser.Parser.ParseWords(word);
			return string.Join(" ", words.SelectMany(ExpandToFields));
			}
		catch (Exception ex) when (ex is Lexer.LexException or Parser.ParseException)
			{
			return word;
			}
		}

	private string Attrs(string name)
		{
		var sb = new System.Text.StringBuilder();
		if (_env.IsArray(name)) sb.Append('a');
		if (_env.IsAssoc(name)) sb.Append('A');
		if (_env.IsInteger(name)) sb.Append('i');
		if (_env.IsReadonly(name)) sb.Append('r');
		if (_env.IsExported(name)) sb.Append('x');
		return sb.ToString();
		}

	/// <summary>Value of name[sub]: arithmetic index for indexed arrays (negative counts
	/// from the end), expanded literal key for associative arrays.</summary>
	private string ElementValue(string name, string sub)
		{
		if (_env.IsAssoc(name)) return _env.GetAssocElement(name, ExpandSubscript(sub));
		if (_env.IsArray(name)) return _env.GetArrayElement(name, (int)EvalArithmetic(sub));
		// a scalar used with a subscript: index 0 is the scalar itself
		return EvalArithmetic(sub) == 0 ? _env.Get(name) : "";
		}

	/// <summary>Index of the first operator character after the parameter name (and its
	/// optional [subscript]); -1 when the whole string is a parameter.</summary>
	private static int FindOperatorStart(string raw)
		{
		int i = 0;
		if (raw.Length > 0 && (char.IsLetter(raw[0]) || raw[0] == '_'))
			while (i < raw.Length && (char.IsLetterOrDigit(raw[i]) || raw[i] == '_')) i++;
		else if (raw.Length > 0 && char.IsAsciiDigit(raw[0]))
			while (i < raw.Length && char.IsAsciiDigit(raw[i])) i++;
		else if (raw.Length > 0 && "@*#?$!-0".IndexOf(raw[0]) >= 0)
			i = 1;
		else
			return -1;
		if (i < raw.Length && raw[i] == '[')
			{
			int depth = 1; i++;
			while (i < raw.Length && depth > 0) { if (raw[i] == '[') depth++; else if (raw[i] == ']') depth--; i++; }
			}
		return i >= raw.Length ? -1 : i;
		}

	private static int FindTopLevelColon(string s)
		{
		int depth = 0;
		for (int i = 0; i < s.Length; i++)
			{
			if (s[i] == '(') depth++;
			else if (s[i] == ')') depth--;
			else if (s[i] == ':' && depth == 0) return i;
			}
		return -1;
		}

	private static int FindUnescapedSlash(string s)
		{
		for (int i = 0; i < s.Length; i++)
			{
			if (s[i] == '\\') { i++; continue; }
			if (s[i] == '/') return i;
			}
		return -1;
		}

	/// <summary>Pattern substitution: longest match of a glob pattern, first or all
	/// occurrences, optionally anchored at the start ('#') or end ('%').</summary>
	private string PatternReplace(string s, string pat, string rep, bool all, char anchor)
		{
		if (pat.Length == 0) return s;
		bool nocase = _eval.Options.NoCaseMatch;
		var body = Glob.ToRegexBody(pat);
		string rx = anchor switch { '#' => "^" + body, '%' => body + "$", _ => body };
		var re = new System.Text.RegularExpressions.Regex(rx,
			(nocase ? System.Text.RegularExpressions.RegexOptions.IgnoreCase : 0) | System.Text.RegularExpressions.RegexOptions.Singleline);
		var repl = rep.Replace("$", "$$");
		if (anchor != '\0' || !all)
			{
			var m = re.Match(s);
			return m.Success ? s[..m.Index] + rep + s[(m.Index + m.Length)..] : s;
			}
		return re.Replace(s, repl);
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
	/// If a field contains unquoted glob characters (* ? [) and matches at least one
	/// filesystem entry, returns the sorted matches (with the pattern's own directory
	/// prefix and separator, never a synthetic `.\`). No match: the field itself, or
	/// nothing under `nullglob`, or an error under `failglob`.
	/// </summary>
	private List<string> GlobExpand(string field)
		{
		if (!ContainsGlob(field)) return [field];
		var o = _eval.Options;
		List<string> matches;
		try { matches = Glob.Expand(field, o.DotGlob, o.NoCaseGlob, o.GlobStar); }
		catch { matches = []; }
		if (matches.Count > 0) return matches;
		if (o.FailGlob) throw new EvalException($"no match: {field}");
		if (o.NullGlob) return [];
		return [field];
		}

	private static bool ContainsGlob(string s)
		{
		foreach (char c in s)
			if (c is '*' or '?' or '[') return true;
		return false;
		}

	// ── command substitution ──────────────────────────────────────────────────

	/// <summary>Run a `$( )` body as a subshell: output captured (trailing newlines
	/// trimmed), variable/cwd changes discarded, `exit` ends only the substitution. The
	/// exit status is recorded in <see cref="LastSubstStatus"/> and `$?`.</summary>
	private string RunSubstitution(Node command)
		{
		var captured = new System.Text.StringBuilder();
		var oldOut = ConsoleMux.OutSlot;
		var oldRaw = ConsoleMux.Raw;
		bool oldCap = ConsoleMux.Capturing;
		using var sw = new System.IO.StringWriter(captured) { NewLine = "\n" };
		var snap = _env.TakeSnapshot();
		int status;
		ConsoleMux.SetOut(sw);
		ConsoleMux.Raw = null;
		ConsoleMux.Capturing = true;   // byte-builtins must write text into the capture
		try   { status = _eval.Execute(command); }
		catch (ExitException ex) { status = ex.Code; }
		catch (BrokenPipeException) { status = 141; }
		finally
			{
			ConsoleMux.SetOut(oldOut); ConsoleMux.Raw = oldRaw; ConsoleMux.Capturing = oldCap;
			_env.RestoreSnapshot(snap);
			}
		LastSubstStatus = status;
		_env.LastExitCode = status;
		return captured.ToString().TrimEnd('\n');
		}

	// ── process substitution (DECISIONS 2026-09-04 #6: temp-file emulation) ──────

	/// <summary>Temp files created by `&lt;( )` on this thread, released by the evaluator
	/// once the node whose expansion created them has finished (see Evaluator.Execute).</summary>
	[ThreadStatic] private static List<string>? _procSubFiles;

	public static int ProcSubMark() => _procSubFiles?.Count ?? 0;

	public static void ReleaseProcSubs(int mark)
		{
		var list = _procSubFiles;
		if (list is null || list.Count <= mark) return;
		for (int i = list.Count - 1; i >= mark; i--)
			{
			try { File.Delete(list[i]); } catch { }
			list.RemoveAt(i);
			}
		}

	/// <summary>`&lt;(cmd)`: run cmd to completion with stdout in a temp file; expand to its
	/// (native) path. The file lives until the enclosing command has finished.</summary>
	private string RunProcessSubstitution(Node command)
		{
		var path = Path.Combine(Path.GetTempPath(), "bash-procsub-" + Guid.NewGuid().ToString("N")[..12]);
		var oldOut = ConsoleMux.OutSlot;
		var oldRaw = ConsoleMux.Raw;
		bool oldCap = ConsoleMux.Capturing;
		var snap = _env.TakeSnapshot();
		int status;
		using (var fs = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read))
		using (var sw = new StreamWriter(fs, ShellEncoding.Utf8) { NewLine = "\n", AutoFlush = true })
			{
			ConsoleMux.SetOut(sw);
			ConsoleMux.Raw = fs;
			ConsoleMux.Capturing = false;
			try   { status = _eval.Execute(command); }
			catch (ExitException ex) { status = ex.Code; }
			catch (BrokenPipeException) { status = 141; }
			finally
				{
				try { sw.Flush(); } catch { }
				ConsoleMux.SetOut(oldOut); ConsoleMux.Raw = oldRaw; ConsoleMux.Capturing = oldCap;
				_env.RestoreSnapshot(snap);
				}
			}
		(_procSubFiles ??= []).Add(path);
		_ = status;   // like bash, the substitution's status does not become $?
		return path;
		}

	// ── arithmetic expansion ──────────────────────────────────────────────────

	private sealed class ArithVars(WordExpander owner) : IArithVars
		{
		private readonly WordExpander _o = owner;
		private int _depth;

		public long Get(string name, string? subscript)
			{
			var env = _o._env;
			string val;
			if (subscript is not null)
				{
				if (env.IsAssoc(name)) val = env.GetAssocElement(name, _o.ExpandSubscript(subscript));
				else val = env.GetArrayElement(name, (int)_o.EvalArithmetic(subscript));
				}
			else val = env.Get(name);
			if (val.Length == 0) return 0;
			if (long.TryParse(val, out var n)) return n;
			// bash evaluates a non-numeric value as an expression itself (x="y+1")
			if (_depth > 8) return 0;
			_depth++;
			try { return _o.EvalArithmetic(val); }
			catch (EvalException) { return 0; }
			finally { _depth--; }
			}

		public void Set(string name, string? subscript, long value)
			{
			var env = _o._env;
			if (subscript is not null)
				{
				if (env.IsAssoc(name)) env.SetAssocElement(name, _o.ExpandSubscript(subscript), value.ToString());
				else env.SetArrayElement(name, (int)_o.EvalArithmetic(subscript), value.ToString());
				}
			else env.Set(name, value.ToString());
			}
		}

	private ArithVars? _arithVars;

	public long EvalArithmetic(string expr)
		{
		expr = ExpandVarsInArith(expr.Trim());
		_arithVars ??= new ArithVars(this);
		return ArithParser.Evaluate(expr, _arithVars);
		}

	public string ExpandArith(string expr) => ExpandVarsInArith(expr.Trim());

	/// <summary>Perform the `$var` / `${…}` / `$(…)` expansions inside an arithmetic
	/// expression before it is parsed. Bare identifiers are left for the parser, which
	/// resolves (and can assign) them through <see cref="IArithVars"/>.</summary>
	private string ExpandVarsInArith(string expr)
		{
		if (!expr.Contains('$')) return expr;
		var sb = new System.Text.StringBuilder();
		int i = 0;
		while (i < expr.Length)
			{
			if (expr[i] == '$' && i + 1 < expr.Length)
				{
				if (expr[i + 1] == '{')
					{
					i += 2;
					int depth = 1, start = i;
					while (i < expr.Length && depth > 0)
						{
						if (expr[i] == '{') depth++;
						else if (expr[i] == '}') { depth--; if (depth == 0) break; }
						i++;
						}
					var v = ExpandBraceParam(expr[start..i]);
					if (i < expr.Length) i++;
					sb.Append(v.Length == 0 ? "0" : v);
					continue;
					}
				if (expr[i + 1] == '(' && i + 2 < expr.Length && expr[i + 2] == '(')
					{
					// nested $(( )) : evaluate and splice the value
					i += 3;
					int depth = 2, start = i;
					while (i < expr.Length && depth > 0)
						{
						if (expr[i] == '(') depth++;
						else if (expr[i] == ')') { depth--; if (depth == 0) break; }
						i++;
						}
					var inner = expr[start..Math.Max(start, i - 1)];
					if (i < expr.Length) i++;
					sb.Append(EvalArithmetic(inner));
					continue;
					}
				if (expr[i + 1] == '(')
					{
					// $( ) inside arithmetic: run it and splice the output
					i += 2;
					int depth = 1, start = i;
					while (i < expr.Length && depth > 0)
						{
						if (expr[i] == '(') depth++;
						else if (expr[i] == ')') { depth--; if (depth == 0) break; }
						i++;
						}
					var body = expr[start..i];
					if (i < expr.Length) i++;
					var tokens = new Lexer.Lexer(body).Tokenize();
					var ast = new Parser.Parser(tokens, body).Parse();
					var v = RunSubstitution(ast);
					sb.Append(v.Length == 0 ? "0" : v);
					continue;
					}
				i++;
				int s0 = i;
				while (i < expr.Length && (char.IsLetterOrDigit(expr[i]) || expr[i] == '_')) i++;
				if (i == s0 && i < expr.Length && "#?$!@*".IndexOf(expr[i]) >= 0) i++;
				var val = _env.Get(expr[s0..i]);
				sb.Append(val.Length == 0 ? "0" : val);
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

	private string StripPrefix(string val, string pat, bool greedy)
		{
		bool nocase = _eval.Options.NoCaseMatch;
		if (!Glob.HasMeta(pat))
			return val.StartsWith(pat, nocase ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal) ? val[pat.Length..] : val;
		var re = Glob.ToRegex(pat, nocase);
		if (greedy)
			{
			for (int i = val.Length; i >= 0; i--)
				if (re.IsMatch(val[..i])) return val[i..];
			}
		else
			{
			for (int i = 0; i <= val.Length; i++)
				if (re.IsMatch(val[..i])) return val[i..];
			}
		return val;
		}

	private string StripSuffix(string val, string pat, bool greedy)
		{
		bool nocase = _eval.Options.NoCaseMatch;
		if (!Glob.HasMeta(pat))
			return val.EndsWith(pat, nocase ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal) ? val[..^pat.Length] : val;
		var re = Glob.ToRegex(pat, nocase);
		if (greedy)
			{
			for (int i = 0; i <= val.Length; i++)
				if (re.IsMatch(val[i..])) return val[..i];
			}
		else
			{
			for (int i = val.Length; i >= 0; i--)
				if (re.IsMatch(val[i..])) return val[..i];
			}
		return val;
		}
	}
