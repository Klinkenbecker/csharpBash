namespace Bash.Lexer;

public enum TokenType
	{
	// Literals
	Word,             // any unquoted word or identifier
	SingleQuoted,     // 'text'
	DoubleQuoted,     // "text" (may contain expansions; raw — backslashes preserved for the parser)
	Digit,            // numeric word (for fd redirects: 2>)

	// Operators
	Pipe,             // |
	PipeAmpersand,    // |& (stderr+stdout pipe)
	Ampersand,        // &  (background)
	AmpersandAmpersand, // &&
	PipePipe,         // ||
	Semicolon,        // ;
	SemicolonSemicolon, // ;; (case terminator)

	// Redirects
	Less,             // <
	Greater,          // >
	GreaterGreater,   // >>
	GreaterAmpersand, // >&
	LessAmpersand,    // <&
	LessLess,         // << (heredoc)
	LessLessDash,     // <<- (heredoc strip tabs)
	LessLessLess,     // <<< (here-string)
	LessGreater,      // <> (read/write)
	LessLParen,       // <( process substitution (input)
	GreaterLParen,    // >( process substitution (output) — rejected loudly (DECISIONS 2026-09-04 #6)
	GreaterPipe,      // >| (clobber)
	AmpersandGreater, // &> (stdout+stderr to file)
	AmpersandGreaterGreater, // &>> (append both)

	// Grouping
	LParen,           // (
	RParen,           // )
	LBrace,           // {
	RBrace,           // }

	// (( expr )) arithmetic command — value is the raw interior text
	ArithCommand,

	// Expansions (lexer emits these as distinct tokens)
	Dollar,           // $ (prefix for expansions)
	DollarLParen,     // $( command substitution
	DollarDollarLParen, // $(( arithmetic
	DollarLBrace,     // ${ parameter expansion
	DollarSingleQuote, // $'...' ANSI-C quoting — value is the decoded string
	Backtick,         // ` legacy command substitution

	// Heredoc body (emitted after the command line that contains <<)
	HeredocBody,      // value = the raw heredoc body (delimiter already stripped)

	// Newline / whitespace
	Newline,
	// EOF
	Eof,
	}
