using System.Text;

namespace Bash.Evaluator;

/// <summary>
/// `find` with a real expression engine, `diff` with unified/recursive output, and `xargs`
/// with GNU option vocabulary. Strict option parsing throughout.
/// </summary>
public sealed partial class Builtins
	{
	// ═══════════════════════════════════════════════════════════════════════════
	// find
	// ═══════════════════════════════════════════════════════════════════════════

	private sealed class FindQuit : Exception;

	private sealed class FindEntry
		{
		public string Display = "";       // path as printed (start point + /relative)
		public string Fs = "";            // filesystem path
		public FileSystemInfo Info = null!;
		public int Depth;
		public string Start = "";         // the starting point this entry belongs to
		public string Given = "";         // a starting point as written (depth 0 only): -execdir's directory and {}
		public bool IsDir => Info is DirectoryInfo;
		public bool IsLink => Info.LinkTarget is not null;
		public long Size => Info is FileInfo fi ? fi.Length : 0;
		}

	private sealed class FindState
		{
		public bool Prune, Quit, AnyAction;
		public int MinDepth = 0, MaxDepth = int.MaxValue;
		public bool PostOrder, FollowLinks;
		public int Rc;
		public List<string> ExecPlusBatch = [];
		public List<string>? ExecPlusCmd;
		public string? ExecPlusDir;   // -execdir: the directory the batch runs from
		public bool ExecDir;          // the expression has -execdir: PATH must be safe for it
		public DateTime Now = DateTime.Now;
		}

	private abstract class FindNode { public abstract bool Eval(FindEntry e, FindState st, Builtins b); }
	private sealed class FindAnd(FindNode l, FindNode r) : FindNode { public override bool Eval(FindEntry e, FindState st, Builtins b) => l.Eval(e, st, b) && r.Eval(e, st, b); }
	private sealed class FindOr(FindNode l, FindNode r) : FindNode { public override bool Eval(FindEntry e, FindState st, Builtins b) => l.Eval(e, st, b) || r.Eval(e, st, b); }
	private sealed class FindNot(FindNode x) : FindNode { public override bool Eval(FindEntry e, FindState st, Builtins b) => !x.Eval(e, st, b); }
	private sealed class FindPred(Func<FindEntry, FindState, Builtins, bool> f) : FindNode { public override bool Eval(FindEntry e, FindState st, Builtins b) => f(e, st, b); }

	private int Find(List<string> args)
		{
		var st = new FindState();
		var paths = new List<string>();
		int i = 0;
		// global options may precede paths: -L -H -P -O -D
		for (; i < args.Count; i++)
			{
			if (args[i] == "-L") st.FollowLinks = true;
			else if (args[i] is "-H" or "-P") { }
			else if (args[i].StartsWith("-O", StringComparison.Ordinal)) { }
			else break;
			}
		for (; i < args.Count && !args[i].StartsWith('-') && args[i] != "!" && args[i] != "(" && args[i] != ","; i++)
			paths.Add(args[i]);
		if (paths.Count == 0) paths.Add(".");

		FindNode? expr;
		try { expr = FindParse(args, ref i, st); }
		catch (UnsupportedOptionException) { throw; }
		catch (Exception ex) { Console.Error.WriteLine($"find: {ex.Message}"); return 1; }
		if (i < args.Count) { Console.Error.WriteLine($"find: unexpected extra operand '{args[i]}'"); return 1; }
		if (st.ExecDir && UnsafePathForExecDir(_env.Get("PATH")) is { } unsafePath) { Console.Error.WriteLine(unsafePath); return 1; }
		if (!st.AnyAction)
			{
			// implicit -print
			var inner = expr;
			expr = new FindPred((e, s, b) => { if (inner is null || inner.Eval(e, s, b)) Console.WriteLine(e.Display); return true; });
			}

		try
			{
			foreach (var p in paths)
				{
				var fs = ShellEnvironment.TranslatePath(p);
				FileSystemInfo? info = Directory.Exists(fs) ? new DirectoryInfo(fs) : File.Exists(fs) ? new FileInfo(fs) : null;
				if (info is null) { Console.Error.WriteLine($"find: '{p}': No such file or directory"); st.Rc = 1; continue; }
				var display = p.Length > 1 ? p.TrimEnd('/', '\\') : p;
				FindWalk(new FindEntry { Display = display, Fs = fs, Info = info, Depth = 0, Start = display, Given = p }, expr!, st);
				if (st.Quit) break;
				}
			}
		catch (FindQuit) { }
		if (st.ExecPlusCmd is not null && st.ExecPlusBatch.Count > 0) FindExecPlusFlush(st);
		return st.Rc;
		}

	private void FindWalk(FindEntry e, FindNode expr, FindState st)
		{
		if (st.Quit) return;
		bool inDepth = e.Depth >= st.MinDepth && e.Depth <= st.MaxDepth;
		st.Prune = false;
		if (!st.PostOrder && inDepth) { expr.Eval(e, st, this); if (st.Quit) throw new FindQuit(); }
		bool descend = e.IsDir && !st.Prune && e.Depth < st.MaxDepth && (!e.IsLink || st.FollowLinks || e.Depth == 0);
		if (descend)
			{
			FileSystemInfo[] children;
			try { children = ((DirectoryInfo)e.Info).GetFileSystemInfos(); }
			catch (Exception ex) { Console.Error.WriteLine($"find: '{e.Display}': {IoError(ex)}"); st.Rc = 1; children = []; }
			Array.Sort(children, (a, b) => string.CompareOrdinal(a.Name, b.Name));
			foreach (var c in children)
				{
				var childDisplay = (e.Display.EndsWith('/') ? e.Display : e.Display + "/") + c.Name;
				FindWalk(new FindEntry { Display = childDisplay, Fs = c.FullName, Info = c, Depth = e.Depth + 1, Start = e.Start }, expr, st);
				if (st.Quit) return;
				}
			}
		if (st.PostOrder && inDepth) { expr.Eval(e, st, this); if (st.Quit) throw new FindQuit(); }
		}

	private static FindNode? FindParse(List<string> a, ref int i, FindState st)
		{
		if (i >= a.Count) return null;
		var node = FindParseOr(a, ref i, st);
		return node;
		}

	private static FindNode FindParseOr(List<string> a, ref int i, FindState st)
		{
		var left = FindParseAnd(a, ref i, st);
		while (i < a.Count && a[i] is "-o" or "-or")
			{
			i++;
			var right = FindParseAnd(a, ref i, st);
			left = new FindOr(left, right);
			}
		return left;
		}

	private static FindNode FindParseAnd(List<string> a, ref int i, FindState st)
		{
		var left = FindParseNot(a, ref i, st);
		while (i < a.Count && a[i] != ")" && a[i] != "-o" && a[i] != "-or" && a[i] != ",")
			{
			if (a[i] is "-a" or "-and") i++;
			var right = FindParseNot(a, ref i, st);
			left = new FindAnd(left, right);
			}
		return left;
		}

	private static FindNode FindParseNot(List<string> a, ref int i, FindState st)
		{
		if (i < a.Count && (a[i] == "!" || a[i] == "-not")) { i++; return new FindNot(FindParseNot(a, ref i, st)); }
		return FindParsePrimary(a, ref i, st);
		}

	private static FindNode FindParsePrimary(List<string> a, ref int i, FindState st)
		{
		if (i >= a.Count) throw new Exception("missing expression");
		var t = a[i++];
		// (argument fetch: FindArg)
		switch (t)
			{
			case "(":
				{
				var inner = FindParseOr(a, ref i, st);
				if (i >= a.Count || a[i] != ")") throw new Exception("invalid expression; missing ')'");
				i++;
				return inner;
				}
			case "-name": case "-iname":
				{
				var pat = FindArg(a, ref i, t); bool ic = t == "-iname";
				return new FindPred((e, s, b) => Glob.Match(pat, e.Info.Name, ic) || (e.Depth == 0 && Glob.Match(pat, e.Display, ic)));
				}
			case "-path": case "-wholename": case "-ipath": case "-iwholename":
				{
				var pat = FindArg(a, ref i, t); bool ic = t.StartsWith("-i", StringComparison.Ordinal);
				return new FindPred((e, s, b) => Glob.Match(pat, e.Display, ic) || Glob.Match(pat, e.Display.Replace('\\', '/'), ic));
				}
			case "-regex": case "-iregex":
				{
				var re = new System.Text.RegularExpressions.Regex("^(?:" + EmacsToNet(FindArg(a, ref i, t)) + ")$", t == "-iregex" ? System.Text.RegularExpressions.RegexOptions.IgnoreCase : System.Text.RegularExpressions.RegexOptions.None);
				return new FindPred((e, s, b) => re.IsMatch(e.Display));
				}
			case "-type": case "-xtype":
				{
				var types = FindArg(a, ref i, t);
				return new FindPred((e, s, b) => types.Any(c => c switch
					{
					'f' => !e.IsLink && e.Info is FileInfo,
					'd' => !e.IsLink && e.IsDir,
					'l' => e.IsLink,
					_ => false,
					}));
				}
			case "-maxdepth": st.MaxDepth = int.Parse(FindArg(a, ref i, t), System.Globalization.CultureInfo.InvariantCulture); return new FindPred((e, s, b) => true);
			case "-mindepth": st.MinDepth = int.Parse(FindArg(a, ref i, t), System.Globalization.CultureInfo.InvariantCulture); return new FindPred((e, s, b) => true);
			case "-depth": st.PostOrder = true; return new FindPred((e, s, b) => true);
			case "-follow": st.FollowLinks = true; return new FindPred((e, s, b) => true);
			case "-mount": case "-xdev": case "-noleaf": case "-daystart": case "-ignore_readdir_race": case "-noignore_readdir_race":
				return new FindPred((e, s, b) => true);
			case "-newer":
				{
				var refTime = File.GetLastWriteTimeUtc(ShellEnvironment.TranslatePath(FindArg(a, ref i, t)));
				return new FindPred((e, s, b) => e.Info.LastWriteTimeUtc > refTime);
				}
			case "-newermt": case "-newerct": case "-newerat":
				{
				var spec = FindArg(a, ref i, t);
				if (!TryParseDate(spec, out var when)) throw new Exception($"I cannot figure out how to interpret '{spec}' as a date or time");
				return new FindPred((e, s, b) => e.Info.LastWriteTime > when);
				}
			case "-mtime": case "-ctime": case "-atime":
				{
				var (cmp, n) = FindNumArg(FindArg(a, ref i, t));
				return new FindPred((e, s, b) => FindCompare((long)Math.Floor((s.Now - (t == "-atime" ? e.Info.LastAccessTime : e.Info.LastWriteTime)).TotalDays), cmp, n));
				}
			case "-mmin": case "-cmin": case "-amin":
				{
				var (cmp, n) = FindNumArg(FindArg(a, ref i, t));
				return new FindPred((e, s, b) => FindCompare((long)Math.Floor((s.Now - (t == "-amin" ? e.Info.LastAccessTime : e.Info.LastWriteTime)).TotalMinutes), cmp, n));
				}
			case "-size":
				{
				var spec = FindArg(a, ref i, t);
				char unit = spec.Length > 0 && char.IsLetter(spec[^1]) ? spec[^1] : 'b';
				var (cmp, n) = FindNumArg(char.IsLetter(spec[^1]) ? spec[..^1] : spec);
				long div = unit switch { 'c' => 1, 'w' => 2, 'b' => 512, 'k' => 1024, 'M' => 1024L * 1024, 'G' => 1024L * 1024 * 1024, _ => throw new Exception($"invalid -size type `{unit}'") };
				return new FindPred((e, s, b) => FindCompare((e.Size + div - 1) / div, cmp, n));
				}
			case "-empty":
				return new FindPred((e, s, b) => e.IsDir ? !((DirectoryInfo)e.Info).EnumerateFileSystemInfos().Any() : e.Size == 0);
			case "-readable": return new FindPred((e, s, b) => true);
			case "-writable": return new FindPred((e, s, b) => e.IsDir || (e.Info.Attributes & FileAttributes.ReadOnly) == 0);
			case "-executable": return new FindPred((e, s, b) => e.IsDir || FileTests.IsExecutable(e.Fs));
			case "-true": return new FindPred((e, s, b) => true);
			case "-false": return new FindPred((e, s, b) => false);
			case "-prune": return new FindPred((e, s, b) => { if (!s.PostOrder) s.Prune = true; return true; });
			case "-quit": st.AnyAction = true; return new FindPred((e, s, b) => { s.Quit = true; return true; });
			case "-print": st.AnyAction = true; return new FindPred((e, s, b) => { Console.WriteLine(e.Display); return true; });
			case "-print0": st.AnyAction = true; return new FindPred((e, s, b) => { Console.Out.Write(e.Display + "\0"); return true; });
			case "-ls": st.AnyAction = true; return new FindPred((e, s, b) => { Console.WriteLine($"0 {(e.Size + 1023) / 1024,4} {LongLine(e.Info, false, false, false)} {e.Display}"); return true; });
			case "-printf":
				{
				var fmt = FindArg(a, ref i, t); st.AnyAction = true;
				return new FindPred((e, s, b) => { Console.Out.Write(FindPrintf(fmt, e)); return true; });
				}
			case "-delete":
				{
				st.AnyAction = true; st.PostOrder = true;
				return new FindPred((e, s, b) =>
					{
					try
						{
						if (IsRoot(e.Fs)) { Console.Error.WriteLine($"find: refusing to delete '{e.Display}'"); s.Rc = 1; return false; }
						if (e.IsDir && !e.IsLink) Directory.Delete(e.Fs, false); else File.Delete(e.Fs);
						return true;
						}
					catch (Exception ex) { Console.Error.WriteLine($"find: cannot delete '{e.Display}': {IoError(ex)}"); s.Rc = 1; return false; }
					});
				}
			case "-exec": case "-execdir":
				{
				st.AnyAction = true;
				var cmd = new List<string>();
				bool plus = false;
				while (true)
					{
					if (i >= a.Count) throw new Exception($"missing argument to `{t}'");
					var w = a[i++];
					if (w == ";") break;
					if (w == "+" && cmd.Count > 0 && cmd[^1] == "{}") { cmd.RemoveAt(cmd.Count - 1); plus = true; break; }
					cmd.Add(w);
					}
				if (cmd.Count == 0) throw new Exception($"missing argument to `{t}'");
				// -execdir runs from the file's own directory with `{}` as ./name, a `+` batch per directory (GNU); it
				// ran as -exec, from the shell's directory with the path as printed (2026-10-17)
				bool inDir = t == "-execdir";
				if (inDir) st.ExecDir = true;
				if (plus)
					{
					return new FindPred((e, s, b) =>
						{
						var (where, arg) = inDir ? ExecDirOf(e) : (null, e.Display);
						if (inDir && s.ExecPlusBatch.Count > 0 && s.ExecPlusDir != where) b.FindExecPlusFlush(s);
						s.ExecPlusCmd = cmd; s.ExecPlusDir = where;
						s.ExecPlusBatch.Add(arg);
						if (s.ExecPlusBatch.Count >= 500) b.FindExecPlusFlush(s);
						return true;
						});
					}
				return new FindPred((e, s, b) =>
					{
					var (where, arg) = inDir ? ExecDirOf(e) : (null, e.Display);
					var line = cmd.Select(w => w.Replace("{}", arg)).ToList();
					return b.FindRunIn(where, line) == 0;
					});
				}
			case "-ok": case "-okdir": throw new UnsupportedOptionException("find", t, "interactive confirmation is not available");
			case "-perm": case "-user": case "-group": case "-uid": case "-gid": case "-nouser": case "-nogroup": case "-inum": case "-links": case "-samefile": case "-fstype": case "-context":
				throw new UnsupportedOptionException("find", t, "no POSIX ownership/permission/inode model on Windows");
			case "-fprint": case "-fprint0": case "-fprintf": case "-fls":
				throw new UnsupportedOptionException("find", t, "file-output actions are not implemented");
			default:
				throw new UnsupportedOptionException("find", t, t.StartsWith('-') ? "unknown predicate" : "paths must precede expression");
			}
		}

	private void FindExecPlusFlush(FindState s)
		{
		if (s.ExecPlusCmd is null || s.ExecPlusBatch.Count == 0) return;
		var line = new List<string>(s.ExecPlusCmd);
		line.AddRange(s.ExecPlusBatch);
		s.ExecPlusBatch.Clear();
		int rc = FindRunIn(s.ExecPlusDir, line);
		if (rc != 0) s.Rc = 1;
		}

	/// <summary>-execdir's working directory and `{}`: the entry's own directory, and ./name. A starting point is taken as
	/// written, as GNU takes it: `.` runs in `.` as `./.`, `proj/` in `.` as `./proj/`, `./a/b` in `./a` as `./b`. From
	/// its full path, `.` ran in the parent directory (red team F7, 2026-10-19).</summary>
	private static (string? Dir, string Arg) ExecDirOf(FindEntry e)
		{
		if (e.Depth == 0 && e.Given.Length > 0)
			{
			var core = e.Given.TrimEnd('/', '\\');
			int cut = core.LastIndexOfAny(['/', '\\']);
			if (core.Length == 0) return (e.Given, "./");   // `/`
			return cut < 0 ? (".", "./" + e.Given)
			     : (cut == 0 ? core[..1] : core[..cut], "./" + e.Given[(cut + 1)..]);
			}
		var full = Path.GetFullPath(e.Fs);
		return (Path.GetDirectoryName(full) ?? full, "./" + Path.GetFileName(full.TrimEnd('/', '\\')));
		}

	/// <summary>GNU find's refusal of -execdir when PATH holds the current directory (`.`, an empty entry) or a relative
	/// entry: the command is looked up from each file's directory, so a program planted beside a file would run (it
	/// did: red team F7, 2026-10-19). The first such entry, as GNU reports it (`;` is this PATH's separator), or null.
	/// Absolute here: a `/` or `\` start (`/c/x` is a drive), or a drive with its slash (`C:/x`).</summary>
	private static string? UnsafePathForExecDir(string path)
		{
		foreach (var d in path.Split(Path.PathSeparator))
			{
			if (d is "" or ".")
				return "find: The current directory is included in the PATH environment variable, which is insecure in combination with the -execdir action of find.  "
				     + $"Please remove the current directory from your $PATH (that is, remove \".\", doubled {Sep}s, or leading or trailing {Sep}s)";
			bool absolute = d[0] is '/' or '\\' || (d.Length >= 3 && char.IsAsciiLetter(d[0]) && d[1] == ':' && d[2] is '/' or '\\');
			if (!absolute)
				return $"find: The relative path '{d}' is included in the PATH environment variable, which is insecure in combination with the -execdir action of find.  Please remove that entry from $PATH";
			}
		return null;
		}
	private static string Sep => Path.PathSeparator == ';' ? "semicolon" : "colon";

	/// <summary>Run a -exec/-execdir command, from <paramref name="dir"/> when given (the shell's cwd comes back).</summary>
	private int FindRunIn(string? dir, List<string> line)
		{
		if (dir is null) return _eval.RunCommand(line[0], line.GetRange(1, line.Count - 1));
		var env = _eval.Env;
		var old = env.Cwd;
		try   { env.ChangeCwd(dir); return _eval.RunCommand(line[0], line.GetRange(1, line.Count - 1)); }
		finally { try { env.ChangeCwd(ShellEnvironment.TranslatePath(old)); } catch { } }
		}

	private static string FindArg(List<string> a, ref int i, string opt)
		{
		if (i >= a.Count) throw new Exception($"missing argument to `{opt}'");
		return a[i++];
		}

	private static (char cmp, long n) FindNumArg(string s)
		{
		char cmp = '=';
		if (s.StartsWith('+')) { cmp = '+'; s = s[1..]; }
		else if (s.StartsWith('-')) { cmp = '-'; s = s[1..]; }
		if (!long.TryParse(s, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var n)) throw new Exception($"invalid argument `{s}'");
		return (cmp, n);
		}

	private static bool FindCompare(long value, char cmp, long n) => cmp switch { '+' => value > n, '-' => value < n, _ => value == n };

	/// <summary>Emacs regex (find -regex default) → .NET: `\|` `\(` `\)` `\{` `\}` are operators; bare are literals.</summary>
	private static string EmacsToNet(string re) => BreToNet(re);

	private static string FindPrintf(string fmt, FindEntry e)
		{
		var sb = new StringBuilder();
		for (int i = 0; i < fmt.Length; i++)
			{
			char c = fmt[i];
			if (c == '\\' && i + 1 < fmt.Length) { i = PrintfFormatter.AppendEscape(fmt, i + 1, sb, out _) - 1; continue; }
			if (c != '%' || i + 1 >= fmt.Length) { sb.Append(c); continue; }
			char k = fmt[++i];
			var mt = e.Info.LastWriteTime;
			switch (k)
				{
				case 'p': sb.Append(e.Display); break;
				case 'P': { var rel = e.Display.Length > e.Start.Length ? e.Display[(e.Start.Length + 1)..] : ""; sb.Append(rel); break; }
				case 'f': sb.Append(e.Depth == 0 ? e.Display : e.Info.Name); break;
				case 'h': { int s = e.Display.LastIndexOf('/'); sb.Append(s < 0 ? "." : s == 0 ? "/" : e.Display[..s]); break; }
				case 'H': sb.Append(e.Start); break;
				case 's': sb.Append(e.Size); break;
				case 'd': sb.Append(e.Depth); break;
				case 'y': sb.Append(e.IsLink ? 'l' : e.IsDir ? 'd' : 'f'); break;
				case 'm': { var ms = ModeString(e.Info); int o = (ms[1] == 'r' ? 4 : 0) + (ms[2] == 'w' ? 2 : 0) + (ms[3] == 'x' ? 1 : 0); sb.Append($"{o}{o}{o}"); break; }
				case 'M': sb.Append(ModeString(e.Info)); break;
				case 'u': case 'U': sb.Append(Environment.UserName); break;
				case 'g': case 'G': sb.Append("None"); break;
				case 'n': sb.Append('1'); break;
				case 'i': sb.Append('0'); break;
				case 'k': sb.Append((e.Size + 1023) / 1024); break;
				case 'b': sb.Append((e.Size + 511) / 512); break;
				case 't': sb.Append(mt.ToString("ddd MMM d HH:mm:ss.fffffff00 yyyy", System.Globalization.CultureInfo.InvariantCulture)); break;
				case 'T': case 'A': case 'C':
					{
					if (i + 1 >= fmt.Length) { sb.Append('%').Append(k); break; }
					char sub = fmt[++i];
					var d = k == 'A' ? e.Info.LastAccessTime : mt;
					sb.Append(sub switch
						{
						'@' => (((DateTimeOffset)d).ToUnixTimeSeconds() + (d.Millisecond / 1000.0)).ToString("F10", System.Globalization.CultureInfo.InvariantCulture).Replace(".", ".").TrimEnd('0'),
						'Y' => d.ToString("yyyy"), 'y' => d.ToString("yy"), 'm' => d.ToString("MM"), 'd' => d.ToString("dd"),
						'H' => d.ToString("HH"), 'M' => d.ToString("mm"), 'S' => d.ToString("ss.fffffff00", System.Globalization.CultureInfo.InvariantCulture),
						'T' => d.ToString("HH:mm:ss.fffffff00", System.Globalization.CultureInfo.InvariantCulture),
						'F' => d.ToString("yyyy-MM-dd"), 'x' => d.ToString("MM/dd/yy"), 'X' => d.ToString("HH:mm:ss"),
						'+' => d.ToString("yyyy-MM-dd+HH:mm:ss.fffffff00", System.Globalization.CultureInfo.InvariantCulture),
						'a' => d.ToString("ddd", System.Globalization.CultureInfo.InvariantCulture), 'b' => d.ToString("MMM", System.Globalization.CultureInfo.InvariantCulture),
						'j' => d.DayOfYear.ToString("D3"),
						_ => "%" + k + sub,
						});
					break;
					}
				case '%': sb.Append('%'); break;
				default: sb.Append('%').Append(k); break;
				}
			}
		return sb.ToString();
		}

	// ═══════════════════════════════════════════════════════════════════════════
	// diff
	// ═══════════════════════════════════════════════════════════════════════════

	private static int Diff(List<string> args)
		{
		var o = Opts.Parse("diff", args, "uU:qsiwbBrNaC:cyx:tTZ",
			["unified:u", "unified=:U", "brief:q", "report-identical-files:s", "ignore-case:i", "ignore-all-space:w", "ignore-space-change:b",
			 "ignore-blank-lines:B", "recursive:r", "new-file:N", "text:a", "context:c", "context=:C", "side-by-side:y", "exclude=:x",
			 "expand-tabs:t", "initial-tab:T", "ignore-trailing-space:Z", "strip-trailing-cr", "color", "color=", "no-dereference", "minimal", "speed-large-files", "label="]);
		if (o.Has('y')) throw new UnsupportedOptionException("diff", "-y", "side-by-side output is not implemented");
		if (o.Has('c') || o.Has('C')) throw new UnsupportedOptionException("diff", "-c", "context format is not implemented (use -u)");
		if (o.Operands.Count < 2) { Console.Error.WriteLine(o.Operands.Count == 0 ? "diff: missing operand" : $"diff: missing operand after '{o.Operands[0]}'"); return 2; }
		bool unified = o.Has('u') || o.Has('U');
		int context = o.Get('U') is string uc && int.TryParse(uc, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var ucn) ? ucn : 3;
		var opt = new DiffOptions
			{
			Unified = unified, Context = context, Brief = o.Has('q'), ReportIdentical = o.Has('s'), IgnoreCase = o.Has('i'),
			IgnoreAllSpace = o.Has('w'), IgnoreSpaceChange = o.Has('b'), IgnoreBlankLines = o.Has('B'), Recursive = o.Has('r'),
			NewFile = o.Has('N'), Text = o.Has('a'), StripCr = o.HasLong("strip-trailing-cr"), Excludes = o.All('x').ToList(),
			IgnoreTrailingSpace = o.Has('Z'),
			};
		// --label names the old file (first) and the new one (second) in the headers, with no timestamp (GNU);
		// accepted and ignored until 2026-10-12, so a patch made with it named the real files
		var labels = o.AllLong("label").ToList();
		if (labels.Count > 0) opt.LabelA = labels[0];
		if (labels.Count > 1) opt.LabelB = labels[1];
		// `-` is standard input (GNU). It was taken as a path ("diff: -: No such file or directory");
		// now it is read once into a temp file that is diffed in its place, still labelled `-`.
		string a = o.Operands[0], b = o.Operands[1];
		if (a == "-" && b == "-") return 0;
		string? stdinCopy = null;
		try
			{
			if (a == "-" || b == "-")
				{
				stdinCopy = Path.Combine(Path.GetTempPath(), "csbash-diff-" + Guid.NewGuid().ToString("N")[..12]);
				using var fs = ShellFile.Create(stdinCopy);
				if (Evaluator.CurrentRawStdin() is { } raw) raw.CopyTo(fs);   // bytes, with any read-ahead first
				else { var bytes = ShellEncoding.Utf8.GetBytes(Console.In.ReadToEnd()); fs.Write(bytes, 0, bytes.Length); }
				}
			return DiffPaths(a, b, opt, a, b, stdinCopy);
			}
		finally { if (stdinCopy is not null) { try { File.Delete(stdinCopy); } catch { } } }
		}

	private sealed class DiffOptions
		{
		public bool Unified, Brief, ReportIdentical, IgnoreCase, IgnoreAllSpace, IgnoreSpaceChange, IgnoreBlankLines, Recursive, NewFile, Text, StripCr, IgnoreTrailingSpace;
		public string? LabelA, LabelB;
		public int Context = 3;
		public List<string> Excludes = [];
		}

	private static int DiffPaths(string a, string b, DiffOptions opt, string labelA, string labelB, string? stdinCopy = null)
		{
		var pa = a == "-" && stdinCopy is not null ? stdinCopy : ShellEnvironment.TranslatePath(a);
		var pb = b == "-" && stdinCopy is not null ? stdinCopy : ShellEnvironment.TranslatePath(b);
		bool da = Directory.Exists(pa), db = Directory.Exists(pb);
		bool fa = ShellFile.Exists(pa), fb = ShellFile.Exists(pb);   // a device is a file here (`diff -u /dev/null new`)
		if (!da && !fa && !(opt.NewFile && (fb || db))) { Console.Error.WriteLine($"diff: {a}: No such file or directory"); return 2; }
		if (!db && !fb && !(opt.NewFile && (fa || da))) { Console.Error.WriteLine($"diff: {b}: No such file or directory"); return 2; }
		if (da && db)
			{
			if (!opt.Recursive && !opt.Brief)
				{
				// non-recursive dir compare: only report common files' diffs and "Only in"
				}
			return DiffDirs(a, b, pa, pb, opt);
			}
		if (da || db)
			{
			// file vs directory: compare with the same-named file inside the directory
			if (da) { a = a.TrimEnd('/', '\\') + "/" + Path.GetFileName(pb); pa = Path.Combine(pa, Path.GetFileName(pb)); }
			else    { b = b.TrimEnd('/', '\\') + "/" + Path.GetFileName(pa); pb = Path.Combine(pb, Path.GetFileName(pa)); }
			}
		return DiffFiles(a, b, pa, pb, opt);
		}

	private static int DiffDirs(string a, string b, string pa, string pb, DiffOptions opt)
		{
		var namesA = Directory.EnumerateFileSystemEntries(pa).Select(Path.GetFileName).ToHashSet(StringComparer.Ordinal)!;
		var namesB = Directory.EnumerateFileSystemEntries(pb).Select(Path.GetFileName).ToHashSet(StringComparer.Ordinal)!;
		var all = namesA.Union(namesB).OrderBy(n => n, StringComparer.Ordinal).ToList();
		int rc = 0;
		foreach (var n in all)
			{
			if (n is null || opt.Excludes.Any(x => Glob.Match(x, n))) continue;
			string ca = a.TrimEnd('/', '\\') + "/" + n, cb = b.TrimEnd('/', '\\') + "/" + n;
			string qa = Path.Combine(pa, n), qb = Path.Combine(pb, n);
			bool inA = namesA.Contains(n), inB = namesB.Contains(n);
			if (inA && !inB)
				{
				if (opt.NewFile && File.Exists(qa)) { int r = DiffFiles(ca, cb, qa, qb, opt); if (r > rc) rc = r; }
				else { Console.WriteLine($"Only in {a.TrimEnd('/', '\\')}: {n}"); rc = Math.Max(rc, 1); }
				continue;
				}
			if (!inA && inB)
				{
				if (opt.NewFile && File.Exists(qb)) { int r = DiffFiles(ca, cb, qa, qb, opt); if (r > rc) rc = r; }
				else { Console.WriteLine($"Only in {b.TrimEnd('/', '\\')}: {n}"); rc = Math.Max(rc, 1); }
				continue;
				}
			bool dA = Directory.Exists(qa), dB = Directory.Exists(qb);
			if (dA && dB)
				{
				if (opt.Recursive) { int r = DiffDirs(ca, cb, qa, qb, opt); if (r > rc) rc = r; }
				else Console.WriteLine($"Common subdirectories: {ca} and {cb}");
				continue;
				}
			if (dA != dB) { Console.WriteLine($"File {ca} is a {(dA ? "directory" : "regular file")} while file {cb} is a {(dB ? "directory" : "regular file")}"); rc = Math.Max(rc, 1); continue; }
			int fr = DiffFiles(ca, cb, qa, qb, opt, printHeaderCmd: true);
			if (fr > rc) rc = fr;
			}
		return rc;
		}

	private static string[] DiffReadLines(string p, DiffOptions opt, out bool binary)
		{
		binary = false;
		if (!File.Exists(p)) return [];
		var bytes = ShellFile.ReadAllBytes(p);
		if (!opt.Text && bytes.Take(8192).Contains((byte)0)) { binary = true; return []; }
		var text = ShellEncoding.Utf8.GetString(bytes);
		var lines = text.Split('\n').ToList();
		if (lines.Count > 0 && lines[^1].Length == 0) lines.RemoveAt(lines.Count - 1);
		if (opt.StripCr) lines = lines.Select(l => l.TrimEnd('\r')).ToList();
		return lines.ToArray();
		}

	private static int DiffFiles(string a, string b, string pa, string pb, DiffOptions opt, bool printHeaderCmd = false)
		{
		string[] la, lb; bool binA, binB;
		try { la = DiffReadLines(pa, opt, out binA); lb = DiffReadLines(pb, opt, out binB); }
		catch (Exception ex) { Console.Error.WriteLine($"diff: {IoError(ex)}"); return 2; }
		if (binA || binB)
			{
			bool same = File.Exists(pa) && File.Exists(pb) && ShellFile.ReadAllBytes(pa).AsSpan().SequenceEqual(ShellFile.ReadAllBytes(pb));
			if (!same) { Console.WriteLine($"Binary files {a} and {b} differ"); return 1; }
			if (opt.ReportIdentical) Console.WriteLine($"Files {a} and {b} are identical");
			return 0;
			}

		string Norm(string s)
			{
			if (opt.IgnoreAllSpace) s = new string(s.Where(c => !char.IsWhiteSpace(c)).ToArray());
			else if (opt.IgnoreSpaceChange) s = System.Text.RegularExpressions.Regex.Replace(s.Trim(), @"\s+", " ");
			else if (opt.IgnoreTrailingSpace) s = s.TrimEnd();
			if (opt.IgnoreCase) s = s.ToLowerInvariant();
			return s;
			}
		bool Eq(string x, string y) => Norm(x) == Norm(y);
		bool Blank(string s) => opt.IgnoreBlankLines && s.Trim().Length == 0;

		int n = la.Length, m = lb.Length;
		var c = new int[n + 1, m + 1];
		for (int ii = n - 1; ii >= 0; ii--)
			for (int jj = m - 1; jj >= 0; jj--)
				c[ii, jj] = Eq(la[ii], lb[jj]) ? c[ii + 1, jj + 1] + 1 : Math.Max(c[ii + 1, jj], c[ii, jj + 1]);

		// edit script as a list of (opChar, aIndex, bIndex): ' ' equal, '-' delete, '+' insert
		var ops = new List<(char op, int ai, int bi)>();
		{
		int x = 0, y = 0;
		while (x < n || y < m)
			{
			if (x < n && y < m && Eq(la[x], lb[y])) { ops.Add((' ', x, y)); x++; y++; }
			else if (y < m && (x >= n || c[x, y + 1] >= c[x + 1, y])) { ops.Add(('+', x, y)); y++; }
			else { ops.Add(('-', x, y)); x++; }
			}
		}
		if (opt.IgnoreBlankLines)
			ops = ops.Select(o2 => o2.op == '-' && Blank(la[o2.ai]) ? (' ', o2.ai, o2.bi) : o2.op == '+' && Blank(lb[o2.bi]) ? (' ', o2.ai, o2.bi) : o2).ToList();
		bool differ = ops.Any(o2 => o2.op != ' ');
		if (!differ)
			{
			if (opt.ReportIdentical) Console.WriteLine($"Files {a} and {b} are identical");
			return 0;
			}
		if (opt.Brief) { Console.WriteLine($"Files {a} and {b} differ"); return 1; }
		if (printHeaderCmd) Console.WriteLine($"diff{(opt.Recursive ? " -r" : "")}{(opt.Unified ? " -u" : "")} {a} {b}");

		if (!opt.Unified)
			{
			// GNU normal format
			int k = 0;
			while (k < ops.Count)
				{
				if (ops[k].op == ' ') { k++; continue; }
				int start = k;
				var dels = new List<int>(); var ins = new List<int>();
				while (k < ops.Count && ops[k].op != ' ')
					{
					if (ops[k].op == '-') dels.Add(ops[k].ai); else ins.Add(ops[k].bi);
					k++;
					}
				string Range(List<int> r) => r.Count == 1 ? $"{r[0] + 1}" : $"{r[0] + 1},{r[^1] + 1}";
				if (dels.Count > 0 && ins.Count > 0)
					{
					Console.WriteLine($"{Range(dels)}c{Range(ins)}");
					foreach (var d in dels) Console.WriteLine($"< {la[d]}");
					Console.WriteLine("---");
					foreach (var i2 in ins) Console.WriteLine($"> {lb[i2]}");
					}
				else if (dels.Count > 0)
					{
					Console.WriteLine($"{Range(dels)}d{ops[start].bi}");
					foreach (var d in dels) Console.WriteLine($"< {la[d]}");
					}
				else
					{
					Console.WriteLine($"{ops[start].ai}a{Range(ins)}");
					foreach (var i2 in ins) Console.WriteLine($"> {lb[i2]}");
					}
				}
			return 1;
			}

		// unified format
		string Stamp(string p) => File.Exists(p)
			? File.GetLastWriteTime(p).ToString("yyyy-MM-dd HH:mm:ss.fffffff00 ", System.Globalization.CultureInfo.InvariantCulture) + File.GetLastWriteTime(p).ToString("zzz").Replace(":", "")
			: "1970-01-01 00:00:00.000000000 +0000";
		Console.WriteLine(opt.LabelA is { } lblA ? $"--- {lblA}" : $"--- {a}\t{Stamp(pa)}");
		Console.WriteLine(opt.LabelB is { } lblB ? $"+++ {lblB}" : $"+++ {b}\t{Stamp(pb)}");
		int ctx = opt.Context;
		int idx = 0;
		while (idx < ops.Count)
			{
			// find next change
			while (idx < ops.Count && ops[idx].op == ' ') idx++;
			if (idx >= ops.Count) break;
			int hunkStart = Math.Max(0, idx - ctx);
			int hunkEnd = idx;   // exclusive, grows
			int last = idx;
			// extend through changes separated by <= 2*ctx equal lines
			while (true)
				{
				int j = last;
				while (j < ops.Count && ops[j].op != ' ') j++;
				last = j;   // first equal after a change run
				int k = j;
				while (k < ops.Count && ops[k].op == ' ' && k - j <= 2 * ctx) k++;
				if (k < ops.Count && ops[k].op != ' ' && k - j <= 2 * ctx) { last = k; continue; }
				hunkEnd = Math.Min(ops.Count, j + ctx);
				break;
				}
			int aStart = ops[hunkStart].ai, bStart = ops[hunkStart].bi;
			int aCount = 0, bCount = 0;
			var body = new StringBuilder();
			// within a change run GNU prints all deletions before all insertions
			for (int k = hunkStart; k < hunkEnd;)
				{
				var (op, ai, bi) = ops[k];
				if (op == ' ') { body.Append(' ').Append(la[ai]).Append('\n'); aCount++; bCount++; k++; continue; }
				int runEnd = k;
				while (runEnd < hunkEnd && ops[runEnd].op != ' ') runEnd++;
				for (int r = k; r < runEnd; r++) if (ops[r].op == '-') { body.Append('-').Append(la[ops[r].ai]).Append('\n'); aCount++; }
				for (int r = k; r < runEnd; r++) if (ops[r].op == '+') { body.Append('+').Append(lb[ops[r].bi]).Append('\n'); bCount++; }
				k = runEnd;
				}
			string RangeStr(int start, int count) => count == 1 ? $"{start + 1}" : count == 0 ? $"{start},0" : $"{start + 1},{count}";
			Console.WriteLine($"@@ -{RangeStr(aStart, aCount)} +{RangeStr(bStart, bCount)} @@");
			Console.Out.Write(body.ToString());
			idx = hunkEnd;
			}
		return 1;
		}

	// ═══════════════════════════════════════════════════════════════════════════
	// xargs
	// ═══════════════════════════════════════════════════════════════════════════

	private int Xargs(List<string> args)
		{
		var o = Opts.Parse("xargs", args, "0n:I:iL:rtd:a:P:s:E:pxo",
			["null:0", "max-args=:n", "replace=:I", "replace:i", "max-lines=:L", "no-run-if-empty:r", "verbose:t", "delimiter=:d", "arg-file=:a",
			 "max-procs=:P", "max-chars=:s", "eof=:E", "interactive:p", "exit:x", "open-tty:o", "process-slot-var="], stopAtFirstOperand: true);
		if (o.Has('p')) throw new UnsupportedOptionException("xargs", "-p", "interactive mode is not available");
		bool nullSep = o.Has('0'), noRunIfEmpty = o.Has('r'), verbose = o.Has('t');
		// GNU's refusals (2026-10-30): at least 1; a bad value was taken as "not given"
		static Func<string, IntProblem, string> Bad(char c) => (v, p) => (p == IntProblem.NotANumber
			? $"xargs: invalid number \"{v}\" for -{c} option" : $"xargs: value {v} for -{c} option should be >= 1")
			+ "\nTry 'xargs --help' for more information.";
		int maxArgs = o.Int('n', 0, 1, Bad('n')), maxLines = o.Int('L', 0, 1, Bad('L'));
		if (maxArgs > 0 && maxLines > 0)   // the later wins, with GNU's warning (2026-10-30; -L always won)
			{
			int lastN = -1, lastL = -1;
			for (int i = 0; i < o.Sequence.Count; i++) { if (o.Sequence[i].opt == 'n') lastN = i; else if (o.Sequence[i].opt == 'L') lastL = i; }
			if (lastL > lastN) { Console.Error.WriteLine("xargs: warning: options --max-args and -L are mutually exclusive, ignoring previous --max-args value"); maxArgs = 0; }
			else { Console.Error.WriteLine("xargs: warning: options --max-lines and --max-args/-n are mutually exclusive, ignoring previous --max-lines value"); maxLines = 0; }
			}
		string? replace = o.Get('I') ?? (o.Has('i') ? "{}" : null);
		string? eof = o.Get('E');
		char? delim = o.Get('d') is string ds ? UnescapeDelims(ds)[0] : null;
		var cmd = o.Operands.Count > 0 ? o.Operands : ["echo"];
		// -s: GNU's command buffer, 131072 unless given; every word, the command's own included, counts its length + 1
		int maxChars = o.Int('s', 131072, 1, Bad('s'));
		if (eof is not null && (nullSep || delim is not null))
			{
			Console.Error.WriteLine("xargs: warning: the -E option has no effect if -0 or -d is used.\n");
			eof = null;
			}

		// Read while the commands run, a batch run as soon as it is full, as GNU's (2026-10-30): it read all its
		// input first, so nothing ran before the input ended and an early stop (255, -E) still read it to the end.
		TextReader src;
		LfReader? file = null;
		if (o.Get('a') is string af)
			{
			try { src = file = new LfReader(ShellFile.OpenInput(ShellEnvironment.TranslatePath(af))); }
			catch (Exception ex) { Console.Error.WriteLine($"xargs: {af}: {IoError(ex)}"); return 1; }
			}
		else
			{
			src = ConsoleMux.In;
			if (ReferenceEquals(src, Console.In)) src = new LfReader(src);   // resolved now: the commands' stdin changes below
			}
		using var fileOwner = file;
		// a command's stdin is /dev/null unless the items come from -a (GNU's): streaming, it would eat the items to come
		using var nullIn = file is null ? _eval.StdinFromNull() : null;

		string? unmatched = null;   // "single" / "double": a quote still open at its line's end
		// The items in order; null marks an input line's end (-L counts them; a line ending in a blank goes on). -0 and
		// -d: a record each, empty ones kept, a trailing separator ending nothing (GNU). Otherwise blanks separate, quotes
		// group within a line, a backslash takes the next character (a newline too); -I: one item a line, leading blanks
		// dropped, a blank line skipped, quotes and backslashes as before. A CR is data. -E's word ends the input.
		IEnumerable<string?> Items()
			{
			if (nullSep || delim is not null)
				{
				char sep = nullSep ? '\0' : delim!.Value;
				string? rec;
				while ((rec = ShellEncoding.ReadRecord(src, sep, out _)) is not null) yield return rec;
				yield break;
				}
			bool perLine = replace is not null;
			var cur = new StringBuilder();
			bool inItem = false, escape = false, blankLast = false;
			char quote = '\0';
			string? text;
			while ((text = ShellEncoding.ReadRecord(src, '\n', out bool ended)) is not null)
				{
				int end = ended ? text.Length + 1 : text.Length;   // the newline, when there was one, is the last character
				for (int i = 0; i < end; i++)
					{
					char ch = i < text.Length ? text[i] : '\n';
					if (escape) { cur.Append(ch); escape = false; continue; }
					if (quote != '\0')
						{
						if (ch == '\n') { unmatched = quote == '\'' ? "single" : "double"; yield break; }
						if (ch == quote) quote = '\0'; else cur.Append(ch);
						continue;
						}
					bool ends = ch == '\n' || (!perLine && ch is ' ' or '\t');
					if (!ends)
						{
						blankLast = false;
						if (perLine && !inItem && ch is ' ' or '\t') continue;
						inItem = true;
						if (ch == '\\') escape = true;
						else if (ch is '\'' or '"') quote = ch;
						else cur.Append(ch);
						continue;
						}
					if (inItem)
						{
						string item = cur.ToString(); cur.Clear(); inItem = false;
						if (item == eof) yield break;
						yield return item;
						}
					if (ch == '\n') { if (!blankLast && !perLine) yield return null; blankLast = false; }
					else blankLast = true;
					}
				}
			if (quote != '\0') { unmatched = quote == '\'' ? "single" : "double"; yield break; }
			if (escape) cur.Append('\\');
			if (inItem && cur.ToString() is var last && last != eof) yield return last;
			}

		int worst = 0;
		bool Run(List<string> line)   // false: stop here (126, 127, 255: GNU's)
			{
			if (verbose) Console.Error.WriteLine(string.Join(" ", line));
			int rc;
			try { rc = _eval.RunCommand(line[0], line.GetRange(1, line.Count - 1)); }
			catch (ExitException ex) { rc = ex.Code; }
			if (rc == 127 || rc == 126) { worst = rc; return false; }
			if (rc == 255) { Console.Error.WriteLine($"xargs: {line[0]}: exited with status 255; aborting"); worst = 124; return false; }
			if (rc != 0 && worst == 0) worst = 123;
			return true;
			}

		int baseChars = 0;
		foreach (var w in cmd) baseChars += ShellEncoding.Utf8.GetByteCount(w) + 1;
		// An EXTERNAL program's batch must also fit Windows' command line: 32767 characters (CreateProcess), less room
		// for the program's own path (at most 260, quoted). GNU's 131072 did not: the batch could not start (2026-10-30;
		// Git's xargs uses 24228). Each argument counts as quoted, and a space.
		const int WindowsLine = 32767 - 1 - 262;
		bool external = !_eval.RunsInProcess(cmd[0]);
		int winBase = 0;
		if (external) foreach (var w in cmd.Skip(1)) winBase += ChildLauncher.QuotedLength(w) + 1;
		var batch = new List<string>(cmd);
		int chars = baseChars, winChars = winBase, lines = 0;
		bool any = false, lineHasItems = false;
		bool RunBatch()
			{
			var line = batch;
			batch = new List<string>(cmd); chars = baseChars; winChars = winBase; lines = 0;
			return Run(line);
			}
		foreach (var item in Items())
			{
			if (item is null)
				{
				if (maxLines > 0 && lineHasItems && ++lines == maxLines && !RunBatch()) return worst;
				lineHasItems = false;
				continue;
				}
			any = true;
			if (replace is not null)
				{
				if (!Run(cmd.Select(w => w.Replace(replace, item)).ToList())) return worst;
				continue;
				}
			int len = ShellEncoding.Utf8.GetByteCount(item) + 1, wlen = external ? ChildLauncher.QuotedLength(item) + 1 : 0;
			if (chars + len > maxChars || winChars + wlen > WindowsLine)
				{
				if (maxLines > 0) { Console.Error.WriteLine("xargs: argument list too long"); return 1; }   // -L implies -x (GNU)
				if (batch.Count > cmd.Count && !RunBatch()) return worst;
				if (chars + len > maxChars || winChars + wlen > WindowsLine) { Console.Error.WriteLine("xargs: argument line too long"); return 1; }
				}
			batch.Add(item); chars += len; winChars += wlen; lineHasItems = true;
			// a full batch runs at once, as GNU's (whose command then runs on while it reads: its messages can come
			// before that command's output; here each command finishes first)
			if (maxLines == 0 && maxArgs > 0 && batch.Count - cmd.Count == maxArgs && !RunBatch()) return worst;
			}
		if (unmatched is not null)
			{
			Console.Error.WriteLine($"xargs: unmatched {unmatched} quote; by default quotes are special to xargs unless you use the -0 option");
			if (batch.Count > cmd.Count) Run(batch);   // the items before it still run (GNU), and the status is 1
			return 1;
			}
		if (batch.Count > cmd.Count) RunBatch();
		else if (!any && !noRunIfEmpty && replace is null) Run(new List<string>(cmd));
		return worst;
		}
	}
