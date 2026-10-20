using Bash.Lexer;
using Bash.Parser;
using Bash.Evaluator;
using Bash.IO;

// UTF-8 output. Setting the encoding needs a console handle, and a host that spawns us with no
// console (Claude Code's Bash tool, CI runners) has none. .NET then writes stdout in the ANSI code
// page, NOT UTF-8 as this comment used to assume: `echo 'é — ✓'` reached Claude Code as "� � ?"
// (measured 2026-10-05; DECISIONS). So without a console, stdout and stderr get UTF-8 writers of
// our own, before ConsoleMux captures them as the real streams.
Timing.Begin();              // the performance review's timing switch: nothing unless BASH_TIMING_LOG is set
ShellCulture.Pin();          // numbers and dates as bash's C locale, whatever the user's culture (U+2212 minus, 2026-10-18)
bool consoleUtf8 = true;
try { Console.OutputEncoding = ShellEncoding.Utf8; } catch (Exception) { consoleUtf8 = false; }   // byte-transparent (2026-09-12)
Timing.Mark("console.out");   // the timing switch splits the "console" segment (review R4, 2026-10-18)
try { Console.InputEncoding  = ShellEncoding.Utf8; } catch (Exception) { }
Timing.Mark("console.in");
if (!consoleUtf8)
	{
	Console.SetOut(new StreamWriter(Console.OpenStandardOutput(), ShellEncoding.Utf8) { AutoFlush = true });
	Console.SetError(new StreamWriter(Console.OpenStandardError(), ShellEncoding.Utf8) { AutoFlush = true });
	}
Console.Out.NewLine = "\n";    // bash emits LF, not Windows CRLF (installed writers set it too)
Console.Error.NewLine = "\n";
Timing.Mark("console.writers");
ConsoleMux.Install();          // per-thread console streams (pipeline stages, captures, jobs)
Timing.Mark("console");

const string BashVersion = "5.1.0-koliada";
const string BashVersionLong = "bash, version 5.1.0-koliada(1)-release (x86_64-pc-msys)";

// The build stamp: "1.0.0+hg.<hash>[+] <rev>[+]", written by Bash.csproj from `hg id` at build
// time (+ = uncommitted tree). Printed by --version and exposed to scripts as CSHARPBASH_BUILD
// (the whole string) and CSHARPBASH_REV (the integer revision, comparable with (( ))), so a
// check can bind to a VALUE rather than to an output line position (web-d6, 2026-09-08).
static string BuildStamp() => BuildInfo.Stamp;
static int BuildRev()
	{
	var last = BuildStamp().Split(' ', StringSplitOptions.RemoveEmptyEntries).LastOrDefault() ?? "";
	// invariant: the current culture loads ICU (~0.7 ms) on every start (2026-10-18)
	return int.TryParse(last.TrimEnd('+'), System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var r) ? r : 0;
	}

// ── command-line flag parsing (bash style: options in any order before the first
//    non-option; combined short flags; `--` ends options; -c takes the first non-option
//    as the command string, the next as $0, the rest as positionals) ─────────────────
string? cFlagCmd = null, scriptPath = null, rcFile = null;
bool cMode = false, login = false, norc = false, noprofile = false, forceInteractive = false, readStdin = false;
var positionals = new List<string>();
var pendingSetFlags = new List<(char flag, bool enable)>();
var pendingLongOpts = new List<(string name, bool enable)>();
var pendingShopts   = new List<(string name, bool enable)>();

// Restricted mode (`-r`, `--restricted`, started as `rbash`) is not implemented, and running
// unrestricted would remove the protection the caller asked for, so the shell refuses to start
// (DECISIONS 2026-10-11). Until then `-r` and `--restricted` were accepted and silently ignored.
if (string.Equals(Path.GetFileNameWithoutExtension(Environment.ProcessPath), "rbash", StringComparison.OrdinalIgnoreCase))
	return RefuseRestricted("started as rbash");

int ai = 0;
for (; ai < args.Length; ai++)
	{
	var a = args[ai];
	if (a == "--") { ai++; break; }
	if (a == "-")  { readStdin = true; ai++; break; }           // "-" = end of options, read stdin
	if (a.Length < 2 || (a[0] != '-' && a[0] != '+')) break;    // first non-option

	if (a.StartsWith("--", StringComparison.Ordinal))   // ordinal: culture-sensitive loads ICU on every start
		{
		switch (a)
			{
			case "--login":     login = true; break;
			case "--norc":      norc = true; break;
			case "--noprofile": noprofile = true; break;
			case "--noediting": case "--verbose": case "--debugger": break;
			case "--posix": NotAvailable.PosixMode("bash: ", "--posix"); break;   // said nothing until 2026-10-11
			case "--restricted": return RefuseRestricted("--restricted");
			case "--rcfile": case "--init-file":
				if (ai + 1 < args.Length) rcFile = args[++ai]; break;
			case "--version":
				// First line keeps bash's shape ("…, version X.Y.Z(1)-release (triplet)") for scripts
				// that grep the version number; it does NOT claim to be GNU bash, and no FSF
				// copyright is printed — this is an independent MIT implementation.
				Console.WriteLine(BashVersionLong);
				Console.WriteLine("C#Bash: a bash-compatible interpreter for Windows, MIT licensed. https://github.com/klinkenbecker/csharpBash");
				// which build this is: stamped from the hg working revision at build time (Bash.csproj)
				Console.WriteLine("C#Bash build " + BuildStamp());
				return 0;
			case "--help":
				Console.WriteLine("Usage: bash [GNU long option] [option] ...");
				Console.WriteLine("       bash [GNU long option] [option] script-file ...");
				Console.WriteLine("       bash -c command_string [name [args ...]]");
				Console.WriteLine("Shell options: -c -i -l -s -r -e -u -x -n -f -v -C -a -o option -O shopt --login --norc --noprofile --rcfile FILE --version");
				return 0;
			default:
				Console.Error.WriteLine($"bash: {a}: invalid option");
				return 2;
			}
		continue;
		}

	bool enable = a[0] == '-';
	for (int k = 1; k < a.Length; k++)
		{
		char f = a[k];
		switch (f)
			{
			case 'c': cMode = true; break;
			case 'l': login = true; break;
			case 'i': forceInteractive = true; break;
			case 's': readStdin = true; break;
			case 'r': if (enable) return RefuseRestricted("-r"); break;   // restricted: not implemented, refused
			case 'D': break;
			case 'o':
				{
				string? name = k + 1 < a.Length ? a[(k + 1)..] : (ai + 1 < args.Length ? args[++ai] : null);
				if (name is null) { Console.Error.WriteLine("bash: -o: option requires an argument"); return 2; }
				pendingLongOpts.Add((name, enable)); k = a.Length; break;
				}
			case 'O':
				{
				string? name = k + 1 < a.Length ? a[(k + 1)..] : (ai + 1 < args.Length ? args[++ai] : null);
				if (name is null) { Console.Error.WriteLine("bash: -O: option requires an argument"); return 2; }
				pendingShopts.Add((name, enable)); k = a.Length; break;
				}
			default:
				pendingSetFlags.Add((f, enable));
				break;
			}
		}
	}

var rest = args.Skip(ai).ToList();
if (cMode)
	{
	if (rest.Count == 0) { Console.Error.WriteLine("bash: -c: option requires an argument"); return 2; }
	cFlagCmd = rest[0];
	positionals.AddRange(rest.Skip(1));
	}
else if (rest.Count > 0 && !readStdin)
	{
	scriptPath = rest[0];
	positionals.AddRange(rest.Skip(1));
	}
else
	positionals.AddRange(rest);

// ── the shell ────────────────────────────────────────────────────────────────────
var evaluator = new Evaluator();
evaluator.ImportFunctions();   // `export -f` in a parent shell (BASH_FUNC_name%%)
Timing.Mark("evaluator");
bool interactive = forceInteractive || (cFlagCmd is null && scriptPath is null && !Console.IsInputRedirected);
evaluator.Options.Interactive   = interactive;
evaluator.Options.CommandString = cFlagCmd is not null;
evaluator.Options.ReadStdin     = cFlagCmd is null && scriptPath is null;
evaluator.Options.LoginShell    = login;
foreach (var (f, en) in pendingSetFlags)
	if (!evaluator.Options.Apply(f, en)) { Console.Error.WriteLine($"bash: -{f}: invalid option"); return 2; }
foreach (var (n, en) in pendingLongOpts)
	{
	if (!evaluator.Options.ApplyLong(n, en)) { Console.Error.WriteLine($"bash: {n}: invalid option name"); return 2; }
	if (en && n == "posix") NotAvailable.PosixMode("bash: ", "-o posix");
	}
foreach (var (n, en) in pendingShopts)
	{
	var errMsg = evaluator.Options.SetShopt(n, en);
	if (errMsg is not null) { Console.Error.WriteLine($"bash: {errMsg}"); return 2; }
	}
if (login) evaluator.Options.SetShopt("login_shell", true);

// Built-in defaults — set before sourcing startup files so they can be overridden.
if (evaluator.Env.Get("BASH_VERSION").Length == 0) evaluator.Env.Set("BASH_VERSION", BashVersion);
evaluator.Env.Set("CSHARPBASH_BUILD", BuildStamp());          // "1.0.0+hg.<hash>[+] <rev>[+]"
evaluator.Env.Set("CSHARPBASH_REV", BuildRev().ToString());   // integer, 0 if unstamped
if (evaluator.Env.Get("PS1").Length == 0) evaluator.Env.Set("PS1", @"\s-\v\$ ");
if (evaluator.Env.Get("PS2").Length == 0) evaluator.Env.Set("PS2", "> ");
// Python writes a pipe or file in the ANSI codepage (cp1252) on Windows, so `py x.py | cat` printed
// mojibake and crashed on a character cp1252 lacks (UnicodeEncodeError); on Linux it writes UTF-8, the
// encoding this shell reads. UTF-8 for its pipes and files, unless the environment already says
// otherwise; a console ignores it (Python 3.6+). NOT HGENCODING: measured 2026-10-10, it makes
// `hg commit -m 'café'` abort on Windows (DECISIONS "UTF-8 for external programs").
if (!evaluator.Env.IsSet("PYTHONIOENCODING")) { evaluator.Env.Set("PYTHONIOENCODING", "utf-8"); evaluator.Env.Export("PYTHONIOENCODING"); }

// ── -c "command" : run a single command string, non-interactive ──────────────────
// bash -c 'cmd' [name [arg ...]] : name becomes $0, the rest $1.. .
if (cFlagCmd is not null)
	{
	if (positionals.Count > 0)
		{
		evaluator.Env.SetArg0(positionals[0]);
		evaluator.Env.SetPositionals(positionals.Skip(1));
		}
	evaluator.TopLevelSource = "environment";
	if (login) SourceLogin(evaluator, noprofile);
	else SourceBashEnv(evaluator);
	Timing.Mark("ready");
	try { var code = evaluator.RunString(cFlagCmd, origin: "-c", mainLoop: true); evaluator.RunExitTrap(); Timing.Mark("done"); return code; }
	// bash ends a -c shell on an expansion error (set -u, ${x:?}) with 127, or 1 where set -e is in
	// force (on, and not suspended by && / || / if, as inside Claude Code's `eval '…' && pwd`); a
	// script file ends with 1 (measured against bash 5.1, 2026-10-08)
	catch (ExitException ex) { evaluator.RunExitTrap(); return ex.ExpansionError && !ex.ErrexitInForce ? 127 : ex.Code; }
	}

// ── script file mode: bash script.sh ─────────────────────────────────────────────
if (scriptPath is not null)
	{
	var resolved = ShellEnvironment.TranslatePath(scriptPath);
	if (!File.Exists(resolved)) { Console.Error.WriteLine($"bash: {scriptPath}: No such file or directory"); return 127; }
	// $0 = script path, $1.. = the args following it.
	evaluator.Env.SetArg0(scriptPath);
	evaluator.Env.SetPositionals(positionals);
	evaluator.EnterScriptFile(scriptPath);
	if (login) SourceLogin(evaluator, noprofile);
	else SourceBashEnv(evaluator);
	// The read is inside the try: a locked, blocked (AV) or vanished script must be an error
	// message and a status, never an unhandled exception (2026-09-12).
	Timing.Mark("ready");
	try { var code = evaluator.RunString(ShellEncoding.ReadAllText(resolved), origin: scriptPath, mainLoop: true); evaluator.RunExitTrap(); Timing.Mark("done"); return code; }
	catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException or System.Security.SecurityException)
		{ Console.Error.WriteLine($"bash: {scriptPath}: {ex.Message}"); return 126; }
	catch (ExitException ex) { evaluator.RunExitTrap(); return ex.Code; }
	}

// ── non-interactive stdin (piped script): no prompts, no editor ───────────────────
if (!interactive)
	{
	evaluator.Env.SetPositionals(positionals);
	if (login) SourceLogin(evaluator, noprofile);
	else SourceBashEnv(evaluator);
	var src = Console.In.ReadToEnd();
	try { var code = evaluator.RunString(src, origin: evaluator.Env.Get("0"), mainLoop: true); evaluator.RunExitTrap(); return code; }   // its input name is $0 (bash: `/bin/bash: line 2: ...`)
	catch (ExitException ex) { evaluator.RunExitTrap(); return ex.Code; }
	}

// ── interactive REPL mode ────────────────────────────────────────────────────────
evaluator.Env.SetPositionals(positionals);
if (login) SourceLogin(evaluator, noprofile);
else if (!norc) SourceInteractiveRc(evaluator, rcFile);

int histSize = int.TryParse(evaluator.Env.Get("HISTSIZE"), System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var hs) && hs > 0 ? hs : 500;
var historyFile = Path.Combine(
	Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".bash_history");
var history    = new History(historyFile, histSize);
var completion = new CompletionEngine(evaluator.Env, evaluator);
var editor     = new LineEditor(history, completion);
evaluator.History = history;   // for the `history` builtin

int commandCount = 0;
var prompt = new PromptExpander(evaluator.Env, () => history.Count + 1, () => commandCount + 1);

// SIGINT (Ctrl+C) while a command runs: don't kill the shell — request an
// interrupt that loop/command boundaries poll. (During line editing the editor
// sets TreatControlCAsInput, so Ctrl+C is delivered as a key there instead.)
Console.CancelKeyPress += (_, e) => { e.Cancel = true; evaluator.RequestInterrupt(); };

string Ps(string name, string fallback)
	{
	var v = evaluator.Env.Get(name);
	return prompt.Expand(v.Length > 0 ? v : fallback);
	}

while (true)
	{
	// PROMPT_COMMAND runs before each primary prompt.
	var promptCmd = evaluator.Env.Get("PROMPT_COMMAND");
	if (promptCmd.Length > 0) evaluator.RunString(promptCmd, reportErrors: false);

	string? line = editor.ReadLine(Ps("PS1", @"\s-\v\$ "));
	if (line is null) { evaluator.RunExitTrap(); break; }
	if (line.Trim() == "") continue;

	// Accumulate continuation lines until the input parses or fails definitively.
	var source = line;
	Script? ast = null;
	bool aborted = false;
	while (true)
		{
		// Backslash-newline line continuation: the backslash and newline are
		// both removed, splicing the next line directly on (bash semantics).
		if (EndsWithLineContinuation(source))
			{
			var more = editor.ReadLine(Ps("PS2", "> "));
			if (more is null) { aborted = true; break; }
			source = source[..^1] + more;
			continue;
			}
		try
			{
			var tokens = new Lexer(source).Tokenize();
			ast = new Parser(tokens, source).Parse();
			break;
			}
		catch (LexException ex) when (ex.Incomplete)
			{
			var more = editor.ReadLine(Ps("PS2", "> "));
			if (more is null) { aborted = true; break; }
			source += "\n" + more;
			}
		catch (ParseException ex) when (ex.Incomplete)
			{
			var more = editor.ReadLine(Ps("PS2", "> "));
			if (more is null) { aborted = true; break; }
			source += "\n" + more;
			}
		catch (LexException ex)   { Console.Error.WriteLine($"bash: {ex.Message}"); break; }
		catch (ParseException ex) { Console.Error.WriteLine($"bash: {ex.Message}"); break; }
		}

	if (aborted) continue;
	history.Add(source);
	if (ast is null) continue;

	commandCount++;
	evaluator.ClearInterrupt();
	try { evaluator.Execute(ast); }
	catch (ExitException ex) { evaluator.RunExitTrap(); return ex.Code; }
	catch (InterruptException) { Console.WriteLine(); evaluator.Env.LastExitCode = 130; }
	catch (CommandAbortException) { evaluator.Env.LastExitCode = 1; }   // a bad `$( )`: this line is abandoned
	catch (EvalException ex) { Console.Error.WriteLine($"bash: {ex.Message}"); }
	// An interactive shell must survive any filesystem failure: report it and keep the prompt.
	catch (Exception ex) when (ex is IOException or UnauthorizedAccessException
	                            or NotSupportedException or System.Security.SecurityException)
		{ Console.Error.WriteLine($"bash: {ex.Message}"); evaluator.Env.LastExitCode = 1; }
	if (evaluator.Options.OneCmd) { evaluator.RunExitTrap(); return evaluator.Env.LastExitCode; }   // -t: one command, then exit
	}

return 0;

// ── startup-file sourcing ────────────────────────────────────────────────────────

// Login shell: /etc/profile, then the first of ~/.bash_profile, ~/.bash_login, ~/.profile.
// Restricted mode is refused, with a link to ask for it (NotAvailable); status 2, as an invalid option.
static int RefuseRestricted(string how)
	{
	foreach (var line in NotAvailable.Lines("bash: ", $"restricted mode ({how})")) Console.Error.WriteLine(line);
	return 2;
	}

static void SourceLogin(Evaluator eval, bool noprofile)
	{
	if (noprofile) return;
	eval.SourceFile("/etc/profile", silentIfMissing: true);
	foreach (var f in new[] { ".bash_profile", ".bash_login", ".profile" })
		{
		var path = HomeFile(eval, f);
		if (File.Exists(ShellEnvironment.TranslatePath(path))) { eval.SourceFile(path, silentIfMissing: true); break; }
		}
	}

// Interactive non-login shell: /etc/bash.bashrc, then --rcfile or ~/.bashrc.
static void SourceInteractiveRc(Evaluator eval, string? rcFile)
	{
	eval.SourceFile("/etc/bash.bashrc", silentIfMissing: true);
	eval.SourceFile(rcFile ?? HomeFile(eval, ".bashrc"), silentIfMissing: true);
	}

// ~/name as bash finds a startup file: in $HOME (the profile when HOME is unset); a literal `~` in a path is a name
static string HomeFile(Evaluator eval, string name) => ShellEnvironment.TildeHome(eval.Env.Get("HOME")) + "/" + name;

// Non-interactive shells source $BASH_ENV if set.
static void SourceBashEnv(Evaluator eval)
	{
	var bashEnv = eval.Env.Get("BASH_ENV");
	// -p (privileged): bash does not read BASH_ENV; it was read anyway until 2026-10-11
	if (bashEnv.Length > 0 && !eval.Options.Privileged) eval.SourceFile(bashEnv, silentIfMissing: true);
	}

// An odd number of trailing backslashes means the final one escapes the newline.
static bool EndsWithLineContinuation(string s)
	{
	int n = 0, i = s.Length - 1;
	while (i >= 0 && s[i] == '\\') { n++; i--; }
	return n % 2 == 1;
	}
