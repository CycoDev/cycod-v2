using System.Text;
using System.Text.RegularExpressions;
using Spectre.Console;

namespace GlowSharp;

/// <summary>
/// Syntax highlighter for code blocks with proper color highlighting.
/// Similar to what Chroma does in Glamour.
/// </summary>
public static class SyntaxHighlighter
{
    public static string Highlight(string code, string language, string theme)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            return string.Empty;
        }

        var isDark = !theme.Equals("vs", StringComparison.OrdinalIgnoreCase);
        var normalizedLang = NormalizeLanguage(language);

        return HighlightCode(code, normalizedLang, isDark);
    }

    private static string NormalizeLanguage(string language)
    {
        if (string.IsNullOrWhiteSpace(language))
            return "";

        return language.ToLowerInvariant().Trim() switch
        {
            "cs" or "csharp" => "csharp",
            "js" or "javascript" => "javascript",
            "ts" or "typescript" => "typescript",
            "py" or "python" => "python",
            "rb" or "ruby" => "ruby",
            "go" or "golang" => "go",
            "rs" or "rust" => "rust",
            "sh" or "bash" or "shell" => "shell",
            "json" => "json",
            "xml" => "xml",
            "html" => "html",
            "css" => "css",
            "sql" => "sql",
            "yaml" or "yml" => "yaml",
            "java" => "java",
            "c++" or "cpp" or "cplusplus" => "cpp",
            "php" => "php",
            "swift" => "swift",
            _ => language.ToLowerInvariant()
        };
    }

    private static string HighlightCode(string code, string language, bool isDark)
    {
        var lines = code.Split('\n');
        var output = new StringBuilder();

        // Color scheme
        var colors = GetColorScheme(isDark);

        foreach (var line in lines)
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                output.AppendLine();
                continue;
            }

            var highlightedLine = language switch
            {
                "csharp" => HighlightCSharp(line, colors),
                "javascript" or "typescript" => HighlightJavaScript(line, colors),
                "python" => HighlightPython(line, colors),
                "go" => HighlightGo(line, colors),
                "rust" => HighlightRust(line, colors),
                "shell" => HighlightShell(line, colors),
                "json" => HighlightJson(line, colors),
                "sql" => HighlightSql(line, colors),
                "java" => HighlightJava(line, colors),
                "cpp" => HighlightCPlusPlus(line, colors),
                "php" => HighlightPhp(line, colors),
                "swift" => HighlightSwift(line, colors),
                "html" => HighlightHtml(line, colors),
                "xml" => HighlightXml(line, colors),
                "css" => HighlightCss(line, colors),
                _ => EscapeMarkup(line)
            };

            output.AppendLine(highlightedLine);
        }

        return output.ToString();
    }

    private static ColorScheme GetColorScheme(bool isDark)
    {
        if (isDark)
        {
            return new ColorScheme
            {
                Keyword = "\u001b[35m",      // Magenta
                String = "\u001b[33m",       // Yellow
                Comment = "\u001b[32m",      // Green
                Number = "\u001b[36m",       // Cyan
                Function = "\u001b[34m",     // Blue
                Type = "\u001b[96m",         // Bright Cyan
                Operator = "\u001b[90m",     // Gray
                Punctuation = "\u001b[37m",  // White
                Reset = "\u001b[0m"          // Reset
            };
        }
        else
        {
            return new ColorScheme
            {
                Keyword = "\u001b[34m",      // Blue
                String = "\u001b[31m",       // Red
                Comment = "\u001b[32m",      // Green
                Number = "\u001b[34m",       // Blue
                Function = "\u001b[35m",     // Magenta
                Type = "\u001b[34m",         // Blue
                Operator = "\u001b[90m",     // Gray
                Punctuation = "\u001b[30m",  // Black
                Reset = "\u001b[0m"          // Reset
            };
        }
    }

    private static string HighlightCSharp(string line, ColorScheme colors)
    {
        // Check for comments first
        var commentMatch = Regex.Match(line, @"//.*$");
        string beforeComment = commentMatch.Success ? line[..commentMatch.Index] : line;
        string comment = commentMatch.Success ? commentMatch.Value : "";

        // Highlight keywords
        var keywords = new[]
        {
            "public", "private", "protected", "internal", "static", "readonly", "const",
            "class", "struct", "interface", "enum", "namespace", "using",
            "void", "int", "string", "bool", "var", "double", "float", "decimal", "long",
            "if", "else", "for", "foreach", "while", "do", "switch", "case", "break",
            "return", "new", "null", "true", "false", "this", "base",
            "async", "await", "try", "catch", "finally", "throw"
        };

        var result = beforeComment;
        foreach (var keyword in keywords)
        {
            result = Regex.Replace(result, $@"\b{keyword}\b", $"{colors.Keyword}{keyword}{colors.Reset}");
        }

        // Highlight strings
        result = Regex.Replace(result, @"""(?:[^""\\]|\\.)*""", m => $"{colors.String}{m.Value}{colors.Reset}");
        result = Regex.Replace(result, @"'(?:[^'\\]|\\.)*'", m => $"{colors.String}{m.Value}{colors.Reset}");

        // Highlight numbers
        result = Regex.Replace(result, @"\b\d+\.?\d*\b", m => $"{colors.Number}{m.Value}{colors.Reset}");

        // Add comment if exists
        if (!string.IsNullOrEmpty(comment))
        {
            result += $"{colors.Comment}{comment}{colors.Reset}";
        }

        return result;
    }

    private static string HighlightJavaScript(string line, ColorScheme colors)
    {
        // Check for comments
        var commentMatch = Regex.Match(line, @"//.*$");
        string beforeComment = commentMatch.Success ? line[..commentMatch.Index] : line;
        string comment = commentMatch.Success ? commentMatch.Value : "";

        var keywords = new[]
        {
            "function", "const", "let", "var", "if", "else", "for", "while", "do",
            "return", "new", "this", "class", "extends", "import", "export", "from",
            "async", "await", "try", "catch", "finally", "throw",
            "true", "false", "null", "undefined"
        };

        var result = beforeComment;
        foreach (var keyword in keywords)
        {
            result = Regex.Replace(result, $@"\b{keyword}\b", $"{colors.Keyword}{keyword}{colors.Reset}");
        }

        // Strings with template literals
        result = Regex.Replace(result, @"`(?:[^`\\]|\\.)*`", m => $"{colors.String}{m.Value}{colors.Reset}");
        result = Regex.Replace(result, @"""(?:[^""\\]|\\.)*""", m => $"{colors.String}{m.Value}{colors.Reset}");
        result = Regex.Replace(result, @"'(?:[^'\\]|\\.)*'", m => $"{colors.String}{m.Value}{colors.Reset}");

        // Numbers
        result = Regex.Replace(result, @"\b\d+\.?\d*\b", m => $"{colors.Number}{m.Value}{colors.Reset}");

        // Functions (word followed by parenthesis)
        result = Regex.Replace(result, @"\b(\w+)(?=\s*\()", m => $"{colors.Function}{m.Groups[1].Value}{colors.Reset}");

        if (!string.IsNullOrEmpty(comment))
        {
            result += $"{colors.Comment}{comment}{colors.Reset}";
        }

        return result;
    }

    private static string HighlightPython(string line, ColorScheme colors)
    {
        // Check for comments
        var commentMatch = Regex.Match(line, @"#.*$");
        string beforeComment = commentMatch.Success ? line[..commentMatch.Index] : line;
        string comment = commentMatch.Success ? commentMatch.Value : "";

        var keywords = new[]
        {
            "def", "class", "if", "elif", "else", "for", "while", "in", "return",
            "import", "from", "as", "try", "except", "finally", "raise", "with",
            "True", "False", "None", "and", "or", "not", "is", "lambda", "yield",
            "async", "await", "pass", "break", "continue"
        };

        var result = beforeComment;
        foreach (var keyword in keywords)
        {
            result = Regex.Replace(result, $@"\b{keyword}\b", $"{colors.Keyword}{keyword}{colors.Reset}");
        }

        // Strings (support triple quotes, double, and single)
        result = Regex.Replace(result, @"(""""""[\s\S]*?""""""|'''[\s\S]*?'''|""(?:[^""\\]|\\.)*""|'(?:[^'\\]|\\.)*')",
            m => $"{colors.String}{m.Value}{colors.Reset}");

        // Numbers
        result = Regex.Replace(result, @"\b\d+\.?\d*\b", m => $"{colors.Number}{m.Value}{colors.Reset}");

        // Functions (def or word followed by parenthesis)
        result = Regex.Replace(result, @"\b(\w+)(?=\s*\()", m => $"{colors.Function}{m.Groups[1].Value}{colors.Reset}");

        if (!string.IsNullOrEmpty(comment))
        {
            result += $"{colors.Comment}{comment}{colors.Reset}";
        }

        return result;
    }

    private static string HighlightGo(string line, ColorScheme colors)
    {
        var commentMatch = Regex.Match(line, @"//.*$");
        string beforeComment = commentMatch.Success ? line[..commentMatch.Index] : line;
        string comment = commentMatch.Success ? commentMatch.Value : "";

        var keywords = new[]
        {
            "package", "import", "func", "var", "const", "type", "struct", "interface",
            "if", "else", "for", "range", "return", "defer", "go", "select", "case",
            "switch", "break", "continue", "fallthrough", "goto", "chan", "map",
            "true", "false", "nil", "make", "new", "len", "cap", "append", "copy"
        };

        var result = beforeComment;
        foreach (var keyword in keywords)
        {
            result = Regex.Replace(result, $@"\b{keyword}\b", $"{colors.Keyword}{keyword}{colors.Reset}");
        }

        // Strings
        result = Regex.Replace(result, @"""(?:[^""\\]|\\.)*""", m => $"{colors.String}{m.Value}{colors.Reset}");
        result = Regex.Replace(result, @"`[^`]*`", m => $"{colors.String}{m.Value}{colors.Reset}");

        // Numbers
        result = Regex.Replace(result, @"\b\d+\.?\d*\b", m => $"{colors.Number}{m.Value}{colors.Reset}");

        // Functions
        result = Regex.Replace(result, @"\b(\w+)(?=\s*\()", m => $"{colors.Function}{m.Groups[1].Value}{colors.Reset}");

        if (!string.IsNullOrEmpty(comment))
        {
            result += $"{colors.Comment}{comment}{colors.Reset}";
        }

        return result;
    }

    private static string HighlightRust(string line, ColorScheme colors)
    {
        var commentMatch = Regex.Match(line, @"//.*$");
        string beforeComment = commentMatch.Success ? line[..commentMatch.Index] : line;
        string comment = commentMatch.Success ? commentMatch.Value : "";

        var keywords = new[]
        {
            "fn", "let", "mut", "const", "static", "pub", "use", "mod", "struct",
            "enum", "trait", "impl", "if", "else", "match", "for", "while", "loop",
            "return", "break", "continue", "as", "async", "await", "move",
            "true", "false", "Some", "None", "Ok", "Err", "self", "Self"
        };

        var result = beforeComment;
        foreach (var keyword in keywords)
        {
            result = Regex.Replace(result, $@"\b{keyword}\b", $"{colors.Keyword}{keyword}{colors.Reset}");
        }

        // Strings
        result = Regex.Replace(result, @"""(?:[^""\\]|\\.)*""", m => $"{colors.String}{m.Value}{colors.Reset}");

        // Numbers
        result = Regex.Replace(result, @"\b\d+\.?\d*\b", m => $"{colors.Number}{m.Value}{colors.Reset}");

        if (!string.IsNullOrEmpty(comment))
        {
            result += $"{colors.Comment}{comment}{colors.Reset}";
        }

        return result;
    }

    private static string HighlightShell(string line, ColorScheme colors)
    {
        var commentMatch = Regex.Match(line, @"#.*$");
        string beforeComment = commentMatch.Success ? line[..commentMatch.Index] : line;
        string comment = commentMatch.Success ? commentMatch.Value : "";

        var keywords = new[]
        {
            "if", "then", "else", "elif", "fi", "for", "while", "do", "done",
            "case", "esac", "function", "return", "exit", "export", "source"
        };

        var result = beforeComment;
        foreach (var keyword in keywords)
        {
            result = Regex.Replace(result, $@"\b{keyword}\b", $"{colors.Keyword}{keyword}{colors.Reset}");
        }

        // Strings
        result = Regex.Replace(result, @"""(?:[^""\\]|\\.)*""", m => $"{colors.String}{m.Value}{colors.Reset}");
        result = Regex.Replace(result, @"'[^']*'", m => $"{colors.String}{m.Value}{colors.Reset}");

        if (!string.IsNullOrEmpty(comment))
        {
            result += $"{colors.Comment}{comment}{colors.Reset}";
        }

        return result;
    }

    private static string HighlightJson(string line, ColorScheme colors)
    {
        var result = line;

        // Strings (keys and values)
        result = Regex.Replace(result, @"""(?:[^""\\]|\\.)*""", m => $"{colors.String}{m.Value}{colors.Reset}");

        // Numbers
        result = Regex.Replace(result, @"\b-?\d+\.?\d*([eE][+-]?\d+)?\b", m => $"{colors.Number}{m.Value}{colors.Reset}");

        // Booleans and null
        result = Regex.Replace(result, @"\b(true|false|null)\b", m => $"{colors.Keyword}{m.Value}{colors.Reset}");

        return result;
    }

    private static string HighlightSql(string line, ColorScheme colors)
    {
        var commentMatch = Regex.Match(line, @"--.*$");
        string beforeComment = commentMatch.Success ? line[..commentMatch.Index] : line;
        string comment = commentMatch.Success ? commentMatch.Value : "";

        var keywords = new[]
        {
            "SELECT", "FROM", "WHERE", "INSERT", "UPDATE", "DELETE", "CREATE", "DROP",
            "ALTER", "TABLE", "INDEX", "JOIN", "LEFT", "RIGHT", "INNER", "OUTER",
            "ON", "AS", "AND", "OR", "NOT", "IN", "LIKE", "ORDER", "BY", "GROUP",
            "HAVING", "LIMIT", "OFFSET", "NULL", "IS", "EXISTS", "BETWEEN"
        };

        var result = beforeComment;
        foreach (var keyword in keywords)
        {
            result = Regex.Replace(result, $@"\b{keyword}\b", $"{colors.Keyword}{keyword}{colors.Reset}", RegexOptions.IgnoreCase);
        }

        // Strings
        result = Regex.Replace(result, @"'(?:[^'\\]|\\.)*'", m => $"{colors.String}{m.Value}{colors.Reset}");

        // Numbers
        result = Regex.Replace(result, @"\b\d+\.?\d*\b", m => $"{colors.Number}{m.Value}{colors.Reset}");

        if (!string.IsNullOrEmpty(comment))
        {
            result += $"{colors.Comment}{comment}{colors.Reset}";
        }

        return result;
    }

    private static string HighlightJava(string line, ColorScheme colors)
    {
        // Check for comments
        var commentMatch = Regex.Match(line, @"//.*$");
        string beforeComment = commentMatch.Success ? line[..commentMatch.Index] : line;
        string comment = commentMatch.Success ? commentMatch.Value : "";

        var keywords = new[]
        {
            "public", "private", "protected", "static", "final", "abstract", "synchronized",
            "class", "interface", "enum", "extends", "implements", "package", "import",
            "void", "int", "long", "double", "float", "boolean", "char", "byte", "short",
            "if", "else", "for", "while", "do", "switch", "case", "break", "continue",
            "return", "new", "null", "true", "false", "this", "super", "instanceof",
            "try", "catch", "finally", "throw", "throws", "assert"
        };

        var result = beforeComment;
        foreach (var keyword in keywords)
        {
            result = Regex.Replace(result, $@"\b{keyword}\b", $"{colors.Keyword}{keyword}{colors.Reset}");
        }

        // Strings
        result = Regex.Replace(result, @"""(?:[^""\\]|\\.)*""", m => $"{colors.String}{m.Value}{colors.Reset}");
        result = Regex.Replace(result, @"'(?:[^'\\]|\\.)*'", m => $"{colors.String}{m.Value}{colors.Reset}");

        // Numbers
        result = Regex.Replace(result, @"\b\d+\.?\d*[LlFfDd]?\b", m => $"{colors.Number}{m.Value}{colors.Reset}");

        // Functions (word followed by parenthesis)
        result = Regex.Replace(result, @"\b(\w+)(?=\s*\()", m => $"{colors.Function}{m.Groups[1].Value}{colors.Reset}");

        if (!string.IsNullOrEmpty(comment))
        {
            result += $"{colors.Comment}{comment}{colors.Reset}";
        }

        return result;
    }

    private static string HighlightCPlusPlus(string line, ColorScheme colors)
    {
        // Check for comments
        var commentMatch = Regex.Match(line, @"//.*$");
        string beforeComment = commentMatch.Success ? line[..commentMatch.Index] : line;
        string comment = commentMatch.Success ? commentMatch.Value : "";

        var keywords = new[]
        {
            "public", "private", "protected", "static", "const", "constexpr", "virtual", "override",
            "class", "struct", "union", "enum", "namespace", "using", "typedef", "template",
            "void", "int", "long", "double", "float", "bool", "char", "unsigned", "signed",
            "if", "else", "for", "while", "do", "switch", "case", "break", "continue",
            "return", "new", "delete", "nullptr", "true", "false", "this", "auto",
            "try", "catch", "throw", "const_cast", "static_cast", "dynamic_cast", "reinterpret_cast",
            "sizeof", "typename", "decltype", "inline", "extern", "friend", "explicit"
        };

        var result = beforeComment;
        foreach (var keyword in keywords)
        {
            result = Regex.Replace(result, $@"\b{keyword}\b", $"{colors.Keyword}{keyword}{colors.Reset}");
        }

        // Strings
        result = Regex.Replace(result, @"""(?:[^""\\]|\\.)*""", m => $"{colors.String}{m.Value}{colors.Reset}");
        result = Regex.Replace(result, @"'(?:[^'\\]|\\.)*'", m => $"{colors.String}{m.Value}{colors.Reset}");

        // Numbers (including hex)
        result = Regex.Replace(result, @"\b0[xX][0-9a-fA-F]+\b", m => $"{colors.Number}{m.Value}{colors.Reset}");
        result = Regex.Replace(result, @"\b\d+\.?\d*[LlFfUu]?\b", m => $"{colors.Number}{m.Value}{colors.Reset}");

        // Functions
        result = Regex.Replace(result, @"\b(\w+)(?=\s*\()", m => $"{colors.Function}{m.Groups[1].Value}{colors.Reset}");

        if (!string.IsNullOrEmpty(comment))
        {
            result += $"{colors.Comment}{comment}{colors.Reset}";
        }

        return result;
    }

    private static string HighlightPhp(string line, ColorScheme colors)
    {
        // Check for comments
        var commentMatch = Regex.Match(line, @"(//|#).*$");
        string beforeComment = commentMatch.Success ? line[..commentMatch.Index] : line;
        string comment = commentMatch.Success ? commentMatch.Value : "";

        var keywords = new[]
        {
            "public", "private", "protected", "static", "final", "abstract",
            "class", "interface", "trait", "extends", "implements", "namespace", "use",
            "function", "return", "if", "else", "elseif", "for", "foreach", "while", "do",
            "switch", "case", "break", "continue", "as", "new", "clone", "instanceof",
            "try", "catch", "finally", "throw", "echo", "print", "var", "const",
            "true", "false", "null", "array", "isset", "empty", "unset", "require", "include"
        };

        var result = beforeComment;
        foreach (var keyword in keywords)
        {
            result = Regex.Replace(result, $@"\b{keyword}\b", $"{colors.Keyword}{keyword}{colors.Reset}");
        }

        // Variables ($variable)
        result = Regex.Replace(result, @"\$\w+", m => $"{colors.Type}{m.Value}{colors.Reset}");

        // Strings
        result = Regex.Replace(result, @"""(?:[^""\\]|\\.)*""", m => $"{colors.String}{m.Value}{colors.Reset}");
        result = Regex.Replace(result, @"'[^']*'", m => $"{colors.String}{m.Value}{colors.Reset}");

        // Numbers
        result = Regex.Replace(result, @"\b\d+\.?\d*\b", m => $"{colors.Number}{m.Value}{colors.Reset}");

        // Functions
        result = Regex.Replace(result, @"\b(\w+)(?=\s*\()", m => $"{colors.Function}{m.Groups[1].Value}{colors.Reset}");

        if (!string.IsNullOrEmpty(comment))
        {
            result += $"{colors.Comment}{comment}{colors.Reset}";
        }

        return result;
    }

    private static string HighlightSwift(string line, ColorScheme colors)
    {
        // Check for comments
        var commentMatch = Regex.Match(line, @"//.*$");
        string beforeComment = commentMatch.Success ? line[..commentMatch.Index] : line;
        string comment = commentMatch.Success ? commentMatch.Value : "";

        var keywords = new[]
        {
            "func", "var", "let", "class", "struct", "enum", "protocol", "extension",
            "import", "init", "deinit", "subscript", "typealias", "associatedtype",
            "public", "private", "fileprivate", "internal", "open", "static", "final",
            "if", "else", "for", "while", "repeat", "switch", "case", "break", "continue",
            "return", "guard", "defer", "where", "in", "is", "as", "try", "catch", "throw",
            "true", "false", "nil", "self", "Self", "super", "mutating", "nonmutating",
            "lazy", "weak", "unowned", "optional", "required", "override", "convenience"
        };

        var result = beforeComment;
        foreach (var keyword in keywords)
        {
            result = Regex.Replace(result, $@"\b{keyword}\b", $"{colors.Keyword}{keyword}{colors.Reset}");
        }

        // Strings (including interpolation)
        result = Regex.Replace(result, @"""(?:[^""\\]|\\.)*""", m => $"{colors.String}{m.Value}{colors.Reset}");

        // Numbers
        result = Regex.Replace(result, @"\b\d+\.?\d*\b", m => $"{colors.Number}{m.Value}{colors.Reset}");

        // Functions
        result = Regex.Replace(result, @"\b(\w+)(?=\s*\()", m => $"{colors.Function}{m.Groups[1].Value}{colors.Reset}");

        if (!string.IsNullOrEmpty(comment))
        {
            result += $"{colors.Comment}{comment}{colors.Reset}";
        }

        return result;
    }

    private static string HighlightHtml(string line, ColorScheme colors)
    {
        var result = line;

        // HTML comments
        result = Regex.Replace(result, @"<!--.*?-->", m => $"{colors.Comment}{m.Value}{colors.Reset}");

        // Doctype
        result = Regex.Replace(result, @"<!DOCTYPE[^>]*>", m => $"{colors.Keyword}{m.Value}{colors.Reset}", RegexOptions.IgnoreCase);

        // Tags with attributes
        result = Regex.Replace(result, @"<(/?)(\w+)((?:\s+[\w-]+(?:\s*=\s*(?:""[^""]*""|'[^']*'|[^\s>]+))?)*\s*)(/?)" + ">",
            m =>
            {
                var openBracket = "<";
                var closingSlash = m.Groups[1].Value;
                var tagName = m.Groups[2].Value;
                var attributes = m.Groups[3].Value;
                var selfClosing = m.Groups[4].Value;
                var closeBracket = ">";

                // Colorize tag name
                var colorizedTag = $"{openBracket}{closingSlash}{colors.Keyword}{tagName}{colors.Reset}";

                // Colorize attributes
                var colorizedAttrs = Regex.Replace(attributes, @"([\w-]+)(\s*=\s*)(""[^""]*""|'[^']*'|[^\s>]+)",
                    attrMatch =>
                    {
                        var attrName = attrMatch.Groups[1].Value;
                        var equals = attrMatch.Groups[2].Value;
                        var attrValue = attrMatch.Groups[3].Value;
                        return $"{colors.Type}{attrName}{colors.Reset}{equals}{colors.String}{attrValue}{colors.Reset}";
                    });

                return $"{colorizedTag}{colorizedAttrs}{selfClosing}{closeBracket}";
            });

        return result;
    }

    private static string HighlightXml(string line, ColorScheme colors)
    {
        var result = line;

        // XML comments
        result = Regex.Replace(result, @"<!--.*?-->", m => $"{colors.Comment}{m.Value}{colors.Reset}");

        // XML declaration
        result = Regex.Replace(result, @"<\?xml[^?]*\?>", m => $"{colors.Keyword}{m.Value}{colors.Reset}", RegexOptions.IgnoreCase);

        // CDATA
        result = Regex.Replace(result, @"<!\[CDATA\[.*?\]\]>", m => $"{colors.String}{m.Value}{colors.Reset}");

        // Tags with attributes (similar to HTML but stricter)
        result = Regex.Replace(result, @"<(/?)(\w+)((?:\s+[\w:-]+(?:\s*=\s*(?:""[^""]*""|'[^']*'))?)*\s*)(/?)" + ">",
            m =>
            {
                var openBracket = "<";
                var closingSlash = m.Groups[1].Value;
                var tagName = m.Groups[2].Value;
                var attributes = m.Groups[3].Value;
                var selfClosing = m.Groups[4].Value;
                var closeBracket = ">";

                // Colorize tag name
                var colorizedTag = $"{openBracket}{closingSlash}{colors.Keyword}{tagName}{colors.Reset}";

                // Colorize attributes
                var colorizedAttrs = Regex.Replace(attributes, @"([\w:-]+)(\s*=\s*)(""[^""]*""|'[^']*')",
                    attrMatch =>
                    {
                        var attrName = attrMatch.Groups[1].Value;
                        var equals = attrMatch.Groups[2].Value;
                        var attrValue = attrMatch.Groups[3].Value;
                        return $"{colors.Type}{attrName}{colors.Reset}{equals}{colors.String}{attrValue}{colors.Reset}";
                    });

                return $"{colorizedTag}{colorizedAttrs}{selfClosing}{closeBracket}";
            });

        return result;
    }

    private static string HighlightCss(string line, ColorScheme colors)
    {
        var result = line;

        // CSS comments
        result = Regex.Replace(result, @"/\*.*?\*/", m => $"{colors.Comment}{m.Value}{colors.Reset}");

        // Selectors (simplified - tags, classes, ids)
        result = Regex.Replace(result, @"^([\w#.\-\[\]:,\s>+~*]+)(?=\s*\{)", m => $"{colors.Keyword}{m.Value}{colors.Reset}");

        // Property names
        result = Regex.Replace(result, @"\b([\w-]+)(\s*:\s*)", m => $"{colors.Type}{m.Groups[1].Value}{colors.Reset}{m.Groups[2].Value}");

        // Property values (strings, colors, numbers, units)
        result = Regex.Replace(result, @"""[^""]*""", m => $"{colors.String}{m.Value}{colors.Reset}");
        result = Regex.Replace(result, @"'[^']*'", m => $"{colors.String}{m.Value}{colors.Reset}");

        // Colors (#hex, rgb(), rgba())
        result = Regex.Replace(result, @"#[0-9a-fA-F]{3,8}\b", m => $"{colors.Number}{m.Value}{colors.Reset}");
        result = Regex.Replace(result, @"\b(rgb|rgba|hsl|hsla)\([^)]+\)", m => $"{colors.Number}{m.Value}{colors.Reset}");

        // Numbers with units
        result = Regex.Replace(result, @"\b\d+\.?\d*(px|em|rem|%|vh|vw|pt|cm|mm|in|ex|ch|pc|vmin|vmax|deg|rad|turn|s|ms)\b",
            m => $"{colors.Number}{m.Value}{colors.Reset}");

        // Plain numbers
        result = Regex.Replace(result, @"\b\d+\.?\d*\b", m => $"{colors.Number}{m.Value}{colors.Reset}");

        // Important keyword
        result = Regex.Replace(result, @"!important\b", m => $"{colors.Keyword}{m.Value}{colors.Reset}");

        return result;
    }

    private static string EscapeMarkup(string text)
    {
        return text.Replace("[", "[[").Replace("]", "]]");
    }

    private class ColorScheme
    {
        public string Keyword { get; set; } = "";
        public string String { get; set; } = "";
        public string Comment { get; set; } = "";
        public string Number { get; set; } = "";
        public string Function { get; set; } = "";
        public string Type { get; set; } = "";
        public string Operator { get; set; } = "";
        public string Punctuation { get; set; } = "";
        public string Reset { get; set; } = "\u001b[0m";
    }
}
