using System;
// TODO: Capability probing (underline color, scrolling regions, mouse); buffering optimization

using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using CycoTui.Core.Backend;
using CycoTui.Core.Style;

namespace CycoTui.Backend.Unix;

/// <summary>
/// Minimal ANSI/Unix backend. Phase-1 implementation writing raw ANSI directly.
/// </summary>
public sealed class UnixTerminalBackend : ITerminalBackend
{
    private BackendCapabilities ProbeCapabilities()
    {
        var colorterm = Environment.GetEnvironmentVariable("COLORTERM") ?? string.Empty;
        var trueColor = colorterm.Contains("truecolor", StringComparison.OrdinalIgnoreCase);
        var level = trueColor ? ColorLevel.TrueColor : ColorLevel.Ansi256;
        return new BackendCapabilities(level, supportsUnderlineColor: false, supportsMouse: true, supportsScrollingRegions: true, supportsTrueColor: trueColor, supportsUnicodeWidthReliably: false);
    }
    private bool _disposed;
    public BackendCapabilities Capabilities { get; }

    public UnixTerminalBackend()
    {
        Capabilities = ProbeCapabilities();
        // Phase-1: capability probing omitted.
    }

    public void Draw(IEnumerable<CellUpdate> updates)
    {
        foreach (var u in updates)
        {
            if (u.X < 0 || u.Y < 0) continue;
            WriteRaw($"\u001b[{u.Y + 1};{u.X + 1}H"); // ANSI cursor move is 1-based
            Console.Write(u.Cell.Grapheme);
        }
    }

    public void WriteRaw(string sequence) => Console.Write(sequence);
    public void Flush() { /* stdout unbuffered enough for phase-1 */ }
    public void HideCursor() => Console.Write("\u001b[?25l");
    public void ShowCursor() => Console.Write("\u001b[?25h");
    public Position GetCursorPosition() => new(0,0); // Phase-1 placeholder
    public void SetCursorPosition(Position position) => WriteRaw($"\u001b[{position.Y + 1};{position.X + 1}H");
    public void Clear(ClearType type = ClearType.All)
    {
        if (type == ClearType.All) WriteRaw("\u001b[2J\u001b[H");
    }
    public void AppendLines(int count = 1)
    {
        for (int i = 0; i < count; i++) Console.Write("\n");
    }
    public Size GetSize()
    {
        try
        {
            // Use window size, not buffer size (buffer can be huge on Unix/macOS)
            int w = Console.WindowWidth;
            int h = Console.WindowHeight;
            return new Size(w, h);
        }
        catch { return new Size(80,24); }
    }
    public WindowSize GetWindowSize() => new(GetSize(), Size.Empty);
    public void ScrollRegionUp(Range region, int lineCount) { /* no-op phase-1 */ }
    public void ScrollRegionDown(Range region, int lineCount) { /* no-op phase-1 */ }

    public (Color? Foreground, Color? Background) QueryDefaultColors()
    {
        // Query both foreground (OSC 10) and background (OSC 11) colors
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

            // Parse response format: ESC ] {code} ; rgb:RRRR/GGGG/BBBB BEL or ESC ]  {code} ; rgb:RRRR/GGGG/BBBB ESC \
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
                    var readTask = Task.Run(() => stdin.Read(buffer, 0, 1));
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
