using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;

namespace Bash.Evaluator;

/// <summary>
/// Per-thread console streams. The builtins all write to <c>Console.Out</c> / read
/// <c>Console.In</c>; the process-global Console cannot serve concurrent pipeline stages, so
/// the real Console writers are replaced once with multiplexers that dispatch to a
/// thread-static slot (null = the real stream). Redirects, captures and pipeline stages set
/// the slot for their thread; the builtins need no changes. Child threads inherit their
/// parent's slots through <see cref="Capture"/>/<see cref="Apply"/>.
///
/// The multiplexers are installed *unsynchronized* (bypassing Console.SetOut's
/// SyncTextWriter wrapper via reflection when possible): a global lock would let a stage
/// blocked on a full pipe stall every other stage's output — a deadlock, not a slowdown.
/// Each target writer is locked individually instead.
/// </summary>
public static class ConsoleMux
	{
	private static TextWriter _realOut = null!;
	private static TextWriter _realErr = null!;
	private static TextReader _realIn  = null!;
	private static bool _installed;

	[ThreadStatic] private static TextWriter? _out;
	[ThreadStatic] private static TextWriter? _err;
	[ThreadStatic] private static TextReader? _in;

	/// <summary>Raw byte sink mirroring the thread's stdout (a file or pipe), for byte-faithful builtins.</summary>
	[ThreadStatic] public static Stream? Raw;
	/// <summary>True while a `$( )` capture is in effect on this thread (byte builtins write text).</summary>
	[ThreadStatic] public static bool Capturing;
	/// <summary>Pipeline pipe ends for this thread's stage, handed to external children directly.</summary>
	[ThreadStatic] public static Stream? PipeIn;
	[ThreadStatic] public static Stream? PipeOut;
	/// <summary>Raw byte view of the thread's CURRENT stdin (a pipe or a `&lt; file`), for byte-faithful
	/// builtins; null when stdin is text (here-doc/here-string) or the console. Set wherever
	/// <see cref="SetIn"/> installs a new stdin. Until 2026-09-11 every stdin byte went through the
	/// UTF-8 text reader and each invalid byte came out as U+FFFD (three bytes) — binary data
	/// through `&lt;` or `|` was silently corrupted (installer-79's report).</summary>
	[ThreadStatic] public static Stream? RawIn;

	public static void Install()
		{
		if (_installed) return;
		_realOut = Console.Out;
		_realErr = Console.Error;
		_realIn  = Console.In;
		var mo = new MuxWriter(false);
		var me = new MuxWriter(true);
		var mi = new MuxReader();
		Console.SetOut(mo);   if (!ReferenceEquals(Console.Out, mo))   SetField("s_out", mo);
		Console.SetError(me); if (!ReferenceEquals(Console.Error, me)) SetField("s_error", me);
		Console.SetIn(mi);    if (!ReferenceEquals(Console.In, mi))    SetField("s_in", mi);
		_installed = true;
		}

	private static void SetField(string name, object value)
		{
		try
			{
			var f = typeof(Console).GetField(name, BindingFlags.NonPublic | BindingFlags.Static);
			f?.SetValue(null, value);
			}
		catch { /* keep the synchronized wrapper: functional, with a cross-stage stall risk */ }
		}

	public static TextWriter Out => _out ?? _realOut ?? Console.Out;
	public static TextWriter Err => _err ?? _realErr ?? Console.Error;
	public static TextReader In  => _in  ?? _realIn  ?? Console.In;

	public static TextWriter? OutSlot => _out;
	public static TextWriter? ErrSlot => _err;
	public static TextReader? InSlot  => _in;
	public static bool OutSwapped => _out is not null;
	public static bool ErrSwapped => _err is not null;
	public static bool InSwapped  => _in  is not null;

	public static void SetOut(TextWriter? w) => _out = w;
	public static void SetErr(TextWriter? w) => _err = w;
	public static void SetIn(TextReader? r)  => _in  = r;

	public static TextWriter RealOut => _realOut ?? Console.Out;

	/// <summary>The thread's whole stdio state, for handing to a child thread.</summary>
	public readonly record struct State(TextWriter? Out, TextWriter? Err, TextReader? In, Stream? Raw, bool Capturing, Stream? PipeIn, Stream? PipeOut, Stream? RawIn);

	public static State Capture() => new(_out, _err, _in, Raw, Capturing, PipeIn, PipeOut, RawIn);

	public static void Apply(State s)
		{
		_out = s.Out; _err = s.Err; _in = s.In;
		Raw = s.Raw; Capturing = s.Capturing; PipeIn = s.PipeIn; PipeOut = s.PipeOut; RawIn = s.RawIn;
		}

	// ── the multiplexers ──────────────────────────────────────────────────────

	private sealed class MuxWriter(bool isErr) : TextWriter
		{
		private TextWriter T => isErr ? Err : Out;
		public override Encoding Encoding => T.Encoding;
		public override IFormatProvider FormatProvider => T.FormatProvider;
		[AllowNull]
		public override string NewLine { get => T.NewLine; set { var t = T; lock (t) t.NewLine = value ?? "\n"; } }
		public override void Write(char value) { var t = T; lock (t) t.Write(value); }
		public override void Write(char[] buffer, int index, int count) { var t = T; lock (t) t.Write(buffer, index, count); }
		public override void Write(ReadOnlySpan<char> buffer) { var t = T; lock (t) t.Write(buffer); }
		public override void Write(string? value) { var t = T; lock (t) t.Write(value); }
		public override void WriteLine() { var t = T; lock (t) t.WriteLine(); }
		public override void WriteLine(string? value) { var t = T; lock (t) t.WriteLine(value); }
		public override void WriteLine(ReadOnlySpan<char> buffer) { var t = T; lock (t) t.WriteLine(buffer); }
		public override void Flush() { var t = T; lock (t) t.Flush(); }
		}

	private sealed class MuxReader : TextReader
		{
		private TextReader T => In;
		public override int Peek() => T.Peek();
		public override int Read() => T.Read();
		public override int Read(char[] buffer, int index, int count) => T.Read(buffer, index, count);
		public override int Read(Span<char> buffer) => T.Read(buffer);
		public override int ReadBlock(char[] buffer, int index, int count) => T.ReadBlock(buffer, index, count);
		public override string? ReadLine() => T.ReadLine();
		public override string ReadToEnd() => T.ReadToEnd();
		}
	}

/// <summary>Thrown to a writer whose reader has gone away — bash's SIGPIPE.</summary>
public sealed class BrokenPipeException() : IOException("Broken pipe");

/// <summary>
/// An in-memory pipe between two pipeline stages: a bounded byte queue with blocking
/// writes (back-pressure), blocking reads, EOF on writer close, and a
/// <see cref="BrokenPipeException"/> to the writer once the reader has closed — so a
/// producer stops when its consumer exits early (`yes | head`).
/// </summary>
public sealed class PipeBuffer
	{
	private readonly object _lock = new();
	private readonly Queue<byte[]> _chunks = new();
	private int _size, _readOffset;
	private bool _writeClosed, _readClosed;
	private readonly int _capacity;

	public PipeBuffer(int capacity = 1 << 20)
		{
		_capacity = capacity;
		WriteEnd = new WriteStream(this);
		ReadEnd  = new ReadStream(this);
		}

	public Stream WriteEnd { get; }
	public Stream ReadEnd  { get; }

	private void Write(byte[] buffer, int offset, int count)
		{
		if (count <= 0) return;
		lock (_lock)
			{
			if (_readClosed) throw new BrokenPipeException();
			while (_size >= _capacity && !_readClosed) Monitor.Wait(_lock);
			if (_readClosed) throw new BrokenPipeException();
			var copy = new byte[count];
			Buffer.BlockCopy(buffer, offset, copy, 0, count);
			_chunks.Enqueue(copy);
			_size += count;
			Monitor.PulseAll(_lock);
			}
		}

	private int Read(byte[] buffer, int offset, int count)
		{
		lock (_lock)
			{
			while (_chunks.Count == 0 && !_writeClosed) Monitor.Wait(_lock);
			if (_chunks.Count == 0) return 0;
			var head = _chunks.Peek();
			int avail = head.Length - _readOffset;
			int n = Math.Min(avail, count);
			Buffer.BlockCopy(head, _readOffset, buffer, offset, n);
			_readOffset += n;
			_size -= n;
			if (_readOffset >= head.Length) { _chunks.Dequeue(); _readOffset = 0; }
			Monitor.PulseAll(_lock);
			return n;
			}
		}

	public void CloseWrite() { lock (_lock) { _writeClosed = true; Monitor.PulseAll(_lock); } }
	public void CloseRead()  { lock (_lock) { _readClosed = true; _chunks.Clear(); _size = 0; _readOffset = 0; Monitor.PulseAll(_lock); } }
	public bool ReadClosed  { get { lock (_lock) return _readClosed; } }

	private sealed class WriteStream(PipeBuffer p) : Stream
		{
		public override bool CanRead => false;
		public override bool CanSeek => false;
		public override bool CanWrite => true;
		public override long Length => throw new NotSupportedException();
		public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
		public override void Flush() { }
		public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
		public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
		public override void SetLength(long value) => throw new NotSupportedException();
		public override void Write(byte[] buffer, int offset, int count) => p.Write(buffer, offset, count);
		public override void Write(ReadOnlySpan<byte> buffer) { var a = buffer.ToArray(); p.Write(a, 0, a.Length); }
		protected override void Dispose(bool disposing) { p.CloseWrite(); base.Dispose(disposing); }
		}

	private sealed class ReadStream(PipeBuffer p) : Stream
		{
		public override bool CanRead => true;
		public override bool CanSeek => false;
		public override bool CanWrite => false;
		public override long Length => throw new NotSupportedException();
		public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
		public override void Flush() { }
		public override int Read(byte[] buffer, int offset, int count) => p.Read(buffer, offset, count);
		public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
		public override void SetLength(long value) => throw new NotSupportedException();
		public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
		protected override void Dispose(bool disposing) { p.CloseRead(); base.Dispose(disposing); }
		}
	}

/// <summary>
/// A Windows job object with KILL_ON_JOB_CLOSE: every child process is assigned to it, so
/// when this shell dies — including being killed by its host on a timeout — its children
/// die with it instead of lingering. Best effort: assignment failures are ignored.
/// </summary>
public static class ChildJobs
	{
	private static IntPtr _job;
	private static bool _tried;

	[StructLayout(LayoutKind.Sequential)]
	private struct JobObjectBasicLimitInformation
		{
		public long PerProcessUserTimeLimit, PerJobUserTimeLimit;
		public uint LimitFlags;
		public UIntPtr MinimumWorkingSetSize, MaximumWorkingSetSize;
		public uint ActiveProcessLimit;
		public UIntPtr Affinity;
		public uint PriorityClass, SchedulingClass;
		}

	[StructLayout(LayoutKind.Sequential)]
	private struct IoCounters
		{
		public ulong ReadOperationCount, WriteOperationCount, OtherOperationCount, ReadTransferCount, WriteTransferCount, OtherTransferCount;
		}

	[StructLayout(LayoutKind.Sequential)]
	private struct JobObjectExtendedLimitInformation
		{
		public JobObjectBasicLimitInformation BasicLimitInformation;
		public IoCounters IoInfo;
		public UIntPtr ProcessMemoryLimit, JobMemoryLimit, PeakProcessMemoryUsed, PeakJobMemoryUsed;
		}

	[DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
	private static extern IntPtr CreateJobObjectW(IntPtr lpJobAttributes, string? lpName);

	[DllImport("kernel32.dll", SetLastError = true)]
	private static extern bool SetInformationJobObject(IntPtr hJob, int jobObjectInformationClass, ref JobObjectExtendedLimitInformation lpJobObjectInformation, int cbJobObjectInformationLength);

	[DllImport("kernel32.dll", SetLastError = true)]
	private static extern bool AssignProcessToJobObject(IntPtr hJob, IntPtr hProcess);

	private const int JobObjectExtendedLimitInformationClass = 9;
	private const uint JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE = 0x2000;

	private static void EnsureJob()
		{
		if (_tried) return;
		_tried = true;
		if (!OperatingSystem.IsWindows()) return;
		try
			{
			var job = CreateJobObjectW(IntPtr.Zero, null);
			if (job == IntPtr.Zero) return;
			var info = new JobObjectExtendedLimitInformation();
			info.BasicLimitInformation.LimitFlags = JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE;
			if (!SetInformationJobObject(job, JobObjectExtendedLimitInformationClass, ref info, Marshal.SizeOf<JobObjectExtendedLimitInformation>()))
				return;
			_job = job;
			}
		catch { }
		}

	/// <summary>Put a freshly started child into the shell's job (no-op when unavailable).</summary>
	public static void Attach(System.Diagnostics.Process proc)
		{
		EnsureJob();
		if (_job == IntPtr.Zero) return;
		try { AssignProcessToJobObject(_job, proc.Handle); } catch { }
		}
	}
