using System.Runtime.InteropServices;

namespace Bash.Evaluator;

/// <summary>
/// Holds shell variables, arrays, exports, attributes (readonly / integer), and the
/// positional-parameter / scope stack; resolves the special parameters; owns path
/// translation. Bash-standard variables (OSTYPE, HOSTNAME, PPID, UID, SHLVL, BASH,
/// BASH_VERSINFO, PWD, IFS) are set at construction.
/// </summary>
public sealed class ShellEnvironment
	{
	/// <summary>One variable in one scope, as bash keeps it: a scalar, an indexed array or an associative
	/// array, with its attributes. Declared with no value (`local x`) is a scalar whose Value is null.</summary>
	internal sealed class Var
		{
		public string? Value;
		public SortedDictionary<int, string>? Indexed;
		public Dictionary<string, string>? Assoc;
		public bool Exported, Readonly, Integer;

		public bool IsUnset => Value is null && Indexed is null && Assoc is null;

		public Var Clone() => new()
			{
			Value = Value,
			Indexed = Indexed is null ? null : new SortedDictionary<int, string>(Indexed),
			Assoc = Assoc is null ? null : new Dictionary<string, string>(Assoc, StringComparer.Ordinal),
			Exported = Exported, Readonly = Readonly, Integer = Integer,
			};
		}

	/// <summary>The scopes, global first, the innermost function call last. Each holds its variables
	/// whole, arrays and attributes included, and a name is looked up innermost-out (bash's dynamic scope).
	/// An assignment to a name no scope holds goes to the GLOBAL scope; `local` (and `declare` in a function)
	/// to the innermost. Until 2026-10-12 only scalars had scopes, a new name went to the innermost one (so a
	/// function's `x=1` vanished on return) and arrays and attributes were shell-wide (DECISIONS 2026-10-12,
	/// "VARIABLE SCOPE").</summary>
	private readonly List<Dictionary<string, Var>> _frames = [new(StringComparer.Ordinal)];

	/// <summary>The innermost variable of that name, or null. FUNCNAME, BASH_SOURCE and BASH_LINENO are
	/// written only when one of them is next read: a function call or return just marks them stale
	/// (<see cref="CallStackStale"/>; rebuilding them on every call and return cost ~0.9 µs a call, +34% on
	/// a bare call, measured 2026-10-10).</summary>
	private Var? Find(string name)
		{
		if (CallStackStale && name is "FUNCNAME" or "BASH_SOURCE" or "BASH_LINENO") RefreshCallStackNow();
		for (int i = _frames.Count - 1; i >= 0; i--)
			if (_frames[i].TryGetValue(name, out var v)) return v;
		return null;
		}

	private void RefreshCallStackNow() { CallStackStale = false; RefreshCallStack?.Invoke(); }

	/// <summary>The variable an assignment changes: the innermost of that name, else a new GLOBAL one.</summary>
	private Var Target(string name)
		{
		var v = Find(name);
		if (v is null) { v = new Var(); _frames[0][name] = v; }
		return v;
		}

	private static void CheckWritable(Var v, string name)
		{
		if (v.Readonly) throw new FatalShellException($"{name}: readonly variable");
		}

	/// <summary>Set by the evaluator: writes its call stack into FUNCNAME / BASH_SOURCE / BASH_LINENO.</summary>
	public Action? RefreshCallStack { get; set; }
	/// <summary>The call stack changed since it was last written.</summary>
	public bool CallStackStale { get; set; }

	/// <summary>Set by the evaluator: its descriptor N (an `exec 3< f`), for /dev/fd/N (ShellFile).</summary>
	public Func<int, Stream?>? FdStream { get; set; }

	/// <summary>Set by the evaluator: its current error prefix (`script: line N: `).</summary>
	public Func<string>? ErrorPrefixProvider { get; set; }
	private readonly Stack<string[]> _positionals = new();
	private string _arg0 = ToShellPath(LaunchedAs());

	/// <summary>argv[0] as the launcher passed it: the command line's first token (Windows' rules), in
	/// the shell's forward-slash form above. A launcher passes the resolved native path, so `$0` (and so
	/// every `-c` error prefix) read `F:\…\bash.exe:` under Claude Code and `bash.EXE` under C#Bash until
	/// 2026-10-11; and GetCommandLineArgs()[0] is `Bash.dll` on the JIT build (an apphost artifact).</summary>
	private static string LaunchedAs()
		{
		try
			{
			var line = Marshal.PtrToStringUni(GetCommandLineW()) ?? "";
			int i = 0;
			while (i < line.Length && line[i] is ' ' or '\t') i++;
			if (i < line.Length && line[i] == '"')
				{
				int close = line.IndexOf('"', i + 1);
				return close > i ? line[(i + 1)..close] : line[(i + 1)..];
				}
			int end = i;
			while (end < line.Length && line[end] is not (' ' or '\t')) end++;
			if (end > i) return line[i..end];
			}
		catch (Exception) { }
		return System.Environment.GetCommandLineArgs()[0];
		}

	[DllImport("kernel32.dll")] private static extern IntPtr GetCommandLineW();
	private readonly Random _random = new();
	private readonly DateTime _start = DateTime.UtcNow;

	public int LastExitCode { get; set; } = 0;

	// ── hooks the evaluator fills in ────────────────────────────────────────────
	/// <summary>`$!` — pid of the most recent background job (0 = none).</summary>
	public int LastBackgroundPid { get; set; }
	/// <summary>`$LINENO` — line of the statement being executed.</summary>
	public int CurrentLine { get; set; }
	/// <summary>`$-` — the current option flags.</summary>
	public Func<string>? FlagsProvider { get; set; }
	/// <summary>`$BASH_COMMAND` — the command running or about to run (in a trap: the one the trap
	/// is for), rendered when read. Empty until 2026-10-11.</summary>
	public Func<string>? CommandTextProvider { get; set; }
	/// <summary>Arithmetic evaluator used for `declare -i` assignments.</summary>
	public Func<string, long>? ArithEval { get; set; }
	/// <summary>`set -a`: export every variable on assignment.</summary>
	public bool AutoExport { get; set; }

	public ShellEnvironment()
		{
		OwnsProcessCwd = true;
		Cwd = ToShellPath(Directory.GetCurrentDirectory());
		_positionals.Push([]);
		foreach (System.Collections.DictionaryEntry e in System.Environment.GetEnvironmentVariables())
			{
			var key = e.Key?.ToString() ?? "";
			var val = e.Value?.ToString() ?? "";
			// Windows spells it "Path"; bash scripts (and Claude Code's snapshot) say PATH.
			// MSYS does the same upper-casing on import.
			if (key.Equals("PATH", StringComparison.OrdinalIgnoreCase)) key = "PATH";
			if (key.Length > 0)
				{
				// PATH in the shell's own form, `/c/x:/f/y` (NormalizePath; 2026-10-31)
				_frames[0][key] = new Var { Value = key == "PATH" ? NormalizePath(val) : val, Exported = true };
				}
			}
		Timing.Mark("env.import");   // the timing switch splits the "evaluator" segment (review R4, 2026-10-18)

		// BASH_PATH: a launcher that keeps its own PATH narrow (claude.exe) hands the shell its PATH here
		// (DECISIONS 2026-10-16 "RATIFIED: the tool-lookup plan"). Taken once and removed, from the process
		// environment too: a nested bash must keep its parent's `export PATH=/x:$PATH`, not reset it.
		if (_frames[0].Remove("BASH_PATH", out var handed))
			{
			try { Environment.SetEnvironmentVariable("BASH_PATH", null); } catch { }
			if (handed.Value is { Length: > 0 } list) Set("PATH", list);
			}

		// No Git search (the architect, 2026-11-09: "remove the git search entirely"): a Git on PATH is not extended
		// with its usr/bin and mingw64/bin, so a start touches no PATH folder (it cost ~2 ms, 48 entries, Git off PATH).
		PrependOwnDirectoryToPath();
		Timing.Mark("env.own");

		// The process's own PATH is an exit too (a program .NET or Windows starts for C#Bash reads it): the Windows form,
		// `C:/x;F:/y`, never a backslash and never an empty entry. An empty entry (`;;`) is the current directory to
		// Windows' launcher, which searches this PROCESS's PATH for a name C#Bash did not find,
		// NoDefaultCurrentDirectoryInExePath notwithstanding: a `.exe` planted in the cwd ran (2026-10-17). `Set` writes
		// it on every assignment; here for the PATH as inherited. The shell's $PATH keeps what it was given.
		try
			{
			var exitForm = ToWindowsPathList(Get("PATH"));
			if (Environment.GetEnvironmentVariable("PATH") is { } processPath && processPath != exitForm)
				Environment.SetEnvironmentVariable("PATH", exitForm.Length > 0 ? exitForm : null);
			}
		catch { }

		if (Get("HOME").Length == 0)
			{
			var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
			Set("HOME", home); Export("HOME");
			}
		// the shell's paths are forward slash (DECISIONS 2026-09-11/12): HOME arrived as C:\Users\x,
		// so $HOME, ~ and cd's default were backslash paths until 2026-10-08
		if (Get("HOME").Contains('\\')) Target("HOME").Value = ToShellPath(Get("HOME"));
		Timing.Mark("env.home");

		// bash-standard shell variables (not exported unless inherited)
		var exe = ToShellPath(Environment.ProcessPath ?? "bash");
		SetIfUnset("OSTYPE",   "msys");                 // DECISIONS 2026-09-04 (1)
		SetIfUnset("MACHTYPE", "x86_64-pc-msys");
		SetIfUnset("HOSTTYPE", "x86_64");
		SetIfUnset("HOSTNAME", LowerInvariant(Environment.MachineName));
		SetIfUnset("IFS",      " \t\n");
		SetIfUnset("BASH",     exe);
		SetIfUnset("OPTIND",   "1");
		SetIfUnset("OPTERR",   "1");
		SetIfUnset("PS4",      "+ ");
		if (Get("SHELL").Length == 0) { Set("SHELL", exe); Export("SHELL"); }
		// invariant: the current culture loads ICU (~0.7 ms) on every start (2026-10-18)
		int shlvl = int.TryParse(Get("SHLVL"), System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var lv) ? lv + 1 : 1;
		Set("SHLVL", shlvl.ToString()); Export("SHLVL");
		var uid = WindowsUid();
		Timing.Mark("env.uid");
		Set("UID", uid.ToString()); Set("EUID", uid.ToString());
		MarkReadonly("UID"); MarkReadonly("EUID");
		Set("PPID", ParentPid().ToString()); MarkReadonly("PPID");
		Timing.Mark("env.ppid");
		Set("PWD", Cwd); Export("PWD");
		SetArrayFromList("BASH_VERSINFO", ["5", "1", "0", "0", "release", "x86_64-pc-msys"]);
		MarkReadonly("BASH_VERSINFO");
		Timing.Mark("env.done");
		}

	private void SetIfUnset(string name, string value) { if (Get(name).Length == 0) Set(name, value); }

	/// <summary>Bypasses the public constructor's process-environment import and PATH probing,
	/// which cost ~1.4 ms (measured 2026-10-03): <see cref="CloneForSubshell"/> fills it.</summary>
	private ShellEnvironment(ShellEnvironment src)
		{
		if (src.CallStackStale) src.RefreshCallStackNow();   // the copy gets the call stack as it stands
		_frames.Clear();
		_frames.AddRange(CopyFrames(src._frames));
		foreach (var p in src._positionals.Reverse()) _positionals.Push(p);   // enumerates top first
		_arg0 = src._arg0;
		_start = src._start;
		LastExitCode = src.LastExitCode;
		LastBackgroundPid = src.LastBackgroundPid;
		CurrentLine = src.CurrentLine;
		AutoExport = src.AutoExport;
		Umask = src.Umask;
		Cwd = src.Cwd;              // a stage starts where the pipeline does ...
		OwnsProcessCwd = false;     // ... and never moves the process cwd
		_baseCwd = src.OwnsProcessCwd ? src.Cwd : src._baseCwd;
		Detached = src.Detached;    // a job's stages, subshells and $( ) are detached with it
		}

	/// <summary>A `;` PATH list without its empty entries (`;;`, a leading or trailing `;`), order kept; the same
	/// string when it has none. Windows takes an empty entry as the current directory (2026-10-17).</summary>
	public static string WithoutEmptyPathEntries(string path)
		{
		if (!path.Contains(";;") && !path.StartsWith(';') && !path.EndsWith(';')) return path;
		return string.Join(';', path.Split(';', StringSplitOptions.RemoveEmptyEntries));
		}

	/// <summary>An independent copy of all variable state, for a pipeline stage: bash runs every
	/// stage in a subshell, so what a stage sets never reaches the parent or a sibling stage.
	/// ~2 µs for Claude Code's full snapshot (measured 2026-10-03). The hooks (`$-`, arithmetic)
	/// are the new owner's to set.</summary>
	public ShellEnvironment CloneForSubshell() => new(this);

	// ── path translation ──────────────────────────────────────────────────────

	/// <summary>
	/// Translate a shell-side path to the form the filesystem APIs get. SYMMETRIC with
	/// <see cref="ToShellPath"/> (ratified 2026-09-12): the shell's path form is FORWARD SLASH
	/// in both directions, so a path that goes out through `pwd` and comes back in as an
	/// argument is the same string. A `~` is a name (the shell expands an unquoted one); `/c/x` (MSYS drive form) →
	/// `C:/x`; `/tmp[/x]` → the temp dir; and any backslashes in the input are normalized to
	/// forward slashes. .NET's file APIs accept forward slashes everywhere, so nothing needs the
	/// backslash form. `/dev/...` is left alone (the redirect layer interprets it). The general
	/// Unix root mapping (`/usr`, `/etc`) is deferred — see DECISIONS.md.
	/// </summary>
	public static string TranslatePath(string path) => FileByDeviceName(AgainstActiveCwd(TranslateForm(path)));

	/// <summary>A path whose final name is a Windows device name (nul, con, aux.txt, com1) and which has a folder part
	/// (`d/nul`, `./nul`, `C:/x/aux.txt`) is that FILE, by its extended path, as under Git Bash: MSYS trees hold such files
	/// (Git for Windows' dev/nul). A bare name (`nul`, `> nul`, `2>nul`) stays the device, as Windows means it, so it never
	/// leaves a file Windows' own tools cannot delete (the architect's ruling, 2026-11-07, on Claude's recommendation).
	/// Until then every such path was the device: `cat d/nul` printed nothing, `cp x d/nul` wrote nowhere.</summary>
	private static string FileByDeviceName(string p)
		{
		int cut = Math.Max(p.LastIndexOf('/'), p.LastIndexOf('\\'));
		if (cut < 0 && !(p.Length > 2 && p[1] == ':')) return p;   // no folder part: the device
		var name = cut >= 0 ? p[(cut + 1)..] : p[2..];
		return name.Length > 0 && ShellFile.IsDeviceName(name) && !p.StartsWith("/dev/", StringComparison.Ordinal)
			? ShellFile.Extended(p) : p;
		}

	/// <summary>A relative path, resolved against the running shell's OWN cwd when that is not the
	/// process cwd -- i.e. in a pipeline stage that has changed directory (DECISIONS 2026-10-03).
	/// Everywhere else it stays relative, so the process cwd resolves it exactly as before and no
	/// output that echoes a translated path changes.</summary>
	private static string AgainstActiveCwd(string p)
		{
		if (p.Length == 0 || t_active is not { OwnsProcessCwd: false } a || (!a.Detached && a.Cwd == a._baseCwd) || Path.IsPathFullyQualified(p)) return p;
		return ToShellPath(FullAgainst(p, a.Cwd));
		}

	/// <summary><paramref name="p"/> made full against the directory <paramref name="cwd"/> (fully qualified, shell form):
	/// relative as joined; `\x` on cwd's drive; `C:x` in cwd when cwd is on C:, else in C:'s own current directory. A
	/// pipeline stage or job resolved `\x` and `C:x` against the PROCESS cwd's drive (2026-10-17).</summary>
	internal static string FullAgainst(string p, string cwd)
		{
		if (Path.IsPathFullyQualified(p)) return p;
		if (p.Length >= 2 && p[1] == ':')
			return cwd.Length >= 2 && cwd[1] == ':' && char.ToUpperInvariant(cwd[0]) == char.ToUpperInvariant(p[0])
				? Path.GetFullPath(Path.Join(cwd, p[2..])) : Path.GetFullPath(p);
		if (p.Length > 0 && p[0] is '\\' or '/')
			return Path.GetFullPath(Path.Join(Path.GetPathRoot(cwd) ?? "", p.TrimStart('\\', '/')));
		return Path.GetFullPath(Path.Join(cwd, p));
		}

	/// <summary>For a shell that does not own the process cwd: the process cwd it started from.
	/// Until it moves away from it, the process cwd resolves its relative paths exactly as before.</summary>
	private string _baseCwd = "";

	/// <summary>
	/// A Windows program's argument, with MSYS-form paths converted to Windows form where Git Bash converts them:
	/// <see cref="NativeValue"/>, unless the argument starts with one of <paramref name="excluded"/> (Git Bash's
	/// MSYS2_ARG_CONV_EXCL prefixes). Until 2026-10-10 every argument reached the program verbatim, so
	/// `git -C /f/repo` failed while the shell's own `cd /f/repo` worked (the architect: "your recommendation is good").
	/// </summary>
	public static string NativeArgument(string arg, string[]? excluded = null)
		{
		if (excluded is not null && Array.Exists(excluded, p => p.Length > 0 && arg.StartsWith(p, StringComparison.Ordinal))) return arg;
		return NativeValue(arg);
		}

	/// <summary>
	/// An argument or an environment value as Git Bash hands it to a Windows program (measured against Git Bash 4.4.23):
	/// an MSYS-form path at the START becomes Windows form, `/c/x` → `C:/x` (a drive that exists, so `/a/b` with no A:
	/// stays), `/tmp/x` → the temp directory, a list of such paths `/c/x:/f/y` → `C:/x;F:/y`; and the same INSIDE it, after
	/// the first `=` (`--file=/c/x`, `a=b=/c/x`, recursively) and after `-` and one letter (`-I/c/x`), each part as a whole
	/// argument would be. Left as typed: anything holding `;` (a Windows list already: Git Bash converts none of it), an
	/// `=` in one holding `://` or starting with a drive (`http://h/a=/c/x`, `C:/a=/c/x`), a bare `/c` (`cmd /c`) or `/`
	/// anywhere. Slashes stay forward (Git Bash writes a list with `\`). `//x` is Git Bash's escape for a Windows switch
	/// (<see cref="DoubleSlash"/>): `cmd //c` runs `cmd /c`. `~` the shell expands before. Until 2026-11-07 only the start
	/// was converted (`--file=/c/x` reached the program as typed) and `/c/x;/f/y` had its first item converted alone.
	/// </summary>
	internal static string NativeValue(string arg)
		{
		// /dev/null for a Windows program is `nul`: the whole argument, after `=`, or after a one-letter option, as Git Bash
		// (measured 2026-11-05: `nul`, `--out=nul`, `-onul`; `x/dev/null` stays). It was passed as typed, a path that does
		// not exist: `curl -o /dev/null` exited 23, a write error.
		if (arg.EndsWith("/dev/null", StringComparison.Ordinal))
			{
			var head = arg[..^9];
			if (head.Length == 0 || head[^1] == '=' || (head.Length == 2 && head[0] == '-' && char.IsAsciiLetter(head[1]))) return head + "nul";
			}
		if (arg.Contains(';')) return arg;
		if (arg.Length >= 2 && arg[0] == '/') return StartConverted(arg);
		int eq = arg.IndexOf('=');
		if (eq >= 0)
			{
			if (arg.Contains("://", StringComparison.Ordinal) || (arg.Length >= 3 && char.IsAsciiLetter(arg[0]) && arg[1] == ':' && arg[2] is '/' or '\\'))
				return arg;
			var value = arg[(eq + 1)..];
			var converted = NativeValue(value);
			return ReferenceEquals(converted, value) ? arg : arg[..(eq + 1)] + converted;
			}
		if (arg.Length >= 4 && arg[0] == '-' && char.IsAsciiLetter(arg[1]) && arg[2] == '/')
			{
			var value = arg[2..];
			var converted = StartConverted(value);
			return ReferenceEquals(converted, value) ? arg : arg[..2] + converted;
			}
		return arg;
		}

	/// <summary>An argument starting with `/`, its MSYS-form path converted (see <see cref="NativeValue"/>).</summary>
	private static string StartConverted(string arg)
		{
		if (arg.Length < 2) return arg;
		if (arg[1] == '/') return DoubleSlash(arg);
		// a colon: a PATH-style list when every piece is a /-rooted path, converted whole, `/c/x:/f/y` -> `C:/x;F:/y`
		// (each piece as a lone argument would be); with any other piece the argument is left as typed (`/c/x:rel`,
		// `/c/x:`, `/c/x::/f/y`, `/c/x/a:b`). Both as Git Bash, measured 2026-10-31, slashes forward. Until then only the
		// start was converted: `C:/Windows:/f/Tools`.
		if (arg.Contains(':'))
			{
			var pieces = arg.Split(':');
			return Array.TrueForAll(pieces, p => p.Length > 0 && p[0] == '/')
				? string.Join(";", pieces.Select(p => NativeArgument(p)))
				: arg;
			}
		if (arg.Length < 3) return arg;
		if (char.IsAsciiLetter(arg[1]) && arg[2] == '/')
			{
			char d = char.ToUpperInvariant(arg[1]);
			if (!Array.Exists(Environment.GetLogicalDrives(), r => r.Length > 0 && char.ToUpperInvariant(r[0]) == d)) return arg;
			return d + ":/" + arg[3..];
			}
		if (arg == "/tmp" || arg.StartsWith("/tmp/", StringComparison.Ordinal))
			{
			var temp = ToShellPath(Path.TrimEndingDirectorySeparator(Path.GetTempPath()));
			return arg.Length == 4 ? temp : temp + arg[4..];
			}
		return arg;
		}

	/// <summary>
	/// An argument starting `//`, as Git Bash hands it to a Windows program: one leading `/` goes and every
	/// `\` becomes `/` (`//c` → `/c`, `//a\b` → `/a/b`, `///x` → `//x`, `//` → `/`); exactly two slashes with
	/// another `/` later is a UNC path and keeps both (`//host/share/x`; a `\` still becomes `/`); an argument
	/// holding a glob or quote character (<c>* ? [ ] " '</c> or a backquote) is left alone. Measured against Git
	/// Bash 2026-10-13. Until then `//c` reached the program as typed, so `cmd //c …` started an interactive
	/// cmd that waited on standard input.
	/// </summary>
	private static string DoubleSlash(string arg)
		{
		if (arg.IndexOfAny(['*', '?', '[', ']', '"', '\'', '`']) >= 0) return arg;
		bool unc = arg.Length > 2 && arg[2] != '/' && arg.IndexOf('/', 2) > 0;
		return (unc ? arg : arg[1..]).Replace('\\', '/');
		}

	/// <summary>The path's form only (`~`, `/c/x`, `/tmp`, slashes), never resolved against a cwd: for a path that
	/// is relative to something else, such as a symlink's target (relative to the link's folder).</summary>
	internal static string TranslateForm(string path)
		{
		if (path.Length == 0) return path;

		// A `~` here is a name, as in bash: the shell expands an unquoted one before (TildeHome). Until 2026-10-19 it was
		// the profile, whatever $HOME said: `d='~'; mkdir "$d"` said "File exists", `echo x > "$d/f"` wrote into the
		// profile, and `rm -rf "$d"` would have aimed at it (red team F5).

		if (Path.DirectorySeparatorChar != '\\') return path;

		if (path.StartsWith("/dev/", StringComparison.Ordinal)) return path;

		// /c, /c/, /c/Users/...  (drive letter, any case) → C:/Users/...
		if (path.Length >= 2 && path[0] == '/' && char.IsAsciiLetter(path[1])
		    && (path.Length == 2 || path[2] is '/' or '\\'))
			{
			var drive = char.ToUpperInvariant(path[1]) + ":/";
			return path.Length <= 3 ? drive : drive + ToShellPath(path[3..]);
			}

		// /tmp, /tmp/...
		if (path == "/tmp" || path.StartsWith("/tmp/", StringComparison.Ordinal) || path.StartsWith("/tmp\\", StringComparison.Ordinal))
			{
			var temp = ToShellPath(Path.TrimEndingDirectorySeparator(Path.GetTempPath()));
			return path.Length == 4 ? temp : temp + "/" + ToShellPath(path[5..]);
			}

		return ToShellPath(path);
		}

	/// <summary>
	/// Convert a native Windows path into the forward-slash form the SHELL LANGUAGE is safe with,
	/// for any path the shell hands back to a script (`pwd`, `$PWD`, `dirs`, `mktemp -d`,
	/// `realpath`, `readlink`). Ratified 2026-09-11 by the architect, refining decision 3 of
	/// 2026-09-04 (which said "native Windows form" on the strength of Claude Code's cwd
	/// read-back alone):
	///
	/// backslash is an ESCAPE CHARACTER in bash, so `PWD=E:\Claude\Installer` is a value the
	/// shell's own operations misread — `${p#pat}`, globs, `[[ ]]`, `case` and every later
	/// expansion treat those backslashes as escapes. `E:/Claude/Installer` is equally valid to
	/// every Windows API and native tool, and contains no escape characters, so it satisfies both
	/// sides. `\\host\share\x` becomes `//host/share/x` (the leading pair is preserved by the
	/// straight character swap), and a bare root `E:\` becomes `E:/`.
	/// </summary>
	public static string ToShellPath(string path) => path.Replace('\\', '/');

	/// <summary>`~` as bash expands it: the value of HOME (<paramref name="home"/>), or the profile when it is unset or
	/// empty (bash takes the account's home directory then), in shell form.</summary>
	public static string TildeHome(string home) =>
		ToShellPath(home.Length > 0 ? home : Environment.GetFolderPath(Environment.SpecialFolder.UserProfile));

	/// <summary>`Path.GetFullPath` in shell form — .NET hands back backslashes whatever it was
	/// given, so every absolute path the shell computes is normalized here (2026-09-12).</summary>
	public static string FullPath(string path) => ToShellPath(Path.GetFullPath(path));

	/// <summary>The working directory in shell form — what `pwd` prints and `$PWD` holds: the
	/// running shell's own cwd.</summary>
	public static string ShellCwd() => t_active?.Cwd ?? ToShellPath(Directory.GetCurrentDirectory());

	// ── working directory ─────────────────────────────────────────────────────

	/// <summary>
	/// This shell's own working directory, in shell form. The shell itself (<see cref="OwnsProcessCwd"/>)
	/// keeps the PROCESS cwd equal to it, so everything outside pipelines behaves exactly as before.
	/// A pipeline stage's copy never touches the process cwd: a stage's `cd` used to move every
	/// stage and the parent -- `(cd sub &amp;&amp; …) | tee out.log` put out.log in sub/ in 148 of 200
	/// runs, and `cd sub | true` moved the shell (DECISIONS 2026-10-03).
	/// </summary>
	public string Cwd { get; private set; } = "";

	/// <summary>The `umask` builtin's file-creation mask, as permission bits (default 0022). Stored and
	/// printed only: Windows files have no mode bits for it to clear (DECISIONS 2026-10-10, umask).</summary>
	public int Umask { get; set; } = 0b000_010_010;

	/// <summary>True for a shell whose cwd IS the process cwd; false for a pipeline stage's copy.</summary>
	public bool OwnsProcessCwd { get; private set; }

	/// <summary>A shell whose owner keeps running (a `&` job, `setsid -f`): the process cwd can move under it at
	/// any moment, so its relative paths always resolve against its own <see cref="Cwd"/>. They resolved against
	/// the process cwd until the job moved: after the parent's `cd`, a job's `rm -f victim` deleted the parent's
	/// victim (2026-10-17). A pipeline stage is not detached: its parent waits for it.</summary>
	public bool Detached { get; private set; }

	/// <summary>Mark this (copied) shell as one whose owner keeps running.</summary>
	public void Detach() => Detached = true;

	[ThreadStatic] private static ShellEnvironment? t_active;

	/// <summary>The environment whose commands this thread is running. Set at every entry where a
	/// thread starts running shell code (RunString, pipeline stages, background jobs, `timeout`).</summary>
	public static ShellEnvironment? Active { get => t_active; set => t_active = value; }

	/// <summary>The PATH command lookups search: the running shell's own PATH variable (a subshell's or a
	/// stage's is its own). They read the process environment until 2026-10-11, which a temporary
	/// `PATH=… cmd` never reaches and a `( export PATH=… )` changed for the parent too.</summary>
	public static string CurrentPath => t_active?.Get("PATH") ?? System.Environment.GetEnvironmentVariable("PATH") ?? "";

	/// <summary>Called on every assignment to PATH: bash resets its command hash table then.</summary>
	public Action? PathChanged { get; set; }

	/// <summary>`cd`: <paramref name="dest"/> is a translated path (absolute when this shell is
	/// detached from the process cwd).</summary>
	public void ChangeCwd(string dest)
		{
		var full = Path.GetFullPath(dest);
		if (OwnsProcessCwd) Directory.SetCurrentDirectory(full);
		Cwd = ToShellPath(full);
		}

	/// <summary>An in-process script's child shell starts where its caller is, owning the process
	/// cwd only if the caller did.</summary>
	public void AdoptCwd(ShellEnvironment caller)
		{
		Cwd = caller.Cwd;
		OwnsProcessCwd = caller.OwnsProcessCwd;
		_baseCwd = caller.OwnsProcessCwd ? caller.Cwd : caller._baseCwd;
		Detached = caller.Detached;   // a script run inside a job
		}

	/// <summary>Put the process cwd back to this (owning) shell's cwd, after a child that also owned
	/// it may have moved it: a script's `cd` does not move its caller.</summary>
	public void ReassertProcessCwd()
		{
		if (!OwnsProcessCwd) return;
		try { if (ToShellPath(Directory.GetCurrentDirectory()) != Cwd && Directory.Exists(Cwd)) Directory.SetCurrentDirectory(Cwd); }
		catch { }
		}

	// ── scalar variables ──────────────────────────────────────────────────────

	public string Get(string name)
		{
		// An ordinary lower-case/underscore name is never a special or dynamic parameter and
		// never a positional: straight to the frames. Measured 2026-09-04 (A/B, 3 runs each):
		// loop 425–435 vs 431–455 ns/it — marginal, ~10 ns, kept because it is free and exact.
		if (name.Length > 0 && ((name[0] >= 'a' && name[0] <= 'z') || name[0] == '_')) goto frames;
		switch (name)
			{
			case "?":  return LastExitCode.ToString();
			case "$":  return Environment.ProcessId.ToString();
			case "!":  return LastBackgroundPid == 0 ? "" : LastBackgroundPid.ToString();
			case "-":  return FlagsProvider?.Invoke() ?? "hB";
			case "#":  return (_positionals.TryPeek(out var p) ? p.Length : 0).ToString();
			case "@":  return string.Join(" ", _positionals.TryPeek(out var pa) ? pa : []);
			case "*":  return string.Join(StarSeparator, _positionals.TryPeek(out var ps) ? ps : []);
			case "0":  return _arg0;
			case "RANDOM":  return _random.Next(0, 32768).ToString();
			case "SRANDOM": return ((uint)_random.NextInt64()).ToString();
			case "SECONDS": return ((long)(DateTime.UtcNow - _start).TotalSeconds).ToString();
			case "LINENO":  return CurrentLine.ToString();
			case "BASH_COMMAND": return CommandTextProvider?.Invoke() ?? "";
			case "BASHPID": return Environment.ProcessId.ToString();
			case "EPOCHSECONDS":  return DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();
			case "EPOCHREALTIME":   // microseconds, as bash (it was milliseconds / 1000.0: the last three digits 000, 2026-10-17)
				{
				long us = (DateTime.UtcNow.Ticks - DateTime.UnixEpoch.Ticks) / 10;
				return (us / 1_000_000).ToString(System.Globalization.CultureInfo.InvariantCulture) + "."
				     + (us % 1_000_000).ToString("D6", System.Globalization.CultureInfo.InvariantCulture);
				}
			}

		if (TryPositional(name, out int idx))
			{
			var pos = _positionals.TryPeek(out var pp) ? pp : [];
			return idx > 0 && idx <= pos.Length ? pos[idx - 1] : "";
			}

		frames:
		var v = Find(name);
		if (v is null) return "";
		if (v.Value is not null) return v.Value;
		// an array's `$a` is its element 0 / "0" (bash)
		if (v.Indexed is not null) return v.Indexed.TryGetValue(0, out var e0) ? e0 : "";
		if (v.Assoc is not null) return v.Assoc.TryGetValue("0", out var k0) ? k0 : "";
		return "";   // declared with no value (`local x`): it hides an outer x but is unset
		}

	// ToLowerInvariant without TextInfo, which loads ICU (~0.7 ms) on every start; a non-ASCII string still uses it
	private static string LowerInvariant(string s)
		{
		if (!System.Text.Ascii.IsValid(s)) return s.ToLowerInvariant();
		var chars = new char[s.Length];
		System.Text.Ascii.ToLower(s, chars, out _);
		return new string(chars);
		}

	// `$1`, `$12`: digits only. Not int.TryParse(name): that parses through the current culture, which loads ICU
	// (~0.7 ms) at start-up and runs on every read of an upper-case name (2026-10-18, review R4).
	private static bool TryPositional(string name, out int idx) =>
		int.TryParse(name, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out idx);

	/// <summary>What joins `$*` and `${a[*]}` where they make one string ("$*", an assignment, `[[ ]]`,
	/// a here-string, `case`): IFS's first character, nothing when IFS is empty, a space when it is
	/// unset (bash). A space whatever IFS held until 2026-10-12.</summary>
	public string StarSeparator => !IsSet("IFS") ? " " : Get("IFS") is { Length: > 0 } ifs ? ifs[..1] : "";

	/// <summary>True if the scalar is set (for `[[ -v x ]]` / `${x+y}`). A name declared with no value
	/// is unset; an array or associative array is set when its element 0 / "0" is, as in bash.</summary>
	public bool IsSet(string name)
		{
		if (name.Length == 1 && "?$!-#@*0".IndexOf(name[0]) >= 0) return true;
		if (name is "RANDOM" or "SECONDS" or "LINENO" or "BASHPID" or "EPOCHSECONDS" or "EPOCHREALTIME" or "SRANDOM" or "BASH_COMMAND") return true;
		if (TryPositional(name, out int idx))
			{
			var pos = _positionals.TryPeek(out var pp) ? pp : [];
			return idx > 0 && idx <= pos.Length;
			}
		var v = Find(name);
		if (v is null) return false;
		if (v.Indexed is not null) return v.Indexed.ContainsKey(0);
		if (v.Assoc is not null) return v.Assoc.ContainsKey("0");
		return v.Value is not null;
		}

	/// <summary>Declared in some scope, with or without a value (`declare -p x`, `unset x`).</summary>
	public bool IsDeclared(string name) => Find(name) is not null;

	/// <summary>`local x` with no value: x is declared in the innermost scope and unset, hiding an
	/// outer x. An x already declared in that scope keeps its value, as in bash; a readonly outer x
	/// cannot be shadowed.</summary>
	public void DeclareLocal(string name)
		{
		if (_frames[^1].ContainsKey(name)) return;
		if (Find(name) is { Readonly: true }) throw new FatalShellException($"{name}: readonly variable");
		_frames[^1][name] = new Var();
		}

	/// <summary>True when the innermost variable of that name is declared with no value (`local x`).</summary>
	public bool IsDeclaredUnset(string name) => Find(name) is { IsUnset: true };

	/// <summary>Make the innermost variable of that name declared-with-no-value again (restoring `x=v cmd`
	/// on a `local x`, where an unset would also remove it).</summary>
	public void RestoreDeclaredUnset(string name)
		{
		if (Find(name) is { } v) { v.Value = null; v.Indexed = null; v.Assoc = null; }
		}

	// PATH has two forms (the architect, 2026-10-31: "two paths is the right thing"; DECISIONS "GO: $PATH has two
	// forms"). INSIDE the shell, $PATH is GNU/MSYS form, `/c/x:/f/y`, so `IFS=:` and the other POSIX idioms work; the
	// colon forces the MSYS root, since `C:/x` holds one. At every EXIT (this process's environment, a child's) it is
	// Windows form with forward slashes, `C:/x;F:/y`: never a backslash (eliminating it is why C#Bash exists), never an
	// empty entry (Windows takes one as the current directory). Until then $PATH read in Windows form, `;`-separated.

	/// <summary>Any PATH value in the shell's form, `/c/x:/f/y`: Windows (`C:\x;F:\y`, either slash), MSYS, or both
	/// mixed (the POSIX idioms PATH="$PATH:DIR" and PATH="DIR:$PATH" with a Windows DIR or list). Empty entries are
	/// kept, as given (RULED 2026-10-17); an entry holding a newline (a profile's echo leaking into a captured PATH)
	/// is dropped.</summary>
	internal static string NormalizePath(string value)
		{
		var entries = PathDirs(value, keepEmpty: true);
		var sb = new System.Text.StringBuilder(value.Length);
		bool first = true;
		foreach (var e in entries)
			{
			if (e.Contains('\n')) continue;
			if (!first) sb.Append(':');
			sb.Append(ToMsysEntry(e));
			first = false;
			}
		return sb.ToString();
		}

	/// <summary>The entries of a PATH in either form, or both mixed, each as written: split at every `;` and at every
	/// colon that is not a drive's (a letter starting the piece, then `/`, `\` or the end). Empty entries only on
	/// request: no lookup searches one (Windows would take it as the current directory).</summary>
	internal static List<string> PathDirs(string value, bool keepEmpty = false)
		{
		var dirs = new List<string>();
		foreach (var entry in value.Split(';'))
			foreach (var p in SplitPathColons(entry))
				if (keepEmpty || p.Length > 0) dirs.Add(p);
		return dirs;
		}

	/// <summary>One PATH entry in the shell's form: `C:\x\` or `C:/x` as `/c/x`, `C:` as `/c`; `\\host\share` as
	/// `//host/share`; MSYS and relative entries as written, slashes forward; a trailing slash dropped. A one-letter
	/// relative entry is written `./A`: `A:/c/x` reads as drive A's `/c/x` (a drive must win there: the shell's own
	/// paths are `C:/x`, as in PATH="$HOME/bin:$PATH"), so `A;C:/x` would have come back as one entry.</summary>
	internal static string ToMsysEntry(string e)
		{
		if (e.Length >= 2 && char.IsAsciiLetter(e[0]) && e[1] == ':' && (e.Length == 2 || e[2] is '/' or '\\'))
			{
			var rest = e[2..].Replace('\\', '/').TrimEnd('/');
			return "/" + char.ToLowerInvariant(e[0]) + rest;
			}
		var fwd = e.Replace('\\', '/');
		fwd = fwd.Length > 1 ? fwd.TrimEnd('/') : fwd;
		return fwd.Length == 1 && char.IsAsciiLetter(fwd[0]) ? "./" + fwd : fwd;
		}

	/// <summary>The PATH a Windows program is given, from a PATH in either form: `C:/x;F:/y`, each entry's form
	/// translated (<see cref="TranslateForm"/>: forward slashes, `/c/x` as `C:/x`), empty entries left out.</summary>
	internal static string ToWindowsPathList(string value) =>
		string.Join(";", PathDirs(value).Select(TranslateForm));

	private static List<string> SplitPathColons(string entry)
		{
		var pieces = new List<string>();
		int start = 0;
		for (int i = 0; i < entry.Length; i++)
			{
			if (entry[i] != ':') continue;
			if (i - start == 1 && char.IsAsciiLetter(entry[start]) && (i + 1 == entry.Length || entry[i + 1] is '/' or '\\')) continue;   // a drive's
			pieces.Add(entry[start..i]);
			start = i + 1;
			}
		pieces.Add(entry[start..]);
		return pieces;
		}

	/// <summary>An assignment: to the innermost variable of that name, or to a new GLOBAL one (bash: a
	/// function's `x=1` outlives the call).</summary>
	public void Set(string name, string value) => Assign(Target(name), name, value);

	/// <summary>Give a variable a value: an integer variable evaluates it; an array takes it as its element
	/// 0 / "0" (bash); PATH reaches the process environment and resets the hash.</summary>
	private void Assign(Var v, string name, string value)
		{
		CheckWritable(v, name);
		if (v.Integer && ArithEval is not null)
			{
			try { value = ArithEval(value).ToString(); } catch (EvalException) { value = "0"; }
			}
		if (name == "PATH") value = NormalizePath(value);
		if (v.Indexed is not null) v.Indexed[0] = value;
		else if (v.Assoc is not null) v.Assoc["0"] = value;
		else v.Value = value;
		if (AutoExport) v.Exported = true;
		if (name == "PATH")
			{
			// the process environment carries PATH too (CurrentPath reads it where no shell is active), in the exit
			// form: forward-slash Windows, without empty entries (Windows' launcher takes one as the cwd, 2026-10-17)
			try { Environment.SetEnvironmentVariable("PATH", ToWindowsPathList(value) is { Length: > 0 } w ? w : null); } catch { }
			PathChanged?.Invoke();   // the hash table is reset, as bash's
			}
		}

	/// <summary>`local x=v`: x in the innermost scope, declared there first if it is not yet (one lookup:
	/// this is every function call's `local`).</summary>
	public void SetLocal(string name, string value)
		{
		var f = _frames[^1];
		if (!f.TryGetValue(name, out var v))
			{
			if (_frames.Count > 1 && Find(name) is { Readonly: true }) throw new FatalShellException($"{name}: readonly variable");
			v = new Var();
			f[name] = v;
			}
		Assign(v, name, value);
		}

	/// <summary>In the global scope (`declare -g`).</summary>
	public void SetGlobal(string name, string value)
		{
		if (!_frames[0].TryGetValue(name, out var v)) { v = new Var(); _frames[0][name] = v; }
		Assign(v, name, value);
		}

	/// <summary>Append to a scalar (var+=value); integers add arithmetically.</summary>
	public void Append(string name, string value)
		{
		if (IsInteger(name) && ArithEval is not null)
			{
			long cur = long.TryParse(Get(name), System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var c) ? c : 0;
			long add; try { add = ArithEval(value); } catch (EvalException) { add = 0; }
			Set(name, (cur + add).ToString());
			return;
			}
		Set(name, Get(name) + value);
		}

	/// <summary>Unset a variable or array; false (and nothing done) for a readonly one. In the function scope
	/// that declared it, it stays declared and unset, still hiding an outer one; anywhere else (a caller's
	/// local unset by a callee, a global, the top level) it goes, uncovering the next one out (bash 5.1,
	/// measured 2026-10-12).</summary>
	public bool Unset(string name)
		{
		for (int i = _frames.Count - 1; i >= 0; i--)
			{
			if (!_frames[i].TryGetValue(name, out var v)) continue;
			if (v.Readonly) return false;
			if (i == _frames.Count - 1 && i > 0) { v.Value = null; v.Indexed = null; v.Assoc = null; v.Exported = false; v.Integer = false; }
			else _frames[i].Remove(name);
			return true;
			}
		return true;
		}

	public void Export(string name) => Target(name).Exported = true;
	/// <summary>Every visible exported name, set or not (`export -p` lists `declare -x x` for an unset x).</summary>
	public IEnumerable<string> ExportedNames => [.. Visible().Where(p => p.V.Exported).Select(p => p.Name)];
	public void Unexport(string name) { if (Find(name) is { } v) v.Exported = false; }
	public bool IsExported(string name) => Find(name) is { Exported: true };

	public void MarkReadonly(string name) => Target(name).Readonly = true;
	public bool IsReadonly(string name) => Find(name) is { Readonly: true };
	public IEnumerable<string> ReadonlyNames => [.. Visible().Where(p => p.V.Readonly).Select(p => p.Name)];

	public void MarkInteger(string name, bool on)
		{
		if (on) Target(name).Integer = true;
		else if (Find(name) is { } v) v.Integer = false;
		}
	public bool IsInteger(string name) => Find(name) is { Integer: true };

	/// <summary>Each visible variable once: the innermost of its name.</summary>
	private List<(string Name, Var V)> Visible()
		{
		if (CallStackStale) RefreshCallStackNow();
		var seen = new HashSet<string>(StringComparer.Ordinal);
		var list = new List<(string, Var)>();
		for (int i = _frames.Count - 1; i >= 0; i--)
			foreach (var kv in _frames[i])
				if (seen.Add(kv.Key)) list.Add((kv.Key, kv.Value));
		return list;
		}

	/// <summary>Every visible variable's name, scalars and arrays (tab completion, `set`, `${!prefix*}`).</summary>
	public IEnumerable<string> VariableNames() => [.. Visible().Select(p => p.Name)];

	/// <summary>Every visible variable that holds something: a value, or an array with elements (`compgen -v`; a
	/// name declared with no value, or an empty array, is left out, as bash 5.1 does).</summary>
	public IEnumerable<string> VariablesWithValues() => [.. Visible().Where(p => p.V.Value is not null || p.V.Indexed is { Count: > 0 } || p.V.Assoc is { Count: > 0 }).Select(p => p.Name)];

	// ── indexed arrays ────────────────────────────────────────────────────────

	private SortedDictionary<int, string>? IndexedOf(string name) => Find(name)?.Indexed;
	private Dictionary<string, string>? AssocOf(string name) => Find(name)?.Assoc;

	/// <summary>The variable an array assignment changes, made an indexed array: a scalar's value becomes
	/// element 0 (bash: `x=a; x[1]=b` is `(a b)`).</summary>
	private SortedDictionary<int, string> IndexedTarget(string name)
		{
		var v = Target(name);
		CheckWritable(v, name);
		if (v.Indexed is null)
			{
			v.Indexed = new SortedDictionary<int, string>();
			if (v.Value is not null) v.Indexed[0] = v.Value;
			v.Value = null; v.Assoc = null;
			}
		return v.Indexed;
		}

	public bool IsArray(string name) => IndexedOf(name) is not null;

	public void DeclareArray(string name)
		{
		var v = Target(name);
		if (v.Indexed is null && v.Assoc is null) IndexedTarget(name);
		}

	public void SetArrayElement(string name, int index, string value)
		{
		var arr = IndexedTarget(name);
		if (index < 0)
			{
			int max = arr.Count > 0 ? arr.Keys.Max() : -1;
			index = max + 1 + index;
			if (index < 0) throw new EvalException($"{name}[{index}]: bad array subscript");
			}
		if (Find(name) is { Integer: true } && ArithEval is not null)
			{
			try { value = ArithEval(value).ToString(); } catch (EvalException) { value = "0"; }
			}
		arr[index] = value;
		}

	/// <summary>`a[i]+=v` (and `declare a[i]+=v`): added under `declare -i`, else joined, as `x+=v`; a scalar is
	/// element 0 (bash: `s=abc; s[0]+=x` makes abcx). Until 2026-10-16 `a[i]+=v` was not an assignment at all.</summary>
	public void AppendArrayElement(string name, int index, string value) =>
		SetArrayElement(name, index, Appended(name, IndexedOf(name) is null && index == 0 ? Get(name) : GetArrayElement(name, index), value));

	/// <summary>`m[k]+=v` on an associative array: as <see cref="AppendArrayElement"/>.</summary>
	public void AppendAssocElement(string name, string key, string value) =>
		SetAssocElement(name, key, Appended(name, GetAssocElement(name, key), value));

	private string Appended(string name, string current, string value)
		{
		if (!IsInteger(name) || ArithEval is null) return current + value;
		long cur = long.TryParse(current, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var c) ? c : 0;
		long add; try { add = ArithEval(value); } catch (EvalException) { add = 0; }
		return (cur + add).ToString();
		}

	public string GetArrayElement(string name, int index)
		{
		if (IndexedOf(name) is { } arr)
			{
			if (index < 0 && arr.Count > 0) index = arr.Keys.Max() + 1 + index;
			if (arr.TryGetValue(index, out var val)) return val;
			}
		return "";
		}

	public bool HasArrayElement(string name, int index)
		{
		if (IndexedOf(name) is not { } arr) return false;
		if (index < 0 && arr.Count > 0) index = arr.Keys.Max() + 1 + index;
		return arr.ContainsKey(index);
		}

	public void UnsetArrayElement(string name, int index)
		{
		if (IndexedOf(name) is { } arr)
			{
			if (index < 0 && arr.Count > 0) index = arr.Keys.Max() + 1 + index;
			arr.Remove(index);
			}
		}

	/// <summary>All values in index order.</summary>
	public List<string> GetArrayValues(string name) => IndexedOf(name) is { } arr ? [.. arr.Values] : [];

	/// <summary>All indices as strings.</summary>
	public List<string> GetArrayKeys(string name) => IndexedOf(name) is { } arr ? arr.Keys.Select(k => k.ToString()).ToList() : [];

	/// <summary>Index/value pairs in order (for `declare -p`).</summary>
	public IEnumerable<KeyValuePair<int, string>> GetArrayPairs(string name) => IndexedOf(name) is { } arr ? arr : [];

	public int GetArrayLength(string name) => IndexedOf(name)?.Count ?? 0;

	/// <summary>Assign from compound list: arr=(a b c). Replaces any existing value of the variable.</summary>
	public void SetArrayFromList(string name, List<string> values)
		{
		var v = Target(name);
		CheckWritable(v, name);
		var arr = new SortedDictionary<int, string>();
		for (int i = 0; i < values.Count; i++)
			arr[i] = values[i];
		v.Indexed = arr; v.Value = null; v.Assoc = null;
		}

	/// <summary>arr+=(x y): append after the highest index (a scalar's value becomes element 0 first).</summary>
	public void AppendArrayFromList(string name, List<string> values)
		{
		var v = Target(name);
		CheckWritable(v, name);
		if (v.Assoc is not null) return;   // bash: appending a list to an assoc array requires [key]=value pairs
		var arr = IndexedTarget(name);
		int next = arr.Count > 0 ? arr.Keys.Max() + 1 : 0;
		foreach (var s in values) arr[next++] = s;
		}

	// ── associative arrays ────────────────────────────────────────────────────

	public bool IsAssoc(string name) => AssocOf(name) is not null;

	public void DeclareAssoc(string name)
		{
		var v = Target(name);
		if (v.Assoc is not null) return;
		v.Assoc = new Dictionary<string, string>(StringComparer.Ordinal);
		if (v.Value is not null) v.Assoc["0"] = v.Value;
		v.Value = null; v.Indexed = null;
		}

	public void SetAssocElement(string name, string key, string value)
		{
		var v = Target(name);
		CheckWritable(v, name);
		if (v.Assoc is null) DeclareAssoc(name);
		v.Assoc![key] = value;
		}

	public string GetAssocElement(string name, string key) =>
		AssocOf(name) is { } map && map.TryGetValue(key, out var val) ? val : "";

	public bool HasAssocElement(string name, string key) => AssocOf(name) is { } map && map.ContainsKey(key);

	public void UnsetAssocElement(string name, string key) => AssocOf(name)?.Remove(key);

	public List<string> GetAssocValues(string name) => AssocOf(name) is { } map ? [.. map.Values] : [];

	public List<string> GetAssocKeys(string name) => AssocOf(name) is { } map ? [.. map.Keys] : [];

	public IEnumerable<KeyValuePair<string, string>> GetAssocPairs(string name) => AssocOf(name) is { } map ? map : [];

	public int GetAssocLength(string name) => AssocOf(name)?.Count ?? 0;

	// ── scopes ────────────────────────────────────────────────────────────────

	/// <summary>Set $0 (script name / shell name).</summary>
	public void SetArg0(string value) => _arg0 = value;

	/// <summary>Current positional parameters $1..$N (for "$@"/"$*" field expansion).</summary>
	public List<string> GetPositionals() => _positionals.TryPeek(out var p) ? [.. p] : [];

	/// <summary>Replace the current (top-level) positional parameters $1..$N.</summary>
	public void SetPositionals(IEnumerable<string> positionals)
		{
		if (_positionals.Count > 0) _positionals.Pop();
		_positionals.Push(positionals.ToArray());
		}

	public void PushScope(string[] positionals)
		{
		_frames.Add(new Dictionary<string, Var>(StringComparer.Ordinal));
		_positionals.Push(positionals);
		}

	public void PopScope()
		{
		if (_frames.Count > 1) _frames.RemoveAt(_frames.Count - 1);
		if (_positionals.Count > 0) _positionals.Pop();
		}

	public int ScopeDepth => _frames.Count;

	// ── snapshot / restore (subshell isolation) ───────────────────────────────

	public sealed class Snapshot
		{
		internal List<Dictionary<string, Var>> Frames = [];
		internal string[][] Positionals = [];
		internal string Arg0 = "";
		internal string Cwd = "";
		internal int Umask;
		}

	/// <summary>A deep copy of the scopes: every variable cloned, arrays and attributes with it.</summary>
	private static List<Dictionary<string, Var>> CopyFrames(List<Dictionary<string, Var>> frames)
		{
		var copy = new List<Dictionary<string, Var>>(frames.Count);
		foreach (var f in frames)
			{
			var d = new Dictionary<string, Var>(f.Count, StringComparer.Ordinal);
			foreach (var kv in f) d[kv.Key] = kv.Value.Clone();
			copy.Add(d);
			}
		return copy;
		}

	/// <summary>Deep-copy the variable state (for `( )` / `$( )` isolation).</summary>
	public Snapshot TakeSnapshot()
		{
		if (CallStackStale) RefreshCallStackNow();
		var s = new Snapshot
			{
			Frames    = CopyFrames(_frames),
			Positionals = _positionals.ToArray(),
			Arg0      = _arg0,
			Cwd       = this.Cwd,
			Umask     = this.Umask,
			};
		return s;
		}

	/// <summary>Put the state back. A snapshot is restored once (a subshell's or a substitution's `finally`), so
	/// its own copy is taken over rather than copied again: one deep copy per `$( )`, not two.</summary>
	public void RestoreSnapshot(Snapshot s)
		{
		_frames.Clear(); _frames.AddRange(s.Frames);
		s.Frames = [];   // spent: restoring it twice would share variables between two states
		_positionals.Clear();
		foreach (var p in s.Positionals.Reverse()) _positionals.Push(p);
		_arg0 = s.Arg0;
		Umask = s.Umask;        // `( umask 077 )` leaves the parent's mask alone
		Cwd = s.Cwd;            // `( cd x )` / `$( cd x )` end where they started
		ReassertProcessCwd();   // the process cwd too, for the shell that owns it
		}

	// ── exports ───────────────────────────────────────────────────────────────

	public Dictionary<string, string> GetExportedVars()
		{
		// only exported names with a value: `export x` on an unset x, or `local -x x`, puts nothing in a
		// child's environment (bash); until 2026-10-08 the child got x set to ""
		// the visible variable of each name; a local `-x` one while its function runs (bash); arrays are not
		// exported
		var result = new Dictionary<string, string>(StringComparer.Ordinal);
		foreach (var (name, v) in Visible())
			if (v.Exported && v.Value is not null) result[name] = v.Value;
		return result;
		}

	// ── platform identity ─────────────────────────────────────────────────────

	/// <summary>MSYS-style uid: 0x30000 + the account's RID (so `id -u` agrees with Git Bash).</summary>
	/// <summary>
	/// This executable's own directory FIRST on the shell's own PATH, so a nested `bash -c …`, `bash script.sh`, `sh`
	/// (and the `sh` a program it starts, make, looks up) is this shell: never WSL's launcher, which a default Windows
	/// PATH puts first (System32), nor a Git bash ahead of it. Only this process's PATH and its children's change; the
	/// system and user PATH are untouched (the architect, 2026-11-03: "we still need ~/.bash/bin to be first on the
	/// path?", "Yes, do it"). The installed folder holds only Bash.exe, sh.exe and claude.cmd, so nothing else is
	/// shadowed; the folder found later on PATH (the installer's "yes") moves to the front, not listed twice. It was
	/// appended at the END "so a real Git bash still wins when present" (a machine without Git needed it at all).
	/// </summary>
	private void PrependOwnDirectoryToPath()
		{
		string dir;
		try { dir = AppContext.BaseDirectory.TrimEnd('\\', '/'); } catch { return; }
		if (dir.Length == 0) return;
		static string Key(string p) => p.Replace('/', '\\').TrimEnd('\\');
		var path = Get("PATH");
		var raw = PathDirs(path, keepEmpty: true);
		bool Mine(string e) => string.Equals(Key(TranslateForm(e)), Key(dir), StringComparison.OrdinalIgnoreCase);   // `/c/x` as `C:/x`
		if (raw.Count > 0 && Mine(raw[0])) return;
		// the rest keeps its own form and its empty entries; NormalizePath reads the `;` mix
		var rest = string.Join(":", raw.Where(e => !Mine(e))).TrimEnd(':');   // a trailing empty entry dropped, as before
		Set("PATH", rest.Length == 0 ? dir : dir + ";" + rest);
		}

	private static long WindowsUid()
		{
		try
			{
			if (!OperatingSystem.IsWindows()) return 1000;
			// From the SID's binary form, not its text: SecurityIdentifier.Value upper-cases through TextInfo, which
			// loads ICU (~0.7 ms) on every start (2026-10-18). Revision, sub-authority count n, a 6-byte big-endian
			// authority, then n little-endian 32-bit sub-authorities; the RID is the last.
			var user = System.Security.Principal.WindowsIdentity.GetCurrent().User;
			if (user is null) return 1000;
			var b = new byte[user.BinaryLength];
			user.GetBinaryForm(b, 0);
			int n = b[1];
			uint Sub(int i) => System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(b.AsSpan(8 + 4 * i));
			if (n > 0)
				{
				long rid = Sub(n - 1);
				bool s1521 = n > 1 && b[2] == 0 && b[3] == 0 && b[4] == 0 && b[5] == 0 && b[6] == 0 && b[7] == 5 && Sub(0) == 21;
				return s1521 ? 0x30000 + rid : 0x10000 + rid;   // S-1-5-21-…: a local or domain account
				}
			}
		catch { }
		return 1000;
		}

	[StructLayout(LayoutKind.Sequential)]
	private struct ProcessBasicInformation
		{
		public IntPtr Reserved1, PebBaseAddress, Reserved2a, Reserved2b, UniqueProcessId, InheritedFromUniqueProcessId;
		}

	[DllImport("ntdll.dll")]
	private static extern int NtQueryInformationProcess(IntPtr processHandle, int processInformationClass,
		ref ProcessBasicInformation processInformation, int processInformationLength, out int returnLength);

	private static long ParentPid()
		{
		try
			{
			if (!OperatingSystem.IsWindows()) return 1;
			var pbi = new ProcessBasicInformation();
			// the current-process pseudo-handle: Process.GetCurrentProcess() cost ~0.45 ms a start only to supply it
			// (the timing switch's split, 2026-10-18)
			int rc = NtQueryInformationProcess(new IntPtr(-1), 0, ref pbi, Marshal.SizeOf<ProcessBasicInformation>(), out _);
			if (rc == 0) return pbi.InheritedFromUniqueProcessId.ToInt64();
			}
		catch { }
		return 1;
		}
	}
