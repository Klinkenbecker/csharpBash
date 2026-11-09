using System.Globalization;
using System.Text;

namespace Bash.Evaluator;

/// <summary>
/// The POSIX character classes as GNU reads them in a UTF-8 locale (glibc's C.UTF-8), C#Bash's own locale: it counts
/// characters, not bytes, everywhere else (`${#x}` of é is 1, `${x^^}` uppercases it), so its classes do too. Until
/// 2026-11-07 every class was ASCII: `[[:alpha:]]` did not match é in `[[ =~ ]]`, globs, grep, sed or awk.
///
/// Measured against GNU grep 3.7 / bash 5.1 under LANG=C.UTF-8 on every BMP character (2026-11-07): alpha is every
/// letter, letter number (Ⅻ) and non-ASCII decimal digit (Arabic ٣); digit and xdigit are ASCII only; upper and lower
/// both hold the titlecase letters (ǅ) and the case-forming numbers and circled letters; space and blank leave out the
/// no-break spaces (U+00A0, U+2007, U+202F); cntrl is the C0/C1 controls and the line and paragraph separators; print is
/// everything assigned but those; graph is print without the spaces; punct is graph without alnum (so ², NBSP, ZWSP,
/// combining marks and symbols are punct). The Unicode version is .NET's, not glibc's: a character assigned in one and
/// not the other differs (print, graph, punct, alpha).
///
/// A regex sees UTF-16: a character beyond U+FFFF (an emoji) is two units there, each in print, graph and punct only
/// (`^[[:punct:]]+$` matches 😀; `^[[:punct:]]$` does not, nor `^.$`). `tr` keeps its ASCII classes, as GNU tr (which
/// works on bytes) does.
/// </summary>
internal static class PosixClasses
	{
	public static bool Is(string name, int cp) => name switch
		{
		"alpha"  => Alpha(cp),
		"digit"  => cp is >= '0' and <= '9',
		"alnum"  => Alnum(cp),
		"upper"  => Upper(cp),
		"lower"  => Lower(cp),
		"space"  => Space(cp),
		"blank"  => Blank(cp),
		"punct"  => Graph(cp) && !Alnum(cp),
		"print"  => Print(cp),
		"graph"  => Graph(cp),
		"cntrl"  => Cntrl(cp),
		"xdigit" => cp is >= '0' and <= '9' or >= 'A' and <= 'F' or >= 'a' and <= 'f',
		_ => false,
		};

	public static bool Known(string name) => name is "alpha" or "digit" or "alnum" or "upper" or "lower" or "space" or "blank"
		or "punct" or "print" or "graph" or "cntrl" or "xdigit";

	private static UnicodeCategory Cat(int cp) => CharUnicodeInfo.GetUnicodeCategory(cp);

	private static bool Alpha(int cp) => Cat(cp) switch
		{
		UnicodeCategory.UppercaseLetter or UnicodeCategory.LowercaseLetter or UnicodeCategory.TitlecaseLetter
			or UnicodeCategory.ModifierLetter or UnicodeCategory.OtherLetter or UnicodeCategory.LetterNumber => true,
		UnicodeCategory.DecimalDigitNumber => cp is < '0' or > '9',
		_ => In(OtherAlphabetic, cp),
		};

	private static bool Alnum(int cp) => cp is >= '0' and <= '9' || Alpha(cp);

	private static bool Upper(int cp) => Cat(cp) is UnicodeCategory.UppercaseLetter or UnicodeCategory.TitlecaseLetter
		|| cp is >= 0x2160 and <= 0x216F or >= 0x24B6 and <= 0x24CF;

	// a titlecase letter is lower only when it has an uppercase form (ǅ → Ǆ; Greek ᾈ has none), as glibc's
	private static bool Lower(int cp) => Cat(cp) switch
		{
		UnicodeCategory.LowercaseLetter => true,
		UnicodeCategory.TitlecaseLetter => cp < 0x10000 && char.ToUpperInvariant((char)cp) != cp,
		_ => cp is >= 0x2170 and <= 0x217F or >= 0x24D0 and <= 0x24E9 || In(OtherLowercase, cp),
		};

	private static bool In(ushort[] ranges, int cp)
		{
		for (int i = 0; i < ranges.Length; i += 2)
			{
			if (cp < ranges[i]) return false;
			if (cp <= ranges[i + 1]) return true;
			}
		return false;
		}

	// Unicode's Other_Alphabetic (combining vowel signs and the like, circled letters) and Other_Lowercase (modifier
	// letters, ª º), which .NET's categories do not carry: in BMP, from start to end inclusive. Made from GNU's own answer
	// (grep under C.UTF-8 on every BMP character, less what the categories above give), 2026-11-07.
	private static readonly ushort[] OtherAlphabetic =
		[
		0x0345, 0x0345, 0x05B0, 0x05BD, 0x05BF, 0x05BF, 0x05C1, 0x05C2, 0x05C4, 0x05C5, 0x05C7, 0x05C7, 0x0610, 0x061A,
		0x064B, 0x0657, 0x0659, 0x065F, 0x0670, 0x0670, 0x06D6, 0x06DC, 0x06E1, 0x06E4, 0x06E7, 0x06E8, 0x06ED, 0x06ED,
		0x0711, 0x0711, 0x0730, 0x073F, 0x07A6, 0x07B0, 0x0816, 0x0817, 0x081B, 0x0823, 0x0825, 0x0827, 0x0829, 0x082C,
		0x08D4, 0x08DF, 0x08E3, 0x08E9, 0x08F0, 0x0903, 0x093A, 0x093B, 0x093E, 0x094C, 0x094E, 0x094F, 0x0955, 0x0957,
		0x0962, 0x0963, 0x0981, 0x0983, 0x09BE, 0x09C4, 0x09C7, 0x09C8, 0x09CB, 0x09CC, 0x09D7, 0x09D7, 0x09E2, 0x09E3,
		0x0A01, 0x0A03, 0x0A3E, 0x0A42, 0x0A47, 0x0A48, 0x0A4B, 0x0A4C, 0x0A51, 0x0A51, 0x0A70, 0x0A71, 0x0A75, 0x0A75,
		0x0A81, 0x0A83, 0x0ABE, 0x0AC5, 0x0AC7, 0x0AC9, 0x0ACB, 0x0ACC, 0x0AE2, 0x0AE3, 0x0AFA, 0x0AFC, 0x0B01, 0x0B03,
		0x0B3E, 0x0B44, 0x0B47, 0x0B48, 0x0B4B, 0x0B4C, 0x0B56, 0x0B57, 0x0B62, 0x0B63, 0x0B82, 0x0B82, 0x0BBE, 0x0BC2,
		0x0BC6, 0x0BC8, 0x0BCA, 0x0BCC, 0x0BD7, 0x0BD7, 0x0C00, 0x0C03, 0x0C3E, 0x0C44, 0x0C46, 0x0C48, 0x0C4A, 0x0C4C,
		0x0C55, 0x0C56, 0x0C62, 0x0C63, 0x0C81, 0x0C83, 0x0CBE, 0x0CC4, 0x0CC6, 0x0CC8, 0x0CCA, 0x0CCC, 0x0CD5, 0x0CD6,
		0x0CE2, 0x0CE3, 0x0D00, 0x0D03, 0x0D3E, 0x0D44, 0x0D46, 0x0D48, 0x0D4A, 0x0D4C, 0x0D57, 0x0D57, 0x0D62, 0x0D63,
		0x0D81, 0x0D83, 0x0DCF, 0x0DD4, 0x0DD6, 0x0DD6, 0x0DD8, 0x0DDF, 0x0DF2, 0x0DF3, 0x0E31, 0x0E31, 0x0E34, 0x0E3A,
		0x0E4D, 0x0E4D, 0x0EB1, 0x0EB1, 0x0EB4, 0x0EB9, 0x0EBB, 0x0EBC, 0x0ECD, 0x0ECD, 0x0F71, 0x0F81, 0x0F8D, 0x0F97,
		0x0F99, 0x0FBC, 0x102B, 0x1036, 0x1038, 0x1038, 0x103B, 0x103E, 0x1056, 0x1059, 0x105E, 0x1060, 0x1062, 0x1064,
		0x1067, 0x106D, 0x1071, 0x1074, 0x1082, 0x108D, 0x108F, 0x108F, 0x109A, 0x109D, 0x1712, 0x1713, 0x1732, 0x1733,
		0x1752, 0x1753, 0x1772, 0x1773, 0x17B6, 0x17C8, 0x1885, 0x1886, 0x18A9, 0x18A9, 0x1920, 0x192B, 0x1930, 0x1938,
		0x1A17, 0x1A1B, 0x1A55, 0x1A5E, 0x1A61, 0x1A74, 0x1ABF, 0x1AC0, 0x1ACC, 0x1ACE, 0x1B00, 0x1B04, 0x1B35, 0x1B43,
		0x1B80, 0x1B82, 0x1BA1, 0x1BA9, 0x1BAC, 0x1BAD, 0x1BE7, 0x1BF1, 0x1C24, 0x1C36, 0x1DE7, 0x1DF4, 0x24B6, 0x24E9,
		0x2DE0, 0x2DFF, 0xA674, 0xA67B, 0xA69E, 0xA69F, 0xA802, 0xA802, 0xA80B, 0xA80B, 0xA823, 0xA827, 0xA880, 0xA881,
		0xA8B4, 0xA8C3, 0xA8C5, 0xA8C5, 0xA8FF, 0xA8FF, 0xA926, 0xA92A, 0xA947, 0xA952, 0xA980, 0xA983, 0xA9B4, 0xA9BF,
		0xA9E5, 0xA9E5, 0xAA29, 0xAA36, 0xAA43, 0xAA43, 0xAA4C, 0xAA4D, 0xAA7B, 0xAA7D, 0xAAB0, 0xAAB0, 0xAAB2, 0xAAB4,
		0xAAB7, 0xAAB8, 0xAABE, 0xAABE, 0xAAEB, 0xAAEF, 0xAAF5, 0xAAF5, 0xABE3, 0xABEA, 0xFB1E, 0xFB1E,
		];

	private static readonly ushort[] OtherLowercase =
		[
		0x00AA, 0x00AA, 0x00BA, 0x00BA, 0x02B0, 0x02B8, 0x02C0, 0x02C1, 0x02E0, 0x02E4, 0x0345, 0x0345, 0x037A, 0x037A,
		0x1D2C, 0x1D6A, 0x1D78, 0x1D78, 0x1D9B, 0x1DBF, 0x2071, 0x2071, 0x207F, 0x207F, 0x2090, 0x209C, 0x2C7C, 0x2C7D,
		0xA69C, 0xA69D, 0xA770, 0xA770, 0xA7F8, 0xA7F9, 0xAB5C, 0xAB5F,
		];

	private static bool NoBreak(int cp) => cp is 0x00A0 or 0x2007 or 0x202F;

	private static bool Blank(int cp) => cp is '\t' or ' ' || Cat(cp) == UnicodeCategory.SpaceSeparator && !NoBreak(cp);

	private static bool Space(int cp) => cp is >= '\t' and <= '\r' or 0x2028 or 0x2029 || Blank(cp);

	private static bool Cntrl(int cp) => Cat(cp) == UnicodeCategory.Control || cp is 0x2028 or 0x2029;

	private static bool Print(int cp) => Cat(cp) is not (UnicodeCategory.Control or UnicodeCategory.OtherNotAssigned
		or UnicodeCategory.Surrogate) && cp is not (0x2028 or 0x2029);

	private static bool Graph(int cp) => Print(cp) && !Space(cp);

	private static readonly Dictionary<string, string> s_sets = [];

	/// <summary>The class as the inside of a .NET character class: its BMP members as `\uXXXX` ranges, made once from
	/// <see cref="Is"/>; null for a name that is not a class.</summary>
	public static string? NetSet(string name)
		{
		if (!Known(name)) return null;
		lock (s_sets)
			{
			if (s_sets.TryGetValue(name, out var set)) return set;
			var sb = new StringBuilder();
			int start = -1;
			for (int cp = 0; cp <= 0x10000; cp++)
				{
				// a surrogate (half of a character beyond U+FFFF, which a regex sees as two units) is print, graph and
				// punct, as nearly every such character (an emoji) is in GNU, and nothing else
				bool member = cp < 0x10000
				              && (cp is >= 0xD800 and <= 0xDFFF ? name is "print" or "graph" or "punct" : Is(name, cp));
				if (member) { if (start < 0) start = cp; continue; }
				if (start < 0) continue;
				sb.Append($"\\u{start:X4}");
				if (cp - 1 > start) sb.Append($"-\\u{cp - 1:X4}");
				start = -1;
				}
			return s_sets[name] = sb.ToString();
			}
		}
	}
