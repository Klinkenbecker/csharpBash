namespace Bash.Lexer;

public enum TokenType
	{
	// Literals
	Word,             // any unquoted word or identifier
	SingleQuoted,     // 'text'
	DoubleQuoted,     // "text" (may contain expansions)
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
	LessGreater,      // <> (read/write)
	GreaterPipe,      // >| (clobber)

	// Grouping
	LParen,           // (
	RParen,           // )
	LBrace,           // {
	RBrace,           // }

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
