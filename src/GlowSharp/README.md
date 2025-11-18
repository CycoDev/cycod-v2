# GlowSharp

A C# port of [Glow](https://github.com/charmbracelet/glow) - render markdown on the CLI, with pizzazz!

## About

GlowSharp is a terminal-based markdown renderer for .NET, ported from the original Glow (written in Go). It focuses on code rendering with syntax highlighting, making it perfect for rendering markdown documentation with code examples in your CLI applications.

## Features

- ✨ Beautiful markdown rendering in the terminal
- 🎨 Syntax highlighting for code blocks (C#, JavaScript, Python, and more)
- 🌓 Auto-detect terminal theme (dark/light mode)
- 📝 YAML frontmatter removal
- 🎯 Code file rendering (wrap non-markdown files in code blocks)
- 📏 Configurable word wrapping
- 🎭 Multiple built-in styles

## Installation

### Build from Source

```bash
cd GlowSharp
dotnet build -c Release
```

### Run

```bash
cd GlowSharp
dotnet run -- <file.md>
```

## Usage

### Basic Usage

```bash
# Render a markdown file
glowsharp README.md

# Read from stdin
echo "# Hello World" | glowsharp -

# Render a code file (auto-wraps in code block)
glowsharp Program.cs
```

### Options

```bash
# Use dark theme
glowsharp --style dark README.md

# Use light theme
glowsharp --style light README.md

# Auto-detect theme (default)
glowsharp --style auto README.md

# Set custom width
glowsharp --width 100 README.md

# Disable word wrapping
glowsharp --width 0 README.md
```

### Examples

```bash
# Render this README
dotnet run -- README.md

# Pipe markdown
echo "**Bold** and *italic*" | dotnet run -- -

# Render C# code with syntax highlighting
dotnet run -- Program.cs
```

## Architecture

GlowSharp is architected similarly to the original Glow:

### Components

1. **MarkdownRenderer** - Main rendering orchestrator (similar to Glow's `executeCLI`)
2. **AnsiRenderer** - AST walker and ANSI output generator (similar to Glamour's ANSI renderer)
3. **SyntaxHighlighter** - Code syntax highlighting (similar to Chroma in Glamour)
4. **StyleConfig** - Style configuration system (similar to Glamour's style JSON files)
5. **MarkdownUtils** - Utility functions (ported from Glow's utils package)

### Rendering Pipeline

```
Markdown Input
    ↓
Remove Frontmatter (MarkdownUtils)
    ↓
Detect if Code File → Wrap in Code Block
    ↓
Parse with Markdig (like Goldmark in Go)
    ↓
Walk AST and Render to ANSI (AnsiRenderer)
    ↓
Apply Syntax Highlighting (SyntaxHighlighter)
    ↓
ANSI Terminal Output
```

## Dependencies

- **Markdig** - CommonMark-compliant markdown parser (equivalent to Goldmark)
- **Spectre.Console** - Terminal capabilities and ANSI styling
- **ColorCode.Core** - Syntax highlighting (equivalent to Chroma)
- **System.CommandLine** - CLI framework

## Code Mapping to Original Glow

| GlowSharp File | Original Glow File | Description |
|----------------|-------------------|-------------|
| `MarkdownRenderer.cs` | `main.go:273-312` | Main rendering logic |
| `MarkdownUtils.cs` | `utils/utils.go` | Utility functions |
| `AnsiRenderer.cs` | Glamour's ANSI renderer | AST → ANSI conversion |
| `StyleConfig.cs` | Glamour's style system | Style configuration |
| `SyntaxHighlighter.cs` | Chroma (via Glamour) | Code highlighting |
| `Program.cs` | `main.go` | CLI entry point |

## What's Different from Glow?

**Not Included:**
- TUI (Text User Interface) mode - GlowSharp is CLI-only
- File discovery/browser - Focused on single-file rendering
- GitHub/GitLab URL fetching - Local files only
- Pager integration - Direct stdout only

**Focus:**
- ✅ Code rendering with excellent syntax highlighting
- ✅ Embeddable in .NET CLI applications
- ✅ Lightweight and fast
- ✅ Easy to integrate into existing .NET projects

## Usage as a Library

```csharp
using GlowSharp;

// Simple usage
var renderer = new MarkdownRenderer();
var output = renderer.Render("# Hello **World**");
Console.Write(output);

// With configuration
var renderer = new MarkdownRenderer.Builder()
    .WithDarkStyle()
    .WithWidth(100)
    .Build();

var output = renderer.RenderFile("README.md");
Console.Write(output);

// Render code files
var codeRenderer = new MarkdownRenderer();
var highlighted = codeRenderer.RenderFile("MyClass.cs");
Console.Write(highlighted);
```

## Contributing

This is a proof-of-concept port. Contributions welcome!

## License

MIT License (same as original Glow)

## Credits

- Original [Glow](https://github.com/charmbracelet/glow) by Charm
- [Glamour](https://github.com/charmbracelet/glamour) markdown renderer
- [Markdig](https://github.com/xoofx/markdig) for C# markdown parsing
- [Spectre.Console](https://spectreconsole.net/) for terminal capabilities

---

Part of the Charm ecosystem, ported to C#! 💜
