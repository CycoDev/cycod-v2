# GlowSharp Usage Guide

## Quick Start

### Build and Run

```bash
cd GlowSharp/GlowSharp
dotnet build
dotnet run -- ../example.md
```

### Command Line Options

```bash
# Basic usage
dotnet run -- <file.md>

# Read from stdin
echo "# Hello" | dotnet run -- -

# Use specific style
dotnet run -- --style dark file.md
dotnet run -- --style light file.md

# Set width
dotnet run -- --width 100 file.md
```

## Using as a Library

### Basic Example

```csharp
using GlowSharp;

var renderer = new MarkdownRenderer();
var output = renderer.Render("# Hello **World**\n\nThis is *markdown*.");
Console.Write(output);
```

### With Configuration

```csharp
using GlowSharp;

// Use builder pattern
var renderer = new MarkdownRenderer.Builder()
    .WithDarkStyle()
    .WithWidth(100)
    .Build();

var output = renderer.RenderFile("README.md");
Console.Write(output);
```

### Render Code Files

```csharp
using GlowSharp;

var renderer = new MarkdownRenderer();

// Automatically wraps in code block with syntax info
var output = renderer.RenderFile("MyClass.cs");
Console.Write(output);
```

## Integration in Your CLI App

```csharp
using GlowSharp;
using System.CommandLine;

var rootCommand = new RootCommand();
var fileArg = new Argument<string>("file");
rootCommand.AddArgument(fileArg);

rootCommand.SetHandler((string file) =>
{
    var renderer = new MarkdownRenderer();
    var output = renderer.RenderFile(file);
    Console.Write(output);
}, fileArg);

await rootCommand.InvokeAsync(args);
```

## Architecture Comparison

### Glow (Go)
```
markdown → goldmark (parse) → glamour (render) → chroma (highlight) → ANSI output
```

### GlowSharp (C#)
```
markdown → Markdig (parse) → AnsiRenderer (render) → SyntaxHighlighter → ANSI output
```

## Key Classes

| Class | Purpose | Go Equivalent |
|-------|---------|---------------|
| `MarkdownRenderer` | Main API | `glamour.TermRenderer` |
| `AnsiRenderer` | AST → ANSI | Glamour's ANSI renderer |
| `StyleConfig` | Styling | Glamour style JSON |
| `MarkdownUtils` | Helpers | `utils/utils.go` |
| `SyntaxHighlighter` | Code highlighting | Chroma |

## Features Implemented

✅ Markdown parsing (via Markdig)
✅ Headings (H1-H6) with styling
✅ Bold, italic, strikethrough
✅ Code blocks with borders
✅ Inline code
✅ Lists (ordered and unordered)
✅ Block quotes
✅ Horizontal rules
✅ Links
✅ Frontmatter removal
✅ Auto code-file wrapping
✅ Terminal width detection
✅ Word wrapping
✅ Dark/light themes

## Limitations

This is a proof-of-concept with simplified implementations:

- ❌ No TUI mode (CLI only)
- ❌ Simplified syntax highlighting (uses borders instead of full highlighting)
- ❌ No GitHub/GitLab URL fetching
- ❌ No file browser
- ❌ Basic table support

## Extending GlowSharp

### Add Custom Styles

```csharp
var customStyle = new StyleConfig
{
    H1 = new HeadingStyle
    {
        Color = Color.Red,
        Bold = true,
        Prefix = "\n=== "
    },
    Code = new TextStyle
    {
        Color = Color.Yellow,
        BackgroundColor = Color.Grey19
    }
};

var renderer = new MarkdownRenderer(customStyle, width: 100);
```

### Improve Syntax Highlighting

The `SyntaxHighlighter` class can be extended to use a full-featured highlighter:

```csharp
// In SyntaxHighlighter.cs
private static string ApplyBasicHighlighting(string line, string language, bool isDark)
{
    // Add your custom highlighting logic here
    // Could integrate with ColorCode, Pygments.NET, or custom lexer
    return line;
}
```

## Testing

```bash
# Test with example markdown
dotnet run -- ../example.md

# Test with code file
dotnet run -- ../example.cs

# Test stdin
echo "# Test\n\n**Bold** text" | dotnet run -- -
```

## Tips

1. **Use in documentation tools**: Integrate into doc generators for beautiful terminal output
2. **CLI help text**: Render markdown help files instead of plain text
3. **Log viewers**: Pretty-print markdown logs
4. **REPL tools**: Render markdown responses in interactive C# tools

## Next Steps

To productionize:

1. Add proper syntax highlighting with ColorCode or similar
2. Implement table rendering
3. Add image placeholder support
4. Improve text wrapping algorithm
5. Add caching for repeated renders
6. Support custom style JSON files
7. Add async rendering for large files
8. Implement streaming for stdin

## Performance

Current implementation prioritizes clarity over performance. For large files:

- Use streaming instead of loading entire file
- Cache parsed AST
- Optimize string concatenation
- Consider parallel rendering for independent blocks
