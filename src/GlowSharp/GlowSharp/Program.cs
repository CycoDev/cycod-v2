using System.CommandLine;
using Spectre.Console;

namespace GlowSharp;

/// <summary>
/// Main CLI program for GlowSharp.
/// Ported from glow/main.go
/// </summary>
class Program
{
    static async Task<int> Main(string[] args)
    {
        var rootCommand = new RootCommand("Render markdown on the CLI, with pizzazz! (C# port of Glow)")
        {
            Name = "glowsharp"
        };

        // Arguments
        var sourceArgument = new Argument<string?>(
            name: "source",
            description: "Markdown file to render, or '-' for stdin",
            getDefaultValue: () => null
        );

        // Options (matching glow's flags)
        var styleOption = new Option<string>(
            aliases: new[] { "--style", "-s" },
            description: "Style to use (auto, dark, light)",
            getDefaultValue: () => "auto"
        );

        var widthOption = new Option<int>(
            aliases: new[] { "--width", "-w" },
            description: "Word-wrap at width (0 to disable)",
            getDefaultValue: () => 0
        );

        var streamOption = new Option<bool>(
            aliases: new[] { "--stream" },
            description: "Stream markdown progressively (simulates LLM response)",
            getDefaultValue: () => false
        );

        rootCommand.AddArgument(sourceArgument);
        rootCommand.AddOption(styleOption);
        rootCommand.AddOption(widthOption);
        rootCommand.AddOption(streamOption);

        rootCommand.SetHandler(
            Execute,
            sourceArgument,
            styleOption,
            widthOption,
            streamOption
        );

        return await rootCommand.InvokeAsync(args);
    }

    static async Task Execute(string? source, string styleName, int width, bool stream)
    {
        try
        {
            // Select style (from main.go:180-190)
            var style = styleName.ToLowerInvariant() switch
            {
                "dark" => StyleConfig.DarkStyle,
                "light" => StyleConfig.LightStyle,
                "auto" or _ => StyleConfig.AutoStyle
            };

            // Handle streaming mode
            if (stream)
            {
                await ExecuteStreaming(source, style);
                return;
            }

            // Create renderer (from main.go:292-298)
            var renderer = new MarkdownRenderer(style, width);

            string output;

            // Handle input source (from main.go:222-260)
            if (source == "-" || (source == null && IsStdinRedirected()))
            {
                // Read from stdin (main.go:228-231)
                output = renderer.RenderStdin();
            }
            else if (!string.IsNullOrEmpty(source))
            {
                // Read from file
                if (!File.Exists(source))
                {
                    AnsiConsole.MarkupLine($"[red]Error:[/] File not found: {source}");
                    Environment.Exit(1);
                }

                output = renderer.RenderFile(source);
            }
            else
            {
                // No input provided
                AnsiConsole.MarkupLine("[yellow]Usage:[/] glowsharp [file] or echo \"# Markdown\" | glowsharp -");
                AnsiConsole.MarkupLine("[grey]Try: glowsharp --help[/]");
                Environment.Exit(1);
                return;
            }

            // Output the rendered markdown (main.go:337-340)
            Console.Write(output);
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]Error:[/] {ex.Message}");
            if (ex.InnerException != null)
            {
                AnsiConsole.MarkupLine($"[grey]{ex.InnerException.Message}[/]");
            }
            Environment.Exit(1);
        }
    }

    /// <summary>
    /// Execute in streaming mode - progressively renders markdown as chunks arrive.
    /// Simulates LLM streaming by emitting chunks of 32-64 characters with 50-150ms delays.
    /// </summary>
    static async Task ExecuteStreaming(string? source, StyleConfig style)
    {
        // Get markdown content
        string markdown;

        if (source == "-" || (source == null && IsStdinRedirected()))
        {
            // Read from stdin
            using var reader = new StreamReader(Console.OpenStandardInput());
            markdown = await reader.ReadToEndAsync();
        }
        else if (!string.IsNullOrEmpty(source))
        {
            // Read from file
            if (!File.Exists(source))
            {
                AnsiConsole.MarkupLine($"[red]Error:[/] File not found: {source}");
                Environment.Exit(1);
                return;
            }

            markdown = await File.ReadAllTextAsync(source);

            // Remove frontmatter
            markdown = MarkdownUtils.RemoveFrontmatter(markdown);
        }
        else
        {
            AnsiConsole.MarkupLine("[yellow]Usage:[/] glowsharp --stream [file]");
            Environment.Exit(1);
            return;
        }

        // Create streaming renderer
        var renderer = new StreamingMarkdownRenderer(style);
        var random = new Random();
        var position = 0;

        // Producer: emit chunks of markdown with random intervals
        while (position < markdown.Length)
        {
            // Pick random chunk size between 32-64 characters
            var chunkSize = random.Next(32, 65);
            var actualSize = Math.Min(chunkSize, markdown.Length - position);
            var chunk = markdown.Substring(position, actualSize);

            // Feed chunk to renderer (renders immediately to console)
            renderer.AppendChunk(chunk);
            position += actualSize;

            // Random delay between 15-50ms (faster streaming)
            await Task.Delay(random.Next(15, 51));
        }

        // Flush any remaining buffered content
        renderer.Flush();
    }

    /// <summary>
    /// Checks if stdin is redirected (piped input).
    /// Ported from main.go:211-220
    /// </summary>
    static bool IsStdinRedirected()
    {
        try
        {
            return Console.IsInputRedirected;
        }
        catch
        {
            return false;
        }
    }
}
