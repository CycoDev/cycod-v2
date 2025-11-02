// Stub DAP client; real DAP integration will replace internals later.
internal class DapClient : IDisposable
{
    public LaunchResult Launch(string programPath, string? arguments, string? workingDirectory) => new LaunchResult { Success = true, Message = $"launched {programPath}" };
    public LaunchResult Attach(int processId) => new LaunchResult { Success = true, Message = $"attached {processId}" };
    public Breakpoint SetBreakpoint(string filePath, int line, string? condition) => new Breakpoint { Id = Guid.NewGuid().ToString("N"), Verified = true };
    public ContinueStatus Continue() => new ContinueStatus { IsRunning = true, StopReason = "running" };
    public SourceLocation StepOver() => new SourceLocation { File = "unknown", Line = 0, Function = "unknown" };
    public SourceLocation StepIn() => new SourceLocation { File = "unknown", Line = 0, Function = "unknown" };
    public SourceLocation StepOut() => new SourceLocation { File = "unknown", Line = 0, Function = "unknown" };
    public IEnumerable<Variable> GetVariables(string scope) => new List<Variable>();
    public EvalResult Evaluate(string expression) => new EvalResult { Value = "0", Type = "int" };
    public IEnumerable<StackFrame> GetStackFrames() => new List<StackFrame>();
    public void Terminate() { }
    public void Dispose() { }
}

internal class LaunchResult { public bool Success { get; set; } public string? Message { get; set; } }
internal class Breakpoint { public string? Id { get; set; } public bool Verified { get; set; } }
internal class ContinueStatus { public bool IsRunning { get; set; } public string? StopReason { get; set; } }
internal class SourceLocation { public string? File { get; set; } public int Line { get; set; } public string? Function { get; set; } }
internal class Variable { public string? Name { get; set; } public string? Value { get; set; } public string? Type { get; set; } }
internal class EvalResult { public string? Value { get; set; } public string? Type { get; set; } public string? Error { get; set; } }
internal class StackFrame { public int Index { get; set; } public string? File { get; set; } public int Line { get; set; } public string? Function { get; set; } }
internal class DebugEvent { public string? Type { get; set; } public string? Message { get; set; } public string? File { get; set; } public int? Line { get; set; } }
