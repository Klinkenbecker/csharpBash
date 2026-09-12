namespace Bash.Evaluator;

/// <summary>
/// Shell option flags (`set -e/-u/-x/…`, `set -o name`), the `shopt` option set, and the
/// invocation facts that feed `$-`. Shared between Evaluator, Builtins and Program.
/// </summary>
public sealed class ShellOptions
	{
	/// <summary>-e: exit immediately if a command fails.</summary>
	public bool ExitOnError { get; set; } = false;

	/// <summary>-u: treat unset variables as an error.</summary>
	public bool UnsetError { get; set; } = false;

	/// <summary>-x: print each command before executing (xtrace).</summary>
	public bool XTrace { get; set; } = false;

	/// <summary>-n: read commands but do not execute them.</summary>
	public bool NoExec { get; set; } = false;

	/// <summary>-f: disable glob expansion.</summary>
	public bool NoGlob { get; set; } = false;

	/// <summary>-v: echo input lines as they are read.</summary>
	public bool Verbose { get; set; } = false;

	/// <summary>-C: refuse to overwrite existing files with `>` (use `>|`).</summary>
	public bool NoClobber { get; set; } = false;

	/// <summary>-a: export every assigned variable.</summary>
	public bool AllExport { get; set; } = false;

	/// <summary>-o pipefail: pipeline fails if any stage fails.</summary>
	public bool PipeFail { get; set; } = false;

	/// <summary>-E / -o errtrace: the ERR trap also fires inside functions.</summary>
	public bool ErrTrace { get; set; } = false;

	/// <summary>-h: remember command locations (always on; listed for `set -o`).</summary>
	public bool HashAll { get; set; } = true;

	/// <summary>-B: brace expansion (always on; listed for `set -o`).</summary>
	public bool BraceExpand { get; set; } = true;

	// ── invocation facts (not settable via `set`) ───────────────────────────────
	public bool Interactive   { get; set; } = false;   // -i or REPL
	public bool CommandString { get; set; } = false;   // -c
	public bool ReadStdin     { get; set; } = false;   // -s / no script
	public bool LoginShell    { get; set; } = false;   // -l

	/// <summary>Apply a flag letter (with sign). Returns false for an unknown letter.</summary>
	public bool Apply(char flag, bool enable)
		{
		switch (flag)
			{
			case 'e': ExitOnError = enable; return true;
			case 'u': UnsetError  = enable; return true;
			case 'x': XTrace      = enable; return true;
			case 'n': NoExec      = enable; return true;
			case 'f': NoGlob      = enable; return true;
			case 'v': Verbose     = enable; return true;
			case 'C': NoClobber   = enable; return true;
			case 'a': AllExport   = enable; return true;
			case 'h': HashAll     = enable; return true;
			case 'B': BraceExpand = enable; return true;
			case 'E': ErrTrace    = enable; return true;
			case 'b': case 'k': case 'm': case 'p': case 't': case 'H': case 'P': case 'T':
				return true;    // accepted, no effect here
			}
		return false;
		}

	/// <summary>`set -o` option names in bash's listing order, with their current state.</summary>
	public IEnumerable<(string Name, bool On)> LongOptions()
		{
		yield return ("allexport",   AllExport);
		yield return ("braceexpand", BraceExpand);
		yield return ("emacs",       Interactive);
		yield return ("errexit",     ExitOnError);
		yield return ("errtrace",    ErrTrace);
		yield return ("functrace",   false);
		yield return ("hashall",     HashAll);
		yield return ("histexpand",  Interactive);
		yield return ("history",     Interactive);
		yield return ("ignoreeof",   false);
		yield return ("interactive-comments", true);
		yield return ("keyword",     false);
		yield return ("monitor",     Interactive);
		yield return ("noclobber",   NoClobber);
		yield return ("noexec",      NoExec);
		yield return ("noglob",      NoGlob);
		yield return ("nolog",       false);
		yield return ("notify",      false);
		yield return ("nounset",     UnsetError);
		yield return ("onecmd",      false);
		yield return ("physical",    false);
		yield return ("pipefail",    PipeFail);
		yield return ("posix",       false);
		yield return ("privileged",  false);
		yield return ("verbose",     Verbose);
		yield return ("vi",          false);
		yield return ("xtrace",      XTrace);
		}

	/// <summary>Apply a long option name. Returns false for an unknown name.</summary>
	public bool ApplyLong(string name, bool enable)
		{
		switch (name)
			{
			case "errexit":     ExitOnError = enable; return true;
			case "nounset":     UnsetError  = enable; return true;
			case "xtrace":      XTrace      = enable; return true;
			case "noexec":      NoExec      = enable; return true;
			case "noglob":      NoGlob      = enable; return true;
			case "verbose":     Verbose     = enable; return true;
			case "noclobber":   NoClobber   = enable; return true;
			case "allexport":   AllExport   = enable; return true;
			case "pipefail":    PipeFail    = enable; return true;
			case "hashall":     HashAll     = enable; return true;
			case "braceexpand": BraceExpand = enable; return true;
			case "errtrace":    ErrTrace    = enable; return true;
			case "emacs": case "vi": case "functrace": case "histexpand":
			case "history": case "ignoreeof": case "interactive-comments": case "keyword":
			case "monitor": case "nolog": case "notify": case "onecmd": case "physical":
			case "posix": case "privileged":
				return true;    // accepted, no effect here
			}
		return false;
		}

	/// <summary>The value of `$-`, in bash's flag order.</summary>
	public string FlagString()
		{
		var sb = new System.Text.StringBuilder();
		if (AllExport)   sb.Append('a');
		if (ExitOnError) sb.Append('e');
		if (NoGlob)      sb.Append('f');
		if (HashAll)     sb.Append('h');
		if (Interactive) sb.Append('i');
		if (Interactive) sb.Append('m');
		if (NoExec)      sb.Append('n');
		if (ReadStdin)   sb.Append('s');
		if (UnsetError)  sb.Append('u');
		if (Verbose)     sb.Append('v');
		if (XTrace)      sb.Append('x');
		if (BraceExpand) sb.Append('B');
		if (NoClobber)   sb.Append('C');
		if (ErrTrace)    sb.Append('E');
		if (Interactive) sb.Append('H');
		if (CommandString) sb.Append('c');
		return sb.ToString();
		}

	// ── shopt ────────────────────────────────────────────────────────────────────

	/// <summary>Every name `shopt` accepts. Names outside this set are an error, as in bash.
	/// Options this interpreter does not implement are accepted but have no effect, except
	/// those in <see cref="UnsupportedShopts"/>, which refuse to be enabled (loud).</summary>
	public static readonly string[] KnownShopts =
		[
		"autocd", "assoc_expand_once", "cdable_vars", "cdspell", "checkhash", "checkjobs",
		"checkwinsize", "cmdhist", "compat31", "compat32", "compat40", "compat41", "compat42",
		"compat43", "compat44", "complete_fullquote", "completion_strip_exe", "direxpand",
		"dirspell", "dotglob", "execfail", "expand_aliases", "extdebug", "extglob", "extquote",
		"failglob", "force_fignore", "globasciiranges", "globskipdots", "globstar", "gnu_errfmt",
		"histappend", "histreedit", "histverify", "hostcomplete", "huponexit", "inherit_errexit",
		"interactive_comments", "lastpipe", "lithist", "localvar_inherit", "localvar_unset",
		"login_shell", "mailwarn", "no_empty_cmd_completion", "nocaseglob", "nocasematch",
		"noexpand_translation", "nullglob", "patsub_replacement", "progcomp", "progcomp_alias",
		"promptvars", "restricted_shell", "shift_verbose", "sourcepath", "varredir_close",
		"xpg_echo",
		];

	/// <summary>Options whose *enabling* would silently change matching semantics we don't
	/// implement — `shopt -s` of these fails loudly rather than pretending.</summary>
	public static readonly HashSet<string> UnsupportedShopts = ["extglob", "extdebug", "restricted_shell"];

	private readonly HashSet<string> _shopt =
		[
		"checkwinsize", "cmdhist", "complete_fullquote", "extquote", "force_fignore",
		"globasciiranges", "hostcomplete", "interactive_comments", "progcomp", "promptvars",
		"sourcepath", "patsub_replacement",
		];

	public bool Shopt(string name) => _shopt.Contains(name);
	public bool IsKnownShopt(string name) => Array.IndexOf(KnownShopts, name) >= 0;

	/// <summary>Set or clear a shopt. Returns an error message, or null on success.</summary>
	public string? SetShopt(string name, bool enable)
		{
		if (!IsKnownShopt(name)) return $"{name}: invalid shell option name";
		if (enable && UnsupportedShopts.Contains(name)) return $"{name}: not supported by this interpreter";
		if (enable) _shopt.Add(name); else _shopt.Remove(name);
		return null;
		}

	// Convenience accessors for the options the evaluator consults.
	public bool NullGlob       => _shopt.Contains("nullglob");
	public bool FailGlob       => _shopt.Contains("failglob");
	public bool DotGlob        => _shopt.Contains("dotglob");
	public bool GlobStar       => _shopt.Contains("globstar");
	public bool NoCaseGlob     => _shopt.Contains("nocaseglob");
	public bool NoCaseMatch    => _shopt.Contains("nocasematch");
	public bool ExpandAliases  => _shopt.Contains("expand_aliases");
	public bool XpgEcho        => _shopt.Contains("xpg_echo");
	public bool LastPipe       => _shopt.Contains("lastpipe");
	}
