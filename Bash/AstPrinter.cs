using Bash.Parser;

namespace Bash;

/// <summary>Renders an AST as an indented text tree for debugging.</summary>
public static class AstPrinter
	{
	public static string Print(Node node)
		{
		var sb = new System.Text.StringBuilder();
		PrintNode(node, sb, 0);
		return sb.ToString();
		}

	private static void PrintNode(Node node, System.Text.StringBuilder sb, int depth)
		{
		string pad = new(' ', depth * 2);
		switch (node)
			{
			case Script s:
				sb.AppendLine($"{pad}Script");
				foreach (var n in s.Nodes) PrintNode(n, sb, depth + 1);
				break;

			case SimpleCommand cmd:
				sb.AppendLine($"{pad}SimpleCmd");
				foreach (var (name, val) in cmd.Assignments)
					sb.AppendLine($"{pad}  assign: {name}={PrintWord(val)}");
				if (cmd.Name is not null)
					sb.AppendLine($"{pad}  name: {PrintWord(cmd.Name)}");
				foreach (var arg in cmd.Args)
					sb.AppendLine($"{pad}  arg: {PrintWord(arg)}");
				foreach (var r in cmd.Redirects)
					sb.AppendLine($"{pad}  {PrintRedirect(r)}");
				break;

			case Pipeline p:
				sb.AppendLine($"{pad}Pipeline{(p.Negated ? " !" : "")}");
				foreach (var (cmd, stderr) in p.Commands)
					{
					sb.AppendLine($"{pad}  [{(stderr ? "|&" : "|")}]");
					PrintNode(cmd, sb, depth + 2);
					}
				break;

			case List lst:
				sb.AppendLine($"{pad}List");
				foreach (var (n, op) in lst.Items)
					{
					PrintNode(n, sb, depth + 1);
					if (op is not null)
						sb.AppendLine($"{pad}  op: {op}");
					}
				break;

			case BraceGroup bg:
				sb.AppendLine($"{pad}BraceGroup");
				PrintNode(bg.Body, sb, depth + 1);
				foreach (var r in bg.Redirects) sb.AppendLine($"{pad}  {PrintRedirect(r)}");
				break;

			case Subshell ss:
				sb.AppendLine($"{pad}Subshell");
				PrintNode(ss.Body, sb, depth + 1);
				foreach (var r in ss.Redirects) sb.AppendLine($"{pad}  {PrintRedirect(r)}");
				break;

			case IfCommand ic:
				sb.AppendLine($"{pad}If");
				sb.AppendLine($"{pad}  condition:");
				PrintNode(ic.Condition, sb, depth + 2);
				sb.AppendLine($"{pad}  then:");
				PrintNode(ic.Then, sb, depth + 2);
				foreach (var (c, b) in ic.Elifs)
					{
					sb.AppendLine($"{pad}  elif:");
					PrintNode(c, sb, depth + 2);
					sb.AppendLine($"{pad}  then:");
					PrintNode(b, sb, depth + 2);
					}
				if (ic.Else is not null)
					{
					sb.AppendLine($"{pad}  else:");
					PrintNode(ic.Else, sb, depth + 2);
					}
				break;

			case WhileCommand wc:
				sb.AppendLine($"{pad}{(wc.Until ? "Until" : "While")}");
				sb.AppendLine($"{pad}  condition:");
				PrintNode(wc.Condition, sb, depth + 2);
				sb.AppendLine($"{pad}  body:");
				PrintNode(wc.Body, sb, depth + 2);
				break;

			case ForCommand fc:
				sb.AppendLine($"{pad}For {fc.Variable} in [{string.Join(", ", fc.Words.Select(PrintWord))}]");
				PrintNode(fc.Body, sb, depth + 1);
				break;

			case CaseCommand cc:
				sb.AppendLine($"{pad}Case {PrintWord(cc.Subject)}");
				foreach (var item in cc.Items)
					{
					sb.AppendLine($"{pad}  patterns: {string.Join(" | ", item.Patterns.Select(PrintWord))}");
					if (item.Body is not null)
						PrintNode(item.Body, sb, depth + 2);
					}
				break;

			case FunctionDef fd:
				sb.AppendLine($"{pad}Function {fd.Name}");
				PrintNode(fd.Body, sb, depth + 1);
				break;

			case ConditionalExpression ce:
				sb.AppendLine($"{pad}[[");
				sb.AppendLine($"{pad}  {PrintCondExpr(ce.Expr)}");
				break;

			default:
				sb.AppendLine($"{pad}{node.GetType().Name}");
				break;
			}
		}

	private static string PrintWord(Word w) =>
		string.Concat(w.Parts.Select(PrintPart));

	private static string PrintPart(WordPart p) => p switch
		{
		LiteralPart l              => l.Value,
		SingleQuotedPart s         => $"'{s.Value}'",
		AnsiCQuotedPart a          => $"$'{a.Value}'",
		HeredocBodyPart h          => $"<<HEREDOC\n{h.Body}",
		TildePart t                => $"~{t.Suffix}",
		DoubleQuotedPart d         => $"\"{string.Concat(d.Parts.Select(PrintPart))}\"",
		VarExpansionPart v         => $"${v.Name}",
		BraceExpansionPart b       => "${" + b.Raw + "}",
		CommandSubstitutionPart cs => $"$({Print(cs.Command).Trim()})",
		ArithmeticExpansionPart a  => $"$(({a.Expression}))",
		_ => "?"
		};

	private static string PrintRedirect(Redirect r)
		{
		var fd = r.Fd.HasValue ? r.Fd.Value.ToString() : "";
		var op = r.Kind switch
			{
			RedirectKind.Input      => "<",
			RedirectKind.Output     => ">",
			RedirectKind.Append     => ">>",
			RedirectKind.Clobber    => ">|",
			RedirectKind.InputDup   => "<&",
			RedirectKind.OutputDup  => ">&",
			RedirectKind.ReadWrite  => "<>",
			RedirectKind.Heredoc    => "<<",
			RedirectKind.HeredocStrip => "<<-",
			_ => "?"
			};
		return $"redirect: {fd}{op}{PrintWord(r.Target)}";
		}

	private static string PrintCondExpr(CondExpr expr) => expr switch
		{
		CondAnd a    => $"({PrintCondExpr(a.Left)} && {PrintCondExpr(a.Right)})",
		CondOr o     => $"({PrintCondExpr(o.Left)} || {PrintCondExpr(o.Right)})",
		CondNot n    => $"!{PrintCondExpr(n.Operand)}",
		CondBinary b => $"{PrintWord(b.Left)} {b.Op} {PrintWord(b.Right)}",
		CondUnary u  => $"{u.Op} {PrintWord(u.Operand)}",
		CondWord w   => PrintWord(w.Value),
		_ => "?"
		};
	}
