using Bash.Lexer;
using Bash.Parser;
using Bash.Evaluator;
using Bash.IO;

// UTF-8 output. Setting the encoding needs a console handle; a host that spawns us with
// pipes and no console (Claude Code's Bash tool, CI runners) has none — then stdout is
// already a byte pipe and the default UTF-8 writer is what we want anyway.
try { Console.OutputEncoding = ShellEncoding.Utf8; } catch (Exception) { }   // byte-transparent (2026-09-12)
try { Console.InputEncoding  = ShellEncoding.Utf8; } catch (Exception) { }
Console.Out.NewLine = "\n";    // bash emits LF, not Windows CRLF (installed writers set it too)
Console.Error.NewLine = "\n";
ConsoleMux.Install();          // per-thread console streams (pipeline stages, captures, jobs)

const string BashVersion = "5.1.0-koliada";
const string BashVersionLong = "bash, version 5.1.0-koliada(1)-release (x86_64-pc-msys)";

// The build stamp: "1.0.0+hg.<hash>[+] <rev>[+]", written by Bash.csproj from `hg id` at build
// time (+ = uncommitted tree). Printed by --version and exposed to scripts as CSHARPBASH_BUILD
// (the whole string) and CSHARPBASH_REV (the integer revision, comparable with (( ))), so a
// check can bind to a VALUE rather than to an output line position (web-d6, 2026-09-08).
static string BuildStamp() => System.Reflection.Assembly.GetEntryAssembly()
	?.GetCustomAttributes(typeof(System.Reflection.AssemblyInformationalVersionAttribute), false)
	.OfType<System.Reflection.AssemblyInformationalVersionAttribute>().FirstOrDefault()?.InformationalVersion ?? "unknown";
static int BuildRev()
	{
	var last = BuildStamp().Split(' ', StringSplitOptions.RemoveEmptyEntries).LastOrDefault() ?? "";
	return int.TryParse(last.TrimEnd('+'), out var r) ? r : 0;
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

int ai = 0;
for (; ai < args.Length; ai++)
	{
	var a = args[ai];
	if (a == "--") { ai++; break; }
	if (a == "-")  { readStdin = true; ai++; break; }           // "-" = end of options, read stdin
	if (a.Length < 2 || (a[0] != '-' && a[0] != '+')) break;    // first non-option

	if (a.StartsWith("--"))
		{
		switch (a)
			{
			case "--login":     login = true; break;
			case "--norc":      norc = true; break;
			case "--noprofile": noprofile = true; break;
			case "--noediting": case "--posix": case "--restricted": case "--verbose": case "--debugger": break;
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
			case 'r': break;                                        // restricted: not implemented
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
bool interactive = forceInteractive || (cFlagCmd is null && scriptPath is null && !Console.IsInputRedirected);
evaluator.Options.Interactive   = interactive;
evaluator.Options.CommandString = cFlagCmd is not null;
evaluator.Options.ReadStdin     = cFlagCmd is null && scriptPath is null;
evaluator.Options.LoginShell    = login;
foreach (var (f, en) in pendingSetFlags)
	if (!evaluator.Options.Apply(f, en)) { Console.Error.WriteLine($"bash: -{f}: invalid option"); return 2; }
foreach (var (n, en) in pendingLongOpts)
	if (!evaluator.Options.ApplyLong(n, en)) { Console.Error.WriteLine($"bash: {n}: invalid option name"); return 2; }
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

// ── -c "command" : run a single command string, non-interactive ──────────────────
// bash -c 'cmd' [name [arg ...]] : name becomes $0, the rest $1.. .
if (cFlagCmd is not null)
	{
	if (positionals.Count > 0)
		{
		evaluator.Env.SetArg0(positionals[0]);
		evaluator.Env.SetPositionals(positionals.Skip(1));
		}
	if (login) SourceLogin(evaluator, noprofile);
	else SourceBashEnv(evaluator);
	try { var code = evaluator.RunString(cFlagCmd, origin: "-c"); evaluator.RunExitTrap(); return code; }
	catch (ExitException ex) { evaluator.RunExitTrap(); return ex.Code; }
	}

// ── script file mode: bash script.sh ─────────────────────────────────────────────
if (scriptPath is not null)
	{
	var resolved = ShellEnvironment.TranslatePath(scriptPath);
	if (!File.Exists(resolved)) { Console.Error.WriteLine($"bash: {scriptPath}: No such file or directory"); return 127; }
	// $0 = script path, $1.. = the args following it.
	evaluator.Env.SetArg0(scriptPath);
	evaluator.Env.SetPositionals(positionals);
	evaluator.Env.SetArrayFromList("BASH_SOURCE", [scriptPath]);
	evaluator.Env.SetArrayFromList("FUNCNAME", ["main"]);
	if (login) SourceLogin(evaluator, noprofile);
	else SourceBashEnv(evaluator);
	// The read is inside the try: a locked, blocked (AV) or vanished script must be an error
	// message and a status, never an unhandled exception (2026-09-12).
	try { var code = evaluator.RunString(ShellEncoding.ReadAllText(resolved), origin: scriptPath); evaluator.RunExitTrap(); return code; }
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
	try { var code = evaluator.RunString(src, origin: "bash"); evaluator.RunExitTrap(); return code; }
	catch (ExitException ex) { evaluator.RunExitTrap(); return ex.Code; }
	}

// ── interactive REPL mode ────────────────────────────────────────────────────────
evaluator.Env.SetPositionals(positionals);
if (login) SourceLogin(evaluator, noprofile);
else if (!norc) SourceInteractiveRc(evaluator, rcFile);

int histSize = int.TryParse(evaluator.Env.Get("HISTSIZE"), out var hs) && hs > 0 ? hs : 500;
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
	catch (EvalException ex) { Console.Error.WriteLine($"bash: {ex.Message}"); }
	// An interactive shell must survive any filesystem failure: report it and keep the prompt.
	catch (Exception ex) when (ex is IOException or UnauthorizedAccessException
	                            or NotSupportedException or System.Security.SecurityException)
		{ Console.Error.WriteLine($"bash: {ex.Message}"); evaluator.Env.LastExitCode = 1; }
	}

return 0;

// ── startup-file sourcing ────────────────────────────────────────────────────────

// Login shell: /etc/profile, then the first of ~/.bash_profile, ~/.bash_login, ~/.profile.
static void SourceLogin(Evaluator eval, bool noprofile)
	{
	if (noprofile) return;
	eval.SourceFile("/etc/profile", silentIfMissing: true);
	foreach (var f in new[] { "~/.bash_profile", "~/.bash_login", "~/.profile" })
		{
		if (File.Exists(ShellEnvironment.TranslatePath(f))) { eval.SourceFile(f, silentIfMissing: true); break; }
		}
	}

// Interactive non-login shell: /etc/bash.bashrc, then --rcfile or ~/.bashrc.
static void SourceInteractiveRc(Evaluator eval, string? rcFile)
	{
	eval.SourceFile("/etc/bash.bashrc", silentIfMissing: true);
	eval.SourceFile(rcFile ?? "~/.bashrc", silentIfMissing: true);
	}

// Non-interactive shells source $BASH_ENV if set.
static void SourceBashEnv(Evaluator eval)
	{
	var bashEnv = eval.Env.Get("BASH_ENV");
	if (bashEnv.Length > 0) eval.SourceFile(bashEnv, silentIfMissing: true);
	}

// An odd number of trailing backslashes means the final one escapes the newline.
static bool EndsWithLineContinuation(string s)
	{
	int n = 0, i = s.Length - 1;
	while (i >= 0 && s[i] == '\\') { n++; i--; }
	return n % 2 == 1;
	}
