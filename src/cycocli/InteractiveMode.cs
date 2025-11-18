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

namespace CycoTui.Sample;

internal static class InteractiveMode
{
    private static bool _shouldExit;
    private static ITerminalBackend? _backend;
    private static Terminal? _terminal;

    private static readonly List<string> _messages = new();
    // Debug area removed; retaining stub methods only
    private static readonly MultiLineInputState _inputState = new();
    private static CompletionState _completionState = CompletionState.CreateInactive();

    // ANSI styled lines
    private class StyledLine
    {
        public List<(string Text, Style Style)> Segments { get; } = new();
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
                            ReplaceOrAppendAssistant("Assistant: " + assistantBuffer.ToString());
                            assistantBuffer.Clear();
                            assistantActive = false;
                        }
                        i++;
                        continue;
                    }
                    else
                    {
                        if (!assistantActive)
                        {
                            if (TryMatchLiteral(scratch, i, read, "Assistant: ", out var advA))
                            {
                                assistantActive = true;
                                i += advA;
                                continue;
                            }
                            if (TryMatchLiteral(scratch, i, read, "User: ", out var advU))
                            {
                                // Suppress raw cycod user prompt; we already show the user's input ourselves.
                                i += advU;
                                while (i < read && scratch[i] != '\n' && scratch[i] != '\r') { i++; }
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

    private static void AddParsedStyledMessage(string raw)
    {
        lock (_renderLock)
        {
            var styled = new StyledLine();
            foreach (var seg in ParseAnsiSegments(raw)) styled.Segments.Add(seg);
            _styledMessages.Add(styled);
            _messages.Add(raw);
            Render();
        }
    }

    private static void ReplaceOrAppendAssistant(string raw)
    {
        lock (_renderLock)
        {
            if (_messages.Count > 0 && _messages[^1].StartsWith("Assistant:"))
            {
                // Replace last assistant message
                _messages[^1] = raw;
                var styled = _styledMessages[^1];
                styled.Segments.Clear();
                foreach (var seg in ParseAnsiSegments(raw)) styled.Segments.Add(seg);
            }
            else
            {
                var styled = new StyledLine();
                foreach (var seg in ParseAnsiSegments(raw)) styled.Segments.Add(seg);
                _styledMessages.Add(styled);
                _messages.Add(raw);
            }
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

    public static void Run(CancellationToken cancellationToken)
    {
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
                AddParsedStyledMessage($"User: {text}");
                TrySendToCycod(text);
            }
        };

        StartCycodInteractive();

        _backend = CreateBackend();
        _backend.Clear();
        _backend.HideCursor();
        _terminal = new Terminal(_backend, new LoggingContext(null));
        try
        {
            Render();
            InputLoop(cancellationToken);
        }
        finally
        {
            _terminal?.Dispose();
            _backend?.Dispose();
        }
    }

    private static ITerminalBackend CreateBackend() => OperatingSystem.IsWindows()
        ? new CycoTui.Backend.Windows.WindowsTerminalBackend()
        : new CycoTui.Backend.Unix.UnixTerminalBackend();

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

            if (_inputState.HandleKey(key))
            {
                UpdateCompletionState();
                Render();
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
        _terminal.Draw(frame =>
        {
            var width = size.Width;
            var height = size.Height;
            var statusLineHeight = 1;
            var inputHeight = Math.Max(1, _inputState.Lines.Count);
            var separators = 2;
            var reserved = inputHeight + separators + statusLineHeight;
            var contentHeight = Math.Max(0, height - reserved);

            var messagesHeight = contentHeight;
            var styledVisible = _styledMessages.TakeLast(messagesHeight).ToList();
            int messageCount = styledVisible.Count;
            int startRow = Math.Max(0, messagesHeight - messageCount);
            // Clear area
            for (var r = 0; r < messagesHeight; r++) frame.WriteString(0, r, new string(' ', width), Style.Empty);
            // Render bottom-aligned
            for (var row = 0; row < messageCount; row++)
            {
                var styledLine = styledVisible[row];
                int col = 0;
                foreach (var (text, style) in styledLine.Segments)
                {
                    if (col >= width) break;
                    var remaining = width - col;
                    var segmentText = text.Length > remaining ? text[..remaining] : text;
                    frame.WriteString(col, startRow + row, segmentText, style);
                    col += segmentText.Length;
                }
                if (col < width) frame.WriteString(col, startRow + row, new string(' ', width - col), Style.Empty);
            }

            var sepAboveInputY = contentHeight;
            frame.WriteString(0, sepAboveInputY, new string('─', width), Style.Empty.Add(TextModifier.Dim));

            var inputRect = new Rect(0, sepAboveInputY + 1, width, inputHeight);
            new MultiLineInputWidget().WithStyles(Style.Empty, Style.Empty.Add(TextModifier.Invert)).Render(frame, inputRect, _inputState);

            var sepBelowInputY = sepAboveInputY + 1 + inputHeight;
            frame.WriteString(0, sepBelowInputY, new string('─', width), Style.Empty.Add(TextModifier.Dim));

            if (_completionState.IsActive)
            {
                var popupWidget = CompletionPopupWidget.Create()
                    .WithMaxVisibleItems(10)
                    .WithStyles(border: Style.Empty.WithForeground(Color.Cyan), title: Style.Empty.WithForeground(Color.Cyan).Add(TextModifier.Bold), selected: Style.Empty.Add(TextModifier.Invert), item: Style.Empty);
                var popupRect = popupWidget.CalculatePopupRect(inputRect, _completionState, height);
                popupWidget.Render(frame, popupRect, _completionState);
            }

            var wordNav = OperatingSystem.IsWindows() ? "Alt+←→" : "Cmd+←→";
            var status = $"Messages: {_messages.Count}  Line: {_inputState.CursorLineIndex + 1}/{_inputState.Lines.Count}  Col: {_inputState.CursorColumn}  ←→↑↓=Move  {wordNav}=Word  Home/End  Enter=Submit  Ctrl+J=NewLine  Esc=Quit";
            if (status.Length > width) status = status[..width];
            frame.WriteString(0, sepBelowInputY + 1, status.PadRight(width), Style.Empty.Add(TextModifier.Bold));
        });
    }
}

