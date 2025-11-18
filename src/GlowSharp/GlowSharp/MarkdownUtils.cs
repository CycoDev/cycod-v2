using System.Text.RegularExpressions;

namespace GlowSharp;

/// <summary>
/// Utility functions for markdown processing (ported from glow/utils/utils.go)
/// </summary>
public static class MarkdownUtils
{
    private static readonly string[] MarkdownExtensions =
    {
        ".md", ".mdown", ".mkdn", ".mkd", ".markdown"
    };

    // Regex to detect YAML frontmatter (--- delimiters)
    private static readonly Regex YamlPattern = new(@"(?m)^---\r?\n(\s*\r?\n)?", RegexOptions.Compiled);

    /// <summary>
    /// Removes YAML frontmatter from markdown content.
    /// Ported from utils.RemoveFrontmatter in utils/utils.go:18-23
    /// </summary>
    public static string RemoveFrontmatter(string content)
    {
        var boundaries = DetectFrontmatter(content);
        if (boundaries.Start == 0 && boundaries.End > 0)
        {
            return content[boundaries.End..];
        }
        return content;
    }

    /// <summary>
    /// Detects frontmatter boundaries in content.
    /// Ported from utils.detectFrontmatter in utils/utils.go:27-32
    /// </summary>
    private static (int Start, int End) DetectFrontmatter(string content)
    {
        var matches = YamlPattern.Matches(content);
        if (matches.Count > 1)
        {
            return (matches[0].Index, matches[1].Index + matches[1].Length);
        }
        return (-1, -1);
    }

    /// <summary>
    /// Wraps content in a markdown code block with language hint.
    /// Ported from utils.WrapCodeBlock in utils/utils.go:44-46
    /// </summary>
    public static string WrapCodeBlock(string content, string extension)
    {
        // Remove the leading dot from extension
        var language = extension.TrimStart('.');
        return $"```{language}\n{content}\n```";
    }

    /// <summary>
    /// Checks if a filename has a markdown extension.
    /// Ported from utils.IsMarkdownFile in utils/utils.go:53-70
    /// </summary>
    public static bool IsMarkdownFile(string? filename)
    {
        if (string.IsNullOrEmpty(filename))
        {
            return true; // Default to markdown if no filename
        }

        var ext = Path.GetExtension(filename);

        if (string.IsNullOrEmpty(ext))
        {
            // By default, assume it's a markdown file
            return true;
        }

        foreach (var mdExt in MarkdownExtensions)
        {
            if (string.Equals(ext, mdExt, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        // Has an extension but not markdown, assume code file
        return false;
    }

    /// <summary>
    /// Expands ~ and environment variables in a path.
    /// Ported from utils.ExpandPath in utils/utils.go:35-41
    /// </summary>
    public static string ExpandPath(string path)
    {
        if (path.StartsWith("~"))
        {
            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            path = path.Replace("~", home);
        }
        return Environment.ExpandEnvironmentVariables(path);
    }
}
