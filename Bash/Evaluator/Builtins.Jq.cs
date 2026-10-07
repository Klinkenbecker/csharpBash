using System.Text;

namespace Bash.Evaluator;

public sealed partial class Builtins
	{
	/// <summary>
	/// `jq [OPTIONS] FILTER [FILE...]`: the in-process subset (DECISIONS 2026-10-05, option (c)).
	/// Outside the subset, an option or a construct is refused while parsing, before any input is
	/// read, so the dispatcher hands the whole command to a PATH jq when there is one, or fails loudly
	/// (DECISIONS 2026-09-04 #2). Behaviour follows jq 1.6, the version the test oracle runs.
	/// </summary>
	private int Jq(List<string> args)
		{
		bool nullInput = false, slurp = false, raw = false, join = false, compact = false, exitStatus = false;
		var named = new Dictionary<string, object?>(StringComparer.Ordinal);
		string? filter = null;
		var files = new List<string>();
		bool endOfOptions = false;
		for (int i = 0; i < args.Count; i++)
			{
			var a = args[i];
			if (!endOfOptions && a == "--") { endOfOptions = true; continue; }
			if (!endOfOptions && a.StartsWith("--", StringComparison.Ordinal))
				{
				switch (a)
					{
					case "--null-input": nullInput = true; continue;
					case "--slurp": slurp = true; continue;
					case "--raw-output": raw = true; continue;
					case "--join-output": raw = join = true; continue;
					case "--compact-output": compact = true; continue;
					case "--exit-status": exitStatus = true; continue;
					case "--monochrome-output": continue;   // never coloured here
					case "--version":
						// Claude's usual first jq call is this probe (8 of 8 in the evidence); an error here
						// reads as "no jq" and it falls back to python
						Console.WriteLine("jq-1.6 (C#Bash in-process subset)");
						return 0;
					case "--arg":
					case "--argjson":
						{
						if (i + 2 >= args.Count) { Console.Error.WriteLine($"jq: {a} takes two parameters (e.g. {a} varname value)"); return 2; }
						var name = args[i + 1]; var value = args[i + 2];
						i += 2;
						if (a == "--arg") { named[name] = value; continue; }
						try { named[name] = Json.ParseOne(value); }
						catch (Json.ParseException) { Console.Error.WriteLine($"jq: Invalid JSON text passed to --argjson"); return 2; }
						continue;
						}
					}
				throw new UnsupportedOptionException("jq", a);
				}
			if (!endOfOptions && a.Length > 1 && a[0] == '-' && filter is null)
				{
				foreach (var ch in a[1..])
					switch (ch)
						{
						case 'n': nullInput = true; break;
						case 's': slurp = true; break;
						case 'r': raw = true; break;
						case 'j': raw = join = true; break;
						case 'c': compact = true; break;
						case 'e': exitStatus = true; break;
						case 'M': break;
						default: throw new UnsupportedOptionException("jq", "-" + ch);
						}
				continue;
				}
			if (filter is null) filter = a; else files.Add(a);
			}
		if (filter is null) { Console.Error.WriteLine("Usage:\tjq [OPTIONS] FILTER [FILES...]"); return 2; }

		JqNode program;
		try { program = JqParser.Parse(filter, JqInterp.IsSupported, JqInterp.IsJqBuiltin, named.Keys); }
		catch (JqUnsupportedException ex) { throw new UnsupportedOptionException("jq", ex.What, "not supported by C#Bash's in-process jq"); }
		catch (JqSyntaxException ex)
			{
			// jq's layout: the message, the source line padded to the error's column, the count
			int off = Math.Clamp(ex.Offset, 0, filter.Length);
			int lineStart = filter.LastIndexOf('\n', Math.Max(0, off - 1)) + 1;
			if (off == 0) lineStart = 0;
			int lineEnd = filter.IndexOf('\n', lineStart);
			var lineText = lineEnd < 0 ? filter[lineStart..] : filter[lineStart..lineEnd];
			int line = 1 + filter.Take(lineStart).Count(c => c == '\n');
			Console.Error.WriteLine($"jq: error: {ex.Message} at <top-level>, line {line}:\n{lineText}{new string(' ', off - lineStart)}\njq: 1 compile error");
			return 3;
			}

		var env = new JObj();
		foreach (var (k, v) in _env.GetExportedVars()) env.Set(k, v);
		var interp = new JqInterp(env);

		// stdout is buffered as jq's stdio is on a pipe or file (flushed at 4 KB and at exit) and errors
		// are not, so with `2>&1` an error line lands before earlier results, as with jq (measured)
		const int StdioBuffer = 4096;
		var outBuf = new StringBuilder();
		int rc = 0;
		bool anyOutput = false;
		object? lastOutput = null;
		void Emit(object? v)
			{
			anyOutput = true; lastOutput = v;
			if (raw && v is string s) outBuf.Append(s); else Json.Write(outBuf, v, compact, 2, 0, sortKeys: false, ascii: false);
			if (!join) outBuf.Append('\n');
			if (outBuf.Length >= StdioBuffer) { Console.Out.Write(outBuf.ToString()); outBuf.Clear(); }
			}
		void Run(object? input, string where)
			{
			try { foreach (var v in interp.Run(program, input, named)) Emit(v); }
			catch (JqError e)
				{
				rc = 5;
				if (e.Value is null) return;   // jq prints nothing for error(null)
				Console.Error.WriteLine(e.Value is string m
					? $"jq: error (at {where}): {m}"
					: $"jq: error (at {where}) (not a string): {Json.Dump(e.Value, compact: true)}");
				}
			}

		if (nullInput) Run(null, "<unknown>");
		else
			{
			var slurped = slurp ? new List<object?>() : null;
			string lastWhere = "<stdin>:0";
			foreach (var (name, text) in JqInputs(files, ref rc))
				{
				using var values = Json.Stream(text).GetEnumerator();
				bool failed = false;
				while (true)
					{
					try { if (!values.MoveNext()) break; }
					catch (Json.ParseException e) { Console.Error.WriteLine($"parse error: {e.Message}"); rc = 2; failed = true; break; }
					var (value, line) = values.Current;
					lastWhere = $"{name}:{line}";
					if (slurped is not null) slurped.Add(value);
					else Run(value, lastWhere);
					}
				if (failed) break;   // jq stops at a parse error
				}
			if (slurped is not null && rc != 2) Run(slurped, lastWhere);
			}
		Console.Out.Write(outBuf.ToString());
		Console.Out.Flush();
		if (rc == 0 && exitStatus) rc = !anyOutput ? 4 : Json.Truthy(lastOutput) ? 0 : 1;
		return rc;
		}

	/// <summary>The text of each input: the named files in order, or stdin (bytes, with anything a
	/// read before us buffered).</summary>
	private static List<(string Name, string Text)> JqInputs(List<string> files, ref int rc)
		{
		var list = new List<(string, string)>();
		if (files.Count == 0) { list.Add(("<stdin>", ReadStdinText())); return list; }
		foreach (var f in files)
			{
			if (f == "-") { list.Add(("<stdin>", ReadStdinText())); continue; }
			try { list.Add((f, ShellEncoding.Utf8.GetString(ShellFile.ReadAllBytes(ShellEnvironment.TranslatePath(f))))); }
			catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
				{
				Console.Error.WriteLine($"jq: error: Could not open {f}: {IoError(ex)}");
				rc = 2;
				}
			}
		return list;
		}

	private static string ReadStdinText()
		{
		if (Evaluator.CurrentRawStdin() is { } raw)
			{
			using var ms = new MemoryStream();
			raw.CopyTo(ms);
			return ShellEncoding.Utf8.GetString(ms.ToArray());
			}
		return Console.In.ReadToEnd();
		}
	}
