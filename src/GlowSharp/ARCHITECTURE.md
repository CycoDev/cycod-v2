# GlowSharp Architecture

## Overview

**GlowSharp** is a C# port of [Glow](https://github.com/charmbracelet/glow), a terminal-based markdown renderer. It provides two distinct rendering modes:
1. **Standard Mode**: Full-featured markdown rendering using Markdig AST parsing
2. **Streaming Mode**: Progressive rendering for LLM responses and real-time streaming

## Technology Stack

### Platform
- **.NET 9.0** - Latest .NET runtime with C# latest language features
- **Cross-platform** - Runs on Windows, macOS, Linux

### Key Dependencies
- **Markdig 0.37.0** - Markdown parser (equivalent to Go's goldmark)
- **Spectre.Console 0.49.1** - Terminal UI framework (colors, styling)
- **System.CommandLine 2.0.0-beta4** - CLI argument parsing (equivalent to Go's Cobra)

## Project Structure

```
GlowSharp/
├── GlowSharp.csproj          # Project configuration
├── Program.cs                 # CLI entry point and orchestration
│
├── Standard Rendering Pipeline:
│   ├── MarkdownRenderer.cs    # High-level renderer facade
│   ├── AnsiRenderer.cs        # AST → ANSI terminal output
│   └── MarkdownUtils.cs       # Utility functions (frontmatter, file detection)
│
├── Streaming Rendering Pipeline:
│   └── StreamingMarkdownRenderer.cs  # Progressive chunk-based renderer
│
├── Shared Components:
│   ├── SyntaxHighlighter.cs   # Code syntax highlighting (16+ languages)
│   └── StyleConfig.cs         # Theme and style definitions
│
└── Test Files:
    ├── TEST.md                # Comprehensive markdown test suite
    ├── formatting-test.md     # Inline formatting tests
    ├── hr-test.md             # Horizontal rule tests
    └── STREAMING_FEATURES.md  # Feature documentation
```

## Architecture Diagrams

### High-Level Architecture

```
┌─────────────────────────────────────────────────────────────┐
│                         Program.cs                          │
│                    (CLI Entry Point)                        │
└─────────┬───────────────────────────────────┬───────────────┘
          │                                   │
          │                                   │
    ┌─────▼──────┐                      ┌─────▼──────┐
    │  Standard  │                      │  Streaming │
    │    Mode    │                      │    Mode    │
    └─────┬──────┘                      └─────┬──────┘
          │                                   │
          │                                   │
┌─────────▼──────────────┐         ┌──────────▼────────────────┐
│ MarkdownRenderer       │         │ StreamingMarkdown-        │
│                        │         │ Renderer                  │
│ 1. Remove frontmatter  │         │                           │
│ 2. Detect file type    │         │ State Machine:            │
│ 3. Wrap code if needed │         │ - None                    │
│ 4. Parse with Markdig  │         │ - InCodeBlock             │
│ 5. Render AST          │         │ - InTable                 │
└─────────┬──────────────┘         │ - InBlockBuffer           │
          │                        └──────────┬────────────────┘
          │                                   │
    ┌─────▼──────────┐                       │
    │  AnsiRenderer  │◄──────────────────────┘
    │                │         (for code blocks only)
    │  Walks AST:    │
    │  - Headings    │
    │  - Paragraphs  │         ┌────────────────────┐
    │  - Code Blocks ├────────►│ SyntaxHighlighter  │
    │  - Tables      │         │                    │
    │  - Lists       │         │ Regex-based        │
    │  - Quotes      │         │ highlighting for:  │
    └────────┬───────┘         │ - Keywords         │
             │                 │ - Strings          │
             │                 │ - Comments         │
             │                 │ - Numbers          │
             │                 │ - Functions        │
             │                 └────────────────────┘
             │
    ┌────────▼────────┐
    │  ANSI Terminal  │
    │  Output         │
    └─────────────────┘
```

### Execution Flow - Standard Mode

```
User Input (file.md)
    │
    ▼
Program.cs:Execute()
    │
    ├─► Select style (dark/light/auto)
    │
    ├─► Create MarkdownRenderer
    │       │
    │       ▼
    │   MarkdownRenderer.Render()
    │       │
    │       ├─► MarkdownUtils.RemoveFrontmatter()
    │       │       └─► Strip YAML --- delimiters
    │       │
    │       ├─► MarkdownUtils.IsMarkdownFile()
    │       │       └─► Check file extension
    │       │
    │       ├─► [If code file] MarkdownUtils.WrapCodeBlock()
    │       │       └─► Wrap in ```language\n...\n```
    │       │
    │       ├─► Markdig.Parse()
    │       │       └─► Build AST (Abstract Syntax Tree)
    │       │
    │       └─► AnsiRenderer.Render(AST)
    │               │
    │               ├─► Walk AST recursively
    │               │
    │               ├─► For each block:
    │               │   ├─ HeadingBlock → RenderHeading()
    │               │   ├─ ParagraphBlock → RenderParagraph()
    │               │   ├─ FencedCodeBlock → RenderFencedCodeBlock()
    │               │   │                      └─► SyntaxHighlighter.Highlight()
    │               │   ├─ QuoteBlock → RenderQuoteBlock()
    │               │   ├─ ListBlock → RenderListBlock()
    │               │   ├─ Table → RenderTable()
    │               │   └─ ThematicBreakBlock → RenderHorizontalRule()
    │               │
    │               └─► For inline elements:
    │                   ├─ LiteralInline → plain text
    │                   ├─ CodeInline → RenderCodeInline()
    │                   ├─ EmphasisInline → RenderEmphasis()
    │                   └─ LinkInline → RenderLink()
    │
    └─► Console.Write(output)
```

### Execution Flow - Streaming Mode

```
User Input (file.md + --stream flag)
    │
    ▼
Program.cs:ExecuteStreaming()
    │
    ├─► Read entire file into memory
    │
    ├─► Remove frontmatter
    │
    ├─► Create StreamingMarkdownRenderer
    │
    └─► Chunk Producer Loop:
            │
            ├─► Pick random chunk size (32-64 chars)
            │
            ├─► Extract chunk from markdown
            │
            ├─► renderer.AppendChunk(chunk)
            │       │
            │       ▼
            │   StreamingMarkdownRenderer.ProcessBuffer()
            │       │
            │       └─► State Machine:
            │           │
            │           ├─► ParserState.None:
            │           │   ├─ Detect ``` → switch to InCodeBlock
            │           │   ├─ Detect | table → switch to InTable
            │           │   ├─ Detect --- hr → RenderHorizontalRule()
            │           │   ├─ Detect # heading → wait for \n, then FormatInlineText()
            │           │   └─ Normal text → wait for \n, then OutputText()
            │           │
            │           ├─► ParserState.InCodeBlock:
            │           │   ├─ Buffer code until closing ```
            │           │   ├─ Keep last 2 chars (handle split ```)
            │           │   └─ When found → RenderCodeBlock()
            │           │                    └─► SyntaxHighlighter.Highlight()
            │           │
            │           ├─► ParserState.InTable:
            │           │   ├─ Buffer rows until blank line
            │           │   └─ When done → RenderTable()
            │           │
            │           └─► ParserState.InBlockBuffer:
            │               └─ Generic buffering (future use)
            │
            ├─► Random delay (15-50ms)
            │
            └─► Loop until entire file processed
                    │
                    └─► renderer.Flush()
                            └─► Render any incomplete buffered content
```

## Core Components

### 1. Program.cs
**Role**: CLI orchestration and entry point

**Responsibilities**:
- Parse command-line arguments using System.CommandLine
- Select rendering mode (standard vs streaming)
- Handle input sources (file, stdin)
- Configure styles based on `--style` flag

**Key Methods**:
- `Main()`: Entry point, sets up CLI parser
- `Execute()`: Standard rendering flow
- `ExecuteStreaming()`: Streaming rendering flow with chunk simulation

**Command-line Interface**:
```bash
glowsharp [file]               # Render file in standard mode
glowsharp --stream [file]      # Render file with streaming simulation
glowsharp --style dark [file]  # Use dark theme
glowsharp --width 100 [file]   # Set max width to 100 chars
echo "# Hi" | glowsharp -      # Read from stdin
```

### 2. MarkdownRenderer.cs
**Role**: High-level facade for standard markdown rendering

**Responsibilities**:
- Orchestrate the full rendering pipeline
- Remove frontmatter before parsing
- Detect code vs markdown files
- Build Markdig parsing pipeline with extensions
- Delegate AST rendering to AnsiRenderer

**Pipeline Extensions**:
- `UseAdvancedExtensions()`: Tables, task lists, etc.
- `UseEmojiAndSmiley()`: GitHub-style emoji (:smile:)
- `UsePipeTables()` / `UseGridTables()`: Table support
- `UseListExtras()`: Advanced list features
- `UseEmphasisExtras()`: Strikethrough, subscript, superscript
- `UseAutoLinks()`: Convert URLs to links

**Design Pattern**: Builder pattern for configuration
```csharp
var renderer = new MarkdownRenderer.Builder()
    .WithDarkStyle()
    .WithWidth(100)
    .Build();
```

### 3. AnsiRenderer.cs
**Role**: AST → ANSI terminal output transformer

**Responsibilities**:
- Walk Markdig AST (Abstract Syntax Tree)
- Convert each block/inline element to ANSI-styled text
- Apply colors, bold, italic, underline via ANSI escape codes
- Handle text wrapping based on terminal width
- Render tables with Unicode box-drawing characters

**Key Rendering Methods**:
- `RenderHeading()`: Color-coded headings (H1-H6)
- `RenderFencedCodeBlock()`: Syntax-highlighted code
- `RenderTable()`: Box-drawing table with column width calculation
- `RenderQuoteBlock()`: Indented gray quotes with │ prefix
- `RenderListBlock()`: Bulleted/numbered lists with nesting
- `RenderHorizontalRule()`: Gray ─── line

**ANSI Escape Code Examples**:
```
\u001b[35;1m  → Magenta + Bold (H1)
\u001b[1m     → Bold (strong)
\u001b[3m     → Italic (emphasis)
\u001b[9m     → Strikethrough
\u001b[0m     → Reset all styles
```

**Table Rendering**:
```
  Column 1 │ Column 2 │ Column 3
  ─────────┼──────────┼─────────
  Data 1   │ Data 2   │ Data 3
```

### 4. StreamingMarkdownRenderer.cs
**Role**: Progressive chunk-based markdown renderer for LLM streaming

**Responsibilities**:
- Process markdown chunks as they arrive (32-64 char chunks)
- Maintain parser state across chunks
- Buffer complex structures (code blocks, tables) until complete
- Render simple text immediately (typewriter effect)
- Handle multi-character tokens split across chunk boundaries

**State Machine**:
```csharp
private enum ParserState
{
    None,           // Processing normal text
    InCodeBlock,    // Buffering code until closing ```
    InTable,        // Buffering table rows
    InBlockBuffer   // Generic buffering (future)
}
```

**Critical Design Decision - Chunk Boundary Handling**:

Problem: Multi-character tokens like ``` can be split across chunks:
- Chunk 1: "...code\n``"
- Chunk 2: "`\n"

Solution: Keep last 2 characters in buffer when buffering code blocks
```csharp
// In ProcessCodeBlock()
if (text.Length > 2)
{
    var toBuffer = text.Substring(0, text.Length - 2);
    _blockBuffer.Append(toBuffer);
    _buffer.Remove(0, toBuffer.Length);
}
// else: Wait for more data
```

**Supported Features**:
- ✅ Headings (all 6 levels)
- ✅ Code blocks with syntax highlighting
- ✅ Tables
- ✅ Horizontal rules (---, ***, ___)
- ✅ Blockquotes
- ✅ Bold, italic, strikethrough, inline code
- ⚠️ Task lists (shows [x] literally, not ✓)
- ⚠️ Links (shows text with URL, not styled)

See STREAMING_FEATURES.md for complete feature matrix.

### 5. SyntaxHighlighter.cs
**Role**: Regex-based syntax highlighting for 16+ programming languages

**Responsibilities**:
- Detect programming language from code fence identifier
- Apply color-coded ANSI styles to keywords, strings, comments, numbers
- Support language-specific features (PHP $vars, C++ hex, CSS units)

**Supported Languages**:
1. Python
2. JavaScript
3. TypeScript
4. C#
5. Go
6. Rust
7. Java
8. C++
9. PHP
10. Swift
11. HTML
12. XML
13. CSS
14. JSON
15. SQL
16. Shell (bash/sh)

**Architecture**:
```csharp
public static string Highlight(string code, string language, string theme)
{
    var lang = NormalizeLanguage(language);
    var colors = GetColorScheme(theme); // monokai, vs, etc.

    return lang switch
    {
        "python" => HighlightPython(code, colors),
        "javascript" => HighlightJavaScript(code, colors),
        // ... 14 more languages
        _ => code  // No highlighting for unknown languages
    };
}
```

**Highlighting Strategy** (per language):
1. Extract comments → color gray, preserve
2. Extract strings → color yellow/green, preserve
3. Highlight keywords → color magenta/blue
4. Highlight numbers → color cyan
5. Highlight functions → color yellow
6. Reassemble with original structure

**Example - Python Keyword Highlighting**:
```csharp
var keywords = new[] { "def", "class", "if", "for", "while", ... };
foreach (var keyword in keywords)
{
    result = Regex.Replace(result,
        $@"\b{keyword}\b",
        $"{colors.Keyword}{keyword}{colors.Reset}");
}
```

### 6. StyleConfig.cs
**Role**: Theme and style definitions using Spectre.Console colors

**Responsibilities**:
- Define text styles for all markdown elements
- Provide pre-configured themes (Dark, Light, Auto)
- Convert Spectre.Console colors to ANSI codes

**Style Classes**:
- `TextStyle`: Color, background, bold, italic, underline, strikethrough
- `HeadingStyle`: Extends TextStyle with prefix/suffix
- `CodeBlockStyle`: Theme name, line numbers, margins
- `BlockQuoteStyle`: Extends TextStyle with indentation
- `ListStyle`: Bullet character, indentation

**Predefined Themes**:

**DarkStyle**:
```csharp
H1 = Fuchsia + Bold
H2 = Blue1 + Bold
H3 = Cyan1 + Bold
Code = Yellow on Grey19
CodeBlock = "monokai" theme
Link = Cyan1 + Underline
```

**LightStyle**:
```csharp
H1 = Blue + Bold
H2 = DarkBlue + Bold
H3 = DarkCyan + Bold
Code = DarkRed on Grey93
CodeBlock = "vs" theme
Link = Blue + Underline
```

**AutoStyle**: Detects terminal color support and picks Dark/Light automatically.

### 7. MarkdownUtils.cs
**Role**: Utility functions for markdown preprocessing

**Responsibilities**:
- Remove YAML frontmatter (--- delimiters)
- Detect markdown vs code files by extension
- Wrap code files in markdown code blocks
- Expand paths with ~ and environment variables

**Key Functions**:
```csharp
RemoveFrontmatter(content)       // Strip YAML between ---
IsMarkdownFile(filename)         // Check .md/.markdown/.mdown extensions
WrapCodeBlock(content, ext)      // Wrap in ```language\n...\n```
ExpandPath(path)                 // Expand ~ and $VARS
```

**Frontmatter Detection**:
Removes content between two `---` markers:
```markdown
---
title: My Document
author: Alice
---

# Actual Content
```
→ Output starts at "# Actual Content"

## Data Flow

### Standard Mode - File Rendering

```
test.md
    │
    ▼
File.ReadAllText()
    │
    ▼
"---\ntitle: Test\n---\n# Hello\n**World**"
    │
    ▼
RemoveFrontmatter()
    │
    ▼
"# Hello\n**World**"
    │
    ▼
Markdig.Parse()
    │
    ▼
MarkdownDocument (AST)
├─ HeadingBlock (level=1)
│  └─ Inline: "Hello"
└─ ParagraphBlock
   └─ EmphasisInline (bold)
      └─ "World"
    │
    ▼
AnsiRenderer.Render()
    │
    ▼
"\u001b[35;1mHello\u001b[0m\n\n\u001b[1mWorld\u001b[0m\n\n"
    │
    ▼
Console.Write()
    │
    ▼
Terminal displays:
    Hello    (magenta, bold)

    World    (bold)
```

### Streaming Mode - Progressive Rendering

```
test.md content: "# Hello\n```python\ndef foo():\n    pass\n```"
    │
    ▼
Chunk Producer (32-64 char chunks)
    │
    ├─ Chunk 1: "# Hello\n```py"
    ├─ Chunk 2: "thon\ndef foo("
    ├─ Chunk 3: "):\n    pass\n``"
    └─ Chunk 4: "`\n"
    │
    ▼
StreamingMarkdownRenderer.AppendChunk()
    │
    ▼
ProcessBuffer() - State Machine
    │
    ├─ Chunk 1: "# Hello\n```py"
    │  ├─ Detect heading → output "\u001b[35;1mHello\u001b[0m\n"
    │  ├─ Detect ``` → switch to InCodeBlock state
    │  └─ Buffer "py" (language identifier incomplete)
    │
    ├─ Chunk 2: "thon\ndef foo("
    │  ├─ Complete language: "python"
    │  └─ Buffer code: "def foo("
    │
    ├─ Chunk 3: "):\n    pass\n``"
    │  ├─ Keep buffering code
    │  └─ Keep last 2 chars "``" (might be start of ```)
    │
    └─ Chunk 4: "`\n"
       ├─ Complete closing ```
       ├─ Highlight buffered code: "def foo():\n    pass"
       ├─ Output highlighted code
       └─ Switch to None state
```

## Design Patterns

### 1. Facade Pattern
**MarkdownRenderer** provides a simple interface hiding complex pipeline:
```csharp
var output = new MarkdownRenderer(style, width).Render(content);
// Hides: frontmatter removal, AST parsing, ANSI rendering
```

### 2. Builder Pattern
**MarkdownRenderer.Builder** for fluent configuration:
```csharp
var renderer = new MarkdownRenderer.Builder()
    .WithDarkStyle()
    .WithWidth(100)
    .Build();
```

### 3. State Machine Pattern
**StreamingMarkdownRenderer** uses explicit state enum:
```csharp
private ParserState _state = ParserState.None;

switch (_state)
{
    case ParserState.None: ProcessNormalText(); break;
    case ParserState.InCodeBlock: ProcessCodeBlock(); break;
    case ParserState.InTable: ProcessTable(); break;
}
```

### 4. Strategy Pattern
**SyntaxHighlighter** selects highlighting strategy by language:
```csharp
return language switch
{
    "python" => HighlightPython(code, colors),
    "javascript" => HighlightJavaScript(code, colors),
    // Different strategy for each language
};
```

### 5. Visitor Pattern
**AnsiRenderer** walks AST and visits each node type:
```csharp
foreach (var block in document)
{
    switch (block)
    {
        case HeadingBlock h: RenderHeading(h); break;
        case ParagraphBlock p: RenderParagraph(p); break;
        case FencedCodeBlock c: RenderCodeBlock(c); break;
    }
}
```

## Performance Characteristics

### Standard Mode
- **Parse Phase**: O(n) - Markdig parses entire document
- **Render Phase**: O(n) - Single pass AST walk
- **Memory**: O(n) - Stores full AST in memory
- **Latency**: Entire document must be parsed before first output

### Streaming Mode
- **Parse Phase**: O(1) per chunk - Incremental state machine
- **Render Phase**: O(1) per chunk - Immediate output when possible
- **Memory**: O(1) for simple text, O(k) for buffered blocks (k = block size)
- **Latency**: Sub-100ms from chunk arrival to output (except buffered blocks)

**Chunk Size Impact**:
- **Smaller chunks (8-16 chars)**: More responsive, higher CPU overhead
- **Larger chunks (128-256 chars)**: Less responsive, lower CPU overhead
- **Sweet spot (32-64 chars)**: Balance between responsiveness and efficiency

## Limitations and Trade-offs

### Standard Mode (MarkdownRenderer)
**Pros**:
- ✅ Full Markdown spec support (via Markdig extensions)
- ✅ Accurate AST-based parsing
- ✅ Task lists, footnotes, definition lists
- ✅ Complex nested structures

**Cons**:
- ❌ Must wait for complete document before rendering
- ❌ Not suitable for streaming LLM responses
- ❌ Higher memory usage (full AST)

### Streaming Mode (StreamingMarkdownRenderer)
**Pros**:
- ✅ Progressive rendering (typewriter effect)
- ✅ Perfect for LLM streaming
- ✅ Low latency for simple text
- ✅ Handles chunk boundary issues correctly

**Cons**:
- ❌ Only core markdown features supported
- ❌ Task lists show [x] literally, not ✓
- ❌ Nested blockquotes not indented properly
- ❌ Links not specially styled
- ❌ Images not indicated
- ❌ Must buffer code blocks and tables (delays rendering)

## Extension Points

### Adding a New Language to SyntaxHighlighter

1. Add language name mapping in `NormalizeLanguage()`:
```csharp
"ruby" or "rb" => "ruby",
```

2. Implement `HighlightRuby()` method:
```csharp
private static string HighlightRuby(string code, ColorScheme colors)
{
    var result = code;

    // 1. Extract comments
    var comments = new Dictionary<string, string>();
    result = Regex.Replace(result, @"#.*$",
        m => { /* store and replace */ });

    // 2. Highlight keywords
    var keywords = new[] { "def", "class", "module", "if", "end" };
    foreach (var kw in keywords)
        result = Regex.Replace(result, $@"\b{kw}\b",
            $"{colors.Keyword}{kw}{colors.Reset}");

    // 3. Restore comments with color
    // 4. Return result
}
```

3. Add case in `Highlight()` switch:
```csharp
"ruby" => HighlightRuby(code, colors),
```

### Adding a New Markdown Feature to StreamingMarkdownRenderer

Example: Add support for task list checkboxes

1. Detect pattern in `FormatInlineText()`:
```csharp
// Handle task lists
text = Regex.Replace(text, @"\[x\]", "✓");
text = Regex.Replace(text, @"\[ \]", "☐");
```

2. Optional: Add color
```csharp
text = Regex.Replace(text, @"\[x\]",
    $"\u001b[32m✓\u001b[0m");  // Green check
text = Regex.Replace(text, @"\[ \]",
    $"\u001b[90m☐\u001b[0m");  // Gray box
```

### Adding a New Style Theme

1. Define in `StyleConfig.cs`:
```csharp
public static StyleConfig GruvboxStyle => new()
{
    H1 = new() { Color = Color.Yellow, Bold = true },
    H2 = new() { Color = Color.Orange1, Bold = true },
    Code = new() { Color = Color.Green, BackgroundColor = Color.Grey23 },
    CodeBlock = new() { Theme = "gruvbox", Margin = 1 },
    // ... more styles
};
```

2. Add CLI option in `Program.cs`:
```csharp
"gruvbox" => StyleConfig.GruvboxStyle,
```

## Comparison to Original Glow

| Feature | Go Glow | C# GlowSharp |
|---------|---------|--------------|
| **Language** | Go | C# (.NET 9.0) |
| **Markdown Parser** | goldmark | Markdig |
| **Terminal Rendering** | glamour | Custom AnsiRenderer |
| **CLI Framework** | Cobra | System.CommandLine |
| **TUI Mode** | Bubble Tea | ❌ Not implemented |
| **Pager Mode** | ✅ Built-in | ❌ Not implemented |
| **File Browser** | ✅ Built-in | ❌ Not implemented |
| **URL Fetching** | ✅ GitHub/GitLab | ❌ Not implemented |
| **Standard Rendering** | ✅ | ✅ Full parity |
| **Streaming Rendering** | ❌ Not available | ✅ **Unique feature!** |
| **Syntax Highlighting** | chroma (1000+ langs) | Custom (16 langs) |
| **Configuration** | Viper + YAML | ❌ Not implemented |

**GlowSharp's Unique Value**:
- **Streaming Mode**: Progressive rendering for LLM responses (not in original Glow)
- **.NET Ecosystem**: Integrates with .NET apps, libraries, and tools
- **Simplified Scope**: CLI-only, no TUI complexity

## Future Enhancements

### High Priority
1. **Task list checkboxes** - Replace [x] with ✓
2. **Link styling** - Underline and color links
3. **Nested blockquote indentation** - Proper >> handling
4. **More syntax highlighting languages** - Ruby, Kotlin, Scala, Haskell

### Medium Priority
5. **Image indicators** - Show ![alt] as [Image: alt]
6. **HTML stripping** - Remove <tags> from output
7. **Configuration file** - YAML config like original Glow
8. **URL fetching** - Render from URLs, GitHub repos

### Low Priority
9. **TUI Mode** - Interactive file browser (using Spectre.Console)
10. **Pager integration** - Pipe to less/more
11. **Word wrapping improvements** - Better handling of long words
12. **Custom color schemes** - User-defined themes

## Testing Strategy

### Current Test Files
- **TEST.md**: Comprehensive markdown feature showcase
  - 13 programming language examples (Fibonacci implementations)
  - All markdown elements (headings, lists, tables, quotes, code)
  - Used for manual visual testing

- **formatting-test.md**: Inline formatting edge cases
  - Bold, italic, strikethrough combinations
  - Task lists
  - Nested blockquotes

- **hr-test.md**: Horizontal rule variants
  - Dashes, asterisks, underscores
  - With/without spaces

### Testing Workflow
```bash
# Standard mode
dotnet run -- ../TEST.md

# Streaming mode (simulates LLM)
dotnet run -- --stream ../TEST.md

# Specific feature test
dotnet run -- --stream ../formatting-test.md
```

### Recommended Unit Tests (Not yet implemented)
- `MarkdownUtils.RemoveFrontmatter()` - Various YAML patterns
- `SyntaxHighlighter.Highlight()` - Each language independently
- `StyleConfig` color conversion - ANSI code correctness
- `StreamingMarkdownRenderer` chunk boundary handling - Split ``` tokens

## Summary

GlowSharp is a **dual-mode markdown renderer**:
1. **Standard Mode**: Full-featured, AST-based rendering using Markdig
2. **Streaming Mode**: Progressive, chunk-based rendering for LLM responses

**Key Innovations**:
- State machine for handling chunk boundaries in streaming mode
- Regex-based syntax highlighting for 16+ languages
- Direct ANSI terminal output (no intermediate format)

**Best Use Cases**:
- **Standard Mode**: Rendering local markdown files, READMEs, documentation
- **Streaming Mode**: Displaying LLM responses in real-time with proper formatting

**Architecture Strengths**:
- Clear separation of concerns (parsing, rendering, styling)
- Extensible design (easy to add languages, themes, features)
- Low-level control over terminal output (ANSI escape codes)

**Architecture Weaknesses**:
- Streaming mode has limited feature set vs standard mode
- No configuration file support (all CLI flags)
- Manual regex-based highlighting (vs library with 1000+ languages)
