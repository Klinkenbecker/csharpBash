using System.Text;

namespace Bash.Parser;

/// <summary>
/// bash's command printer (print_cmd.c, as bash 5.1 shows a function in `declare -f` and `type`), rebuilt
/// from the AST: compound commands laid out with 4-space indents, `;` and newlines where bash puts them,
/// simple commands rendered from their source text (<see cref="CommandText"/>), words as written. The rules
/// and their probes are in DECISIONS 2026-10-12 "PRINTER P1"; `tests/cases/command_printing.sh` holds the
/// probes. Until 2026-10-12 C#Bash printed a function's source text as written.
/// </summary>
public static class CommandPrinter
	{
	/// <summary>A function as `declare -f` prints it: `NAME () `, `{ `, the body, `}` and its redirections.</summary>
	public static string Function(FunctionDef fd)
		{
		var p = new Printer();
		p.FunctionText(fd, 0, topLevel: true);
		return p.Text;
		}

	/// <summary>A command as `jobs` shows a background job (bash 5.1, measured 2026-10-12): the same layout
	/// at indent 0, except that a brace group or a subshell stays on one line (`{ a; b; } > f`,
	/// `( a; b )`; a compound command inside one still takes its lines) and a pipeline's `!` is not shown.</summary>
	public static string Command(Node node)
		{
		var p = new Printer { JobText = true };
		p.Pipe(node, 0);
		p.FlushHeredocs();
		return p.Text.TrimEnd('\n');
		}

	private sealed class Printer
		{
		private readonly StringBuilder _sb = new();
		private readonly List<string> _pendingHeredocs = [];

		/// <summary>`jobs`' text rather than `declare -f`'s (see <see cref="Command"/>).</summary>
		public bool JobText { get; init; }

		public string Text => _sb.ToString();

		/// <summary>A newline and the next line's indent; here-documents waiting for the end of their line
		/// are written first (bash's print_deferred_heredocs).</summary>
		private void NewLine(int indent)
			{
			FlushHeredocs();
			_sb.Append('\n').Append(' ', indent);
			}

		public void FlushHeredocs()
			{
			if (_pendingHeredocs.Count == 0) return;
			_sb.Append('\n');
			foreach (var b in _pendingHeredocs) _sb.Append(b);
			_pendingHeredocs.Clear();
			_wasHeredoc = true;
			}

		/// <summary>bash's was_heredoc: once a here-document's body is written, the next `;` is left out (the
		/// `fi` of an `if` whose last command had one prints bare) until another simple command starts.</summary>
		private bool _wasHeredoc;

		public void FunctionText(FunctionDef fd, int indent, bool topLevel)
			{
			_sb.Append(topLevel ? "" : "function ").Append(fd.Name).Append(" () ");
			NewLine(indent);
			_sb.Append("{ ");
			// a brace body prints its commands (its redirections are the function's: `} 1>&2`); any other
			// compound body (a subshell, `if` …) is one command inside the braces
			var body = fd.Body is BraceGroup bg ? bg.Body : fd.Body;
			NewLine(indent + 4);
			Body(body, indent + 4, terminateLast: false);
			NewLine(indent);
			_sb.Append('}');
			if (fd.Body is BraceGroup bgr) Redirects(bgr.Redirects);
			Redirects(fd.Redirects);
			}

		/// <summary>The commands of a list, one per line at <paramref name="indent"/> (the first line's indent
		/// is already written). `&amp;&amp;`, `||`, `|` and a `&amp;` that is not last stay on the line.
		/// <paramref name="terminateLast"/>: the last command gets `;` too (loops and `if` bodies).</summary>
		public void Body(Node? node, int indent, bool terminateLast)
			{
			if (node is null) return;
			var items = node is List l ? l.Items : [(node, (ListOperator?)null)];
			for (int k = 0; k < items.Count; k++)
				{
				var (cmd, op) = items[k];
				Pipe(cmd, indent);
				bool last = k == items.Count - 1;
				switch (op)
					{
					case ListOperator.And: _sb.Append(" && "); continue;
					case ListOperator.Or:  _sb.Append(" || "); continue;
					case ListOperator.Background:
						_sb.Append(last ? " &" : " & ");
						if (last) return;
						continue;
					}
				// a here-document's command takes no `;`: its body ends the line
				bool heredoc = _pendingHeredocs.Count > 0 || _wasHeredoc;
				if (last)
					{
					if (terminateLast && !heredoc) _sb.Append(';');
					return;
					}
				if (!heredoc) _sb.Append(';');
				NewLine(indent);
				}
			}

		/// <summary>A pipeline: `time`, `!`, its stages joined by ` | ` (`|&amp;` as ` 2>&amp;1 |`); a stage
		/// with a here-document ends its line with ` |`, the body follows, and the next stage is indented
		/// two spaces (bash).</summary>
		public void Pipe(Node node, int indent)
			{
			if (node is not Pipeline p) { Cmd(node, indent); return; }
			if (p.Timed) _sb.Append(p.TimePosix ? "time -p " : "time ");
			if (p.Negated && !JobText) _sb.Append("! ");
			for (int s = 0; s < p.Commands.Count; s++)
				{
				var (cmd, stderrToo) = p.Commands[s];
				if (cmd is Script { Nodes.Count: 0 }) continue;   // a bare `time`
				Cmd(cmd, indent);
				if (s == p.Commands.Count - 1) break;
				if (stderrToo) _sb.Append(" 2>&1");
				if (_pendingHeredocs.Count > 0) { _sb.Append(" |"); FlushHeredocs(); _sb.Append("  "); }
				else _sb.Append(" | ");
				}
			}

		/// <summary>One command (the indent of its first line is already written).</summary>
		public void Cmd(Node node, int indent)
			{
			switch (node)
				{
				case ConditionalExpression ce:
					// printed from its expression, as bash does: a bare word gets `-n` (`[[ $x ]]` → `[[ -n $x ]]`)
					_wasHeredoc = false;
					_sb.Append("[[ ").Append(Cond(ce.Expr)).Append(" ]]");
					break;
				case SimpleCommand or ArrayElementAssign or ArrayCompoundAssign or ArithmeticCommand:
					Leaf(node);
					break;
				case Pipeline or List:
					Inline(node, indent);
					break;
				case BraceGroup bg when JobText:
					_sb.Append("{ ");
					Inline(bg.Body, indent);
					_sb.Append("; }");
					Redirects(bg.Redirects);
					break;
				case Subshell sub when JobText:
					_sb.Append("( ");
					Inline(sub.Body, indent);
					_sb.Append(" )");
					Redirects(sub.Redirects);
					break;
				case BraceGroup bg:
					_sb.Append("{ ");
					NewLine(indent + 4);
					Body(bg.Body, indent + 4, terminateLast: false);
					NewLine(indent);
					_sb.Append('}');
					Redirects(bg.Redirects);
					break;
				case Subshell sub:
					// `( a;` then the later commands at the CURRENT indent, `… )` (bash)
					_sb.Append("( ");
					Body(sub.Body, indent, terminateLast: false);
					_sb.Append(" )");
					Redirects(sub.Redirects);
					break;
				case IfCommand ic:
					If(ic.Condition, ic.Then, ic.Elifs, 0, ic.Else, indent);
					Redirects(ic.Redirects);
					break;
				case WhileCommand wc:
					_sb.Append(wc.Until ? "until " : "while ");
					Inline(wc.Condition, indent);
					_sb.Append("; do");
					NewLine(indent + 4);
					Body(wc.Body, indent + 4, terminateLast: true);
					NewLine(indent);
					_sb.Append("done");
					Redirects(wc.Redirects);
					break;
				case ForCommand fc:
					_sb.Append("for ").Append(fc.Variable).Append(" in ");
					_sb.Append(fc.Words.Count == 0 ? "\"$@\"" : string.Join(" ", fc.Words.Select(WordText)));
					_sb.Append(';');
					NewLine(indent);
					_sb.Append("do");
					NewLine(indent + 4);
					Body(fc.Body, indent + 4, terminateLast: true);
					NewLine(indent);
					_sb.Append("done");
					Redirects(fc.Redirects);
					break;
				case ArithForCommand af:
					// each part with its front trimmed, its end as written (`i++ ))`), an empty one as `1` (bash):
					// `Heads` holds `((part))`
					_sb.Append("for ((").Append(string.Join("; ", af.Heads.Select(h => h.Length >= 4 && h[2..^2].Trim().Length > 0 ? h[2..^2] : "1"))).Append("))");
					NewLine(indent);
					_sb.Append("do");
					NewLine(indent + 4);
					Body(af.Body, indent + 4, terminateLast: true);
					NewLine(indent);
					_sb.Append("done");
					Redirects(af.Redirects);
					break;
				case CaseCommand cc:
					_sb.Append("case ").Append(WordText(cc.Subject)).Append(" in ");
					foreach (var item in cc.Items)
						{
						NewLine(indent + 4);
						_sb.Append(string.Join(" | ", item.Patterns.Select(WordText))).Append(')');
						if (item.Body is null) { FlushHeredocs(); _sb.Append('\n'); }   // an empty body: an empty line
						else
							{
							NewLine(indent + 8);
							Body(item.Body, indent + 8, terminateLast: false);
							}
						NewLine(indent + 4);
						_sb.Append(item.Terminator switch { CaseTerminator.FallThrough => ";&", CaseTerminator.TestNext => ";;&", _ => ";;" });
						}
					NewLine(indent);
					_sb.Append("esac");
					Redirects(cc.Redirects);
					break;
				case FunctionDef fd:
					FunctionText(fd, indent, topLevel: false);
					break;
				default:
					_sb.Append(node.Span.IsKnown ? node.Span.Text : "");
					break;
				}
			}

		/// <summary>`if C; then` … `else` … `fi`; an `elif` is printed as `else` and a nested `if` (bash).</summary>
		private void If(Node cond, Node then, List<(Node Condition, Node Body)> elifs, int e, Node? elseBody, int indent)
			{
			_sb.Append("if ");
			Inline(cond, indent);
			_sb.Append("; then");
			NewLine(indent + 4);
			Body(then, indent + 4, terminateLast: true);
			if (e < elifs.Count)
				{
				NewLine(indent);
				_sb.Append("else");
				NewLine(indent + 4);
				If(elifs[e].Condition, elifs[e].Body, elifs, e + 1, elseBody, indent + 4);
				_sb.Append(';');
				}
			else if (elseBody is not null)
				{
				NewLine(indent);
				_sb.Append("else");
				NewLine(indent + 4);
				Body(elseBody, indent + 4, terminateLast: true);
				}
			NewLine(indent);
			_sb.Append("fi");
			}

		/// <summary>A list on one line (a condition): `a && b`, `a; b`.</summary>
		private void Inline(Node node, int indent)
			{
			if (node is Pipeline) { Pipe(node, indent); return; }
			if (node is not List l) { Cmd(node, indent); return; }
			for (int k = 0; k < l.Items.Count; k++)
				{
				var (cmd, op) = l.Items[k];
				Pipe(cmd, indent);
				if (k == l.Items.Count - 1) { if (op == ListOperator.Background) _sb.Append(" &"); break; }
				_sb.Append(op switch { ListOperator.And => " && ", ListOperator.Or => " || ", ListOperator.Background => " & ", _ => "; " });
				}
			}

		/// <summary>A simple command, an assignment or `(( ))`: its source text as bash renders it; its here-
		/// documents wait for the end of the line.</summary>
		private void Leaf(Node node)
			{
			_wasHeredoc = false;
			_sb.Append(CommandText.RenderParts(node.Span, out var bodies));
			_pendingHeredocs.AddRange(bodies);
			}

		/// <summary>A `[[ ]]` expression from its tree (bash's print_cond_node): a bare word is `-n WORD`;
		/// parentheses where they were written.</summary>
		private static string Cond(CondExpr e) => e switch
			{
			CondWord w   => "-n " + WordText(w.Value),
			CondUnary u  => u.Op + " " + WordText(u.Operand),
			CondBinary b => WordText(b.Left) + " " + b.Op + " " + WordText(b.Right),
			CondNot n    => "! " + Cond(n.Operand),
			CondAnd a    => Cond(a.Left) + " && " + Cond(a.Right),
			CondOr o     => Cond(o.Left) + " || " + Cond(o.Right),
			CondGroup g  => "( " + Cond(g.Inner) + " )",
			_            => "",
			};

		private void Redirects(List<Redirect> redirects)
			{
			foreach (var r in redirects) _sb.Append(' ').Append(RedirectText(r));
			}

		private static string WordText(Word w) => w.Raw is { } raw ? CommandText.PrintWord(raw) : "";

		/// <summary>A redirection on a compound command, as bash prints it (rule 8).</summary>
		private static string RedirectText(Redirect r)
			{
			string fd = r.Fd?.ToString() ?? "", t = WordText(r.Target);
			return r.Kind switch
				{
				RedirectKind.Input      => $"{fd}< {t}",
				RedirectKind.Output     => $"{fd}> {t}",
				RedirectKind.Append     => $"{fd}>> {t}",
				RedirectKind.Clobber    => $"{fd}>| {t}",
				RedirectKind.InputDup   => $"{(fd.Length > 0 ? fd : "0")}<&{t}",
				RedirectKind.OutputDup  => $"{(fd.Length > 0 ? fd : "1")}>&{t}",
				RedirectKind.ReadWrite  => $"{fd}<> {t}",
				RedirectKind.HereString => $"{fd}<<< {t}",
				RedirectKind.OutputBoth => $"&> {t}",
				RedirectKind.AppendBoth => $"&>> {t}",
				_                       => $"{fd}<< {t}",   // a here-document on a compound command: rare, not measured
				};
			}
		}
	}
