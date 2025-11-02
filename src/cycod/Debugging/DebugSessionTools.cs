using System.ComponentModel;
using System.Text.Json;
using System.Text.Json.Serialization;

// Skeleton debugging tools; real DAP integration will replace stub internals later.
// Instance-based so a single debug session can maintain state across tool calls.
public class DebugSessionTools : IAsyncDisposable
{
    private readonly object _sessionLock = new();
    private DapClient? _dap;
    private bool _running;
    private string? _sessionId;
    private readonly List<DebugEvent> _eventQueue = new();

    [Description("Starts a new debug session. Provide programPath OR attachProcessId. Returns session id and status.")]
    public string StartDebugSession(
        string? programPath = null,
        string? arguments = null,
        string? workingDirectory = null,
        int? attachProcessId = null)
    {
        lock (_sessionLock)
        {
            if (_dap != null) return Serialize(new { warning = "session-already-active", sessionId = _sessionId });
            var needPathOrPid = string.IsNullOrEmpty(programPath) && !attachProcessId.HasValue;
            if (needPathOrPid) return Serialize(new { error = "programPath-or-attachProcessId-required" });

            _sessionId = Guid.NewGuid().ToString("N");
            _dap = new DapClient();
            var launchResult = attachProcessId.HasValue
                ? _dap.Attach(attachProcessId.Value)
                : _dap.Launch(programPath!, arguments, workingDirectory);
            _running = launchResult.Success;
            return Serialize(new { sessionId = _sessionId, status = _running ? "running" : "error", message = launchResult.Message });
        }
    }

    [Description("Sets a breakpoint at filePath:line. Optional condition. Returns breakpoint id and verification status.")]
    public string SetBreakpoint(string filePath, int line, string? condition = null)
    {
        lock (_sessionLock)
        {
            if (!EnsureSession()) return Serialize(new { error = "no-active-session" });
            var bp = _dap!.SetBreakpoint(filePath, line, condition);
            return Serialize(new { breakpointId = bp.Id, verified = bp.Verified });
        }
    }

    [Description("Continues execution until next pause or termination. Returns running state and last stop reason.")]
    public string ContinueExecution()
    {
        lock (_sessionLock)
        {
            if (!EnsureSession()) return Serialize(new { error = "no-active-session" });
            var status = _dap!.Continue();
            _running = status.IsRunning;
            return Serialize(new { running = _running, reason = status.StopReason });
        }
    }

    [Description("Steps over the current line. Returns new source location.")]
    public string StepOver()
    {
        lock (_sessionLock)
        {
            if (!EnsureSession()) return Serialize(new { error = "no-active-session" });
            var loc = _dap!.StepOver();
            return Serialize(new { file = loc.File, line = loc.Line, function = loc.Function });
        }
    }

    [Description("Steps into the next call. Returns new source location.")]
    public string StepIn()
    {
        lock (_sessionLock)
        {
            if (!EnsureSession()) return Serialize(new { error = "no-active-session" });
            var loc = _dap!.StepIn();
            return Serialize(new { file = loc.File, line = loc.Line, function = loc.Function });
        }
    }

    [Description("Steps out of the current function. Returns new source location.")]
    public string StepOut()
    {
        lock (_sessionLock)
        {
            if (!EnsureSession()) return Serialize(new { error = "no-active-session" });
            var loc = _dap!.StepOut();
            return Serialize(new { file = loc.File, line = loc.Line, function = loc.Function });
        }
    }

    [Description("Lists variables for the given scope (locals, globals). Returns an array of name/value/type objects.")]
    [ReadOnly(true)]
    public string ListVariables(string scope = "locals")
    {
        lock (_sessionLock)
        {
            if (!EnsureSession()) return Serialize(new { error = "no-active-session" });
            var vars = _dap!.GetVariables(scope);
            var list = vars.Select(v => new { v.Name, v.Value, v.Type });
            return Serialize(list);
        }
    }

    [Description("Evaluates an expression in the current frame context. Returns value, type and error if any.")]
    [ReadOnly(true)]
    public string EvaluateExpression(string expression)
    {
        lock (_sessionLock)
        {
            if (!EnsureSession()) return Serialize(new { error = "no-active-session" });
            var result = _dap!.Evaluate(expression);
            return Serialize(new { expression, result.Value, result.Type, result.Error });
        }
    }

    [Description("Returns current stack frames. Each frame includes index, file, line, function.")]
    [ReadOnly(true)]
    public string GetStackFrames()
    {
        lock (_sessionLock)
        {
            if (!EnsureSession()) return Serialize(new { error = "no-active-session" });
            var frames = _dap!.GetStackFrames();
            var list = frames.Select(f => new { f.Index, f.File, f.Line, f.Function });
            return Serialize(list);
        }
    }

    [Description("Retrieves and clears pending debug events (breakpoint hits, exceptions). Returns array of events.")]
    [ReadOnly(true)]
    public string GetPendingDebugEvents()
    {
        lock (_sessionLock)
        {
            var events = _eventQueue.ToList();
            _eventQueue.Clear();
            var list = events.Select(e => new { e.Type, e.Message, e.File, e.Line });
            return Serialize(list);
        }
    }

    [Description("Stops and terminates the current debug session. Returns final status.")]
    public string StopDebugSession()
    {
        lock (_sessionLock)
        {
            if (_dap == null) return Serialize(new { status = "no-active-session" });
            _dap.Terminate();
            _dap.Dispose();
            _dap = null;
            _running = false;
            var sid = _sessionId;
            _sessionId = null;
            return Serialize(new { sessionId = sid, status = "stopped" });
        }
    }

    private bool EnsureSession() => _dap != null;

    private static string Serialize(object obj)
    {
        var json = JsonSerializer.Serialize(obj, new JsonSerializerOptions
        {
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        });
        return json;
    }

    public ValueTask DisposeAsync()
    {
        StopDebugSession();
        return ValueTask.CompletedTask;
    }
}
