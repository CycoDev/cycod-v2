using System.ComponentModel;
using System.Text.Json;
using Cycod.Debugging.Protocol;

// Multi-session debugging tool manager. Phase 3 implementation.
public class MultiSessionDebugTools : IAsyncDisposable
{
    private readonly object _lock = new();
    private readonly Dictionary<string, DebugSession> _sessions = new();
    private readonly Dictionary<string, RealDapClient> _clients = new();
    private readonly Dictionary<string, List<DebugEventModel>> _events = new();
    private readonly Dictionary<string, List<string>> _outputLines = new();
    private const int MaxSessions = 2;
    private const int MaxEventQueue = 500;
    private const int MaxOutputLines = 2000;
    private const int MaxOutputLineChars = 500;

    [Description("Starts a new debug session for the given program path. Returns session id and status.")]
    public string StartDebugSession(string programPath, string arguments = "", string workingDirectory = "", bool stopAtEntry = false)
    {
        lock (_lock)
        {
            if (_sessions.Count >= MaxSessions)
            {
                return Serialize(new { error = "max-sessions-reached", limit = MaxSessions });
            }

            if (string.IsNullOrWhiteSpace(programPath))
            {
                return Serialize(new { error = "programPath-required" });
            }

            var fullPath = Path.GetFullPath(programPath);
            if (!File.Exists(fullPath)) return Serialize(new { error = "programPath-not-found", programPath = fullPath });

            string adapterPath;
            try
            {
                adapterPath = DebugAdapterLocator.FindNetcoredbg();
            }
            catch (Exception ex)
            {
                return Serialize(new { error = "adapter-not-found", detail = ex.Message });
            }

            var sessionId = Guid.NewGuid().ToString("N");
            var session = new DebugSession { SessionId = sessionId, TargetProgram = fullPath };
            _sessions[sessionId] = session;
            _events[sessionId] = new List<DebugEventModel>();
            _outputLines[sessionId] = new List<string>();

            try
            {
                var client = new RealDapClient(adapterPath, evt => HandleEvent(sessionId, session, evt));
                _clients[sessionId] = client;

                // initialize
                var initResp = client.SendRequestAsync(DapProtocol.InitializeCommand, new InitializeRequestArguments()).Result;
                if (!initResp.Success) return Serialize(new { error = "initialize-failed", message = initResp.Message });
                session.IsInitialized = true;

                // launch
                var launchArgs = new LaunchRequestArguments { Program = fullPath, Cwd = string.IsNullOrEmpty(workingDirectory) ? Path.GetDirectoryName(fullPath) : workingDirectory, StopAtEntry = stopAtEntry };
                var launchResp = client.SendRequestAsync(DapProtocol.LaunchCommand, launchArgs).Result;
                if (!launchResp.Success) return Serialize(new { error = "launch-failed", message = launchResp.Message });
                session.IsLaunched = true;

                // Wait for initialized event (best effort, non-blocking fallback)
                session.IsRunning = true; // program will run after configurationDone; set when continued later.

                return Serialize(new { status = "ok", sessionId, adapterPath, programPath = fullPath });
            }
            catch (Exception ex)
            {
                return Serialize(new { error = "start-failed", message = ex.Message });
            }
        }
    }

    [Description("Stops and terminates the specified debug session.")]
    public string StopDebugSession(string sessionId)
    {
        lock (_lock)
        {
            if (!_sessions.TryGetValue(sessionId, out var session)) return Serialize(new { error = "session-not-found", sessionId });
            if (_clients.TryGetValue(sessionId, out var client))
            {
                try { client.Dispose(); } catch { }
                _clients.Remove(sessionId);
            }
            _sessions.Remove(sessionId);
            _events.Remove(sessionId);
            _outputLines.Remove(sessionId);
            session.IsTerminated = true;
            return Serialize(new { status = "stopped", sessionId });
        }
    }

    [ReadOnly(true)]
    [Description("Lists all active debug sessions.")]
    public string ListSessions()
    {
        lock (_lock)
        {
            var list = _sessions.Values.Select(s => new
            {
                s.SessionId,
                programPath = s.TargetProgram,
                s.IsInitialized,
                s.IsLaunched,
                s.IsRunning,
                s.IsTerminated
            });
            return Serialize(new { status = "ok", sessions = list });
        }
    }

    [ReadOnly(true)]
    [Description("Returns and clears pending structured debug events for the given session.")]
    public string GetPendingDebugEvents(string sessionId)
    {
        lock (_lock)
        {
            if (!_events.TryGetValue(sessionId, out var queue)) return Serialize(new { error = "session-not-found", sessionId });
            var events = queue.ToList();
            queue.Clear();
            return Serialize(new { status = "ok", sessionId, count = events.Count, events });
        }
    }

    [ReadOnly(true)]
    [Description("Returns whether the session has a pending critical event (e.g., stopped).")]
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
    [Description("Gets buffered stdout/stderr output lines for a session.")]
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

    private void HandleEvent(string sessionId, DebugSession session, Event evt)
    {
        lock (_lock)
        {
            if (!_events.TryGetValue(sessionId, out var queue)) return; // session removed
            var model = new DebugEventModel { Type = evt.EventType };
            switch (evt.EventType)
            {
                case DapProtocol.StoppedEvent:
                    var stopped = TryDeserialize<StoppedEventBody>(evt.Body);
                    if (stopped != null)
                    {
                        model.Reason = stopped.Reason;
                        model.ThreadId = stopped.ThreadId;
                        model.Message = stopped.Text;
                        session.CurrentThreadId = stopped.ThreadId;
                        session.IsRunning = false;
                    }
                    break;
                case DapProtocol.ThreadEvent:
                    var thread = TryDeserialize<ThreadEventBody>(evt.Body);
                    if (thread != null && thread.Reason == DapProtocol.ThreadReasonStarted)
                    {
                        session.CurrentThreadId = thread.ThreadId;
                    }
                    break;
                case DapProtocol.OutputEvent:
                    var output = TryDeserialize<OutputEventBody>(evt.Body);
                    if (output != null && !string.IsNullOrEmpty(output.Output))
                    {
                        model.Category = output.Category;
                        model.Output = TruncateLine(output.Output);
                        if (_outputLines.TryGetValue(sessionId, out var list))
                        {
                            list.Add(model.Output);
                            if (list.Count > MaxOutputLines) list.RemoveAt(0);
                        }
                    }
                    break;
                case DapProtocol.ExitedEvent:
                    var exited = TryDeserialize<ExitedEventBody>(evt.Body);
                    if (exited != null)
                    {
                        model.ExitCode = exited.ExitCode;
                        session.IsRunning = false;
                        session.IsTerminated = true;
                    }
                    break;
                case DapProtocol.TerminatedEvent:
                    session.IsRunning = false;
                    session.IsTerminated = true;
                    break;
            }
            queue.Add(model);
            if (queue.Count > MaxEventQueue) queue.RemoveAt(0);
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

    private string TruncateLine(string line)
    {
        if (line.Length <= MaxOutputLineChars) return line;
        return line.Substring(0, MaxOutputLineChars) + "…";
    }

    private static string Serialize(object obj)
    {
        return JsonSerializer.Serialize(obj, new JsonSerializerOptions
        {
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
        });
    }

    public ValueTask DisposeAsync()
    {
        lock (_lock)
        {
            foreach (var client in _clients.Values) { try { client.Dispose(); } catch { } }
            _clients.Clear();
            _sessions.Clear();
            _events.Clear();
            _outputLines.Clear();
        }
        return ValueTask.CompletedTask;
    }
}
