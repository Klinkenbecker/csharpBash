namespace Bash.Evaluator;

/// <summary>
/// Raised by an in-process coreutil for an option (or option value) it does not
/// implement. The dispatcher applies DECISIONS 2026-09-04 #2: fall through to a PATH
/// external of the same name when one exists, otherwise fail loudly with exit 2.
/// Never silently ignore.
/// </summary>
public sealed class UnsupportedOptionException(string tool, string option, string? reason = null)
	: Exception(reason is null ? $"{tool}: invalid option -- '{option.TrimStart('-')}'" : $"{tool}: {option}: {reason}")
	{
	public string Tool { get; } = tool;
	public string Option { get; } = option;
	}

/// <summary>A value an option cannot take (a number that is not one, or below its least): the tool refuses it in
/// GNU's own words and exit code, as GNU does. Not a fall-through, not an unserviced request (2026-10-30).</summary>
public sealed class OptionValueException(string message, int exitCode) : Exception(message)
	{
	public int ExitCode { get; } = exitCode;
	}

/// <summary>Why <see cref="Opts.Int"/> refused a value, for the tool's own words.</summary>
public enum IntProblem { NotANumber, Negative, BelowLeast }

/// <summary>`--help` met as an option (not as an option's value, nor after the operands of a tool that stops at its
/// first one): the builtin answers with its manual page from help.json (DECISIONS 2026-10-27).</summary>
public sealed class HelpRequestedException(string tool) : Exception($"{tool}: --help")
	{
	public string Tool { get; } = tool;
	}

/// <summary>
/// GNU-style option parsing for the in-process coreutils. Short options cluster
/// (`-ni`), take attached or separate values (`-n5`, `-n 5`), long options take
/// `--name=value` or `--name value`; `--` ends options; `-` is an operand. Anything not in
/// the accepted set throws <see cref="UnsupportedOptionException"/> — the point of the
/// exercise (strict, so nothing is silently ignored).
/// </summary>
public sealed class Opts
	{
	private readonly HashSet<char> _flags = [];
	private readonly Dictionary<char, string> _values = [];
	private readonly List<(char opt, string val)> _all = [];
	private readonly Dictionary<string, string?> _longs = new(StringComparer.Ordinal);
	private readonly List<(string name, string? val)> _longAll = [];   // every long option in order (a repeated one keeps each value)
	private readonly List<(char opt, string? val)> _sequence = [];
	public List<string> Operands { get; } = [];
	/// <summary>Every short option (and long option aliased to one) in command-line order, with its
	/// value if it takes one. For tools where the last of several options wins (df's -h/-k/-B).</summary>
	public IReadOnlyList<(char opt, string? val)> Sequence => _sequence;
	/// <summary>Set when a bare numeric option (`-5`) was given and allowed.</summary>
	public string? Numeric { get; private set; }

	public bool Has(char c) => _flags.Contains(c);
	public string? Get(char c) => _values.TryGetValue(c, out var v) ? v : null;
	public IEnumerable<string> All(char c) => _all.Where(p => p.opt == c).Select(p => p.val);
	public bool HasLong(string name) => _longs.ContainsKey(name);
	public string? GetLong(string name) => _longs.TryGetValue(name, out var v) ? v : null;
	/// <summary>Every value of a repeated long option, in order (`diff --label A --label B`).</summary>
	public IEnumerable<string> AllLong(string name) => _longAll.Where(p => p.name == name && p.val is not null).Select(p => p.val!);

	/// <summary>-<paramref name="c"/> as an integer: <paramref name="def"/> when it is absent; when it is not a whole
	/// number, or below <paramref name="least"/>, the tool refuses it with <paramref name="refuse"/>'s words (from the
	/// value as given and why) and <paramref name="exitCode"/>, as GNU's do. It replaces GetInt (removed 2026-10-30),
	/// which took such a value as the default (`fold -w x` folded at 80, `uniq -f x` skipped nothing).</summary>
	public int Int(char c, int def, int least, Func<string, IntProblem, string> refuse, int exitCode = 1)
		{
		var v = Get(c);
		if (v is null) return def;
		if (!int.TryParse(v, System.Globalization.NumberStyles.AllowLeadingSign, System.Globalization.CultureInfo.InvariantCulture, out var n))
			throw new OptionValueException(refuse(v, IntProblem.NotANumber), exitCode);
		if (n < least) throw new OptionValueException(refuse(v, n < 0 ? IntProblem.Negative : IntProblem.BelowLeast), exitCode);
		return n;
		}

	/// <param name="tool">Tool name for messages.</param>
	/// <param name="args">Arguments after the command name.</param>
	/// <param name="shortSpec">Accepted short options; a trailing ':' marks one taking a value.</param>
	/// <param name="longSpec">Accepted long options; a trailing '=' marks one taking a value,
	/// "=?" one whose value is optional (only `--name=value` supplies it).
	/// A long option may map onto a short one as "name=:c" (value) or "name:c" (flag).</param>
	/// <param name="stopAtFirstOperand">Stop option parsing at the first operand (xargs, env, timeout).</param>
	/// <param name="allowNumeric">Accept `-N` as a numeric option (head/tail/uniq style).</param>
	public static Opts Parse(string tool, IReadOnlyList<string> args, string shortSpec, string[]? longSpec = null,
		bool stopAtFirstOperand = false, bool allowNumeric = false)
		{
		var takesValue = new HashSet<char>();
		var optionalValue = new HashSet<char>();   // "c::" — value only when attached (-i.bak)
		var known = new HashSet<char>();
		for (int i = 0; i < shortSpec.Length; i++)
			{
			char c = shortSpec[i];
			if (c == ':') continue;
			known.Add(c);
			if (i + 2 < shortSpec.Length && shortSpec[i + 1] == ':' && shortSpec[i + 2] == ':') optionalValue.Add(c);
			else if (i + 1 < shortSpec.Length && shortSpec[i + 1] == ':') takesValue.Add(c);
			}
		var longKnown = new Dictionary<string, (bool value, char? alias)>(StringComparer.Ordinal);
		if (longSpec is not null)
			foreach (var l in longSpec)
				{
				var name = l; char? alias = null;
				int colon = name.IndexOf(':');
				if (colon >= 0) { alias = name[colon + 1]; name = name[..colon]; }
				bool optional = name.EndsWith("=?", StringComparison.Ordinal);   // value only via --name=value
					if (optional) name = name[..^2];
					bool value = !optional && name.EndsWith('=');
				if (value) name = name[..^1];
				longKnown[name] = (value, alias);
				}

		var o = new Opts();
		bool endOfOptions = false;
		for (int i = 0; i < args.Count; i++)
			{
			var a = args[i];
			if (endOfOptions || a == "-" || a.Length < 2 || a[0] != '-')
				{
				o.Operands.Add(a);
				if (stopAtFirstOperand) { for (int k = i + 1; k < args.Count; k++) o.Operands.Add(args[k]); break; }
				continue;
				}
			if (a == "--") { endOfOptions = true; continue; }
			if (a.StartsWith("--", StringComparison.Ordinal))
				{
				var body = a[2..];
				string name = body; string? val = null;
				int eq = body.IndexOf('=');
				if (eq >= 0) { name = body[..eq]; val = body[(eq + 1)..]; }
				if (name == "help" && val is null && !longKnown.ContainsKey(name)) throw new HelpRequestedException(tool);
				if (!longKnown.TryGetValue(name, out var spec))
					throw new UnsupportedOptionException(tool, "--" + name, "unrecognized option");
				if (spec.value && val is null)
					{
					if (i + 1 >= args.Count) throw new UnsupportedOptionException(tool, "--" + name, "option requires an argument");
					val = args[++i];
					}
				o._longs[name] = val;
				o._longAll.Add((name, val));
				if (spec.alias is char ac)
					{
					if (spec.value) { o._values[ac] = val!; o._all.Add((ac, val!)); }
					o._flags.Add(ac);
					o._sequence.Add((ac, val));
					}
				continue;
				}
			// short cluster
			if (allowNumeric && a.Length > 1 && char.IsAsciiDigit(a[1]) && a[1..].All(char.IsAsciiDigit))
				{ o.Numeric = a[1..]; continue; }
			for (int k = 1; k < a.Length; k++)
				{
				char c = a[k];
				if (!known.Contains(c))
					{
					if (allowNumeric && char.IsAsciiDigit(c))
						{ o.Numeric = a[k..]; break; }
					throw new UnsupportedOptionException(tool, "-" + c);
					}
				o._flags.Add(c);
				if (optionalValue.Contains(c))
					{
					string? ov = k + 1 < a.Length ? a[(k + 1)..] : null;
					if (ov is not null) { o._values[c] = ov; o._all.Add((c, ov)); }
					o._sequence.Add((c, ov));
					break;
					}
				if (takesValue.Contains(c))
					{
					string val;
					if (k + 1 < a.Length) val = a[(k + 1)..];
					else if (i + 1 < args.Count) val = args[++i];
					else throw new UnsupportedOptionException(tool, "-" + c, "option requires an argument");
					o._values[c] = val;
					o._all.Add((c, val));
					o._sequence.Add((c, val));
					break;
					}
				o._sequence.Add((c, null));
				}
			}
		return o;
		}
	}
