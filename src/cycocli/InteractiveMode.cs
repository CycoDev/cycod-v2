using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using CycoTui.Core.Backend;
using CycoTui.Core.Logging;
using CycoTui.Core.Style;
using CycoTui.Core.Layout;
using CycoTui.Core.Terminal;
using CycoTui.Core.Widgets;
using GlowSharp;

namespace CycoTui.Sample;

internal static class InteractiveMode
{
    private static bool _shouldExit;
    private static ITerminalBackend? _backend;
    private static Terminal? _terminal;
    private static string? _cachedGitBranch;
    private static string? _cachedCurrentLLM;
    private static int _assistantMessageStartIndex = -1;

    private static readonly List<string> _messages = new();
    // Debug area removed; retaining stub methods only
    private static readonly MultiLineInputState _inputState = new();
    private static CompletionState _completionState = CompletionState.CreateInactive();

    // ANSI styled lines
    private class StyledLine
    {
        public List<(string Text, Style Style)> Segments { get; } = new();
        public bool IsUserMessage { get; set; }
    }
    private static readonly List<StyledLine> _styledMessages = new();

    private static List<(string Text, Style Style)> ParseAnsiSegments(string input)
    {
        var segments = new List<(string Text, Style Style)>();
        var currentStyle = Style.Empty;
        var sb = new System.Text.StringBuilder();
        for (int i = 0; i < input.Length; i++)
        {
            char c = input[i];
            if (c == '\u001b')
            {
                if (sb.Length > 0)
                {
                    segments.Add((sb.ToString(), currentStyle));
                    sb.Clear();
                }
                // CSI sequence ESC[
                if (i + 1 < input.Length && input[i + 1] == '[')
                {
                    int start = i + 2;
                    int j = start;
                    // Find final byte of CSI (any final byte in '@'..'~')
                    while (j < input.Length && !IsCsiFinalByte(input[j])) j++;
                    if (j < input.Length)
                    {
                        // If it's an SGR ('m'), parse; otherwise skip entire CSI
                        if (input[j] == 'm')
                        {
                            var codesStr = input.Substring(start, j - start);
                            var codes = codesStr.Split(';', StringSplitOptions.RemoveEmptyEntries);
                            currentStyle = ApplySgrCodes(currentStyle, codes);
                        }
                        i = j; // advance past final byte

                        continue;
                    }
                    // Incomplete CSI at end - skip remainder
                    break;
                }
                else if (i + 1 < input.Length && input[i + 1] == ']') // OSC sequence ESC]
                {
                    int start = i + 2;
                    int j = start;
                    // OSC ends with BEL (0x07) or ESC \
                    while (j < input.Length && input[j] != '\u0007')
                    {
                        if (input[j] == '\u001b' && j + 1 < input.Length && input[j + 1] == '\\')
                        {
                            j += 2; // consume ESC \
                            break;
                        }
                        j++;
                    }
                    i = j; // skip OSC
                    continue;
                }
                else
                {
                    // Single-char escape (ESC=, ESC>) -> skip next char if present
                    if (i + 1 < input.Length) i++;
                    continue;
                }
            }
            sb.Append(c);
        }
        if (sb.Length > 0) segments.Add((sb.ToString(), currentStyle));
        return segments;
    }
    private static bool IsCsiFinalByte(char c) => c >= '@' && c <= '~';

    private static Style ApplySgrCodes(Style baseStyle, string[] codes)
    {
        var style = baseStyle;
        foreach (var codeStr in codes)
        {
            if (!int.TryParse(codeStr, out var code)) continue;
            switch (code)
            {
                case 0: style = Style.Empty; break;
                case 1: style = style.Add(TextModifier.Bold); break;
                case 2: style = style.Add(TextModifier.Dim); break;
                case 7: style = style.Add(TextModifier.Invert); break;
                case 22: style = RemoveStyleModifiers(style, TextModifier.Bold, TextModifier.Dim); break;
                case 27: style = RemoveStyleModifiers(style, TextModifier.Invert); break;
                case >= 30 and <= 37: style = style.WithForeground(MapAnsiColor(code - 30)); break;
                case >= 90 and <= 97: style = style.WithForeground(MapAnsiColor(code - 90, bright: true)); break;
            }
        }
        return style;
    }

    private static Style RemoveStyleModifiers(Style style, params TextModifier[] mods)
    {
        style = Style.Empty;
        return style;
    }

    private static Color MapAnsiColor(int idx, bool bright = false) => idx switch
    {
        0 => bright ? Color.Gray : Color.Black,
        1 => Color.Red,
        2 => Color.Green,
        3 => Color.Yellow,
        4 => Color.Blue,
        5 => Color.Magenta,
        6 => Color.Cyan,
        7 => Color.Gray,
        _ => Color.Gray
    };

    private static readonly List<CompletionTrigger> _completionTriggers = new()
    {
        new CompletionTrigger('@', async () =>
        {
            var workspaceRoot = Environment.CurrentDirectory;
            return await Task.Run(() => WorkspaceFileScanner.ScanFiles(workspaceRoot, maxFiles: 1000));
        }, "Files"),
        new CompletionTrigger('/', async () =>
        {
            await Task.CompletedTask;
            return new List<string> { "exit" };
        }, "Commands"),
        new CompletionTrigger('#', async () =>
        {
            await Task.CompletedTask;
            return new List<string> { "bug", "feature", "enhancement", "documentation", "refactor", "test", "performance", "security" };
        }, "Tags")
    };

    private static readonly object _renderLock = new();
    private static Process? _cycodProcess;
    private static StreamWriter? _cycodStdin;

    private static void StartCycodInteractive()
    {
        if (_cycodProcess != null) return;
        var cycodPath = ResolveCycodPath();
        if (cycodPath == null)
        {
            AddMessage("cycod executable not found. Install with: dotnet tool install -g cycod");
            return;
        }
        var (rawFile, rawArgs) = cycodPath.Value;
        try
        {
            string fileName;
            string arguments;
            if (!OperatingSystem.IsWindows())
            {
                fileName = "script";
                arguments = rawFile == "dotnet" ? $"-q /dev/null dotnet {rawArgs} chat" : $"-q /dev/null {rawFile} chat";
            }
            else
            {
                fileName = rawFile;
                arguments = (string.IsNullOrEmpty(rawArgs) ? string.Empty : rawArgs + " ") + "chat";
            }

            var psi = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                UseShellExecute = false,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                WorkingDirectory = Environment.CurrentDirectory
            };
            psi.Environment["FORCE_COLOR"] = "1";
            psi.Environment["NO_COLOR"] = "0";
            psi.Environment["TERM"] = "xterm-256color";

            _cycodProcess = Process.Start(psi);
            if (_cycodProcess == null)
            {
                AddMessage("Failed to start cycod process");
                return;
            }
            _cycodProcess.EnableRaisingEvents = true;
            _cycodProcess.Exited += (_, __) => AddDebug($"cycod exited: code {_cycodProcess.ExitCode}");
            _cycodStdin = _cycodProcess.StandardInput;
            Task.Run(() => ReadStreamChunked(_cycodProcess!.StandardOutput, false));
            Task.Run(() => ReadStreamChunked(_cycodProcess!.StandardError, true));
            AddMessage("Started cycod interactive session");
            AddDebug($"cycod start: {psi.FileName} {psi.Arguments}");
        }
        catch (Exception ex)
        {
            AddMessage($"Error starting cycod: {ex.Message}");
        }
    }

    private static (string fileName, string args)? ResolveCycodPath()
    {
        var candidates = new List<string> { "cycod", "dotnet tool run cycod" };
        foreach (var c in candidates)
        {
            try
            {
                var fileName = c.StartsWith("dotnet ") ? "dotnet" : c;
                var args = c.StartsWith("dotnet ") ? c.Substring(7) + " --version" : "--version";
                using var test = Process.Start(new ProcessStartInfo
                {
                    FileName = fileName,
                    Arguments = args,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                });
                if (test == null) continue;
                test.WaitForExit(1500);
                if (test.ExitCode == 0)
                {
                    AddDebug($"cycod candidate ok: {c}");
                    return c.StartsWith("dotnet ") ? ("dotnet", c.Substring(7)) : (c, string.Empty);
                }
                AddDebug($"cycod candidate not executable: {c} (exit {test.ExitCode})");
            }
            catch { AddDebug($"cycod candidate failed: {c}"); }
        }
        return null;
    }

    private static void TrySendToCycod(string text)
    {
        try
        {
            if (_cycodStdin == null) return;
            AddDebug($"send: {text}");
            _cycodStdin.WriteLine(text);
            _cycodStdin.Flush();
        }
        catch (Exception ex)
        {
            AddMessage($"Failed to send to cycod: {ex.Message}");
        }
    }

    private static void AddDebug(string debug) { /* debug output suppressed */ }

    private static readonly System.Text.StringBuilder _stdoutLine = new();
    private static readonly System.Text.StringBuilder _stderrLine = new();

    private static void ReadStreamChunked(StreamReader reader, bool isError)
    {
        bool assistantActive = false;
        var assistantBuffer = new System.Text.StringBuilder();
        var scratch = new char[256];
        try
        {
            while (true)
            {
                int read = reader.Read(scratch, 0, scratch.Length);
                if (read <= 0) break;
                int i = 0;
                while (i < read)
                {
                    char c = scratch[i];
                    if (c == '\u001b')
                    {
                        // Skip escape/control sequences
                        if (i + 1 < read && scratch[i + 1] == '[')
                        {
                            int j = i + 2;
                            while (j < read && !IsCsiFinalByte(scratch[j])) j++;
                            i = (j < read) ? j + 1 : read;
                            continue;
                        }
                        else if (i + 1 < read && scratch[i + 1] == ']')
                        {
                            int j = i + 2;
                            while (j < read && scratch[j] != '\u0007')
                            {
                                if (scratch[j] == '\u001b' && j + 1 < read && scratch[j + 1] == '\\') { j += 2; break; }
                                j++;
                            }
                            i = Math.Min(read, j + 1);
                            continue;
                        }
                        else
                        {
                            i += (i + 1 < read) ? 2 : 1;
                            continue;
                        }
                    }
                    else if (c == '\r') { i++; continue; }
                    else if (c == '\n')
                    {
                        if (assistantActive)
                        {
                            assistantBuffer.Append('\n');
                        }
                        i++;
                        continue;
                    }
                    else
                    {
                        // Check for "User: " - if found, flush any active assistant response first
                        if (TryMatchLiteral(scratch, i, read, "User: ", out var advU))
                        {
                            if (assistantActive && assistantBuffer.Length > 0)
                            {
                                ReplaceOrAppendAssistant("Assistant: " + assistantBuffer.ToString());
                                assistantBuffer.Clear();
                                assistantActive = false;
                            }
                            // Reset assistant message tracking
                            lock (_renderLock) { _assistantMessageStartIndex = -1; }
                            // Suppress raw cycod user prompt; we already show the user's input ourselves.
                            i += advU;
                            while (i < read && scratch[i] != '\n' && scratch[i] != '\r') { i++; }
                            continue;
                        }

                        if (!assistantActive)
                        {
                            if (TryMatchLiteral(scratch, i, read, "Assistant: ", out var advA))
                            {
                                assistantActive = true;
                                i += advA;
                                continue;
                            }
                        }
                        if (assistantActive) assistantBuffer.Append(c);
                        i++;
                    }
                }
            }
            if (assistantActive && assistantBuffer.Length > 0)
            {
                ReplaceOrAppendAssistant("Assistant: " + assistantBuffer.ToString());
            }
        }
        catch (Exception ex)
        {
            AddDebug($"stream error: {ex.Message}");
        }
    }

    private static bool TryMatchLiteral(char[] buffer, int index, int length, string literal, out int advance)
    {
        advance = 0;
        if (index + literal.Length > length) return false;
        for (int i = 0; i < literal.Length; i++) if (buffer[index + i] != literal[i]) return false;
        advance = literal.Length;
        return true;
    }

    /* Legacy buffered line processing removed for overwrite-aware streaming */
    private static void ProcessBufferedText(System.Text.StringBuilder sb, bool isError, bool flushAll = false)
    {
        // Deprecated path: should no longer be invoked; keep for fallback.
        var text = sb.ToString();
        int lastNewline = text.LastIndexOf('\n');
        if (lastNewline == -1 && !flushAll) return;
        var emitLength = flushAll ? text.Length : lastNewline + 1;
        var toEmit = text[..emitLength];
        var remainder = text[emitLength..];
        var lines = toEmit.Split('\n');
        foreach (var raw in lines)
        {
            var line = raw.TrimEnd('\r');
            if (line.Length == 0) continue;
            bool isPrompt = line.StartsWith("User:") || line.StartsWith("Assistant:") || line.EndsWith(":");
            if (isError)
            {
                if (isPrompt)
                {
                    int cr = line.LastIndexOf('\r');
                    if (cr >= 0) line = line[(cr + 1)..];
                    AddParsedStyledMessage(line);
                    AddDebug($"prompt(recv): {line}");
                }
                else
                {
                    AddDebug($"stderr: {line}");
                }
            }
            else
            {
                // Option A: handle carriage return overwrite semantics
                int cr = line.LastIndexOf('\r');
                if (cr >= 0) line = line[(cr + 1)..];
                AddParsedStyledMessage(line);
                AddDebug($"recv: {line}");
            }
        }
        sb.Clear();
        sb.Append(remainder);
    }

    // Track if we're waiting for a response (showing dots)
    private static bool _waitingForResponse = false;
    private static System.Threading.Timer? _dotTimer;
    private static int _dotCount = 0;

    private static void AddUserMessage(string text)
    {
        lock (_renderLock)
        {
            if (_backend == null) return;

            // Step 1: Clear everything EXCEPT the upper separator line
            // Use same calculation as Render() for consistency
            var size = _backend.GetSize();
            var height = size.Height;

            // Warp terminal adjustment (must match Render())
            var isWarp = Environment.GetEnvironmentVariable("TERM_PROGRAM")?.Contains("WarpTerminal") == true
                      || Environment.GetEnvironmentVariable("WARP_SESSION_ID") != null
                      || Environment.GetEnvironmentVariable("WARP_IS_LOCAL_SHELL_SESSION") != null;
            if (isWarp && height > 2) height -= 2;

            var inputHeight = Math.Max(1, _inputState.Lines.Count);
            var reserved = 1 + inputHeight + 1 + 1 + 1; // upper_sep + input + lower_sep + status + padding
            var upperSepY = height - reserved; // 0-indexed row of upper separator
            var inputY = upperSepY + 1; // 0-indexed row of input line
            // Move to input row (convert to 1-indexed for ANSI) and clear from there
            _backend.WriteRaw($"\u001b[{inputY + 1};1H"); // +1 for 1-indexed ANSI
            _backend.WriteRaw("\u001b[J"); // Clear from cursor to end of screen

            // Step 2: Print user's input (dimmed/grey)
            Console.WriteLine($"\u001b[90m  {text}\u001b[0m"); // Grey color, 2-space margin

            // Step 3: Start showing dots
            _waitingForResponse = true;
            _dotCount = 0;
            Console.Write("  "); // 2-space margin for dots

            // Start a timer to add dots every second
            _dotTimer = new System.Threading.Timer(_ =>
            {
                if (_waitingForResponse && _dotCount < 10)
                {
                    Console.Write(".");
                    _dotCount++;
                }
            }, null, 1000, 1000);
        }
    }

    private static void AddParsedStyledMessage(string raw)
    {
        lock (_renderLock)
        {
            var styled = new StyledLine();
            foreach (var seg in ParseAnsiSegments(raw)) styled.Segments.Add(seg);
            _styledMessages.Add(styled);
            _messages.Add(raw);
            // Reset assistant message tracking when adding a new message (typically user input)
            _assistantMessageStartIndex = -1;
            Render();
        }
    }

    private static void ReplaceOrAppendAssistant(string raw)
    {
        lock (_renderLock)
        {
            // Stop the dot timer and erase the dots line
            if (_waitingForResponse)
            {
                _waitingForResponse = false;
                _dotTimer?.Dispose();
                _dotTimer = null;

                // Erase the dots line: move to beginning of current line and clear it
                Console.Write("\u001b[2K"); // Clear entire current line
                Console.Write("\r"); // Move cursor to beginning of line
            }

            // Strip "Assistant: " prefix if present
            var content = raw.StartsWith("Assistant: ") ? raw.Substring(11) : raw;

            // Use StreamingMarkdownRenderer to format the content
            var renderer = new StreamingMarkdownRenderer();
            var outputBuffer = new System.Text.StringBuilder();
            renderer.OnOutput += (text) => outputBuffer.Append(text);

            // Process the content through the markdown renderer
            renderer.AppendChunk(content);
            renderer.Flush();

            // Get the formatted output and print it
            var formattedContent = outputBuffer.ToString();
            foreach (var line in formattedContent.Split('\n'))
            {
                Console.WriteLine($"  {line}"); // 2-space margin
            }

            // Print blank lines to push content into scrollback, then Render() draws input area
            // This ensures our content is above the viewport and Render() doesn't overwrite it
            var size = _backend?.GetSize();
            var height = size?.Height ?? 24;
            var reserved = 5; // upper_sep + input + lower_sep + status + padding
            for (int i = 0; i < reserved; i++)
            {
                Console.WriteLine();
            }

            // Now call Render() to draw the input area at the bottom
            Render();
        }
    }

    private static void AddMessage(string message)
    {
        lock (_renderLock)
        {
            _messages.Add(message);
            Render();
        }
    }

    public static void Run(CancellationToken cancellationToken, string? gitBranch = null, string? currentLLM = null)
    {
        // Cache the passed-in values
        _cachedGitBranch = gitBranch;
        _cachedCurrentLLM = currentLLM;

        _inputState.OnSubmit += text =>
        {
            var trimmed = text.Trim();
            if (trimmed == "/exit")
            {
                _shouldExit = true;
                TrySendToCycod("exit");
            }
            else
            {
                AddUserMessage(text);
                TrySendToCycod(text);
            }
        };

        _backend = CreateBackend();

        // Don't use alternate screen buffer - we want native terminal scrolling
        // Content is "committed" to terminal scrollback, input/status float at bottom
        _backend.HideCursor();
        _terminal = new Terminal(_backend, new LoggingContext(null));

        StartCycodInteractive();

        try
        {
            Render();
            InputLoop(cancellationToken);
        }
        finally
        {
            // Clear the input/status area before exiting
            ClearInputStatusArea();
            _backend?.ShowCursor();
            _terminal?.Dispose();
            _backend?.Dispose();
        }
    }

    /// <summary>
    /// Calculates the height of the input/status area (separators + input + status + padding)
    /// </summary>
    private static int GetInputStatusAreaHeight()
    {
        var inputHeight = Math.Max(1, _inputState.Lines.Count);
        return 1 + inputHeight + 1 + 1 + 2; // upper sep + input + lower sep + status + 2 padding lines
    }

    /// <summary>
    /// Clears the input/status area using absolute cursor positioning
    /// </summary>
    private static void ClearInputStatusArea()
    {
        if (_backend == null) return;
        var size = _backend.GetSize();
        var areaHeight = GetInputStatusAreaHeight();

        // Calculate the row where input/status area starts (1-indexed for ANSI)
        var startRow = size.Height - areaHeight + 1;

        // Move cursor to start of input/status area (absolute positioning)
        _backend.WriteRaw($"\u001b[{startRow};1H"); // Move to row, column 1
        _backend.WriteRaw("\u001b[J"); // Clear from cursor to end of screen
    }

    /// <summary>
    /// Commits finalized content to the terminal scrollback, then redraws input/status
    /// </summary>
    private static void CommitToTerminal(string content, Style style)
    {
        if (_backend == null) return;
        var size = _backend.GetSize();
        var areaHeight = GetInputStatusAreaHeight();

        // Calculate the row where scrollable area ends (1-indexed for ANSI)
        var scrollEndRow = size.Height - areaHeight;

        // Set scroll region to exclude input/status area
        _backend.WriteRaw($"\u001b[1;{scrollEndRow}r"); // Set scroll region rows 1 to scrollEndRow

        // Move cursor to bottom of scroll region
        _backend.WriteRaw($"\u001b[{scrollEndRow};1H");

        // Print the content to terminal (scrolls within the region)
        var ansiStyle = StyleToAnsi(style);
        foreach (var line in content.Split('\n'))
        {
            Console.WriteLine($"  {ansiStyle}{line}\u001b[0m"); // 2-space left margin
        }

        // Reset scroll region to full screen
        _backend.WriteRaw("\u001b[r");

        // Redraw input/status area (it wasn't affected by scrolling)
        Render();
    }

    private static ITerminalBackend CreateBackend() => OperatingSystem.IsWindows()
        ? new CycoTui.Backend.Windows.WindowsTerminalBackend()
        : new CycoTui.Backend.Unix.UnixTerminalBackend();

    public static string? GetGitBranch()
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "git",
                Arguments = "branch --show-current",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                WorkingDirectory = Environment.CurrentDirectory
            };

            using var process = Process.Start(psi);
            if (process == null) return null;

            process.WaitForExit(500);
            if (process.ExitCode != 0) return null;

            var branch = process.StandardOutput.ReadToEnd().Trim();
            return string.IsNullOrEmpty(branch) ? null : branch;
        }
        catch
        {
            return null;
        }
    }

    public static string? GetCurrentLLM()
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "cycod",
                Arguments = "config list",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                WorkingDirectory = Environment.CurrentDirectory
            };

            using var process = Process.Start(psi);
            if (process == null) return null;

            process.WaitForExit(1000);
            if (process.ExitCode != 0) return null;

            var output = process.StandardOutput.ReadToEnd();

            // Parse the output to find App.PreferredProvider and model name
            var provider = ParseConfigValue(output, "App.PreferredProvider");
            if (string.IsNullOrEmpty(provider)) return null;

            // Based on provider, get the corresponding model name
            var modelKey = GetModelKeyForProvider(provider);
            if (modelKey == null) return null;

            var modelName = ParseConfigValue(output, modelKey);
            if (string.IsNullOrEmpty(modelName)) return null;

            return $"{provider}:{modelName}";
        }
        catch
        {
            return null;
        }
    }

    private static string? ParseConfigValue(string configOutput, string key)
    {
        var lines = configOutput.Split('\n');
        foreach (var line in lines)
        {
            var trimmed = line.Trim();
            if (trimmed.StartsWith($"{key}:", StringComparison.OrdinalIgnoreCase))
            {
                var colonIndex = trimmed.IndexOf(':');
                if (colonIndex >= 0 && colonIndex + 1 < trimmed.Length)
                {
                    return trimmed.Substring(colonIndex + 1).Trim();
                }
            }
        }
        return null;
    }

    private static string? GetModelKeyForProvider(string provider)
    {
        var lowerProvider = provider.ToLowerInvariant();
        return lowerProvider switch
        {
            "copilot" or "copilot-github" => "Copilot.ModelName",
            "anthropic" => "Anthropic.ModelName",
            "openai" => "OpenAI.ChatModelName",
            "google" or "gemini" or "google-gemini" => "Google.Gemini.ModelId",
            "grok" or "x.ai" => "Grok.ModelName",
            "aws" or "bedrock" or "aws-bedrock" => "AWS.Bedrock.ModelId",
            "azure" or "azure-openai" => "Azure.OpenAI.ChatDeployment",
            _ => null
        };
    }

    private static void InputLoop(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested && !_shouldExit)
        {
            if (_cycodProcess != null && _cycodProcess.HasExited)
            {
                AddMessage($"cycod process exited (code {_cycodProcess.ExitCode})");
                _shouldExit = true;
                break;
            }

            var key = Console.ReadKey(intercept: true);

            if (key.Key == ConsoleKey.Escape)
            {
                if (_completionState.IsActive)
                {
                    _completionState = _completionState.Deactivate();
                    Render();
                    continue;
                }
                break;
            }

            if (key.Key == ConsoleKey.Q && (key.Modifiers & ConsoleModifiers.Control) != 0) break;

            if (_completionState.IsActive)
            {
                var handled = HandleCompletionKey(key);
                if (handled)
                {
                    Render();
                    continue;
                }
            }

            // Note: Page Up/Down not needed - native terminal scrolling handles history

            if (_inputState.HandleKey(key))
            {
                UpdateCompletionState();
                // Don't render while waiting for response - we're showing dots
                if (!_waitingForResponse)
                {
                    Render();
                }
            }
        }
    }

    private static bool HandleCompletionKey(ConsoleKeyInfo key)
    {
        if (key.Key == ConsoleKey.UpArrow || (key.Key == ConsoleKey.P && (key.Modifiers & ConsoleModifiers.Control) != 0))
        {
            _completionState = _completionState.SelectPrevious();
            return true;
        }
        if (key.Key == ConsoleKey.DownArrow || (key.Key == ConsoleKey.N && (key.Modifiers & ConsoleModifiers.Control) != 0))
        {
            _completionState = _completionState.SelectNext();
            return true;
        }
        if (key.Key == ConsoleKey.Enter || key.Key == ConsoleKey.Tab)
        {
            var selectedItem = _completionState.GetSelectedItem();
            if (selectedItem != null)
            {
                InsertSelectedFile(selectedItem);
                _completionState = _completionState.Deactivate();
                return true;
            }
        }
        return false;
    }

    private static void UpdateCompletionState()
    {
        if (_inputState.Lines.Count == 0) return;
        var currentLine = _inputState.Lines[_inputState.CursorLineIndex];
        var cursorColumn = _inputState.CursorColumn;
        var triggerChars = _completionTriggers.Select(t => t.TriggerChar);

        var query = CompletionHelper.DetectCompletionQuery(currentLine, cursorColumn, triggerChars, out var triggerColumn, out var foundTriggerChar);

        if (query != null)
        {
            var trigger = _completionTriggers.FirstOrDefault(t => t.TriggerChar == foundTriggerChar);
            if (trigger.Provider == null)
            {
                _completionState = _completionState.Deactivate();
                return;
            }
            if (!_completionState.IsActive || _completionState.TriggerChar != foundTriggerChar)
            {
                try
                {
                    var items = trigger.Provider().GetAwaiter().GetResult();
                    _completionState = _completionState.Activate(items, _inputState.CursorLineIndex, triggerColumn, foundTriggerChar, trigger.Label);
                }
                catch (Exception ex)
                {
                    _completionState = _completionState.ActivateWithError($"Error loading items: {ex.Message}", _inputState.CursorLineIndex, triggerColumn, foundTriggerChar, trigger.Label);
                }
            }
            if (!_completionState.IsError) _completionState = _completionState.UpdateQuery(query);
        }
        else if (_completionState.IsActive) _completionState = _completionState.Deactivate();
    }

    private static void InsertSelectedFile(string selectedFile)
    {
        var currentLine = _inputState.Lines[_inputState.CursorLineIndex];
        var cursorColumn = _inputState.CursorColumn;
        var newLine = CompletionHelper.InsertCompletion(currentLine, _completionState.TriggerColumn, cursorColumn, _completionState.TriggerChar, selectedFile, out var newCursorColumn);
        _inputState.SetLine(_inputState.CursorLineIndex, newLine, newCursorColumn);
    }

    private static void Render()
    {
        if (_backend == null || _terminal == null) return;
        var size = _backend.GetSize();

        // Note: Scrollback buffer writing via Console.WriteLine() has been disabled
        // because it conflicts with Terminal.Draw() rendering. Messages are displayed
        // in the messages area rendered by Terminal.Draw() instead.
        // TODO: Re-implement scrollback support using a scroll region or alternate buffer

        _terminal.Draw(frame =>
        {
            var width = size.Width;
            var height = size.Height;

            // Warp terminal detection - check for common Warp environment variables
            var isWarp = Environment.GetEnvironmentVariable("TERM_PROGRAM")?.Contains("WarpTerminal") == true
                      || Environment.GetEnvironmentVariable("WARP_SESSION_ID") != null
                      || Environment.GetEnvironmentVariable("WARP_IS_LOCAL_SHELL_SESSION") != null;

            // Warp seems to report extra rows that aren't actually visible
            // Subtract 2 to ensure proper spacing at bottom
            if (isWarp && height > 2) height -= 2;

            // Left margin for better readability
            const int leftMargin = 2;
            var contentWidth = width - leftMargin;

            // Layout calculation (from bottom up, like Claude Code)
            var bottomPaddingHeight = 1; // Empty line at very bottom
            var statusLineHeight = 1;
            var lowerSeparatorHeight = 1;
            var inputHeight = Math.Max(1, _inputState.Lines.Count);
            var upperSeparatorHeight = 1;
            var reserved = bottomPaddingHeight + statusLineHeight + lowerSeparatorHeight + inputHeight + upperSeparatorHeight;
            var messagesHeight = Math.Max(0, height - reserved);

            // NOTE: We do NOT render a "messages area" here.
            // All content (user messages and LLM responses) is committed to terminal scrollback.
            // We only render the input/status area at the bottom.

            // Calculate where input/status area starts
            var upperSepY = height - reserved;
            for (var r = upperSepY; r < height; r++)
            {
                frame.WriteString(0, r, new string(' ', width), Style.Empty);
            }

            // Upper separator (above input) - with left margin
            frame.WriteString(leftMargin, upperSepY, new string('─', contentWidth), Style.Empty.Add(TextModifier.Dim));

            // Input area - with left margin and prompt
            var inputY = upperSepY + 1;
            // Draw prompt character
            frame.WriteString(leftMargin, inputY, "> ", Style.Empty);
            // Input area starts after prompt
            var inputRect = new Rect(leftMargin + 2, inputY, contentWidth - 2, inputHeight);
            new MultiLineInputWidget().WithStyles(Style.Empty, Style.Empty.Add(TextModifier.Invert)).Render(frame, inputRect, _inputState);

            // Lower separator (below input, above status) - with left margin
            var lowerSepY = inputY + inputHeight;
            frame.WriteString(leftMargin, lowerSepY, new string('─', contentWidth), Style.Empty.Add(TextModifier.Dim));

            // Status line (one row above the bottom)
            var statusY = lowerSepY + 1;
            var currentDir = Environment.CurrentDirectory;
            var gitBranch = _cachedGitBranch;
            var currentLLM = _cachedCurrentLLM;

            // Static colors: model=lightblue, branch=salmon, path=lightgreen
            var llmColor = Color.Rgb(173, 216, 230);    // Light blue
            var branchColor = Color.Rgb(250, 128, 114); // Salmon
            var pathColor = Color.Rgb(144, 238, 144);   // Light green

            // Build status parts in order: llm - branch - path
            var parts = new List<(string text, Color color)>();
            if (currentLLM != null) parts.Add((currentLLM, llmColor));
            if (gitBranch != null) parts.Add((gitBranch, branchColor));
            parts.Add((currentDir, pathColor));

            // Calculate total length with separators
            var separator = " - ";
            var totalLength = parts.Sum(p => p.text.Length) + (separator.Length * (parts.Count - 1));

            // Truncate path if needed
            if (totalLength > contentWidth && parts.Count > 0)
            {
                var pathIndex = parts.Count - 1;
                var pathPart = parts[pathIndex].text;
                var otherPartsLength = parts.Take(pathIndex).Sum(p => p.text.Length) + (separator.Length * (parts.Count - 1));
                var availableForPath = contentWidth - otherPartsLength;

                if (availableForPath > 10)
                {
                    var truncatedPath = "..." + pathPart.Substring(pathPart.Length - (availableForPath - 3));
                    parts[pathIndex] = (truncatedPath, pathColor);
                }
                else if (availableForPath > 3)
                {
                    parts[pathIndex] = ("...", pathColor);
                }
            }

            // Render each part with its color - with left margin
            int statusCol = leftMargin;
            for (int i = 0; i < parts.Count; i++)
            {
                if (statusCol >= width) break;

                var (text, color) = parts[i];
                var style = Style.Empty.WithForeground(color);
                var displayText = text.Length > width - statusCol ? text.Substring(0, width - statusCol) : text;
                frame.WriteString(statusCol, statusY, displayText, style);
                statusCol += displayText.Length;

                // Add separator if not last part
                if (i < parts.Count - 1 && statusCol + separator.Length <= width)
                {
                    frame.WriteString(statusCol, statusY, separator, style);
                    statusCol += separator.Length;
                }
            }

            // Fill remaining space
            if (statusCol < width)
            {
                frame.WriteString(statusCol, statusY, new string(' ', width - statusCol), Style.Empty);
            }

            // Empty line at the very bottom
            var bottomPaddingY = statusY + 1;
            frame.WriteString(0, bottomPaddingY, new string(' ', width), Style.Empty);

            // Warp-specific: Write an additional empty line using backend directly
            if (isWarp && bottomPaddingY + 1 < size.Height)
            {
                _backend?.WriteRaw($"\u001b[{bottomPaddingY + 2};1H"); // Move to row below padding
                _backend?.WriteRaw(new string(' ', width)); // Write spaces
            }

            // Completion popup
            if (_completionState.IsActive)
            {
                var popupWidget = CompletionPopupWidget.Create()
                    .WithMaxVisibleItems(10)
                    .WithStyles(border: Style.Empty.WithForeground(Color.Cyan), title: Style.Empty.WithForeground(Color.Cyan).Add(TextModifier.Bold), selected: Style.Empty.Add(TextModifier.Invert), item: Style.Empty);
                var popupRect = popupWidget.CalculatePopupRect(inputRect, _completionState, height);
                popupWidget.Render(frame, popupRect, _completionState);
            }
        });
    }

    private static string StyleToAnsi(Style style)
    {
        var sb = new System.Text.StringBuilder();

        // Handle foreground color
        if (style.Foreground != null)
        {
            var fg = style.Foreground.Value;
            if (fg.R >= 0 && fg.G >= 0 && fg.B >= 0)
            {
                sb.Append($"\u001b[38;2;{fg.R};{fg.G};{fg.B}m");
            }
        }

        // Handle background color
        if (style.Background != null)
        {
            var bg = style.Background.Value;
            if (bg.R >= 0 && bg.G >= 0 && bg.B >= 0)
            {
                sb.Append($"\u001b[48;2;{bg.R};{bg.G};{bg.B}m");
            }
        }

        // Handle text modifiers (use AddModifier property)
        if (style.AddModifier.HasFlag(TextModifier.Bold))
            sb.Append("\u001b[1m");
        if (style.AddModifier.HasFlag(TextModifier.Dim))
            sb.Append("\u001b[2m");
        if (style.AddModifier.HasFlag(TextModifier.Italic))
            sb.Append("\u001b[3m");
        if (style.AddModifier.HasFlag(TextModifier.Underline))
            sb.Append("\u001b[4m");
        if (style.AddModifier.HasFlag(TextModifier.Invert))
            sb.Append("\u001b[7m");

        return sb.ToString();
    }
}

