using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Bash.Evaluator;

/// <summary>
/// Evaluates a jq filter (DECISIONS 2026-10-05, the in-process jq subset). Every filter is a
/// generator: one input, a stream of outputs. Assignment (`=`, `|=`, `op=`, `del`) works on PATHS,
/// as jq's does: <see cref="Paths"/> yields each (path, value) a path expression selects.
/// </summary>
internal sealed class JqInterp(JObj env)
	{
	// ── the builtins in scope, by name/arity (the parser refuses everything else) ─────────────────
	private static readonly HashSet<string> Supported =
		[
		"empty/0", "not/0", "length/0", "keys/0", "keys_unsorted/0", "values/0", "has/1", "map/1", "map_values/1",
		"to_entries/0", "from_entries/0", "with_entries/1", "add/0", "any/0", "any/1", "any/2", "all/0", "all/1", "all/2",
		"select/1", "recurse/0", "sort/0", "sort_by/1", "group_by/1", "unique/0", "unique_by/1",
		"min/0", "max/0", "min_by/1", "max_by/1", "reverse/0", "first/0", "last/0", "first/1", "last/1",
		"flatten/0", "flatten/1", "range/1", "range/2", "range/3", "tostring/0", "tonumber/0", "type/0",
		"tojson/0", "fromjson/0", "split/1", "join/1", "test/1", "test/2", "sub/2", "sub/3", "gsub/2", "gsub/3",
		"startswith/1", "endswith/1", "ltrimstr/1", "rtrimstr/1", "ascii_downcase/0", "ascii_upcase/0",
		"contains/1", "error/0", "error/1", "env/0", "del/1", "floor/0", "ceil/0", "round/0", "sqrt/0", "fabs/0",
		"arrays/0", "objects/0", "iterables/0", "booleans/0", "numbers/0", "strings/0", "nulls/0", "scalars/0",
		];

	public static bool IsSupported(string name, int arity) => Supported.Contains($"{name}/{arity}");

	/// <summary>Every builtin jq 1.6 defines (its own `builtins`): a name/arity outside it is jq's
	/// compile error "is not defined"; one inside it but not <see cref="Supported"/> is deferred.</summary>
	private static readonly HashSet<string> Jq16Builtins = [.. "IN/1 IN/2 INDEX/1 INDEX/2 JOIN/2 JOIN/3 JOIN/4 acos/0 acosh/0 add/0 all/0 all/1 all/2 any/0 any/1 any/2 arrays/0 ascii_downcase/0 ascii_upcase/0 asin/0 asinh/0 atan/0 atan2/2 atanh/0 booleans/0 bsearch/1 builtins/0 capture/1 capture/2 cbrt/0 ceil/0 combinations/0 combinations/1 contains/1 copysign/2 cos/0 cosh/0 debug/0 del/1 delpaths/1 drem/2 empty/0 endswith/1 env/0 erf/0 erfc/0 error/0 error/1 exp/0 exp10/0 exp2/0 explode/0 expm1/0 fabs/0 fdim/2 finites/0 first/0 first/1 flatten/0 flatten/1 floor/0 fma/3 fmax/2 fmin/2 fmod/2 format/1 frexp/0 from_entries/0 fromdate/0 fromdateiso8601/0 fromjson/0 fromstream/1 gamma/0 get_jq_origin/0 get_prog_origin/0 get_search_list/0 getpath/1 gmtime/0 group_by/1 gsub/2 gsub/3 halt/0 halt_error/0 halt_error/1 has/1 hypot/2 implode/0 in/1 index/1 indices/1 infinite/0 input/0 input_filename/0 input_line_number/0 inputs/0 inside/1 isempty/1 isfinite/0 isinfinite/0 isnan/0 isnormal/0 iterables/0 j0/0 j1/0 jn/2 join/1 keys/0 keys_unsorted/0 last/0 last/1 ldexp/2 leaf_paths/0 length/0 lgamma/0 lgamma_r/0 limit/2 localtime/0 log/0 log10/0 log1p/0 log2/0 logb/0 ltrimstr/1 map/1 map_values/1 match/1 match/2 max/0 max_by/1 min/0 min_by/1 mktime/0 modf/0 modulemeta/0 nan/0 nearbyint/0 nextafter/2 nexttoward/2 normals/0 not/0 now/0 nth/1 nth/2 nulls/0 numbers/0 objects/0 path/1 paths/0 paths/1 pow/2 pow10/0 range/1 range/2 range/3 recurse/0 recurse/1 recurse/2 recurse_down/0 remainder/2 repeat/1 reverse/0 rindex/1 rint/0 round/0 rtrimstr/1 scalars/0 scalars_or_empty/0 scalb/2 scalbln/2 scan/1 select/1 setpath/2 significand/0 sin/0 sinh/0 sort/0 sort_by/1 split/1 split/2 splits/1 splits/2 sqrt/0 startswith/1 stderr/0 strflocaltime/1 strftime/1 strings/0 strptime/1 sub/2 sub/3 tan/0 tanh/0 test/1 test/2 tgamma/0 to_entries/0 todate/0 todateiso8601/0 tojson/0 tonumber/0 tostream/0 tostring/0 transpose/0 trunc/0 truncate_stream/1 type/0 unique/0 unique_by/1 until/2 utf8bytelength/0 values/0 walk/1 while/2 with_entries/1 y0/0 y1/0 yn/2".Split(' ')];

	public static bool IsJqBuiltin(string name, int arity) => Jq16Builtins.Contains($"{name}/{arity}");

	private sealed record Vars(string Name, object? Value, Vars? Next);

	public IEnumerable<object?> Run(JqNode filter, object? input, IReadOnlyDictionary<string, object?> named)
		{
		Vars? vars = null;
		foreach (var (k, v) in named) vars = new Vars(k, v, vars);
		return Eval(filter, input, vars);
		}

	// ── evaluation ──────────────────────────────────────────────────────────────────────────

	private IEnumerable<object?> Eval(JqNode n, object? input, Vars? vars)
		{
		switch (n)
			{
			case JIdentity: return [input];
			case JRecurse: return Recurse(input);
			case JLiteral l: return [l.Value];
			case JField f: return EvalField(f, input, vars);
			case JIndex x: return EvalIndex(x, input, vars);
			case JSlice s: return EvalSlice(s, input, vars);
			case JIterate it: return EvalIterate(it, input, vars);
			case JTry t: return EvalTry(t, input, vars);
			case JString s: return EvalString(s, input, vars);
			case JFormat f: return [Format(f.Name, input)];
			case JArray a: return [a.Body is null ? new List<object?>() : Eval(a.Body, input, vars).ToList()];
			case JObject o: return EvalObject(o, 0, new JObj(), input, vars);
			case JNeg ng: return EvalNeg(ng, input, vars);
			case JPipe p: return EvalPipe(p, input, vars);
			case JComma c: return EvalComma(c, input, vars);
			case JBinary b: return EvalBinary(b, input, vars);
			case JAnd a: return EvalAnd(a, input, vars);
			case JOr o: return EvalOr(o, input, vars);
			case JAlt a: return EvalAlt(a, input, vars);
			case JAssign a: return EvalAssign(a, input, vars);
			case JIf i: return EvalIf(i, input, vars);
			case JReduce r: return EvalReduce(r, input, vars);
			case JBind b: return EvalBind(b, input, vars);
			case JVar v: return [Lookup(v.Name, vars)];
			case JCall c: return Call(c, input, vars);
			}
		throw new JqError($"internal: unhandled {n.GetType().Name}");
		}

	private object? Lookup(string name, Vars? vars)
		{
		for (var v = vars; v is not null; v = v.Next) if (v.Name == name) return v.Value;
		if (name == "ENV") return env;
		throw new JqError($"${name} is not defined");
		}

	private static IEnumerable<object?> Recurse(object? v)
		{
		yield return v;
		if (v is List<object?> a) { foreach (var x in a) foreach (var y in Recurse(x)) yield return y; }
		else if (v is JObj o) { foreach (var kv in o.Pairs()) foreach (var y in Recurse(kv.Value)) yield return y; }
		}

	private IEnumerable<object?> EvalField(JField f, object? input, Vars? vars)
		{
		foreach (var v in Eval(f.Target, input, vars)) yield return Index(v, f.Name);
		}

	private IEnumerable<object?> EvalIndex(JIndex x, object? input, Vars? vars)
		{
		foreach (var v in Eval(x.Target, input, vars))
			foreach (var k in Eval(x.Index, input, vars))   // the subscript runs on the term's own input
				yield return Index(v, k);
		}

	private IEnumerable<object?> EvalSlice(JSlice s, object? input, Vars? vars)
		{
		foreach (var v in Eval(s.Target, input, vars))
			foreach (var from in s.From is null ? [null] : Eval(s.From, input, vars))
				foreach (var to in s.To is null ? [null] : Eval(s.To, input, vars))
					yield return Slice(v, from, to);
		}

	private IEnumerable<object?> EvalIterate(JIterate it, object? input, Vars? vars)
		{
		foreach (var v in Eval(it.Target, input, vars))
			foreach (var x in Iterate(v)) yield return x;
		}

	private static IEnumerable<object?> Iterate(object? v)
		{
		switch (v)
			{
			case List<object?> a: return a;
			case JObj o: return o.Pairs().Select(p => p.Value);
			case null: throw new JqError("Cannot iterate over null");
			default: throw new JqError($"Cannot iterate over {Json.TypeName(v)} ({Json.Brief(v)})");
			}
		}

	private IEnumerable<object?> EvalTry(JTry t, object? input, Vars? vars)
		{
		using var it = Eval(t.Body, input, vars).GetEnumerator();
		while (true)
			{
			JqError? err = null;
			bool more = false;
			try { more = it.MoveNext(); }
			catch (JqError e) { err = e; }
			if (err is not null)
				{
				if (t.Catch is not null) foreach (var c in Eval(t.Catch, err.Value, vars)) yield return c;
				yield break;
				}
			if (!more) yield break;
			yield return it.Current;
			}
		}

	private IEnumerable<object?> EvalString(JString s, object? input, Vars? vars)
		{
		// every combination of the interpolations' outputs; the first one varies fastest
		IEnumerable<string> Build(int i)
			{
			if (i < 0) { yield return ""; yield break; }
			if (s.Parts[i] is string lit) { foreach (var pre in Build(i - 1)) yield return pre + lit; yield break; }
			foreach (var v in Eval((JqNode)s.Parts[i], input, vars))
				{
				var text = s.Format is null ? ToText(v) : Format(s.Format, v);
				foreach (var pre in Build(i - 1)) yield return pre + text;
				}
			}
		foreach (var str in Build(s.Parts.Count - 1)) yield return str;
		}

	private IEnumerable<object?> EvalObject(JObject o, int i, JObj acc, object? input, Vars? vars)
		{
		if (i == o.Entries.Count) { yield return acc; yield break; }
		var (keyNode, valueNode) = o.Entries[i];
		foreach (var k in Eval(keyNode, input, vars))
			{
			if (k is not string key) throw new JqError($"Object keys must be strings");
			foreach (var v in Eval(valueNode, input, vars))
				{
				var next = acc.Copy();
				next.Set(key, v);
				foreach (var r in EvalObject(o, i + 1, next, input, vars)) yield return r;
				}
			}
		}

	private IEnumerable<object?> EvalNeg(JNeg n, object? input, Vars? vars)
		{
		foreach (var v in Eval(n.Body, input, vars))
			yield return v is double d ? -d : throw new JqError($"{Json.TypeName(v)} ({Json.Brief(v)}) cannot be negated");
		}

	/// <summary>Left's outputs, THEN right's: right is not even started before left is done (an eager
	/// `Concat(Eval(right))` ran right first, so `"a", error("x")` lost the "a").</summary>
	private IEnumerable<object?> EvalComma(JComma c, object? input, Vars? vars)
		{
		foreach (var v in Eval(c.Left, input, vars)) yield return v;
		foreach (var v in Eval(c.Right, input, vars)) yield return v;
		}

	private IEnumerable<object?> EvalPipe(JPipe p, object? input, Vars? vars)
		{
		foreach (var a in Eval(p.Left, input, vars))
			foreach (var b in Eval(p.Right, a, vars)) yield return b;
		}

	private IEnumerable<object?> EvalBinary(JBinary b, object? input, Vars? vars)
		{
		foreach (var r in Eval(b.Right, input, vars))   // jq: the right operand varies slowest
			foreach (var l in Eval(b.Left, input, vars))
				yield return Binary(b.Op, l, r);
		}

	private IEnumerable<object?> EvalAnd(JAnd a, object? input, Vars? vars)
		{
		foreach (var l in Eval(a.Left, input, vars))
			{
			if (!Json.Truthy(l)) { yield return false; continue; }
			foreach (var r in Eval(a.Right, input, vars)) yield return Json.Truthy(r);
			}
		}

	private IEnumerable<object?> EvalOr(JOr o, object? input, Vars? vars)
		{
		foreach (var l in Eval(o.Left, input, vars))
			{
			if (Json.Truthy(l)) { yield return true; continue; }
			foreach (var r in Eval(o.Right, input, vars)) yield return Json.Truthy(r);
			}
		}

	private IEnumerable<object?> EvalAlt(JAlt a, object? input, Vars? vars)
		{
		// `a // b`: a's outputs that are neither null nor false, else b. An error in a is NOT
		// suppressed in jq 1.6 (measured: `.a.b // 5` over {"a":3} is an error)
		var good = Eval(a.Left, input, vars).Where(Json.Truthy).ToList();
		return good.Count > 0 ? good : Eval(a.Right, input, vars);
		}

	private IEnumerable<object?> EvalIf(JIf i, object? input, Vars? vars)
		{
		foreach (var c in Eval(i.Cond, input, vars))
			{
			var branch = Json.Truthy(c) ? i.Then : i.Else;
			if (branch is null) { yield return input; continue; }
			foreach (var r in Eval(branch, input, vars)) yield return r;
			}
		}

	private IEnumerable<object?> EvalReduce(JReduce r, object? input, Vars? vars)
		{
		foreach (var init in Eval(r.Init, input, vars))
			{
			object? state = init;
			foreach (var v in Eval(r.Source, input, vars))
				foreach (var bound in Bind(r.Pattern, v, vars))
					{
					object? last = null; bool any = false;
					foreach (var u in Eval(r.Update, state, bound)) { last = u; any = true; }
					state = any ? last : null;
					}
			yield return state;
			}
		}

	private IEnumerable<object?> EvalBind(JBind b, object? input, Vars? vars)
		{
		foreach (var v in Eval(b.Source, input, vars))
			foreach (var bound in Bind(b.Pattern, v, vars))
				foreach (var r in Eval(b.Body, input, bound)) yield return r;
		}

	/// <summary>Destructure <paramref name="v"/> by a pattern: one set of variables (a key
	/// expression with several outputs gives several).</summary>
	private IEnumerable<Vars?> Bind(JqPattern p, object? v, Vars? vars)
		{
		switch (p)
			{
			case JPVar pv: yield return new Vars(pv.Name, v, vars); break;
			case JPArray pa:
				{
				if (v is not null and not List<object?>) throw new JqError($"Cannot index {Json.TypeName(v)} with number");
				IEnumerable<Vars?> acc = [vars];
				for (int i = 0; i < pa.Items.Count; i++)
					{
					int idx = i;
					var item = Index(v, (double)idx);
					acc = acc.SelectMany(a => Bind(pa.Items[idx], item, a)).ToList();
					}
				foreach (var a in acc) yield return a;
				break;
				}
			case JPObject po:
				{
				IEnumerable<Vars?> acc = [vars];
				foreach (var (keyNode, sub) in po.Entries)
					{
					var next = new List<Vars?>();
					foreach (var a in acc)
						foreach (var k in Eval(keyNode, v, a))
							{
							if (k is not string ks) throw new JqError($"Cannot index {Json.TypeName(v)} with {Json.TypeName(k)}");
							next.AddRange(Bind(sub, Index(v, ks), a));
							}
					acc = next;
					}
				foreach (var a in acc) yield return a;
				break;
				}
			}
		}

	// ── indexing ────────────────────────────────────────────────────────────────────────────

	private static object? Index(object? v, object? k)
		{
		switch (v, k)
			{
			case (null, string or double or null): return null;
			case (JObj o, string s): return o.Get(s);
			case (List<object?> a, double d):
				{
				int i = (int)Math.Floor(d);
				if (i < 0) i += a.Count;
				return i >= 0 && i < a.Count ? a[i] : null;
				}
			case (List<object?> or string or null, JObj range): return Slice(v, range.Get("start"), range.Get("end"));
			// jq 1.6's wording: a string key is quoted with its type, any other key named by type only
			case (_, string s): throw new JqError($"Cannot index {Json.TypeName(v)} with string \"{s}\"");
			default: throw new JqError($"Cannot index {Json.TypeName(v)} with {Json.TypeName(k)}");
			}
		}

	private static (int Start, int End) SliceBounds(int len, object? from, object? to)
		{
		int Clamp(object? x, int dflt)
			{
			if (x is null) return dflt;
			if (x is not double d) throw new JqError("Start and end indices of an array slice must be numbers");
			int i = (int)Math.Floor(d);
			if (i < 0) i += len;
			return Math.Clamp(i, 0, len);
			}
		int s = Clamp(from, 0), e = Clamp(to, len);
		return (s, Math.Max(s, e));
		}

	private static object? Slice(object? v, object? from, object? to)
		{
		switch (v)
			{
			case null: return null;
			case List<object?> a: { var (s, e) = SliceBounds(a.Count, from, to); return a.GetRange(s, e - s); }
			case string str:
				{
				var runes = str.EnumerateRunes().ToList();
				var (s, e) = SliceBounds(runes.Count, from, to);
				var sb = new StringBuilder();
				for (int i = s; i < e; i++) sb.Append(runes[i].ToString());
				return sb.ToString();
				}
			default: throw new JqError($"Cannot index {Json.TypeName(v)} with object");
			}
		}

	// ── arithmetic and comparison ─────────────────────────────────────────────────────────────

	private static object? Binary(string op, object? l, object? r)
		{
		switch (op)
			{
			case "==": return Json.Equal(l, r);
			case "!=": return !Json.Equal(l, r);
			case "<":  return Json.Compare(l, r) < 0;
			case "<=": return Json.Compare(l, r) <= 0;
			case ">":  return Json.Compare(l, r) > 0;
			case ">=": return Json.Compare(l, r) >= 0;
			case "+":
				if (l is null) return r;
				if (r is null) return l;
				switch (l, r)
					{
					case (double a, double b): return a + b;
					case (string a, string b): return a + b;
					case (List<object?> a, List<object?> b): return a.Concat(b).ToList();
					case (JObj a, JObj b): { var o = a.Copy(); foreach (var kv in b.Pairs()) o.Set(kv.Key, kv.Value); return o; }
					}
				break;
			case "-":
				switch (l, r)
					{
					case (double a, double b): return a - b;
					case (List<object?> a, List<object?> b): return a.Where(x => !b.Any(y => Json.Equal(x, y))).ToList();
					}
				break;
			case "*":
				switch (l, r)
					{
					case (double a, double b): return a * b;
					case (string s, double n): return Repeat(s, n);
					case (double n, string s): return Repeat(s, n);
					case (JObj a, JObj b): return DeepMerge(a, b);
					}
				break;
			case "/":
				switch (l, r)
					{
					case (double a, double b):
						if (b == 0) throw new JqError($"{Json.TypeName(l)} ({Json.Brief(l)}) and {Json.TypeName(r)} ({Json.Brief(r)}) cannot be divided because the divisor is zero");
						return a / b;
					case (string a, string b): return Split(a, b);
					}
				break;
			case "%":
				switch (l, r)
					{
					case (double a, double b):
						{
						long x = (long)a, y = (long)b;
						if (y == 0) throw new JqError($"{Json.TypeName(l)} ({Json.Brief(l)}) and {Json.TypeName(r)} ({Json.Brief(r)}) cannot be divided (remainder) because the divisor is zero");
						return (double)(Math.Abs(x) % Math.Abs(y) * (x < 0 ? -1 : 1));
						}
					}
				break;
			}
		string verb = op switch { "+" => "added", "-" => "subtracted", "*" => "multiplied", "/" => "divided", _ => "divided" };
		throw new JqError($"{Json.TypeName(l)} ({Json.Brief(l)}) and {Json.TypeName(r)} ({Json.Brief(r)}) cannot be {verb}");
		}

	private static object? Repeat(string s, double n)
		{
		// jq 1.6: `for (n = number - 1; n > 0; n--) append`, null when n ends below 0, so "ab" * 0.5
		// and "ab" * 1.5 are both "ab", and "ab" * 0 is null (measured)
		int extra = (int)(n - 1);
		return extra < 0 ? null : string.Concat(Enumerable.Repeat(s, extra + 1));
		}

	private static JObj DeepMerge(JObj a, JObj b)
		{
		var o = a.Copy();
		foreach (var kv in b.Pairs())
			o.Set(kv.Key, o.Get(kv.Key) is JObj x && kv.Value is JObj y ? DeepMerge(x, y) : kv.Value);
		return o;
		}

	private static List<object?> Split(string s, string sep)
		{
		if (s.Length == 0) return [];
		if (sep.Length == 0) return s.EnumerateRunes().Select(r => (object?)r.ToString()).ToList();
		return s.Split(sep).Select(x => (object?)x).ToList();
		}

	// ── paths and assignment ────────────────────────────────────────────────────────────────

	private IEnumerable<(List<object?> Path, object? Value)> Paths(JqNode n, object? input, Vars? vars)
		{
		switch (n)
			{
			case JIdentity: yield return ([], input); break;
			case JRecurse:
				foreach (var x in RecursePaths([], input)) yield return x;
				break;
			case JField f:
				foreach (var (p, v) in Paths(f.Target, input, vars)) yield return ([.. p, f.Name], Index(v, f.Name));
				break;
			case JIndex x:
				foreach (var (p, v) in Paths(x.Target, input, vars))
					foreach (var k in Eval(x.Index, input, vars))
						yield return ([.. p, k], Index(v, k));
				break;
			case JSlice s:
				foreach (var (p, v) in Paths(s.Target, input, vars))
					foreach (var from in s.From is null ? [null] : Eval(s.From, input, vars))
						foreach (var to in s.To is null ? [null] : Eval(s.To, input, vars))
							{
							var range = new JObj(); range.Set("start", from); range.Set("end", to);
							yield return ([.. p, range], Slice(v, from, to));
							}
				break;
			case JIterate it:
				foreach (var (p, v) in Paths(it.Target, input, vars))
					{
					if (v is List<object?> a) { for (int i = 0; i < a.Count; i++) yield return ([.. p, (double)i], a[i]); }
					else if (v is JObj o) { foreach (var kv in o.Pairs()) yield return ([.. p, kv.Key], kv.Value); }
					else if (v is not null) throw new JqError($"Cannot iterate over {Json.TypeName(v)} ({Json.Brief(v)})");
					}
				break;
			case JTry { Catch: null } t:
				{
				using var e = Paths(t.Body, input, vars).GetEnumerator();
				while (true)
					{
					bool more;
					try { more = e.MoveNext(); } catch (JqError) { yield break; }
					if (!more) yield break;
					yield return e.Current;
					}
				}
			case JPipe p:
				foreach (var (pa, va) in Paths(p.Left, input, vars))
					foreach (var (pb, vb) in Paths(p.Right, va, vars))
						yield return ([.. pa, .. pb], vb);
				break;
			case JComma c:
				foreach (var x in Paths(c.Left, input, vars)) yield return x;
				foreach (var x in Paths(c.Right, input, vars)) yield return x;
				break;
			case JIf i:
				foreach (var c in Eval(i.Cond, input, vars))
					{
					var branch = Json.Truthy(c) ? i.Then : i.Else;
					if (branch is null) { yield return ([], input); continue; }
					foreach (var x in Paths(branch, input, vars)) yield return x;
					}
				break;
			case JAlt a:
				{
				var good = Paths(a.Left, input, vars).Where(x => Json.Truthy(x.Value)).ToList();
				if (good.Count > 0) { foreach (var x in good) yield return x; }
				else foreach (var x in Paths(a.Right, input, vars)) yield return x;
				break;
				}
			case JBind b:
				foreach (var v in Eval(b.Source, input, vars))
					foreach (var bound in Bind(b.Pattern, v, vars))
						foreach (var x in Paths(b.Body, input, bound)) yield return x;
				break;
			case JCall { Name: "select", Args.Count: 1 } sel:
				foreach (var c in Eval(sel.Args[0], input, vars)) if (Json.Truthy(c)) yield return ([], input);
				break;
			case JCall { Name: "empty", Args.Count: 0 }: break;
			case JCall { Name: "values" or "arrays" or "objects" or "iterables" or "booleans" or "numbers" or "strings" or "nulls" or "scalars", Args.Count: 0 } tf:
				// select-like: the path to `.` when the input passes (`del(.. | nulls)`)
				if (Call(tf, input, vars).Any()) yield return ([], input);
				break;
			case JCall { Name: "recurse", Args.Count: 0 }:
				foreach (var x in RecursePaths([], input)) yield return x;
				break;
			case JCall { Name: "first", Args.Count: 1 } first:
				foreach (var x in Paths(first.Args[0], input, vars)) { yield return x; break; }
				break;
			case JCall { Name: "first", Args.Count: 0 }: yield return ([0.0], Index(input, 0.0)); break;
			case JCall { Name: "last", Args.Count: 0 }: yield return ([-1.0], Index(input, -1.0)); break;
			case JCall { Name: "error" }:
				foreach (var _ in Eval(n, input, vars)) { }
				break;
			default:
				{
				var v = Eval(n, input, vars).FirstOrDefault();
				throw new JqError($"Invalid path expression with result {Json.Brief(v)}");
				}
			}
		}

	private static IEnumerable<(List<object?>, object?)> RecursePaths(List<object?> at, object? v)
		{
		yield return (at, v);
		if (v is List<object?> a) { for (int i = 0; i < a.Count; i++) foreach (var x in RecursePaths([.. at, (double)i], a[i])) yield return x; }
		else if (v is JObj o) { foreach (var kv in o.Pairs()) foreach (var x in RecursePaths([.. at, kv.Key], kv.Value)) yield return x; }
		}

	private static object? GetPath(object? root, List<object?> path)
		{
		object? v = root;
		foreach (var k in path) { if (v is null) return null; v = Index(v, k); }
		return v;
		}

	private static object? SetPath(object? root, List<object?> path, int i, object? value)
		{
		if (i == path.Count) return value;
		var k = path[i];
		switch (k)
			{
			case string s:
				{
				if (root is not null and not JObj) throw new JqError($"Cannot index {Json.TypeName(root)} with \"{s}\"");
				var o = root is JObj src ? src.Copy() : new JObj();
				o.Set(s, SetPath(o.Get(s), path, i + 1, value));
				return o;
				}
			case double d:
				{
				if (root is not null and not List<object?>) throw new JqError($"Cannot index {Json.TypeName(root)} with number");
				var a = root is List<object?> src ? new List<object?>(src) : [];
				int idx = (int)Math.Floor(d);
				if (idx < 0) { idx += a.Count; if (idx < 0) throw new JqError("Out of bounds negative array index"); }
				while (a.Count <= idx) a.Add(null);
				a[idx] = SetPath(a[idx], path, i + 1, value);
				return a;
				}
			case JObj range:
				{
				if (root is not null and not List<object?>) throw new JqError($"Cannot update field at object index of {Json.TypeName(root)}");
				var a = root as List<object?> ?? [];
				var (s, e) = SliceBounds(a.Count, range.Get("start"), range.Get("end"));
				var replaced = SetPath(a.GetRange(s, e - s), path, i + 1, value);
				if (replaced is not List<object?> r) throw new JqError("A slice of an array can only be assigned another array");
				return a.Take(s).Concat(r).Concat(a.Skip(e)).ToList();
				}
			default: throw new JqError($"Invalid path component {Json.Brief(k)}");
			}
		}

	private static object? DeletePaths(object? root, List<List<object?>> paths)
		{
		// last first, so earlier array indices stay valid
		paths.Sort((a, b) => Json.Compare(b, a));
		foreach (var p in paths) root = DeletePath(root, p, 0);
		return root;
		}

	private static object? DeletePath(object? root, List<object?> path, int i)
		{
		if (root is null || path.Count == 0) return path.Count == 0 ? null : root;
		var k = path[i];
		bool last = i == path.Count - 1;
		switch (root, k)
			{
			case (JObj o, string s):
				{
				if (!o.ContainsKey(s)) return root;
				var c = o.Copy();
				if (last) c.Remove(s); else c.Set(s, DeletePath(o.Get(s), path, i + 1));
				return c;
				}
			case (List<object?> a, double d):
				{
				int idx = (int)Math.Floor(d);
				if (idx < 0) idx += a.Count;
				if (idx < 0 || idx >= a.Count) return root;
				var c = new List<object?>(a);
				if (last) c.RemoveAt(idx); else c[idx] = DeletePath(a[idx], path, i + 1);
				return c;
				}
			case (List<object?> a, JObj range):
				{
				var (s, e) = SliceBounds(a.Count, range.Get("start"), range.Get("end"));
				if (last) return a.Take(s).Concat(a.Skip(e)).ToList();
				var inner = DeletePath(a.GetRange(s, e - s), path, i + 1) as List<object?> ?? [];
				return a.Take(s).Concat(inner).Concat(a.Skip(e)).ToList();
				}
			default: throw new JqError($"Cannot delete field at index {Json.Brief(k)} of {Json.TypeName(root)}");
			}
		}

	private IEnumerable<object?> EvalAssign(JAssign a, object? input, Vars? vars)
		{
		var paths = Paths(a.Lhs, input, vars).Select(x => x.Path).ToList();
		switch (a.Op)
			{
			case "=":
				foreach (var v in Eval(a.Rhs, input, vars))
					{
					object? acc = input;
					foreach (var p in paths) acc = SetPath(acc, p, 0, v);
					yield return acc;
					}
				yield break;
			case "|=":
				{
				object? acc = input;
				var gone = new List<List<object?>>();
				foreach (var p in paths)
					{
					var old = GetPath(acc, p);
					bool any = false;
					foreach (var nv in Eval(a.Rhs, old, vars)) { acc = SetPath(acc, p, 0, nv); any = true; break; }
					if (!any) gone.Add(p);
					}
				yield return gone.Count > 0 ? DeletePaths(acc, gone) : acc;
				yield break;
				}
			default:
				{
				var op = a.Op[..^1];   // += → +, //= → //
				foreach (var v in Eval(a.Rhs, input, vars))
					{
					object? acc = input;
					foreach (var p in paths)
						{
						var old = GetPath(acc, p);
						acc = SetPath(acc, p, 0, op == "//" ? (Json.Truthy(old) ? old : v) : Binary(op, old, v));
						}
					yield return acc;
					}
				yield break;
				}
			}
		}

	// ── builtins ────────────────────────────────────────────────────────────────────────────

	/// <summary>Every combination of the arguments' values (the first argument varies slowest).</summary>
	private IEnumerable<object?[]> ArgValues(List<JqNode> args, object? input, Vars? vars, int i = 0)
		{
		if (i == args.Count) { yield return new object?[args.Count]; yield break; }
		foreach (var v in Eval(args[i], input, vars))
			foreach (var rest in ArgValues(args, input, vars, i + 1))
				{
				rest[i] = v;
				yield return (object?[])rest.Clone();
				}
		}

	private IEnumerable<object?> Call(JCall c, object? input, Vars? vars)
		{
		var a = c.Args;
		switch (c.Name, a.Count)
			{
			case ("empty", 0): return [];
			case ("not", 0): return [!Json.Truthy(input)];
			case ("length", 0): return [Length(input)];
			case ("keys", 0): return [Keys(input, sorted: true)];
			case ("keys_unsorted", 0): return [Keys(input, sorted: false)];
			case ("values", 0): return input is null ? [] : [input];
			case ("has", 1): return ArgValues(a, input, vars).Select(k => (object?)Has(input, k[0]));
			case ("map", 1): return [Iterate(input).SelectMany(x => Eval(a[0], x, vars)).ToList()];
			case ("map_values", 1): return EvalAssign(new JAssign("|=", new JIterate(new JIdentity()), a[0]), input, vars);
			case ("to_entries", 0): return [ToEntries(input)];
			case ("from_entries", 0): return [FromEntries(input)];
			case ("with_entries", 1):
				return [FromEntries(((List<object?>)ToEntries(input)).SelectMany(x => Eval(a[0], x, vars)).ToList())];
			case ("add", 0): return [Iterate(input).Aggregate((object?)null, (acc, x) => Binary("+", acc, x))];
			case ("any", 0): return [Iterate(input).Any(Json.Truthy)];
			case ("all", 0): return [Iterate(input).All(Json.Truthy)];
			case ("any", 1): return [Iterate(input).Any(x => Eval(a[0], x, vars).Any(Json.Truthy))];
			case ("all", 1): return [Iterate(input).All(x => Eval(a[0], x, vars).All(Json.Truthy))];
			case ("any", 2): return [Eval(a[0], input, vars).Any(x => Eval(a[1], x, vars).Any(Json.Truthy))];
			case ("all", 2): return [Eval(a[0], input, vars).All(x => Eval(a[1], x, vars).All(Json.Truthy))];
			case ("select", 1): return Select(a[0], input, vars);
			case ("recurse", 0): return Recurse(input);
			case ("sort", 0): return [SortBy(input, x => x)];
			case ("sort_by", 1): return [SortBy(input, x => Eval(a[0], x, vars).ToList())];
			case ("group_by", 1): return [GroupBy(input, x => Eval(a[0], x, vars).ToList())];
			case ("unique", 0): return [GroupBy(input, x => x).Select(g => ((List<object?>)g!)[0]).ToList()];
			case ("unique_by", 1): return [GroupBy(input, x => Eval(a[0], x, vars).ToList()).Select(g => ((List<object?>)g!)[0]).ToList()];
			case ("min", 0): return [MinMax(input, x => x, max: false)];
			case ("max", 0): return [MinMax(input, x => x, max: true)];
			case ("min_by", 1): return [MinMax(input, x => Eval(a[0], x, vars).ToList(), max: false)];
			case ("max_by", 1): return [MinMax(input, x => Eval(a[0], x, vars).ToList(), max: true)];
			case ("reverse", 0): return [Reverse(input)];
			case ("first", 0): return [Index(input, 0.0)];
			case ("last", 0): return [Index(input, -1.0)];
			case ("first", 1): return Eval(a[0], input, vars).Take(1);
			case ("last", 1): { object? last = null; bool any = false; foreach (var v in Eval(a[0], input, vars)) { last = v; any = true; } return any ? [last] : []; }
			case ("flatten", 0): return [Flatten(input, int.MaxValue)];
			case ("flatten", 1): return ArgValues(a, input, vars).Select(d => d[0] is double depth && depth >= 0
				? Flatten(input, (int)depth) : throw new JqError("flatten depth must not be negative"));
			case ("range", _): return ArgValues(a, input, vars).SelectMany(Range);
			case ("tostring", 0): return [ToText(input)];
			case ("tonumber", 0): return [ToNumber(input)];
			case ("type", 0): return [Json.TypeName(input)];
			case ("tojson", 0): return [Json.Dump(input, compact: true)];
			case ("fromjson", 0): return [FromJson(input)];
			case ("split", 1): return ArgValues(a, input, vars).Select(s => input is string str && s[0] is string sep
				? (object?)Split(str, sep) : throw new JqError("split input and separator must be strings"));
			case ("join", 1): return ArgValues(a, input, vars).Select(s => Join(input, s[0]));
			case ("test", _): return ArgValues(a, input, vars).Select(s => (object?)CompileRegex(input, s[0], s.Length > 1 ? s[1] : null).IsMatch((string)input!));
			case ("sub", _): return Sub(c, input, vars, global: false);
			case ("gsub", _): return Sub(c, input, vars, global: true);
			case ("startswith", 1): return ArgValues(a, input, vars).Select(s => input is string x && s[0] is string y
				? (object?)x.StartsWith(y, StringComparison.Ordinal) : throw new JqError("startswith() requires string inputs"));
			case ("endswith", 1): return ArgValues(a, input, vars).Select(s => input is string x && s[0] is string y
				? (object?)x.EndsWith(y, StringComparison.Ordinal) : throw new JqError("endswith() requires string inputs"));
			case ("ltrimstr", 1): return ArgValues(a, input, vars).Select(s => input is string x && s[0] is string y && x.StartsWith(y, StringComparison.Ordinal) ? x[y.Length..] : input);
			case ("rtrimstr", 1): return ArgValues(a, input, vars).Select(s => input is string x && s[0] is string y && x.EndsWith(y, StringComparison.Ordinal) && y.Length > 0 ? x[..^y.Length] : input);
			case ("ascii_downcase", 0): return [Ascii(input, upper: false)];
			case ("ascii_upcase", 0): return [Ascii(input, upper: true)];
			case ("contains", 1): return ArgValues(a, input, vars).Select(s => (object?)Contains(input, s[0]));
			case ("error", 0): throw new JqError(input);
			case ("error", 1): return ErrorWith(a[0], input, vars);
			case ("env", 0): return [env];
			case ("del", 1): return [DeletePaths(input, Paths(a[0], input, vars).Select(x => x.Path).ToList())];
			case ("floor", 0): return [Math.Floor(Num(input, "floor"))];
			case ("ceil", 0): return [Math.Ceiling(Num(input, "ceil"))];
			case ("round", 0): return [Math.Round(Num(input, "round"), MidpointRounding.AwayFromZero)];
			case ("sqrt", 0): return [Math.Sqrt(Num(input, "sqrt"))];
			case ("fabs", 0): return [Math.Abs(Num(input, "fabs"))];
			case ("arrays", 0): return input is List<object?> ? [input] : [];
			case ("objects", 0): return input is JObj ? [input] : [];
			case ("iterables", 0): return input is List<object?> or JObj ? [input] : [];
			case ("booleans", 0): return input is bool ? [input] : [];
			case ("numbers", 0): return input is double ? [input] : [];
			case ("strings", 0): return input is string ? [input] : [];
			case ("nulls", 0): return input is null ? [input] : [];
			case ("scalars", 0): return input is List<object?> or JObj ? [] : [input];
			}
		throw new JqError($"{c.Name}/{a.Count} is not defined");
		}

	private IEnumerable<object?> ErrorWith(JqNode msg, object? input, Vars? vars)
		{
		foreach (var m in Eval(msg, input, vars)) throw new JqError(m);
		yield break;
		}

	private IEnumerable<object?> Select(JqNode f, object? input, Vars? vars)
		{
		foreach (var c in Eval(f, input, vars)) if (Json.Truthy(c)) yield return input;
		}

	private static double Num(object? v, string fn) =>
		v is double d ? d : throw new JqError($"{Json.TypeName(v)} ({Json.Brief(v)}) number required");

	private static object? Length(object? v) => v switch
		{
		null => 0.0,
		bool => throw new JqError($"boolean ({Json.Brief(v)}) has no length"),
		double d => Math.Abs(d),
		string s => (double)s.EnumerateRunes().Count(),
		List<object?> a => (double)a.Count,
		JObj o => (double)o.Count,
		_ => 0.0,
		};

	private static object? Keys(object? v, bool sorted) => v switch
		{
		JObj o => (sorted ? Json.SortedKeys(o) : o.Keys.ToList()).Select(k => (object?)k).ToList(),
		List<object?> a => Enumerable.Range(0, a.Count).Select(i => (object?)(double)i).ToList(),
		_ => throw new JqError($"{Json.TypeName(v)} ({Json.Brief(v)}) has no keys"),
		};

	private static bool Has(object? v, object? k) => (v, k) switch
		{
		(JObj o, string s) => o.ContainsKey(s),
		(List<object?> a, double d) => d >= 0 && d < a.Count,
		_ => throw new JqError($"Cannot check whether {Json.TypeName(v)} has a {Json.TypeName(k)} key"),
		};

	private static object? ToEntries(object? v)
		{
		if (v is JObj o)
			return o.Pairs().Select(kv => { var e = new JObj(); e.Set("key", kv.Key); e.Set("value", kv.Value); return (object?)e; }).ToList();
		if (v is List<object?> a)
			return a.Select((x, i) => { var e = new JObj(); e.Set("key", (double)i); e.Set("value", x); return (object?)e; }).ToList();
		throw new JqError($"{Json.TypeName(v)} ({Json.Brief(v)}) has no keys");
		}

	/// <summary>jq 1.6's from_entries (measured against it): the key is the first of key, Key, name,
	/// Name that is neither null nor false, and must be a string; the value is `value` when present,
	/// else `Value`. (k/v, which jq 1.7 accepts, are not.)</summary>
	private static object? FromEntries(object? v)
		{
		var o = new JObj();
		foreach (var e in Iterate(v))
			{
			object? key = null;
			foreach (var name in new[] { "key", "Key", "name", "Name" })
				{
				key = Index(e, name);
				if (Json.Truthy(key)) break;
				}
			if (key is not string ks) throw new JqError($"Cannot use {Json.TypeName(key)} ({Json.Brief(key)}) as object key");
			o.Set(ks, e is JObj eo && eo.ContainsKey("value") ? eo.Get("value") : Index(e, "Value"));
			}
		return o;
		}

	private static List<object?> SortBy(object? v, Func<object?, object?> key)
		{
		if (v is not List<object?> a) throw new JqError($"{Json.TypeName(v)} ({Json.Brief(v)}) cannot be sorted, as it is not an array");
		var keyed = a.Select((x, i) => (Key: key(x), Index: i, Value: x)).ToList();
		keyed.Sort((p, q) => { int c = Json.Compare(KeyList(p.Key), KeyList(q.Key)); return c != 0 ? c : p.Index.CompareTo(q.Index); });
		return keyed.Select(p => p.Value).ToList();
		}

	private static object? KeyList(object? k) => k is List<object?> l ? l : k;

	private static List<object?> GroupBy(object? v, Func<object?, object?> key)
		{
		if (v is not List<object?> a) throw new JqError($"Cannot index {Json.TypeName(v)} with number");
		var keyed = a.Select((x, i) => (Key: key(x), Index: i, Value: x)).ToList();
		keyed.Sort((p, q) => { int c = Json.Compare(p.Key, q.Key); return c != 0 ? c : p.Index.CompareTo(q.Index); });
		var groups = new List<object?>();
		List<object?>? cur = null; object? curKey = null;
		foreach (var (k, _, x) in keyed)
			{
			if (cur is null || !Json.Equal(curKey, k)) { cur = []; groups.Add(cur); curKey = k; }
			cur.Add(x);
			}
		return groups;
		}

	private static object? MinMax(object? v, Func<object?, object?> key, bool max)
		{
		if (v is not List<object?> a) throw new JqError($"Cannot index {Json.TypeName(v)} with number");
		if (a.Count == 0) return null;
		object? best = a[0]; var bestKey = key(best);
		for (int i = 1; i < a.Count; i++)
			{
			var k = key(a[i]);
			int c = Json.Compare(k, bestKey);
			if (max ? c >= 0 : c < 0) { best = a[i]; bestKey = k; }   // max keeps the last of equals, min the first
			}
		return best;
		}

	private static object? Reverse(object? v) => v switch
		{
		null => new List<object?>(),
		List<object?> a => Enumerable.Reverse(a).ToList(),
		_ => throw new JqError($"Cannot index {Json.TypeName(v)} with number"),
		};

	private static List<object?> Flatten(object? v, int depth)
		{
		if (v is not List<object?> a) throw new JqError($"Cannot iterate over {Json.TypeName(v)}");
		var r = new List<object?>();
		foreach (var x in a)
			{
			if (x is List<object?> inner && depth > 0) r.AddRange(Flatten(inner, depth - 1));
			else r.Add(x);
			}
		return r;
		}

	private static IEnumerable<object?> Range(object?[] a)
		{
		double from = 0, upto, by = 1;
		if (a.Length == 1) upto = a[0] is double u ? u : throw new JqError("Range bounds must be numeric");
		else
			{
			from = a[0] is double f ? f : throw new JqError("Range bounds must be numeric");
			upto = a[1] is double u2 ? u2 : throw new JqError("Range bounds must be numeric");
			if (a.Length == 3) by = a[2] is double b ? b : throw new JqError("Range bounds must be numeric");
			}
		if (by > 0) for (double x = from; x < upto; x += by) yield return x;
		else if (by < 0) for (double x = from; x > upto; x += by) yield return x;
		}

	private static string ToText(object? v) => v is string s ? s : Json.Dump(v, compact: true);

	private static object? ToNumber(object? v)
		{
		if (v is double) return v;
		if (v is string s)
			{
			// jq parses the string as JSON, so its messages are the parser's
			object? n;
			try { n = Json.ParseOne(s); }
			catch (Json.ParseException e) { throw new JqError($"{e.Message} (while parsing '{s}')"); }
			return n is double ? n : throw new JqError($"{Json.TypeName(n)} ({Json.Brief(n)}) cannot be parsed as a number");
			}
		throw new JqError($"{Json.TypeName(v)} ({Json.Brief(v)}) cannot be parsed as a number");
		}

	private static object? FromJson(object? v)
		{
		if (v is not string s) throw new JqError($"{Json.TypeName(v)} ({Json.Brief(v)}) cannot be parsed as JSON");
		try { return Json.ParseOne(s); }
		catch (Json.ParseException e) { throw new JqError($"{e.Message} (while parsing '{s}')"); }
		}

	/// <summary>jq 1.6's join: null is "", strings as they are, numbers and booleans as JSON.</summary>
	private static object? Join(object? v, object? sep)
		{
		if (sep is not string s) throw new JqError("join separator must be a string");
		var sb = new StringBuilder();
		bool first = true;
		foreach (var x in Iterate(v))
			{
			if (!first) sb.Append(s);
			first = false;
			sb.Append(x switch
				{
				null => "",
				string str => str,
				double or bool => Json.Dump(x, compact: true),
				_ => throw new JqError($"Cannot join with {Json.TypeName(x)}"),
				});
			}
		return sb.ToString();
		}

	private static object? Ascii(object? v, bool upper)
		{
		if (v is not string s) throw new JqError($"{(upper ? "ascii_upcase" : "ascii_downcase")} input must be a string");
		var cs = s.ToCharArray();
		for (int i = 0; i < cs.Length; i++)
			{
			if (upper && cs[i] is >= 'a' and <= 'z') cs[i] = (char)(cs[i] - 32);
			else if (!upper && cs[i] is >= 'A' and <= 'Z') cs[i] = (char)(cs[i] + 32);
			}
		return new string(cs);
		}

	private static bool Contains(object? a, object? b)
		{
		switch (a, b)
			{
			case (JObj x, JObj y): return y.Pairs().All(kv => x.TryGet(kv.Key, out var v) && Contains(v, kv.Value));
			case (List<object?> x, List<object?> y): return y.All(yb => x.Any(xa => Contains(xa, yb)));
			case (string x, string y): return x.Contains(y, StringComparison.Ordinal);
			}
		if (Json.TypeName(a) != Json.TypeName(b))
			throw new JqError($"{Json.TypeName(a)} ({Json.Brief(a)}) and {Json.TypeName(b)} ({Json.Brief(b)}) cannot have their containment checked");
		return Json.Equal(a, b);
		}

	// ── regular expressions (Oniguruma syntax is close to .NET's for what jq users write) ──────────

	private static readonly Dictionary<(string, string), Regex> RegexCache = [];

	private static Regex CompileRegex(object? input, object? re, object? flags)
		{
		if (input is not string) throw new JqError($"{Json.TypeName(input)} ({Json.Brief(input)}) cannot be matched, as it is not a string");
		if (re is not string pattern) throw new JqError($"{Json.TypeName(re)} ({Json.Brief(re)}) cannot be matched, as it is not a string");
		var f = flags as string ?? "";
		if (flags is not null and not string) throw new JqError($"{Json.TypeName(flags)} ({Json.Brief(flags)}) is not a string");
		lock (RegexCache)
			{
			if (RegexCache.TryGetValue((pattern, f), out var cached)) return cached;
			var opts = RegexOptions.None;
			foreach (var ch in f)
				opts |= ch switch
					{
					'i' => RegexOptions.IgnoreCase, 'x' => RegexOptions.IgnorePatternWhitespace, 's' => RegexOptions.Singleline,
					'g' or 'n' or 'l' or 'p' => RegexOptions.None,
					_ => throw new JqError($"{f} is not a valid modifier string"),
					};
			Regex rx;
			try { rx = new Regex(pattern, opts); }
			catch (ArgumentException e) { throw new JqError($"{pattern} (at offset 0) is not a valid regex: {e.Message}"); }
			RegexCache[(pattern, f)] = rx;
			return rx;
			}
		}

	private IEnumerable<object?> Sub(JCall c, object? input, Vars? vars, bool global)
		{
		var a = c.Args;
		foreach (var re in Eval(a[0], input, vars))
			foreach (var flags in a.Count > 2 ? Eval(a[2], input, vars) : [null])
				{
				var rx = CompileRegex(input, re, flags);
				bool all = global || (flags as string ?? "").Contains('g');
				var s = (string)input!;
				var matches = all ? rx.Matches(s).Cast<Match>().ToList() : rx.Match(s) is { Success: true } one ? [one] : [];
				if (matches.Count == 0) { yield return s; continue; }
				var sb = new StringBuilder();
				int pos = 0;
				foreach (var m in matches)
					{
					sb.Append(s, pos, m.Index - pos);
					var captures = new JObj();
					foreach (var g in rx.GetGroupNames())
						if (!int.TryParse(g, out _)) captures.Set(g, m.Groups[g].Success ? m.Groups[g].Value : null);
					var rep = Eval(a[1], captures, vars).FirstOrDefault();
					if (rep is not string rs) throw new JqError($"{Json.TypeName(rep)} ({Json.Brief(rep)}) cannot be added to a string");
					sb.Append(rs);
					pos = m.Index + m.Length;
					}
				sb.Append(s, pos, s.Length - pos);
				yield return sb.ToString();
				}
		}

	// ── @formats ─────────────────────────────────────────────────────────────────────────────

	public static string Format(string name, object? v)
		{
		switch (name)
			{
			case "text": return ToText(v);
			case "json": return Json.Dump(v, compact: true);
			case "base64": return Convert.ToBase64String(ShellEncoding.Utf8.GetBytes(ToText(v)));
			case "csv":
			case "tsv":
				{
				if (v is not List<object?> row) throw new JqError($"{Json.TypeName(v)} ({Json.Brief(v)}) cannot be {name}-formatted, only an array can be");
				var cells = row.Select(x => x switch
					{
					null => "",
					bool b => b ? "true" : "false",
					double d => Json.Number(d),
					string s => name == "csv"
						? "\"" + s.Replace("\"", "\"\"") + "\""
						: s.Replace("\\", "\\\\").Replace("\t", "\\t").Replace("\n", "\\n").Replace("\r", "\\r"),
					_ => throw new JqError($"{Json.TypeName(x)} ({Json.Brief(x)}) is not valid in a csv row"),
					});
				return string.Join(name == "csv" ? "," : "\t", cells);
				}
			}
		throw new JqError($"{name} is not a valid format");
		}
	}
