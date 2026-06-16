namespace Bash.Evaluator;

/// <summary>
/// Shell option flags set via 'set -e', 'set -x', 'set -u', etc.
/// Shared between Evaluator and Builtins.
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

	/// <summary>-o pipefail: pipeline fails if any stage fails.</summary>
	public bool PipeFail { get; set; } = false;

	/// <summary>Apply a flag letter (with sign).</summary>
	public void Apply(char flag, bool enable)
		{
		switch (flag)
			{
			case 'e': ExitOnError = enable; break;
			case 'u': UnsetError  = enable; break;
			case 'x': XTrace      = enable; break;
			case 'n': NoExec      = enable; break;
			case 'f': NoGlob      = enable; break;
			}
		}

	/// <summary>Apply a long option name.</summary>
	public void ApplyLong(string name, bool enable)
		{
		switch (name)
			{
			case "errexit":   ExitOnError = enable; break;
			case "nounset":   UnsetError  = enable; break;
			case "xtrace":    XTrace      = enable; break;
			case "noexec":    NoExec      = enable; break;
			case "noglob":    NoGlob      = enable; break;
			case "pipefail":  PipeFail    = enable; break;
			}
		}
	}
