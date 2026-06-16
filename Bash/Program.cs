using Bash.Lexer;
using Bash.Parser;
using Bash.Evaluator;
using Bash.IO;

Console.OutputEncoding = System.Text.Encoding.UTF8;
Console.Out.NewLine = "\n";    // bash emits LF, not Windows CRLF (installed writers set it too)
Console.Error.NewLine = "\n";

var evaluator = new Evaluator();

// Built-in defaults — set before sourcing startup files so they can be overridden.
const string BashVersion = "5.1.0-koliada";
if (evaluator.Env.Get("BASH_VERSION").Length == 0) evaluator.Env.Set("BASH_VERSION", BashVersion);
if (evaluator.Env.Get("PS1").Length == 0) evaluator.Env.Set("PS1", @"\s-\v\$ ");
if (evaluator.Env.Get("PS2").Length == 0) evaluator.Env.Set("PS2", "> ");

// ── command-line flag parsing ───────────────────────────────────────────────────
string? cFlag = null, scriptPath = null, rcFile = null;
bool login = false, norc = false, noprofile = false;
var positionals = new List<string>();

for (int i = 0; i < args.Length; i++)
	{
	var a = args[i];
	if (cFlag is null && scriptPath is null && a.Length > 1 && a[0] == '-')
		{
		switch (a)
			{
			case "-c":          if (i + 1 < args.Length) cFlag  = args[++i]; break;
			case "-l": case "--login":  login = true; break;
			case "-i":          break;                       // force-interactive: implied
			case "--norc":      norc = true; break;
			case "--noprofile": noprofile = true; break;
			case "--rcfile":    if (i + 1 < args.Length) rcFile = args[++i]; break;
			default:            break;                       // unknown flag — ignore
			}
		}
	else if (scriptPath is null && cFlag is null) scriptPath = a;
	else positionals.Add(a);
	}

// ── -c "command" : run a single command string, non-interactive ──────────────────
// bash -c 'cmd' [name [arg ...]] : name becomes $0, the rest $1.. .
if (cFlag is not null)
	{
	if (positionals.Count > 0)
		{
		evaluator.Env.SetArg0(positionals[0]);
		evaluator.Env.SetPositionals(positionals.Skip(1));
		}
	if (login) SourceLogin(evaluator, noprofile);
	try { var code = evaluator.RunString(cFlag); evaluator.RunExitTrap(); return code; }
	catch (ExitException ex) { evaluator.RunExitTrap(); return ex.Code; }
	}

// ── script file mode: bash script.sh ─────────────────────────────────────────────
if (scriptPath is not null)
	{
	if (!File.Exists(scriptPath)) { Console.Error.WriteLine($"bash: {scriptPath}: No such file or directory"); return 1; }
	// $0 = script path, $1.. = the args following it.
	evaluator.Env.SetArg0(scriptPath);
	evaluator.Env.SetPositionals(positionals);
	// Non-interactive shells source $BASH_ENV if set.
	var bashEnv = evaluator.Env.Get("BASH_ENV");
	if (bashEnv.Length > 0) evaluator.SourceFile(bashEnv, silentIfMissing: true);
	try { var code = evaluator.RunString(File.ReadAllText(scriptPath)); evaluator.RunExitTrap(); return code; }
	catch (ExitException ex) { evaluator.RunExitTrap(); return ex.Code; }
	}

// ── interactive REPL mode ────────────────────────────────────────────────────────
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
			ast = new Parser(tokens).Parse();
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

// An odd number of trailing backslashes means the final one escapes the newline.
static bool EndsWithLineContinuation(string s)
	{
	int n = 0, i = s.Length - 1;
	while (i >= 0 && s[i] == '\\') { n++; i--; }
	return n % 2 == 1;
	}
