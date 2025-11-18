# Syntax Highlighting in GlowSharp

GlowSharp now includes **full color syntax highlighting** for code blocks, just like Glow!

## Supported Languages

✅ **C# / .NET**
- Keywords: `public`, `class`, `void`, `var`, `if`, `return`, etc.
- Strings: Double and single quoted
- Comments: `//` style
- Numbers: Integers and decimals
- Functions: Method names

✅ **JavaScript / TypeScript**
- Keywords: `function`, `const`, `let`, `var`, `if`, `return`, etc.
- Strings: Double, single, and template literals (backticks)
- Comments: `//` style
- Numbers: All numeric types
- Functions: Function names

✅ **Python**
- Keywords: `def`, `class`, `if`, `for`, `return`, `import`, etc.
- Strings: Single, double, and triple-quoted
- Comments: `#` style
- Numbers: All numeric types
- Functions: Function and method names

✅ **Go**
- Keywords: `package`, `func`, `var`, `if`, `return`, etc.
- Strings: Double-quoted and backtick strings
- Comments: `//` style
- Numbers: All numeric types
- Functions: Function names

✅ **Rust**
- Keywords: `fn`, `let`, `mut`, `if`, `return`, etc.
- Strings: Double-quoted
- Comments: `//` style
- Numbers: All numeric types

✅ **Shell / Bash**
- Keywords: `if`, `then`, `for`, `while`, etc.
- Strings: Single and double-quoted
- Comments: `#` style

✅ **JSON**
- Strings: All JSON strings
- Numbers: All numeric values
- Keywords: `true`, `false`, `null`

✅ **SQL**
- Keywords: `SELECT`, `FROM`, `WHERE`, `INSERT`, etc. (case-insensitive)
- Strings: Single-quoted
- Comments: `--` style
- Numbers: All numeric types

## Color Scheme

### Dark Theme (Default)
- **Keywords**: Magenta (`\u001b[35m`)
- **Strings**: Yellow (`\u001b[33m`)
- **Comments**: Green (`\u001b[32m`)
- **Numbers**: Cyan (`\u001b[36m`)
- **Functions**: Blue (`\u001b[34m`)

### Light Theme
- **Keywords**: Blue (`\u001b[34m`)
- **Strings**: Red (`\u001b[31m`)
- **Comments**: Green (`\u001b[32m`)
- **Numbers**: Blue (`\u001b[34m`)
- **Functions**: Magenta (`\u001b[35m`)

## Usage

### In Markdown Files

Simply specify the language in your code fence:

~~~markdown
```csharp
public class HelloWorld
{
    public static void Main(string[] args)
    {
        Console.WriteLine("Hello, World!");
    }
}
```
~~~

### From Command Line

```bash
# Auto-detect dark mode
dotnet run -- example.md

# Force light theme
dotnet run -- --style light example.md

# Force dark theme
dotnet run -- --style dark example.md
```

### In Code

```csharp
using GlowSharp;

var renderer = new MarkdownRenderer.Builder()
    .WithDarkStyle()
    .Build();

var output = renderer.RenderFile("code-sample.md");
Console.Write(output); // Will show colors in terminal
```

## How It Works

1. **Language Detection**: Language is determined from the code fence info string (e.g., ` ```csharp`)
2. **Lexical Analysis**: Regex patterns identify keywords, strings, comments, numbers, and functions
3. **ANSI Colorization**: Each token type is wrapped with ANSI escape codes
4. **Terminal Rendering**: The terminal interprets ANSI codes and displays colors

## Comparison with Glow

GlowSharp's syntax highlighting is similar to Glow's Chroma-based highlighting:

| Feature | Glow (Chroma) | GlowSharp |
|---------|---------------|-----------|
| Keyword highlighting | ✅ | ✅ |
| String highlighting | ✅ | ✅ |
| Comment highlighting | ✅ | ✅ |
| Number highlighting | ✅ | ✅ |
| Function highlighting | ✅ | ✅ |
| Color themes | ✅ | ✅ (Dark/Light) |
| Language support | 200+ | 8 core languages |

## Example Output

When rendered in a terminal, code like this:

```csharp
public class Example
{
    // This is a comment
    private string message = "Hello, World!";
    private int count = 42;
}
```

Appears with:
- `public`, `class`, `private`, `string`, `int` in **magenta**
- `"Hello, World!"` in **yellow**
- `// This is a comment` in **green**
- `42` in **cyan**

## Extending

To add support for more languages, edit `SyntaxHighlighter.cs`:

```csharp
private static string HighlightCode(string code, string language, bool isDark)
{
    // ...
    var highlightedLine = language switch
    {
        "csharp" => HighlightCSharp(line, colors),
        "javascript" or "typescript" => HighlightJavaScript(line, colors),
        // Add your new language here:
        "mylang" => HighlightMyLang(line, colors),
        _ => EscapeMarkup(line)
    };
    // ...
}
```

Then implement `HighlightMyLang` following the pattern of existing highlighters.

## Testing

Test your code highlighting:

```bash
# Create a markdown file with code
cat > test.md << 'EOF'
# Code Sample

```csharp
using System;
Console.WriteLine("Hello!");
```
EOF

# Render it
dotnet run -- test.md
```

## Notes

- Colors are rendered using ANSI escape codes
- Works in any terminal that supports ANSI colors (most modern terminals)
- Colors may appear differently depending on your terminal's color scheme
- When output is redirected (piped), ANSI codes are still present but not rendered as colors

## See It in Action

Run the included example:

```bash
cd GlowSharp/GlowSharp
dotnet run -- ../test-colors.md
```

This will show C#, JavaScript, and Python code with full syntax highlighting! 🎨
