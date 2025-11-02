// Represents per-session state for a debug adapter connection.
public class DebugSession
{
    public string SessionId { get; set; } = string.Empty;
    public bool IsInitialized { get; set; }
    public bool IsLaunched { get; set; }
    public bool IsConfigured { get; set; }
    public bool IsRunning { get; set; }
    public bool IsTerminated { get; set; }
    public int? CurrentThreadId { get; set; }
    public string? TargetProgram { get; set; }

    // Breakpoints: file path -> set of line numbers
    public Dictionary<string, HashSet<int>> Breakpoints { get; } = new();

    public void AddBreakpoint(string filePath, int line)
    {
        filePath = System.IO.Path.GetFullPath(filePath);
        if (!Breakpoints.ContainsKey(filePath)) Breakpoints[filePath] = new HashSet<int>();
        Breakpoints[filePath].Add(line);
    }

    public bool RemoveBreakpoint(string filePath, int line)
    {
        filePath = System.IO.Path.GetFullPath(filePath);
        if (!Breakpoints.TryGetValue(filePath, out var lines)) return false;
        var removed = lines.Remove(line);
        if (removed && lines.Count == 0) Breakpoints.Remove(filePath);
        return removed;
    }

    public IEnumerable<(string file, int line)> GetAllBreakpoints()
    {
        foreach (var kvp in Breakpoints)
        {
            foreach (var line in kvp.Value) yield return (kvp.Key, line);
        }
    }
}
