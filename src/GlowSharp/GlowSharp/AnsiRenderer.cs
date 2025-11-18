using System.Text;
using Markdig.Extensions.Tables;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;
using Spectre.Console;
using SpectreStyle = Spectre.Console.Style;
using MarkdigTable = Markdig.Extensions.Tables.Table;
using MarkdigTableRow = Markdig.Extensions.Tables.TableRow;
using MarkdigTableCell = Markdig.Extensions.Tables.TableCell;

namespace GlowSharp;

/// <summary>
/// Renders Markdig AST to ANSI-styled terminal output.
/// Inspired by Glamour's ANSI renderer.
/// </summary>
public class AnsiRenderer
{
    private readonly StyleConfig _style;
    private readonly int _width;
    private readonly StringBuilder _output;
    private int _listLevel = 0;
    private int _listItemIndex = 0;

    public AnsiRenderer(StyleConfig style, int width)
    {
        _style = style;
        _width = width > 0 ? width : 80;
        _output = new StringBuilder();
    }

    public string Render(MarkdownDocument document)
    {
        _output.Clear();

        foreach (var block in document)
        {
            RenderBlock(block);
        }

        return _output.ToString();
    }

    private void RenderBlock(Block block)
    {
        switch (block)
        {
            case HeadingBlock heading:
                RenderHeading(heading);
                break;
            case ParagraphBlock paragraph:
                RenderParagraph(paragraph);
                break;
            case FencedCodeBlock fencedCodeBlock:
                RenderFencedCodeBlock(fencedCodeBlock);
                break;
            case CodeBlock codeBlock:
                RenderCodeBlock(codeBlock);
                break;
            case QuoteBlock quote:
                RenderQuoteBlock(quote);
                break;
            case ListBlock list:
                RenderListBlock(list);
                break;
            case ThematicBreakBlock:
                RenderHorizontalRule();
                break;
            case MarkdigTable table:
                RenderTable(table);
                break;
            default:
                // Fallback for other block types
                if (block is ContainerBlock container)
                {
                    foreach (var child in container)
                    {
                        RenderBlock(child);
                    }
                }
                break;
        }
    }

    private void RenderHeading(HeadingBlock heading)
    {
        var style = heading.Level switch
        {
            1 => _style.H1,
            2 => _style.H2,
            3 => _style.H3,
            4 => _style.H4,
            5 => _style.H5,
            _ => _style.H6
        };

        _output.Append(style.Prefix);

        var text = GetInlineText(heading.Inline);
        _output.Append(ApplyStyle(text, style));

        _output.Append(style.Suffix);
        _output.AppendLine();
        _output.AppendLine();
    }

    private void RenderParagraph(ParagraphBlock paragraph)
    {
        if (paragraph.Inline != null)
        {
            var text = RenderInlines(paragraph.Inline);
            _output.Append(WrapText(text, _width));
            _output.AppendLine();
            _output.AppendLine();
        }
    }

    private void RenderCodeBlock(CodeBlock codeBlock)
    {
        var code = GetCodeBlockContent(codeBlock);
        RenderHighlightedCode(code, "");
    }

    private void RenderFencedCodeBlock(FencedCodeBlock fencedCodeBlock)
    {
        var code = GetCodeBlockContent(fencedCodeBlock);
        var language = fencedCodeBlock.Info ?? "";
        RenderHighlightedCode(code, language);
    }

    private void RenderHighlightedCode(string code, string language)
    {
        // Add margin
        for (int i = 0; i < _style.CodeBlock.Margin; i++)
        {
            _output.AppendLine();
        }

        // Render code with syntax highlighting
        var highlightedCode = SyntaxHighlighter.Highlight(code, language, _style.CodeBlock.Theme);

        // Indent the code block slightly
        var lines = highlightedCode.Split('\n');
        foreach (var line in lines)
        {
            if (!string.IsNullOrEmpty(line))
            {
                _output.Append("  "); // 2 space indent
            }
            _output.AppendLine(line);
        }

        // Add margin
        for (int i = 0; i < _style.CodeBlock.Margin; i++)
        {
            _output.AppendLine();
        }
    }

    private void RenderQuoteBlock(QuoteBlock quote)
    {
        var quoteStyle = _style.BlockQuote;
        var indent = new string(' ', quoteStyle.Indent);

        foreach (var block in quote)
        {
            // Temporarily capture output
            var tempOutput = new StringBuilder();
            var originalLength = _output.Length;

            RenderBlock(block);

            // Get the rendered block text
            var blockText = _output.ToString()[originalLength..];

            // Remove the block text we just added
            _output.Length = originalLength;

            // Add prefix and indent to each line
            var lines = blockText.Split('\n');
            foreach (var line in lines)
            {
                if (!string.IsNullOrWhiteSpace(line))
                {
                    _output.AppendLine(ApplyStyle($"{indent}{quoteStyle.Prefix}{line}", quoteStyle));
                }
            }
        }
        _output.AppendLine();
    }

    private void RenderListBlock(ListBlock list)
    {
        var currentListLevel = _listLevel;
        _listLevel++;
        var indent = new string(' ', _listLevel * _style.List.Indent);
        var currentItemIndex = _listItemIndex;

        foreach (ListItemBlock item in list)
        {
            if (!list.IsOrdered)
            {
                _output.Append($"{indent}{_style.List.BulletChar} ");
            }
            else
            {
                currentItemIndex++;
                _output.Append($"{indent}{currentItemIndex}. ");
            }

            foreach (var block in item)
            {
                if (block is ParagraphBlock para)
                {
                    var text = RenderInlines(para.Inline);
                    _output.AppendLine(text);
                }
                else
                {
                    RenderBlock(block);
                }
            }
        }

        _listLevel = currentListLevel;
        if (_listLevel == 0)
        {
            _listItemIndex = 0;
            _output.AppendLine();
        }
        else
        {
            _listItemIndex = currentItemIndex;
        }
    }

    private void RenderHorizontalRule()
    {
        var rule = new string('─', Math.Min(_width, 80));
        _output.Append("\u001b[90m"); // Gray
        _output.Append(rule);
        _output.AppendLine("\u001b[0m"); // Reset
        _output.AppendLine();
    }

    private void RenderTable(MarkdigTable table)
    {
        _output.AppendLine();

        // Extract table data
        var rows = new List<List<string>>();
        var columnCount = 0;

        foreach (var tableRow in table.Cast<MarkdigTableRow>())
        {
            var rowData = new List<string>();
            foreach (var cell in tableRow.Cast<MarkdigTableCell>())
            {
                // TableCell is a container, extract text from its paragraph
                var cellText = "";
                foreach (var block in cell)
                {
                    if (block is ParagraphBlock para)
                    {
                        cellText = GetInlineText(para.Inline);
                        break;
                    }
                }
                rowData.Add(cellText);
            }
            rows.Add(rowData);
            columnCount = Math.Max(columnCount, rowData.Count);
        }

        if (rows.Count == 0 || columnCount == 0)
            return;

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
            columnWidths[col] = Math.Max(maxWidth, 3); // Minimum width of 3
        }

        // Render header row (first row)
        if (rows.Count > 0)
        {
            _output.Append("  "); // Indent
            for (int col = 0; col < columnCount; col++)
            {
                var cell = col < rows[0].Count ? rows[0][col] : "";
                _output.Append("\u001b[1m"); // Bold for headers
                _output.Append(cell.PadRight(columnWidths[col]));
                _output.Append("\u001b[0m");
                if (col < columnCount - 1)
                {
                    _output.Append(" │ ");
                }
            }
            _output.AppendLine();

            // Render separator
            _output.Append("  ");
            for (int col = 0; col < columnCount; col++)
            {
                _output.Append(new string('─', columnWidths[col]));
                if (col < columnCount - 1)
                {
                    _output.Append("─┼─");
                }
            }
            _output.AppendLine();
        }

        // Render data rows (skip header row)
        for (int rowIdx = 1; rowIdx < rows.Count; rowIdx++)
        {
            _output.Append("  "); // Indent
            var row = rows[rowIdx];
            for (int col = 0; col < columnCount; col++)
            {
                var cell = col < row.Count ? row[col] : "";
                _output.Append(cell.PadRight(columnWidths[col]));
                if (col < columnCount - 1)
                {
                    _output.Append(" │ ");
                }
            }
            _output.AppendLine();
        }

        _output.AppendLine();
    }

    private string RenderInlines(ContainerInline? inlineContainer)
    {
        if (inlineContainer == null) return "";

        var result = new StringBuilder();

        foreach (var inline in inlineContainer)
        {
            result.Append(RenderInline(inline));
        }

        return result.ToString();
    }

    private string RenderInline(Inline inline)
    {
        return inline switch
        {
            LiteralInline literal => literal.Content.ToString(),
            CodeInline code => RenderCodeInline(code),
            EmphasisInline emphasis => RenderEmphasis(emphasis),
            LinkInline link => RenderLink(link),
            LineBreakInline => "\n",
            _ => GetInlineText(inline)
        };
    }

    private string RenderCodeInline(CodeInline code)
    {
        var style = _style.Code;
        var text = code.Content;
        return ApplyStyle($"{style.Prefix}{text}{style.Suffix}", style);
    }

    private string RenderEmphasis(EmphasisInline emphasis)
    {
        var text = GetInlineText(emphasis);
        var style = emphasis.DelimiterCount == 2 ? _style.Strong : _style.Emphasis;

        if (emphasis.DelimiterChar == '~')
        {
            style = _style.Strikethrough;
        }

        return ApplyStyle(text, style);
    }

    private string RenderLink(LinkInline link)
    {
        var text = GetInlineText(link);
        var url = link.Url ?? "";

        var style = _style.Link;
        var linkText = string.IsNullOrEmpty(url) ? text : $"{text} ({url})";
        return ApplyStyle(linkText, style);
    }

    private string GetInlineText(Inline? inline)
    {
        if (inline == null) return "";

        var result = new StringBuilder();

        if (inline is ContainerInline container)
        {
            foreach (var child in container)
            {
                result.Append(GetInlineText(child));
            }
        }
        else if (inline is LiteralInline literal)
        {
            result.Append(literal.Content.ToString());
        }

        return result.ToString();
    }

    private string GetCodeBlockContent(LeafBlock codeBlock)
    {
        var lines = codeBlock.Lines;
        var result = new StringBuilder();

        for (int i = 0; i < lines.Count; i++)
        {
            var line = lines.Lines[i];
            result.AppendLine(line.Slice.ToString());
        }

        return result.ToString().TrimEnd();
    }

    private string WrapText(string text, int width)
    {
        if (width <= 0 || text.Length <= width)
        {
            return text;
        }

        var result = new StringBuilder();
        var words = text.Split(' ');
        var currentLine = new StringBuilder();

        foreach (var word in words)
        {
            if (currentLine.Length + word.Length + 1 > width)
            {
                result.AppendLine(currentLine.ToString());
                currentLine.Clear();
            }

            if (currentLine.Length > 0)
            {
                currentLine.Append(' ');
            }
            currentLine.Append(word);
        }

        if (currentLine.Length > 0)
        {
            result.Append(currentLine.ToString());
        }

        return result.ToString();
    }

    /// <summary>
    /// Applies ANSI styling to text based on TextStyle
    /// </summary>
    private string ApplyStyle(string text, TextStyle style)
    {
        if (string.IsNullOrEmpty(text))
            return text;

        var sb = new StringBuilder();

        // Build ANSI escape code
        var codes = new List<string>();

        // Color
        if (style.Color.HasValue)
        {
            codes.Add(ColorToAnsi(style.Color.Value, false));
        }

        // Background color
        if (style.BackgroundColor.HasValue)
        {
            codes.Add(ColorToAnsi(style.BackgroundColor.Value, true));
        }

        // Text decorations
        if (style.Bold) codes.Add("1");
        if (style.Italic) codes.Add("3");
        if (style.Underline) codes.Add("4");
        if (style.Strikethrough) codes.Add("9");

        // Apply codes if any
        if (codes.Count > 0)
        {
            sb.Append($"\u001b[{string.Join(";", codes)}m");
        }

        sb.Append(text);

        // Reset
        if (codes.Count > 0)
        {
            sb.Append("\u001b[0m");
        }

        return sb.ToString();
    }

    /// <summary>
    /// Converts Spectre.Console Color to ANSI code
    /// </summary>
    private string ColorToAnsi(Color color, bool isBackground)
    {
        // Map common Spectre colors to ANSI codes
        var offset = isBackground ? 10 : 0;

        // Try to match by name
        var colorName = color.ToString().ToLowerInvariant();

        return colorName switch
        {
            "black" => (30 + offset).ToString(),
            "red" => (31 + offset).ToString(),
            "green" => (32 + offset).ToString(),
            "yellow" => (33 + offset).ToString(),
            "blue" => (34 + offset).ToString(),
            "magenta" or "fuchsia" => (35 + offset).ToString(),
            "cyan" or "cyan1" => (36 + offset).ToString(),
            "white" => (37 + offset).ToString(),
            "grey" or "gray" or "grey19" => (90 + offset).ToString(),
            "grey93" => (97 + offset).ToString(),
            _ => isBackground ? "49" : "39" // Default
        };
    }
}
