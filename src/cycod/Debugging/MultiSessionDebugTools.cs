using System.ComponentModel;
using System.Text.Json;
using System.Linq;
using Cycod.Debugging.Protocol;

public class MultiSessionDebugTools : IAsyncDisposable
{
    private readonly object _lock = new();
    private readonly Dictionary<string, DebugSession> _sessions = new();
    private readonly Dictionary<string, IDapClient> _clients = new();
    private readonly Dictionary<string, List<DebugEventModel>> _events = new();
    private readonly Dictionary<string, List<string>> _outputLines = new();

    private const int MaxSessions = 2;
    private const int MaxEventQueue = 500;
    private const int MaxOutputLines = 2000;
    private const int MaxOutputLineChars = 500;

    private readonly Func<string, Action<Event>, IDapClient> _clientFactory;
    public MultiSessionDebugTools() : this((adapterPath, cb) => new RealDapClient(adapterPath, cb)) { }
    public MultiSessionDebugTools(Func<string, Action<Event>, IDapClient> clientFactory) { _clientFactory = clientFactory; }

    [Description("Starts a new debug session. Returns session id and adapter path.")]
    public string StartDebugSession(string programPath, string arguments = "", string workingDirectory = "", bool stopAtEntry = false)
    {
        lock (_lock)
        {
            if (_sessions.Count >= MaxSessions) return Serialize(new { error = "max-sessions-reached", limit = MaxSessions });
            if (string.IsNullOrWhiteSpace(programPath)) return Serialize(new { error = "programPath-required" });
            var fullPath = Path.GetFullPath(programPath);
            if (!File.Exists(fullPath)) return Serialize(new { error = "programPath-not-found", programPath = fullPath });
            string adapterPath;
            try { adapterPath = DebugAdapterLocator.FindNetcoredbg(); }
            catch (Exception ex) { return Serialize(new { error = "adapter-not-found", detail = ex.Message }); }

            var sessionId = Guid.NewGuid().ToString("N");
            var session = new DebugSession { SessionId = sessionId, TargetProgram = fullPath };
            _sessions[sessionId] = session;
            _events[sessionId] = new List<DebugEventModel>();
            _outputLines[sessionId] = new List<string>();

            try
            {
                var client = _clientFactory(adapterPath, evt => HandleEvent(sessionId, session, evt));
                _clients[sessionId] = client;
                var initResp = client.SendRequestAsync(DapProtocol.InitializeCommand, new InitializeRequestArguments()).Result;
                if (!initResp.Success) return Serialize(new { error = "initialize-failed", message = initResp.Message });
                session.IsInitialized = true;
                var launchResp = client.SendRequestAsync(DapProtocol.LaunchCommand, new LaunchRequestArguments { Program = fullPath, Cwd = string.IsNullOrEmpty(workingDirectory) ? Path.GetDirectoryName(fullPath) : workingDirectory, StopAtEntry = stopAtEntry }).Result;
                if (!launchResp.Success) return Serialize(new { error = "launch-failed", message = launchResp.Message });
                session.IsLaunched = true;
                session.IsRunning = true;
                return Serialize(new { status = "ok", sessionId, adapterPath, programPath = fullPath });
            }
            catch (Exception ex) { return Serialize(new { error = "start-failed", message = ex.Message }); }
        }
    }

    [Description("Stops and terminates a debug session.")]
    public string StopDebugSession(string sessionId)
    {
        lock (_lock)
        {
            if (!_sessions.TryGetValue(sessionId, out var session)) return Serialize(new { error = "session-not-found", sessionId });
            if (_clients.TryGetValue(sessionId, out var client)) { try { client.Dispose(); } catch { } _clients.Remove(sessionId); }
            _sessions.Remove(sessionId); _events.Remove(sessionId); _outputLines.Remove(sessionId);
            session.IsTerminated = true;
            return Serialize(new { status = "stopped", sessionId });
        }
    }

    [ReadOnly(true)]
    [Description("Lists active debug sessions.")]
    public string ListSessions()
    {
        lock (_lock)
        {
            var list = _sessions.Values.Select(s => new { s.SessionId, programPath = s.TargetProgram, s.IsInitialized, s.IsLaunched, s.IsRunning, s.IsTerminated });
            return Serialize(new { status = "ok", sessions = list });
        }
    }

    [ReadOnly(true)]
    [Description("Returns and clears pending structured debug events.")]
    public string GetPendingDebugEvents(string sessionId)
    {
        lock (_lock)
        {
            if (!_events.TryGetValue(sessionId, out var queue)) return Serialize(new { error = "session-not-found", sessionId });
            var events = queue.ToList(); queue.Clear();
            return Serialize(new { status = "ok", sessionId, count = events.Count, events });
        }
    }


    [Description("Attaches to an existing process id. Returns session id and adapter path.")]

    public string AttachDebugSession(int processId, bool stopAtEntry = false, string workingDirectory = "")
    {
        lock (_lock)
        {
            if (_sessions.Count >= MaxSessions) return Serialize(new { error = "max-sessions-reached", limit = MaxSessions });
            if (processId <= 0) return Serialize(new { error = "attach-processid-required", message = "processId must be > 0" });
            string adapterPath;
            try { adapterPath = DebugAdapterLocator.FindNetcoredbg(); }
            catch (Exception ex) { return Serialize(new { error = "adapter-not-found", message = ex.Message }); }
            var sessionId = Guid.NewGuid().ToString("N");
            var session = new DebugSession { SessionId = sessionId };
            _sessions[sessionId] = session;
            _events[sessionId] = new List<DebugEventModel>();
            _outputLines[sessionId] = new List<string>();
            try
            {
                var client = _clientFactory(adapterPath, evt => HandleEvent(sessionId, session, evt));
                _clients[sessionId] = client;
                var initResp = client.SendRequestAsync(DapProtocol.InitializeCommand, new InitializeRequestArguments()).Result;
                if (!initResp.Success) return Serialize(new { error = "initialize-failed", sessionId, message = initResp.Message });
                session.IsInitialized = true;
                var attachResp = client.SendRequestAsync(DapProtocol.AttachCommand, new AttachRequestArguments { ProcessId = processId, Cwd = workingDirectory, StopAtEntry = stopAtEntry }).Result;
                if (!attachResp.Success) return Serialize(new { error = "attach-failed", sessionId, message = attachResp.Message });
                session.IsLaunched = true; // treat as launched
                session.IsRunning = true;
                return Serialize(new { status = "ok", sessionId, adapterPath, processId });
            }
            catch (Exception ex) { return Serialize(new { error = "attach-failed", sessionId, message = ex.Message }); }
        }
    }

    [ReadOnly(true)]
    [Description("Returns whether a pending stopped event exists.")]
    public string HasPendingCriticalEvent(string sessionId)
    {
        lock (_lock)
        {
            if (!_events.TryGetValue(sessionId, out var queue)) return Serialize(new { error = "session-not-found", sessionId });
            var has = queue.Any(e => e.Type == DapProtocol.StoppedEvent);
            return Serialize(new { status = "ok", sessionId, hasCritical = has });
        }
    }

    [ReadOnly(true)]
    [Description("Gets buffered stdout/stderr output lines.")]
    public string GetSessionOutput(string sessionId, int fromIndex = 0, int maxLines = 200, bool clear = false)
    {
        lock (_lock)
        {
            if (!_outputLines.TryGetValue(sessionId, out var lines)) return Serialize(new { error = "session-not-found", sessionId });
            var clampedFrom = Math.Max(0, fromIndex);
            var slice = lines.Skip(clampedFrom).Take(maxLines).Select((text, i) => new { index = clampedFrom + i, text }).ToList();
            if (clear) lines.Clear();
            return Serialize(new { status = "ok", sessionId, fromIndex = clampedFrom, nextIndex = clampedFrom + slice.Count, totalLines = lines.Count, lines = slice });
        }
    }

    [Description("Gets a source snippet around current frame (alias).")]
    public string GetCurrentFrameSourceSnippet(string sessionId, int frameIndex = 0, int radius = 5) => GetSourceSnippet(sessionId, frameIndex, radius);


    [Description("Gets current stack frames up to levels limit.")]
    public string GetStackFrames(string sessionId, int levels = 50)
    {
        lock (_lock)
        {
            if (levels <= 0) levels = 1; if (levels > 200) levels = 200;
            if (!_sessions.TryGetValue(sessionId, out var session)) return Serialize(new { error = "session-not-found", sessionId, message = "Session not found" });
            if (!_clients.TryGetValue(sessionId, out var client)) return Serialize(new { error = "client-not-found", sessionId, message = "Client missing" });
            if (session.CurrentThreadId == null) return Serialize(new { error = "no-thread-id", sessionId, message = "No current thread id" });
            try
            {
                var stResp = client.SendRequestAsync(DapProtocol.StackTraceCommand, new StackTraceArguments { ThreadId = session.CurrentThreadId.Value, Levels = levels }).Result;
                if (!stResp.Success) return Serialize(new { error = "stacktrace-failed", sessionId, message = stResp.Message });
                var stBody = DeserializeBody<StackTraceResponseBody>(stResp.Body);
                if (stBody == null) return Serialize(new { error = "stacktrace-empty", sessionId, message = "Empty stackTrace body" });
                var frames = stBody.StackFrames.Select((f, i) => new { index = i, function = f.Name, file = f.Source?.Path, line = f.Line, column = f.Column }).ToList();
                return Serialize(new { status = "ok", sessionId, totalFrames = stBody.TotalFrames, frames });
            }
            catch (Exception ex) { return Serialize(new { error = "stacktrace-error", sessionId, message = ex.Message }); }
        }
    }


    [Description("Evaluates an expression in the current frame context.")]

    public string EvaluateExpression(string sessionId, string expression, int frameIndex = 0, string context = "repl")
    {
        lock (_lock)
        {
            if (!_sessions.TryGetValue(sessionId, out var session)) return Serialize(new { error = "session-not-found", sessionId, message = "Session not found" });
            if (!_clients.TryGetValue(sessionId, out var client)) return Serialize(new { error = "client-not-found", sessionId, message = "Client missing" });
            if (session.IsRunning) return Serialize(new { error = "not-stopped", sessionId, message = "Session must be stopped to evaluate." });
            if (string.IsNullOrWhiteSpace(expression)) return Serialize(new { error = "evaluate-failed", sessionId, message = "Expression required" });
            if (session.CurrentThreadId == null) return Serialize(new { error = "no-thread-id", sessionId, message = "No current thread id" });
            try
            {
                var stResp = client.SendRequestAsync(DapProtocol.StackTraceCommand, new StackTraceArguments { ThreadId = session.CurrentThreadId.Value, Levels = frameIndex + 1 }).Result;
                if (!stResp.Success) return Serialize(new { error = "stacktrace-failed", sessionId, message = stResp.Message });
                var stBody = DeserializeBody<StackTraceResponseBody>(stResp.Body);
                if (stBody == null || stBody.StackFrames.Length == 0 || frameIndex >= stBody.StackFrames.Length) return Serialize(new { error = "frame-index-out-of-range", sessionId, message = "Frame index out of range" });
                var frame = stBody.StackFrames[frameIndex];
                var evalArgs = new { expression, frameId = frame.Id, context };
                var evalResp = client.SendRequestAsync(DapProtocol.EvaluateCommand, evalArgs).Result;
                if (!evalResp.Success)
                {
                    var msg = evalResp.Message ?? "evaluation failed";
                    return Serialize(new { error = "evaluate-failed", sessionId, message = msg });
                }
                var bodyJson = evalResp.Body?.ToString();
                string value = ""; string type = "";
                if (bodyJson != null)
                {
                    try
                    {
                        using var doc = System.Text.Json.JsonDocument.Parse(bodyJson);
                        if (doc.RootElement.TryGetProperty("result", out var r)) value = r.GetString() ?? "";
                        if (doc.RootElement.TryGetProperty("type", out var t)) type = t.GetString() ?? "";
                    }
                    catch { }
                }
                return Serialize(new { status = "ok", sessionId, expression, value, type });
            }
            catch (Exception ex) { return Serialize(new { error = "evaluate-failed", sessionId, message = ex.Message }); }
        }
    }


    [Description("Sets a breakpoint for a file and line.")]
    public string SetBreakpoint(string sessionId, string filePath, int line, string condition = "")
    {
        lock (_lock)
        {
            if (!_sessions.TryGetValue(sessionId, out var session)) return Serialize(new { error = "session-not-found", sessionId });
            if (!_clients.TryGetValue(sessionId, out var client)) return Serialize(new { error = "client-not-found", sessionId });
            var full = Path.GetFullPath(filePath); if (!File.Exists(full)) return Serialize(new { error = "file-not-found", filePath = full });
            session.AddBreakpoint(full, line);
            var result = SyncBreakpointsForFile(session, client, full);
            return Serialize(new { status = "ok", sessionId, file = full, line, verified = result.verified, message = result.message });
        }
    }

    [Description("Removes a breakpoint for a file and line.")]
    public string RemoveBreakpoint(string sessionId, string filePath, int line)
    {
        lock (_lock)
        {
            if (!_sessions.TryGetValue(sessionId, out var session)) return Serialize(new { error = "session-not-found", sessionId });
            if (!_clients.TryGetValue(sessionId, out var client)) return Serialize(new { error = "client-not-found", sessionId });
            var full = Path.GetFullPath(filePath);
            var removed = session.RemoveBreakpoint(full, line);
            if (!removed) return Serialize(new { error = "breakpoint-not-found", file = full, line });
            var result = SyncBreakpointsForFile(session, client, full);
            return Serialize(new { status = "ok", sessionId, file = full, line, remaining = session.Breakpoints.ContainsKey(full) ? session.Breakpoints[full].Count : 0, verifiedAll = result.verified });
        }
    }

    [ReadOnly(true)]
    [Description("Lists breakpoints for a session.")]
    public string ListBreakpoints(string sessionId)
    {
        lock (_lock)
        {
            if (!_sessions.TryGetValue(sessionId, out var session)) return Serialize(new { error = "session-not-found", sessionId });
            var list = session.GetAllBreakpoints().Select(b => new { b.file, b.line });
            return Serialize(new { status = "ok", sessionId, breakpoints = list });
        }
    }

    [Description("Continues execution (handles initial configuration).")]
    public string ContinueExecution(string sessionId)
    {
        lock (_lock)
        {
            if (!_sessions.TryGetValue(sessionId, out var session)) return Serialize(new { error = "session-not-found", sessionId });
            if (!_clients.TryGetValue(sessionId, out var client)) return Serialize(new { error = "client-not-found", sessionId });
            if (session.CurrentThreadId == null) return Serialize(new { error = "no-thread-id", sessionId });
            try
            {
                if (!session.IsConfigured)
                {
                    var cfgResp = client.SendRequestAsync(DapProtocol.ConfigurationDoneCommand, new { }).Result;
                    if (!cfgResp.Success) return Serialize(new { error = "configuration-failed", message = cfgResp.Message, sessionId });
                    session.IsConfigured = true;
                }
                var resp = client.SendRequestAsync(DapProtocol.ContinueCommand, new ContinueArguments { ThreadId = session.CurrentThreadId.Value }).Result;
                if (!resp.Success) return Serialize(new { error = "continue-failed", message = resp.Message, sessionId });
                session.IsRunning = true;
                return Serialize(new { status = "ok", sessionId, running = true });
            }
            catch (Exception ex) { return Serialize(new { error = "continue-error", message = ex.Message, sessionId }); }
        }
    }

    [Description("Steps over the current line.")]
    public string StepOver(string sessionId) => PerformStep(sessionId, DapProtocol.NextCommand, "stepover-failed");
    [Description("Steps into the next call.")]
    public string StepIn(string sessionId) => PerformStep(sessionId, DapProtocol.StepInCommand, "stepin-failed");
    [Description("Steps out of the current function.")]
    public string StepOut(string sessionId) => PerformStep(sessionId, DapProtocol.StepOutCommand, "stepout-failed");

    [ReadOnly(true)]
    [Description("Lists variables for a frame and scope.")]
    public string ListVariables(string sessionId, string scope = "locals", int frameIndex = 0, int max = 200)
    {
        lock (_lock)
        {
            if (!_sessions.TryGetValue(sessionId, out var session)) return Serialize(new { error = "session-not-found", sessionId });
            if (!_clients.TryGetValue(sessionId, out var client)) return Serialize(new { error = "client-not-found", sessionId });
            if (session.CurrentThreadId == null) return Serialize(new { error = "no-thread-id", sessionId });
            try
            {
                var stResp = client.SendRequestAsync(DapProtocol.StackTraceCommand, new StackTraceArguments { ThreadId = session.CurrentThreadId.Value, Levels = frameIndex + 1 }).Result;
                if (!stResp.Success) return Serialize(new { error = "stacktrace-failed", message = stResp.Message, sessionId });
                var stBody = DeserializeBody<StackTraceResponseBody>(stResp.Body);
                if (stBody == null || stBody.StackFrames.Length == 0 || frameIndex >= stBody.StackFrames.Length) return Serialize(new { error = "frame-index-out-of-range", sessionId, frameIndex });
                var frame = stBody.StackFrames[frameIndex];
                var scopesResp = client.SendRequestAsync(DapProtocol.ScopesCommand, new ScopesArguments { FrameId = frame.Id }).Result;
                if (!scopesResp.Success) return Serialize(new { error = "scopes-failed", message = scopesResp.Message, sessionId });
                var scopesBody = DeserializeBody<ScopesResponseBody>(scopesResp.Body); if (scopesBody == null) return Serialize(new { error = "scopes-empty", sessionId });
                var matchScope = scopesBody.Scopes.FirstOrDefault(s => string.Equals(s.Name, scope, StringComparison.OrdinalIgnoreCase)); if (matchScope == null) return Serialize(new { error = "scope-not-found", scope, sessionId });
                var varsResp = client.SendRequestAsync(DapProtocol.VariablesCommand, new VariablesArguments { VariablesReference = matchScope.VariablesReference }).Result; if (!varsResp.Success) return Serialize(new { error = "variables-failed", message = varsResp.Message, sessionId });
                var varsBody = DeserializeBody<VariablesResponseBody>(varsResp.Body);
                Cycod.Debugging.Protocol.Variable[] vars;
                if (varsBody == null) vars = Array.Empty<Cycod.Debugging.Protocol.Variable>();
                else vars = varsBody.Variables ?? Array.Empty<Cycod.Debugging.Protocol.Variable>();
                var limited = vars.Take(max).Select(v => new { v.Name, v.Value, v.Type }).ToList();
                return Serialize(new { status = "ok", sessionId, scope, frameIndex, count = limited.Count, truncated = vars.Length > limited.Count, variables = limited });
            }
            catch (Exception ex) { return Serialize(new { error = "variables-error", message = ex.Message, sessionId }); }
        }
    }

    [Description("Sets a variable's value.")]
    public string SetVariable(string sessionId, string variableName, string newValue, string scope = "locals", int frameIndex = 0)
    {
        lock (_lock)
        {
            if (!_sessions.TryGetValue(sessionId, out var session)) return Serialize(new { error = "session-not-found", sessionId });
            if (!_clients.TryGetValue(sessionId, out var client)) return Serialize(new { error = "client-not-found", sessionId });
            if (session.CurrentThreadId == null) return Serialize(new { error = "no-thread-id", sessionId });
            try
            {
                var stResp = client.SendRequestAsync(DapProtocol.StackTraceCommand, new StackTraceArguments { ThreadId = session.CurrentThreadId.Value, Levels = frameIndex + 1 }).Result;
                if (!stResp.Success) return Serialize(new { error = "stacktrace-failed", message = stResp.Message, sessionId });
                var stBody = DeserializeBody<StackTraceResponseBody>(stResp.Body); if (stBody == null || stBody.StackFrames.Length == 0 || frameIndex >= stBody.StackFrames.Length) return Serialize(new { error = "frame-index-out-of-range", sessionId, frameIndex });
                var frame = stBody.StackFrames[frameIndex];
                var scopesResp = client.SendRequestAsync(DapProtocol.ScopesCommand, new ScopesArguments { FrameId = frame.Id }).Result; if (!scopesResp.Success) return Serialize(new { error = "scopes-failed", message = scopesResp.Message, sessionId });
                var scopesBody = DeserializeBody<ScopesResponseBody>(scopesResp.Body); if (scopesBody == null) return Serialize(new { error = "scopes-empty", sessionId });
                var matchScope = scopesBody.Scopes.FirstOrDefault(s => string.Equals(s.Name, scope, StringComparison.OrdinalIgnoreCase)); if (matchScope == null) return Serialize(new { error = "scope-not-found", scope, sessionId });
                var varsResp = client.SendRequestAsync(DapProtocol.VariablesCommand, new VariablesArguments { VariablesReference = matchScope.VariablesReference }).Result; if (!varsResp.Success) return Serialize(new { error = "variables-failed", message = varsResp.Message, sessionId });
                var varsBody = DeserializeBody<VariablesResponseBody>(varsResp.Body); Cycod.Debugging.Protocol.Variable[] vars;
                if (varsBody == null) vars = Array.Empty<Cycod.Debugging.Protocol.Variable>();
                else vars = varsBody.Variables ?? Array.Empty<Cycod.Debugging.Protocol.Variable>(); var target = vars.FirstOrDefault(v => v.Name == variableName); if (target == null) return Serialize(new { error = "variable-not-found", variableName, sessionId });
                var setResp = client.SendRequestAsync(DapProtocol.SetVariableCommand, new SetVariableArguments { VariablesReference = matchScope.VariablesReference, Name = variableName, Value = newValue }).Result; if (!setResp.Success) return Serialize(new { error = "setvariable-failed", message = setResp.Message, sessionId });
                return Serialize(new { status = "ok", sessionId, variable = variableName, oldValue = target.Value, newValue });
            }
            catch (Exception ex) { return Serialize(new { error = "setvariable-error", message = ex.Message, sessionId }); }
        }
    }

    [ReadOnly(true)]
    [Description("Gets a source snippet around the current frame line.")]
    public string GetSourceSnippet(string sessionId, int frameIndex = 0, int radius = 5)
    {
        lock (_lock)
        {
            if (radius < 0) radius = 0; if (radius > 50) radius = 50;
            if (!_sessions.TryGetValue(sessionId, out var session)) return Serialize(new { error = "session-not-found", sessionId });
            if (!_clients.TryGetValue(sessionId, out var client)) return Serialize(new { error = "client-not-found", sessionId });
            if (session.CurrentThreadId == null) return Serialize(new { error = "no-thread-id", sessionId });
            try
            {
                var stResp = client.SendRequestAsync(DapProtocol.StackTraceCommand, new StackTraceArguments { ThreadId = session.CurrentThreadId.Value, Levels = frameIndex + 1 }).Result;
                if (!stResp.Success) return Serialize(new { error = "stacktrace-failed", message = stResp.Message, sessionId });
                var stBody = DeserializeBody<StackTraceResponseBody>(stResp.Body); if (stBody == null || stBody.StackFrames.Length == 0 || frameIndex >= stBody.StackFrames.Length) return Serialize(new { error = "frame-index-out-of-range", sessionId, frameIndex });
                var frame = stBody.StackFrames[frameIndex]; var file = frame.Source?.Path; if (string.IsNullOrEmpty(file) || !File.Exists(file)) return Serialize(new { error = "file-not-found", file });
                var allLines = File.ReadAllLines(file); var center = frame.Line; var start = Math.Max(1, center - radius); var end = Math.Min(allLines.Length, center + radius);
                var snippet = new SourceSnippetModel { File = file, FileName = Path.GetFileName(file), CenterLine = center, StartLine = start, EndLine = end, Radius = radius, RadiusClamped = radius > 50, HighlightIndex = center - start };
                for (int lineNum = start; lineNum <= end; lineNum++) snippet.Lines.Add(new SourceSnippetLine { Line = lineNum, Text = allLines[lineNum - 1] });
                return Serialize(new { status = "ok", sessionId, snippet });
            }
            catch (Exception ex) { return Serialize(new { error = "snippet-error", message = ex.Message, sessionId }); }
        }
    }

    private string PerformStep(string sessionId, string command, string errorKey)
    {
        lock (_lock)
        {
            if (!_sessions.TryGetValue(sessionId, out var session)) return Serialize(new { error = "session-not-found", sessionId });
            if (!_clients.TryGetValue(sessionId, out var client)) return Serialize(new { error = "client-not-found", sessionId });
            if (session.CurrentThreadId == null) return Serialize(new { error = "no-thread-id", sessionId });
            try
            {
                object args = command switch
                {
                    var c when c == DapProtocol.NextCommand => new NextArguments { ThreadId = session.CurrentThreadId.Value },
                    var c when c == DapProtocol.StepInCommand => new StepInArguments { ThreadId = session.CurrentThreadId.Value },
                    _ => new StepOutArguments { ThreadId = session.CurrentThreadId.Value }
                };
                var resp = client.SendRequestAsync(command, args).Result;
                if (!resp.Success) return Serialize(new { error = errorKey, message = resp.Message, sessionId });
                session.IsRunning = true;
                return Serialize(new { status = "ok", sessionId, stepping = command });
            }
            catch (Exception ex) { return Serialize(new { error = errorKey, message = ex.Message, sessionId }); }
        }
    }

    private (bool verified, string? message) SyncBreakpointsForFile(DebugSession session, IDapClient client, string file)
    {
        var lines = session.Breakpoints.TryGetValue(file, out var set) ? set.ToArray() : Array.Empty<int>();
        var bps = lines.Select(l => new SourceBreakpoint { Line = l }).ToArray();
        var args = new SetBreakpointsArguments { Source = new Source { Name = Path.GetFileName(file), Path = file }, Breakpoints = bps };
        try
        {
            var resp = client.SendRequestAsync(DapProtocol.SetBreakpointsCommand, args).Result;
            if (!resp.Success) return (false, resp.Message);
            var bodyJson = resp.Body?.ToString(); if (bodyJson == null) return (bps.Length == 0, null);
            var body = JsonSerializer.Deserialize<SetBreakpointsResponseBody>(bodyJson);
            var allVerified = body?.Breakpoints.All(b => b.Verified) ?? true;
            return (allVerified, null);
        }
        catch (Exception ex) { return (false, ex.Message); }
    }

    private void HandleEvent(string sessionId, DebugSession session, Event evt)
    {
        lock (_lock)
        {
            if (!_events.TryGetValue(sessionId, out var queue)) return;
            var model = new DebugEventModel { Type = evt.EventType };
            switch (evt.EventType)
            {
                case DapProtocol.StoppedEvent:
                    var stopped = TryDeserialize<StoppedEventBody>(evt.Body);
                    if (stopped != null)
                    {
                        model.Reason = stopped.Reason; model.ThreadId = stopped.ThreadId; model.Message = stopped.Text;
                        session.CurrentThreadId = stopped.ThreadId; session.IsRunning = false;
                    }
                    break;
                case DapProtocol.ThreadEvent:
                    var thread = TryDeserialize<ThreadEventBody>(evt.Body);
                    if (thread != null && thread.Reason == DapProtocol.ThreadReasonStarted) session.CurrentThreadId = thread.ThreadId;
                    break;
                case DapProtocol.OutputEvent:
                    var output = TryDeserialize<OutputEventBody>(evt.Body);
                    if (output != null && !string.IsNullOrEmpty(output.Output))
                    {
                        model.Category = output.Category; model.Output = TruncateLine(output.Output);
                        if (_outputLines.TryGetValue(sessionId, out var list)) { list.Add(model.Output); if (list.Count > MaxOutputLines) list.RemoveAt(0); }
                    }
                    break;
                case DapProtocol.ExitedEvent:
                    var exited = TryDeserialize<ExitedEventBody>(evt.Body);
                    if (exited != null) { model.ExitCode = exited.ExitCode; session.IsRunning = false; session.IsTerminated = true; }
                    break;
                case DapProtocol.TerminatedEvent:
                    session.IsRunning = false; session.IsTerminated = true;
                    break;
            }
            queue.Add(model); if (queue.Count > MaxEventQueue) queue.RemoveAt(0);
        }
    }

    private static T? TryDeserialize<T>(object? body)
    {
        try
        {
            if (body == null) return default;
            var json = body.ToString();
            return json != null ? JsonSerializer.Deserialize<T>(json) : default;
        }
        catch { return default; }
    }

    private static TBody? DeserializeBody<TBody>(object? body)
    {
        try
        {
            if (body == null) return default;
            var json = body.ToString();
            return json != null ? JsonSerializer.Deserialize<TBody>(json) : default;
        }
        catch { return default; }
    }

    private string TruncateLine(string line) => line.Length <= MaxOutputLineChars ? line : line.Substring(0, MaxOutputLineChars) + "…";
    private static string Serialize(object obj) => JsonSerializer.Serialize(obj, new JsonSerializerOptions { DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull });

    public ValueTask DisposeAsync()
    {
        lock (_lock)
        {
            foreach (var client in _clients.Values) { try { client.Dispose(); } catch { } }
            _clients.Clear(); _sessions.Clear(); _events.Clear(); _outputLines.Clear();
        }
        return ValueTask.CompletedTask;
    }
}
