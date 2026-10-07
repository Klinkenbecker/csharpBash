using System.Text;

namespace Bash.Evaluator;

/// <summary>
/// UTF-8 that is BYTE-TRANSPARENT: the shell's single encoding for files, pipes, captures and
/// escapes (ratified 2026-09-12).
///
/// A bash string is a byte sequence; a .NET string is UTF-16. Encoding plain UTF-8 between them
/// is lossy in both directions — a byte that is not valid UTF-8 decodes to U+FFFD and can never
/// be written back, and an escape like <c>printf '\377'</c> means "the byte 0xFF" but became the
/// CHARACTER U+00FF and encoded back out as two bytes. Measured 2026-09-11/12: five raw bytes
/// through <c>x=$(cat b.bin)</c> came back as two characters and six different bytes.
///
/// The fix is Python's "surrogateescape": a byte that cannot be decoded becomes the lone
/// surrogate U+DC00+byte (0xDC80–0xDCFF), and encoding maps that range back to the single
/// original byte. Text is ordinary UTF-8; anything that was not text survives a round trip
/// anyway, so the shell needs no per-call-site decision about whether its data is text.
/// Escape handlers (<c>printf '\xNN'</c>, <c>echo -e</c>, <c>$'…'</c>) produce this range
/// directly via <see cref="ByteChar"/> for values above 0x7F.
///
/// The one place the transparency is deliberately NOT wanted is an interactive console, which
/// cannot render a lone surrogate: <see cref="ForConsole"/> is used there and substitutes
/// U+FFFD, matching what a terminal would show anyway.
/// </summary>
public sealed class ShellEncoding : Encoding
	{
	/// <summary>The shell's encoding for every file, pipe and capture.</summary>
	public static readonly ShellEncoding Utf8 = new();

	/// <summary>Lowest lone surrogate used to carry an undecodable byte.</summary>
	public const char ByteBase = '\uDC00';

	/// <summary>The character that carries raw byte <paramref name="b"/> through a shell string:
	/// ASCII stays itself, anything above 0x7F becomes its escape surrogate.</summary>
	public static char ByteChar(int b) => b <= 0x7F ? (char)b : (char)(ByteBase + (b & 0xFF));

	/// <summary>True if <paramref name="c"/> carries a raw byte rather than a character.
	/// CAUTION: this range overlaps the LOW half of a legitimate surrogate pair — an astral
	/// character (U+10000 and above) ends in U+DC00–DFFF, and a quarter of those land here. Only a
	/// LONE low surrogate is a carried byte, so every encode path must also check that the
	/// preceding character is not a high surrogate (<see cref="IsLoneByte"/>). Missing that
	/// corrupted valid 4-byte UTF-8; caught by the 64 KB random round-trip check, 2026-09-12.</summary>
	public static bool IsByteChar(char c) => c is >= '\uDC80' and <= '\uDCFF';

	/// <summary>True if the character at <paramref name="i"/> is a carried byte rather than the
	/// low half of a surrogate pair.</summary>
	private static bool IsLoneByte(ReadOnlySpan<char> s, int i) =>
		IsByteChar(s[i]) && (i == 0 || !char.IsHighSurrogate(s[i - 1]));

	/// <summary>Render a string for a device that cannot show lone surrogates (a console).</summary>
	public static string ForConsole(string s)
		{
		if (s.AsSpan().IndexOfAnyInRange('\uDC80', '\uDCFF') < 0) return s;
		var sb = new StringBuilder(s.Length);
		foreach (var c in s) sb.Append(IsByteChar(c) ? '�' : c);
		return sb.ToString();
		}

	/// <summary>Read a file as shell text. NOT `File.ReadAllText(path, enc)`: that defaults to
	/// detectEncodingFromByteOrderMarks, so a file whose first bytes happen to be FF FE or EF BB BF
	/// is decoded as UTF-16/UTF-8-with-BOM and the requested encoding is discarded — which silently
	/// mangled binary data through `$(cat file)` (measured 2026-09-12). Bytes in, bytes out.</summary>
	public static string ReadAllText(string path) => Utf8.GetString(ShellFile.ReadAllBytes(path));

	/// <summary>Lines of a file as shell text, split on LF only. A CR is data and stays in the
	/// line, as in every GNU tool (DECISIONS 2026-10-03).</summary>
	public static string[] ReadAllLines(string path)
		{
		var text = ReadAllText(path);
		if (text.Length == 0) return [];
		var lines = text.Split('\n');
		return lines[^1].Length == 0 ? lines[..^1] : lines;
		}

	/// <summary>The next line of <paramref name="r"/>, ended by LF ONLY (the LF not included), or
	/// null at end of input. Every line-reading tool uses this, never <c>TextReader.ReadLine</c>:
	/// that also ends a line at a lone CR and drops the CR of CRLF, so data changed silently in
	/// awk, head, grep and friends, and a `grep -c $'\r'` check read 0 (DECISIONS 2026-10-03).
	/// An <see cref="LfReader"/> (every stdin the shell installs) answers by block scan; any other
	/// reader -- a here-document, an interactive console -- is read char by char, so it is never
	/// consumed past the line.</summary>
	public static string? ReadLine(TextReader r)
		{
		if (ReferenceEquals(r, Console.In)) r = ConsoleMux.In;   // resolve the per-thread slot once
		if (r is LfReader lf) return lf.ReadLine();
		var sb = t_line ??= new StringBuilder(256);
		sb.Clear();
		int c;
		while ((c = r.Read()) >= 0)
			{
			if (c == '\n') return sb.ToString();
			sb.Append((char)c);
			}
		return sb.Length > 0 ? sb.ToString() : null;
		}

	[ThreadStatic] private static StringBuilder? t_line;

	/// <summary>A next-line function for <paramref name="r"/>, LF only. Stdin is shared -- what
	/// this command leaves unread belongs to the next one -- so it goes through
	/// <see cref="ReadLine"/>, never read ahead of; any other reader is a file the caller opened
	/// and owns, so it gets its own <see cref="LfReader"/>. Measured 2026-10-03: plain
	/// char-by-char reading cost ~85 ns a line, +12.7 % on six tools over 200 k-line files.</summary>
	public static Func<string?> LineReaderFor(TextReader r)
		{
		if (ReferenceEquals(r, Console.In) || ReferenceEquals(r, ConsoleMux.In))
			return () => ReadLine(r);
		return (r as LfReader ?? new LfReader(r)).ReadLine;
		}

	/// <summary>All lines of <paramref name="r"/>, LF only, as <see cref="LineReaderFor"/> reads
	/// them -- written out rather than calling it, as a delegate call per line cost ~10 % (measured).</summary>
	public static IEnumerable<string> Lines(TextReader r)
		{
		string? l;
		if (ReferenceEquals(r, Console.In) || ReferenceEquals(r, ConsoleMux.In))
			{
			while ((l = ReadLine(r)) is not null) yield return l;
			yield break;
			}
		var lf = r as LfReader ?? new LfReader(r);
		while ((l = lf.ReadLine()) is not null) yield return l;
		}

	private static readonly UTF8Encoding Plain = new(false, false);

	private ShellEncoding() { }

	public override string EncodingName => "utf-8 (byte-transparent)";
	public override int CodePage => Plain.CodePage;
	public override byte[] GetPreamble() => [];

	// ── encode ──────────────────────────────────────────────────────────────────
	// Runs of ordinary characters go through UTF-8 unchanged; each escape surrogate emits the
	// single byte it carries. Splitting on the surrogates (rather than encoding char by char)
	// keeps the common all-text case at plain UTF-8 speed.

	public override int GetByteCount(char[] chars, int index, int count) =>
		GetByteCount(new ReadOnlySpan<char>(chars, index, count));

	public override int GetByteCount(ReadOnlySpan<char> chars)
		{
		int total = 0, run = 0;
		for (int i = 0; i < chars.Length; i++)
			{
			if (IsLoneByte(chars, i))
				{
				if (run > 0) { total += Plain.GetByteCount(chars.Slice(i - run, run)); run = 0; }
				total++;
				}
			else run++;
			}
		if (run > 0) total += Plain.GetByteCount(chars[^run..]);
		return total;
		}

	public override int GetBytes(char[] chars, int charIndex, int charCount, byte[] bytes, int byteIndex)
		{
		var written = GetBytes(new ReadOnlySpan<char>(chars, charIndex, charCount), bytes.AsSpan(byteIndex));
		return written;
		}

	public override int GetBytes(ReadOnlySpan<char> chars, Span<byte> bytes)
		{
		int outPos = 0, run = 0;
		for (int i = 0; i < chars.Length; i++)
			{
			if (IsLoneByte(chars, i))
				{
				if (run > 0) { outPos += Plain.GetBytes(chars.Slice(i - run, run), bytes[outPos..]); run = 0; }
				bytes[outPos++] = (byte)(chars[i] - ByteBase);
				}
			else run++;
			}
		if (run > 0) outPos += Plain.GetBytes(chars[^run..], bytes[outPos..]);
		return outPos;
		}

	public override int GetMaxByteCount(int charCount) => Plain.GetMaxByteCount(charCount);

	// ── decode ──────────────────────────────────────────────────────────────────
	// A DecoderFallback would suffice for this direction (it may emit characters), but the
	// encode direction cannot use a fallback — an EncoderFallback may only emit characters to be
	// re-encoded, never raw bytes — so both directions live here for symmetry.

	private static readonly Encoding Strict = Encoding.GetEncoding("utf-8",
		EncoderFallback.ExceptionFallback, DecoderFallback.ExceptionFallback);

	public override int GetCharCount(byte[] bytes, int index, int count) =>
		GetCharCount(new ReadOnlySpan<byte>(bytes, index, count));

	public override int GetCharCount(ReadOnlySpan<byte> bytes)
		{
		Span<char> probe = bytes.Length <= 512 ? stackalloc char[bytes.Length * 2] : new char[bytes.Length * 2];
		return GetChars(bytes, probe);
		}

	public override int GetChars(byte[] bytes, int byteIndex, int byteCount, char[] chars, int charIndex) =>
		GetChars(new ReadOnlySpan<byte>(bytes, byteIndex, byteCount), chars.AsSpan(charIndex));

	public override int GetChars(ReadOnlySpan<byte> bytes, Span<char> chars)
		{
		int outPos = 0, i = 0;
		while (i < bytes.Length)
			{
			// longest valid UTF-8 run from here, decoded in one call
			int len = ValidRunLength(bytes[i..]);
			if (len > 0)
				{
				outPos += Strict.GetChars(bytes.Slice(i, len), chars[outPos..]);
				i += len;
				continue;
				}
			chars[outPos++] = (char)(ByteBase + bytes[i]);   // undecodable byte, carried intact
			i++;
			}
		return outPos;
		}

	/// <summary>Length of the longest prefix of <paramref name="bytes"/> that is valid UTF-8.</summary>
	private static int ValidRunLength(ReadOnlySpan<byte> bytes)
		{
		int i = 0;
		while (i < bytes.Length)
			{
			int seq = SequenceLength(bytes[i..]);
			if (seq <= 0) break;
			i += seq;
			}
		return i;
		}

	/// <summary>Length of a valid UTF-8 sequence at the start, or 0 if it is not one. A truncated
	/// sequence at the very end of the span is NOT valid here: a chunked reader would otherwise
	/// split a character, so the tail bytes are carried as escapes and re-joined never happens.
	/// Callers that read in chunks must therefore decode whole buffers (all of ours do).</summary>
	private static int SequenceLength(ReadOnlySpan<byte> b)
		{
		byte b0 = b[0];
		if (b0 < 0x80) return 1;
		static bool Cont(byte x) => (x & 0xC0) == 0x80;
		if (b0 is >= 0xC2 and <= 0xDF)
			return b.Length >= 2 && Cont(b[1]) ? 2 : 0;
		if (b0 is >= 0xE0 and <= 0xEF)
			{
			if (b.Length < 3 || !Cont(b[1]) || !Cont(b[2])) return 0;
			if (b0 == 0xE0 && b[1] < 0xA0) return 0;                 // overlong
			if (b0 == 0xED && b[1] >= 0xA0) return 0;                // surrogate range
			return 3;
			}
		if (b0 is >= 0xF0 and <= 0xF4)
			{
			if (b.Length < 4 || !Cont(b[1]) || !Cont(b[2]) || !Cont(b[3])) return 0;
			if (b0 == 0xF0 && b[1] < 0x90) return 0;                 // overlong
			if (b0 == 0xF4 && b[1] >= 0x90) return 0;                // > U+10FFFF
			return 4;
			}
		return 0;
		}

	public override int GetMaxCharCount(int byteCount) => byteCount;   // worst case: one char per byte
	}

/// <summary>
/// A text reader whose <see cref="ReadLine"/> ends a line at LF ONLY -- a CR is data -- found by
/// a vectorised scan over its own buffer. The shell installs one as every stdin it creates (a
/// pipeline stage, `&lt; file`, `&lt;&amp;n`, a redirected process stdin), so all the commands
/// reading that stdin share this ONE buffer: lines are fast, and none of them reads past where
/// another stopped. A tool also wraps a file it opened itself. DECISIONS 2026-10-03.
/// </summary>
public sealed class LfReader : TextReader
	{
	private readonly TextReader? _text;     // a reader of a file the caller owns
	private readonly Stream? _stream;       // a stdin: decoded here, ONE read per fill
	private readonly Decoder? _decoder;
	private readonly byte[]? _bytes;
	private readonly bool _leaveOpen;
	private readonly char[] _buf = new char[8192];
	private int _pos, _len;
	private StringBuilder? _carry;

	public LfReader(TextReader inner) => _text = inner;

	/// <summary>Over a raw stdin stream. NOT over a StreamReader: asked for a block, a
	/// StreamReader keeps reading until the block is full or a read comes back short, so a
	/// pipeline stage waited on its producer instead of overlapping it -- measured 2026-10-03,
	/// `head | tail` 132 -> 295 ms. One stream read per fill returns whatever has arrived.</summary>
	public LfReader(Stream stream, bool leaveOpen = false)
		{
		_stream = stream;
		_decoder = ShellEncoding.Utf8.GetDecoder();
		_bytes = new byte[4096];
		_leaveOpen = leaveOpen;
		}

	/// <summary>True when buffered chars are available, reading the next block if needed.</summary>
	private bool Fill()
		{
		if (_pos < _len) return true;
		_pos = 0;
		_len = _text is not null ? _text.Read(_buf, 0, _buf.Length) : ReadStream();
		if (_len > 0) return true;
		_len = 0;
		return false;
		}

	private int ReadStream()
		{
		while (true)
			{
			int nb = _stream!.Read(_bytes!, 0, _bytes!.Length);
			bool eof = nb <= 0;
			int nc = _decoder!.GetChars(_bytes, 0, eof ? 0 : nb, _buf, 0, flush: eof);
			if (nc > 0 || eof) return nc;
			}
		}

	public override int Peek() => Fill() ? _buf[_pos] : -1;
	public override int Read() => Fill() ? _buf[_pos++] : -1;
	public override int Read(char[] buffer, int index, int count) => Read(buffer.AsSpan(index, count));

	public override int Read(Span<char> buffer)
		{
		if (buffer.Length == 0) return 0;
		if (_pos == _len && _text is not null) return _text.Read(buffer);
		if (!Fill()) return 0;
		int n = Math.Min(buffer.Length, _len - _pos);
		_buf.AsSpan(_pos, n).CopyTo(buffer);
		_pos += n;
		return n;
		}

	public override string? ReadLine()
		{
		StringBuilder? sb = null;
		while (Fill())
			{
			int nl = Array.IndexOf(_buf, '\n', _pos, _len - _pos);
			if (nl >= 0)
				{
				string line = sb is null ? new string(_buf, _pos, nl - _pos) : sb.Append(_buf, _pos, nl - _pos).ToString();
				_pos = nl + 1;
				return line;
				}
			(sb ??= (_carry ??= new StringBuilder()).Clear()).Append(_buf, _pos, _len - _pos);   // a line spanning blocks
			_pos = _len;
			}
		return sb is { Length: > 0 } ? sb.ToString() : null;
		}

	/// <summary>Hand over (and drop) what has been read ahead but not consumed, for a byte reader
	/// of the same stdin that takes over from here: `{ read -r x; cat; } &lt; f` lost the rest.</summary>
	public string TakeBuffered()
		{
		var s = new string(_buf, _pos, _len - _pos);
		_pos = _len;
		return s;
		}

	public override string ReadToEnd()
		{
		var sb = new StringBuilder();
		if (_pos < _len) { sb.Append(_buf, _pos, _len - _pos); _pos = _len; }
		if (_text is not null) return sb.Append(_text.ReadToEnd()).ToString();
		while (Fill()) { sb.Append(_buf, _pos, _len - _pos); _pos = _len; }
		return sb.ToString();
		}

	protected override void Dispose(bool disposing)
		{
		if (disposing)
			{
			_text?.Dispose();
			if (!_leaveOpen) _stream?.Dispose();
			}
		base.Dispose(disposing);
		}
	}

/// <summary>Read-only: <paramref name="prefix"/>'s bytes, then <paramref name="rest"/>'s. Never
/// disposes <paramref name="rest"/> (it is the stdin behind a redirect, owned elsewhere).</summary>
public sealed class PrefixedStream(byte[] prefix, Stream rest) : Stream
	{
	private int _pos;

	public override int Read(byte[] buffer, int offset, int count) => Read(buffer.AsSpan(offset, count));

	public override int Read(Span<byte> buffer)
		{
		if (_pos < prefix.Length)
			{
			int n = Math.Min(buffer.Length, prefix.Length - _pos);
			prefix.AsSpan(_pos, n).CopyTo(buffer);
			_pos += n;
			return n;
			}
		return rest.Read(buffer);
		}

	public override bool CanRead => true;
	public override bool CanSeek => false;
	public override bool CanWrite => false;
	public override long Length => throw new NotSupportedException();
	public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
	public override void Flush() { }
	public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
	public override void SetLength(long value) => throw new NotSupportedException();
	public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
	}
