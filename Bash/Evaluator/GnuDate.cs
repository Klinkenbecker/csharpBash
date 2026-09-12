using System.Globalization;
using System.Text.RegularExpressions;

namespace Bash.Evaluator;

/// <summary>
/// A pragmatic GNU `date -d` / `touch -d` parser: calendar dates (ISO, YYYYMMDD, MM/DD[/YYYY],
/// "Mar 5 2024", "5 Mar 2024", RFC-2822), times (HH:MM[:SS], am/pm, `T` joined, `Z`/±HHMM
/// zones, UTC/GMT), `@epoch`, "now/today/yesterday/tomorrow", relative items ("3 days ago",
/// "+1 week", "next month", "last year", "-1 hour"), and weekday items ("monday",
/// "next friday", "last tuesday"). A date or weekday without a time means 00:00:00; relative
/// items alone keep the current time of day, as GNU does. Anything unrecognised fails
/// (returns false) rather than guessing.
/// </summary>
public static class GnuDate
	{
	private static readonly string[] Months = ["jan", "feb", "mar", "apr", "may", "jun", "jul", "aug", "sep", "oct", "nov", "dec"];
	private static readonly string[] Days = ["sun", "mon", "tue", "wed", "thu", "fri", "sat"];
	private static readonly Dictionary<string, string> Units = new()
		{
		["sec"] = "s", ["second"] = "s", ["min"] = "m", ["minute"] = "m", ["hour"] = "h", ["day"] = "d",
		["week"] = "w", ["fortnight"] = "f", ["month"] = "M", ["year"] = "y",
		};

	public static bool TryParse(string input, bool utc, out DateTime result)
		{
		var now = utc ? DateTime.UtcNow : DateTime.Now;
		result = now;
		var text = input.Trim().ToLowerInvariant();
		if (text.Length == 0) return false;
		if (text is "now") return true;

		int? year = null, month = null, day = null;
		int? hour = null, minute = null, second = null;
		TimeSpan? zone = null;
		int? weekday = null; int ordinal = 0;
		long? epoch = null;
		var rel = new List<(char unit, int n)>();

		var toks = Regex.Split(text, @"[\s,]+").Where(t => t.Length > 0).ToList();
		for (int i = 0; i < toks.Count; i++)
			{
			var t = toks[i];
			Match m;
			if (t[0] == '@' && long.TryParse(t[1..], out var ep)) { epoch = ep; continue; }
			if (t is "now" or "today" or "this") continue;
			if (t == "yesterday") { rel.Add(('d', -1)); continue; }
			if (t == "tomorrow") { rel.Add(('d', 1)); continue; }
			if (t is "utc" or "gmt" or "z" or "ut") { zone = TimeSpan.Zero; continue; }
			if (t == "ago") { if (rel.Count == 0) return false; rel[^1] = (rel[^1].unit, -rel[^1].n); continue; }
			// ISO date, optionally joined to a time with 't'
			if ((m = Regex.Match(t, @"^(\d{4})-(\d{1,2})-(\d{1,2})(?:t(\d{1,2}):(\d{2})(?::(\d{2})(?:\.\d+)?)?(z|[+-]\d{2}:?\d{2})?)?$")).Success)
				{
				year = int.Parse(m.Groups[1].Value); month = int.Parse(m.Groups[2].Value); day = int.Parse(m.Groups[3].Value);
				if (m.Groups[4].Success)
					{
					hour = int.Parse(m.Groups[4].Value); minute = int.Parse(m.Groups[5].Value); second = m.Groups[6].Success ? int.Parse(m.Groups[6].Value) : 0;
					if (m.Groups[7].Success) zone = ParseZone(m.Groups[7].Value);
					}
				continue;
				}
			if ((m = Regex.Match(t, @"^(\d{4})(\d{2})(\d{2})$")).Success)
				{ year = int.Parse(m.Groups[1].Value); month = int.Parse(m.Groups[2].Value); day = int.Parse(m.Groups[3].Value); continue; }
			if ((m = Regex.Match(t, @"^(\d{1,2})/(\d{1,2})(?:/(\d{2,4}))?$")).Success)
				{
				month = int.Parse(m.Groups[1].Value); day = int.Parse(m.Groups[2].Value);
				if (m.Groups[3].Success) { year = int.Parse(m.Groups[3].Value); if (year < 100) year += year < 69 ? 2000 : 1900; }
				continue;
				}
			// time: HH:MM[:SS[.frac]][am|pm][z|+hhmm]
			if ((m = Regex.Match(t, @"^(\d{1,2}):(\d{2})(?::(\d{2})(?:\.\d+)?)?(am|pm)?(z|[+-]\d{2}:?\d{2})?$")).Success)
				{
				hour = int.Parse(m.Groups[1].Value); minute = int.Parse(m.Groups[2].Value); second = m.Groups[3].Success ? int.Parse(m.Groups[3].Value) : 0;
				if (m.Groups[4].Success) hour = Meridian(hour.Value, m.Groups[4].Value);
				else if (i + 1 < toks.Count && toks[i + 1] is "am" or "pm" or "a.m." or "p.m.") { hour = Meridian(hour.Value, toks[++i][..2].Replace(".", "")); }
				if (m.Groups[5].Success) zone = ParseZone(m.Groups[5].Value);
				continue;
				}
			if ((m = Regex.Match(t, @"^(\d{1,2})(am|pm)$")).Success)
				{ hour = Meridian(int.Parse(m.Groups[1].Value), m.Groups[2].Value); minute = 0; second = 0; continue; }
			// zone offset token
			if ((m = Regex.Match(t, @"^[+-]\d{2}:?\d{2}$")).Success && (hour is not null || year is not null))
				{ zone = ParseZone(t); continue; }
			// relative: [+-]N unit | next/last/this unit | unit alone
			if ((m = Regex.Match(t, @"^([+-]?\d+)$")).Success && i + 1 < toks.Count && UnitOf(toks[i + 1]) is char u1)
				{ rel.Add((u1, int.Parse(m.Groups[1].Value))); i++; continue; }
			if ((m = Regex.Match(t, @"^([+-]?\d+)([a-z]+)$")).Success && UnitOf(m.Groups[2].Value) is char u2)
				{ rel.Add((u2, int.Parse(m.Groups[1].Value))); continue; }
			if (t is "next" or "last" or "this" or "first" or "third" or "fourth" or "fifth")
				{
				int n = t switch { "next" or "first" => 1, "last" => -1, "this" => 0, "third" => 3, "fourth" => 4, _ => 5 };
				if (i + 1 >= toks.Count) return false;
				var nx = toks[i + 1];
				if (UnitOf(nx) is char u3) { rel.Add((u3, n)); i++; continue; }
				if (DayOf(nx) is int wd) { weekday = wd; ordinal = n; i++; continue; }
				return false;
				}
			if (UnitOf(t) is char u4) { rel.Add((u4, 1)); continue; }
			if (DayOf(t) is int wd2)
				{
				// a weekday preceding an explicit date (RFC style "tue 05 mar 2024") is decorative
				bool dateFollows = toks.Skip(i + 1).Any(x => Regex.IsMatch(x, @"^\d{4}-\d") || MonthOf(x) is not null || Regex.IsMatch(x, @"^\d{1,2}/\d"));
				if (!dateFollows) { weekday = wd2; ordinal = 0; }
				continue;
				}
			// month-name forms: "mar 5 [2024]" / "5 mar [2024]"
			if (MonthOf(t) is int mo)
				{
				month = mo;
				if (i + 1 < toks.Count && Regex.IsMatch(toks[i + 1], @"^\d{1,2}(st|nd|rd|th)?$")) { day = int.Parse(Regex.Match(toks[++i], @"\d+").Value); }
				if (i + 1 < toks.Count && Regex.IsMatch(toks[i + 1], @"^\d{4}$")) { year = int.Parse(toks[++i]); }
				continue;
				}
			if (Regex.IsMatch(t, @"^\d{1,2}$") && i + 1 < toks.Count && MonthOf(toks[i + 1]) is int mo2)
				{
				day = int.Parse(t); month = mo2; i++;
				if (i + 1 < toks.Count && Regex.IsMatch(toks[i + 1], @"^\d{4}$")) { year = int.Parse(toks[++i]); }
				continue;
				}
			if (Regex.IsMatch(t, @"^\d{4}$") && month is not null && year is null) { year = int.Parse(t); continue; }
			return false;
			}

		DateTime dt;
		if (epoch is long e)
			{
			var dto = DateTimeOffset.FromUnixTimeSeconds(e);
			dt = utc ? dto.UtcDateTime : dto.LocalDateTime;
			}
		else
			{
			bool dateGiven = year is not null || month is not null || day is not null || weekday is not null;
			int y = year ?? now.Year, mo = month ?? now.Month, d = day ?? now.Day;
			int h, mi, s;
			if (hour is not null) { h = hour.Value; mi = minute ?? 0; s = second ?? 0; }
			else if (dateGiven) { h = mi = s = 0; }
			else { h = now.Hour; mi = now.Minute; s = now.Second; }
			try { dt = new DateTime(y, mo, d, h, mi, s, utc ? DateTimeKind.Utc : DateTimeKind.Local); }
			catch (ArgumentOutOfRangeException) { return false; }
			if (zone is TimeSpan z)
				{
				var dto = new DateTimeOffset(DateTime.SpecifyKind(dt, DateTimeKind.Unspecified), z);
				dt = utc ? dto.UtcDateTime : dto.LocalDateTime;
				}
			if (weekday is int wd)
				{
				int delta = ((wd - (int)dt.DayOfWeek) + 7) % 7;
				if (ordinal >= 1) { if (delta == 0) delta = 7; delta += 7 * (ordinal - 1); }
				else if (ordinal < 0) delta = delta == 0 ? -7 : delta - 7;
				dt = dt.AddDays(delta);
				}
			}
		foreach (var (unit, n) in rel)
			{
			dt = unit switch
				{
				's' => dt.AddSeconds(n), 'm' => dt.AddMinutes(n), 'h' => dt.AddHours(n), 'd' => dt.AddDays(n),
				'w' => dt.AddDays(7 * n), 'f' => dt.AddDays(14 * n), 'M' => dt.AddMonths(n), 'y' => dt.AddYears(n), _ => dt,
				};
			}
		result = dt;
		return true;
		}

	private static int Meridian(int h, string ap) => ap == "pm" ? (h % 12) + 12 : h % 12;

	private static TimeSpan ParseZone(string z)
		{
		if (z == "z") return TimeSpan.Zero;
		int sign = z[0] == '-' ? -1 : 1;
		var digits = z[1..].Replace(":", "");
		int hh = int.Parse(digits[..2]), mm = int.Parse(digits[2..]);
		return TimeSpan.FromMinutes(sign * (hh * 60 + mm));
		}

	private static char? UnitOf(string t)
		{
		if (t.EndsWith('s') && t.Length > 3) t = t[..^1];
		return Units.TryGetValue(t, out var u) ? u[0] : null;
		}

	private static int? DayOf(string t)
		{
		if (t.Length < 3) return null;
		for (int i = 0; i < 7; i++)
			{
			var full = CultureInfo.InvariantCulture.DateTimeFormat.DayNames[i].ToLowerInvariant();
			if (t == Days[i] || t == full) return i;
			}
		return null;
		}

	private static int? MonthOf(string t)
		{
		if (t.Length < 3) return null;
		for (int i = 0; i < 12; i++)
			{
			var full = CultureInfo.InvariantCulture.DateTimeFormat.MonthNames[i].ToLowerInvariant();
			if (t == Months[i] || t == full || (t.Length == 4 && t == "sept" && i == 8)) return i + 1;
			}
		return null;
		}
	}
