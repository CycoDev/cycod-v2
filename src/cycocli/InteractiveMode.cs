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
    private static readonly MultiLineInputState _inputState = new();
    private static CompletionState _completionState = CompletionState.CreateInactive();

    private static readonly List<CompletionTrigger> _completionTriggers = new()
    {
        new CompletionTrigger(
            '@',
            async () =>
            {
                var workspaceRoot = Environment.CurrentDirectory;
                return await Task.Run(() => WorkspaceFileScanner.ScanFiles(workspaceRoot, maxFiles: 1000));
            },
            "Files"
        ),
        new CompletionTrigger(
            '/',
            async () =>
            {
                await Task.CompletedTask;
                return new List<string> { "exit" };
            },
            "Commands"
        ),
        new CompletionTrigger(
            '#',
            async () =>
            {
                await Task.CompletedTask;
                return new List<string>
                {
                    "bug", "feature", "enhancement", "documentation",
                    "refactor", "test", "performance", "security"
                };
            },
            "Tags"
        )
    };

    private static readonly object _renderLock = new();
    private static Process? _cycodProcess;
    private static StreamWriter? _cycodStdin;

    // --- Cycod interactive helpers ---
    private static void StartCycodInteractive()
    {
        if (_cycodProcess != null) return;
        var cycodPath = ResolveCycodPath();
        if (cycodPath == null)
        {
            AddMessage("cycod executable not found. Install with: dotnet tool install -g cycod");
            return;
        }
        var (fileName, args) = cycodPath.Value;
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = (string.IsNullOrEmpty(args) ? string.Empty : args + " ") + "chat",
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
            _cycodStdin = _cycodProcess.StandardInput;
            Task.Run(() => ReadOutputLoop(_cycodProcess!.StandardOutput));
            Task.Run(() => ReadOutputLoop(_cycodProcess!.StandardError));
            AddMessage($"Started cycod interactive: {psi.FileName} {psi.Arguments}");
        }
        catch (Exception ex)
        {
            AddMessage($"Error starting cycod: {ex.Message}");
        }
    }

    private static (string fileName, string args)? ResolveCycodPath()
    {
        var candidates = new List<string>
        {
            "cycod",
            "dotnet tool run cycod"
        };
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
                    return c.StartsWith("dotnet ") ? ("dotnet", c.Substring(7)) : (c, string.Empty);
                }
            }
            catch { }
        }
        return null;
    }

    private static void TrySendToCycod(string text)
    {
        try
        {
            if (_cycodStdin == null) return;
            _cycodStdin.WriteLine(text);
            _cycodStdin.Flush();
        }
        catch (Exception ex)
        {
            AddMessage($"Failed to send to cycod: {ex.Message}");
        }
    }

    private static void ReadOutputLoop(StreamReader reader)
    {
        try
        {
            string? line;
            while ((line = reader.ReadLine()) != null)
            {
                if (string.IsNullOrEmpty(line)) continue;
                AddMessage(line);
            }
        }
        catch (Exception ex)
        {
            AddMessage($"Output read error: {ex.Message}");
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

    // --- Entry point ---
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
                AddMessage($">> {text}");
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

    private static ITerminalBackend CreateBackend()
    {
        if (OperatingSystem.IsWindows()) return new CycoTui.Backend.Windows.WindowsTerminalBackend();
        return new CycoTui.Backend.Unix.UnixTerminalBackend();
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

            if (key.Key == ConsoleKey.Q && (key.Modifiers & ConsoleModifiers.Control) != 0)
            {
                break;
            }

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

        var query = CompletionHelper.DetectCompletionQuery(
            currentLine,
            cursorColumn,
            triggerChars,
            out var triggerColumn,
            out var foundTriggerChar);

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
            if (!_completionState.IsError)
            {
                _completionState = _completionState.UpdateQuery(query);
            }
        }
        else if (_completionState.IsActive)
        {
            _completionState = _completionState.Deactivate();
        }
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

            var visibleMessages = _messages.TakeLast(contentHeight).ToList();
            for (var i = 0; i < contentHeight; i++)
            {
                var line = i < visibleMessages.Count ? visibleMessages[i] : string.Empty;
                if (line.Length > width) line = line[..width];
                frame.WriteString(0, i, line.PadRight(width), Style.Empty);
            }

            var sepAboveInputY = contentHeight;
            frame.WriteString(0, sepAboveInputY, new string('─', width), Style.Empty.Add(TextModifier.Dim));

            var inputRect = new Rect(0, sepAboveInputY + 1, width, inputHeight);
            new MultiLineInputWidget()
                .WithStyles(Style.Empty, Style.Empty.Add(TextModifier.Invert))
                .Render(frame, inputRect, _inputState);

            var sepBelowInputY = sepAboveInputY + 1 + inputHeight;
            frame.WriteString(0, sepBelowInputY, new string('─', width), Style.Empty.Add(TextModifier.Dim));

            if (_completionState.IsActive)
            {
                var popupWidget = CompletionPopupWidget.Create()
                    .WithMaxVisibleItems(10)
                    .WithStyles(
                        border: Style.Empty.WithForeground(Color.Cyan),
                        title: Style.Empty.WithForeground(Color.Cyan).Add(TextModifier.Bold),
                        selected: Style.Empty.Add(TextModifier.Invert),
                        item: Style.Empty);
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
