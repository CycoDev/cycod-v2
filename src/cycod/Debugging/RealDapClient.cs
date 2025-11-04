using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Cycod.Debugging.Protocol;

// Minimal DAP client wrapper for netcoredbg process. Events routed via callback.
public class RealDapClient : IDapClient
{
    private readonly Process _process;
    private readonly StreamReader _reader;
    private readonly JsonSerializerOptions _jsonOptions;
    private readonly Dictionary<int, TaskCompletionSource<Response>> _pendingRequests = new();
    private readonly object _lock = new();
    private int _sequence = 1;
    private readonly Dictionary<string, Queue<Cycod.Debugging.Protocol.Event>> _eventQueue = new();
    private readonly Dictionary<string, List<TaskCompletionSource<Cycod.Debugging.Protocol.Event>>> _eventWaiters = new();
    private readonly List<string> _stderrLines = new();
    private DateTime _lastMessageTime = DateTime.UtcNow;

    private readonly CancellationTokenSource _cts = new();
    private readonly Task _listenTask;

    private readonly Action<Event> _eventCallback;

    public RealDapClient(string adapterPath, Action<Event> eventCallback)
    {
        _eventCallback = eventCallback;

        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
        };

        var startInfo = new ProcessStartInfo
        {
            FileName = adapterPath,
            Arguments = "--interpreter=vscode",
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        Logger.Info($"Starting debug adapter: {adapterPath}");
        _process = Process.Start(startInfo) ?? throw new Exception("Failed to start netcoredbg process");
        _reader = new StreamReader(_process.StandardOutput.BaseStream, Encoding.UTF8);

        _process.ErrorDataReceived += (_, e) =>
        {
            if (!string.IsNullOrEmpty(e.Data))
            {
                lock (_lock)
                {
                    _stderrLines.Add(e.Data);
                    if (_stderrLines.Count > 50) _stderrLines.RemoveAt(0);
                }
                Logger.Verbose($"adapter-stderr: {e.Data}");
            }
        };
        _process.BeginErrorReadLine();

        _listenTask = Task.Run(ListenLoop, _cts.Token);
    }

    public int PendingRequestCount { get { lock (_lock) return _pendingRequests.Count; } }
    public int LastMessageAgeMs => (int)(DateTime.UtcNow - _lastMessageTime).TotalMilliseconds;
    public bool HasExited => _process.HasExited;


    public async Task<Response> SendRequestAsync(string command, object? args, CancellationToken ct = default)
    {
        int seq;
        var tcs = new TaskCompletionSource<Response>(TaskCreationOptions.RunContinuationsAsynchronously);
        lock (_lock)
        {
            seq = _sequence++;
            _pendingRequests[seq] = tcs;
        }

        var req = new Request { Seq = seq, Command = command, Arguments = args };
        await SendMessageAsync(req, ct);
        Logger.Verbose($"→ {command} #{seq}");

        using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct, _cts.Token);
        var initTimeoutMs = Environment.GetEnvironmentVariable("CYCOD_DAP_INIT_TIMEOUT_MS");
        int baseTimeoutMs = 15000;
        if (command == DapProtocol.InitializeCommand && int.TryParse(initTimeoutMs, out var overrideMs) && overrideMs > 0) baseTimeoutMs = overrideMs;
        linked.CancelAfter(TimeSpan.FromMilliseconds(baseTimeoutMs));
        try
        {
            var sw = Stopwatch.StartNew();
            var resp = await tcs.Task.WaitAsync(linked.Token);
            Logger.Verbose($"← {command} #{seq} ({sw.ElapsedMilliseconds} ms) success={resp.Success}");
            _lastMessageTime = DateTime.UtcNow;
            return resp;
        }
        catch (OperationCanceledException)
        {
            lock (_lock) _pendingRequests.Remove(seq);
            var elapsed = DateTime.UtcNow - _lastMessageTime;
            string stderrSnapshot;
            lock (_lock) stderrSnapshot = string.Join(" | ", _stderrLines);
            Logger.Error($"Timeout waiting for {command} #{seq}; adapterExited={_process.HasExited}; lastMessageAgeMs={(int)elapsed.TotalMilliseconds}; stderr='{stderrSnapshot}' pending={_pendingRequests.Count}");
            throw new TimeoutException($"Request '{command}' timed out after {baseTimeoutMs} ms");
        }
    }

    public Task SendRequestNoResponseAsync(string command, object? args)
    {
        // Fire and forget
        int seq;
        lock (_lock) seq = _sequence++;
        var req = new Request { Seq = seq, Command = command, Arguments = args };
        Logger.Verbose($"→ {command} #{seq} (no-response)");
        return SendMessageAsync(req, _cts.Token);
    }

    private async Task SendMessageAsync(ProtocolMessage message, CancellationToken ct)
    {
        var json = JsonSerializer.Serialize(message, message.GetType(), _jsonOptions);
        var bytes = Encoding.UTF8.GetBytes(json);
        var header = Encoding.UTF8.GetBytes($"Content-Length: {bytes.Length}\r\n\r\n");
        await _process.StandardInput.BaseStream.WriteAsync(header, 0, header.Length, ct);
        await _process.StandardInput.BaseStream.WriteAsync(bytes, 0, bytes.Length, ct);
        await _process.StandardInput.BaseStream.FlushAsync(ct);
    }

    private async Task ListenLoop()
    {
        try
        {
            while (!_cts.IsCancellationRequested)
            {
                var node = await ReceiveMessageAsync(_cts.Token);
                if (node == null) break;
                await HandleMessageAsync(node);
            }
        }
        catch (Exception ex)
        {
            Logger.Error($"DAP listen error: {ex.Message}");
        }
        finally
        {
            Logger.Info("DAP listen loop ended");
        }
    }

    private async Task<JsonNode?> ReceiveMessageAsync(CancellationToken ct)
    {
        // Read all headers until blank line
        var headers = new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);
        while (true)
        {
            var line = await _reader.ReadLineAsync(ct);
            if (line == null) return null; // stream closed
            if (line.Length == 0) break; // end of headers
            var idx = line.IndexOf(':');
            if (idx > 0)
            {
                var key = line.Substring(0, idx).Trim();
                var value = line.Substring(idx + 1).Trim();
                headers[key] = value;
            }
        }
        if (!headers.TryGetValue("Content-Length", out var lenStr) || !int.TryParse(lenStr, out var len) || len < 0)
        {
            Logger.Error("DAP missing/invalid Content-Length header");
            return null;
        }
        var buffer = new char[len];
        var readTotal = 0;
        while (readTotal < len)
        {
            var read = await _reader.ReadAsync(buffer, readTotal, len - readTotal);
            if (read == 0) throw new EndOfStreamException("Unexpected end of stream while reading body");
            readTotal += read;
        }
        var json = new string(buffer);
        if (Environment.GetEnvironmentVariable("CYCOD_DAP_TRACE") == "1") Logger.Verbose($"DAP RAW IN: {json}");
        _lastMessageTime = DateTime.UtcNow;
        return JsonNode.Parse(json);
    }

    private Task HandleMessageAsync(JsonNode node)
    {
        var type = node["type"]?.GetValue<string>();
        if (type == "response")
        {
            var resp = node.Deserialize<Response>(_jsonOptions);
            if (resp != null)
            {
                TaskCompletionSource<Response>? tcs = null;
                lock (_lock)
                {
                    if (_pendingRequests.TryGetValue(resp.RequestSeq, out tcs)) _pendingRequests.Remove(resp.RequestSeq);
                }
                tcs?.SetResult(resp);
            }
        }
        else if (type == "event")
        {
            var evt = node.Deserialize<Event>(_jsonOptions);
            if (evt != null)
            {
                _eventCallback(evt);
                EnqueueEvent(evt);
            }
        }
        return Task.CompletedTask;
    }

    
    

    public Task<Cycod.Debugging.Protocol.Event> WaitForEventAsync(string eventType, int timeoutMs)
    {
        var tcs = new TaskCompletionSource<Cycod.Debugging.Protocol.Event>(TaskCreationOptions.RunContinuationsAsynchronously);
        lock (_lock)
        {
            if (!_eventWaiters.TryGetValue(eventType, out var list))
            {
                list = new List<TaskCompletionSource<Cycod.Debugging.Protocol.Event>>();
                _eventWaiters[eventType] = list;
            }
            list.Add(tcs);
        }
        var cts = new CancellationTokenSource(timeoutMs);
        cts.Token.Register(() => tcs.TrySetException(new TimeoutException($"Event '{eventType}' not received within {timeoutMs} ms")));
        return tcs.Task;
    }

    private void EnqueueEvent(Cycod.Debugging.Protocol.Event evt)
    {
        lock (_lock)
        {
            var typeKey = evt.EventType ?? string.Empty;
            if (_eventWaiters.TryGetValue(typeKey, out var waiters) && waiters.Count > 0)
            {
                var first = waiters[0];
                waiters.RemoveAt(0);
                first.TrySetResult(evt);
                return;
            }
            if (!_eventQueue.TryGetValue(evt.EventType, out var q))
            {
                q = new Queue<Event>();
                _eventQueue[evt.EventType] = q;
            }
            q.Enqueue(new Event { EventType = evt.EventType, Body = evt.Body });
        }
    }

    public Cycod.Debugging.Protocol.Event? TryDequeueEvent(string eventType)
    {
        lock (_lock)
        {
            if (_eventQueue.TryGetValue(eventType, out var q) && q.Count > 0)
            {
                var e = q.Dequeue();
                return e;
            }
        }
        return null;
    }

    public void Dispose()
    {
        try { _cts.Cancel(); } catch { }
        try { _listenTask.Wait(TimeSpan.FromSeconds(2)); } catch { }
        try { if (!_process.HasExited) _process.Kill(); } catch { }
        _process.Dispose();
        _cts.Dispose();
    }
}
