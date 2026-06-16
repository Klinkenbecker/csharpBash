namespace Bash.IO;

/// <summary>
/// A readline-style single-line editor for the REPL: cursor movement, in-line
/// editing, history navigation (up/down), and tab completion. Falls back to
/// <see cref="Console.ReadLine"/> when input is redirected (no interactive TTY).
///
/// Rendering re-derives the input anchor after every write so that scrolling and
/// line-wrapping stay correct: the buffer is reprinted from the anchor, and the
/// anchor row is recomputed from where the cursor actually landed.
/// </summary>
public sealed class LineEditor(History history, CompletionEngine completion)
	{
	private readonly History _history = history;
	private readonly CompletionEngine _completion = completion;

	private readonly System.Text.StringBuilder _buf = new();
	private int _cursor;            // logical caret index within _buf
	private string _prompt = "";
	private int _anchorLeft;        // column where the buffer starts (after the prompt)
	private int _anchorTop;         // row where the buffer starts (corrected for scroll)
	private int _prevRenderLen;     // chars written last render, for trailing erase

	/// <summary>Read one line. Returns null on EOF (Ctrl+D on an empty line / redirected EOF).</summary>
	public string? ReadLine(string prompt)
		{
		if (Console.IsInputRedirected)
			{
			Console.Write(prompt);
			return Console.ReadLine();
			}

		_prompt = prompt;
		_buf.Clear();
		_cursor = 0;
		_prevRenderLen = 0;

		Console.Write(prompt);
		_anchorLeft = Console.CursorLeft;
		_anchorTop  = Console.CursorTop;

		int histIndex = _history.Count;     // _history.Count == the in-progress line
		string savedBottom = "";

		bool prevCtrlC = Console.TreatControlCAsInput;
		Console.TreatControlCAsInput = true;
		try
			{
			while (true)
				{
				var key = Console.ReadKey(intercept: true);
				bool ctrl = key.Modifiers.HasFlag(ConsoleModifiers.Control);

				switch (key.Key)
					{
					case ConsoleKey.Enter:
						MoveCursorToEnd();
						Console.WriteLine();
						return _buf.ToString();

					case ConsoleKey.Backspace:
						if (_cursor > 0) { _buf.Remove(_cursor - 1, 1); _cursor--; Render(); }
						continue;

					case ConsoleKey.Delete:
						if (_cursor < _buf.Length) { _buf.Remove(_cursor, 1); Render(); }
						continue;

					case ConsoleKey.LeftArrow:
						_cursor = ctrl ? WordLeft(_cursor) : Math.Max(0, _cursor - 1);
						Render();
						continue;

					case ConsoleKey.RightArrow:
						_cursor = ctrl ? WordRight(_cursor) : Math.Min(_buf.Length, _cursor + 1);
						Render();
						continue;

					case ConsoleKey.Home:
						_cursor = 0; Render(); continue;

					case ConsoleKey.End:
						_cursor = _buf.Length; Render(); continue;

					case ConsoleKey.UpArrow:
						if (histIndex > 0)
							{
							if (histIndex == _history.Count) savedBottom = _buf.ToString();
							histIndex--;
							SetBuffer(_history[histIndex]);
							}
						continue;

					case ConsoleKey.DownArrow:
						if (histIndex < _history.Count)
							{
							histIndex++;
							SetBuffer(histIndex == _history.Count ? savedBottom : _history[histIndex]);
							}
						continue;

					case ConsoleKey.Tab:
						DoComplete();
						continue;
					}

				// Control chords (by character) ------------------------------------
				if (ctrl)
					{
					switch (key.Key)
						{
						case ConsoleKey.A: _cursor = 0; Render(); continue;
						case ConsoleKey.E: _cursor = _buf.Length; Render(); continue;
						case ConsoleKey.B: _cursor = Math.Max(0, _cursor - 1); Render(); continue;
						case ConsoleKey.F: _cursor = Math.Min(_buf.Length, _cursor + 1); Render(); continue;
						case ConsoleKey.U:                       // kill to start of line
							if (_cursor > 0) { _buf.Remove(0, _cursor); _cursor = 0; Render(); }
							continue;
						case ConsoleKey.K:                       // kill to end of line
							if (_cursor < _buf.Length) { _buf.Remove(_cursor, _buf.Length - _cursor); Render(); }
							continue;
						case ConsoleKey.W:                       // delete word before cursor
							{
							int w = WordLeft(_cursor);
							if (w < _cursor) { _buf.Remove(w, _cursor - w); _cursor = w; Render(); }
							continue;
							}
						case ConsoleKey.L:                       // clear screen
							Console.Clear();
							Console.Write(_prompt);
							_anchorLeft = Console.CursorLeft; _anchorTop = Console.CursorTop;
							_prevRenderLen = 0; Render();
							continue;
						case ConsoleKey.R:                       // reverse incremental history search
							{
							var submit = ReverseSearch();
							if (submit is not null)
								{
								Console.SetCursorPosition(0, _anchorTop);
								Console.Write(_prompt + submit);
								Console.WriteLine();
								return submit;
								}
							continue;
							}
						case ConsoleKey.C:                       // cancel current line
							MoveCursorToEnd();
							Console.WriteLine("^C");
							return "";
						case ConsoleKey.D:                       // EOF on empty, else delete-forward
							if (_buf.Length == 0) { Console.WriteLine(); return null; }
							if (_cursor < _buf.Length) { _buf.Remove(_cursor, 1); Render(); }
							continue;
						}
					// Unhandled control chord — ignore.
					continue;
					}

				// Printable character ----------------------------------------------
				if (key.KeyChar != '\0' && !char.IsControl(key.KeyChar))
					{
					// Fast path: appending at the end of the line with room left on the
					// current row — just emit the char, no reposition/rewrite (no flicker).
					// Anything else (mid-line insert, or a write that would wrap) needs a
					// full redraw so the tail shifts and the scroll anchor stays correct.
					if (_cursor == _buf.Length && Console.CursorLeft < Console.BufferWidth - 1)
						{
						_buf.Append(key.KeyChar);
						_cursor++;
						_prevRenderLen = _buf.Length;
						Console.Write(key.KeyChar);
						}
					else
						{
						_buf.Insert(_cursor, key.KeyChar);
						_cursor++;
						Render();
						}
					}
				}
			}
		finally
			{
			Console.TreatControlCAsInput = prevCtrlC;
			}
		}

	// ── completion ──────────────────────────────────────────────────────────────

	private void DoComplete()
		{
		var r = _completion.Complete(_buf.ToString(), _cursor);
		if (r.Matches.Count == 0) return;

		if (r.Matches.Count == 1)
			{
			ReplaceSpan(r.Start, r.Length, r.Matches[0], addSpaceIfFile: r.SpaceOnSingle);
			return;
			}

		string lcp = LongestCommonPrefix(r.Matches);
		string token = _buf.ToString(r.Start, r.Length);
		if (lcp.Length > token.Length)
			{
			ReplaceSpan(r.Start, r.Length, lcp, addSpaceIfFile: false);
			return;
			}

		// No further common prefix — list the candidates, then redraw the line.
		MoveCursorToEnd();
		Console.WriteLine();
		PrintColumns(r.Matches);
		Console.Write(_prompt);
		_anchorLeft = Console.CursorLeft; _anchorTop = Console.CursorTop;
		_prevRenderLen = 0;
		Render();
		}

	private void ReplaceSpan(int start, int length, string replacement, bool addSpaceIfFile)
		{
		_buf.Remove(start, length);
		_buf.Insert(start, replacement);
		_cursor = start + replacement.Length;
		// A completed file (not a directory) gets a trailing space, like bash.
		if (addSpaceIfFile && !replacement.EndsWith('/') && !replacement.EndsWith('\\'))
			{ _buf.Insert(_cursor, ' '); _cursor++; }
		Render();
		}

	private static void PrintColumns(List<string> items)
		{
		int width = Math.Max(1, Console.BufferWidth);
		int colW  = items.Max(s => s.Length) + 2;
		int cols  = Math.Max(1, width / colW);
		for (int i = 0; i < items.Count; i++)
			{
			Console.Write(items[i].PadRight(colW));
			if ((i + 1) % cols == 0) Console.WriteLine();
			}
		if (items.Count % cols != 0) Console.WriteLine();
		}

	private static string LongestCommonPrefix(List<string> items)
		{
		if (items.Count == 0) return "";
		string prefix = items[0];
		foreach (var s in items.Skip(1))
			{
			int n = 0;
			while (n < prefix.Length && n < s.Length &&
			       char.ToLowerInvariant(prefix[n]) == char.ToLowerInvariant(s[n])) n++;
			prefix = prefix[..n];
			if (prefix.Length == 0) break;
			}
		return prefix;
		}

	// ── reverse incremental search (Ctrl+R) ─────────────────────────────────────

	/// <summary>
	/// Incremental reverse history search. Returns a line to submit (Enter), or null
	/// to resume editing (Esc/Ctrl+G cancels back to the original; a motion key
	/// accepts the current match into the editor). Single-line search UI: very long
	/// buffers may leave artifacts on wrapped rows below — refine if it proves
	/// bothersome in practice.
	/// </summary>
	private string? ReverseSearch()
		{
		string original = _buf.ToString();
		int originalCursor = _cursor;
		string query = "";
		int found = FindMatch(query, _history.Count - 1);

		DrawSearch(query, found);
		while (true)
			{
			var key = Console.ReadKey(intercept: true);
			bool ctrl = key.Modifiers.HasFlag(ConsoleModifiers.Control);

			if (key.Key == ConsoleKey.Enter)
				return found >= 0 ? _history[found] : original;

			if (key.Key == ConsoleKey.Escape || (ctrl && key.Key == ConsoleKey.G))
				{ RestoreLine(original, originalCursor); return null; }

			if (ctrl && key.Key == ConsoleKey.R)            // step to next older match
				{
				if (found > 0)
					{ int f = FindMatch(query, found - 1); if (f >= 0) found = f; }
				DrawSearch(query, found);
				continue;
				}

			if (key.Key == ConsoleKey.Backspace)
				{
				if (query.Length > 0) query = query[..^1];
				found = FindMatch(query, _history.Count - 1);
				DrawSearch(query, found);
				continue;
				}

			if (key.Key is ConsoleKey.LeftArrow or ConsoleKey.RightArrow
			            or ConsoleKey.Home or ConsoleKey.End)
				{
				string accepted = found >= 0 ? _history[found] : original;
				RestoreLine(accepted, accepted.Length);
				return null;
				}

			if (!ctrl && key.KeyChar != '\0' && !char.IsControl(key.KeyChar))
				{
				query += key.KeyChar;
				found = FindMatch(query, _history.Count - 1);
				DrawSearch(query, found);
				}
			// other keys ignored while searching
			}
		}

	private int FindMatch(string query, int from)
		{
		if (_history.Count == 0) return -1;
		int begin = Math.Min(from, _history.Count - 1);
		if (query.Length == 0) return begin;
		for (int i = begin; i >= 0; i--)
			if (_history[i].Contains(query, StringComparison.OrdinalIgnoreCase)) return i;
		return -1;
		}

	private void DrawSearch(string query, int found)
		{
		string match = found >= 0 ? _history[found] : "";
		string text  = $"(reverse-i-search)`{query}': {match}";
		int width = Math.Max(1, Console.BufferWidth);
		if (text.Length > width - 1) text = text[..(width - 1)];

		bool wasVisible = CursorVisibleSafe();
		Console.CursorVisible = false;
		try
			{
			Console.SetCursorPosition(0, _anchorTop);
			Console.Write(text);
			int rem = width - 1 - text.Length;
			if (rem > 0) Console.Write(new string(' ', rem));
			Console.SetCursorPosition(text.Length, _anchorTop);
			}
		finally { Console.CursorVisible = wasVisible; }
		}

	private void RestoreLine(string content, int cursor)
		{
		int width = Math.Max(1, Console.BufferWidth);
		Console.SetCursorPosition(0, _anchorTop);
		Console.Write(new string(' ', width - 1));
		Console.SetCursorPosition(0, _anchorTop);
		Console.Write(_prompt);
		_anchorLeft = Console.CursorLeft;
		_anchorTop  = Console.CursorTop;
		_prevRenderLen = 0;
		_buf.Clear(); _buf.Append(content);
		_cursor = Math.Min(cursor, _buf.Length);
		Render();
		}

	// ── rendering ───────────────────────────────────────────────────────────────

	private void SetBuffer(string value)
		{
		_buf.Clear();
		_buf.Append(value);
		_cursor = _buf.Length;
		Render();
		}

	private void Render()
		{
		// Hide the hardware cursor while repositioning + rewriting the line, otherwise
		// it visibly darts to the anchor and back on every keystroke (the flicker).
		bool wasVisible = CursorVisibleSafe();
		Console.CursorVisible = false;
		try
			{
			int width = Math.Max(1, Console.BufferWidth);
			Console.SetCursorPosition(_anchorLeft, _anchorTop);

			string text = _buf.ToString();
			int pad = Math.Max(0, _prevRenderLen - text.Length);
			Console.Write(text);
			if (pad > 0) Console.Write(new string(' ', pad));
			_prevRenderLen = text.Length;

			// Re-derive the anchor row from where the cursor ended up (handles scroll).
			int consumed     = _anchorLeft + text.Length + pad;
			int rowsAdvanced = consumed / width;
			_anchorTop = Math.Max(0, Console.CursorTop - rowsAdvanced);

			MoveCursorToLogical();
			}
		finally { Console.CursorVisible = wasVisible; }
		}

	private static bool CursorVisibleSafe()
		{
		try { return Console.CursorVisible; }
		catch { return true; }   // getter is unsupported on some platforms
		}

	private void MoveCursorToLogical()
		{
		int width = Math.Max(1, Console.BufferWidth);
		int pos = _anchorLeft + _cursor;
		int row = Math.Min(Console.BufferHeight - 1, _anchorTop + pos / width);
		int col = pos % width;
		Console.SetCursorPosition(col, row);
		}

	private void MoveCursorToEnd()
		{
		_cursor = _buf.Length;
		MoveCursorToLogical();
		}

	// ── word motion ─────────────────────────────────────────────────────────────

	private int WordLeft(int pos)
		{
		int i = pos;
		while (i > 0 && char.IsWhiteSpace(_buf[i - 1])) i--;
		while (i > 0 && !char.IsWhiteSpace(_buf[i - 1])) i--;
		return i;
		}

	private int WordRight(int pos)
		{
		int i = pos;
		while (i < _buf.Length && char.IsWhiteSpace(_buf[i])) i++;
		while (i < _buf.Length && !char.IsWhiteSpace(_buf[i])) i++;
		return i;
		}
	}
