namespace Bash.Evaluator;

/// <summary>The build stamp, "1.0.0+hg.&lt;hash&gt;[+] &lt;rev&gt;[+]", written by Bash.csproj from `hg id` at build time.</summary>
public static class BuildInfo
	{
	public static string Stamp { get; } = System.Reflection.Assembly.GetEntryAssembly()
		?.GetCustomAttributes(typeof(System.Reflection.AssemblyInformationalVersionAttribute), false)
		.OfType<System.Reflection.AssemblyInformationalVersionAttribute>().FirstOrDefault()?.InformationalVersion ?? "unknown";
	}

/// <summary>
/// A feature C#Bash does not have says so, with a link that opens a NEW GitHub issue, title and body
/// filled in, for the user to send under their own account (DECISIONS 2026-10-11: never an automatic
/// post, which would need a write token inside an MIT binary and would be telemetry without consent).
/// The link carries the feature and the build only: never the command line, which can hold secrets.
/// </summary>
public static class NotAvailable
	{
	public const string Repository = "https://github.com/klinkenbecker/csharpBash";

	/// <summary>The two lines to print, each after <paramref name="prefix"/>; <paramref name="goingOn"/> when
	/// the shell carries on without the feature (POSIX mode) rather than refusing (restricted mode).</summary>
	public static string[] Lines(string prefix, string feature, bool goingOn = false) =>
		[
		$"{prefix}{feature} is not currently available in C#Bash{(goingOn ? "; going on in bash's default mode" : "")}",
		$"{prefix}to ask for it, open: {IssueLink(feature)}",
		];

	/// <summary>Turning on POSIX mode (`--posix`, `-o posix`, `set -o posix`, `shopt -so posix`): it was
	/// accepted and silently ignored. It only narrows bash's behaviour, so scripts that ask for it mostly
	/// run as they are: say so and go on (DECISIONS 2026-10-11).</summary>
	public static void PosixMode(string prefix, string how)
		{
		foreach (var line in Lines(prefix, $"POSIX mode ({how})", goingOn: true)) Console.Error.WriteLine(line);
		}

	public static string IssueLink(string feature) =>
		$"{Repository}/issues/new?title={Uri.EscapeDataString("Build request: " + feature)}"
		+ $"&body={Uri.EscapeDataString($"Requested from C#Bash build {BuildInfo.Stamp}")}";   // no final '.': terminals drop it from a link
	}
