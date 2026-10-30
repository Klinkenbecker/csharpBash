using System.Text.RegularExpressions;

namespace Bash.Evaluator;

/// <summary>
/// The shell's time zone: TZ as glibc reads it, for every tool that prints or parses a time (date, ls, stat, find,
/// diff's headers, touch -d). TZ was ignored everywhere until 2026-10-30, so `TZ=UTC date +%H%M%SZ` stamped local
/// time with a Z. glibc's rules, measured (WSL, a fixed epoch): unset is the machine's own zone (here Windows'); set
/// but empty is UTC; UTC, GMT, UTC0, Etc/UTC and the like are UTC, named as given; an IANA name (`Asia/Tokyo`,
/// `:Europe/London`, `EST5EDT`) is that zone; a POSIX `NAME[+-]h[:mm[:ss]]` is that fixed offset, the sign WEST of
/// Greenwich (`UTC+3` is -0300), its DST part honoured only through an IANA zone of its name (`CET-1CEST` as `CET`);
/// anything else is UTC under the name's leading letters (`Foo/Bar`: Foo). Times handed around are wall-clock times
/// in this zone (DateTimeKind.Unspecified, or Local when the zone is the machine's own).
/// </summary>
internal static class ShellClock
	{
	private sealed record Resolved(string Tz, TimeZoneInfo Zone, string? Std, string? Dst, bool Iana);

	[ThreadStatic] private static Resolved? t_last;
	private static readonly Resolved LocalZone = new("", TimeZoneInfo.Local, null, null, false);
	private static int s_warned;

	private static readonly Regex Posix = new(@"^(?<std>[A-Za-z]{3,}|<[^>]+>)(?<off>[+-]?\d{1,2}(?::\d{2}){0,2})(?<dst>[A-Za-z]{3,}|<[^>]+>)?", RegexOptions.CultureInvariant);

	private static Resolved Current
		{
		get
			{
			var env = ShellEnvironment.Active;
			bool set = env?.IsSet("TZ") ?? Environment.GetEnvironmentVariable("TZ") is not null;
			if (!set) return LocalZone;
			var tz = env?.Get("TZ") ?? Environment.GetEnvironmentVariable("TZ") ?? "";
			var last = t_last;
			if (last is not null && last.Tz == tz) return last;
			return t_last = Resolve(tz);
			}
		}

	private static Resolved Resolve(string tz)
		{
		var name = tz.StartsWith(':') ? tz[1..] : tz;
		if (name.Length == 0) return new(tz, TimeZoneInfo.Utc, "UTC", null, false);
		if (name is "UTC" or "UTC0" or "Etc/UTC" or "Universal" or "Etc/Universal" or "Zulu" or "Etc/Zulu" or "UCT" or "Etc/UCT")
			return new(tz, TimeZoneInfo.Utc, "UTC", null, false);
		if (name is "GMT" or "GMT0" or "Etc/GMT" or "Greenwich" or "Etc/Greenwich" or "GMT+0" or "GMT-0")
			return new(tz, TimeZoneInfo.Utc, "GMT", null, false);
		var posix = Posix.Match(name);
		string? std = posix.Success ? posix.Groups["std"].Value.Trim('<', '>') : null;
		string? dst = posix.Success && posix.Groups["dst"].Success ? posix.Groups["dst"].Value.Trim('<', '>') : null;
		if (Find(name) is { } z) return new(tz, z, std, dst, std is null);
		if (posix.Success)
			{
			// a DST part: the zone of its standard name carries the rules (`CET-1CEST` as IANA's CET), else a fixed offset
			if (dst is not null && Find(std!) is { } byName) return new(tz, byName, std, dst, false);
			var parts = posix.Groups["off"].Value.TrimStart('+', '-').Split(':');
			var west = new TimeSpan(int.Parse(parts[0]), parts.Length > 1 ? int.Parse(parts[1]) : 0, parts.Length > 2 ? int.Parse(parts[2]) : 0);
			var offset = posix.Groups["off"].Value.StartsWith('-') ? west : -west;
			return new(tz, TimeZoneInfo.CreateCustomTimeZone(name, offset, name, std), std, null, false);
			}
		// glibc: UTC, named by the leading letters. A name with a `/` that ICU should know but cannot (no ICU here):
		// say so once, rather than silently giving UTC for a real zone
		if (name.Contains('/') && !TimeZoneInfo.TryConvertIanaIdToWindowsId("America/New_York", out _) && Interlocked.Exchange(ref s_warned, 1) == 0)
			Console.Error.WriteLine($"C#Bash: TZ={tz}: zone names cannot be resolved here (no ICU); using UTC");
		var letters = new string(name.TakeWhile(char.IsAsciiLetter).ToArray());
		return new(tz, TimeZoneInfo.Utc, letters.Length > 0 ? letters : "UTC", null, false);
		}

	private static TimeZoneInfo? Find(string id)
		{
		try { return TimeZoneInfo.FindSystemTimeZoneById(id); }
		catch (Exception) { return null; }
		}

	/// <summary>True when TZ is unset: times are the machine's own, as before.</summary>
	public static bool IsMachineZone => ReferenceEquals(Current, LocalZone);

	/// <summary>Now, as a wall-clock time in the shell's zone.</summary>
	public static DateTime Now => ToShell(DateTime.UtcNow);

	/// <summary>A time (UTC, or Local/Unspecified meaning the machine's local time, as .NET's file times are) as a
	/// wall-clock time in the shell's zone.</summary>
	public static DateTime ToShell(DateTime t)
		{
		var r = Current;
		if (ReferenceEquals(r, LocalZone)) return t.Kind == DateTimeKind.Utc ? t.ToLocalTime() : t;
		var utc = t.Kind == DateTimeKind.Utc ? t : t.ToUniversalTime();
		return DateTime.SpecifyKind(TimeZoneInfo.ConvertTimeFromUtc(utc, r.Zone), DateTimeKind.Unspecified);
		}

	/// <summary>A wall-clock time in the shell's zone (an Unspecified one, as GnuDate returns) as UTC; a Utc or Local
	/// one is converted as its kind says.</summary>
	public static DateTime ToUtc(DateTime wall)
		{
		if (wall.Kind == DateTimeKind.Utc) return wall;
		if (wall.Kind == DateTimeKind.Local) return wall.ToUniversalTime();
		var r = Current;
		return ReferenceEquals(r, LocalZone) ? DateTime.SpecifyKind(wall, DateTimeKind.Local).ToUniversalTime()
		                                     : TimeZoneInfo.ConvertTimeToUtc(wall, r.Zone);
		}

	/// <summary>The shell zone's offset at this wall-clock time.</summary>
	public static TimeSpan Offset(DateTime wall)
		{
		var r = Current;
		return r.Zone.GetUtcOffset(ReferenceEquals(r, LocalZone) ? wall : DateTime.SpecifyKind(wall, DateTimeKind.Unspecified));
		}

	/// <summary>%Z: the name TZ gave (UTC, GMT, a POSIX zone's standard or DST name, an unknown name's letters), else the
	/// numeric form glibc prints for a zone with no letters (`+09`, `+0530`): .NET has no zone abbreviations, so an IANA
	/// zone (glibc: JST) is numeric here. Null: the machine's zone (the caller's own form).</summary>
	public static string? Abbrev(DateTime wall)
		{
		var r = Current;
		if (ReferenceEquals(r, LocalZone)) return null;
		bool dst = r.Zone.IsDaylightSavingTime(DateTime.SpecifyKind(wall, DateTimeKind.Unspecified));
		if (dst && r.Dst is not null) return r.Dst;
		if (!dst && r.Std is not null) return r.Std;
		var off = Offset(wall);
		string sign = off < TimeSpan.Zero ? "-" : "+";
		off = off.Duration();
		return off.Minutes == 0 ? $"{sign}{off.Hours:D2}" : $"{sign}{off.Hours:D2}{off.Minutes:D2}";
		}

	/// <summary>`2026-10-30 11:38:19.444793300 -0700`: ls --full-time, stat, diff -u's headers.</summary>
	public static string FullIso(DateTime anyTime)
		{
		var w = ToShell(anyTime);
		var off = Offset(w);
		string sign = off < TimeSpan.Zero ? "-" : "+";
		off = off.Duration();
		return w.ToString("yyyy-MM-dd HH:mm:ss.fffffff00 ", System.Globalization.CultureInfo.InvariantCulture) + $"{sign}{off.Hours:D2}{off.Minutes:D2}";
		}
	}
