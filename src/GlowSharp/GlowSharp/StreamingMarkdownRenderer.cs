using System.Text;

namespace GlowSharp;

/// <summary>
/// Renders markdown progressively as chunks arrive (e.g., from LLM streaming).
/// Buffers complex structures (code blocks, tables) and renders simple text immediately.
/// </summary>
public class StreamingMarkdownRenderer
{
    private readonly StyleConfig _style;
    private readonly StringBuilder _buffer = new();
    private ParserState _state = ParserState.None;
    private readonly StringBuilder _blockBuffer = new();
    private string _codeBlockLanguage = "";
    private readonly List<string> _tableLines = new();

    /// <summary>
    /// Event triggered when markdown is rendered. Subscribe to this to receive ANSI-formatted output
    /// instead of writing to Console. If null, output goes to Console.
    /// </summary>
    public event Action<string>? OnOutput;

    public StreamingMarkdownRenderer(StyleConfig? style = null)
    {
        _style = style ?? StyleConfig.DarkStyle;
    }

    /// <summary>
    /// Emit output - either to callback or Console
    /// </summary>
    private void Emit(string text)
    {
        if (OnOutput != null)
            OnOutput(text);
        else
            Console.Write(text);
    }

    /// <summary>
    /// Emit output with newline - either to callback or Console
    /// </summary>
    private void EmitLine(string text = "")
    {
        if (OnOutput != null)
            OnOutput(text + "\n");
        else
            Console.WriteLine(text);
    }

    /// <summary>
    /// Process a chunk of markdown text. Renders immediately where possible,
    /// buffers complex blocks until complete.
    /// </summary>
    public void AppendChunk(string chunk)
    {
        _buffer.Append(chunk);
        ProcessBuffer();
    }

    /// <summary>
    /// Flush any remaining buffered content. Call when stream ends.
    /// </summary>
    public void Flush()
    {
        // Process any remaining buffer
        if (_buffer.Length > 0)
        {
            ProcessBuffer();
        }

        // Flush incomplete blocks
        switch (_state)
        {
            case ParserState.InCodeBlock:
                RenderCodeBlock();
                break;
            case ParserState.InTable:
                RenderTable();
                break;
            case ParserState.InBlockBuffer:
                FlushBlockBuffer();
                break;
        }

        _state = ParserState.None;
        _buffer.Clear();
    }

    private void ProcessBuffer()
    {
        while (_buffer.Length > 0)
        {
            var previousLength = _buffer.Length;

            switch (_state)
            {
                case ParserState.None:
                    ProcessNormalText();
                    break;
                case ParserState.InCodeBlock:
                    ProcessCodeBlock();
                    break;
                case ParserState.InTable:
                    ProcessTable();
                    break;
                case ParserState.InBlockBuffer:
                    ProcessBlockBuffer();
                    break;
                default:
                    return;
            }

            // If we didn't consume anything from the buffer, we're waiting for more data
            // Break out of the loop to avoid infinite looping
            if (_buffer.Length == previousLength)
            {
                break;
            }
        }
    }

    private void ProcessNormalText()
    {
        var text = _buffer.ToString();

        // Check for code block start
        if (text.Contains("```"))
        {
            var idx = text.IndexOf("```");

            // Output everything before the code block
            if (idx > 0)
            {
                var before = text.Substring(0, idx);
                OutputText(before);
                _buffer.Remove(0, idx);
                return;
            }

            // Check if we have the language identifier on same line
            var langNewlineIdx = text.IndexOf('\n', idx);
            if (langNewlineIdx > 0)
            {
                _codeBlockLanguage = text.Substring(idx + 3, langNewlineIdx - idx - 3).Trim();
                _buffer.Remove(0, langNewlineIdx + 1);
                _blockBuffer.Clear();
                _state = ParserState.InCodeBlock;
                return;
            }

            // Need more data to get language
            return;
        }

        // Check for table start (line starting with |)
        var lines = text.Split('\n');
        for (int i = 0; i < lines.Length; i++)
        {
            var line = lines[i];

            if (line.TrimStart().StartsWith("|") && line.TrimEnd().EndsWith("|"))
            {
                // Output everything before table
                if (i > 0)
                {
                    var before = string.Join("\n", lines[..i]);
                    OutputText(before);
                    _buffer.Remove(0, before.Length);
                }

                _tableLines.Clear();
                _tableLines.Add(line);
                _buffer.Remove(0, line.Length);
                if (_buffer.Length > 0 && _buffer[0] == '\n')
                {
                    _buffer.Remove(0, 1);
                }
                _state = ParserState.InTable;
                return;
            }
        }

        // Check for horizontal rule (---, ***, ___)
        var hrNewlineIdx = text.IndexOf('\n');
        if (hrNewlineIdx >= 0)
        {
            var line = text.Substring(0, hrNewlineIdx);
            var trimmedLine = line.Trim();

            // Check if it's a horizontal rule (3+ dashes, asterisks, or underscores)
            if ((trimmedLine.All(c => c == '-' || c == ' ') && trimmedLine.Replace(" ", "").Length >= 3) ||
                (trimmedLine.All(c => c == '*' || c == ' ') && trimmedLine.Replace(" ", "").Length >= 3) ||
                (trimmedLine.All(c => c == '_' || c == ' ') && trimmedLine.Replace(" ", "").Length >= 3))
            {
                // Render horizontal rule
                RenderHorizontalRule();
                _buffer.Remove(0, hrNewlineIdx + 1);
                return;
            }
        }

        // Check for heading or other block elements that should wait for newline
        if (text.StartsWith("#") || text.StartsWith(">"))
        {
            var headingNewlineIdx = text.IndexOf('\n');
            if (headingNewlineIdx >= 0)
            {
                var line = text.Substring(0, headingNewlineIdx + 1);
                OutputText(line);
                _buffer.Remove(0, headingNewlineIdx + 1);
                return;
            }
            // Wait for complete line
            return;
        }

        // For plain text, check if we have complete lines
        var newlineIdx = text.IndexOf('\n');
        if (newlineIdx >= 0)
        {
            var line = text.Substring(0, newlineIdx + 1);
            OutputText(line);
            _buffer.Remove(0, newlineIdx + 1);
            return;
        }

        // If buffer is getting large (more than 100 chars) without newline, flush it
        if (_buffer.Length > 100)
        {
            OutputText(_buffer.ToString());
            _buffer.Clear();
        }
    }

    private void ProcessCodeBlock()
    {
        var text = _buffer.ToString();
        var closeIdx = text.IndexOf("```");

        if (closeIdx >= 0)
        {
            // Found end of code block
            var code = text.Substring(0, closeIdx);
            _blockBuffer.Append(code);

            // Render the complete code block
            RenderCodeBlock();

            // Remove processed text including closing ```
            var endIdx = closeIdx + 3;
            if (endIdx < text.Length && text[endIdx] == '\n')
            {
                endIdx++;
            }
            _buffer.Remove(0, endIdx);

            _state = ParserState.None;
            _blockBuffer.Clear();
        }
        else
        {
            // Keep buffering, but don't clear everything - we need to keep the last few chars
            // in case the closing ``` is split across chunks
            // Keep the last 2 characters in the buffer to handle split markers
            if (text.Length > 2)
            {
                var toBuffer = text.Substring(0, text.Length - 2);
                _blockBuffer.Append(toBuffer);
                _buffer.Remove(0, toBuffer.Length);
            }
            // else: buffer too small, wait for more data
        }
    }

    private void ProcessTable()
    {
        var text = _buffer.ToString();
        var newlineIdx = text.IndexOf('\n');

        // Wait for a complete line
        if (newlineIdx < 0)
        {
            return;
        }

        var line = text.Substring(0, newlineIdx);

        // Check if this is the end of the table (blank line or non-table line)
        if (string.IsNullOrWhiteSpace(line) ||
            !(line.TrimStart().StartsWith("|") && line.TrimEnd().EndsWith("|")))
        {
            // Render the table and switch back to normal mode
            if (_tableLines.Count > 0)
            {
                RenderTable();
                _tableLines.Clear();
            }
            _state = ParserState.None;
            _buffer.Remove(0, newlineIdx + 1);
            return;
        }

        // This is a table line - add it to the collection
        _tableLines.Add(line);
        _buffer.Remove(0, newlineIdx + 1);
    }

    private void ProcessBlockBuffer()
    {
        // Generic block buffering for future use
        var text = _buffer.ToString();
        if (text.Contains("\n\n"))
        {
            FlushBlockBuffer();
            _state = ParserState.None;
        }
    }

    private void OutputText(string text)
    {
        if (string.IsNullOrEmpty(text)) return;

        // Apply basic formatting
        var formatted = FormatInlineText(text);
        Emit(formatted);
    }

    private string FormatInlineText(string text)
    {
        // Strip leading newlines/whitespace to check for headings
        var trimmed = text.TrimStart('\n', '\r');

        // Handle headings (ATX-style: # Heading)
        if (trimmed.StartsWith("#"))
        {
            var level = 0;
            while (level < trimmed.Length && trimmed[level] == '#')
                level++;

            if (level > 0 && level < trimmed.Length && trimmed[level] == ' ')
            {
                var headerText = trimmed.Substring(level + 1).TrimEnd();
                var color = level switch
                {
                    1 => "\u001b[35;1m", // Magenta + Bold
                    2 => "\u001b[39;1m", // Default + Bold
                    3 => "\u001b[36;1m", // Cyan + Bold
                    4 => "\u001b[32;1m", // Green + Bold
                    5 => "\u001b[33;3m", // Yellow + Italic
                    _ => "\u001b[90;3m"  // Gray + Italic
                };

                // Preserve leading newlines but render heading
                var leadingNewlines = text.Substring(0, text.Length - trimmed.Length);
                return $"{leadingNewlines}{color}{headerText}\u001b[0m\n";
            }
        }

        // Handle blockquotes
        if (text.TrimStart().StartsWith(">"))
        {
            var quoted = text.TrimStart().Substring(1).TrimStart();
            return $"\u001b[90;3m  │ {quoted}\u001b[0m";
        }

        // Handle bold **text** or __text__
        text = System.Text.RegularExpressions.Regex.Replace(
            text, @"\*\*(.+?)\*\*", m => $"\u001b[1m{m.Groups[1].Value}\u001b[0m");
        text = System.Text.RegularExpressions.Regex.Replace(
            text, @"__(.+?)__", m => $"\u001b[1m{m.Groups[1].Value}\u001b[0m");

        // Handle italic *text* or _text_
        text = System.Text.RegularExpressions.Regex.Replace(
            text, @"\*(.+?)\*", m => $"\u001b[3m{m.Groups[1].Value}\u001b[0m");
        text = System.Text.RegularExpressions.Regex.Replace(
            text, @"_(.+?)_", m => $"\u001b[3m{m.Groups[1].Value}\u001b[0m");

        // Handle strikethrough ~~text~~
        text = System.Text.RegularExpressions.Regex.Replace(
            text, @"~~(.+?)~~", m => $"\u001b[9m{m.Groups[1].Value}\u001b[0m");

        // Handle inline code `code`
        text = System.Text.RegularExpressions.Regex.Replace(
            text, @"`(.+?)`", m => $"\u001b[33;100m{m.Groups[1].Value}\u001b[0m");

        return text;
    }

    private void RenderCodeBlock()
    {
        var code = _blockBuffer.ToString();
        EmitLine(); // Margin before

        var highlighted = SyntaxHighlighter.Highlight(code, _codeBlockLanguage, _style.CodeBlock.Theme);
        var lines = highlighted.Split('\n');

        foreach (var line in lines)
        {
            if (!string.IsNullOrEmpty(line))
            {
                Emit("  "); // Indent
            }
            EmitLine(line);
        }

        EmitLine(); // Margin after
    }

    private void RenderHorizontalRule()
    {
        // Render a horizontal line using Unicode box-drawing characters
        var rule = new string('─', 80);
        EmitLine($"\u001b[90m{rule}\u001b[0m"); // Gray color
        EmitLine();
    }

    private void RenderTable()
    {
        if (_tableLines.Count == 0) return;

        EmitLine();

        // Parse table structure
        var rows = new List<List<string>>();
        var columnCount = 0;

        foreach (var line in _tableLines)
        {
            // Skip separator line (|---|---|)
            if (line.Contains("---"))
                continue;

            var cells = line.Split('|')
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .Select(s => s.Trim())
                .ToList();

            if (cells.Count > 0)
            {
                rows.Add(cells);
                columnCount = Math.Max(columnCount, cells.Count);
            }
        }

        if (rows.Count == 0) return;

        // Calculate column widths
        var columnWidths = new int[columnCount];
        for (int col = 0; col < columnCount; col++)
        {
            var maxWidth = 0;
            foreach (var row in rows)
            {
                if (col < row.Count)
                {
                    maxWidth = Math.Max(maxWidth, row[col].Length);
                }
            }
            columnWidths[col] = Math.Max(maxWidth, 3);
        }

        // Render header
        if (rows.Count > 0)
        {
            var headerLine = new StringBuilder("  ");
            for (int col = 0; col < columnCount; col++)
            {
                var cell = col < rows[0].Count ? rows[0][col] : "";
                headerLine.Append("\u001b[1m");
                headerLine.Append(cell.PadRight(columnWidths[col]));
                headerLine.Append("\u001b[0m");
                if (col < columnCount - 1)
                {
                    headerLine.Append(" │ ");
                }
            }
            EmitLine(headerLine.ToString());

            // Separator
            var separatorLine = new StringBuilder("  ");
            for (int col = 0; col < columnCount; col++)
            {
                separatorLine.Append(new string('─', columnWidths[col]));
                if (col < columnCount - 1)
                {
                    separatorLine.Append("─┼─");
                }
            }
            EmitLine(separatorLine.ToString());
        }

        // Render data rows
        for (int rowIdx = 1; rowIdx < rows.Count; rowIdx++)
        {
            var dataLine = new StringBuilder("  ");
            var row = rows[rowIdx];
            for (int col = 0; col < columnCount; col++)
            {
                var cell = col < row.Count ? row[col] : "";
                dataLine.Append(cell.PadRight(columnWidths[col]));
                if (col < columnCount - 1)
                {
                    dataLine.Append(" │ ");
                }
            }
            EmitLine(dataLine.ToString());
        }

        EmitLine();
    }

    private void FlushBlockBuffer()
    {
        OutputText(_blockBuffer.ToString());
        _blockBuffer.Clear();
    }

    private enum ParserState
    {
        None,
        InCodeBlock,
        InTable,
        InBlockBuffer
    }
}
