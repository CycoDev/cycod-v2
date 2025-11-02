using System.Text.Json;
using Cycod.Debugging.Protocol;

public class MockDapClient : IDapClient
{
    private readonly Action<Event> _eventCallback;
    private readonly Dictionary<string, HashSet<int>> _breakpoints = new();
    private readonly Dictionary<string, (string value, string type)> _variables = new();
    private int _nextSeq = 1;
    private int _currentThreadId = 1;

    public MockDapClient(Action<Event> eventCallback)
    {
        _eventCallback = eventCallback;
        _variables["x"] = ("10", "int");
        _variables["y"] = ("20", "int");
    }

    public Task<Response> SendRequestAsync(string command, object? args, CancellationToken ct = default)
    {
        var resp = new Response { Seq = _nextSeq++, RequestSeq = _nextSeq, Success = true, Command = command };
        switch (command)
        {
            case DapProtocol.InitializeCommand:
                resp.Body = new { capabilities = new { } }; break;
            case DapProtocol.LaunchCommand:
                resp.Body = new { launched = true }; break;
            case DapProtocol.ConfigurationDoneCommand:
                resp.Body = new { configured = true }; break;
            case DapProtocol.SetBreakpointsCommand:
                var sbArgsJson = JsonSerializer.Serialize(args);
                var sbArgs = JsonSerializer.Deserialize<SetBreakpointsArguments>(sbArgsJson);
                if (sbArgs?.Source?.Path != null)
                {
                    var path = sbArgs.Source.Path;
                    _breakpoints[path] = new HashSet<int>(sbArgs.Breakpoints?.Select(b => b.Line) ?? Enumerable.Empty<int>());
                    resp.Body = new SetBreakpointsResponseBody
                    {
                        Breakpoints = _breakpoints[path].Select(l => new BreakpointResult { Line = l, Verified = true }).ToArray()
                    };
                }
                break;
            case DapProtocol.ContinueCommand:
            case DapProtocol.NextCommand:
            case DapProtocol.StepInCommand:
            case DapProtocol.StepOutCommand:
                EmitStoppedEvent("breakpoint");
                resp.Body = new { running = true };
                break;
            case DapProtocol.StackTraceCommand:
                resp.Body = new StackTraceResponseBody
                {
                    StackFrames = new[]
                    {
                        new Cycod.Debugging.Protocol.StackFrame { Id = 100, Name = "Main", Source = new Cycod.Debugging.Protocol.Source { Name = "MockProgram.cs", Path = GetMockSourcePath() }, Line = FirstBreakpointOr(10), Column = 1 }
                    },
                    TotalFrames = 1
                };
                break;
            case DapProtocol.ScopesCommand:
                resp.Body = new ScopesResponseBody { Scopes = new[] { new Scope { Name = "locals", VariablesReference = 200, Expensive = false } } };
                break;
            case DapProtocol.VariablesCommand:
                resp.Body = new VariablesResponseBody
                {
                    Variables = _variables.Select(kv => new Cycod.Debugging.Protocol.Variable { Name = kv.Key, Value = kv.Value.value, Type = kv.Value.type }).ToArray()
                };
                break;
            case DapProtocol.SetVariableCommand:
                var setVarJson = JsonSerializer.Serialize(args);
                var setArgs = JsonSerializer.Deserialize<SetVariableArguments>(setVarJson);
                if (setArgs?.Name != null)
                {
                    if (_variables.ContainsKey(setArgs.Name))
                        _variables[setArgs.Name] = (setArgs.Value ?? "", _variables[setArgs.Name].type);
                    resp.Body = new { updated = true };
                }
                break;
            default:
                resp.Body = new { ok = true };
                break;
        }
        return Task.FromResult(resp);
    }

    public Task SendRequestNoResponseAsync(string command, object? args) => Task.CompletedTask;

    private void EmitStoppedEvent(string reason)
    {
        var evt = new Event
        {
            Seq = _nextSeq++,
            EventType = DapProtocol.StoppedEvent,
            Body = new StoppedEventBody { Reason = reason, ThreadId = _currentThreadId, Text = "Stopped (mock)" }
        };
        _eventCallback(evt);
    }

    private string GetMockSourcePath()
    {
        var tmp = Path.Combine(Path.GetTempPath(), "MockProgram.cs");
        if (!File.Exists(tmp))
        {
            File.WriteAllText(tmp, "// Mock source\nclass MockProgram { static void Main() { int x = 10; } }\n");
        }
        return tmp;
    }

    private int FirstBreakpointOr(int fallback)
    {
        foreach (var kv in _breakpoints) if (kv.Value.Count > 0) return kv.Value.Min();
        return fallback;
    }

    public void Dispose() { }
}
