using System.Buffers;
using System.Text;

namespace Bash.Evaluator;

/// <summary>
/// The byte layer the tools share (2026-10-27): an input as bytes (<see cref="OpenBytes"/>), read a rented block at a
/// time (<see cref="Block"/>), written byte for byte (<see cref="ByteSink"/>), a file read from its end
/// (<see cref="RecordsStart"/>) and a pipe's end kept in a bounded window (<see cref="PipeWindow"/>). It replaced
/// whole-input reads (one array, one string), which failed on more than 2 GiB (koliada-net-28: tail, wc). Blocks are
/// rented, not allocated: a call allocating its own 64 KiB, and 131 KB of characters on the large-object heap, made a
/// 111-byte `wc` 10 us slower than the whole-input code (AOT, in-process, measured 2026-10-27).
/// </summary>
public sealed partial class Builtins
	{
	private const int IoBlock = 65536;

	/// <summary>A rented block of <see cref="IoBlock"/> bytes (the shared pool's buckets are powers of two, so exactly
	/// that long); Dispose returns it, and it must not be used after.</summary>
	private readonly struct Block : IDisposable
		{
		public readonly byte[] Bytes;
		private Block(byte[] bytes) => Bytes = bytes;
		public static Block Rent() => new(ArrayPool<byte>.Shared.Rent(IoBlock));
		public void Dispose() => ArrayPool<byte>.Shared.Return(Bytes);
		}

	/// <summary>An input as bytes: the file or device, the bytes behind stdin, or a here-document's text encoded back.
	/// The caller disposes it unless it is stdin.</summary>
	private static Stream OpenBytes(string f) =>
		f != "-" ? ShellFile.OpenInput(ShellEnvironment.TranslatePath(f))
		: Evaluator.CurrentRawStdin() ?? new MemoryStream(ShellEncoding.Utf8.GetBytes(Console.In.ReadToEnd()));

	/// <summary>Several inputs read as one, each to its end in turn (od's "combined input"). Disposes none of them.</summary>
	private sealed class ConcatStream(List<Stream> parts) : Stream
		{
		private int _at;
		public override bool CanRead => true;
		public override bool CanSeek => false;
		public override bool CanWrite => false;
		public override long Length => throw new NotSupportedException();
		public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
		public override int Read(byte[] buffer, int offset, int count)
			{
			for (; _at < parts.Count; _at++)
				if (parts[_at].Read(buffer, offset, count) is var n and > 0) return n;
			return 0;
			}
		public override void Flush() { }
		public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
		public override void SetLength(long value) => throw new NotSupportedException();
		public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
		}

	/// <summary>Where copied bytes go: the raw sink when there is one, else decoded text (a capture), the decoder
	/// carrying a character split across two writes. Flush once, at the end.</summary>
	private sealed class ByteSink
		{
		private readonly Stream? _raw = Evaluator.CurrentRawStdout();
		private readonly Decoder? _decoder;
		private char[]? _chars;

		public ByteSink() { if (_raw is not null) Console.Out.Flush(); else _decoder = ShellEncoding.Utf8.GetDecoder(); }

		public void Write(byte[] b, int offset, int count)
			{
			if (count <= 0) return;
			if (_raw is not null) { _raw.Write(b, offset, count); return; }
			Chars(count + 4);   // up to 3 carried bytes come out as well
			Console.Out.Write(_chars!, 0, _decoder!.GetChars(b, offset, count, _chars!, 0, false));
			}

		public void Flush()
			{
			if (_raw is not null) { _raw.Flush(); return; }
			Chars(4);
			Console.Out.Write(_chars!, 0, _decoder!.GetChars([], 0, 0, _chars!, 0, true));
			ArrayPool<char>.Shared.Return(_chars!);
			_chars = null;
			}

		private void Chars(int at)
			{
			if (_chars is not null && _chars.Length >= at) return;
			if (_chars is not null) ArrayPool<char>.Shared.Return(_chars);
			_chars = ArrayPool<char>.Shared.Rent(Math.Max(at, IoBlock + 4));
			}
		}

	/// <summary>Reads exactly <paramref name="count"/> bytes at <paramref name="pos"/>.</summary>
	private delegate void ReadAt(long pos, byte[] buf, int count);

	/// <summary>The end of a pipe as tail and head need it: whole rented blocks, the oldest dropped (or written, for
	/// head) as soon as the newer ones alone hold enough, so memory follows what is kept and not the input
	/// (`cat log | tail` on a 93 GB log). Every block but the last is full, so a position finds its block by division.
	/// Dispose returns the blocks.</summary>
	private sealed class PipeWindow : IDisposable
		{
		private readonly Queue<(byte[] Buf, int Len, long Terms)> _blocks = new();
		private (byte[] Buf, int Len, long Terms)[] _kept = [];
		private long _terms;
		public long Length { get; private set; }

		/// <summary>Read <paramref name="src"/> to its end. <paramref name="enough"/> is given the byte and terminator
		/// counts of all blocks but the oldest; while it says those suffice, the oldest goes to <paramref name="spill"/>
		/// (or nowhere).</summary>
		public static PipeWindow Fill(Stream src, byte term, Func<long, long, bool> enough, ByteSink? spill)
			{
			var w = new PipeWindow();
			var buf = ArrayPool<byte>.Shared.Rent(IoBlock); int used = 0; long terms = 0;
			for (int n; (n = src.Read(buf, used, IoBlock - used)) > 0; )
				{
				terms += buf.AsSpan(used, n).Count(term);
				used += n;
				if (used < IoBlock) continue;
				w.Add(buf, used, terms);
				buf = ArrayPool<byte>.Shared.Rent(IoBlock); used = 0; terms = 0;
				while (w._blocks.Count > 1 && enough(w.Length - w._blocks.Peek().Len, w._terms - w._blocks.Peek().Terms))
					{
					var (b, len, t) = w._blocks.Dequeue();
					w.Length -= len; w._terms -= t;
					spill?.Write(b, 0, len);
					ArrayPool<byte>.Shared.Return(b);
					}
				}
			if (used > 0) w.Add(buf, used, terms); else ArrayPool<byte>.Shared.Return(buf);
			w._kept = w._blocks.ToArray();
			return w;
			}

		private void Add(byte[] b, int len, long terms) { _blocks.Enqueue((b, len, terms)); Length += len; _terms += terms; }

		public void Read(long pos, byte[] buf, int count)
			{
			for (int done = 0; done < count; )
				{
				var (b, len, _) = _kept[(int)(pos / IoBlock)];
				int off = (int)(pos % IoBlock), n = Math.Min(count - done, len - off);
				Buffer.BlockCopy(b, off, buf, done, n);
				done += n; pos += n;
				}
			}

		public void Dispose()
			{
			foreach (var (b, _, _) in _kept) ArrayPool<byte>.Shared.Return(b);
			_kept = [];
			}
		}

	/// <summary>Where the last <paramref name="n"/> records of [lo, hi) start: just after the n-th terminator from the
	/// end, not counting one that is the last byte (it ends the last record). Read backwards a block at a time, so a
	/// file is read only as far back as those records go, whatever its size.</summary>
	private static long RecordsStart(ReadAt read, long lo, long hi, long n, byte term)
		{
		if (n <= 0) return hi;
		using var block = Block.Rent();
		var buf = block.Bytes;
		for (long pos = hi; pos > lo; )
			{
			int len = (int)Math.Min(IoBlock, pos - lo);
			pos -= len;
			read(pos, buf, len);
			for (int i = len; (i = buf.AsSpan(0, i).LastIndexOf(term)) >= 0; )
				if (pos + i != hi - 1 && --n == 0) return pos + i + 1;
			}
		return lo;
		}

	private static void CopyRange(ReadAt read, long from, long to, ByteSink sink)
		{
		using var block = Block.Rent();
		var buf = block.Bytes;
		for (long pos = from; pos < to; pos += IoBlock)
			{
			int n = (int)Math.Min(IoBlock, to - pos);
			read(pos, buf, n);
			sink.Write(buf, 0, n);
			}
		}

	/// <summary>tail's last <paramref name="count"/> bytes or records, or (<paramref name="head"/>) everything before
	/// them. A seekable input is read from its end; a pipe through a <see cref="PipeWindow"/>. Returns where the input
	/// was taken to end (`tail -f` follows from there).</summary>
	private static long FromEnd(Stream src, long count, bool bytes, byte term, bool head, ByteSink sink)
		{
		ReadAt read; long lo, hi;
		PipeWindow? window = null;
		try
			{
			if (src.CanSeek)
				{
				lo = src.Position; hi = Math.Max(lo, src.Length);
				read = (pos, buf, n) => { src.Seek(pos, SeekOrigin.Begin); src.ReadExactly(buf, 0, n); };
				}
			else
				{
				Func<long, long, bool> enough = bytes ? (len, _) => len >= count : (_, terms) => terms > count;
				window = PipeWindow.Fill(src, term, enough, head ? sink : null);
				lo = 0; hi = window.Length; read = window.Read;
				}
			long cut = bytes ? Math.Max(lo, hi - count) : RecordsStart(read, lo, hi, count, term);
			if (head) CopyRange(read, lo, cut, sink); else CopyRange(read, cut, hi, sink);
			return hi;
			}
		finally { window?.Dispose(); }
		}

	/// <summary>tail +N: everything after the first <paramref name="skip"/> bytes or records (a seekable input seeks
	/// past bytes). Returns where the input ended.</summary>
	private static long CopyAfter(Stream src, long skip, bool bytes, byte term, ByteSink sink)
		{
		if (bytes && src.CanSeek)
			{
			src.Seek(skip >= src.Length - src.Position ? src.Length : src.Position + skip, SeekOrigin.Begin);
			skip = 0;
			}
		using var block = Block.Rent();
		var buf = block.Bytes;
		for (int n; (n = src.Read(buf, 0, IoBlock)) > 0; )
			{
			int from = 0;
			if (bytes && skip > 0) { from = (int)Math.Min(skip, n); skip -= from; }
			while (!bytes && skip > 0 && from < n)
				{
				int i = buf.AsSpan(from, n - from).IndexOf(term);
				if (i < 0) { from = n; break; }
				from += i + 1; skip--;
				}
			sink.Write(buf, from, n - from);
			}
		return src.CanSeek ? src.Position : 0;
		}

	/// <summary>One piece of an input, at its offset from the first byte handed on.</summary>
	private delegate void Piece(long offset, ReadOnlySpan<byte> bytes);

	/// <summary>Up to <paramref name="limit"/> bytes of <paramref name="src"/> handed to <paramref name="piece"/>
	/// <paramref name="width"/> at a time, the last piece maybe shorter (od's and hexdump's lines). Returns how many
	/// bytes there were.</summary>
	private static long ForEachPiece(Stream src, long limit, int width, Piece piece)
		{
		using var block = Block.Rent();
		var buf = block.Bytes;
		var line = new byte[width];   // a line is small (16 by default)
		int have = 0; long total = 0, handed = 0;
		for (int n; total < limit && (n = src.Read(buf, 0, (int)Math.Min(IoBlock, limit - total))) > 0; )
			{
			total += n;
			var rest = buf.AsSpan(0, n);
			while (rest.Length > 0)
				{
				if (have == 0 && rest.Length >= width) { piece(handed, rest[..width]); handed += width; rest = rest[width..]; continue; }
				int take = Math.Min(width - have, rest.Length);
				rest[..take].CopyTo(line.AsSpan(have));
				have += take; rest = rest[take..];
				if (have == width) { piece(handed, line); handed += width; have = 0; }
				}
			}
		if (have > 0) piece(handed, line.AsSpan(0, have));
		return total;
		}

	/// <summary>All of <paramref name="src"/> to a new <see cref="ByteSink"/>, a block at a time.</summary>
	private static void CopyAll(Stream src)
		{
		var sink = new ByteSink();
		using var block = Block.Rent();
		for (int n; (n = src.Read(block.Bytes, 0, IoBlock)) > 0; ) sink.Write(block.Bytes, 0, n);
		sink.Flush();
		}

	/// <summary>head -z -n N: the first <paramref name="count"/> records.</summary>
	private static void CopyRecords(Stream src, long count, byte term, ByteSink sink)
		{
		using var block = Block.Rent();
		var buf = block.Bytes;
		for (int n; count > 0 && (n = src.Read(buf, 0, IoBlock)) > 0; )
			{
			int upto = 0;
			while (count > 0 && upto < n)
				{
				int i = buf.AsSpan(upto, n - upto).IndexOf(term);
				if (i < 0) { upto = n; break; }
				upto += i + 1; count--;
				}
			sink.Write(buf, 0, upto);
			}
		}
	}
