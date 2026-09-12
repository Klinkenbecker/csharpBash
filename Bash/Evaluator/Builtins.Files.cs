using System.Runtime.InteropServices;
using System.Text;

namespace Bash.Evaluator;

/// <summary>
/// File and directory coreutils with GNU option vocabulary and strict parsing:
/// basename dirname mkdir rmdir touch rm mv cp ls du cmp stat mktemp realpath readlink
/// chmod ln truncate. Destructive tools keep their footgun guards.
/// </summary>
public sealed partial class Builtins
	{
	private static string P(string userPath) => ShellEnvironment.TranslatePath(userPath);

	private static bool IsRoot(string full) =>
		string.Equals(Path.GetFullPath(full).TrimEnd('\\', '/'), Path.GetPathRoot(Path.GetFullPath(full))?.TrimEnd('\\', '/'), StringComparison.OrdinalIgnoreCase);

	// ── basename / dirname ──────────────────────────────────────────────────────

	private static int Basename(List<string> args)
		{
		var o = Opts.Parse("basename", args, "as:z", ["multiple:a", "suffix=:s", "zero:z"]);
		bool multiple = o.Has('a') || o.Has('s');
		string? suffix = o.Get('s');
		if (o.Operands.Count == 0) { Console.Error.WriteLine("basename: missing operand"); return 1; }
		var names = multiple ? o.Operands : [o.Operands[0]];
		if (!multiple && o.Operands.Count > 1) suffix = o.Operands[1];
		string term = o.Has('z') ? "\0" : "\n";
		foreach (var a in names)
			{
			var path = a.TrimEnd('/', '\\');
			if (path.Length == 0) { Console.Out.Write("/" + term); continue; }
			int slash = Math.Max(path.LastIndexOf('/'), path.LastIndexOf('\\'));
			var name = slash >= 0 ? path[(slash + 1)..] : path;
			if (suffix is not null && suffix.Length > 0 && name.Length > suffix.Length && name.EndsWith(suffix, StringComparison.Ordinal))
				name = name[..^suffix.Length];
			Console.Out.Write(name + term);
			}
		return 0;
		}

	private static int Dirname(List<string> args)
		{
		var o = Opts.Parse("dirname", args, "z", ["zero:z"]);
		if (o.Operands.Count == 0) { Console.Error.WriteLine("dirname: missing operand"); return 1; }
		string term = o.Has('z') ? "\0" : "\n";
		foreach (var raw in o.Operands)
			{
			var path = raw.TrimEnd('/', '\\');
			if (path.Length == 0) { Console.Out.Write("/" + term); continue; }
			int slash = Math.Max(path.LastIndexOf('/'), path.LastIndexOf('\\'));
			if (slash < 0) { Console.Out.Write("." + term); continue; }
			if (slash == 0) { Console.Out.Write(path[..1] + term); continue; }
			var dir = path[..slash].TrimEnd('/', '\\');
			Console.Out.Write((dir.Length == 0 ? path[..1] : dir) + term);
			}
		return 0;
		}

	// ── mkdir / rmdir / touch ───────────────────────────────────────────────────

	private static int Mkdir(List<string> args)
		{
		var o = Opts.Parse("mkdir", args, "pvm:", ["parents:p", "verbose:v", "mode=:m"]);
		bool parents = o.Has('p'), verbose = o.Has('v');
		if (o.Operands.Count == 0) { Console.Error.WriteLine("mkdir: missing operand"); return 1; }
		int rc = 0;
		foreach (var d in o.Operands)
			{
			var p = P(d);
			try
				{
				if (Directory.Exists(p) || File.Exists(p))
					{
					if (!parents || File.Exists(p)) { Console.Error.WriteLine($"mkdir: cannot create directory '{d}': File exists"); rc = 1; }
					continue;
					}
				if (!parents)
					{
					var parent = Path.GetDirectoryName(Path.GetFullPath(p));
					if (!string.IsNullOrEmpty(parent) && !Directory.Exists(parent))
						{ Console.Error.WriteLine($"mkdir: cannot create directory '{d}': No such file or directory"); rc = 1; continue; }
					}
				Directory.CreateDirectory(p);
				if (verbose) Console.WriteLine($"mkdir: created directory '{d}'");
				}
			catch (Exception ex) { Console.Error.WriteLine($"mkdir: cannot create directory '{d}': {IoError(ex)}"); rc = 1; }
			}
		return rc;
		}

	private static int Rmdir(List<string> args)
		{
		var o = Opts.Parse("rmdir", args, "pv", ["parents:p", "verbose:v", "ignore-fail-on-non-empty"]);
		bool parents = o.Has('p'), verbose = o.Has('v'), ignoreNonEmpty = o.HasLong("ignore-fail-on-non-empty");
		if (o.Operands.Count == 0) { Console.Error.WriteLine("rmdir: missing operand"); return 1; }
		int rc = 0;
		foreach (var d in o.Operands)
			{
			var cur = d.TrimEnd('/', '\\');
			while (true)
				{
				var p = P(cur);
				try
					{
					if (!Directory.Exists(p)) { Console.Error.WriteLine($"rmdir: failed to remove '{cur}': No such file or directory"); rc = 1; break; }
					if (Directory.EnumerateFileSystemEntries(p).Any())
						{
						if (!ignoreNonEmpty) { Console.Error.WriteLine($"rmdir: failed to remove '{cur}': Directory not empty"); rc = 1; }
						break;
						}
					Directory.Delete(p, false);
					if (verbose) Console.WriteLine($"rmdir: removing directory, '{cur}'");
					}
				catch (Exception ex) { Console.Error.WriteLine($"rmdir: failed to remove '{cur}': {IoError(ex)}"); rc = 1; break; }
				if (!parents) break;
				int slash = Math.Max(cur.LastIndexOf('/'), cur.LastIndexOf('\\'));
				if (slash <= 0) break;
				cur = cur[..slash];
				}
			}
		return rc;
		}

	private static int Touch(List<string> args)
		{
		var o = Opts.Parse("touch", args, "camd:t:r:f", ["no-create:c", "date=:d", "reference=:r", "time="]);
		bool noCreate = o.Has('c'), atime = o.Has('a'), mtime = o.Has('m');
		if (!atime && !mtime) atime = mtime = true;
		DateTime when = DateTime.Now;
		if (o.Get('d') is string ds)
			{
			if (!TryParseDate(ds, out when)) { Console.Error.WriteLine($"touch: invalid date format '{ds}'"); return 1; }
			}
		if (o.Get('t') is string ts)
			{
			// [[CC]YY]MMDDhhmm[.ss]
			var digits = ts.Replace(".", "");
			string[] fmts = ["yyyyMMddHHmmss", "yyyyMMddHHmm", "yyMMddHHmmss", "yyMMddHHmm", "MMddHHmm", "MMddHHmmss"];
			if (!DateTime.TryParseExact(digits, fmts, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out when))
				{ Console.Error.WriteLine($"touch: invalid date format '{ts}'"); return 1; }
			}
		if (o.Get('r') is string rf)
			{
			try { when = File.GetLastWriteTime(P(rf)); }
			catch (Exception ex) { Console.Error.WriteLine($"touch: failed to get attributes of '{rf}': {IoError(ex)}"); return 1; }
			}
		if (o.Operands.Count == 0) { Console.Error.WriteLine("touch: missing file operand"); return 1; }
		int rc = 0;
		foreach (var f in o.Operands)
			{
			try
				{
				var p = P(f);
				if (File.Exists(p))
					{
					if (mtime) File.SetLastWriteTime(p, when);
					if (atime) File.SetLastAccessTime(p, when);
					}
				else if (Directory.Exists(p))
					{
					if (mtime) Directory.SetLastWriteTime(p, when);
					if (atime) Directory.SetLastAccessTime(p, when);
					}
				else if (!noCreate)
					{
					File.Create(p).Dispose();
					if (o.Get('d') is not null || o.Get('t') is not null || o.Get('r') is not null) File.SetLastWriteTime(p, when);
					}
				}
			catch (Exception ex) { Console.Error.WriteLine($"touch: cannot touch '{f}': {IoError(ex)}"); rc = 1; }
			}
		return rc;
		}

	/// <summary>GNU-style date parsing (local time) shared by touch -d and date -d: see <see cref="GnuDate"/>.</summary>
	private static bool TryParseDate(string s, out DateTime result) => GnuDate.TryParse(s, false, out result);
	// ── rm / mv / cp ────────────────────────────────────────────────────────────

	private static int Rm(List<string> args)
		{
		var o = Opts.Parse("rm", args, "rRfivdI", ["recursive:r", "force:f", "interactive", "verbose:v", "dir:d", "preserve-root", "no-preserve-root", "one-file-system"]);
		bool recursive = o.Has('r') || o.Has('R'), force = o.Has('f'), verbose = o.Has('v'), emptyDir = o.Has('d');
		if (o.Operands.Count == 0) { if (!force) Console.Error.WriteLine("rm: missing operand"); return force ? 0 : 1; }
		int rc = 0;
		foreach (var f in o.Operands)
			{
			var p = P(f);
			try
				{
				if (Directory.Exists(p))
					{
					// Footgun guard: never remove a filesystem root (/, C:\, …).
					if (IsRoot(p) && !o.HasLong("no-preserve-root"))
						{ Console.Error.WriteLine($"rm: it is dangerous to operate recursively on '{f}'\nrm: use --no-preserve-root to override this failsafe"); rc = 1; continue; }
					if (!recursive)
						{
						if (emptyDir && !Directory.EnumerateFileSystemEntries(p).Any()) { Directory.Delete(p); if (verbose) Console.WriteLine($"removed directory '{f}'"); continue; }
						Console.Error.WriteLine($"rm: cannot remove '{f}': Is a directory"); rc = 1; continue;
						}
					RmTree(p, f, verbose);
					}
				else if (File.Exists(p))
					{
					var attrs = File.GetAttributes(p);
					if ((attrs & FileAttributes.ReadOnly) != 0 && force) File.SetAttributes(p, attrs & ~FileAttributes.ReadOnly);
					File.Delete(p);
					if (verbose) Console.WriteLine($"removed '{f}'");
					}
				else if (!force) { Console.Error.WriteLine($"rm: cannot remove '{f}': No such file or directory"); rc = 1; }
				}
			catch (Exception ex) { if (!force || ex is not (FileNotFoundException or DirectoryNotFoundException)) { Console.Error.WriteLine($"rm: cannot remove '{f}': {IoError(ex)}"); rc = 1; } }
			}
		return rc;
		}

	private static void RmTree(string p, string display, bool verbose)
		{
		foreach (var f in Directory.GetFiles(p))
			{
			var attrs = File.GetAttributes(f);
			if ((attrs & FileAttributes.ReadOnly) != 0) File.SetAttributes(f, attrs & ~FileAttributes.ReadOnly);
			File.Delete(f);
			if (verbose) Console.WriteLine($"removed '{display}/{Path.GetFileName(f)}'");
			}
		foreach (var d in Directory.GetDirectories(p))
			{
			var info = new DirectoryInfo(d);
			if (info.LinkTarget is not null) { Directory.Delete(d); continue; }   // don't descend into junctions/symlinks
			RmTree(d, $"{display}/{Path.GetFileName(d)}", verbose);
			}
		Directory.Delete(p, false);
		if (verbose) Console.WriteLine($"removed directory '{display}'");
		}

	private static int Mv(List<string> args)
		{
		var o = Opts.Parse("mv", args, "finvt:Tu", ["force:f", "interactive:i", "no-clobber:n", "verbose:v", "target-directory=:t", "no-target-directory:T", "update:u", "backup", "strip-trailing-slashes"]);
		bool noClobber = o.Has('n'), verbose = o.Has('v');
		var ps = o.Operands;
		string dst; List<string> srcs;
		if (o.Get('t') is string tdir) { dst = tdir; srcs = ps; }
		else
			{
			if (ps.Count < 2) { Console.Error.WriteLine(ps.Count == 0 ? "mv: missing file operand" : $"mv: missing destination file operand after '{ps[0]}'"); return 1; }
			dst = ps[^1]; srcs = ps.GetRange(0, ps.Count - 1);
			}
		var dstP = P(dst);
		bool dstIsDir = Directory.Exists(dstP) && !o.Has('T');
		if (srcs.Count > 1 && !dstIsDir) { Console.Error.WriteLine($"mv: target '{dst}' is not a directory"); return 1; }
		int rc = 0;
		foreach (var s in srcs)
			{
			var sp = P(s);
			try
				{
				var target = dstIsDir ? Path.Combine(dstP, Path.GetFileName(sp.TrimEnd('/', '\\'))) : dstP;
				if (noClobber && (File.Exists(target) || Directory.Exists(target))) continue;
				if (Directory.Exists(sp))
					{
					if (Directory.Exists(target)) { Console.Error.WriteLine($"mv: cannot overwrite directory '{target}' with directory '{s}'"); rc = 1; continue; }
					try { Directory.Move(sp, target); }
					catch (IOException) { CopyDir(sp, target, true); Directory.Delete(sp, true); }   // cross-volume
					}
				else if (File.Exists(sp))
					{
					if (Directory.Exists(target)) { Console.Error.WriteLine($"mv: cannot overwrite directory '{target}' with non-directory"); rc = 1; continue; }
					File.Move(sp, target, overwrite: true);
					}
				else { Console.Error.WriteLine($"mv: cannot stat '{s}': No such file or directory"); rc = 1; continue; }
				if (verbose) Console.WriteLine($"renamed '{s}' -> '{(dstIsDir ? dst.TrimEnd('/', '\\') + "/" + Path.GetFileName(sp.TrimEnd('/', '\\')) : dst)}'");
				}
			catch (Exception ex) { Console.Error.WriteLine($"mv: cannot move '{s}' to '{dst}': {IoError(ex)}"); rc = 1; }
			}
		return rc;
		}

	private static int Cp(List<string> args)
		{
		var o = Opts.Parse("cp", args, "rRapfnuvit:TLPdlsx", ["recursive:r", "archive:a", "preserve", "force:f", "no-clobber:n", "update:u", "verbose:v", "interactive:i", "target-directory=:t", "no-target-directory:T", "dereference:L", "no-dereference:P", "parents", "no-preserve=", "reflink="]);
		bool recursive = o.Has('r') || o.Has('R') || o.Has('a'), preserve = o.Has('p') || o.Has('a') || o.HasLong("preserve"), noClobber = o.Has('n'), update = o.Has('u'), verbose = o.Has('v');
		if (o.Has('s') || o.Has('l')) throw new UnsupportedOptionException("cp", o.Has('s') ? "-s" : "-l", "link modes are not implemented");
		var ps = o.Operands;
		string dst; List<string> srcs;
		if (o.Get('t') is string tdir) { dst = tdir; srcs = ps; }
		else
			{
			if (ps.Count < 2) { Console.Error.WriteLine(ps.Count == 0 ? "cp: missing file operand" : $"cp: missing destination file operand after '{ps[0]}'"); return 1; }
			dst = ps[^1]; srcs = ps.GetRange(0, ps.Count - 1);
			}
		var dstP = P(dst);
		bool dstIsDir = Directory.Exists(dstP) && !o.Has('T');
		if (srcs.Count > 1 && !dstIsDir) { Console.Error.WriteLine($"cp: target '{dst}' is not a directory"); return 1; }
		int rc = 0;
		foreach (var s in srcs)
			{
			var sp = P(s);
			try
				{
				string target;
				if (o.HasLong("parents") && dstIsDir) { target = Path.Combine(dstP, s.Replace('/', '\\')); Directory.CreateDirectory(Path.GetDirectoryName(target)!); }
				else target = dstIsDir ? Path.Combine(dstP, Path.GetFileName(sp.TrimEnd('/', '\\'))) : dstP;
				if (Directory.Exists(sp))
					{
					if (!recursive) { Console.Error.WriteLine($"cp: -r not specified; omitting directory '{s}'"); rc = 1; continue; }
					// `cp -r src existingdir` copies INTO the directory (src/ → existingdir/src)
					CopyDir(sp, target, preserve);
					}
				else if (File.Exists(sp))
					{
					if (Directory.Exists(target)) target = Path.Combine(target, Path.GetFileName(sp));
					if (noClobber && File.Exists(target)) continue;
					if (update && File.Exists(target) && File.GetLastWriteTimeUtc(target) >= File.GetLastWriteTimeUtc(sp)) continue;
					File.Copy(sp, target, overwrite: true);
					if (preserve) { File.SetLastWriteTime(target, File.GetLastWriteTime(sp)); File.SetAttributes(target, File.GetAttributes(sp)); }
					}
				else { Console.Error.WriteLine($"cp: cannot stat '{s}': No such file or directory"); rc = 1; continue; }
				if (verbose) Console.WriteLine($"'{s}' -> '{target}'");
				}
			catch (Exception ex) { Console.Error.WriteLine($"cp: cannot copy '{s}': {IoError(ex)}"); rc = 1; }
			}
		return rc;
		}

	private static void CopyDir(string src, string dst, bool preserve)
		{
		Directory.CreateDirectory(dst);
		foreach (var f in Directory.GetFiles(src))
			{
			var t = Path.Combine(dst, Path.GetFileName(f));
			File.Copy(f, t, true);
			if (preserve) File.SetLastWriteTime(t, File.GetLastWriteTime(f));
			}
		foreach (var d in Directory.GetDirectories(src)) CopyDir(d, Path.Combine(dst, Path.GetFileName(d)), preserve);
		if (preserve) Directory.SetLastWriteTime(dst, Directory.GetLastWriteTime(src));
		}

	// ── ls ──────────────────────────────────────────────────────────────────────

	private static int Ls(List<string> args)
		{
		var o = Opts.Parse("ls", args, "aAlh1drtSRFpGognisumxCLXvqbNTw:cU",
			["all:a", "almost-all:A", "human-readable:h", "directory:d", "reverse:r", "recursive:R", "classify:F", "indicator-style=", "color", "color=",
			 "group-directories-first", "time-style=", "sort=", "time=", "no-group:G", "inode:i", "size:s", "literal:N", "dereference:L", "quoting-style=", "hide=", "ignore=:I", "format=", "full-time", "block-size="]);
		bool all = o.Has('a'), almostAll = o.Has('A'), longFmt = o.Has('l') || o.Has('o') || o.Has('g') || o.Has('n') || o.HasLong("full-time"), human = o.Has('h'),
		     dirSelf = o.Has('d'), reverse = o.Has('r'), byTime = o.Has('t'), bySize = o.Has('S'), recursive = o.Has('R'),
		     classify = o.Has('F'), slashDirs = o.Has('p'), dirsFirst = o.HasLong("group-directories-first"), noGroup = o.Has('G') || o.Has('o'),
		     inode = o.Has('i'), blocks = o.Has('s'), byExt = o.Has('X'), byVersion = o.Has('v'), noSort = o.Has('U') || o.GetLong("sort") == "none";
		if (o.GetLong("sort") is string sk) { if (sk == "time") byTime = true; else if (sk == "size") bySize = true; else if (sk == "extension") byExt = true; else if (sk == "version") byVersion = true; }
		if (o.Has('m') || o.Has('x') || o.Has('C')) throw new UnsupportedOptionException("ls", o.Has('m') ? "-m" : o.Has('x') ? "-x" : "-C", "column output is not implemented (output is one entry per line)");
		bool fullTime = o.HasLong("full-time") || o.GetLong("time-style") == "full-iso";
		var ignorePats = o.All('I').ToList();
		var operands = o.Operands.Count == 0 ? ["."] : o.Operands;

		var files = new List<(string display, FileSystemInfo info)>();
		var dirs = new List<(string display, DirectoryInfo info)>();
		int rc = 0;
		foreach (var op in operands)
			{
			var fs = P(op);
			if (Directory.Exists(fs) && !dirSelf) dirs.Add((op, new DirectoryInfo(fs)));
			else if (Directory.Exists(fs)) files.Add((op, new DirectoryInfo(fs)));
			else if (File.Exists(fs)) files.Add((op, new FileInfo(fs)));
			else { Console.Error.WriteLine($"ls: cannot access '{op}': No such file or directory"); rc = 2; }
			}

		int Compare((string display, FileSystemInfo info) a, (string display, FileSystemInfo info) b)
			{
			if (dirsFirst)
				{
				bool da = a.info is DirectoryInfo, db = b.info is DirectoryInfo;
				if (da != db) return da ? -1 : 1;
				}
			int c;
			if (noSort) c = 0;
			else if (byTime) c = b.info.LastWriteTimeUtc.CompareTo(a.info.LastWriteTimeUtc);
			else if (bySize) c = (b.info as FileInfo)?.Length.CompareTo((a.info as FileInfo)?.Length ?? 0) ?? 0;
			else if (byExt) c = string.CompareOrdinal(Path.GetExtension(a.display), Path.GetExtension(b.display));
			else if (byVersion) c = VersionCompare(a.display, b.display);
			else c = 0;
			if (c == 0) c = string.CompareOrdinal(a.display, b.display);
			return reverse ? -c : c;
			}

		string Name(string display, FileSystemInfo info)
			{
			var n = display;
			if (classify || slashDirs)
				{
				if (info is DirectoryInfo) n += "/";
				else if (classify && info is FileInfo fi && FileTests.IsExecutable(fi.FullName)) n += "*";
				}
			return n;
			}

		string SizeStr(long n) => human ? Human1024(n) : n.ToString();

		void PrintEntries(List<(string display, FileSystemInfo info)> entries, bool inDir)
			{
			entries.Sort(Compare);
			if (longFmt && inDir)
				{
				long total = entries.Sum(e => e.info is FileInfo fi ? (fi.Length + 1023) / 1024 : 0);
				Console.WriteLine($"total {(human ? Human1024(total * 1024) : total.ToString())}");
				}
			foreach (var (display, info) in entries)
				{
				var sb = new StringBuilder();
				if (inode) sb.Append("0 ");
				if (blocks) sb.Append(((info is FileInfo f0 ? (f0.Length + 1023) / 1024 : 0)).ToString().PadLeft(4)).Append(' ');
				if (longFmt) sb.Append(LongLine(info, human, noGroup, fullTime)).Append(' ');
				sb.Append(Name(display, info));
				if (longFmt && info.LinkTarget is string lt) sb.Append(" -> ").Append(lt);
				Console.WriteLine(sb.ToString());
				}
			}

		if (files.Count > 0) PrintEntries(files, false);
		bool header = dirs.Count > 1 || files.Count > 0 || recursive;
		bool first = files.Count == 0;
		void ListDir(string display, DirectoryInfo di)
			{
			if (header) { if (!first) Console.WriteLine(); Console.WriteLine($"{display}:"); }
			first = false;
			var entries = new List<(string, FileSystemInfo)>();
			try
				{
				if (all) { entries.Add((".", di)); entries.Add(("..", di.Parent ?? di)); }
				foreach (var e in di.EnumerateFileSystemInfos())
					{
					var n = e.Name;
					if (!all && !almostAll && n.StartsWith('.')) continue;
					if (ignorePats.Any(p => Glob.Match(p, n))) continue;
					entries.Add((n, e));
					}
				}
			catch (Exception ex) { Console.Error.WriteLine($"ls: cannot open directory '{display}': {IoError(ex)}"); rc = 2; return; }
			PrintEntries(entries, true);
			if (recursive)
				foreach (var (n, e) in entries.Where(e => e.Item2 is DirectoryInfo && e.Item1 != "." && e.Item1 != "..").OrderBy(e => e.Item1, StringComparer.Ordinal))
					if (e.LinkTarget is null) ListDir($"{display.TrimEnd('/')}/{n}", (DirectoryInfo)e);
			}
		foreach (var (display, di) in dirs) ListDir(display, di);
		return rc;
		}

	private static string Human1024(long n)
		{
		string[] u = ["", "K", "M", "G", "T", "P"]; double d = n; int i = 0;
		while (d >= 1024 && i < u.Length - 1) { d /= 1024; i++; }
		if (i == 0) return n.ToString();
		return d < 10 ? $"{Math.Ceiling(d * 10) / 10:0.0}{u[i]}" : $"{Math.Ceiling(d)}{u[i]}";
		}

	private static string ModeString(FileSystemInfo info)
		{
		bool dir = info is DirectoryInfo;
		bool link = info.LinkTarget is not null;
		bool ro = (info.Attributes & FileAttributes.ReadOnly) != 0;
		bool exec = dir || (info is FileInfo fi && FileTests.IsExecutable(fi.FullName));
		string perm = (ro ? "r-" : "rw") + (exec ? "x" : "-");
		return (link ? "l" : dir ? "d" : "-") + perm + perm + perm;
		}

	private static string LongLine(FileSystemInfo info, bool human, bool noGroup, bool fullTime)
		{
		long size = info is FileInfo fi ? fi.Length : 0;
		var mtime = info.LastWriteTime;
		string when = fullTime
			? mtime.ToString("yyyy-MM-dd HH:mm:ss.fffffff00 ", System.Globalization.CultureInfo.InvariantCulture) + mtime.ToString("zzz").Replace(":", "")
			: (DateTime.Now - mtime).TotalDays < 183 && mtime <= DateTime.Now.AddHours(1)
				? mtime.ToString("MMM", System.Globalization.CultureInfo.InvariantCulture) + " " + mtime.Day.ToString().PadLeft(2) + " " + mtime.ToString("HH:mm")
				: mtime.ToString("MMM", System.Globalization.CultureInfo.InvariantCulture) + " " + mtime.Day.ToString().PadLeft(2) + "  " + mtime.ToString("yyyy");
		string owner = Environment.UserName;
		string group = noGroup ? "" : " " + (OperatingSystem.IsWindows() ? "None" : "users");
		string sizeStr = (human ? Human1024(size) : size.ToString()).PadLeft(human ? 4 : 5);
		return $"{ModeString(info)} 1 {owner}{group} {sizeStr} {when}";
		}

	// ── du ──────────────────────────────────────────────────────────────────────

	private static int Du(List<string> args)
		{
		var o = Opts.Parse("du", args, "sabhckmd:xL0", ["summarize:s", "all:a", "bytes:b", "human-readable:h", "total:c", "max-depth=:d", "apparent-size", "one-file-system:x", "dereference:L", "null:0", "block-size=", "time", "exclude="]);
		bool summary = o.Has('s'), all = o.Has('a'), bytes = o.Has('b') || o.HasLong("apparent-size"), human = o.Has('h'), total = o.Has('c'), kilo = o.Has('k'), mega = o.Has('m');
		int maxDepth = summary ? 0 : o.GetInt('d', int.MaxValue);
		var exclude = o.All('e').ToList(); // unused placeholder
		var paths = o.Operands.Count == 0 ? ["."] : o.Operands;
		int rc = 0; long grand = 0;
		string Fmt(long size) => human ? Human1024(size) : bytes ? size.ToString() : mega ? ((size + 1048575) / 1048576).ToString() : ((size + 1023) / 1024).ToString();
		void Print(long size, string path) => Console.WriteLine($"{Fmt(size)}\t{path}");
		long Walk(string fs, string disp, int depth)
			{
			long sum = 0;
			try
				{
				foreach (var f in Directory.GetFiles(fs))
					{
					long len = new FileInfo(f).Length;
					sum += len;
					if (all && depth < maxDepth) Print(len, disp + "/" + Path.GetFileName(f));
					}
				foreach (var d in Directory.GetDirectories(fs))
					{
					if (new DirectoryInfo(d).LinkTarget is not null && !o.Has('L')) continue;
					sum += Walk(d, disp + "/" + Path.GetFileName(d), depth + 1);
					}
				}
			catch (Exception ex) { Console.Error.WriteLine($"du: cannot read directory '{disp}': {IoError(ex)}"); rc = 1; }
			if (depth <= maxDepth) Print(sum, disp);
			return sum;
			}
		foreach (var pth in paths)
			{
			var p = P(pth);
			try
				{
				if (File.Exists(p)) { long len = new FileInfo(p).Length; Print(len, pth); grand += len; }
				else if (Directory.Exists(p)) grand += Walk(p, pth.TrimEnd('/', '\\'), 0);
				else { Console.Error.WriteLine($"du: cannot access '{pth}': No such file or directory"); rc = 1; }
				}
			catch (Exception ex) { Console.Error.WriteLine($"du: {pth}: {IoError(ex)}"); rc = 1; }
			}
		if (total) Print(grand, "total");
		return rc;
		}

	// ── cmp ─────────────────────────────────────────────────────────────────────

	private static int Cmp(List<string> args)
		{
		var o = Opts.Parse("cmp", args, "slbn:i:", ["silent:s", "quiet:s", "verbose:l", "print-bytes:b", "bytes=:n", "ignore-initial=:i"]);
		bool silent = o.Has('s'), verbose = o.Has('l');
		if (o.Operands.Count < 1) { Console.Error.WriteLine("cmp: missing operand"); return 2; }
		string f1 = o.Operands[0], f2 = o.Operands.Count > 1 ? o.Operands[1] : "-";
		long limit = o.Get('n') is string ns ? ParseCount("cmp", ns, 'n') : long.MaxValue;
		long skip = o.Get('i') is string isk ? ParseCount("cmp", isk.Split(':')[0], 'i') : 0;
		try
			{
			var a = ReadBytes(f1); var b = ReadBytes(f2);
			int start = (int)Math.Min(skip, Math.Min(a.Length, b.Length));
			int min = (int)Math.Min(Math.Min(a.Length, b.Length), start + limit); long line = 1; bool differ = false;
			for (int i = start; i < min; i++)
				{
				if (a[i] != b[i])
					{
					differ = true;
					if (verbose) { Console.WriteLine($"{i + 1,7} {Convert.ToString(a[i], 8).PadLeft(3, '0')} {Convert.ToString(b[i], 8).PadLeft(3, '0')}"); continue; }
					if (!silent) Console.WriteLine($"{f1} {f2} differ: byte {i + 1}, line {line}");
					return 1;
					}
				if (a[i] == (byte)'\n') line++;
				}
			if (differ) return 1;
			if (a.Length != b.Length && min < Math.Max(a.Length, b.Length) && limit == long.MaxValue)
				{ if (!silent) Console.Error.WriteLine($"cmp: EOF on {(a.Length < b.Length ? f1 : f2)}"); return 1; }
			return 0;
			}
		catch (Exception ex) { Console.Error.WriteLine($"cmp: {IoError(ex)}"); return 2; }
		}

	// ── stat ────────────────────────────────────────────────────────────────────

	private static int Stat(List<string> args)
		{
		var o = Opts.Parse("stat", args, "Lc:tf", ["dereference:L", "format=:c", "printf=", "terse:t", "file-system:f"]);
		if (o.Has('f')) throw new UnsupportedOptionException("stat", "-f", "file-system status is not implemented");
		if (o.Operands.Count == 0) { Console.Error.WriteLine("stat: missing operand"); return 1; }
		string? fmt = o.GetLong("printf") ?? o.Get('c');
		bool printf = o.HasLong("printf");
		int rc = 0;
		foreach (var f in o.Operands)
			{
			var p = P(f);
			FileSystemInfo? info = File.Exists(p) ? new FileInfo(p) : Directory.Exists(p) ? new DirectoryInfo(p) : null;
			if (info is null) { Console.Error.WriteLine($"stat: cannot statx '{f}': No such file or directory"); rc = 1; continue; }
			if (o.Has('L') && info.LinkTarget is not null)
				{
				var tp = Path.IsPathRooted(info.LinkTarget) ? info.LinkTarget : Path.Combine(Path.GetDirectoryName(p)!, info.LinkTarget);
				info = Directory.Exists(tp) ? new DirectoryInfo(tp) : new FileInfo(tp);
				}
			long size = info is FileInfo fi ? fi.Length : 0;
			bool dir = info is DirectoryInfo;
			string mode = ModeString(info);
			string octal = ((dir ? 0 : 0) + (mode[1] == 'r' ? 4 : 0) + (mode[2] == 'w' ? 2 : 0) + (mode[3] == 'x' ? 1 : 0)).ToString();
			octal = octal + octal + octal;
			string type = info.LinkTarget is not null ? "symbolic link" : dir ? "directory" : size == 0 ? "regular empty file" : "regular file";
			var mt = info.LastWriteTime; var at = info.LastAccessTime; var ct = info.CreationTime;
			string Iso(DateTime d) => d.ToString("yyyy-MM-dd HH:mm:ss.fffffff00 ", System.Globalization.CultureInfo.InvariantCulture) + d.ToString("zzz").Replace(":", "");
			if (o.Has('t')) fmt ??= "%n %s %b %f %u %g %D %i %h %t %T %X %Y %Z %W %o %C";
			if (fmt is null)
				{
				Console.WriteLine($"  File: {f}{(info.LinkTarget is not null ? " -> " + info.LinkTarget : "")}");
				Console.WriteLine($"  Size: {size,-15} Blocks: {(size + 511) / 512,-10} IO Block: 4096   {type}");
				Console.WriteLine($"Device: 0h/0d\tInode: 0           Links: 1");
				Console.WriteLine($"Access: (0{octal}/{mode})  Uid: (    0/{Environment.UserName})   Gid: (    0/None)");
				Console.WriteLine($"Access: {Iso(at)}");
				Console.WriteLine($"Modify: {Iso(mt)}");
				Console.WriteLine($"Change: {Iso(mt)}");
				Console.WriteLine($" Birth: {Iso(ct)}");
				continue;
				}
			var sb = new StringBuilder();
			for (int i = 0; i < fmt.Length; i++)
				{
				char c = fmt[i];
				if (c == '\\' && i + 1 < fmt.Length && printf) { i = PrintfFormatter.AppendEscape(fmt, i + 1, sb, out _) - 1; continue; }
				if (c != '%' || i + 1 >= fmt.Length) { sb.Append(c); continue; }
				char k = fmt[++i];
				sb.Append(k switch
					{
					'n' => f, 'N' => $"'{f}'" + (info.LinkTarget is not null ? $" -> '{info.LinkTarget}'" : ""),
					's' => size.ToString(), 'b' => ((size + 511) / 512).ToString(), 'B' => "512",
					'f' => (dir ? 0x4000 : 0x8000 | Convert.ToInt32(octal, 8)).ToString("x"),
					'F' => type, 'a' => octal, 'A' => mode, 'h' => "1", 'i' => "0", 'd' => "0", 'D' => "0",
					'u' => "0", 'U' => Environment.UserName, 'g' => "0", 'G' => "None",
					'x' => Iso(at), 'X' => ((DateTimeOffset)at).ToUnixTimeSeconds().ToString(),
					'y' => Iso(mt), 'Y' => ((DateTimeOffset)mt).ToUnixTimeSeconds().ToString(),
					'z' => Iso(mt), 'Z' => ((DateTimeOffset)mt).ToUnixTimeSeconds().ToString(),
					'w' => Iso(ct), 'W' => ((DateTimeOffset)ct).ToUnixTimeSeconds().ToString(),
					'o' => "4096", 'm' => Path.GetPathRoot(Path.GetFullPath(p)) ?? "", 'C' => "?", 't' => "0", 'T' => "0",
					'%' => "%",
					_ => "%" + k,
					});
				}
			if (printf) Console.Out.Write(sb.ToString()); else Console.WriteLine(sb.ToString());
			}
		return rc;
		}

	// ── mktemp / realpath / readlink ────────────────────────────────────────────

	private static int Mktemp(List<string> args)
		{
		var o = Opts.Parse("mktemp", args, "dqup:t", ["directory:d", "quiet:q", "dry-run:u", "tmpdir=", "tmpdir", "suffix=", "template=:t"]);
		bool dir = o.Has('d'), dryRun = o.Has('u');
		string template = o.Operands.Count > 0 ? o.Operands[0] : "tmp.XXXXXXXXXX";
		string suffix = o.GetLong("suffix") ?? "";
		string? tmpdir = o.GetLong("tmpdir") ?? o.Get('p');
		bool useTmp = tmpdir is not null || o.HasLong("tmpdir") || o.Has('t') || o.Operands.Count == 0 || !(template.Contains('/') || template.Contains('\\'));
		if (o.Operands.Count > 0 && (template.Contains('/') || template.Contains('\\')) && tmpdir is null) useTmp = false;
		string baseDir = useTmp ? P(tmpdir is { Length: > 0 } ? tmpdir : (Environment.GetEnvironmentVariable("TMPDIR") is { Length: > 0 } td ? td : Path.GetTempPath())) : "";
		int xs = template.Length - template.TrimEnd('X').Length;
		if (xs < 3 && suffix.Length == 0 && !template.Contains("XXX")) { Console.Error.WriteLine($"mktemp: too few X's in template '{template}'"); return 1; }
		var rnd = new Random();
		const string chars = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
		for (int attempt = 0; attempt < 100; attempt++)
			{
			var name = new StringBuilder(template);
			int lastX = template.LastIndexOf("XXX", StringComparison.Ordinal);
			int run = template.Length - template.TrimEnd('X').Length;
			for (int i = template.Length - run; i < template.Length; i++) name[i] = chars[rnd.Next(chars.Length)];
			var candidate = name + suffix;
			var full = useTmp ? Path.Combine(baseDir, candidate) : P(candidate);
			if (File.Exists(full) || Directory.Exists(full)) continue;
			if (!dryRun)
				{
				try { if (dir) Directory.CreateDirectory(full); else File.Create(full).Dispose(); }
				catch (Exception ex) { if (!o.Has('q')) Console.Error.WriteLine($"mktemp: failed to create {(dir ? "directory" : "file")} via template '{template}': {IoError(ex)}"); return 1; }
				}
			Console.WriteLine(ShellEnvironment.ToShellPath(full));   // forward-slash form: no shell escapes (2026-09-11)
			return 0;
			}
		Console.Error.WriteLine("mktemp: too many attempts");
		return 1;
		}

	private static int Realpath(List<string> args)
		{
		var o = Opts.Parse("realpath", args, "emsqzLP", ["canonicalize-existing:e", "canonicalize-missing:m", "no-symlinks:s", "strip:s", "quiet:q", "zero:z", "relative-to=", "relative-base=", "logical:L", "physical:P"]);
		if (o.Operands.Count == 0) { Console.Error.WriteLine("realpath: missing operand"); return 1; }
		int rc = 0;
		string? relTo = o.GetLong("relative-to");
		foreach (var f in o.Operands)
			{
			var p = P(f);
			string full;
			try
				{
				full = Path.GetFullPath(p);
				if (!o.Has('s')) full = ResolveLinks(full);
				if (o.Has('e') && !File.Exists(full) && !Directory.Exists(full)) { if (!o.Has('q')) Console.Error.WriteLine($"realpath: {f}: No such file or directory"); rc = 1; continue; }
				if (relTo is not null) full = Path.GetRelativePath(Path.GetFullPath(P(relTo)), full);
				}
			catch (Exception ex) { Console.Error.WriteLine($"realpath: {f}: {IoError(ex)}"); rc = 1; continue; }
			Console.Out.Write(ShellEnvironment.ToShellPath(full) + (o.Has('z') ? "\0" : "\n"));
			}
		return rc;
		}

	private static string ResolveLinks(string full)
		{
		try
			{
			var info = File.Exists(full) ? new FileInfo(full) : Directory.Exists(full) ? (FileSystemInfo)new DirectoryInfo(full) : null;
			var target = info?.ResolveLinkTarget(true);
			return target?.FullName ?? full;
			}
		catch { return full; }
		}

	private static int Readlink(List<string> args)
		{
		var o = Opts.Parse("readlink", args, "femnqsvz", ["canonicalize:f", "canonicalize-existing:e", "canonicalize-missing:m", "no-newline:n", "quiet:q", "silent:s", "verbose:v", "zero:z"]);
		if (o.Operands.Count == 0) { Console.Error.WriteLine("readlink: missing operand"); return 1; }
		int rc = 0;
		string term = o.Has('n') ? "" : o.Has('z') ? "\0" : "\n";
		foreach (var f in o.Operands)
			{
			var p = P(f);
			try
				{
				if (o.Has('f') || o.Has('e') || o.Has('m'))
					{
					var full = ResolveLinks(Path.GetFullPath(p));
					if (o.Has('e') && !File.Exists(full) && !Directory.Exists(full)) { rc = 1; continue; }
					Console.Out.Write(full + term);
					continue;
					}
				FileSystemInfo info = Directory.Exists(p) ? new DirectoryInfo(p) : new FileInfo(p);
				if (info.LinkTarget is null) { if (o.Has('v')) Console.Error.WriteLine($"readlink: {f}: Invalid argument"); rc = 1; continue; }
				Console.Out.Write(info.LinkTarget + term);
				}
			catch (Exception ex) { if (o.Has('v')) Console.Error.WriteLine($"readlink: {f}: {IoError(ex)}"); rc = 1; }
			}
		return rc;
		}

	// ── chmod / ln / truncate ───────────────────────────────────────────────────

	/// <summary>Windows has no POSIX mode bits: the only thing chmod can honour is the
	/// read-only attribute (`-w`/`+w`, or an octal mode without owner-write). Execute bits are
	/// accepted and ignored (executability is by extension/shebang), as Git Bash effectively does.</summary>
	private static int Chmod(List<string> args)
		{
		// modes like "+x" / "-w" begin with '-' and are not options: pull them out first
		var rest = new List<string>(); string? mode = null;
		foreach (var a in args)
			{
			if (mode is null && (a.StartsWith('+') || (a.StartsWith('-') && a.Length > 1 && "rwxXstugoa".IndexOf(a[1]) >= 0 && !a.StartsWith("-R") && !a.StartsWith("-v") && !a.StartsWith("-c") && !a.StartsWith("-f")) || (a.Length > 0 && char.IsDigit(a[0])) || (a.Length > 0 && "ugoa".IndexOf(a[0]) >= 0 && a.IndexOfAny(['+', '-', '=']) > 0)))
				{ mode = a; continue; }
			rest.Add(a);
			}
		var o = Opts.Parse("chmod", args.Count > 0 && mode is not null ? rest : args, "Rvcf", ["recursive:R", "verbose:v", "changes:c", "silent:f", "quiet:f", "reference=", "preserve-root", "no-preserve-root"]);
		if (mode is null && o.GetLong("reference") is null) { Console.Error.WriteLine("chmod: missing operand"); return 1; }
		var files = o.Operands;
		if (files.Count == 0) { Console.Error.WriteLine($"chmod: missing operand after '{mode}'"); return 1; }
		bool? readOnly = null;   // null = leave as is
		if (mode is not null)
			{
			if (mode.All(char.IsDigit))
				{
				int m = Convert.ToInt32(mode, 8);
				readOnly = (m & 0x80) == 0;
				}
			else
				{
				foreach (var clause in mode.Split(','))
					{
					int opIdx = clause.IndexOfAny(['+', '-', '=']);
					if (opIdx < 0) continue;
					var who = clause[..opIdx]; char op = clause[opIdx]; var perms = clause[(opIdx + 1)..];
					bool affectsOwner = who.Length == 0 || who.Contains('u') || who.Contains('a');
					if (!affectsOwner || !perms.Contains('w')) { if (op == '=' && affectsOwner) readOnly = true; continue; }
					readOnly = op == '-';
					}
				}
			}
		int rc = 0;
		foreach (var f in files)
			{
			var p = P(f);
			try
				{
				if (!File.Exists(p) && !Directory.Exists(p)) { if (!o.Has('f')) Console.Error.WriteLine($"chmod: cannot access '{f}': No such file or directory"); rc = 1; continue; }
				if (readOnly is bool ro && File.Exists(p))
					{
					var attrs = File.GetAttributes(p);
					var nattrs = ro ? attrs | FileAttributes.ReadOnly : attrs & ~FileAttributes.ReadOnly;
					if (nattrs != attrs) { File.SetAttributes(p, nattrs); if (o.Has('v') || o.Has('c')) Console.WriteLine($"mode of '{f}' changed"); }
					else if (o.Has('v')) Console.WriteLine($"mode of '{f}' retained");
					}
				else if (o.Has('v')) Console.WriteLine($"mode of '{f}' retained");
				if (o.Has('R') && Directory.Exists(p) && readOnly is bool ro2)
					foreach (var sub in Directory.EnumerateFiles(p, "*", SearchOption.AllDirectories))
						{
						var attrs = File.GetAttributes(sub);
						File.SetAttributes(sub, ro2 ? attrs | FileAttributes.ReadOnly : attrs & ~FileAttributes.ReadOnly);
						}
				}
			catch (Exception ex) { if (!o.Has('f')) Console.Error.WriteLine($"chmod: changing permissions of '{f}': {IoError(ex)}"); rc = 1; }
			}
		return rc;
		}

	[DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
	private static extern bool CreateHardLinkW(string lpFileName, string lpExistingFileName, IntPtr lpSecurityAttributes);

	private static int Ln(List<string> args)
		{
		var o = Opts.Parse("ln", args, "sfvnrTt:iLP", ["symbolic:s", "force:f", "verbose:v", "no-dereference:n", "relative:r", "no-target-directory:T", "target-directory=:t", "interactive:i", "logical:L", "physical:P", "backup"]);
		bool symbolic = o.Has('s'), force = o.Has('f'), verbose = o.Has('v');
		var ps = o.Operands;
		string dst; List<string> srcs;
		if (o.Get('t') is string tdir) { dst = tdir; srcs = ps; }
		else if (ps.Count == 1) { dst = Path.GetFileName(ps[0].TrimEnd('/', '\\')); srcs = [ps[0]]; }
		else
			{
			if (ps.Count < 2) { Console.Error.WriteLine("ln: missing file operand"); return 1; }
			dst = ps[^1]; srcs = ps.GetRange(0, ps.Count - 1);
			}
		var dstP = P(dst);
		bool dstIsDir = Directory.Exists(dstP) && !o.Has('T');
		int rc = 0;
		foreach (var s in srcs)
			{
			var target = dstIsDir ? Path.Combine(dstP, Path.GetFileName(s.TrimEnd('/', '\\'))) : dstP;
			try
				{
				if (File.Exists(target) || Directory.Exists(target))
					{
					if (!force) { Console.Error.WriteLine($"ln: failed to create {(symbolic ? "symbolic" : "hard")} link '{dst}': File exists"); rc = 1; continue; }
					if (Directory.Exists(target) && new DirectoryInfo(target).LinkTarget is null) { Console.Error.WriteLine($"ln: '{target}': cannot overwrite directory"); rc = 1; continue; }
					if (File.Exists(target)) File.Delete(target); else Directory.Delete(target);
					}
				if (symbolic)
					{
					var linkTarget = o.Has('r') ? Path.GetRelativePath(Path.GetDirectoryName(Path.GetFullPath(target))!, Path.GetFullPath(P(s))) : s.Replace('/', '\\');
					var sp = Path.IsPathRooted(P(s)) ? P(s) : Path.Combine(Path.GetDirectoryName(Path.GetFullPath(target))!, P(s));
					if (Directory.Exists(sp)) Directory.CreateSymbolicLink(target, linkTarget);
					else File.CreateSymbolicLink(target, linkTarget);
					}
				else
					{
					var sp = P(s);
					if (!File.Exists(sp)) { Console.Error.WriteLine($"ln: failed to access '{s}': No such file or directory"); rc = 1; continue; }
					if (!CreateHardLinkW(target, sp, IntPtr.Zero)) throw new IOException(Marshal.GetLastPInvokeErrorMessage());
					}
				if (verbose) Console.WriteLine($"'{target}' -> '{s}'");
				}
			catch (Exception ex) { Console.Error.WriteLine($"ln: failed to create {(symbolic ? "symbolic" : "hard")} link '{target}': {IoError(ex)}"); rc = 1; }
			}
		return rc;
		}

	private static int Truncate(List<string> args)
		{
		var o = Opts.Parse("truncate", args, "s:cor:", ["size=:s", "no-create:c", "io-blocks:o", "reference=:r"]);
		if (o.Operands.Count == 0) { Console.Error.WriteLine("truncate: missing file operand"); return 1; }
		long? refSize = o.Get('r') is string rf ? new FileInfo(P(rf)).Length : null;
		string? spec = o.Get('s');
		if (spec is null && refSize is null) { Console.Error.WriteLine("truncate: you must specify either '--size' or '--reference'"); return 1; }
		int rc = 0;
		foreach (var f in o.Operands)
			{
			var p = P(f);
			try
				{
				if (!File.Exists(p)) { if (o.Has('c')) continue; File.Create(p).Dispose(); }
				using var fs = new FileStream(p, FileMode.Open, FileAccess.ReadWrite);
				long cur = fs.Length, size;
				if (spec is null) size = refSize!.Value;
				else
					{
					char rel = spec[0];
					long n = ParseCount("truncate", rel is '+' or '-' or '<' or '>' or '/' or '%' ? spec[1..] : spec, 's');
					size = rel switch { '+' => cur + n, '-' => Math.Max(0, cur - n), '<' => Math.Min(cur, n), '>' => Math.Max(cur, n), '/' => n == 0 ? cur : cur / n * n, '%' => n == 0 ? cur : (cur + n - 1) / n * n, _ => n };
					}
				fs.SetLength(size);
				}
			catch (Exception ex) { Console.Error.WriteLine($"truncate: cannot open '{f}' for writing: {IoError(ex)}"); rc = 1; }
			}
		return rc;
		}
	}
