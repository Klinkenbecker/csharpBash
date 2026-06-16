namespace Bash.Parser;

// ── Word parts ────────────────────────────────────────────────────────────────
// A "word" in bash is a sequence of parts that get concatenated after expansion.

public abstract record WordPart;

/// <summary>Literal text with no expansion.</summary>
public sealed record LiteralPart(string Value) : WordPart;

/// <summary>Single-quoted string — no expansion, value is literal.</summary>
public sealed record SingleQuotedPart(string Value) : WordPart;

/// <summary>$'...' ANSI-C quoted string — already decoded by the lexer.</summary>
public sealed record AnsiCQuotedPart(string Value) : WordPart;

/// <summary>Double-quoted string — may contain sub-parts with expansions.</summary>
public sealed record DoubleQuotedPart(List<WordPart> Parts) : WordPart;

/// <summary>$VAR or $N (positional).</summary>
public sealed record VarExpansionPart(string Name) : WordPart;

/// <summary>${VAR}, ${VAR:-default}, ${#VAR}, ${VAR/pat/rep}, etc.</summary>
public sealed record BraceExpansionPart(string Raw) : WordPart;

/// <summary>$(command) or `command`.</summary>
public sealed record CommandSubstitutionPart(Node Command) : WordPart;

/// <summary>$(( expr )).</summary>
public sealed record ArithmeticExpansionPart(string Expression) : WordPart;

/// <summary>~ or ~/path — tilde expansion.</summary>
public sealed record TildePart(string Suffix) : WordPart;

/// <summary>Heredoc body — the raw text between delimiter lines.</summary>
public sealed record HeredocBodyPart(string Body) : WordPart;

/// <summary>A fully assembled word composed of one or more parts.</summary>
public sealed record Word(List<WordPart> Parts)
	{
	/// <summary>Convenience: single literal word.</summary>
	public static Word Literal(string s) => new([new LiteralPart(s)]);
	}

// ── Redirects ─────────────────────────────────────────────────────────────────

public enum RedirectKind
	{
	Input,          // <
	Output,         // >
	Append,         // >>
	Clobber,        // >|
	InputDup,       // <&
	OutputDup,      // >&
	ReadWrite,      // <>
	Heredoc,        // <<
	HeredocStrip,   // <<-
	}

public sealed record Redirect(
	int? Fd,            // explicit fd number (e.g. 2 in 2>&1); null = default (0 or 1)
	RedirectKind Kind,
	Word Target);       // file path, fd number, or heredoc delimiter

// ── Base node ─────────────────────────────────────────────────────────────────

public abstract record Node;

// ── Simple command ────────────────────────────────────────────────────────────

/// <summary>
/// A single command: optional var assignments, a command word, arguments, redirects.
/// e.g.  FOO=bar cmd arg1 arg2 >out.txt
/// </summary>
public sealed record SimpleCommand(
	List<(string Name, Word Value)> Assignments,
	Word? Name,
	List<Word> Args,
	List<Redirect> Redirects) : Node;

/// <summary>Indexed array element assignment: arr[n]=val</summary>
public sealed record ArrayElementAssign(
	string ArrayName,
	Word Index,
	Word Value,
	List<Redirect> Redirects) : Node;

/// <summary>Compound array assignment: arr=(a b c)</summary>
public sealed record ArrayCompoundAssign(
	string ArrayName,
	List<Word> Values,
	List<Redirect> Redirects) : Node;

// ── Pipeline ──────────────────────────────────────────────────────────────────

/// <summary>
/// One or more commands connected by | or |&.
/// Negated = prefixed with !.
/// </summary>
public sealed record Pipeline(
	List<(Node Command, bool StderrToo)> Commands,   // StderrToo = |&
	bool Negated) : Node;

// ── Lists (&&, ||, ;, &, newline) ─────────────────────────────────────────────

public enum ListOperator { And, Or, Sequential, Background }

/// <summary>
/// A sequence of pipelines joined by &&, ||, ;, or &.
/// </summary>
public sealed record List(List<(Node Pipeline, ListOperator? Op)> Items) : Node;

// ── Compound commands ─────────────────────────────────────────────────────────

/// <summary>{ list; } — runs in current shell.</summary>
public sealed record BraceGroup(Node Body, List<Redirect> Redirects) : Node;

/// <summary>( list ) — runs in a subshell.</summary>
public sealed record Subshell(Node Body, List<Redirect> Redirects) : Node;

/// <summary>if/then/elif/else/fi</summary>
public sealed record IfCommand(
	Node Condition,
	Node Then,
	List<(Node Condition, Node Body)> Elifs,
	Node? Else,
	List<Redirect> Redirects) : Node;

/// <summary>while condition; do body; done</summary>
public sealed record WhileCommand(
	Node Condition,
	Node Body,
	bool Until,             // true = until loop
	List<Redirect> Redirects) : Node;

/// <summary>for name in words; do body; done</summary>
public sealed record ForCommand(
	string Variable,
	List<Word> Words,       // empty = "$@"
	Node Body,
	List<Redirect> Redirects) : Node;

/// <summary>case word in pattern) list;; esac</summary>
public sealed record CaseCommand(
	Word Subject,
	List<CaseItem> Items,
	List<Redirect> Redirects) : Node;

public sealed record CaseItem(
	List<Word> Patterns,
	Node? Body);            // null = empty body

/// <summary>[[ expression ]]</summary>
public sealed record ConditionalExpression(CondExpr Expr) : Node;

// ── Conditional expressions ([[ ]]) ──────────────────────────────────────────

public abstract record CondExpr;
public sealed record CondAnd(CondExpr Left, CondExpr Right) : CondExpr;
public sealed record CondOr(CondExpr Left, CondExpr Right) : CondExpr;
public sealed record CondNot(CondExpr Operand) : CondExpr;
public sealed record CondBinary(string Op, Word Left, Word Right) : CondExpr;
public sealed record CondUnary(string Op, Word Operand) : CondExpr;
public sealed record CondWord(Word Value) : CondExpr;   // bare word — true if non-empty

// ── Functions ─────────────────────────────────────────────────────────────────

/// <summary>name() compound-command [redirect]</summary>
public sealed record FunctionDef(
	string Name,
	Node Body,
	List<Redirect> Redirects) : Node;

// ── Script root ───────────────────────────────────────────────────────────────

/// <summary>Top-level sequence of nodes (the whole script or REPL input).</summary>
public sealed record Script(List<Node> Nodes) : Node;
