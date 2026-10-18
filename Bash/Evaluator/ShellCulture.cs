using System.Globalization;

namespace Bash.Evaluator;

/// <summary>
/// The shell formats and reads numbers and dates as bash's C locale does, whatever the Windows user's culture
/// (ARCHITECTURE 9.4). Before 2026-10-18 every `long.ToString()` (each `$(( ))` result, `$?`, …) used the user's
/// culture, so on one whose minus sign is U+2212 (sv-SE and others, verified in-process) `echo $((-5))` printed
/// "−5", and a culture's date separator reached `find -printf %Tx`. Nothing in the shell uses the user's culture on
/// purpose (no explicit CurrentCulture, no culture casing or collation calls), so the process takes the invariant
/// culture at start-up, every thread included; culture data stays where it is the behaviour (non-ASCII case mapping,
/// local time), and setting it loads no ICU.
/// </summary>
public static class ShellCulture
	{
	/// <summary>Called first thing in Main, before any thread starts. The default alone: a thread that never set its
	/// own culture (none in the shell does) takes it over the user's, measured in-process; also setting
	/// CultureInfo.CurrentCulture (an AsyncLocal) cost ~0.13 ms a start. Case icu_lazy's `echo $((-5))` checks it.</summary>
	public static void Pin() => CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
	}
