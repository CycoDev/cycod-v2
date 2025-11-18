using Markdig;
using Spectre.Console;

namespace GlowSharp;

/// <summary>
/// Main markdown renderer class.
/// This is the C# equivalent of Glamour's TermRenderer.
/// Ported from glow/main.go executeCLI function (lines 273-312).
/// </summary>
public class MarkdownRenderer
{
    private readonly StyleConfig _style;
    private readonly int _width;
    private readonly MarkdownPipeline _pipeline;

    public MarkdownRenderer(StyleConfig? style = null, int width = 0)
    {
        _style = style ?? StyleConfig.AutoStyle;
        _width = width > 0 ? width : GetTerminalWidth();

        // Build Markdig pipeline with extensions (similar to goldmark extensions)
        _pipeline = new MarkdownPipelineBuilder()
            .UseAdvancedExtensions()      // Tables, task lists, etc.
            .UseEmojiAndSmiley()          // GitHub-style emoji (like goldmark-emoji)
            .UsePipeTables()              // Pipe tables
            .UseGridTables()              // Grid tables
            .UseListExtras()              // Extra list features
            .UseEmphasisExtras()          // Strikethrough, subscript, superscript
            .UseAutoLinks()               // Auto-convert URLs to links
            .Build();
    }

    /// <summary>
    /// Renders markdown content to ANSI-styled terminal output.
    /// Ported from executeCLI in main.go:273-312
    /// </summary>
    public string Render(string content, string? filePath = null)
    {
        // Step 1: Remove frontmatter (from utils/utils.go:18-23)
        content = MarkdownUtils.RemoveFrontmatter(content);

        // Step 2: Check if this is a code file, not markdown (from main.go:289)
        var isCode = !MarkdownUtils.IsMarkdownFile(filePath);

        // Step 3: If code file, wrap in code block (from main.go:305-307)
        if (isCode && !string.IsNullOrEmpty(filePath))
        {
            var ext = Path.GetExtension(filePath);
            content = MarkdownUtils.WrapCodeBlock(content, ext);
        }

        // Step 4: Parse markdown with Markdig (equivalent to goldmark parsing)
        var document = Markdown.Parse(content, _pipeline);

        // Step 5: Render to ANSI (equivalent to glamour.Render in main.go:309)
        var renderer = new AnsiRenderer(_style, _width);
        var output = renderer.Render(document);

        return output;
    }

    /// <summary>
    /// Renders markdown from a file.
    /// </summary>
    public string RenderFile(string filePath)
    {
        var content = File.ReadAllText(filePath);
        return Render(content, filePath);
    }

    /// <summary>
    /// Renders markdown from stdin.
    /// </summary>
    public string RenderStdin()
    {
        using var reader = Console.In;
        var content = reader.ReadToEnd();
        return Render(content);
    }

    /// <summary>
    /// Gets the terminal width, with a default of 80 and max of 120.
    /// Ported from main.go:192-207
    /// </summary>
    private static int GetTerminalWidth()
    {
        try
        {
            var width = Console.WindowWidth;

            // Match glow's behavior: max width of 120
            if (width > 120)
            {
                width = 120;
            }

            return width > 0 ? width : 80;
        }
        catch
        {
            return 80; // Default fallback
        }
    }

    /// <summary>
    /// Builder pattern for configuring the renderer.
    /// </summary>
    public class Builder
    {
        private StyleConfig? _style;
        private int _width;

        public Builder WithStyle(StyleConfig style)
        {
            _style = style;
            return this;
        }

        public Builder WithDarkStyle()
        {
            _style = StyleConfig.DarkStyle;
            return this;
        }

        public Builder WithLightStyle()
        {
            _style = StyleConfig.LightStyle;
            return this;
        }

        public Builder WithAutoStyle()
        {
            _style = StyleConfig.AutoStyle;
            return this;
        }

        public Builder WithWidth(int width)
        {
            _width = width;
            return this;
        }

        public MarkdownRenderer Build()
        {
            return new MarkdownRenderer(_style, _width);
        }
    }
}
