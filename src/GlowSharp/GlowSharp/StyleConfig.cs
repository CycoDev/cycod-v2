using Spectre.Console;

namespace GlowSharp;

/// <summary>
/// Style configuration for markdown rendering.
/// Inspired by Glamour's style system.
/// </summary>
public class StyleConfig
{
    public HeadingStyle H1 { get; set; } = new() { Color = Color.Fuchsia, Bold = true, Prefix = "\n" };
    public HeadingStyle H2 { get; set; } = new() { Color = Color.Blue, Bold = true, Prefix = "\n" };
    public HeadingStyle H3 { get; set; } = new() { Color = Color.Cyan1, Bold = true };
    public HeadingStyle H4 { get; set; } = new() { Color = Color.Green, Bold = true };
    public HeadingStyle H5 { get; set; } = new() { Color = Color.Yellow, Italic = true };
    public HeadingStyle H6 { get; set; } = new() { Color = Color.Grey, Italic = true };

    public TextStyle Code { get; set; } = new()
    {
        Color = Color.Yellow,
        BackgroundColor = Color.Grey19,
        Prefix = "`",
        Suffix = "`"
    };

    public CodeBlockStyle CodeBlock { get; set; } = new()
    {
        Theme = "monokai",
        ShowLineNumbers = false,
        Margin = 1
    };

    public TextStyle Link { get; set; } = new()
    {
        Color = Color.Blue,
        Underline = true
    };

    public TextStyle Strong { get; set; } = new() { Bold = true };
    public TextStyle Emphasis { get; set; } = new() { Italic = true };
    public TextStyle Strikethrough { get; set; } = new() { Strikethrough = true };

    public BlockQuoteStyle BlockQuote { get; set; } = new()
    {
        Color = Color.Grey,
        Italic = true,
        Prefix = "│ ",
        Indent = 2
    };

    public ListStyle List { get; set; } = new()
    {
        BulletChar = "•",
        Indent = 2
    };

    public static StyleConfig DarkStyle => new()
    {
        H1 = new() { Color = Color.Fuchsia, Bold = true, Prefix = "\n" },
        H2 = new() { Color = Color.Blue1, Bold = true, Prefix = "\n" },
        H3 = new() { Color = Color.Cyan1, Bold = true },
        Code = new() { Color = Color.Yellow, BackgroundColor = Color.Grey19 },
        CodeBlock = new() { Theme = "monokai", Margin = 1 },
        Link = new() { Color = Color.Cyan1, Underline = true }
    };

    public static StyleConfig LightStyle => new()
    {
        H1 = new() { Color = Color.Blue, Bold = true, Prefix = "\n" },
        H2 = new() { Color = Color.DarkBlue, Bold = true, Prefix = "\n" },
        H3 = new() { Color = Color.DarkCyan, Bold = true },
        Code = new() { Color = Color.DarkRed, BackgroundColor = Color.Grey93 },
        CodeBlock = new() { Theme = "vs", Margin = 1 },
        Link = new() { Color = Color.Blue, Underline = true }
    };

    public static StyleConfig AutoStyle
    {
        get
        {
            // Try to detect dark mode
            var isDark = AnsiConsole.Profile.Capabilities.ColorSystem != ColorSystem.NoColors;
            return isDark ? DarkStyle : LightStyle;
        }
    }
}

public class HeadingStyle : TextStyle
{
    public new string Prefix { get; set; } = "";
    public new string Suffix { get; set; } = "";
}

public class TextStyle
{
    public Color? Color { get; set; }
    public Color? BackgroundColor { get; set; }
    public bool Bold { get; set; }
    public bool Italic { get; set; }
    public bool Underline { get; set; }
    public bool Strikethrough { get; set; }
    public string Prefix { get; set; } = "";
    public string Suffix { get; set; } = "";

    public Style ToSpectreStyle()
    {
        var style = new Style(foreground: Color, background: BackgroundColor);

        if (Bold) style = style.Decoration(Decoration.Bold);
        if (Italic) style = style.Decoration(Decoration.Italic);
        if (Underline) style = style.Decoration(Decoration.Underline);
        if (Strikethrough) style = style.Decoration(Decoration.Strikethrough);

        return style;
    }
}

public class CodeBlockStyle
{
    public string Theme { get; set; } = "monokai";
    public bool ShowLineNumbers { get; set; } = false;
    public int Margin { get; set; } = 1;
}

public class BlockQuoteStyle : TextStyle
{
    public int Indent { get; set; } = 2;
}

public class ListStyle
{
    public string BulletChar { get; set; } = "•";
    public int Indent { get; set; } = 2;
}
