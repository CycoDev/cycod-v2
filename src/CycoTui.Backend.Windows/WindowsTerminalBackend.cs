using System;
// TODO: Capability probing (underline color, scrolling regions, true color); buffering optimization

using System.Collections.Generic;
using System.Text;
using System.Threading;
using CycoTui.Core.Backend;
using CycoTui.Core.Style;

namespace CycoTui.Backend.Windows;

/// <summary>
/// Minimal Windows console backend implementation. Phase-1: basic raw write, size, cursor show/hide.
/// </summary>
public sealed class WindowsTerminalBackend : ITerminalBackend
{
    private BackendCapabilities ProbeCapabilities()
    {
        // Simple heuristic probing placeholder
        // TODO: real probing (e.g., check WT_SESSION for Windows Terminal truecolor)
        return BackendCapabilities.Minimal;
    }
    private bool _disposed;
    public BackendCapabilities Capabilities { get; }

    public WindowsTerminalBackend()
    {
        Capabilities = ProbeCapabilities();

    }

    public void Draw(IEnumerable<CellUpdate> updates)
    {
        foreach (var u in updates)
        {
            if (u.X < 0 || u.Y < 0) continue; // style-only placeholder not used here
            TrySetCursor(u.X, u.Y);
            Console.Write(u.Cell.Grapheme);
        }
    }

    private static void TrySetCursor(int x, int y)
    {
        try
        {
            Console.SetCursorPosition(x, y);
        }
        catch { /* Ignore out-of-range */ }
    }

    public void WriteRaw(string sequence)
    {
        Console.Write(sequence);
    }

    public void Flush() { /* Console.Write is immediate */ }
    public void HideCursor() { try { Console.CursorVisible = false; } catch { } }
    public void ShowCursor() { try { Console.CursorVisible = true; } catch { } }
    public Position GetCursorPosition() => new(Console.CursorLeft, Console.CursorTop);
    public void SetCursorPosition(Position position) => TrySetCursor(position.X, position.Y);
    public void Clear(ClearType type = ClearType.All)
    {
        if (type == ClearType.All)
        {
            Console.Clear();
        }
    }
    public void AppendLines(int count = 1)
    {
        for (int i = 0; i < count; i++) Console.WriteLine();
    }
    public Size GetSize() => new(Console.WindowWidth, Console.WindowHeight);
    public WindowSize GetWindowSize() => new(new Size(Console.WindowWidth, Console.WindowHeight), Size.Empty);
    public void ScrollRegionUp(Range region, int lineCount) { /* no-op phase-1 */ }
    public void ScrollRegionDown(Range region, int lineCount) { /* no-op phase-1 */ }

    public (Color? Foreground, Color? Background) QueryDefaultColors()
    {
        // Modern Windows Terminal supports OSC 10/11, but legacy console does not
        try
        {
            var fg = QueryOscColor(10);
            var bg = QueryOscColor(11);
            return (fg, bg);
        }
        catch
        {
            return (null, null);
        }
    }

    private Color? QueryOscColor(int oscCode)
    {
        try
        {
            // Send OSC query: ESC ] {code} ; ? BEL
            var query = $"\u001b]{oscCode};?\u0007";
            Console.Write(query);
            Console.Out.Flush();

            // Try to read response with timeout
            var response = ReadOscResponse(timeoutMs: 100);
            if (string.IsNullOrEmpty(response))
                return null;

            // Parse response format: ESC ] {code} ; rgb:RRRR/GGGG/BBBB BEL or ESC ] {code} ; rgb:RRRR/GGGG/BBBB ESC \
            return ParseOscRgbResponse(response);
        }
        catch
        {
            return null;
        }
    }

    private string? ReadOscResponse(int timeoutMs)
    {
        var sb = new StringBuilder();
        var startTime = DateTime.UtcNow;

        try
        {
            var stdin = Console.OpenStandardInput();
            var buffer = new byte[1];

            // Read bytes directly from stdin until we hit BEL or timeout
            while ((DateTime.UtcNow - startTime).TotalMilliseconds < timeoutMs)
            {
                // Check if data is available to read
                if (stdin.CanRead)
                {
                    // Try non-blocking read with timeout
                    var readTask = System.Threading.Tasks.Task.Run(() => stdin.Read(buffer, 0, 1));
                    if (readTask.Wait(5))
                    {
                        if (readTask.Result > 0)
                        {
                            char c = (char)buffer[0];
                            sb.Append(c);

                            // Check for BEL terminator
                            if (c == '\u0007')
                                break;

                            // Check for ST terminator (ESC \)
                            if (sb.Length >= 2 && sb[sb.Length - 2] == '\u001b' && sb[sb.Length - 1] == '\\')
                                break;
                        }
                    }
                }
                else
                {
                    Thread.Sleep(5);
                }
            }

            return sb.ToString();
        }
        catch
        {
            return null;
        }
    }

    private Color? ParseOscRgbResponse(string response)
    {
        // Expected format: ESC ] code ; rgb:RRRR/GGGG/BBBB BEL
        // or with the escape sequence still in the buffer

        // Find "rgb:" in the response
        var rgbIndex = response.IndexOf("rgb:", StringComparison.Ordinal);
        if (rgbIndex == -1)
            return null;

        // Extract the RGB values after "rgb:"
        var rgbPart = response.Substring(rgbIndex + 4);

        // Remove any trailing BEL or ESC \ sequences
        rgbPart = rgbPart.TrimEnd('\u0007', '\\', '\u001b');

        // Split by '/' to get R/G/B components
        var parts = rgbPart.Split('/');
        if (parts.Length != 3)
            return null;

        // Parse hex values (typically 4-digit hex: RRRR/GGGG/BBBB)
        // Convert to 8-bit RGB by taking the high byte
        if (!ushort.TryParse(parts[0], System.Globalization.NumberStyles.HexNumber, null, out var r16))
            return null;
        if (!ushort.TryParse(parts[1], System.Globalization.NumberStyles.HexNumber, null, out var g16))
            return null;
        if (!ushort.TryParse(parts[2], System.Globalization.NumberStyles.HexNumber, null, out var b16))
            return null;

        // Convert 16-bit to 8-bit (take high byte)
        byte r = (byte)(r16 >> 8);
        byte g = (byte)(g16 >> 8);
        byte b = (byte)(b16 >> 8);

        return Color.Rgb(r, g, b);
    }

    public void Dispose()
    {
        if (_disposed) return;
        ShowCursor();
        _disposed = true;
    }
}
