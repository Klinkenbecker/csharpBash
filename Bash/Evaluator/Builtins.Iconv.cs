namespace Bash.Evaluator;

/// <summary>
/// `iconv` in-process (2026-11-05; installer-a4's need of 2026-11-01, a UTF-16LE Windows INF; the open list's item 3c):
/// the encodings Windows hands out -- UTF-8, UTF-16, UTF-32 (LE, BE, and the plain names with GNU's BOM handling),
/// ISO-8859-1, Windows-1252, ASCII -- as glibc's iconv converts them, its messages and positions included (measured in
/// WSL). The decoders are written out, not .NET's: glibc names the BYTE where an input sequence went wrong. Any other
/// encoding, `//TRANSLIT` or `//IGNORE` goes to the PATH iconv (none here: then it says so).
/// </summary>
public sealed partial class Builtins
	{
	private enum IconvKind { Utf8, Utf16, Utf16Le, Utf16Be, Utf32, Utf32Le, Utf32Be, Latin1, Cp1252, Ascii }

	/// <summary>The names converted in-process (upper case; glibc's aliases for them), for -l and the lookup.</summary>
	private static readonly (string Name, IconvKind Kind)[] IconvNames =
		[
		("UTF-8", IconvKind.Utf8), ("UTF8", IconvKind.Utf8),
		("UTF-16", IconvKind.Utf16), ("UTF16", IconvKind.Utf16), ("UTF-16LE", IconvKind.Utf16Le), ("UTF16LE", IconvKind.Utf16Le),
		("UTF-16BE", IconvKind.Utf16Be), ("UTF16BE", IconvKind.Utf16Be),
		("UTF-32", IconvKind.Utf32), ("UTF32", IconvKind.Utf32), ("UTF-32LE", IconvKind.Utf32Le), ("UTF32LE", IconvKind.Utf32Le),
		("UTF-32BE", IconvKind.Utf32Be), ("UTF32BE", IconvKind.Utf32Be),
		("ISO-8859-1", IconvKind.Latin1), ("ISO8859-1", IconvKind.Latin1), ("ISO_8859-1", IconvKind.Latin1), ("LATIN1", IconvKind.Latin1),
		("L1", IconvKind.Latin1), ("CP819", IconvKind.Latin1), ("IBM819", IconvKind.Latin1),
		("CP1252", IconvKind.Cp1252), ("WINDOWS-1252", IconvKind.Cp1252), ("MS-ANSI", IconvKind.Cp1252),
		("ASCII", IconvKind.Ascii), ("US-ASCII", IconvKind.Ascii), ("ANSI_X3.4-1968", IconvKind.Ascii), ("646", IconvKind.Ascii),
		];

	/// <summary>Windows-1252's 0x80-0x9F (0 = undefined there, which glibc refuses as illegal input).</summary>
	private static readonly ushort[] Cp1252High =
		[
		0x20AC, 0, 0x201A, 0x0192, 0x201E, 0x2026, 0x2020, 0x2021, 0x02C6, 0x2030, 0x0160, 0x2039, 0x0152, 0, 0x017D, 0,
		0, 0x2018, 0x2019, 0x201C, 0x201D, 0x2022, 0x2013, 0x2014, 0x02DC, 0x2122, 0x0161, 0x203A, 0x0153, 0, 0x017E, 0x0178,
		];

	private static IconvKind? IconvLookup(string name)
		{
		var n = name.ToUpperInvariant();
		if (n.EndsWith("//", StringComparison.Ordinal)) n = n[..^2];   // "UTF-8//" is UTF-8; any other suffix is not ours
		foreach (var (nm, kind) in IconvNames) if (nm == n) return kind;
		return null;
		}

	private sealed class IconvError(string message) : Exception(message);

	/// <summary>`iconv [-c] [-f FROM] [-t TO] [-o FILE] [FILE...]`, `iconv -l`, as glibc's for the encodings above.</summary>
	private int Iconv(List<string> args)
		{
		var o = Opts.Parse("iconv", args, "f:t:co:ls", ["from-code=:f", "to-code=:t", "output=:o", "list:l", "silent:s"]);
		if (o.Has('l')) { foreach (var (nm, _) in IconvNames) Console.Out.Write(nm + "//\n"); return 0; }
		string from = o.Get('f') ?? "UTF-8", to = o.Get('t') ?? "UTF-8";   // the default is the locale's: UTF-8 here
		var src = IconvLookup(from) ?? throw new UnsupportedOptionException("iconv", $"-f {from}", "this encoding is not converted in-process");
		var dst = IconvLookup(to) ?? throw new UnsupportedOptionException("iconv", $"-t {to}", "this encoding is not converted in-process");
		bool omit = o.Has('c');
		List<string> files = o.Operands.Count == 0 ? ["-"] : o.Operands;

		Stream? outFile = null;
		ByteSink? sink = null;
		void Emit(byte[] b, int n) { if (n == 0) return; if (outFile is not null) outFile.Write(b, 0, n); else sink!.Write(b, 0, n); }
		try
			{
			if (o.Get('o') is string op) outFile = ShellFile.Create(ShellEnvironment.TranslatePath(op));
			else sink = new ByteSink();
			foreach (var f in files)
				{
				Stream input;
				try { input = OpenBytes(f); }
				catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
					{
					Console.Error.WriteLine($"iconv: cannot open input file `{f}': {IoError(ex)}");
					return 1;
					}
				try { IconvOne(input, src, dst, omit, Emit); }
				catch (IconvError e) { sink?.Flush(); outFile?.Flush(); Console.Error.WriteLine($"iconv: {e.Message}"); return 1; }
				finally { if (f != "-") input.Dispose(); }
				}
			return 0;
			}
		finally { sink?.Flush(); outFile?.Dispose(); }
		}

	/// <summary>One input converted: each file is its own conversion (its own BOM, positions from its start), as glibc.</summary>
	private static void IconvOne(Stream input, IconvKind src, IconvKind dst, bool omit, Action<byte[], int> emit)
		{
		var buf = new byte[IoBlock + 4];
		var outBuf = new List<byte>(IoBlock * 2);
		int have = 0;              // bytes in buf not yet decoded (a sequence cut by the block's end, carried)
		long baseOffset = 0;       // the input offset of buf[0]
		bool first = true, eof = false;
		IconvKind inKind = src;    // the plain UTF-16/32 names settle their byte order at the BOM
		bool bomOwed = dst is IconvKind.Utf16 or IconvKind.Utf32;   // written with the first character: empty input gets none (glibc)

		void Flush() { if (outBuf.Count == 0) return; var a = outBuf.ToArray(); outBuf.Clear(); emit(a, a.Length); }

		while (!eof)
			{
			int n = input.Read(buf, have, IoBlock);
			if (n <= 0) eof = true;
			int end = have + Math.Max(n, 0);
			int i = 0;
			if (first && (src is IconvKind.Utf16 or IconvKind.Utf32) && (end >= 4 || eof))
				{
				// the plain names: a BOM says the order and is not text; none means little-endian (glibc on this machine)
				first = false;
				if (src is IconvKind.Utf16)
					{
					if (end >= 2 && buf[0] == 0xFF && buf[1] == 0xFE) { inKind = IconvKind.Utf16Le; i = 2; }
					else if (end >= 2 && buf[0] == 0xFE && buf[1] == 0xFF) { inKind = IconvKind.Utf16Be; i = 2; }
					else inKind = IconvKind.Utf16Le;   // no BOM: the machine's order, little-endian (glibc, measured; not the spec's BE)
					}
				else
					{
					if (end >= 4 && buf[0] == 0xFF && buf[1] == 0xFE && buf[2] == 0 && buf[3] == 0) { inKind = IconvKind.Utf32Le; i = 4; }
					else if (end >= 4 && buf[0] == 0 && buf[1] == 0 && buf[2] == 0xFE && buf[3] == 0xFF) { inKind = IconvKind.Utf32Be; i = 4; }
					else inKind = IconvKind.Utf32Le;
					}
				}
			else if (first && src is not (IconvKind.Utf16 or IconvKind.Utf32)) first = false;
			if (first) { have = end; continue; }   // too little yet to see a BOM

			while (i < end)
				{
				int at = i;
				int cp, len;
				switch (inKind)
					{
					case IconvKind.Latin1: cp = buf[i]; len = 1; break;
					case IconvKind.Ascii: cp = buf[i] < 0x80 ? buf[i] : -1; len = 1; break;
					case IconvKind.Cp1252:
						{
						int b = buf[i];
						cp = b is >= 0x80 and <= 0x9F ? (Cp1252High[b - 0x80] is var m and > 0 ? m : -1) : b;
						len = 1;
						break;
						}
					case IconvKind.Utf8:
						{
						int b0 = buf[i];
						int need = b0 < 0x80 ? 1 : b0 is >= 0xC2 and <= 0xDF ? 2 : b0 is >= 0xE0 and <= 0xEF ? 3 : b0 is >= 0xF0 and <= 0xF4 ? 4 : 0;
						if (need == 0) { cp = -1; len = 1; break; }
						if (end - i < need)
							{
							// a sequence cut by the block: carry it, unless what is there is already wrong or the input ended
							bool ok = true;
							for (int k = 1; k < end - i; k++) if (!Utf8Cont(b0, k, buf[i + k])) { ok = false; break; }
							if (ok && !eof) goto carry;
							if (ok) throw new IconvError("incomplete character or shift sequence at end of buffer");
							cp = -1; len = 1; break;
							}
						cp = need == 1 ? b0 : need == 2 ? b0 & 0x1F : need == 3 ? b0 & 0x0F : b0 & 0x07;
						len = need;
						for (int k = 1; k < need; k++)
							{
							if (!Utf8Cont(b0, k, buf[i + k])) { cp = -1; len = 1; break; }
							cp = (cp << 6) | (buf[i + k] & 0x3F);
							}
						break;
						}
					case IconvKind.Utf16Le or IconvKind.Utf16Be:
						{
						bool le = inKind == IconvKind.Utf16Le;
						if (end - i < 2) { if (!eof) goto carry; throw new IconvError("incomplete character or shift sequence at end of buffer"); }
						int u = le ? buf[i] | buf[i + 1] << 8 : buf[i] << 8 | buf[i + 1];
						if (u is >= 0xD800 and <= 0xDBFF)
							{
							if (end - i < 4) { if (!eof) goto carry; throw new IconvError("incomplete character or shift sequence at end of buffer"); }
							int u2 = le ? buf[i + 2] | buf[i + 3] << 8 : buf[i + 2] << 8 | buf[i + 3];
							if (u2 is >= 0xDC00 and <= 0xDFFF) { cp = 0x10000 + ((u - 0xD800) << 10) + (u2 - 0xDC00); len = 4; }
							else { cp = -1; len = 2; }
							}
						else if (u is >= 0xDC00 and <= 0xDFFF) { cp = -1; len = 2; }
						else { cp = u; len = 2; }
						break;
						}
					default:   // UTF-32 LE/BE
						{
						bool le = inKind == IconvKind.Utf32Le;
						if (end - i < 4) { if (!eof) goto carry; throw new IconvError("incomplete character or shift sequence at end of buffer"); }
						long v = le ? buf[i] | buf[i + 1] << 8 | buf[i + 2] << 16 | (long)buf[i + 3] << 24
						            : (long)buf[i] << 24 | buf[i + 1] << 16 | buf[i + 2] << 8 | buf[i + 3];
						cp = v > 0x10FFFF || v is >= 0xD800 and <= 0xDFFF ? -1 : (int)v;
						len = 4;
						break;
						}
					}
				i += len;
				if (cp < 0)
					{
					if (omit) continue;   // -c: an invalid input sequence is left out too (glibc)
					Flush();
					throw new IconvError($"illegal input sequence at position {baseOffset + at}");
					}
				if (bomOwed)
					{
					bomOwed = false;
					outBuf.Add(0xFF); outBuf.Add(0xFE);
					if (dst is IconvKind.Utf32) { outBuf.Add(0); outBuf.Add(0); }
					}
				if (!IconvEncode(dst, cp, outBuf))
					{
					if (omit) continue;
					Flush();
					throw new IconvError($"illegal input sequence at position {baseOffset + at}");
					}
				}
			// the whole block decoded
			baseOffset += end;
			have = 0;
			Flush();
			continue;
		carry:
			// keep the cut sequence at the front for the next read
			int keep = end - i;
			Array.Copy(buf, i, buf, 0, keep);
			baseOffset += i;
			have = keep;
			Flush();
			}
		Flush();
		}

	/// <summary>UTF-8's continuation byte <paramref name="k"/> of a sequence led by <paramref name="lead"/> (the second
	/// byte's narrower ranges keep out overlong forms, surrogates and values past U+10FFFF).</summary>
	private static bool Utf8Cont(int lead, int k, byte b) => k != 1 ? b is >= 0x80 and <= 0xBF : lead switch
		{
		0xE0 => b is >= 0xA0 and <= 0xBF,
		0xED => b is >= 0x80 and <= 0x9F,
		0xF0 => b is >= 0x90 and <= 0xBF,
		0xF4 => b is >= 0x80 and <= 0x8F,
		_ => b is >= 0x80 and <= 0xBF,
		};

	/// <summary>A code point into <paramref name="kind"/>; false when that encoding has no such character.</summary>
	private static bool IconvEncode(IconvKind kind, int cp, List<byte> o)
		{
		switch (kind)
			{
			case IconvKind.Utf8:
				if (cp < 0x80) o.Add((byte)cp);
				else if (cp < 0x800) { o.Add((byte)(0xC0 | cp >> 6)); o.Add((byte)(0x80 | cp & 0x3F)); }
				else if (cp < 0x10000) { o.Add((byte)(0xE0 | cp >> 12)); o.Add((byte)(0x80 | cp >> 6 & 0x3F)); o.Add((byte)(0x80 | cp & 0x3F)); }
				else { o.Add((byte)(0xF0 | cp >> 18)); o.Add((byte)(0x80 | cp >> 12 & 0x3F)); o.Add((byte)(0x80 | cp >> 6 & 0x3F)); o.Add((byte)(0x80 | cp & 0x3F)); }
				return true;
			case IconvKind.Utf16 or IconvKind.Utf16Le or IconvKind.Utf16Be:
				{
				bool be = kind == IconvKind.Utf16Be;
				void Unit(int u) { if (be) { o.Add((byte)(u >> 8)); o.Add((byte)u); } else { o.Add((byte)u); o.Add((byte)(u >> 8)); } }
				if (cp < 0x10000) Unit(cp);
				else { int v = cp - 0x10000; Unit(0xD800 + (v >> 10)); Unit(0xDC00 + (v & 0x3FF)); }
				return true;
				}
			case IconvKind.Utf32 or IconvKind.Utf32Le:
				o.Add((byte)cp); o.Add((byte)(cp >> 8)); o.Add((byte)(cp >> 16)); o.Add((byte)(cp >> 24));
				return true;
			case IconvKind.Utf32Be:
				o.Add((byte)(cp >> 24)); o.Add((byte)(cp >> 16)); o.Add((byte)(cp >> 8)); o.Add((byte)cp);
				return true;
			case IconvKind.Latin1:
				if (cp > 0xFF) return false;
				o.Add((byte)cp);
				return true;
			case IconvKind.Ascii:
				if (cp > 0x7F) return false;
				o.Add((byte)cp);
				return true;
			default:   // Windows-1252
				if (cp < 0x80 || cp is >= 0xA0 and <= 0xFF) { o.Add((byte)cp); return true; }
				int at = Array.IndexOf(Cp1252High, (ushort)cp);
				if (cp > 0xFFFF || at < 0 || cp == 0) return false;
				o.Add((byte)(0x80 + at));
				return true;
			}
		}
	}
